namespace Voxpad.Core.Ai;

public sealed record TranscriptAiResult(bool Success, string? OutputText, string? ErrorMessage)
{
    public static TranscriptAiResult Disabled(string message = "Local-AI is disabled.") =>
        new(false, null, message);

    public static TranscriptAiResult Failure(string message) =>
        new(false, null, message);

    public static TranscriptAiResult FromOutput(string outputText) =>
        new(true, outputText, null);
}
