using System.Globalization;
using System.Text;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Export;

public sealed class SrtExporter : IExporter
{
    public string Format => "srt";

    public string FileExtension => ".srt";

    public string Export(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var builder = new StringBuilder();
        var segments = transcript.Segments
            .Where(static s => !string.IsNullOrWhiteSpace(s.Text))
            .ToArray();

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];

            builder.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            builder.Append(SubtitleTimestampFormatter.ToSrtTimestamp(segment.StartMs));
            builder.Append(" --> ");
            builder.AppendLine(SubtitleTimestampFormatter.ToSrtTimestamp(segment.EndMs));
            builder.AppendLine(segment.Text.Trim());

            if (i < segments.Length - 1)
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}
