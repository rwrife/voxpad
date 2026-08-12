using System.Text;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Export;

public sealed class PlainTextExporter : IExporter
{
    public string Format => "txt";

    public string FileExtension => ".txt";

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

            builder.Append(segment.Text.Trim());
        }

        return builder.ToString();
    }
}
