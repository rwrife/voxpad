using System.Net;
using System.Text;
using System.Text.Json;
using Voxpad.Core.Transcription;
using Voxpad.Core.Voice;

namespace Voxpad.Core.Tests.Voice;

public sealed class LocalOpenAiVoiceGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_WhenDisabled_ReturnsDisabledWithoutNetworkCall()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var service = new LocalOpenAiVoiceGenerationService(client);

        var result = await service.GenerateAsync(CreateSourceTranscript(), CreateVoiceProfile(), "en-US");

        Assert.False(result.Success);
        Assert.True(result.IsDisabled);
        Assert.Empty(result.Artifacts);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateAsync_WhenEnabled_ReturnsNarrationArtifactWithMetadata()
    {
        var audioBytes = Encoding.UTF8.GetBytes("fake-audio-bytes");
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(audioBytes)
            }));

        using var client = new HttpClient(handler);
        var service = new LocalOpenAiVoiceGenerationService(client, new VoiceGenerationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "kokoro",
            AudioFormat = "mp3"
        });

        var result = await service.GenerateAsync(CreateSourceTranscript(), CreateVoiceProfile(), "es-ES");

        Assert.True(result.Success);
        Assert.False(result.IsDisabled);
        var artifact = Assert.Single(result.Artifacts);
        Assert.Equal("es-ES", artifact.LanguageCode);
        Assert.Equal("local-openai", artifact.Provider);
        Assert.Equal("kokoro", artifact.Model);
        Assert.Equal("Narrator", artifact.VoiceProfileName);
        Assert.Equal("alloy", artifact.VoiceId);
        Assert.Equal("mp3", artifact.Format);
        Assert.Equal(".mp3", artifact.FileExtension);
        Assert.Equal("audio/mpeg", artifact.MimeType);
        Assert.Equal(audioBytes, artifact.AudioBytes);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:11434/v1/audio/speech", request.RequestUri.AbsoluteUri);

        using var requestJson = JsonDocument.Parse(request.Body!);
        Assert.Equal("kokoro", requestJson.RootElement.GetProperty("model").GetString());
        Assert.Equal("alloy", requestJson.RootElement.GetProperty("voice").GetString());
        Assert.Equal("mp3", requestJson.RootElement.GetProperty("response_format").GetString());
    }

    [Fact]
    public async Task GenerateAsync_WhenLanguageCodeMissing_UsesSourceLanguageMetadata()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            }));

        using var client = new HttpClient(handler);
        var service = new LocalOpenAiVoiceGenerationService(client, new VoiceGenerationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "kokoro"
        });

        var result = await service.GenerateAsync(CreateSourceTranscript(), CreateVoiceProfile(), languageCode: null);

        var artifact = Assert.Single(result.Artifacts);
        Assert.Equal("source", artifact.LanguageCode);
        Assert.Equal("Source language", artifact.LanguageDisplayName);
    }

    [Fact]
    public async Task GenerateAsync_WhenVoiceProfileInvalid_ReturnsFailureWithoutNetworkCall()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        using var client = new HttpClient(handler);
        var service = new LocalOpenAiVoiceGenerationService(client, new VoiceGenerationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "kokoro"
        });

        var result = await service.GenerateAsync(
            CreateSourceTranscript(),
            new VoiceProfile("Narrator", string.Empty),
            "en-US");

        Assert.False(result.Success);
        Assert.False(result.IsDisabled);
        Assert.Contains("Voice profile id", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GenerateAsync_WhenEndpointReturnsFailure_ReturnsActionableError()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                ReasonPhrase = "upstream down"
            }));

        using var client = new HttpClient(handler);
        var service = new LocalOpenAiVoiceGenerationService(client, new VoiceGenerationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "kokoro"
        });

        var result = await service.GenerateAsync(CreateSourceTranscript(), CreateVoiceProfile(), "en-US");

        Assert.False(result.Success);
        Assert.False(result.IsDisabled);
        Assert.Empty(result.Artifacts);
        Assert.Contains("502", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("upstream down", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static TranscriptDocument CreateSourceTranscript()
    {
        return TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("Hello world.", 0, 1_000),
            new TranscriptSegment("This is a narration sample.", 1_500, 2_400)
        ]);
    }

    private static VoiceProfile CreateVoiceProfile()
    {
        return new VoiceProfile(
            "Narrator",
            "alloy",
            "Warm, clear pacing suitable for tutorial narration.");
    }

    private sealed record RequestSnapshot(HttpMethod Method, Uri RequestUri, string? Body);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;
        private readonly List<RequestSnapshot> requests = [];

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        public int RequestCount => requests.Count;

        public IReadOnlyList<RequestSnapshot> Requests => requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? body = null;
            if (request.Content is not null)
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            requests.Add(new RequestSnapshot(request.Method, request.RequestUri!, body));
            return await responder(request, cancellationToken);
        }
    }
}
