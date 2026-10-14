using MySqlConnector;
public static class Sharing
{
    public record FeedbackInput(string? Content);
    static Account? Current(HttpContext c)=>c.Items["account"] as Account;
    static IResult Error(string text,int status=403)=>Results.Json(new{message=text},statusCode:status);
    static async Task<bool> Allowed(MySqlConnection c,Account viewer,long owner){
        if(viewer.Id==owner)return true;
        await using var q=Db.Command(c,viewer.Role=="admin"?
            "SELECT COUNT(*) FROM user_settings WHERE user_id=@id AND share_with_teacher=TRUE":
            "SELECT COUNT(*) FROM student_visibility WHERE user_id=@id AND share_with_students=TRUE",("@id",owner));
        return Convert.ToInt32(await q.ExecuteScalarAsync())==1;
    }
    public static void Map(WebApplication app,Db db){
        app.MapGet("/api/people/{id:long}/profile",async(long id,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            await using var c=await db.Open();await using var q=Db.Command(c,@"SELECT u.name,p.biography,p.avatar FROM users u LEFT JOIN user_profiles p ON p.user_id=u.id WHERE u.id=@id AND u.status='active' AND
              (u.id=@me OR p.discoverable=TRUE OR EXISTS(SELECT 1 FROM friendships f WHERE f.low_user=LEAST(@me,@id) AND f.high_user=GREATEST(@me,@id) AND f.status='accepted') OR @teacher=TRUE OR EXISTS(SELECT 1 FROM student_visibility v WHERE v.user_id=@id AND v.share_with_students=TRUE))
              AND (u.id=@me OR NOT EXISTS(SELECT 1 FROM friendships f WHERE f.low_user=LEAST(@me,@id) AND f.high_user=GREATEST(@me,@id) AND f.status='blocked'))",("@id",id),("@me",a.Id),("@teacher",a.Role=="admin"));
            string name,bio;bool hasAvatar;
            await using(var r=await q.ExecuteReaderAsync()){if(!await r.ReadAsync())return Error("Perfil indisponível.",404);name=r.GetString(0);bio=r.IsDBNull(1)?"":r.GetString(1);hasAvatar=!r.IsDBNull(2);}
            var allowed=await Allowed(c,a,id);var submissions=new List<object>();
            if(allowed){
                await using var s=Db.Command(c,"SELECT id,exercise_number,title,solution,feedback,feedback_at,created_at FROM submissions WHERE user_id=@id ORDER BY created_at DESC,id DESC",("@id",id));
                await using(var r=await s.ExecuteReaderAsync()){while(await r.ReadAsync())submissions.Add(new{id=r.GetInt64(0),exerciseNumber=r.GetInt32(1),title=r.GetString(2),solution=r.GetString(3),teacherFeedback=r.IsDBNull(4)?null:r.GetString(4),feedbackAt=r.IsDBNull(5)?(DateTime?)null:r.GetDateTime(5),createdAt=r.GetDateTime(6)});}
            }
            var comments=new List<object>();
            if(allowed){await using var f=Db.Command(c,@"SELECT f.id,f.submission_id,f.content,f.created_at,u.name,u.id FROM peer_feedback f JOIN submissions s ON s.id=f.submission_id JOIN users u ON u.id=f.author_id WHERE s.user_id=@id AND u.status='active' ORDER BY f.id",("@id",id));await using var r=await f.ExecuteReaderAsync();while(await r.ReadAsync())comments.Add(new{id=r.GetInt64(0),submissionId=r.GetInt64(1),content=r.GetString(2),createdAt=r.GetDateTime(3),author=r.GetString(4),authorId=r.GetInt64(5)});}
            return Results.Ok(new{id,name,biography=bio,hasAvatar,canViewSubmissions=allowed,canComment=allowed&&a.Role=="student"&&a.Id!=id,canTeacherFeedback=allowed&&a.Role=="admin"&&a.Id!=id,submissions,comments});
        });
        app.MapPost("/api/submissions/{id:long}/comments",async(long id,FeedbackInput input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);if(a.Role!="student")return Error("Use o feedback do professor no painel.");
            var text=input.Content?.Trim();if(string.IsNullOrWhiteSpace(text)||text.Length>5000)return Error("Escreva um feedback de até 5.000 caracteres.",400);
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
            await using var q=Db.Command(c,@"SELECT s.user_id FROM submissions s JOIN users u ON u.id=s.user_id JOIN student_visibility v ON v.user_id=u.id
              WHERE s.id=@id AND u.status='active' AND v.share_with_students=TRUE AND s.user_id<>@me
              AND NOT EXISTS(SELECT 1 FROM friendships f WHERE f.low_user=LEAST(s.user_id,@me) AND f.high_user=GREATEST(s.user_id,@me) AND f.status='blocked') FOR UPDATE",("@id",id),("@me",a.Id));q.Transaction=tx;
            var owner=await q.ExecuteScalarAsync();if(owner==null)return Error("Entrega indisponível ou acesso revogado.",404);
            await using var terms=Db.Command(c,"SELECT COUNT(*) FROM user_settings WHERE user_id=@me AND terms_version=@terms",("@me",a.Id),("@terms",Portal.TermsVersion));terms.Transaction=tx;if(Convert.ToInt32(await terms.ExecuteScalarAsync())!=1)return Error("Aceite os termos atualizados nas preferências.");
            await using var insert=Db.Command(c,"INSERT INTO peer_feedback(submission_id,author_id,content) VALUES(@id,@me,@text)",("@id",id),("@me",a.Id),("@text",text));insert.Transaction=tx;await insert.ExecuteNonQueryAsync();await tx.CommitAsync();
            return Results.Ok(new{message="Feedback enviado."});
        }).RequireRateLimiting("social");
    }
}
