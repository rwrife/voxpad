namespace Voxpad.Core.Voice;

public sealed record VoiceGenerationSettings
{
    public bool Enabled { get; init; } = false;

    public string EndpointUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "kokoro";

    public string Provider { get; init; } = "local-openai";

    public string AudioFormat { get; init; } = "mp3";
}
