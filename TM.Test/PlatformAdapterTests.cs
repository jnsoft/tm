using TM.Desktop.Services;
using TM.Services;

namespace TM.Test;

[TestClass]
public sealed class PlatformAdapterTests
{
    [TestMethod]
    public async Task UnsupportedClipboard_RefusesSecretCopiesWithoutSchedulingCleanupAsync()
    {
        UnsupportedNativeClipboard clipboard = new("Linux");

        PlatformNotSupportedException error = await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => clipboard.WriteAsync("secret"));

        StringAssert.Contains(error.Message, "Linux");
        Assert.IsTrue(await clipboard.ClearIfOwnedAsync(1));
    }

}
