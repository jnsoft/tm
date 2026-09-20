using System.Net;
using System.Text.RegularExpressions;
using TM.Desktop.Services;

namespace TM.Test;

[TestClass]
public sealed class WorkspaceHttpTests
{
    [TestMethod]
    public async Task HtmxWorkflow_EncodesTextAndClearsSecretsOnLockAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"TM.HttpWorkspace.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            TestProjectFileDialogs dialogs = new() { SavePath = Path.Combine(directory, "saved.xml") };
            await using HttpWorkspace browser = await HttpWorkspace.CreateAsync(dialogs);
            await browser.PostAsync("New", ("Password", "http-test-password"));
            Assert.IsFalse(browser.Html.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase));
            await browser.PostAsync("Add", ("NodeType", "Project"), ("Name", "<script>alert('test')</script>"));
            Assert.IsFalse(browser.Html.Contains("<script>alert", StringComparison.Ordinal));
            Assert.IsTrue(browser.Html.Contains("&lt;script&gt;", StringComparison.Ordinal));
            string root = browser.EditorId;
            await browser.PostAsync("Add", ("NodeType", "Protected"), ("NodeId", root), ("Name", "Synthetic credential"));
            string item = browser.EditorId;
            await browser.PostAsync("Edit", ("NodeId", item), ("Name", "Synthetic credential"),
                ("Login", "synthetic-user"), ("ReplaceSecret", "true"), ("Secret", "synthetic-http-secret"));
            Assert.IsFalse(browser.Html.Contains("synthetic-http-secret", StringComparison.Ordinal));
            await browser.PostAsync("Save");
            Assert.IsTrue(File.Exists(dialogs.SavePath));
            await browser.PostAsync("Reveal", ("NodeId", item));
            Assert.IsTrue(browser.Html.Contains("synthetic-http-secret", StringComparison.Ordinal));
            string ordinaryGet = await browser.Client.GetStringAsync("/");
            Assert.IsFalse(ordinaryGet.Contains("synthetic-http-secret", StringComparison.Ordinal));
            await browser.PostAsync("Lock");
            Assert.IsTrue(browser.Html.Contains("Locked", StringComparison.Ordinal));
            Assert.IsFalse(browser.Html.Contains("synthetic-user", StringComparison.Ordinal));
            Assert.IsFalse(browser.Html.Contains("synthetic-http-secret", StringComparison.Ordinal));
            Assert.IsFalse(browser.Html.Contains("data-editor", StringComparison.Ordinal));
            await browser.PostAsync("Unlock", ("Password", "wrong-http-password"));
            Assert.IsTrue(browser.Html.Contains("Locked", StringComparison.Ordinal));
            Assert.IsTrue(browser.Html.Contains("operation failed", StringComparison.Ordinal));
            Assert.IsFalse(browser.Html.Contains("wrong-http-password", StringComparison.Ordinal));
            await browser.PostAsync("Unlock", ("Password", "http-test-password"));
            Assert.IsTrue(browser.Html.Contains("Document unlocked", StringComparison.Ordinal));
            await browser.PostAsync("Reveal", ("NodeId", item));
            Assert.IsTrue(browser.Html.Contains("synthetic-http-secret", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public async Task InvalidAndStalePosts_ReturnSafeRefreshedViewAsync()
    {
        await using HttpWorkspace browser = await HttpWorkspace.CreateAsync(new TestProjectFileDialogs());
        await browser.PostAsync("New", ("Password", "test-password"));
        await browser.PostAsync("Add", ("NodeType", "Project"), ("Name", "Keep this"));
        string root = browser.EditorId;
        await browser.PostAsync("Edit", ("NodeId", root), ("Name", "Invalid edit"),
            ("Progress", "101"), ("Secret", "never-echo-this"));
        Assert.IsTrue(browser.Html.Contains("Check the submitted", StringComparison.Ordinal));
        Assert.IsTrue(browser.Html.Contains("Keep this", StringComparison.Ordinal));
        Assert.IsFalse(browser.Html.Contains("never-echo-this", StringComparison.Ordinal));
        await browser.PostAsync("Delete", ("NodeId", root), ("ConfirmDelete", "true"), ("Revision", "0"));
        Assert.IsTrue(browser.Html.Contains("document changed", StringComparison.Ordinal));
        Assert.IsTrue(browser.Html.Contains("Keep this", StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GeneratedPassword_IsOnlyDisclosedByExplicitRevealAsync(bool complex)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"TM.HttpGeneration.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            TestProjectFileDialogs dialogs = new() { SavePath = Path.Combine(directory, "generated.xml") };
            await using HttpWorkspace browser = await HttpWorkspace.CreateAsync(dialogs);
            await browser.PostAsync("New", ("Password", "http-test-password"));
            await browser.PostAsync("Add", ("NodeType", "Project"), ("Name", "Root"));
            Assert.IsFalse(browser.Html.Contains("Generate and replace password", StringComparison.Ordinal));
            await browser.PostAsync("Add", ("NodeType", "Protected"), ("NodeId", browser.EditorId), ("Name", "Credential"));
            string id = browser.EditorId;
            Assert.IsTrue(browser.Html.Contains("Generate and replace password", StringComparison.Ordinal));
            if (complex)
                await browser.PostAsync("GeneratePassword", ("NodeId", id), ("ConfirmGeneratePassword", "true"),
                    ("GeneratedPasswordLength", "30"), ("UseComplexGeneratedPassword", "true"));
            else
                await browser.PostAsync("GeneratePassword", ("NodeId", id), ("ConfirmGeneratePassword", "true"));
            string generatedHtml = browser.Html;
            Assert.IsFalse(generatedHtml.Contains("data-secret", StringComparison.Ordinal));
            await browser.PostAsync("Reveal", ("NodeId", id));
            string secret = RevealedSecret(browser.Html);
            Assert.AreEqual(complex ? 30 : 12, secret.Length);
            // Compare against Razor's encoded output as well as the raw value.
            string encoded = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(secret);
            Assert.IsFalse(generatedHtml.Contains(secret, StringComparison.Ordinal));
            Assert.IsFalse(generatedHtml.Contains(encoded, StringComparison.Ordinal));
            string ordinaryGet = await browser.Client.GetStringAsync("/");
            Assert.IsFalse(ordinaryGet.Contains("data-secret", StringComparison.Ordinal));
            Assert.IsFalse(ordinaryGet.Contains(encoded, StringComparison.Ordinal));
            await browser.PostAsync("Save");
            Assert.IsFalse((await File.ReadAllTextAsync(dialogs.SavePath)).Contains(secret, StringComparison.Ordinal));
            await browser.PostAsync("Lock");
            Assert.IsFalse(browser.Html.Contains("data-secret", StringComparison.Ordinal));
            Assert.IsFalse(browser.Html.Contains("Generate and replace password", StringComparison.Ordinal));
            await browser.PostAsync("GeneratePassword", ("NodeId", id), ("ConfirmGeneratePassword", "true"));
            Assert.IsTrue(browser.Html.Contains("Open or unlock", StringComparison.Ordinal));
            await browser.PostAsync("Unlock", ("Password", "http-test-password"));
            await browser.PostAsync("Reveal", ("NodeId", id));
            Assert.AreEqual(secret, RevealedSecret(browser.Html));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public async Task InvalidGenerationPosts_DoNotReplaceExistingPasswordAsync()
    {
        await using HttpWorkspace browser = await HttpWorkspace.CreateAsync(new TestProjectFileDialogs());
        await browser.PostAsync("New", ("Password", "http-test-password"));
        await browser.PostAsync("Add", ("NodeType", "Project"), ("Name", "Root"));
        await browser.PostAsync("Add", ("NodeType", "Protected"), ("NodeId", browser.EditorId), ("Name", "Credential"));
        string id = browser.EditorId;
        await browser.PostAsync("Edit", ("NodeId", id), ("Name", "Credential"),
            ("ReplaceSecret", "true"), ("Secret", "keep-http-secret"));
        string revision = browser.Revision;
        foreach (string length in new[] { "4", "31", "not-a-number", "2147483648" })
        {
            await browser.PostAsync("GeneratePassword", ("NodeId", id), ("ConfirmGeneratePassword", "true"),
                ("GeneratedPasswordLength", length));
            Assert.IsTrue(browser.Html.Contains("Check the submitted", StringComparison.Ordinal));
            Assert.AreEqual(revision, browser.Revision);
            Assert.IsFalse(browser.Html.Contains("keep-http-secret", StringComparison.Ordinal));
        }
        await browser.PostAsync("GeneratePassword", ("NodeId", id));
        Assert.IsTrue(browser.Html.Contains("Confirm replacing", StringComparison.Ordinal));
        Assert.AreEqual(revision, browser.Revision);
        await browser.PostAsync("GeneratePassword", ("NodeId", id), ("ConfirmGeneratePassword", "true"), ("Revision", "0"));
        Assert.IsTrue(browser.Html.Contains("document changed", StringComparison.Ordinal));
        Assert.AreEqual(revision, browser.Revision);
        await browser.PostAsync("Reveal", ("NodeId", id));
        Assert.AreEqual("keep-http-secret", RevealedSecret(browser.Html));
    }

    private static string RevealedSecret(string html)
    {
        Match match = Regex.Match(html, "<output data-secret>(.*?)</output>", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        Assert.IsTrue(match.Success, "Expected an explicit reveal response.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private sealed class HttpWorkspace(DesktopWebHost host, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public string Html { get; private set; } = "";
        public string Revision => Field(Html, "Input.Revision");
        public string EditorId
        {
            get
            {
                Match editor = Regex.Match(Html, "<form[^>]*data-editor[^>]*>(.*?)</form>", RegexOptions.Singleline, TimeSpan.FromSeconds(1));
                Assert.IsTrue(editor.Success, "Expected the editor form.");
                return Field(editor.Groups[1].Value, "Input.NodeId");
            }
        }

        public static async Task<HttpWorkspace> CreateAsync(TestProjectFileDialogs dialogs)
        {
            DesktopWebHost host = await DesktopWebHost.StartAsync(AppContext.BaseDirectory, dialogs: dialogs);
            HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false })
            {
                BaseAddress = host.Security.Origin,
                Timeout = TimeSpan.FromSeconds(15)
            };
            try
            {
                using HttpResponseMessage response = await client.GetAsync(host.Security.BootstrapUri);
                Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
                client.DefaultRequestHeaders.Add("Origin", host.Security.Origin.GetLeftPart(UriPartial.Authority));
                client.DefaultRequestHeaders.Add("HX-Request", "true");
                return new(host, client) { Html = await client.GetStringAsync("/") };
            }
            catch
            {
                client.Dispose();
                await host.DisposeAsync();
                throw;
            }
        }

        public async Task PostAsync(string action, params (string Name, string Value)[] fields)
        {
            Dictionary<string, string> values = new()
            {
                ["__RequestVerificationToken"] = Field(Html, "__RequestVerificationToken"),
                ["Input.Action"] = action,
                ["Input.Revision"] = Field(Html, "Input.Revision")
            };
            foreach ((string name, string value) in fields) values["Input." + name] = value;
            using HttpResponseMessage response = await client.PostAsync("/?handler=Workspace", new FormUrlEncodedContent(values));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsTrue(response.Headers.CacheControl?.NoStore);
            Html = await response.Content.ReadAsStringAsync();
        }

        private static string Field(string html, string name)
        {
            Match match = Regex.Match(html, "name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(1));
            Assert.IsTrue(match.Success, $"Missing form field {name}.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await host.DisposeAsync();
        }
    }
}
