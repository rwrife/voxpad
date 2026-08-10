using Voxpad.Core.Transcription;

namespace Voxpad.Core.Tests.Transcription;

public sealed class TranscriptDocumentTests
{
    [Fact]
    public void Constructor_Throws_WhenSegmentsOverlap()
    {
        var segments = new[]
        {
            new TranscriptSegment("a", 0, 1000),
            new TranscriptSegment("b", 900, 1500)
        };

        Assert.Throws<InvalidDataException>(() => new TranscriptDocument(segments));
    }

    [Fact]
    public void FromSegments_SortsByStartTimestamp()
    {
        var segments = new[]
        {
            new TranscriptSegment("b", 1000, 1500),
            new TranscriptSegment("a", 0, 900)
        };

        var doc = TranscriptDocument.FromSegments(segments);

        Assert.Equal("a", doc.Segments[0].Text);
        Assert.Equal("b", doc.Segments[1].Text);
    }
}
