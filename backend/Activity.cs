using MySqlConnector;
public static class Activity
{
    public const int PresenceSeconds=90;
    static Account? Current(HttpContext c)=>c.Items["account"] as Account;
    static string? Key(HttpContext c)=>Current(c)!=null&&c.Request.Cookies.TryGetValue("portfolio.session",out var token)&&token.Length==64?Input.Hash(token):null;
    static DateTime Utc(DateTime value)=>DateTime.SpecifyKind(value,DateTimeKind.Utc);
    static DateTime? Date(MySqlDataReader r,string field)=>r.IsDBNull(r.GetOrdinal(field))?null:Utc(r.GetDateTime(field));
    public static async Task Login(MySqlConnection c,MySqlTransaction tx,long user,string key){
        await using var q=Db.Command(c,"INSERT INTO activity_sessions(user_id,session_key,login_at,observed_at,last_seen_at) VALUES(@id,@key,UTC_TIMESTAMP(),UTC_TIMESTAMP(),UTC_TIMESTAMP())",("@id",user),("@key",key));q.Transaction=tx;await q.ExecuteNonQueryAsync();var id=q.LastInsertedId;
        await using var e=Db.Command(c,"INSERT INTO activity_events(user_id,session_id,event_type) VALUES(@user,@session,'login')",("@user",user),("@session",id));e.Transaction=tx;await e.ExecuteNonQueryAsync();
    }
    public static async Task Touch(Db db,HttpContext context,string? page=null){
        var a=Current(context);var key=Key(context);if(a==null||key==null)return;
        await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
        await using var q=Db.Command(c,@"INSERT INTO activity_sessions(user_id,session_key,observed_at,last_seen_at) VALUES(@id,@key,UTC_TIMESTAMP(),UTC_TIMESTAMP())
          ON DUPLICATE KEY UPDATE online_seconds=online_seconds+IF(logout_at IS NULL AND TIMESTAMPDIFF(SECOND,last_seen_at,UTC_TIMESTAMP()) BETWEEN 0 AND 90,TIMESTAMPDIFF(SECOND,last_seen_at,UTC_TIMESTAMP()),0),last_seen_at=IF(logout_at IS NULL,UTC_TIMESTAMP(),last_seen_at)",("@id",a.Id),("@key",key));q.Transaction=tx;await q.ExecuteNonQueryAsync();
        if(page!=null){await using var e=Db.Command(c,@"INSERT INTO activity_events(user_id,session_id,event_type,page_path)
            SELECT @id,id,'page',@page FROM activity_sessions WHERE session_key=@key AND user_id=@id AND logout_at IS NULL",("@id",a.Id),("@key",key),("@page","/"+page));e.Transaction=tx;await e.ExecuteNonQueryAsync();}
        await tx.CommitAsync();
    }
    public static async Task Logout(Db db,HttpContext context){
        var a=Current(context);var key=Key(context);if(a==null||key==null)return;
        await Touch(db,context);
        await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
        await using var q=Db.Command(c,@"UPDATE activity_sessions SET online_seconds=online_seconds+IF(TIMESTAMPDIFF(SECOND,last_seen_at,UTC_TIMESTAMP()) BETWEEN 0 AND 90,TIMESTAMPDIFF(SECOND,last_seen_at,UTC_TIMESTAMP()),0),
            last_seen_at=UTC_TIMESTAMP(),logout_at=UTC_TIMESTAMP(),exit_reason='logout' WHERE user_id=@id AND session_key=@key AND logout_at IS NULL",("@id",a.Id),("@key",key));q.Transaction=tx;var updated=await q.ExecuteNonQueryAsync();
        if(updated>0){await using var e=Db.Command(c,"INSERT INTO activity_events(user_id,session_id,event_type) SELECT @id,id,'logout' FROM activity_sessions WHERE session_key=@key AND user_id=@id",("@id",a.Id),("@key",key));e.Transaction=tx;await e.ExecuteNonQueryAsync();}
        await tx.CommitAsync();
    }
    public static async Task Cleanup(Db db){
        await db.Execute(@"UPDATE activity_sessions a LEFT JOIN sessions s ON s.token_hash=a.session_key
          SET a.logout_at=a.last_seen_at,a.exit_reason=IF(s.expires_at<=UTC_TIMESTAMP(),'expired','revoked')
          WHERE a.logout_at IS NULL AND (s.token_hash IS NULL OR s.expires_at<=UTC_TIMESTAMP())");
        await db.Execute("DELETE FROM activity_events WHERE occurred_at<UTC_TIMESTAMP()-INTERVAL 90 DAY");
        await db.Execute("DELETE FROM activity_sessions WHERE observed_at<UTC_TIMESTAMP()-INTERVAL 90 DAY");
    }
    public static void Map(WebApplication app,Db db){
        app.MapPost("/api/activity/presence",async(HttpContext c)=>{
            if(Current(c)==null)return Results.Json(new{message="Entre para continuar."},statusCode:401);
            await Touch(db,c);return Results.Ok(new{presenceSeconds=PresenceSeconds});
        }).RequireRateLimiting("social");
        app.MapGet("/api/admin/activity/users",async(string? q,HttpContext context)=>{
            if(Current(context)?.Role!="admin")return Results.Json(new{message="Acesso restrito ao professor."},statusCode:403);
            q=q?.Trim()??"";if(q.Length>100)return Results.BadRequest();
            var rows=new List<object>();await using var c=await db.Open();await using var cmd=Db.Command(c,@"SELECT u.id,u.name,u.email,u.role,u.status,
              MAX(a.last_seen_at) AS last_seen,
              COALESCE(MAX(a.logout_at IS NULL AND a.last_seen_at>=UTC_TIMESTAMP()-INTERVAL 90 SECOND AND s.expires_at>UTC_TIMESTAMP() AND u.status='active'),0) AS online,
              COALESCE(SUM(a.online_seconds),0) AS seconds
              FROM users u LEFT JOIN activity_sessions a ON a.user_id=u.id AND a.observed_at>=UTC_TIMESTAMP()-INTERVAL 90 DAY
              LEFT JOIN sessions s ON s.token_hash=a.session_key
              WHERE @q='' OR LOCATE(@q,u.name)>0 OR LOCATE(@q,u.email)>0
              GROUP BY u.id,u.name,u.email,u.role,u.status ORDER BY online DESC,u.name,u.id LIMIT 200",("@q",q));await using var r=await cmd.ExecuteReaderAsync();
            while(await r.ReadAsync())rows.Add(new{id=r.GetInt64("id"),name=r.GetString("name"),email=r.GetString("email"),role=r.GetString("role"),status=r.GetString("status"),lastSeenAt=Date(r,"last_seen"),online=Convert.ToInt32(r["online"])==1,onlineSeconds=Convert.ToInt64(r["seconds"])});
            return Results.Ok(new{users=rows,serverTime=DateTime.UtcNow,presenceSeconds=PresenceSeconds,retentionDays=90});
        });
        app.MapGet("/api/admin/activity/users/{id:long}",async(long id,long? before,long? beforeSession,HttpContext context)=>{
            if(Current(context)?.Role!="admin")return Results.Json(new{message="Acesso restrito ao professor."},statusCode:403);
            await using var c=await db.Open();await using var user=Db.Command(c,"SELECT name,email FROM users WHERE id=@id",("@id",id));
            string name,email;await using(var r=await user.ExecuteReaderAsync()){if(!await r.ReadAsync())return Results.NotFound();name=r.GetString(0);email=r.GetString(1);}
            var sessions=new List<object>();long? lastSession=null;var scount=0;
            await using(var q=Db.Command(c,@"SELECT a.id,a.login_at,a.observed_at,a.last_seen_at,a.logout_at,a.exit_reason,a.online_seconds,
              (a.logout_at IS NULL AND a.last_seen_at>=UTC_TIMESTAMP()-INTERVAL 90 SECOND AND s.expires_at>UTC_TIMESTAMP() AND u.status='active') AS online
              FROM activity_sessions a JOIN users u ON u.id=a.user_id LEFT JOIN sessions s ON s.token_hash=a.session_key
              WHERE a.user_id=@id AND a.observed_at>=UTC_TIMESTAMP()-INTERVAL 90 DAY AND a.id<@before ORDER BY a.id DESC LIMIT 25",("@id",id),("@before",beforeSession is >0?beforeSession:long.MaxValue))){
                await using var r=await q.ExecuteReaderAsync();while(await r.ReadAsync()){lastSession=r.GetInt64("id");scount++;sessions.Add(new{id=lastSession,loginAt=Date(r,"login_at"),observedAt=Date(r,"observed_at"),lastSeenAt=Date(r,"last_seen_at"),logoutAt=Date(r,"logout_at"),exitReason=r.IsDBNull(r.GetOrdinal("exit_reason"))?null:r.GetString("exit_reason"),onlineSeconds=r.GetInt64("online_seconds"),online=!r.IsDBNull(r.GetOrdinal("online"))&&Convert.ToInt32(r["online"])==1});}
            }
            var events=new List<object>();long? lastEvent=null;var ecount=0;
            await using(var q=Db.Command(c,"SELECT id,session_id,event_type,page_path,occurred_at FROM activity_events WHERE user_id=@id AND occurred_at>=UTC_TIMESTAMP()-INTERVAL 90 DAY AND id<@before ORDER BY id DESC LIMIT 50",("@id",id),("@before",before is >0?before:long.MaxValue))){
                await using var r=await q.ExecuteReaderAsync();while(await r.ReadAsync()){lastEvent=r.GetInt64("id");ecount++;events.Add(new{id=lastEvent,sessionId=r.GetInt64("session_id"),type=r.GetString("event_type"),path=r.IsDBNull(r.GetOrdinal("page_path"))?null:r.GetString("page_path"),occurredAt=Date(r,"occurred_at")});}
            }
            return Results.Ok(new{id,name,email,sessions,events,nextBefore=ecount==50?lastEvent:null,nextBeforeSession=scount==25?lastSession:null,serverTime=DateTime.UtcNow});
        });
    }
}
public sealed class ActivityMaintenance(Db db,ILogger<ActivityMaintenance> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken){
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(1));
        do{try{await Activity.Cleanup(db);}catch(MySqlException){logger.LogWarning("Não foi possível executar a manutenção dos registros de atividade.");}}
        while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
