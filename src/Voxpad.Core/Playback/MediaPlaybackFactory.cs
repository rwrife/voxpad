namespace Voxpad.Core.Playback;

public static class MediaPlaybackFactory
{
    public static IMediaPlayback Create(Func<IMediaPlayback> createBackend, string fallbackReason)
    {
        ArgumentNullException.ThrowIfNull(createBackend);
        try
        {
            return createBackend();
        }
        catch (Exception ex)
        {
            var reason = string.IsNullOrWhiteSpace(fallbackReason)
                ? "Playback is unavailable."
                : fallbackReason;
            return new UnavailableMediaPlayback($"{reason} ({ex.Message})");
        }
    }
}
