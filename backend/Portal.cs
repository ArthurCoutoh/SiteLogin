using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using MySqlConnector;

public static class Portal
{
    public const string TermsVersion="2026-10-13";
    static Account? Current(HttpContext c)=>c.Items["account"] as Account;
    static IResult Error(string message,int status=400)=>Results.Json(new{message},statusCode:status);
    static async Task<int> Execute(MySqlConnection c,MySqlTransaction tx,string sql,params (string Key,object? Value)[] args)
    { await using var q=Db.Command(c,sql,args);q.Transaction=tx;return await q.ExecuteNonQueryAsync(); }
    static async Task<Account?> LockUser(MySqlConnection c,MySqlTransaction tx,string email)
    { await using var q=Db.Command(c,"SELECT * FROM users WHERE email=@email FOR UPDATE",("@email",email));q.Transaction=tx;await using var r=await q.ExecuteReaderAsync();return await r.ReadAsync()?Db.Read(r):null; }
    public static void Map(WebApplication app,Db db,IPasswordHasher<Account> hasher)
    {
        app.MapGet("/api/profile",async(HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            await using var c=await db.Open();await using var q=Db.Command(c,"SELECT share_with_teacher,terms_version FROM user_settings WHERE user_id=@id",("@id",a.Id));await using var r=await q.ExecuteReaderAsync();
            var found=await r.ReadAsync();return Results.Ok(new{name=a.Name,email=a.Email,shareWithTeacher=found&&r.GetBoolean(0),acceptedTerms=found&&!r.IsDBNull(1)&&r.GetString(1)==TermsVersion});
        });
        app.MapPost("/api/profile/privacy",async(PrivacyRequest input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            await db.Execute("INSERT INTO user_settings(user_id,share_with_teacher,terms_version,accepted_at) VALUES(@id,@share,@terms,@at) ON DUPLICATE KEY UPDATE share_with_teacher=@share,terms_version=COALESCE(@terms,terms_version),accepted_at=COALESCE(@at,accepted_at)",("@id",a.Id),("@share",input.ShareWithTeacher),("@terms",input.AcceptTerms?TermsVersion:null),("@at",input.AcceptTerms?DateTime.UtcNow:null));
            return Results.Ok(new{message="Preferências salvas. O professor só pode consultar suas entregas enquanto você permitir."});
        });
        app.MapGet("/api/submissions",async(HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            return await ReadSubmissions(db,false,a.Id);
        });
        app.MapPost("/api/submissions",async(SubmissionRequest input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            var title=input.Title?.Trim();
            if(input.ExerciseNumber<1||input.ExerciseNumber>30||string.IsNullOrWhiteSpace(title)||title.Length>150||string.IsNullOrWhiteSpace(input.Solution)||input.Solution.Length>20000)return Error("Escolha um exercício de 1 a 30, título de até 150 caracteres e código de até 20.000 caracteres.");
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
            // Lock the user before settings so account bans and privacy changes remain consistent.
            var active=await LockUser(c,tx,a.Email);if(active?.Status!="active")return Error("Sua conta está bloqueada.",403);
            await using var terms=Db.Command(c,"SELECT terms_version FROM user_settings WHERE user_id=@id FOR UPDATE",("@id",a.Id));terms.Transaction=tx;
            if((await terms.ExecuteScalarAsync()) as string!=TermsVersion)return Error("Aceite os termos em Privacidade antes de enviar.",403);
            await using var q=Db.Command(c,"INSERT INTO submissions(user_id,exercise_number,title,solution) VALUES(@id,@n,@title,@code)",("@id",a.Id),("@n",input.ExerciseNumber),("@title",title),("@code",input.Solution));q.Transaction=tx;await q.ExecuteNonQueryAsync();var id=q.LastInsertedId;await tx.CommitAsync();
            return Results.Ok(new{id,message="Entrega salva. Para receber feedback, permita o acesso do professor em Privacidade."});
        });
        app.MapPost("/api/submissions/{id:long}/delete",async(long id,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            var changed=await db.Execute("DELETE FROM submissions WHERE id=@id AND user_id=@user",("@id",id),("@user",a.Id));return changed==0?Error("Entrega não encontrada.",404):Results.Ok(new{message="Entrega excluída."});
        });
        app.MapGet("/api/teacher/submissions",async(HttpContext context)=>Current(context)?.Role!="admin"?Error("Acesso restrito ao professor.",403):await ReadSubmissions(db,true,0));
        app.MapPost("/api/teacher/submissions/{id:long}/feedback",async(long id,FeedbackRequest input,HttpContext context)=>{
            if(Current(context)?.Role!="admin")return Error("Acesso restrito ao professor.",403);
            var text=input.Feedback?.Trim();if(string.IsNullOrWhiteSpace(text)||text.Length>5000)return Error("Escreva um feedback de até 5.000 caracteres.");
            var updated=await db.Execute("UPDATE submissions s JOIN users u ON u.id=s.user_id JOIN user_settings p ON p.user_id=u.id SET s.feedback=@text,s.feedback_at=UTC_TIMESTAMP() WHERE s.id=@id AND p.share_with_teacher=TRUE AND u.status='active'",("@text",text),("@id",id));
            return updated==0?Error("Entrega indisponível ou acesso não permitido pelo aluno.",404):Results.Ok(new{message="Feedback enviado."});
        });
        app.MapPost("/api/auth/password-request",async(ResetRequest input)=>{
            var email=Input.Email(input.Email);if(email==null)return Error("Informe um e-mail válido.");
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();var a=await LockUser(c,tx,email);
            if(a?.Role=="student"&&a.Status=="active"){
                await using var q=Db.Command(c,"SELECT COUNT(*) FROM password_requests WHERE user_id=@id AND (status='pending' OR (status='approved' AND expires_at>UTC_TIMESTAMP()))",("@id",a.Id));q.Transaction=tx;
                if(Convert.ToInt64(await q.ExecuteScalarAsync())==0)await Execute(c,tx,"INSERT INTO password_requests(user_id) VALUES(@id)",("@id",a.Id));
            }
            await tx.CommitAsync();return Results.Ok(new{message="Se houver uma conta elegível, o professor receberá sua solicitação. Fale com ele para confirmar sua identidade e obter o código de autorização."});
        }).RequireRateLimiting("auth");
        app.MapGet("/api/admin/password-requests",async(HttpContext context)=>{
            if(Current(context)?.Role!="admin")return Error("Acesso restrito ao professor.",403);
            var rows=new List<object>();await using var c=await db.Open();await using var q=Db.Command(c,"SELECT p.id,u.name,u.email,p.status,p.expires_at,p.created_at FROM password_requests p JOIN users u ON p.user_id=u.id WHERE u.status='active' AND (p.status='pending' OR p.status='approved') ORDER BY p.created_at DESC LIMIT 200");await using var r=await q.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add(new{id=r.GetInt64(0),name=r.GetString(1),email=r.GetString(2),status=r.GetString(3),expiresAt=r.IsDBNull(4)?(DateTime?)null:r.GetDateTime(4),createdAt=r.GetDateTime(5)});return Results.Ok(rows);
        });
        app.MapPost("/api/admin/password-requests/{id:long}/decision",async(long id,DecisionRequest input,HttpContext context)=>{
            if(Current(context)?.Role!="admin")return Error("Acesso restrito ao professor.",403);
            if(input.Decision is not ("approved" or "denied"))return Error("Decisão inválida.");
            // Resolve email first, then follow the same user -> request lock order as reset.
            await using var c=await db.Open();await using var find=Db.Command(c,"SELECT u.email FROM password_requests p JOIN users u ON u.id=p.user_id WHERE p.id=@id",("@id",id));var email=(await find.ExecuteScalarAsync()) as string;if(email==null)return Error("Solicitação não encontrada.",404);
            await using var tx=await c.BeginTransactionAsync();var a=await LockUser(c,tx,email);if(a?.Role!="student"||a.Status!="active")return Error("Conta indisponível.",404);
            var code=input.Decision=="approved"?Convert.ToHexString(RandomNumberGenerator.GetBytes(16)):null;
            var n=await Execute(c,tx,"UPDATE password_requests SET status=@status,token_hash=@hash,expires_at=@expiry WHERE id=@id AND status IN ('pending','approved')",("@status",input.Decision),("@hash",code==null?null:Input.Hash(code)),("@expiry",code==null?null:DateTime.UtcNow.AddMinutes(30)),("@id",id));
            if(n==0)return Error("Solicitação já encerrada.",404);await tx.CommitAsync();
            return Results.Ok(new{message=code==null?"Solicitação recusada.":"Autorização válida por 30 minutos. Confirme a identidade do aluno antes de entregar o código. O código aparece somente agora.",code});
        });
        app.MapPost("/api/auth/password-reset",async(ResetPasswordRequest input)=>{
            var email=Input.Email(input.Email);var code=input.Code?.Trim().ToUpperInvariant();
            if(email==null||code is not {Length:32}||!code.All(Uri.IsHexDigit)||!Input.Password(input.NewPassword))return Error("Informe e-mail, código de autorização e nova senha de 12 a 128 caracteres.");
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();var a=await LockUser(c,tx,email);
            if(a?.Role!="student"||a.Status!="active")return Error("Código inválido ou expirado.");
            await using var q=Db.Command(c,"SELECT id FROM password_requests WHERE user_id=@user AND token_hash=@hash AND status='approved' AND expires_at>UTC_TIMESTAMP() FOR UPDATE",("@user",a.Id),("@hash",Input.Hash(code)));q.Transaction=tx;var id=await q.ExecuteScalarAsync();if(id==null)return Error("Código inválido ou expirado.");
            await Execute(c,tx,"UPDATE users SET password_hash=@hash,failed_attempts=0,locked_until=NULL WHERE id=@id",("@hash",hasher.HashPassword(a,input.NewPassword!)),("@id",a.Id));
            await Execute(c,tx,"UPDATE password_requests SET status=CASE WHEN id=@request THEN 'completed' ELSE 'denied' END,token_hash=NULL,expires_at=NULL WHERE user_id=@user AND status IN ('approved','pending')",("@request",id),("@user",a.Id));
            await Execute(c,tx,"DELETE FROM sessions WHERE user_id=@id",("@id",a.Id));await tx.CommitAsync();return Results.Ok(new{message="Senha atualizada. Entre novamente com sua nova senha."});
        }).RequireRateLimiting("auth");
    }
    static async Task<IResult> ReadSubmissions(Db db,bool teacher,long user)
    {
        var rows=new List<object>();await using var c=await db.Open();
        var sql="SELECT s.id,s.exercise_number,s.title,s.solution,s.feedback,s.feedback_at,s.created_at,u.name,u.email FROM submissions s JOIN users u ON u.id=s.user_id "+(teacher?"JOIN user_settings p ON p.user_id=u.id WHERE p.share_with_teacher=TRUE AND u.status='active'":"WHERE s.user_id=@user")+" ORDER BY s.created_at DESC,s.id DESC LIMIT 200";
        await using var q=Db.Command(c,sql,("@user",user));await using var r=await q.ExecuteReaderAsync();
        while(await r.ReadAsync())rows.Add(new{id=r.GetInt64(0),exerciseNumber=r.GetInt32(1),title=r.GetString(2),solution=r.GetString(3),feedback=r.IsDBNull(4)?null:r.GetString(4),feedbackAt=r.IsDBNull(5)?(DateTime?)null:r.GetDateTime(5),createdAt=r.GetDateTime(6),name=r.GetString(7),email=r.GetString(8)});
        return Results.Ok(rows);
    }
}
public record PrivacyRequest(bool ShareWithTeacher,bool AcceptTerms=false);
public record SubmissionRequest(int ExerciseNumber,string? Title,string? Solution);
public record FeedbackRequest(string? Feedback);
public record ResetRequest(string? Email);
public record DecisionRequest(string? Decision);
public record ResetPasswordRequest(string? Email,string? Code,string? NewPassword);
