using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using MySqlConnector;

var builder=WebApplication.CreateBuilder(args);
var port=builder.Configuration["PORT"];
if(!string.IsNullOrWhiteSpace(port))builder.WebHost.UseUrls($"http://0.0.0.0:{int.Parse(port)}");
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=131072);
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<IPasswordHasher<Account>,PasswordHasher<Account>>();
builder.Services.Configure<PasswordHasherOptions>(o=>o.IterationCount=210000);
builder.Services.AddAntiforgery(o=>{
    o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="portfolio.csrf";o.Cookie.HttpOnly=true;
    o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.SecurePolicy=builder.Environment.IsDevelopment()?CookieSecurePolicy.SameAsRequest:CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(o=>{
    o.RejectionStatusCode=429;
    o.AddPolicy("social",context=>RateLimitPartition.GetFixedWindowLimiter((context.Items["account"] as Account)?.Id.ToString()??context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
    o.AddPolicy("auth",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",
        _=>new FixedWindowRateLimiterOptions{PermitLimit=30,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
});
var app=builder.Build();
var db=app.Services.GetRequiredService<Db>();var hasher=app.Services.GetRequiredService<IPasswordHasher<Account>>();
await db.Initialize(app.Configuration,hasher);
var dummy=new Account(0,"","","","student","blocked",0,null);
var dummyHash=hasher.HashPassword(dummy,Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
var siteRoot=Path.GetFullPath(app.Configuration["SiteRoot"]??Path.Combine(app.Environment.ContentRootPath,".."));
var publicFolders=new HashSet<string>(["menu","sobre","foco","alem-do-codigo","contato","assets","shared","login","termos"],StringComparer.OrdinalIgnoreCase);
var fileTypes=new FileExtensionContentTypeProvider();
CookieOptions SessionCookie()=>new(){HttpOnly=true,Secure=!app.Environment.IsDevelopment(),SameSite=SameSiteMode.Lax,Path="/",IsEssential=true,MaxAge=TimeSpan.FromHours(8)};
Account? Current(HttpContext c)=>c.Items["account"] as Account;
IResult Error(string text,int status=400)=>Results.Json(new{message=text},statusCode:status);

app.Use(async(context,next)=>{
    // Production runs behind the HTTPS ingress of the hosting provider.
    // Browser cookies remain Secure; do not expose this service over plain public HTTP.
    if(!app.Environment.IsDevelopment())context.Request.Scheme="https";
    context.Response.Headers["Cache-Control"]="private, no-store";
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["Referrer-Policy"]="same-origin";
    context.Response.Headers["X-Frame-Options"]="DENY";
    context.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    try {
        if(context.Request.Cookies.TryGetValue("portfolio.session",out var token)&&token.Length==64){
            var account=await db.Session(Input.Hash(token));
            if(account!=null){context.Items["account"]=account;context.User=new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier,account.Id.ToString()),new Claim(ClaimTypes.Name,account.Name),new Claim(ClaimTypes.Role,account.Role)],"PortfolioSession"));}
        }
        await next();
    } catch(MySqlException){if(!context.Response.HasStarted){context.Response.StatusCode=503;await context.Response.WriteAsJsonAsync(new{message="Serviço indisponível. Tente novamente mais tarde."});}}
});
app.UseRateLimiter();
app.Use(async(context,next)=>{
    if(HttpMethods.IsPost(context.Request.Method)&&context.Request.Path.StartsWithSegments("/api")){
        try{await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);}
        catch(AntiforgeryValidationException){context.Response.StatusCode=400;await context.Response.WriteAsJsonAsync(new{message="Atualize a página e tente novamente."});return;}
    }
    await next();
});
app.MapGet("/health",()=>Results.Ok(new{status="ok"}));
app.MapGet("/api/auth/csrf",(HttpContext context,IAntiforgery antiforgery)=>Results.Ok(new{token=antiforgery.GetAndStoreTokens(context).RequestToken}));
app.MapGet("/api/auth/me",(HttpContext c)=>Current(c) is Account a?Results.Ok(new{id=a.Id,name=a.Name,email=a.Email,role=a.Role}):Error("Entre para continuar.",401));
app.MapPost("/api/auth/register",async(RegisterRequest input)=>{
    var email=Input.Email(input.Email);var name=input.Name?.Trim();
    if(!input.AcceptTerms)return Error("Aceite os termos de uso para criar sua conta.");
    if(email==null||string.IsNullOrWhiteSpace(name)||name.Length>100||name.Any(char.IsControl)||!Input.Password(input.Password))
        return Error("Informe nome, e-mail válido e senha de 12 a 128 caracteres.");
    var account=new Account(0,name,email,"","student","active",0,null);
    try{
        await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
        await using var q=Db.Command(c,"INSERT INTO users(name,email,password_hash,status) VALUES(@name,@email,@hash,'active')",("@name",name),("@email",email),("@hash",hasher.HashPassword(account,input.Password!)));q.Transaction=tx;await q.ExecuteNonQueryAsync();
        await using var settings=Db.Command(c,"INSERT INTO user_settings(user_id,terms_version,accepted_at) VALUES(@id,@version,UTC_TIMESTAMP())",("@id",q.LastInsertedId),("@version",Portal.TermsVersion));settings.Transaction=tx;await settings.ExecuteNonQueryAsync();await tx.CommitAsync();
    }catch(MySqlException e) when(e.Number==1062){/* Avoid revealing whether an address exists. */}
    return Results.Ok(new{message="Cadastro recebido. Você já pode entrar. Se já possui conta, use sua senha atual."});
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/login",async(LoginRequest input,HttpContext context)=>{
    var email=Input.Email(input.Email);
    if(email==null||input.Password is not {Length: >0 and <=128})return Error("E-mail ou senha inválidos.",401);
    // Lock the user row so simultaneous attempts cannot bypass the account lockout.
    await using var connection=await db.Open();await using var tx=await connection.BeginTransactionAsync();
    await using var q=Db.Command(connection,"SELECT * FROM users WHERE email=@email FOR UPDATE",("@email",email));q.Transaction=tx;
    Account? account;
    await using(var reader=await q.ExecuteReaderAsync()){account=await reader.ReadAsync()?Db.Read(reader):null;}
    if(account==null){hasher.VerifyHashedPassword(dummy,dummyHash,input.Password);return Error("E-mail ou senha inválidos.",401);}
    if(account.LockedUntil>DateTime.UtcNow)return Error("E-mail ou senha inválidos. Aguarde alguns minutos antes de tentar novamente.",401);
    var check=hasher.VerifyHashedPassword(account,account.Hash,input.Password);
    if(check==PasswordVerificationResult.Failed){
        var failures=account.LockedUntil.HasValue?1:account.Failures+1;
        await using var fail=Db.Command(connection,"UPDATE users SET failed_attempts=@fail, locked_until=@until WHERE id=@id",("@fail",failures),("@until",failures>=5?DateTime.UtcNow.AddMinutes(15):null),("@id",account.Id));fail.Transaction=tx;
        await fail.ExecuteNonQueryAsync();await tx.CommitAsync();return Error("E-mail ou senha inválidos.",401);
    }
    if(account.Status!="active")return Error("Sua conta está bloqueada. Entre em contato com o professor.",403);
    var hash=check==PasswordVerificationResult.SuccessRehashNeeded?hasher.HashPassword(account,input.Password):account.Hash;
    await using var reset=Db.Command(connection,"UPDATE users SET failed_attempts=0,locked_until=NULL,password_hash=@hash WHERE id=@id",("@hash",hash),("@id",account.Id));reset.Transaction=tx;await reset.ExecuteNonQueryAsync();
    var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    await using var session=Db.Command(connection,"INSERT INTO sessions(token_hash,user_id,expires_at) VALUES(@hash,@id,@expires)",("@hash",Input.Hash(token)),("@id",account.Id),("@expires",DateTime.UtcNow.AddHours(8)));session.Transaction=tx;await session.ExecuteNonQueryAsync();
    await tx.CommitAsync();context.Response.Cookies.Append("portfolio.session",token,SessionCookie());
    return Results.Ok(new{message="Login realizado.",role=account.Role});
}).RequireRateLimiting("auth");
app.MapPost("/api/auth/logout",async(HttpContext context)=>{
    if(context.Request.Cookies.TryGetValue("portfolio.session",out var token))await db.Execute("DELETE FROM sessions WHERE token_hash=@hash",("@hash",Input.Hash(token)));
    context.Response.Cookies.Delete("portfolio.session",SessionCookie());return Results.Ok(new{message="Você saiu."});
});
app.MapPost("/api/auth/password",async(PasswordRequest input,HttpContext context)=>{
    var a=Current(context);if(a==null)return Error("Entre para continuar.",401);
    if(a.Role!="admin")return Error("Solicite ao professor a autorização para trocar a senha.",403);
    if(input.CurrentPassword is not {Length: >0 and <=128}||!Input.Password(input.NewPassword))return Error("A nova senha deve ter de 12 a 128 caracteres.");
    if(hasher.VerifyHashedPassword(a,a.Hash,input.CurrentPassword)==PasswordVerificationResult.Failed)return Error("Senha atual incorreta.");
    await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
    await using var q=Db.Command(c,"UPDATE users SET password_hash=@hash,failed_attempts=0,locked_until=NULL WHERE id=@id",("@hash",hasher.HashPassword(a,input.NewPassword!)),("@id",a.Id));q.Transaction=tx;await q.ExecuteNonQueryAsync();
    await using var sessions=Db.Command(c,"DELETE FROM sessions WHERE user_id=@id",("@id",a.Id));sessions.Transaction=tx;await sessions.ExecuteNonQueryAsync();await tx.CommitAsync();
    context.Response.Cookies.Delete("portfolio.session",SessionCookie());return Results.Ok(new{message="Senha atualizada. Entre novamente."});
});
app.MapGet("/api/admin/students",async(HttpContext context)=>{
    if(Current(context)?.Role!="admin")return Error("Acesso restrito ao professor.",403);
    var users=new List<object>();await using var c=await db.Open();await using var q=Db.Command(c,"SELECT id,name,email,status,created_at FROM users WHERE role='student' ORDER BY created_at DESC");await using var r=await q.ExecuteReaderAsync();
    while(await r.ReadAsync())users.Add(new{id=r.GetInt64("id"),name=r.GetString("name"),email=r.GetString("email"),status=r.GetString("status"),createdAt=r.GetDateTime("created_at")});
    return Results.Ok(users);
});
app.MapPost("/api/admin/students/{id:long}/status",async(long id,StatusRequest input,HttpContext context)=>{
    if(Current(context)?.Role!="admin")return Error("Acesso restrito ao professor.",403);
    if(input.Status is not ("active" or "blocked"))return Error("Status inválido.");
    await using var c=await db.Open();await using var tx=await c.BeginTransactionAsync();
    await using var q=Db.Command(c,"UPDATE users SET status=@status WHERE id=@id AND role='student'",("@status",input.Status),("@id",id));q.Transaction=tx;await q.ExecuteNonQueryAsync();
    await using var exists=Db.Command(c,"SELECT COUNT(*) FROM users WHERE id=@id AND role='student'",("@id",id));exists.Transaction=tx;
    if(Convert.ToInt64(await exists.ExecuteScalarAsync())==0)return Error("Aluno não encontrado.",404);
    await using var revoke=Db.Command(c,"DELETE FROM sessions WHERE user_id=@id",("@id",id));revoke.Transaction=tx;await revoke.ExecuteNonQueryAsync();await tx.CommitAsync();
    return Results.Ok(new{message="Acesso atualizado."});
});
Portal.Map(app,db,hasher);
Social.Map(app,db);
Notifications.Map(app,db);
Sharing.Map(app,db);
// No public static-file middleware: authorization happens BEFORE reading study files.
app.MapGet("/{**path}",async(string? path,HttpContext context)=>{
    if((path??"").Contains('\\'))return Results.NotFound();
    string full;
    try{full=Path.GetFullPath(Path.Combine(siteRoot,path??""));}catch{return Results.NotFound();}
    if(!full.StartsWith(siteRoot+Path.DirectorySeparatorChar,StringComparison.Ordinal)&&full!=siteRoot)return Results.NotFound();
    var relative=Path.GetRelativePath(siteRoot,full).Replace('\\','/');
    if(relative!="."&&relative.Split('/').Any(part=>part.StartsWith('.')))return Results.NotFound();
    var folder=relative.Split('/')[0];
    var study=folder.Equals("perfil",StringComparison.OrdinalIgnoreCase)||folder.Equals("mensagens",StringComparison.OrdinalIgnoreCase)||folder.Equals("amigos",StringComparison.OrdinalIgnoreCase)||folder.Equals("entregas",StringComparison.OrdinalIgnoreCase)||folder.Equals("exercicios",StringComparison.OrdinalIgnoreCase)||folder.Equals("apoio",StringComparison.OrdinalIgnoreCase);
    var admin=folder.Equals("alunos",StringComparison.OrdinalIgnoreCase);
    if(study||admin){
        var a=Current(context);
        if(a==null){if(Path.GetExtension(relative) is ".js" or ".css" or ".json")return Error("Entre para continuar.",401);return Results.Redirect("/login/index.html?returnUrl="+Uri.EscapeDataString("/"+relative+context.Request.QueryString.Value));}
        if(admin&&a.Role!="admin")return Error("Acesso restrito ao professor.",403);
        if(folder.Equals("entregas",StringComparison.OrdinalIgnoreCase)&&a.Role=="admin"){if(Path.GetExtension(relative) is ".js" or ".css")return Error("Use o painel do professor.",403);return Results.Redirect("/alunos/index.html");}
    } else if(relative is not ("." or "index.html" or "config.js")&&!publicFolders.Contains(folder))return Results.NotFound();
    if(Directory.Exists(full))full=Path.Combine(full,"index.html");
    var extension=Path.GetExtension(full).ToLowerInvariant();
    if(extension is not (".html" or ".css" or ".js" or ".png" or ".jpg" or ".jpeg" or ".svg" or ".webp" or ".ico" or ".pdf"))return Results.NotFound();
    if(!File.Exists(full)||!fileTypes.TryGetContentType(full,out var contentType))return Results.NotFound();
    return Results.File(full,contentType,enableRangeProcessing:false);
});
app.Run();
