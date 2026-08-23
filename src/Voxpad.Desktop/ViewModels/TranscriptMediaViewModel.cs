using System.Collections.ObjectModel;
using System.Windows.Input;
using Voxpad.Core.Playback;
using Voxpad.Core.Transcription;
using Voxpad.Desktop.Infrastructure;

namespace Voxpad.Desktop.ViewModels;

public sealed class TranscriptMediaViewModel : ViewModelBase, IDisposable
{
    private readonly IMediaPlayback mediaPlayback;
    private readonly TranscriptMediaSession session;
    private readonly Action<TranscriptDocument>? transcriptChanged;
    private readonly SynchronizationContext? uiContext;
    private string? errorMessage;
    private bool requiresDiscardConfirmation;

    public TranscriptMediaViewModel(
        IMediaPlayback mediaPlayback,
        Action<TranscriptDocument>? transcriptChanged = null)
    {
        this.mediaPlayback = mediaPlayback ?? throw new ArgumentNullException(nameof(mediaPlayback));
        session = new TranscriptMediaSession(mediaPlayback);
        this.transcriptChanged = transcriptChanged;
        uiContext = SynchronizationContext.Current;
        mediaPlayback.PlaybackChanged += OnPlaybackChanged;
        TogglePlaybackCommand = new AsyncCommand(() => TogglePlaybackAsync());
    }

    public ObservableCollection<TranscriptSegmentViewModel> Segments { get; } = [];

    public ICommand TogglePlaybackCommand { get; }

    public bool HasUnsavedChanges => session.HasUnsavedChanges;

    public bool IsPlaybackAvailable => mediaPlayback.IsAvailable;

    public string PlaybackAvailability => mediaPlayback.IsAvailable
        ? "Playback ready"
        : mediaPlayback.UnavailabilityReason ?? "Playback is unavailable.";

    public string PlaybackStateLabel => mediaPlayback.State.ToString();

    public string PlayPauseLabel => mediaPlayback.State == MediaPlaybackState.Playing ? "Pause" : "Play";

    public string PositionLabel => FormatPosition(mediaPlayback.Position);

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool RequiresDiscardConfirmation
    {
        get => requiresDiscardConfirmation;
        private set => SetProperty(ref requiresDiscardConfirmation, value);
    }

    public string MediaName => mediaPlayback.MediaPath is null
        ? "No media loaded"
        : Path.GetFileName(mediaPlayback.MediaPath);

    public async Task<bool> LoadMediaAsync(
        string mediaPath,
        bool discardUnsavedChanges,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ErrorMessage = null;
            var loaded = await session.TryLoadMediaAsync(mediaPath, discardUnsavedChanges, cancellationToken);
            RequiresDiscardConfirmation = !loaded;
            if (!loaded)
            {
                return false;
            }

            PopulateSegments(session.CurrentTranscript);
            if (session.HasTranscript)
            {
                transcriptChanged?.Invoke(session.CurrentTranscript);
            }

            RaisePropertyChanged(nameof(MediaName));
            RaisePropertyChanged(nameof(HasUnsavedChanges));
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            RequiresDiscardConfirmation = false;
            ErrorMessage = $"Unable to load media: {ex.Message}";
            return false;
        }
    }

    public async Task TogglePlaybackAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ErrorMessage = null;
            if (!mediaPlayback.IsAvailable)
            {
                ErrorMessage = mediaPlayback.UnavailabilityReason ?? "Playback is unavailable.";
                return;
            }

            if (mediaPlayback.MediaPath is null)
            {
                ErrorMessage = "Load a media file before starting playback.";
                return;
            }

            if (mediaPlayback.State == MediaPlaybackState.Playing)
            {
                await mediaPlayback.PauseAsync(cancellationToken);
            }
            else
            {
                await mediaPlayback.PlayAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to control playback: {ex.Message}";
        }
    }

    public async Task SeekToSegmentAsync(
        TranscriptSegmentViewModel segment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(segment);
        try
        {
            ErrorMessage = null;
            await session.SeekToSegmentAsync(segment.Index, cancellationToken);
            RaisePropertyChanged(nameof(PositionLabel));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unable to seek media: {ex.Message}";
        }
    }

    public void LoadTranscript(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        session.LoadTranscript(transcript);
        PopulateSegments(transcript);
        RaisePropertyChanged(nameof(HasUnsavedChanges));
    }

    private void PopulateSegments(TranscriptDocument transcript)
    {
        Segments.Clear();
        for (var i = 0; i < transcript.Segments.Count; i++)
        {
            var segment = transcript.Segments[i];
            var index = i;
            Segments.Add(new TranscriptSegmentViewModel(
                index,
                segment.Text,
                segment.StartMs,
                segment.EndMs,
                text => UpdateSegment(index, text)));
        }

        RaisePropertyChanged(nameof(HasUnsavedChanges));
    }

    public void Dispose()
    {
        mediaPlayback.PlaybackChanged -= OnPlaybackChanged;
        if (mediaPlayback is IDisposable disposable)
        {
            disposable.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void UpdateSegment(int index, string text)
    {
        session.EditSegment(index, text);
        RaisePropertyChanged(nameof(HasUnsavedChanges));
        transcriptChanged?.Invoke(session.CurrentTranscript);
    }

    private void OnPlaybackChanged(object? sender, EventArgs e)
    {
        if (uiContext is not null && SynchronizationContext.Current != uiContext)
        {
            uiContext.Post(_ => RaisePlaybackPropertiesChanged(), null);
            return;
        }

        RaisePlaybackPropertiesChanged();
    }

    private void RaisePlaybackPropertiesChanged()
    {
        RaisePropertyChanged(nameof(PositionLabel));
        RaisePropertyChanged(nameof(PlaybackStateLabel));
        RaisePropertyChanged(nameof(PlayPauseLabel));
        RaisePropertyChanged(nameof(MediaName));
    }

    private static string FormatPosition(TimeSpan position) =>
        $"{(int)position.TotalMinutes:00}:{position.Seconds:00}.{position.Milliseconds:000}";
}
