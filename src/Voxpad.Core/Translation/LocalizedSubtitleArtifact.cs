namespace Voxpad.Core.Translation;

public sealed record LocalizedSubtitleArtifact(
    string LanguageCode,
    string LanguageDisplayName,
    string Format,
    string FileExtension,
    string Content);
