using Voxpad.Core.Transcription;

namespace Voxpad.Desktop.ViewModels;

public sealed record PostProcessingVariantViewModel(
    string DisplayName,
    string Stage,
    string? LanguageCode,
    string OutputText,
    string Provider,
    string Model,
    TranscriptDocument? Transcript = null)
{
    public string Provenance => $"{Stage} · {Provider} · {Model}";
}
