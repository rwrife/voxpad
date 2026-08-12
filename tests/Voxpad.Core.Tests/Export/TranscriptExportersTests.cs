using Voxpad.Core.Export;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Tests.Export;

public sealed class TranscriptExportersTests
{
    [Fact]
    public void PlainTextExporter_ProducesPlainTextLines()
    {
        var exporter = new PlainTextExporter();

        var output = exporter.Export(CreateSampleDocument());

        Assert.Equal(
            "Hello from voxpad." + Environment.NewLine + "Second line.",
            output);
    }

    [Fact]
    public void MarkdownExporter_SupportsOptionalTimestampPrefixes()
    {
        var transcript = CreateSampleDocument();
        var withoutTimestamps = new MarkdownExporter(includeTimestamps: false);
        var withTimestamps = new MarkdownExporter(includeTimestamps: true);

        var withoutTimestampOutput = withoutTimestamps.Export(transcript);
        var withTimestampOutput = withTimestamps.Export(transcript);

        Assert.Equal(
            "- Hello from voxpad." + Environment.NewLine + "- Second line.",
            withoutTimestampOutput);

        Assert.Equal(
            "- [00:00:00.000] Hello from voxpad." + Environment.NewLine + "- [00:00:02.500] Second line.",
            withTimestampOutput);
    }

    [Fact]
    public void SrtExporter_ProducesSequentiallyNumberedCuesWithSrtTimestamps()
    {
        var exporter = new SrtExporter();

        var output = exporter.Export(CreateSampleDocument());

        var expected =
            "1" + Environment.NewLine +
            "00:00:00,000 --> 00:00:01,250" + Environment.NewLine +
            "Hello from voxpad." + Environment.NewLine +
            Environment.NewLine +
            "2" + Environment.NewLine +
            "00:00:02,500 --> 00:00:04,000" + Environment.NewLine +
            "Second line." + Environment.NewLine;

        Assert.Equal(expected, output);
    }

    [Fact]
    public void VttExporter_ProducesWebVttHeaderAndVttTimestamps()
    {
        var exporter = new VttExporter();

        var output = exporter.Export(CreateSampleDocument());

        var expected =
            "WEBVTT" + Environment.NewLine +
            Environment.NewLine +
            "00:00:00.000 --> 00:00:01.250" + Environment.NewLine +
            "Hello from voxpad." + Environment.NewLine +
            Environment.NewLine +
            "00:00:02.500 --> 00:00:04.000" + Environment.NewLine +
            "Second line." + Environment.NewLine;

        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void SubtitleExporters_ThrowOnNegativeTimestamps(long negativeTimestamp)
    {
        var transcript = TranscriptDocument.FromSegments(
            new[]
            {
                new TranscriptSegment("bad", negativeTimestamp, 10)
            });

        var srt = new SrtExporter();
        var vtt = new VttExporter();

        Assert.Throws<ArgumentOutOfRangeException>(() => srt.Export(transcript));
        Assert.Throws<ArgumentOutOfRangeException>(() => vtt.Export(transcript));
    }

    private static TranscriptDocument CreateSampleDocument()
    {
        return TranscriptDocument.FromSegments(
            new[]
            {
                new TranscriptSegment("  Hello from voxpad. ", 0, 1250),
                new TranscriptSegment("", 1500, 1700),
                new TranscriptSegment("Second line.", 2500, 4000)
            });
    }
}
