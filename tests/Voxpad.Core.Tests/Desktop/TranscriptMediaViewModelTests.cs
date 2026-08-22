using Voxpad.Core.Playback;
using Voxpad.Core.Transcription;
using Voxpad.Desktop.ViewModels;

namespace Voxpad.Core.Tests.Desktop;

public sealed class TranscriptMediaViewModelTests
{
    [Fact]
    public void EditingSegment_UpdatesCanonicalTranscriptWithoutChangingTimestamps()
    {
        var playback = new FakeMediaPlayback();
        TranscriptDocument? synchronized = null;
        var viewModel = new TranscriptMediaViewModel(playback, transcript => synchronized = transcript);
        viewModel.LoadTranscript(TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("Hello", 100, 900),
            new TranscriptSegment("world", 1_200, 2_300)
        ]));

        viewModel.Segments[0].Text = "Hello there";

        Assert.NotNull(synchronized);
        Assert.Equal("Hello there", synchronized.Segments[0].Text);
        Assert.Equal((100L, 900L), (synchronized.Segments[0].StartMs, synchronized.Segments[0].EndMs));
        Assert.Equal((1_200L, 2_300L), (synchronized.Segments[1].StartMs, synchronized.Segments[1].EndMs));
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task SeekToSegmentAsync_SeeksLoadedMediaAndPublishesCurrentPosition()
    {
        var playback = new FakeMediaPlayback();
        var viewModel = new TranscriptMediaViewModel(playback);
        viewModel.LoadTranscript(TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("First", 100, 900),
            new TranscriptSegment("Second", 1_200, 2_300)
        ]));

        await viewModel.SeekToSegmentAsync(viewModel.Segments[1]);

        Assert.Equal(TimeSpan.FromMilliseconds(1_200), playback.Position);
        Assert.Equal("00:01.200", viewModel.PositionLabel);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task LoadMediaAsync_WithUnsavedEdits_RequestsConfirmationAndPreservesSessionUntilConfirmed()
    {
        var playback = new FakeMediaPlayback();
        var viewModel = new TranscriptMediaViewModel(playback);
        viewModel.LoadTranscript(TranscriptDocument.FromSegments([new TranscriptSegment("Original", 0, 1_000)]));
        viewModel.Segments[0].Text = "Unsaved edit";

        var loaded = await viewModel.LoadMediaAsync("replacement.wav", discardUnsavedChanges: false);

        Assert.False(loaded);
        Assert.True(viewModel.RequiresDiscardConfirmation);
        Assert.Null(playback.MediaPath);
        Assert.Equal("Unsaved edit", viewModel.Segments[0].Text);

        loaded = await viewModel.LoadMediaAsync("replacement.wav", discardUnsavedChanges: true);

        Assert.True(loaded);
        Assert.False(viewModel.RequiresDiscardConfirmation);
        Assert.Equal("replacement.wav", viewModel.MediaName);
        Assert.Equal("Original", viewModel.Segments[0].Text);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public async Task LoadMediaAsync_WithoutTimestampedTranscript_DoesNotOverwritePlainTextSource()
    {
        var playback = new FakeMediaPlayback();
        var synchronizationCalls = 0;
        var viewModel = new TranscriptMediaViewModel(playback, _ => synchronizationCalls++);

        var loaded = await viewModel.LoadMediaAsync("sample.wav", discardUnsavedChanges: true);

        Assert.True(loaded);
        Assert.Equal(0, synchronizationCalls);
        Assert.Empty(viewModel.Segments);
    }

    [Fact]
    public async Task TogglePlaybackAsync_ExposesPlayPauseState()
    {
        var playback = new FakeMediaPlayback();
        var viewModel = new TranscriptMediaViewModel(playback);
        await viewModel.LoadMediaAsync("sample.wav", discardUnsavedChanges: true);

        await viewModel.TogglePlaybackAsync();

        Assert.Equal(MediaPlaybackState.Playing, playback.State);
        Assert.Equal("Playing", viewModel.PlaybackStateLabel);
        Assert.Equal("Pause", viewModel.PlayPauseLabel);

        await viewModel.TogglePlaybackAsync();

        Assert.Equal(MediaPlaybackState.Paused, playback.State);
        Assert.Equal("Paused", viewModel.PlaybackStateLabel);
        Assert.Equal("Play", viewModel.PlayPauseLabel);
    }

    [Fact]
    public async Task PlaybackUnavailable_DoesNotBlockOrDiscardTranscriptEditing()
    {
        var viewModel = new TranscriptMediaViewModel(new UnavailableMediaPlayback("Install VLC."));
        viewModel.LoadTranscript(TranscriptDocument.FromSegments([new TranscriptSegment("Original", 0, 1_000)]));
        viewModel.Segments[0].Text = "Edited offline";

        var loaded = await viewModel.LoadMediaAsync("sample.wav", discardUnsavedChanges: true);
        await viewModel.TogglePlaybackAsync();

        Assert.False(loaded);
        Assert.Equal("Edited offline", viewModel.Segments[0].Text);
        Assert.True(viewModel.HasUnsavedChanges);
        Assert.Contains("Install VLC", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsPlaybackAvailable);
    }

    private sealed class FakeMediaPlayback : IMediaPlayback
    {
        public bool IsAvailable => true;

        public string? UnavailabilityReason => null;

        public string? MediaPath { get; private set; }

        public TimeSpan Position { get; private set; }

        public TimeSpan? Duration { get; private set; }

        public MediaPlaybackState State { get; private set; }

        public event EventHandler? PlaybackChanged;

        public Task LoadAsync(string mediaPath, CancellationToken cancellationToken = default)
        {
            MediaPath = mediaPath;
            State = MediaPlaybackState.Stopped;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken cancellationToken = default)
        {
            State = MediaPlaybackState.Playing;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            State = MediaPlaybackState.Paused;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            Position = position;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
