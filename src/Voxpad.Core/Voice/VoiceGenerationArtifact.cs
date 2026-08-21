namespace Voxpad.Core.Voice;

public sealed record VoiceGenerationArtifact(
    string LanguageCode,
    string LanguageDisplayName,
    string Format,
    string MimeType,
    string FileExtension,
    string Provider,
    string Model,
    string VoiceProfileName,
    string VoiceId,
    byte[] AudioBytes);
