using Voxpad.Core.Transcription;

namespace Voxpad.Core.Playback;

public sealed class TranscriptMediaSession
{
    private readonly IMediaPlayback mediaPlayback;
    private TranscriptDocument currentTranscript = TranscriptDocument.FromSegments([]);
    private TranscriptDocument? originalTranscript;

    public TranscriptMediaSession(IMediaPlayback mediaPlayback)
    {
        this.mediaPlayback = mediaPlayback ?? throw new ArgumentNullException(nameof(mediaPlayback));
    }

    public TranscriptDocument CurrentTranscript => currentTranscript;

    public bool HasTranscript => originalTranscript is not null;

    public bool HasUnsavedChanges { get; private set; }

    public void LoadTranscript(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        originalTranscript = transcript;
        currentTranscript = transcript;
        HasUnsavedChanges = false;
    }

    public async Task<bool> TryLoadMediaAsync(
        string mediaPath,
        bool discardUnsavedChanges,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        if (HasUnsavedChanges && !discardUnsavedChanges)
        {
            return false;
        }

        await mediaPlayback.LoadAsync(mediaPath, cancellationToken);
        if (HasUnsavedChanges && originalTranscript is not null)
        {
            LoadTranscript(originalTranscript);
        }

        return true;
    }

    public async Task<bool> TryLoadAsync(
        string mediaPath,
        TranscriptDocument transcript,
        bool discardUnsavedChanges,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaPath);
        ArgumentNullException.ThrowIfNull(transcript);

        if (HasUnsavedChanges && !discardUnsavedChanges)
        {
            return false;
        }

        await mediaPlayback.LoadAsync(mediaPath, cancellationToken);
        LoadTranscript(transcript);
        return true;
    }

    public async Task SeekToSegmentAsync(int segmentIndex, CancellationToken cancellationToken = default)
    {
        if (segmentIndex < 0 || segmentIndex >= currentTranscript.Segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        }

        await mediaPlayback.SeekAsync(
            TimeSpan.FromMilliseconds(currentTranscript.Segments[segmentIndex].StartMs),
            cancellationToken);
    }

    public void EditSegment(int segmentIndex, string text)
    {
        if (segmentIndex < 0 || segmentIndex >= currentTranscript.Segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        }

        var segments = currentTranscript.Segments.ToArray();
        var original = segments[segmentIndex];
        segments[segmentIndex] = new TranscriptSegment(
            text ?? string.Empty,
            original.StartMs,
            original.EndMs,
            original.Words);
        currentTranscript = TranscriptDocument.FromSegments(segments);
        HasUnsavedChanges = originalTranscript is not null &&
            !originalTranscript.Segments.Select(static segment => segment.Text)
                .SequenceEqual(currentTranscript.Segments.Select(static segment => segment.Text), StringComparer.Ordinal);
    }
}
