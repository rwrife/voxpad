using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Voxpad.Core.Transcription;

namespace Voxpad.Core.Voice;

public sealed class LocalOpenAiVoiceGenerationService : IVoiceGenerationService
{
    private readonly HttpClient httpClient;

    public LocalOpenAiVoiceGenerationService(HttpClient httpClient, VoiceGenerationSettings? settings = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Settings = settings ?? new VoiceGenerationSettings();
    }

    public VoiceGenerationSettings Settings { get; }

    public async Task<VoiceGenerationStageResult> GenerateAsync(
        TranscriptDocument transcriptVariant,
        VoiceProfile voiceProfile,
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transcriptVariant);
        ArgumentNullException.ThrowIfNull(voiceProfile);

        if (!Settings.Enabled)
        {
            return VoiceGenerationStageResult.Disabled(transcriptVariant);
        }

        if (!voiceProfile.TryValidate(out var voiceValidationError))
        {
            return VoiceGenerationStageResult.Failure(transcriptVariant, voiceValidationError!);
        }

        if (string.IsNullOrWhiteSpace(Settings.Model))
        {
            return VoiceGenerationStageResult.Failure(transcriptVariant, "Voice generation model is not configured.");
        }

        var normalizedLanguageCode = NormalizeLanguageCode(languageCode);
        var languageDisplayName = ResolveLanguageDisplayName(normalizedLanguageCode);
        var transcriptText = BuildTranscriptText(transcriptVariant);

        if (string.IsNullOrWhiteSpace(transcriptText))
        {
            return VoiceGenerationStageResult.Failure(transcriptVariant, "Transcript text is empty and cannot be narrated.");
        }

        if (!TryBuildEndpointUri("v1/audio/speech", out var endpointUri, out var configError))
        {
            return VoiceGenerationStageResult.Failure(transcriptVariant, configError!);
        }

        var format = NormalizeAudioFormat(Settings.AudioFormat);
        var payload = new SpeechRequest(
            Settings.Model,
            voiceProfile.VoiceId,
            transcriptText,
            format,
            voiceProfile.ReferenceInstructions);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpointUri)
            {
                Content = JsonContent.Create(payload)
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return VoiceGenerationStageResult.Failure(
                    transcriptVariant,
                    $"Voice generation request failed: {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (audioBytes.Length == 0)
            {
                return VoiceGenerationStageResult.Failure(transcriptVariant, "Voice generation response contained no audio bytes.");
            }

            var artifact = new VoiceGenerationArtifact(
                normalizedLanguageCode,
                languageDisplayName,
                format,
                ResolveMimeType(format),
                ResolveFileExtension(format),
                Settings.Provider,
                Settings.Model,
                voiceProfile.ProfileName,
                voiceProfile.VoiceId,
                audioBytes);

            return VoiceGenerationStageResult.FromArtifacts(transcriptVariant, [artifact]);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VoiceGenerationStageResult.Failure(transcriptVariant, "Voice generation request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return VoiceGenerationStageResult.Failure(transcriptVariant, $"Voice generation request failed: {ex.Message}");
        }
    }

    private bool TryBuildEndpointUri(string relativePath, out Uri? endpointUri, out string? error)
    {
        endpointUri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(Settings.EndpointUrl))
        {
            error = "Voice generation endpoint URL is not configured.";
            return false;
        }

        if (!Uri.TryCreate(Settings.EndpointUrl, UriKind.Absolute, out var baseUri))
        {
            error = $"Invalid voice generation endpoint URL '{Settings.EndpointUrl}'.";
            return false;
        }

        if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            baseUri = new Uri(baseUri.AbsoluteUri + "/", UriKind.Absolute);
        }

        endpointUri = new Uri(baseUri, relativePath);
        return true;
    }

    private static string BuildTranscriptText(TranscriptDocument transcriptVariant)
    {
        return string.Join(
            Environment.NewLine,
            transcriptVariant.Segments
                .Select(static segment => segment.Text?.Trim())
                .Where(static text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return "source";
        }

        var normalized = languageCode.Trim().Replace('_', '-');
        var segments = normalized.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return "source";
        }

        if (segments.Length == 1)
        {
            return segments[0].ToLowerInvariant();
        }

        return $"{segments[0].ToLowerInvariant()}-{segments[1].ToUpperInvariant()}";
    }

    private static string ResolveLanguageDisplayName(string languageCode)
    {
        if (string.Equals(languageCode, "source", StringComparison.OrdinalIgnoreCase))
        {
            return "Source language";
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageCode).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            return languageCode;
        }
    }

    private static string NormalizeAudioFormat(string? format)
    {
        var normalized = format?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? "mp3" : normalized;
    }

    private static string ResolveFileExtension(string format)
    {
        return format switch
        {
            "wav" => ".wav",
            "flac" => ".flac",
            "opus" => ".opus",
            _ => ".mp3"
        };
    }

    private static string ResolveMimeType(string format)
    {
        return format switch
        {
            "wav" => "audio/wav",
            "flac" => "audio/flac",
            "opus" => "audio/opus",
            _ => "audio/mpeg"
        };
    }

    private sealed record SpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("voice")] string Voice,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("response_format")] string ResponseFormat,
        [property: JsonPropertyName("instructions")] string? Instructions);
}
