using Voxpad.Core.Playback;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Tests.Playback;

public sealed class TranscriptMediaSessionTests
{
    [Fact]
    public void MediaPlaybackFactory_WhenNativeBackendFails_ReturnsUnavailablePlayback()
    {
        var playback = MediaPlaybackFactory.Create(
            () => throw new DllNotFoundException("libvlc not found"),
            "Install VLC to enable playback.");

        var unavailable = Assert.IsType<UnavailableMediaPlayback>(playback);
        Assert.False(unavailable.IsAvailable);
        Assert.Contains("Install VLC", unavailable.UnavailabilityReason, StringComparison.Ordinal);
    }

    [Fact]
    public void EditSegment_PreservesOriginalTimingAndWordMetadata()
    {
        var words = new[] { new TranscriptWord("Hello", 120, 520) };
        var original = TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("Hello", 100, 900, words),
            new TranscriptSegment("world", 1_200, 2_300)
        ]);
        var session = new TranscriptMediaSession(new FakeMediaPlayback());
        session.LoadTranscript(original);

        session.EditSegment(0, "Hello there");

        var edited = session.CurrentTranscript;
        Assert.Equal("Hello there", edited.Segments[0].Text);
        Assert.Equal((100L, 900L), (edited.Segments[0].StartMs, edited.Segments[0].EndMs));
        Assert.Same(words, edited.Segments[0].Words);
        Assert.Equal((1_200L, 2_300L), (edited.Segments[1].StartMs, edited.Segments[1].EndMs));
        Assert.True(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task SeekToSegmentAsync_SeeksPlaybackToSegmentStart()
    {
        var playback = new FakeMediaPlayback();
        var session = new TranscriptMediaSession(playback);
        session.LoadTranscript(TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("First", 100, 900),
            new TranscriptSegment("Second", 1_200, 2_300)
        ]));

        await session.SeekToSegmentAsync(1);

        Assert.Equal(TimeSpan.FromMilliseconds(1_200), playback.Position);
    }

    [Fact]
    public async Task TryLoadAsync_WhenTranscriptHasEdits_RequiresExplicitDiscardBeforeReplacingSession()
    {
        var playback = new FakeMediaPlayback();
        var session = new TranscriptMediaSession(playback);
        var first = TranscriptDocument.FromSegments([new TranscriptSegment("First", 0, 1_000)]);
        var second = TranscriptDocument.FromSegments([new TranscriptSegment("Second", 0, 2_000)]);
        await session.TryLoadAsync("first.wav", first, discardUnsavedChanges: true);
        session.EditSegment(0, "Edited first");

        var replaced = await session.TryLoadAsync("second.wav", second, discardUnsavedChanges: false);

        Assert.False(replaced);
        Assert.Equal("first.wav", playback.MediaPath);
        Assert.Equal("Edited first", Assert.Single(session.CurrentTranscript.Segments).Text);

        replaced = await session.TryLoadAsync("second.wav", second, discardUnsavedChanges: true);

        Assert.True(replaced);
        Assert.Equal("second.wav", playback.MediaPath);
        Assert.Equal("Second", Assert.Single(session.CurrentTranscript.Segments).Text);
        Assert.False(session.HasUnsavedChanges);
    }

    [Fact]
    public async Task TryLoadMediaAsync_WhenDiscardIsConfirmed_RestoresOriginalTranscript()
    {
        var playback = new FakeMediaPlayback();
        var session = new TranscriptMediaSession(playback);
        session.LoadTranscript(TranscriptDocument.FromSegments([new TranscriptSegment("Original", 0, 1_000)]));
        session.EditSegment(0, "Unsaved edit");

        var loaded = await session.TryLoadMediaAsync("replacement.wav", discardUnsavedChanges: false);

        Assert.False(loaded);
        Assert.Null(playback.MediaPath);
        Assert.Equal("Unsaved edit", Assert.Single(session.CurrentTranscript.Segments).Text);

        loaded = await session.TryLoadMediaAsync("replacement.wav", discardUnsavedChanges: true);

        Assert.True(loaded);
        Assert.Equal("replacement.wav", playback.MediaPath);
        Assert.Equal("Original", Assert.Single(session.CurrentTranscript.Segments).Text);
        Assert.False(session.HasUnsavedChanges);
    }

    private sealed class FakeMediaPlayback : IMediaPlayback
    {
        public bool IsAvailable => true;

        public string? UnavailabilityReason => null;

        public string? MediaPath { get; private set; }

        public TimeSpan Position { get; private set; }

        public TimeSpan? Duration => null;

        public MediaPlaybackState State => MediaPlaybackState.Stopped;

        public event EventHandler? PlaybackChanged;

        public Task LoadAsync(string mediaPath, CancellationToken cancellationToken = default)
        {
            MediaPath = mediaPath;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public Task PlayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        {
            Position = position;
            PlaybackChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
