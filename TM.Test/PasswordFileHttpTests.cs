using System.Net;
using System.Text.RegularExpressions;
using TM.Desktop.Services;

namespace TM.Test;

[TestClass]
public sealed class PasswordFileHttpTests
{
    [TestMethod]
    public async Task PasswordTools_ValidateBeforeDialogsAndNeverEchoCredentialsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"TM.PasswordHttp.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "source"), encrypted = Path.Combine(directory, "encrypted");
            await File.WriteAllTextAsync(source, "synthetic file content");
            TestFileToolDialogs dialogs = new() { Input = source, Output = encrypted };
            await using DesktopWebHost host = await DesktopWebHost.StartAsync(AppContext.BaseDirectory,
                clipboard: new TestNativeClipboard(), fileDialogs: dialogs);
            using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = host.Security.Origin };
            using (HttpResponseMessage anonymous = await client.PostAsync("/FileTools?handler=Password", new FormUrlEncodedContent([])))
                Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using (HttpResponseMessage bootstrap = await client.GetAsync(host.Security.BootstrapUri))
                Assert.AreEqual(HttpStatusCode.Redirect, bootstrap.StatusCode);
            client.DefaultRequestHeaders.Add("Origin", host.Security.Origin.GetLeftPart(UriPartial.Authority));
            client.DefaultRequestHeaders.Add("HX-Request", "true");
            string html = await client.GetStringAsync("/FileTools");
            string token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value);
            using (HttpResponseMessage missing = await client.PostAsync("/FileTools?handler=Password", new FormUrlEncodedContent([])))
                Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
            await PostAsync("Encrypt", "", "");
            await PostAsync("Encrypt", "http-file-password", "mismatch-password");
            await PostAsync("999", "http-file-password", "http-file-password");
            await PostAsync("Encrypt", new string('x', 4097), "http-file-password");
            Assert.AreEqual(0, dialogs.InputCalls);
            string success = await PostAsync("Encrypt", "http-file-password", "http-file-password");
            Assert.IsTrue(success.Contains("Output file created", StringComparison.Ordinal));
            Assert.AreEqual("synthetic file content", await File.ReadAllTextAsync(source));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "injected")));
            dialogs.Input = encrypted;
            dialogs.Output = Path.Combine(directory, "decoded");
            Assert.IsTrue((await PostAsync("Decrypt", "wrong-password", "")).Contains("operation failed", StringComparison.Ordinal));
            Assert.IsFalse(File.Exists(dialogs.Output));
            Assert.IsTrue((await PostAsync("Decrypt", "http-file-password", "")).Contains("Output file created", StringComparison.Ordinal));
            Assert.AreEqual("synthetic file content", await File.ReadAllTextAsync(dialogs.Output));
            dialogs.Input = null;
            Assert.IsTrue((await PostAsync("Encrypt", "http-file-password", "http-file-password")).Contains("canceled", StringComparison.Ordinal));
            Assert.HasCount(0, Directory.GetDirectories(directory, ".tm-crypto-*"));

            async Task<string> PostAsync(string operation, string password, string confirmation)
            {
                using HttpResponseMessage response = await client.PostAsync("/FileTools?handler=Password", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token, ["PasswordOperation"] = operation,
                    ["FilePassword"] = password, ["ConfirmFilePassword"] = confirmation,
                    ["SourcePath"] = "ignored", ["DestinationPath"] = Path.Combine(directory, "injected")
                }));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.Headers.CacheControl?.NoStore);
                string result = await response.Content.ReadAsStringAsync();
                foreach (string secret in new[] { "http-file-password", "wrong-password", "mismatch-password", "synthetic file content", directory })
                    Assert.IsFalse(result.Contains(secret, StringComparison.Ordinal));
                return result;
            }
        }
        finally { Directory.Delete(directory, true); }
    }
}
