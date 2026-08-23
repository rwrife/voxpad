namespace Voxpad.Core.Playback;

public sealed class UnavailableMediaPlayback : IMediaPlayback
{
    public UnavailableMediaPlayback(string reason)
    {
        UnavailabilityReason = string.IsNullOrWhiteSpace(reason)
            ? "Playback is unavailable."
            : reason;
    }

    public bool IsAvailable => false;

    public string UnavailabilityReason { get; }

    public string? MediaPath => null;

    public TimeSpan Position => TimeSpan.Zero;

    public TimeSpan? Duration => null;

    public MediaPlaybackState State => MediaPlaybackState.Stopped;

    public event EventHandler? PlaybackChanged
    {
        add { }
        remove { }
    }

    public Task LoadAsync(string mediaPath, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException(UnavailabilityReason));

    public Task PlayAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException(UnavailabilityReason));

    public Task PauseAsync(CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException(UnavailabilityReason));

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) =>
        Task.FromException(new InvalidOperationException(UnavailabilityReason));
}
