namespace Voxpad.Core.Translation;

public sealed record TranslationSettings
{
    public bool Enabled { get; init; } = false;

    public string EndpointUrl { get; init; } = "http://localhost:11434";

    public string Model { get; init; } = "llama3.2:3b";

    public string Provider { get; init; } = "local-openai";
}
