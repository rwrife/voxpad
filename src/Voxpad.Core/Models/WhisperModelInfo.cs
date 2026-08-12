namespace Voxpad.Core.Models;

public sealed record WhisperModelInfo
{
    public WhisperModelInfo(
        string id,
        string displayName,
        string fileName,
        string downloadUrl,
        string sha256,
        long sizeBytes,
        string language,
        bool isMultilingual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);

        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "Model size must be positive.");
        }

        Id = id;
        DisplayName = displayName;
        FileName = fileName;
        DownloadUrl = downloadUrl;
        Sha256 = sha256;
        SizeBytes = sizeBytes;
        Language = language;
        IsMultilingual = isMultilingual;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string FileName { get; }

    public string DownloadUrl { get; }

    public string Sha256 { get; }

    public long SizeBytes { get; }

    public string Language { get; }

    public bool IsMultilingual { get; }
}
