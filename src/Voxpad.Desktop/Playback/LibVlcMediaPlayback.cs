using LibVLCSharp.Shared;
using Voxpad.Core.Playback;

namespace Voxpad.Desktop.Playback;

public sealed class LibVlcMediaPlayback : IMediaPlayback, IDisposable
{
    private readonly LibVLC libVlc;
    private readonly MediaPlayer player;
    private Media? media;
    private string? mediaPath;
    private MediaPlaybackState state;
    private bool disposed;

    public LibVlcMediaPlayback()
    {
        var nativeLibraryPath = FindNativeLibraryPath();
        if (nativeLibraryPath is null)
        {
            LibVLCSharp.Shared.Core.Initialize();
        }
        else
        {
            LibVLCSharp.Shared.Core.Initialize(nativeLibraryPath);
        }

        libVlc = new LibVLC("--no-video-title-show", "--quiet");
        player = new MediaPlayer(libVlc);
        player.TimeChanged += (_, _) => PlaybackChanged?.Invoke(this, EventArgs.Empty);
        player.LengthChanged += (_, _) => PlaybackChanged?.Invoke(this, EventArgs.Empty);
        player.Playing += (_, _) => SetState(MediaPlaybackState.Playing);
        player.Paused += (_, _) => SetState(MediaPlaybackState.Paused);
        player.Stopped += (_, _) => SetState(MediaPlaybackState.Stopped);
        player.EndReached += (_, _) => SetState(MediaPlaybackState.Stopped);
        player.EncounteredError += (_, _) => SetState(MediaPlaybackState.Stopped);
    }

    public bool IsAvailable => !disposed;

    public string? UnavailabilityReason => disposed ? "The playback engine has been disposed." : null;

    public string? MediaPath => mediaPath;

    public TimeSpan Position => TimeSpan.FromMilliseconds(Math.Max(0, player.Time));

    public TimeSpan? Duration => player.Length > 0
        ? TimeSpan.FromMilliseconds(player.Length)
        : null;

    public MediaPlaybackState State => state;

    public event EventHandler? PlaybackChanged;

    public Task LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The selected media file does not exist.", fullPath);
        }

        player.Stop();
        var replacement = new Media(libVlc, new Uri(fullPath));
        var previous = media;
        player.Media = replacement;
        media = replacement;
        mediaPath = fullPath;
        previous?.Dispose();
        SetState(MediaPlaybackState.Stopped);
        return Task.CompletedTask;
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMediaLoaded();
        if (!player.Play())
        {
            throw new InvalidOperationException("The VLC playback engine could not start the selected media.");
        }

        SetState(MediaPlaybackState.Playing);
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMediaLoaded();
        player.Pause();
        SetState(MediaPlaybackState.Paused);
        return Task.CompletedTask;
    }

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMediaLoaded();

        var requestedMs = Math.Max(0, (long)position.TotalMilliseconds);
        if (player.Length > 0)
        {
            requestedMs = Math.Min(requestedMs, player.Length);
        }

        player.Time = requestedMs;
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        player.Stop();
        media?.Dispose();
        player.Dispose();
        libVlc.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string? FindNativeLibraryPath()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var candidates = new[]
        {
            "/Applications/VLC.app/Contents/MacOS/lib",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Applications/VLC.app/Contents/MacOS/lib")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private void EnsureMediaLoaded()
    {
        if (media is null)
        {
            throw new InvalidOperationException("Load a media file before controlling playback.");
        }
    }

    private void SetState(MediaPlaybackState newState)
    {
        state = newState;
        PlaybackChanged?.Invoke(this, EventArgs.Empty);
    }
}
