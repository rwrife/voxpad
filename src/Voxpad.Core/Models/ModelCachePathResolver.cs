namespace Voxpad.Core.Models;

public enum VoxpadPlatform
{
    Windows,
    MacOS
}

public static class ModelCachePathResolver
{
    public static string ResolveForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Resolve(VoxpadPlatform.Windows, appData);
        }

        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Resolve(VoxpadPlatform.MacOS, userHomeDirectory: home);
        }

        throw new PlatformNotSupportedException("voxpad currently supports only Windows and macOS model cache paths.");
    }

    public static string Resolve(
        VoxpadPlatform platform,
        string? windowsAppDataDirectory = null,
        string? userHomeDirectory = null)
    {
        return platform switch
        {
            VoxpadPlatform.Windows => ResolveWindowsPath(windowsAppDataDirectory),
            VoxpadPlatform.MacOS => ResolveMacPath(userHomeDirectory),
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, "Unknown platform.")
        };
    }

    private static string ResolveWindowsPath(string? windowsAppDataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowsAppDataDirectory);
        return Path.Combine(windowsAppDataDirectory, "voxpad", "models");
    }

    private static string ResolveMacPath(string? userHomeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userHomeDirectory);
        return Path.Combine(userHomeDirectory, "Library", "Application Support", "voxpad", "models");
    }
}
