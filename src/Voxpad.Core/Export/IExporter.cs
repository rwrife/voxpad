using Voxpad.Core.Transcription;

namespace Voxpad.Core.Export;

public interface IExporter
{
    string Format { get; }

    string FileExtension { get; }

    string Export(TranscriptDocument transcript);
}
