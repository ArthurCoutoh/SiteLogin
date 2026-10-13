using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
public static class Input
{
    public static string? Email(string? text)
    {
        text=text?.Trim().ToLowerInvariant();
        if(string.IsNullOrEmpty(text)||text.Length>254||text.Any(c=>c>127))return null;
        return MailAddress.TryCreate(text,out var parsed)&&parsed.Address==text?text:null;
    }
    public static bool Password(string? value)=>value is {Length: >=12 and <=128}&&!string.IsNullOrWhiteSpace(value);
    public static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static string ReturnPath(string? value)
    {
        // Redirect only to this site's known study paths; never to a different origin.
        if(string.IsNullOrEmpty(value)||value.Contains('\\')||value.Any(char.IsControl))return "/exercicios/index.html";
        if(!Uri.TryCreate("https://portfolio.invalid"+value,UriKind.Absolute,out var uri)||uri.Host!="portfolio.invalid")return "/exercicios/index.html";
        return value.StartsWith("/exercicios/",StringComparison.Ordinal)||value.StartsWith("/apoio/",StringComparison.Ordinal)?uri.PathAndQuery:"/exercicios/index.html";
    }
}
public record LoginRequest(string? Email,string? Password);
public record RegisterRequest(string? Name,string? Email,string? Password);
public record StatusRequest(string? Status);
public record PasswordRequest(string? CurrentPassword,string? NewPassword);
