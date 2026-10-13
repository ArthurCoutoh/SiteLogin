using MySqlConnector;

public static class Social
{
    static Account? Current(HttpContext c)=>c.Items["account"] as Account;
    static IResult Error(string message,int status=400)=>Results.Json(new{message},statusCode:status);
    static async Task<bool> Terms(Db db,Account a)
    {
        if(a.Role=="admin")return true;
        await using var c=await db.Open();await using var q=Db.Command(c,"SELECT terms_version FROM user_settings WHERE user_id=@id",("@id",a.Id));return (await q.ExecuteScalarAsync()) as string==Portal.TermsVersion;
    }
    static bool Avatar(string? value)
    {
        if(value==null)return true;if(value.Length>110000)return false;
        var comma=value.IndexOf(',');if(comma<0)return false;var prefix=value[..comma];byte[] bytes;
        try{bytes=Convert.FromBase64String(value[(comma+1)..]);}catch(FormatException){return false;}
        if(bytes.Length<12||bytes.Length>80000)return false;
        return prefix switch {
            "data:image/jpeg;base64"=>bytes[0]==255&&bytes[1]==216&&bytes[2]==255,
            "data:image/png;base64"=>bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),
            "data:image/webp;base64"=>System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP",
            _=>false
        };
    }
    static async Task<(long Id,string Status,long RequestedBy,long? BlockedBy,DateTime Updated)?> Friendship(MySqlConnection c,long a,long b,MySqlTransaction? tx=null)
    {
        await using var q=Db.Command(c,"SELECT id,status,requested_by,blocked_by,updated_at FROM friendships WHERE low_user=@low AND high_user=@high"+(tx!=null?" FOR UPDATE":""),("@low",Math.Min(a,b)),("@high",Math.Max(a,b)));q.Transaction=tx;
        await using var r=await q.ExecuteReaderAsync();return await r.ReadAsync()?(r.GetInt64(0),r.GetString(1),r.GetInt64(2),r.IsDBNull(3)?(long?)null:r.GetInt64(3),r.GetDateTime(4)):null;
    }
    static async Task<bool> LockPair(MySqlConnection c,MySqlTransaction tx,long a,long b)
    {
        if(a==b||b<=0)return false;
        await using var q=Db.Command(c,"SELECT id FROM users WHERE id IN (@a,@b) AND status='active' ORDER BY id FOR UPDATE",("@a",a),("@b",b));q.Transaction=tx;await using var r=await q.ExecuteReaderAsync();var count=0;while(await r.ReadAsync())count++;return count==2;
    }
    static async Task Execute(MySqlConnection c,MySqlTransaction tx,string sql,params (string Key,object? Value)[] args)
    {await using var q=Db.Command(c,sql,args);q.Transaction=tx;await q.ExecuteNonQueryAsync();}
    public static void Map(WebApplication app,Db db)
    {
        app.MapPost("/api/profile",async(ProfileRequest input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            var name=input.Name?.Trim();var bio=input.Biography?.Trim()??"";
            if(string.IsNullOrWhiteSpace(name)||name.Length>100||name.Any(char.IsControl)||bio.Length>600||!Avatar(input.Avatar))return Error("Use nome de até 100 caracteres, biografia de até 600 e foto JPEG, PNG ou WebP de até 80 KB.");
            if(!input.AcceptTerms&&!await Terms(db,a))return Error("Aceite os termos atualizados antes de salvar seu perfil.",403);
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
            await using var active=Db.Command(c,"SELECT status FROM users WHERE id=@id FOR UPDATE",("@id",a.Id));active.Transaction=tx;if((await active.ExecuteScalarAsync()) as string!="active")return Error("Conta indisponível.",403);
            await Execute(c,tx,"UPDATE users SET name=@name WHERE id=@id",("@name",name),("@id",a.Id));
            await Execute(c,tx,"INSERT INTO user_profiles(user_id,biography,avatar,discoverable) VALUES(@id,@bio,@avatar,@visible) ON DUPLICATE KEY UPDATE biography=@bio,avatar=CASE WHEN @remove THEN NULL ELSE COALESCE(@avatar,avatar) END,discoverable=@visible",("@id",a.Id),("@bio",bio),("@avatar",input.RemoveAvatar?null:input.Avatar),("@visible",input.Discoverable),("@remove",input.RemoveAvatar));
            if(input.AcceptTerms)await Execute(c,tx,"INSERT INTO user_settings(user_id,terms_version,accepted_at) VALUES(@id,@terms,UTC_TIMESTAMP()) ON DUPLICATE KEY UPDATE terms_version=@terms,accepted_at=UTC_TIMESTAMP()",("@id",a.Id),("@terms",Portal.TermsVersion));
            await tx.CommitAsync();return Results.Ok(new{message="Perfil atualizado."});
        }).RequireRateLimiting("social");
        app.MapGet("/api/people",async(string? q,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            q=q?.Trim();if(q is not {Length: >=2 and <=100})return Error("Busque por pelo menos dois caracteres do nome.");
            var rows=new List<object>();await using var c=await db.Open();await using var cmd=Db.Command(c,"SELECT u.id,u.name,p.avatar FROM users u JOIN user_profiles p ON p.user_id=u.id WHERE u.status='active' AND p.discoverable=TRUE AND u.id<>@me AND LOCATE(@query,u.name)>0 AND NOT EXISTS(SELECT 1 FROM friendships f WHERE f.low_user=LEAST(u.id,@me) AND f.high_user=GREATEST(u.id,@me) AND f.status='blocked') ORDER BY u.name,u.id LIMIT 20",("@me",a.Id),("@query",q));await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add(new{userId=r.GetInt64(0),name=r.GetString(1),hasAvatar=!r.IsDBNull(2)});return Results.Ok(rows);
        }).RequireRateLimiting("social");
        app.MapGet("/api/avatars/{id:long}",async(long id,HttpContext context)=>{
            var a=Current(context);if(a==null)return Results.StatusCode(401);
            await using var c=await db.Open();await using var q=Db.Command(c,"SELECT p.avatar FROM user_profiles p JOIN users u ON u.id=p.user_id WHERE p.user_id=@id AND u.status='active' AND (@id=@me OR p.discoverable=TRUE OR EXISTS(SELECT 1 FROM friendships f WHERE f.low_user=LEAST(@id,@me) AND f.high_user=GREATEST(@id,@me) AND f.status='accepted')) AND (@id=@me OR NOT EXISTS(SELECT 1 FROM friendships b WHERE b.low_user=LEAST(@id,@me) AND b.high_user=GREATEST(@id,@me) AND b.status='blocked'))",("@id",id),("@me",a.Id));var data=await q.ExecuteScalarAsync() as string;if(data==null||!Avatar(data))return Results.NotFound();var comma=data.IndexOf(',');return Results.File(Convert.FromBase64String(data[(comma+1)..]),data[5..data.IndexOf(';')]);
        });
        app.MapGet("/api/friends",async(HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            var rows=new List<object>();await using var c=await db.Open();await using var q=Db.Command(c,"SELECT f.id,u.id,u.name,p.biography,p.avatar,f.status,f.requested_by,f.blocked_by FROM friendships f JOIN users u ON u.id=CASE WHEN f.low_user=@me THEN f.high_user ELSE f.low_user END LEFT JOIN user_profiles p ON p.user_id=u.id WHERE (f.low_user=@me OR f.high_user=@me) AND u.status='active' AND (f.status IN ('pending','accepted') OR (f.status='blocked' AND f.blocked_by=@me)) ORDER BY f.updated_at DESC",("@me",a.Id));await using var r=await q.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add(new{friendshipId=r.GetInt64(0),userId=r.GetInt64(1),name=r.GetString(2),biography=r.GetString(5)=="accepted"&&!r.IsDBNull(3)?r.GetString(3):"",hasAvatar=!r.IsDBNull(4),status=r.GetString(5),direction=r.GetInt64(6)==a.Id?"sent":"received"});return Results.Ok(rows);
        });
        app.MapPost("/api/friends/{id:long}/request",async(long id,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);if(!await Terms(db,a))return Error("Aceite os termos atualizados nas preferências.",403);
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();if(!await LockPair(c,tx,a.Id,id))return Error("Pessoa indisponível.",404);
            var f=await Friendship(c,a.Id,id,tx);if(f?.Status is "blocked" or "accepted" or "pending")return Error("Pedido indisponível ou amizade já existente.",409);
            if(f?.Status=="declined"&&f?.Updated>DateTime.UtcNow.AddHours(-24))return Error("Aguarde 24 horas para enviar outro pedido.",409);
            await using var visible=Db.Command(c,"SELECT discoverable FROM user_profiles WHERE user_id=@id",("@id",id));visible.Transaction=tx;if(Convert.ToInt32(await visible.ExecuteScalarAsync())!=1)return Error("Pessoa indisponível.",404);
            await Execute(c,tx,"INSERT INTO friendships(low_user,high_user,requested_by) VALUES(@low,@high,@me) ON DUPLICATE KEY UPDATE status='pending',requested_by=@me,blocked_by=NULL,updated_at=UTC_TIMESTAMP()",("@low",Math.Min(a.Id,id)),("@high",Math.Max(a.Id,id)),("@me",a.Id));await tx.CommitAsync();return Results.Ok(new{message="Pedido de amizade enviado."});
        }).RequireRateLimiting("social");
        app.MapPost("/api/friends/{id:long}/respond",async(long id,FriendResponse input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);if(input.Decision is not ("accepted" or "declined"))return Error("Escolha aceitar ou recusar.");if(!await Terms(db,a))return Error("Aceite os termos atualizados nas preferências.",403);
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();if(!await LockPair(c,tx,a.Id,id))return Error("Pessoa indisponível.",404);var f=await Friendship(c,a.Id,id,tx);
            if(f?.Status!="pending"||f?.RequestedBy==a.Id)return Error("Pedido não encontrado.",404);
            await Execute(c,tx,"UPDATE friendships SET status=@status,updated_at=UTC_TIMESTAMP() WHERE id=@id",("@status",input.Decision),("@id",f.Value.Id));await tx.CommitAsync();return Results.Ok(new{message=input.Decision=="accepted"?"Amizade aceita. Vocês já podem conversar.":"Pedido recusado."});
        }).RequireRateLimiting("social");
        app.MapPost("/api/friends/{id:long}/manage",async(long id,FriendManage input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);if(input.Action is not ("remove" or "block" or "unblock"))return Error("Ação inválida.");
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();if(!await LockPair(c,tx,a.Id,id))return Error("Pessoa indisponível.",404);var f=await Friendship(c,a.Id,id,tx);
            if(f==null)return Error("Relacionamento não encontrado.",404);
            if(f.Value.Status=="blocked"&&(input.Action!="unblock"||f.Value.BlockedBy!=a.Id))return Error("Ação indisponível.",403);
            if(input.Action=="unblock"&&(f.Value.Status!="blocked"||f.Value.BlockedBy!=a.Id))return Error("Ação indisponível.",403);
            await Execute(c,tx,"UPDATE friendships SET status=@status,blocked_by=@blocked,updated_at=UTC_TIMESTAMP() WHERE id=@id",("@status",input.Action=="block"?"blocked":"removed"),("@blocked",input.Action=="block"?a.Id:null),("@id",f.Value.Id));await tx.CommitAsync();return Results.Ok(new{message="Preferência de amizade atualizada."});
        }).RequireRateLimiting("social");
        app.MapGet("/api/friends/{id:long}/messages",async(long id,long? before,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
            await using var c=await db.Open();await using var active=Db.Command(c,"SELECT COUNT(*) FROM users WHERE id=@id AND status='active'",("@id",id));if(Convert.ToInt32(await active.ExecuteScalarAsync())!=1)return Error("Conversa indisponível.",404);
            var f=await Friendship(c,a.Id,id);if(f?.Status!="accepted")return Error("Conversa indisponível. É necessário aceitar a amizade.",403);
            var rows=new List<object>();await using var q=Db.Command(c,"SELECT id,sender_id,content,sent_at FROM messages WHERE friendship_id=@id AND id<@before ORDER BY id DESC LIMIT 100",("@id",f.Value.Id),("@before",before is >0?before:long.MaxValue));await using var r=await q.ExecuteReaderAsync();while(await r.ReadAsync())rows.Add(new{id=r.GetInt64(0),senderId=r.GetInt64(1),content=r.GetString(2),sentAt=r.GetDateTime(3)});rows.Reverse();return Results.Ok(rows);
        });
        app.MapPost("/api/friends/{id:long}/messages",async(long id,MessageRequest input,HttpContext context)=>{
            var a=Current(context);if(a==null)return Error("Entre para continuar.",401);var text=input.Content?.Trim();if(string.IsNullOrWhiteSpace(text)||text.Length>2000)return Error("Escreva uma mensagem de até 2.000 caracteres.");if(!await Terms(db,a))return Error("Aceite os termos atualizados nas preferências.",403);
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();if(!await LockPair(c,tx,a.Id,id))return Error("Pessoa indisponível.",404);var f=await Friendship(c,a.Id,id,tx);if(f?.Status!="accepted")return Error("Conversa indisponível. É necessário aceitar a amizade.",403);
            await Execute(c,tx,"INSERT INTO messages(friendship_id,sender_id,content) VALUES(@id,@sender,@text)",("@id",f.Value.Id),("@sender",a.Id),("@text",text));await tx.CommitAsync();return Results.Ok(new{message="Mensagem enviada."});
        }).RequireRateLimiting("social");
    }
}
public record ProfileRequest(string? Name,string? Biography,string? Avatar=null,bool RemoveAvatar=false,bool Discoverable=false,bool AcceptTerms=false);
public record FriendResponse(string? Decision);
public record FriendManage(string? Action);
public record MessageRequest(string? Content);
