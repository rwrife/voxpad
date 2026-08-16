using Voxpad.Core.Transcription;

namespace Voxpad.Core.Translation;

public sealed record TranslationStageResult(
    TranscriptDocument SourceTranscript,
    bool Success,
    bool IsDisabled,
    IReadOnlyList<TranslatedTranscriptVariant> Variants,
    IReadOnlyList<LocalizedSubtitleArtifact> SubtitleArtifacts,
    string? ErrorMessage)
{
    public static TranslationStageResult Disabled(
        TranscriptDocument sourceTranscript,
        string message = "Translation stage is disabled.")
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);
        return new TranslationStageResult(
            sourceTranscript,
            Success: false,
            IsDisabled: true,
            Variants: Array.Empty<TranslatedTranscriptVariant>(),
            SubtitleArtifacts: Array.Empty<LocalizedSubtitleArtifact>(),
            ErrorMessage: message);
    }

    public static TranslationStageResult Failure(TranscriptDocument sourceTranscript, string message)
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new TranslationStageResult(
            sourceTranscript,
            Success: false,
            IsDisabled: false,
            Variants: Array.Empty<TranslatedTranscriptVariant>(),
            SubtitleArtifacts: Array.Empty<LocalizedSubtitleArtifact>(),
            ErrorMessage: message);
    }

    public static TranslationStageResult FromVariants(
        TranscriptDocument sourceTranscript,
        IReadOnlyList<TranslatedTranscriptVariant> variants,
        IReadOnlyList<LocalizedSubtitleArtifact> subtitleArtifacts,
        string? warning = null)
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentNullException.ThrowIfNull(subtitleArtifacts);

        var copiedVariants = variants.ToArray();
        var copiedArtifacts = subtitleArtifacts.ToArray();
        var hasVariants = copiedVariants.Length > 0;

        return new TranslationStageResult(
            sourceTranscript,
            Success: hasVariants && string.IsNullOrWhiteSpace(warning),
            IsDisabled: false,
            Variants: copiedVariants,
            SubtitleArtifacts: copiedArtifacts,
            ErrorMessage: string.IsNullOrWhiteSpace(warning) ? null : warning);
    }
}
