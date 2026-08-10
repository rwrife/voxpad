namespace Voxpad.Core.Transcription;

public sealed record WhisperTranscriptionOptions
{
    public required string ModelPath { get; init; }

    public string? Language { get; init; } = "auto";

    public bool TranslateToEnglish { get; init; }

    public bool EnableWordTimestamps { get; init; } = true;

    public int Threads { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);

    public WhisperBackendPreference BackendPreference { get; init; } = WhisperBackendPreference.ManagedThenCli;

    public string? WhisperCliPath { get; init; }
}
