using System.Net;
using System.Text.RegularExpressions;
using TM.Desktop.Services;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class FileToolsHttpTests
{
    [TestMethod]
    public async Task Tools_OnlyUseNativePathsAndReturnSafeResultsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"TM.ToolsHttp.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            TestFileToolDialogs dialogs = new()
            {
                Input = Path.Combine(directory, "private-source.txt"),
                Output = Path.Combine(directory, "output.txt")
            };
            await File.WriteAllTextAsync(dialogs.Input, "abc");
            await using DesktopWebHost host = await DesktopWebHost.StartAsync(AppContext.BaseDirectory,
                clipboard: new TestNativeClipboard(), fileDialogs: dialogs);
            using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = host.Security.Origin };
            using (HttpResponseMessage anonymous = await client.GetAsync("/FileTools"))
                Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using (HttpResponseMessage bootstrap = await client.GetAsync(host.Security.BootstrapUri))
                Assert.AreEqual(HttpStatusCode.Redirect, bootstrap.StatusCode);
            client.DefaultRequestHeaders.Add("Origin", host.Security.Origin.GetLeftPart(UriPartial.Authority));
            client.DefaultRequestHeaders.Add("HX-Request", "true");
            string html = await client.GetStringAsync("/FileTools");
            string token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value);
            Assert.IsFalse(string.IsNullOrEmpty(token));
            using (HttpResponseMessage noToken = await client.PostAsync("/FileTools", new FormUrlEncodedContent(new Dictionary<string, string> { ["Operation"] = "Sha256" })))
                Assert.AreEqual(HttpStatusCode.BadRequest, noToken.StatusCode);
            foreach (string operation in new[] { "999", "not-an-operation" })
            {
                string invalid = await PostAsync(operation);
                Assert.IsTrue(invalid.Contains("supported file operation", StringComparison.Ordinal));
            }
            Assert.AreEqual(0, dialogs.InputCalls);
            string success = await PostAsync("Base64Encode");
            Assert.IsTrue(success.Contains("Output file created", StringComparison.Ordinal));
            Assert.AreEqual("YWJj", await File.ReadAllTextAsync(dialogs.Output));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "injected-output")));
            Assert.IsFalse(success.Contains(directory, StringComparison.Ordinal));
            Assert.IsFalse(success.Contains("private-source", StringComparison.Ordinal));
            Assert.IsFalse(success.Contains("YWJj", StringComparison.Ordinal));
            string failure = await PostAsync("Sha256");
            Assert.IsTrue(failure.Contains("operation failed", StringComparison.Ordinal));
            Assert.AreEqual("YWJj", await File.ReadAllTextAsync(dialogs.Output));
            dialogs.Input = null;
            Assert.IsTrue((await PostAsync("Sha256")).Contains("canceled", StringComparison.Ordinal));

            async Task<string> PostAsync(string operation)
            {
                using HttpResponseMessage response = await client.PostAsync("/FileTools", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token, ["Operation"] = operation,
                    ["SourcePath"] = Path.Combine(directory, "nonexistent"),
                    ["DestinationPath"] = Path.Combine(directory, "injected-output")
                }));
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsTrue(response.Headers.CacheControl?.NoStore);
                return await response.Content.ReadAsStringAsync();
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task CanceledDialogsAndToken_DoNotCreateOutputsAsync()
    {
        TestFileToolDialogs dialogs = new();
        ProjectCryptoService crypto = new();
        using FileToolsService tools = new(new FileUtilityService(), dialogs, new PasswordFileService(), new DocumentFileService(crypto), new DocumentHmacService(crypto), new AccountFileService(new TestAccountFileProtection()), new PublicKeyFileService(crypto), new DocumentSignatureService(crypto), new DocumentCertificateService(), new DocumentTransferService(crypto));
        Assert.IsTrue((await tools.ExecuteAsync(FileUtilityOperation.Sha256)).Contains("canceled", StringComparison.Ordinal));
        Assert.AreEqual(0, dialogs.OutputCalls);
        dialogs.Input = "synthetic.txt";
        Assert.IsTrue((await tools.ExecuteAsync(FileUtilityOperation.Sha256)).Contains("canceled", StringComparison.Ordinal));
        Assert.AreEqual(1, dialogs.OutputCalls);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => tools.ExecuteAsync(FileUtilityOperation.Sha256, canceled.Token));
        Assert.AreEqual(2, dialogs.InputCalls);
    }

    [TestMethod]
    public async Task SignatureVerification_UsesNativePathsAndSafeResultsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"TM.SignatureHttp.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string data = Path.Combine(directory, "private-data"), signature = Path.Combine(directory, "signature.p7c");
            await File.WriteAllTextAsync(data, "synthetic signed data");
            ProjectCryptoService crypto = new(); TM.Models.ProjectDocument document = new();
            crypto.InitializeNew(document, "synthetic-password".ToCharArray().ToSecureStringAndClear()); crypto.EnsureCaCertificate(document);
            try { await File.WriteAllBytesAsync(signature, crypto.SignFile(document, data)); }
            finally { document.Security.CaCertificate?.Dispose(); crypto.ClearAll(document); }
            TestFileToolDialogs dialogs = new(); dialogs.Inputs.Enqueue(data); dialogs.Inputs.Enqueue(signature);
            await using DesktopWebHost host = await DesktopWebHost.StartAsync(AppContext.BaseDirectory, clipboard: new TestNativeClipboard(), fileDialogs: dialogs);
            using HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false }) { BaseAddress = host.Security.Origin };
            using (HttpResponseMessage anonymous = await client.PostAsync("/FileTools?handler=VerifySignature", new FormUrlEncodedContent([]))) Assert.AreEqual(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using (HttpResponseMessage bootstrap = await client.GetAsync(host.Security.BootstrapUri)) Assert.AreEqual(HttpStatusCode.Redirect, bootstrap.StatusCode);
            client.DefaultRequestHeaders.Add("Origin", host.Security.Origin.GetLeftPart(UriPartial.Authority)); client.DefaultRequestHeaders.Add("HX-Request", "true");
            string html = await client.GetStringAsync("/FileTools");
            string token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(1)).Groups[1].Value);
            using HttpResponseMessage response = await client.PostAsync("/FileTools?handler=VerifySignature", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, ["DataPath"] = data }));
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode); string result = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(result.Contains("CMS signature is valid", StringComparison.Ordinal));
            Assert.IsFalse(result.Contains(directory, StringComparison.Ordinal)); Assert.IsFalse(result.Contains("synthetic signed data", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }
}

internal sealed class TestFileToolDialogs : IFileToolDialogs
{
    public string? Input { get; set; }
    public string? Output { get; set; }
    public Queue<string?> Inputs { get; } = new();
    public int InputCalls { get; private set; }
    public int OutputCalls { get; private set; }
    public TaskCompletionSource? InputEntered { get; set; }
    public TaskCompletionSource? ReleaseInput { get; set; }
    public async Task<string?> SelectInputAsync(CancellationToken cancellationToken = default)
    {
        InputCalls++;
        InputEntered?.TrySetResult();
        if (ReleaseInput is { } release) await release.Task.WaitAsync(cancellationToken);
        return Inputs.Count > 0 ? Inputs.Dequeue() : Input;
    }
    public Task<string?> SelectOutputAsync(string suggestedName, CancellationToken cancellationToken = default)
    {
        OutputCalls++;
        return Task.FromResult(Output);
    }
}
