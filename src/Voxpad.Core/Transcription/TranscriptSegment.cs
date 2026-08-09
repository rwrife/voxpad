namespace Voxpad.Core.Transcription;

public sealed class TranscriptSegment
{
    public TranscriptSegment(string text, long startMs, long endMs, IReadOnlyList<TranscriptWord>? words = null)
    {
        if (endMs < startMs)
        {
            throw new ArgumentException("Segment end timestamp must be >= start timestamp.", nameof(endMs));
        }

        Text = text ?? string.Empty;
        StartMs = startMs;
        EndMs = endMs;
        Words = words ?? Array.Empty<TranscriptWord>();
    }

    public string Text { get; }

    public long StartMs { get; }

    public long EndMs { get; }

    public IReadOnlyList<TranscriptWord> Words { get; }
}
