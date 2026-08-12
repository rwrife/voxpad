using System.Text;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Export;

public sealed class VttExporter : IExporter
{
    public string Format => "vtt";

    public string FileExtension => ".vtt";

    public string Export(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var segments = transcript.Segments
            .Where(static s => !string.IsNullOrWhiteSpace(s.Text))
            .ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("WEBVTT");

        if (segments.Length > 0)
        {
            builder.AppendLine();
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            builder.Append(SubtitleTimestampFormatter.ToVttTimestamp(segment.StartMs));
            builder.Append(" --> ");
            builder.AppendLine(SubtitleTimestampFormatter.ToVttTimestamp(segment.EndMs));
            builder.AppendLine(segment.Text.Trim());

            if (i < segments.Length - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
