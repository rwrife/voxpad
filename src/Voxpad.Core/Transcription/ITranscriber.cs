namespace Voxpad.Core.Transcription;

public interface ITranscriber
{
    Task<TranscriptDocument> TranscribeAsync(
        string audioPath,
        WhisperTranscriptionOptions options,
        CancellationToken cancellationToken = default);
}
