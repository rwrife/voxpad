namespace Voxpad.Core.Audio;

public sealed class AudioImportService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
        ".mp3",
        ".m4a",
        ".mp4",
        ".mov"
    };

    private readonly IAudioDecoder audioDecoder;

    public AudioImportService(IAudioDecoder audioDecoder)
    {
        this.audioDecoder = audioDecoder ?? throw new ArgumentNullException(nameof(audioDecoder));
    }

    public static IReadOnlyCollection<string> AllowedExtensions => SupportedExtensions;

    public bool IsSupportedInputPath(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        return SupportedExtensions.Contains(Path.GetExtension(inputPath));
    }

    public Task<DecodedAudioPcm> ImportAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Audio import file not found.", inputPath);
        }

        if (!IsSupportedInputPath(inputPath))
        {
            throw new NotSupportedException($"Unsupported audio import format: '{Path.GetExtension(inputPath)}'.");
        }

        return audioDecoder.DecodeToMono16KhzPcmAsync(inputPath, cancellationToken);
    }
}
