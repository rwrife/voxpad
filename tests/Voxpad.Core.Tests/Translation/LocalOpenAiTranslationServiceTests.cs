using System.Net;
using System.Text;
using System.Text.Json;
using Voxpad.Core.Transcription;
using Voxpad.Core.Translation;

namespace Voxpad.Core.Tests.Translation;

public sealed class LocalOpenAiTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_WhenDisabled_ReturnsDisabledWithoutNetworkCall()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var service = new LocalOpenAiTranslationService(client);

        var result = await service.TranslateAsync(CreateSourceTranscript(), ["es"]);

        Assert.False(result.Success);
        Assert.True(result.IsDisabled);
        Assert.Empty(result.Variants);
        Assert.Empty(result.SubtitleArtifacts);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task TranslateAsync_WhenEnabled_GeneratesVariantsAndPerLanguageSubtitleArtifacts()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            CreateChatCompletionResponse("{\"translations\":[\"Hola mundo.\",\"Segunda linea.\"]}"),
            CreateChatCompletionResponse("{\"translations\":[\"Bonjour le monde.\",\"Deuxieme ligne.\"]}")
        ]);

        var handler = new RecordingHandler((_, _) => Task.FromResult(responses.Dequeue()));
        using var client = new HttpClient(handler);
        var service = new LocalOpenAiTranslationService(client, new TranslationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "llama3.1"
        });

        var source = CreateSourceTranscript();
        var result = await service.TranslateAsync(source, ["es", "fr"]);

        Assert.True(result.Success);
        Assert.False(result.IsDisabled);
        Assert.Equal(2, result.Variants.Count);
        Assert.Equal(4, result.SubtitleArtifacts.Count);

        var spanish = Assert.Single(result.Variants, v => v.LanguageCode == "es");
        Assert.Equal("local-openai", spanish.Provider);
        Assert.Equal("llama3.1", spanish.Model);
        Assert.Equal(source.Segments[0].StartMs, spanish.Transcript.Segments[0].StartMs);
        Assert.Equal(source.Segments[0].EndMs, spanish.Transcript.Segments[0].EndMs);
        Assert.Equal("Hola mundo.", spanish.Transcript.Segments[0].Text);

        var spanishSrt = Assert.Single(result.SubtitleArtifacts, a => a.LanguageCode == "es" && a.Format == "srt");
        var spanishVtt = Assert.Single(result.SubtitleArtifacts, a => a.LanguageCode == "es" && a.Format == "vtt");

        Assert.Contains("Hola mundo.", spanishSrt.Content);
        Assert.Contains("WEBVTT", spanishVtt.Content);

        Assert.Equal(2, handler.RequestCount);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://localhost:11434/v1/chat/completions", request.RequestUri.AbsoluteUri);
        });
    }

    [Fact]
    public async Task TranslateAsync_WhenOneLanguageFails_ReturnsPartialResultsWithoutThrowing()
    {
        var call = 0;
        var handler = new RecordingHandler((_, _) =>
        {
            call++;
            if (call == 1)
            {
                return Task.FromResult(CreateChatCompletionResponse("{\"translations\":[\"Hola mundo.\",\"Segunda linea.\"]}"));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "upstream failure"
            });
        });

        using var client = new HttpClient(handler);
        var service = new LocalOpenAiTranslationService(client, new TranslationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "llama3.1"
        });

        var result = await service.TranslateAsync(CreateSourceTranscript(), ["es", "de"]);

        Assert.False(result.Success);
        Assert.False(result.IsDisabled);
        Assert.Single(result.Variants);
        Assert.Equal(2, result.SubtitleArtifacts.Count);
        Assert.Contains("de", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("500", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TranslateAsync_WhenResponseSegmentCountMismatch_ReturnsFailure()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(CreateChatCompletionResponse("{\"translations\":[\"Solo una\"]}")));

        using var client = new HttpClient(handler);
        var service = new LocalOpenAiTranslationService(client, new TranslationSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "llama3.1"
        });

        var result = await service.TranslateAsync(CreateSourceTranscript(), ["es"]);

        Assert.False(result.Success);
        Assert.False(result.IsDisabled);
        Assert.Empty(result.Variants);
        Assert.Contains("expected 2", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static TranscriptDocument CreateSourceTranscript()
    {
        return TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("Hello world.", 0, 1_000),
            new TranscriptSegment("Second line.", 1_500, 2_400)
        ]);
    }

    private static HttpResponseMessage CreateChatCompletionResponse(string content)
    {
        var payload = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content
                    }
                }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
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
