namespace Voxpad.Desktop.ViewModels;

public sealed class PipelineArtifactViewModel
{
    private PipelineArtifactViewModel(
        string fileName,
        string kind,
        string languageCode,
        string format,
        string? textContent,
        byte[]? binaryContent)
    {
        FileName = fileName;
        Kind = kind;
        LanguageCode = languageCode;
        Format = format;
        TextContent = textContent;
        BinaryContent = binaryContent?.ToArray();
    }

    public string FileName { get; }

    public string Kind { get; }

    public string LanguageCode { get; }

    public string Format { get; }

    public string? TextContent { get; }

    public byte[]? BinaryContent { get; }

    public long SizeBytes => BinaryContent?.LongLength ??
        System.Text.Encoding.UTF8.GetByteCount(TextContent ?? string.Empty);

    public string SizeLabel => SizeBytes < 1_024
        ? $"{SizeBytes} B"
        : $"{SizeBytes / 1_024d:0.0} KB";

    public static PipelineArtifactViewModel Subtitle(
        string languageCode,
        string format,
        string fileExtension,
        string content)
    {
        return new PipelineArtifactViewModel(
            $"transcript-{SafeFilePart(languageCode)}{NormalizeExtension(fileExtension)}",
            "Subtitle",
            languageCode,
            format,
            content,
            null);
    }

    public static PipelineArtifactViewModel Audio(
        string languageCode,
        string format,
        string fileExtension,
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new PipelineArtifactViewModel(
            $"narration-{SafeFilePart(languageCode)}{NormalizeExtension(fileExtension)}",
            "Audio",
            languageCode,
            format,
            null,
            content);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : $".{trimmed}";
    }

    private static string SafeFilePart(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safe = new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => invalidCharacters.Contains(character) ? '-' : character)
            .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "source" : safe;
    }
}
