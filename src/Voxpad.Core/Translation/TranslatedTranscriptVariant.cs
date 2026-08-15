using Voxpad.Core.Transcription;

namespace Voxpad.Core.Translation;

public sealed record TranslatedTranscriptVariant(
    string LanguageCode,
    string LanguageDisplayName,
    TranscriptDocument Transcript,
    string Provider,
    string Model);
