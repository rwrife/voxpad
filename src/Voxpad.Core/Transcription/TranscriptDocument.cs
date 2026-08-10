namespace Voxpad.Core.Transcription;

public sealed class TranscriptDocument
{
    public TranscriptDocument(IReadOnlyList<TranscriptSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ValidateTimestampOrdering(segments);
        Segments = segments;
    }

    public IReadOnlyList<TranscriptSegment> Segments { get; }

    public static TranscriptDocument FromSegments(IEnumerable<TranscriptSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var ordered = segments.OrderBy(s => s.StartMs).ToArray();
        return new TranscriptDocument(ordered);
    }

    private static void ValidateTimestampOrdering(IReadOnlyList<TranscriptSegment> segments)
    {
        long? previousEnd = null;

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];

            if (segment.EndMs < segment.StartMs)
            {
                throw new InvalidDataException($"Segment {i} has EndMs < StartMs.");
            }

            if (previousEnd is not null && segment.StartMs < previousEnd.Value)
            {
                throw new InvalidDataException($"Segment {i} overlaps with the previous segment.");
            }

            previousEnd = segment.EndMs;
        }
    }
}
