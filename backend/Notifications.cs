using MySqlConnector;

public static class Notifications
{
    public record Item(string Id,string Kind,string Title,string Url,DateTime CreatedAt,bool Read);
    public record ReadRequest(string? Id);
    public static void Map(WebApplication app,Db db)
    {
        app.MapGet("/api/notifications",async(HttpContext context)=>{
            if(context.Items["account"] is not Account a)return Results.Json(new{message="Entre para continuar."},statusCode:401);
            return Results.Ok(await List(db,a));
        });
        app.MapPost("/api/notifications/read",async(ReadRequest input,HttpContext context)=>{
            if(context.Items["account"] is not Account a)return Results.Json(new{message="Entre para continuar."},statusCode:401);
            if(input.Id is not {Length: >0 and <=100})return Results.BadRequest(new{message="Notificação inválida."});
            if(!(await List(db,a)).Any(x=>x.Id==input.Id))return Results.NotFound(new{message="Notificação indisponível."});
            await db.Execute("INSERT IGNORE INTO notification_reads(user_id,event_key) VALUES(@me,@key)",("@me",a.Id),("@key",input.Id));
            return Results.Ok(new{message="Notificação lida."});
        }).RequireRateLimiting("social");
        app.MapPost("/api/notifications/read-all",async(HttpContext context)=>{
            if(context.Items["account"] is not Account a)return Results.Json(new{message="Entre para continuar."},statusCode:401);
            var items=await List(db,a);var ids=items.Select(x=>x.Id).ToArray();
            await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
            foreach(var id in items.Where(x=>!x.Read).Select(x=>x.Id)){await using var q=Db.Command(c,"INSERT IGNORE INTO notification_reads(user_id,event_key) VALUES(@me,@key)",("@me",a.Id),("@key",id));q.Transaction=tx;await q.ExecuteNonQueryAsync();}
            await tx.CommitAsync();return Results.Ok(new{ids,message="Notificações marcadas como lidas."});
        }).RequireRateLimiting("social");
    }
    static async Task<List<Item>> List(Db db,Account a)
    {
        var rows=new List<Item>();var read=new HashSet<string>();
        await using var c=await db.Open();
        await using(var q=Db.Command(c,"SELECT event_key FROM notification_reads WHERE user_id=@me",("@me",a.Id)))
        {await using var r=await q.ExecuteReaderAsync();while(await r.ReadAsync())read.Add(r.GetString(0));}
        async Task Query(string sql)
        {
            await using var q=Db.Command(c,sql,("@me",a.Id));await using var r=await q.ExecuteReaderAsync();
            while(await r.ReadAsync()){var id=r.GetString(0);rows.Add(new(id,r.GetString(1),r.GetString(2),r.GetString(3),r.GetDateTime(4),read.Contains(id)));}
        }
        await Query(@"SELECT CONCAT('friend:',f.id,':',f.status,':',DATE_FORMAT(f.updated_at,'%Y%m%d%H%i%s')),'friend',
          CONCAT(u.name,CASE WHEN f.status='pending' THEN ' enviou um pedido de amizade' ELSE ' aceitou seu pedido de amizade' END),
          CONCAT('/amigos/index.html?aba=',IF(f.status='pending','pending','accepted'),'&pessoa=',u.id),f.updated_at
          FROM friendships f JOIN users u ON u.id=IF(f.low_user=@me,f.high_user,f.low_user)
          WHERE (f.low_user=@me OR f.high_user=@me) AND u.status='active'
          AND ((f.status='pending' AND f.requested_by<>@me) OR (f.status='accepted' AND f.requested_by=@me))
          ORDER BY f.updated_at DESC LIMIT 50");
        await Query(@"SELECT CONCAT('message:',m.id),'message',CONCAT(u.name,' enviou uma mensagem'),
          CONCAT('/mensagens/index.html?amigo=',u.id,'&mensagem=',m.id),m.sent_at
          FROM messages m JOIN friendships f ON f.id=m.friendship_id JOIN users u ON u.id=m.sender_id
          WHERE (f.low_user=@me OR f.high_user=@me) AND f.status='accepted' AND m.sender_id<>@me AND u.status='active'
          ORDER BY m.id DESC LIMIT 50");
        await Query(@"SELECT CONCAT('peer:',f.id),'feedback',CONCAT(u.name,' comentou no exercício ',s.exercise_number),
          CONCAT('/perfil/index.html?usuario=',s.user_id,'&aba=submissions&entrega=',s.id),f.created_at
          FROM peer_feedback f JOIN submissions s ON s.id=f.submission_id JOIN users u ON u.id=f.author_id
          WHERE s.user_id=@me AND f.author_id<>@me AND u.status='active' ORDER BY f.id DESC LIMIT 50");
        if(a.Role=="admin"){
            await Query(@"SELECT CONCAT('password:',p.id,':pending'),'password',CONCAT(u.name,' solicitou troca de senha'),
              CONCAT('/alunos/index.html?aba=requests&item=',p.id),p.created_at FROM password_requests p JOIN users u ON u.id=p.user_id
              WHERE p.status='pending' AND u.status='active' ORDER BY p.created_at DESC LIMIT 50");
            await Query(@"SELECT CONCAT('submission:',s.id),'submission',CONCAT(u.name,' enviou o exercício ',s.exercise_number),
              CONCAT('/alunos/index.html?aba=submissions&item=',s.id),s.created_at FROM submissions s JOIN users u ON u.id=s.user_id
              JOIN user_settings p ON p.user_id=u.id WHERE p.share_with_teacher=TRUE AND u.status='active'
              ORDER BY s.created_at DESC LIMIT 50");
        }else{
            await Query(@"SELECT CONCAT('feedback:',s.id,':',DATE_FORMAT(s.feedback_at,'%Y%m%d%H%i%s')),'feedback',
              CONCAT('Feedback no exercício ',s.exercise_number),CONCAT('/entregas/index.html?entrega=',s.id),s.feedback_at
              FROM submissions s WHERE s.user_id=@me AND s.feedback_at IS NOT NULL ORDER BY s.feedback_at DESC LIMIT 50");
            await Query(@"SELECT CONCAT('password:',p.id,':',p.status),'password',
              CASE p.status WHEN 'approved' THEN 'Troca de senha autorizada. Peça o código ao professor.' WHEN 'denied' THEN 'Pedido de troca de senha recusado' ELSE 'Pedido de troca de senha recebido' END,
              '/login/index.html?recuperar=1',p.created_at FROM password_requests p
              WHERE p.user_id=@me AND (p.status IN ('pending','denied') OR (p.status='approved' AND p.expires_at>UTC_TIMESTAMP()))
              ORDER BY p.created_at DESC LIMIT 50");
        }
        return rows.OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).Take(100).ToList();
    }
}
