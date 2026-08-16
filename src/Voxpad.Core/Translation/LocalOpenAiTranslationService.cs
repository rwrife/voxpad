using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Voxpad.Core.Export;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Translation;

public sealed class LocalOpenAiTranslationService : ITranslationService
{
    private readonly HttpClient httpClient;
    private readonly IExporter[] subtitleExporters =
    [
        new SrtExporter(),
        new VttExporter()
    ];

    public LocalOpenAiTranslationService(HttpClient httpClient, TranslationSettings? settings = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Settings = settings ?? new TranslationSettings();
    }

    public TranslationSettings Settings { get; }

    public async Task<TranslationStageResult> TranslateAsync(
        TranscriptDocument sourceTranscript,
        IReadOnlyList<string> targetLanguages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceTranscript);
        ArgumentNullException.ThrowIfNull(targetLanguages);

        var normalizedLanguages = NormalizeTargetLanguages(targetLanguages);
        if (normalizedLanguages.Length == 0)
        {
            return TranslationStageResult.Failure(sourceTranscript, "At least one target language must be provided.");
        }

        if (!Settings.Enabled)
        {
            return TranslationStageResult.Disabled(sourceTranscript);
        }

        if (string.IsNullOrWhiteSpace(Settings.Model))
        {
            return TranslationStageResult.Failure(sourceTranscript, "Translation model is not configured.");
        }

        if (!TryBuildEndpointUri("v1/chat/completions", out var endpointUri, out var configError))
        {
            return TranslationStageResult.Failure(sourceTranscript, configError!);
        }

        var sourceTexts = sourceTranscript.Segments.Select(static s => s.Text).ToArray();
        var variants = new List<TranslatedTranscriptVariant>();
        var artifacts = new List<LocalizedSubtitleArtifact>();
        var errors = new List<string>();

        foreach (var languageCode in normalizedLanguages)
        {
            var translatedTextsResult = await TranslateLanguageAsync(
                sourceTexts,
                languageCode,
                endpointUri!,
                cancellationToken);

            if (!translatedTextsResult.Success)
            {
                errors.Add($"{languageCode}: {translatedTextsResult.ErrorMessage}");
                continue;
            }

            var translatedDocument = BuildTranslatedTranscript(sourceTranscript, translatedTextsResult.TranslatedTexts!);
            var languageDisplayName = ResolveLanguageDisplayName(languageCode);

            variants.Add(
                new TranslatedTranscriptVariant(
                    languageCode,
                    languageDisplayName,
                    translatedDocument,
                    Settings.Provider,
                    Settings.Model));

            foreach (var exporter in subtitleExporters)
            {
                artifacts.Add(
                    new LocalizedSubtitleArtifact(
                        languageCode,
                        languageDisplayName,
                        exporter.Format,
                        exporter.FileExtension,
                        exporter.Export(translatedDocument)));
            }
        }

        if (variants.Count == 0)
        {
            var error = errors.Count > 0
                ? string.Join(" ", errors)
                : "No translation variants were generated.";

            return TranslationStageResult.Failure(sourceTranscript, error);
        }

        var warning = errors.Count > 0
            ? string.Join(" ", errors)
            : null;

        return TranslationStageResult.FromVariants(sourceTranscript, variants, artifacts, warning);
    }

    private static TranscriptDocument BuildTranslatedTranscript(
        TranscriptDocument sourceTranscript,
        IReadOnlyList<string> translatedTexts)
    {
        var sourceSegments = sourceTranscript.Segments;
        if (sourceSegments.Count != translatedTexts.Count)
        {
            throw new InvalidOperationException("Translated segment count does not match source segment count.");
        }

        var translatedSegments = new TranscriptSegment[sourceSegments.Count];
        for (var i = 0; i < sourceSegments.Count; i++)
        {
            var source = sourceSegments[i];
            translatedSegments[i] = new TranscriptSegment(
                translatedTexts[i],
                source.StartMs,
                source.EndMs);
        }

        return TranscriptDocument.FromSegments(translatedSegments);
    }

    private async Task<TranslateTextsResult> TranslateLanguageAsync(
        IReadOnlyList<string> sourceSegments,
        string languageCode,
        Uri endpointUri,
        CancellationToken cancellationToken)
    {
        var sourcePayload = JsonSerializer.Serialize(sourceSegments);
        var payload = new ChatCompletionsRequest(
            Settings.Model,
            [
                new ChatMessage(
                    "system",
                    "You are a translation engine for subtitle generation. Return strict JSON only."),
                new ChatMessage(
                    "user",
                    $"Translate each transcript segment to '{languageCode}'. " +
                    "Preserve segment ordering and return JSON with this shape: " +
                    "{\"translations\":[\"segment 1\",\"segment 2\"]}. " +
                    $"Return exactly {sourceSegments.Count} translated strings.\n\n" +
                    $"Source segments JSON:\n{sourcePayload}")
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
                return TranslateTextsResult.Failure(
                    $"Translation request failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

            if (!TryExtractCompletionText(document.RootElement, out var completionText))
            {
                return TranslateTextsResult.Failure("Translation response did not contain a completion.");
            }

            if (!TryParseTranslations(completionText!, sourceSegments.Count, out var translatedTexts, out var parseError))
            {
                return TranslateTextsResult.Failure(parseError!);
            }

            return TranslateTextsResult.FromTexts(translatedTexts!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TranslateTextsResult.Failure("Translation request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return TranslateTextsResult.Failure($"Translation request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return TranslateTextsResult.Failure($"Failed to parse translation response: {ex.Message}");
        }
    }

    private static bool TryExtractCompletionText(JsonElement rootElement, out string? outputText)
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

    private static bool TryParseTranslations(
        string completionText,
        int expectedCount,
        out IReadOnlyList<string>? translatedTexts,
        out string? error)
    {
        translatedTexts = null;
        error = null;

        var normalized = StripMarkdownFence(completionText);

        try
        {
            using var document = JsonDocument.Parse(normalized);
            var root = document.RootElement;

            JsonElement translationsElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (!root.TryGetProperty("translations", out translationsElement) ||
                    translationsElement.ValueKind != JsonValueKind.Array)
                {
                    error = "Translation completion JSON must contain a 'translations' array.";
                    return false;
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                translationsElement = root;
            }
            else
            {
                error = "Translation completion JSON must be an object or array.";
                return false;
            }

            var texts = new List<string>(translationsElement.GetArrayLength());
            foreach (var entry in translationsElement.EnumerateArray())
            {
                var text = entry.GetString()?.Trim() ?? string.Empty;
                texts.Add(text);
            }

            if (texts.Count != expectedCount)
            {
                error = $"Translation completion returned {texts.Count} segments; expected {expectedCount}.";
                return false;
            }

            translatedTexts = texts;
            return true;
        }
        catch (JsonException ex)
        {
            error = $"Translation completion was not valid JSON: {ex.Message}";
            return false;
        }
    }

    private bool TryBuildEndpointUri(string relativePath, out Uri? endpointUri, out string? error)
    {
        endpointUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(Settings.EndpointUrl))
        {
            error = "Translation endpoint URL is not configured.";
            return false;
        }

        if (!Uri.TryCreate(Settings.EndpointUrl, UriKind.Absolute, out var baseUri))
        {
            error = $"Invalid translation endpoint URL '{Settings.EndpointUrl}'.";
            return false;
        }

        if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
        }

        endpointUri = new Uri(baseUri, relativePath);
        return true;
    }

    private static string ResolveLanguageDisplayName(string languageCode)
    {
        try
        {
            return CultureInfo.GetCultureInfo(languageCode).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return languageCode;
        }
    }

    private static string[] NormalizeTargetLanguages(IReadOnlyList<string> targetLanguages)
    {
        return targetLanguages
            .Where(static language => !string.IsNullOrWhiteSpace(language))
            .Select(NormalizeLanguageCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeLanguageCode(string languageCode)
    {
        var normalized = languageCode.Trim().Replace('_', '-');
        var segments = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return normalized;
        }

        if (segments.Length == 1)
        {
            return segments[0].ToLowerInvariant();
        }

        return $"{segments[0].ToLowerInvariant()}-{segments[1].ToUpperInvariant()}";
    }

    private static string StripMarkdownFence(string value)
    {
        var trimmed = value.Trim();

        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineBreak = trimmed.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return trimmed;
        }

        var contentStart = firstLineBreak + 1;
        var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence <= contentStart)
        {
            return trimmed;
        }

        return trimmed[contentStart..closingFence].Trim();
    }

    private sealed record ChatCompletionsRequest(string Model, IReadOnlyList<ChatMessage> Messages);

    private sealed record ChatMessage(string Role, string Content);

    private sealed record TranslateTextsResult(bool Success, IReadOnlyList<string>? TranslatedTexts, string? ErrorMessage)
    {
        public static TranslateTextsResult Failure(string message) => new(false, null, message);

        public static TranslateTextsResult FromTexts(IReadOnlyList<string> translatedTexts) =>
            new(true, translatedTexts, null);
    }
}
