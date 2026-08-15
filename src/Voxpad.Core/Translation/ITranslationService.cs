using Voxpad.Core.Transcription;

namespace Voxpad.Core.Translation;

public interface ITranslationService
{
    TranslationSettings Settings { get; }

    Task<TranslationStageResult> TranslateAsync(
        TranscriptDocument sourceTranscript,
        IReadOnlyList<string> targetLanguages,
        CancellationToken cancellationToken = default);
}
