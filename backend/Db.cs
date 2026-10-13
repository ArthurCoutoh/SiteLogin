using MySqlConnector;
using Microsoft.AspNetCore.Identity;

public record Account(long Id, string Name, string Email, string Hash, string Role, string Status,
    int Failures, DateTime? LockedUntil);
public sealed class Db
{
    private readonly string connectionString;
    public Db(IConfiguration config)
    {
        connectionString = config.GetConnectionString("MySql") ?? "";
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var cs = new MySqlConnectionStringBuilder {
                Server = config["MYSQLHOST"] ?? "localhost",
                Port = uint.Parse(config["MYSQLPORT"] ?? "3306"),
                UserID = config["MYSQLUSER"] ?? "",
                Password = config["MYSQLPASSWORD"] ?? "",
                Database = config["MYSQLDATABASE"] ?? "portfolio",
                ConnectionTimeout = 10, DefaultCommandTimeout = 15,
                SslMode = Enum.Parse<MySqlSslMode>(config["MYSQL_SSL_MODE"] ?? "Required", true)
            };
            if (string.IsNullOrWhiteSpace(cs.UserID)) throw new InvalidOperationException("Configure MySQL antes de iniciar.");
            connectionString = cs.ConnectionString;
        }
    }
    public async Task<MySqlConnection> Open()
    {
        var c = new MySqlConnection(connectionString);
        try { await c.OpenAsync(); return c; } catch { await c.DisposeAsync(); throw; }
    }
    public static MySqlCommand Command(MySqlConnection c, string sql, params (string Key, object? Value)[] args)
    {
        var command = new MySqlCommand(sql, c);
        foreach (var (k,v) in args) command.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return command;
    }
    public async Task<int> Execute(string sql, params (string Key, object? Value)[] args)
    {
        await using var c = await Open(); await using var q = Command(c,sql,args);
        return await q.ExecuteNonQueryAsync();
    }
    public async Task<Account?> Find(string email)
    {
        await using var c=await Open(); await using var q=Command(c,"SELECT * FROM users WHERE email=@email",("@email",email));
        await using var r=await q.ExecuteReaderAsync();return await r.ReadAsync()?Read(r):null;
    }
    public static Account Read(MySqlDataReader r) => new(r.GetInt64("id"),r.GetString("name"),r.GetString("email"),
        r.GetString("password_hash"),r.GetString("role"),r.GetString("status"),r.GetInt32("failed_attempts"),
        r.IsDBNull(r.GetOrdinal("locked_until"))?null:DateTime.SpecifyKind(r.GetDateTime("locked_until"),DateTimeKind.Utc));
    public async Task<Account?> Session(string hash)
    {
        await using var c=await Open();await using var q=Command(c,
            "SELECT u.* FROM users u JOIN sessions s ON s.user_id=u.id WHERE s.token_hash=@hash AND s.expires_at>UTC_TIMESTAMP() AND u.status='active'",("@hash",hash));
        await using var r=await q.ExecuteReaderAsync();return await r.ReadAsync()?Read(r):null;
    }
    public async Task Initialize(IConfiguration config, IPasswordHasher<Account> hasher)
    {
        var file=Path.Combine(AppContext.BaseDirectory,"schema.sql");
        foreach(var sql in (await File.ReadAllTextAsync(file)).Split(';',StringSplitOptions.RemoveEmptyEntries)) await Execute(sql);
        await using var c=await Open();await using var count=Command(c,"SELECT COUNT(*) FROM users WHERE role='admin'");
        if(Convert.ToInt64(await count.ExecuteScalarAsync())==0)
        {
            var email=Input.Email(config["ADMIN_EMAIL"]);var password=config["ADMIN_PASSWORD"]??"";
            if(email==null||!Input.Password(password))throw new InvalidOperationException("Primeiro início: defina ADMIN_EMAIL e ADMIN_PASSWORD (12 a 128 caracteres).");
            var user=new Account(0,"Arthur Couto",email,"","admin","active",0,null);
            await Execute("INSERT INTO users(name,email,password_hash,role,status) VALUES(@name,@email,@hash,'admin','active')",
                ("@name",user.Name),("@email",email),("@hash",hasher.HashPassword(user,password)));
        }
        await Execute("DELETE FROM sessions WHERE expires_at<=UTC_TIMESTAMP()");
    }
}
