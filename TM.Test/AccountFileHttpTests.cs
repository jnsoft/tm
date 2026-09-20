using System.Net;
using System.Text.RegularExpressions;
using TM.Desktop.Services;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class AccountFileHttpTests
{
    [TestMethod]
    public async Task AccountTools_RequireAcknowledgementAndNativePathsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"TM.AccountHttp.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "private-source"), encrypted = Path.Combine(directory, "encrypted");
            await File.WriteAllTextAsync(source, "synthetic account content");
            TestFileToolDialogs dialogs = new() { Input = source, Output = encrypted };
            TestAccountFileProtection protection = new();
            await using DesktopWebHost host = await DesktopWebHost.StartAsync(AppContext.BaseDirectory,
                clipboard: new TestNativeClipboard(), fileDialogs: dialogs, accountProtection: protection);
            using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = host.Security.Origin };
            using (HttpResponseMessage anonymous = await client.PostAsync("/FileTools?handler=Account", new FormUrlEncodedContent([])))
                Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using (HttpResponseMessage bootstrap = await client.GetAsync(host.Security.BootstrapUri))
                Assert.AreEqual(HttpStatusCode.Redirect, bootstrap.StatusCode);
            client.DefaultRequestHeaders.Add("Origin", host.Security.Origin.GetLeftPart(UriPartial.Authority));
            client.DefaultRequestHeaders.Add("HX-Request", "true");
            string html = await client.GetStringAsync("/FileTools");
            Assert.IsTrue(html.Contains("not a portable encrypted attachment", StringComparison.Ordinal));
            string token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value);
            using (HttpResponseMessage missing = await client.PostAsync("/FileTools?handler=Account", new FormUrlEncodedContent([])))
                Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
            await PostAsync("Encrypt", "false");
            await PostAsync("Encrypt", "invalid-bool");
            await PostAsync("999", "true");
            await PostAsync("invalid-operation", "true");
            Assert.AreEqual(0, dialogs.InputCalls);
            Assert.IsTrue((await PostAsync("Encrypt", "true")).Contains("Output file created", StringComparison.Ordinal));
            Assert.AreEqual(1, protection.Calls);
            Assert.IsTrue(protection.LastEncrypted);
            Assert.AreEqual("synthetic account content", await File.ReadAllTextAsync(source));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "injected")));
            Assert.IsTrue((await PostAsync("Encrypt", "true")).Contains("EFS operation failed", StringComparison.Ordinal));
            dialogs.Input = encrypted;
            dialogs.Output = Path.Combine(directory, "plain");
            Assert.IsTrue((await PostAsync("Decrypt", "true")).Contains("EFS operation failed", StringComparison.Ordinal));
            protection.EncryptedSources.Add(encrypted);
            Assert.IsTrue((await PostAsync("Decrypt", "true")).Contains("Output file created", StringComparison.Ordinal));
            Assert.IsFalse(protection.LastEncrypted);
            Assert.AreEqual("synthetic account content", await File.ReadAllTextAsync(dialogs.Output));
            dialogs.Output = Path.Combine(directory, "failed");
            protection.DuringSet = () => throw new IOException(source);
            Assert.IsTrue((await PostAsync("Encrypt", "true")).Contains("EFS operation failed", StringComparison.Ordinal));
            Assert.IsFalse(File.Exists(dialogs.Output));
            dialogs.Output = null;
            Assert.IsTrue((await PostAsync("Encrypt", "true")).Contains("canceled", StringComparison.Ordinal));
            dialogs.Input = null;
            Assert.IsTrue((await PostAsync("Encrypt", "true")).Contains("canceled", StringComparison.Ordinal));
            Assert.HasCount(0, Directory.GetDirectories(directory));

            async Task<string> PostAsync(string operation, string acknowledgement)
            {
                using HttpResponseMessage response = await client.PostAsync("/FileTools?handler=Account", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token, ["AccountOperation"] = operation,
                    ["AcknowledgeAccountLimits"] = acknowledgement,
                    ["SourcePath"] = "ignored", ["DestinationPath"] = Path.Combine(directory, "injected")
                }));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.Headers.CacheControl?.NoStore);
                string result = await response.Content.ReadAsStringAsync();
                foreach (string secret in new[] { "private-source", "synthetic account content", directory })
                    Assert.IsFalse(result.Contains(secret, StringComparison.Ordinal));
                return result;
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task AccountDialogs_SerializeWithOtherFileToolsAsync()
    {
        TestFileToolDialogs dialogs = new()
        {
            InputEntered = new(TaskCreationOptions.RunContinuationsAsynchronously),
            ReleaseInput = new(TaskCreationOptions.RunContinuationsAsynchronously)
        };
        ProjectCryptoService crypto = new();
        using FileToolsService tools = new(new FileUtilityService(), dialogs, new PasswordFileService(),
            new DocumentFileService(crypto), new DocumentHmacService(crypto), new AccountFileService(new TestAccountFileProtection()), new PublicKeyFileService(crypto), new DocumentSignatureService(crypto));
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => tools.ExecuteAccountAsync(AccountFileOperation.Encrypt, true, canceled.Token));
        Assert.AreEqual(0, dialogs.InputCalls);
        Task<string> account = tools.ExecuteAccountAsync(AccountFileOperation.Encrypt, true);
        try
        {
            await dialogs.InputEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task<string> hash = tools.ExecuteAsync(FileUtilityOperation.Sha256);
            Assert.IsFalse(hash.IsCompleted);
            Assert.AreEqual(1, dialogs.InputCalls);
            dialogs.ReleaseInput.TrySetResult();
            Assert.IsTrue((await account).Contains("canceled", StringComparison.Ordinal));
            Assert.IsTrue((await hash).Contains("canceled", StringComparison.Ordinal));
            Assert.AreEqual(2, dialogs.InputCalls);
        }
        finally { dialogs.ReleaseInput.TrySetResult(); }
    }
}
