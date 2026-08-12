using System.Text;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Export;

public sealed class MarkdownExporter : IExporter
{
    public MarkdownExporter(bool includeTimestamps)
    {
        IncludeTimestamps = includeTimestamps;
    }

    public bool IncludeTimestamps { get; }

    public string Format => "md";

    public string FileExtension => ".md";

    public string Export(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var builder = new StringBuilder();

        foreach (var segment in transcript.Segments)
        {
            if (string.IsNullOrWhiteSpace(segment.Text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("- ");

            if (IncludeTimestamps)
            {
                builder.Append('[');
                builder.Append(SubtitleTimestampFormatter.ToVttTimestamp(segment.StartMs));
                builder.Append("] ");
            }

            builder.Append(segment.Text.Trim());
        }

        return builder.ToString();
    }
}
