namespace Voxpad.Core.Playback;

public interface IMediaPlayback
{
    bool IsAvailable { get; }

    string? UnavailabilityReason { get; }

    string? MediaPath { get; }

    TimeSpan Position { get; }

    TimeSpan? Duration { get; }

    MediaPlaybackState State { get; }

    event EventHandler? PlaybackChanged;

    Task LoadAsync(string mediaPath, CancellationToken cancellationToken = default);

    Task PlayAsync(CancellationToken cancellationToken = default);

    Task PauseAsync(CancellationToken cancellationToken = default);

    Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
}
