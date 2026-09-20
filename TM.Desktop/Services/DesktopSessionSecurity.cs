using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace TM.Desktop.Services;

public sealed class DesktopSessionSecurity
{
    private readonly string bootstrap = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string session = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string cookieName = "tm-session-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    private int bootstrapUsed;
    public Uri Origin { get; private set; } = new("http://127.0.0.1:1");
    public Uri BootstrapUri => new(Origin, "/bootstrap?key=" + bootstrap);

    public void SetOrigin(Uri origin)
    {
        if (origin.Scheme is not "http" || origin.Host is not "127.0.0.1")
            throw new ArgumentException("A loopback HTTP origin is required.", nameof(origin));
        Origin = origin;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; connect-src 'self'; base-uri 'none'; object-src 'none'; frame-ancestors 'none'; form-action 'self'";

        if (context.Connection.RemoteIpAddress is not IPAddress address || !IPAddress.IsLoopback(address)
            || !string.Equals(context.Request.Host.Value, Origin.Authority, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (context.Request.Path == "/bootstrap" && HttpMethods.IsGet(context.Request.Method))
        {
            if (!Matches(context.Request.Query["key"].ToString(), bootstrap)
                || Interlocked.CompareExchange(ref bootstrapUsed, 1, 0) != 0)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            context.Response.Cookies.Append(cookieName, session, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
                Path = "/"
            });
            context.Response.Redirect("/");
            return;
        }

        if (!Matches(context.Request.Cookies[cookieName], session))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        bool safe = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (!safe && !string.Equals(context.Request.Headers.Origin.ToString(), Origin.GetLeftPart(UriPartial.Authority), StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }

    private static bool Matches(string? supplied, string expected) => supplied is not null
        && supplied.Length == expected.Length
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
}
