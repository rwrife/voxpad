using Voxpad.Core.Models;

namespace Voxpad.Core.Tests.Models;

public sealed class ModelCachePathResolverTests
{
    [Fact]
    public void Resolve_Windows_UsesAppDataDirectory()
    {
        var path = ModelCachePathResolver.Resolve(VoxpadPlatform.Windows, windowsAppDataDirectory: @"C:\Users\me\AppData\Roaming");

        Assert.Equal("C:/Users/me/AppData/Roaming/voxpad/models", NormalizePath(path));
    }

    [Fact]
    public void Resolve_MacOs_UsesUserHomeDirectory()
    {
        var path = ModelCachePathResolver.Resolve(VoxpadPlatform.MacOS, userHomeDirectory: "/Users/me");

        Assert.Equal("/Users/me/Library/Application Support/voxpad/models", NormalizePath(path));
    }

    [Fact]
    public void Resolve_ThrowsForMissingWindowsAppDataDirectory()
    {
        Assert.ThrowsAny<ArgumentException>(() => ModelCachePathResolver.Resolve(VoxpadPlatform.Windows));
    }

    [Fact]
    public void Resolve_ThrowsForMissingMacHomeDirectory()
    {
        Assert.ThrowsAny<ArgumentException>(() => ModelCachePathResolver.Resolve(VoxpadPlatform.MacOS));
    }

    private static string NormalizePath(string path) => path.Replace('\\', '/');
}
