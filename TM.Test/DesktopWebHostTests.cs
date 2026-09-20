using System.Net;
using System.Text.RegularExpressions;
using TM.Desktop.Services;

namespace TM.Test;

[TestClass]
public sealed class DesktopWebHostTests
{
    [TestMethod]
    public async Task AnonymousRequests_CannotReadPagesOrAssetsAsync()
    {
        await using DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        foreach (string path in new[] { "/", "/vendor/htmx.min.js", "/css/app.css" })
        {
            using HttpResponseMessage response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.IsTrue(response.Headers.CacheControl?.NoStore);
        }
    }

    [TestMethod]
    public async Task Bootstrap_IsSingleUseAndIssuesProtectedSessionCookieAsync()
    {
        await using DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        using HttpResponseMessage first = await client.GetAsync(host.Security.BootstrapUri);
        Assert.AreEqual(HttpStatusCode.Redirect, first.StatusCode);
        string cookie = first.Headers.GetValues("Set-Cookie").Single();
        Assert.IsTrue(cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(cookie.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("/", first.Headers.Location?.OriginalString);
        using HttpResponseMessage replay = await client.GetAsync(host.Security.BootstrapUri);
        Assert.AreEqual(HttpStatusCode.Unauthorized, replay.StatusCode);
        using HttpResponseMessage page = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        Assert.IsTrue(page.Headers.CacheControl?.NoStore);
        Assert.AreEqual("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());
        Assert.IsTrue(page.Headers.GetValues("Content-Security-Policy").Single().Contains("frame-ancestors 'none'", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task InvalidBootstrap_DoesNotConsumeValidTokenAsync()
    {
        await using DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        using HttpResponseMessage invalid = await client.GetAsync("/bootstrap?key=invalid");
        Assert.AreEqual(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using HttpResponseMessage valid = await client.GetAsync(host.Security.BootstrapUri);
        Assert.AreEqual(HttpStatusCode.Redirect, valid.StatusCode);
    }

    [TestMethod]
    public async Task HostAndCrossOriginMutations_AreRejectedAsync()
    {
        await using DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        await AuthenticateAsync(host, client);
        using HttpRequestMessage wrongHost = new(HttpMethod.Get, "/");
        wrongHost.Headers.Host = "untrusted.example";
        using HttpResponseMessage hostResponse = await client.SendAsync(wrongHost);
        Assert.AreEqual(HttpStatusCode.Forbidden, hostResponse.StatusCode);
        foreach (string? origin in new string?[] { null, "https://untrusted.example", "null" })
        {
            using HttpRequestMessage post = new(HttpMethod.Post, "/?handler=Check");
            if (origin is not null)
                post.Headers.Add("Origin", origin);
            post.Content = new FormUrlEncodedContent([]);
            using HttpResponseMessage response = await client.SendAsync(post);
            Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [TestMethod]
    public async Task Post_RequiresAntiforgeryTokenAndSupportsHtmxAsync()
    {
        await using DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        await AuthenticateAsync(host, client);
        client.DefaultRequestHeaders.Add("Origin", host.Security.Origin.GetLeftPart(UriPartial.Authority));
        using HttpResponseMessage missing = await client.PostAsync("/?handler=Check", new FormUrlEncodedContent([]));
        Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
        string html = await client.GetStringAsync("/");
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.None, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success, "The rendered form must contain an antiforgery token.");
        using FormUrlEncodedContent content = new(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value)
        });
        using HttpResponseMessage accepted = await client.PostAsync("/?handler=Check", content);
        Assert.AreEqual(HttpStatusCode.OK, accepted.StatusCode);
        Assert.IsTrue((await accepted.Content.ReadAsStringAsync()).Contains("shell ready", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task AuthenticatedAssets_AreAvailableLocallyAsync()
    {
        await using DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        await AuthenticateAsync(host, client);
        string script = await client.GetStringAsync("/vendor/htmx.min.js");
        Assert.IsTrue(script.Contains("2.0.10", StringComparison.Ordinal));
        Assert.IsTrue((await client.GetStringAsync("/css/app.css")).Contains("font-family", StringComparison.Ordinal));
        string html = await client.GetStringAsync("/");
        Assert.IsTrue(html.Contains("historyCacheSize", StringComparison.Ordinal));
        Assert.IsFalse(html.Contains("https://", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task DisposeAsync_StopsListeningAsync()
    {
        DesktopWebHost host = await StartAsync();
        using HttpClient client = CreateClient(host);
        await host.DisposeAsync();
        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("/"));
    }

    private static Task<DesktopWebHost> StartAsync() => DesktopWebHost.StartAsync(AppContext.BaseDirectory);

    private static HttpClient CreateClient(DesktopWebHost host) => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        CookieContainer = new CookieContainer()
    }) { BaseAddress = host.Security.Origin, Timeout = TimeSpan.FromSeconds(15) };

    private static async Task AuthenticateAsync(DesktopWebHost host, HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync(host.Security.BootstrapUri);
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
    }
}
