namespace Voxpad.Core.Transcription.Backends;

internal interface IWhisperBackend
{
    string Name { get; }

    bool IsAvailable(WhisperTranscriptionOptions options);

    Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        WhisperTranscriptionRequest request,
        CancellationToken cancellationToken);
}
