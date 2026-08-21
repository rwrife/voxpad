using Voxpad.Core.Transcription;

namespace Voxpad.Core.Voice;

public interface IVoiceGenerationService
{
    VoiceGenerationSettings Settings { get; }

    Task<VoiceGenerationStageResult> GenerateAsync(
        TranscriptDocument transcriptVariant,
        VoiceProfile voiceProfile,
        string? languageCode = null,
        CancellationToken cancellationToken = default);
}
