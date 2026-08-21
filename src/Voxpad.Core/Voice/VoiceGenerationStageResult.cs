using Voxpad.Core.Transcription;

namespace Voxpad.Core.Voice;

public sealed record VoiceGenerationStageResult(
    TranscriptDocument SourceTranscript,
    bool Success,
    bool IsDisabled,
    IReadOnlyList<VoiceGenerationArtifact> Artifacts,
    string? ErrorMessage)
{
    public static VoiceGenerationStageResult Disabled(
        TranscriptDocument sourceTranscript,
        string message = "Voice generation stage is disabled.")
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);

        return new VoiceGenerationStageResult(
            sourceTranscript,
            Success: false,
            IsDisabled: true,
            Artifacts: Array.Empty<VoiceGenerationArtifact>(),
            ErrorMessage: message);
    }

    public static VoiceGenerationStageResult Failure(TranscriptDocument sourceTranscript, string message)
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new VoiceGenerationStageResult(
            sourceTranscript,
            Success: false,
            IsDisabled: false,
            Artifacts: Array.Empty<VoiceGenerationArtifact>(),
            ErrorMessage: message);
    }

    public static VoiceGenerationStageResult FromArtifacts(
        TranscriptDocument sourceTranscript,
        IReadOnlyList<VoiceGenerationArtifact> artifacts,
        string? warning = null)
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);
        ArgumentNullException.ThrowIfNull(artifacts);

        var copiedArtifacts = artifacts
            .Select(static artifact => artifact with { AudioBytes = artifact.AudioBytes.ToArray() })
            .ToArray();

        var hasArtifacts = copiedArtifacts.Length > 0;

        return new VoiceGenerationStageResult(
            sourceTranscript,
            Success: hasArtifacts && string.IsNullOrWhiteSpace(warning),
            IsDisabled: false,
            Artifacts: copiedArtifacts,
            ErrorMessage: string.IsNullOrWhiteSpace(warning) ? null : warning);
    }
}
