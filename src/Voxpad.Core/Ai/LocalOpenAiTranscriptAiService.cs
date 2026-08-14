using System.Net.Http.Json;
using System.Text.Json;

namespace Voxpad.Core.Ai;

public sealed class LocalOpenAiTranscriptAiService : ITranscriptAiService
{
    private readonly HttpClient httpClient;

    public LocalOpenAiTranscriptAiService(HttpClient httpClient, TranscriptAiSettings? settings = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Settings = settings ?? new TranscriptAiSettings();
    }

    public TranscriptAiSettings Settings { get; }

    public async Task<TranscriptAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!Settings.Enabled)
        {
            return new TranscriptAiProbeResult(
                IsEnabled: false,
                IsReachable: false,
                Message: "Local-AI is disabled.");
        }

        if (!TryBuildEndpointUri("v1/models", out var endpointUri, out var configError))
        {
            return new TranscriptAiProbeResult(
                IsEnabled: true,
                IsReachable: false,
                Message: configError!);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpointUri);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode
                ? new TranscriptAiProbeResult(true, true, "Local endpoint is reachable.")
                : new TranscriptAiProbeResult(
                    true,
                    false,
                    $"Local endpoint responded with {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TranscriptAiProbeResult(true, false, "Local endpoint probe timed out.");
        }
        catch (HttpRequestException ex)
        {
            return new TranscriptAiProbeResult(true, false, $"Local endpoint probe failed: {ex.Message}");
        }
    }

    public Task<TranscriptAiResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default)
    {
        return ExecutePromptAsync(
            transcriptText,
            "You summarize meeting transcripts into concise bullet points with action items.",
            "Summarize the transcript. Keep key decisions and action items.",
            cancellationToken);
    }

    public Task<TranscriptAiResult> CleanUpAsync(string transcriptText, CancellationToken cancellationToken = default)
    {
        return ExecutePromptAsync(
            transcriptText,
            "You rewrite spoken transcripts into clean text while preserving meaning.",
            "Clean up filler words, false starts, and punctuation while preserving intent.",
            cancellationToken);
    }

    public Task<TranscriptAiResult> AutoTitleAsync(string transcriptText, CancellationToken cancellationToken = default)
    {
        return ExecutePromptAsync(
            transcriptText,
            "You generate short, specific titles for transcripts.",
            "Generate a short title (max 8 words). Respond with title text only.",
            cancellationToken);
    }

    private async Task<TranscriptAiResult> ExecutePromptAsync(
        string transcriptText,
        string systemPrompt,
        string userInstruction,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transcriptText);

        if (!Settings.Enabled)
        {
            return TranscriptAiResult.Disabled();
        }

        if (string.IsNullOrWhiteSpace(Settings.Model))
        {
            return TranscriptAiResult.Failure("Local-AI model is not configured.");
        }

        if (!TryBuildEndpointUri("v1/chat/completions", out var endpointUri, out var configError))
        {
            return TranscriptAiResult.Failure(configError!);
        }

        var payload = new ChatCompletionsRequest(
            Settings.Model,
            [
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", $"{userInstruction}\n\nTranscript:\n{transcriptText}")
            ]);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return TranscriptAiResult.Failure(
                    $"Local-AI request failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (!TryExtractContent(document.RootElement, out var outputText))
            {
                return TranscriptAiResult.Failure("Local-AI response did not contain a completion.");
            }

            return TranscriptAiResult.FromOutput(outputText!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TranscriptAiResult.Failure("Local-AI request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return TranscriptAiResult.Failure($"Local-AI request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return TranscriptAiResult.Failure($"Failed to parse Local-AI response: {ex.Message}");
        }
    }

    private bool TryBuildEndpointUri(string relativePath, out Uri? endpointUri, out string? error)
    {
        endpointUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(Settings.EndpointUrl))
        {
            error = "Local-AI endpoint URL is not configured.";
            return false;
        }

        if (!Uri.TryCreate(Settings.EndpointUrl, UriKind.Absolute, out var baseUri))
        {
            error = $"Invalid Local-AI endpoint URL '{Settings.EndpointUrl}'.";
            return false;
        }

        if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
        }

        endpointUri = new Uri(baseUri, relativePath);
        return true;
    }

    private static bool TryExtractContent(JsonElement rootElement, out string? outputText)
    {
        outputText = null;

        if (!rootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        if (choices.GetArrayLength() == 0)
        {
            return false;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return false;
        }

        var text = content.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        outputText = text;
        return true;
    }

    private sealed record ChatCompletionsRequest(string Model, IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(string Role, string Content);
}
