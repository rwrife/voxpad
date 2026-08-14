using System.Net;
using System.Text;
using System.Text.Json;
using Voxpad.Core.Ai;

namespace Voxpad.Core.Tests.Ai;

public sealed class LocalOpenAiTranscriptAiServiceTests
{
    [Fact]
    public async Task ProbeAsync_WhenDisabled_DoesNotCallHttp()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var service = new LocalOpenAiTranscriptAiService(client);

        var result = await service.ProbeAsync();

        Assert.False(result.IsEnabled);
        Assert.False(result.IsReachable);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task SummarizeAsync_WhenDisabled_ReturnsDisabledWithoutNetworkCall()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var service = new LocalOpenAiTranscriptAiService(client);

        var result = await service.SummarizeAsync("transcript text");

        Assert.False(result.Success);
        Assert.Contains("disabled", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task ProbeAsync_WhenEnabled_RequestsModelsEndpoint()
    {
        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            }));

        using var client = new HttpClient(handler);
        var settings = new TranscriptAiSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "phi3"
        };

        var service = new LocalOpenAiTranscriptAiService(client, settings);
        var result = await service.ProbeAsync();

        Assert.True(result.IsEnabled);
        Assert.True(result.IsReachable);
        Assert.Equal(1, handler.RequestCount);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://localhost:11434/v1/models", request.RequestUri.AbsoluteUri);
    }

    [Fact]
    public async Task SummarizeAsync_WhenEnabled_CallsChatCompletionsAndReturnsOutput()
    {
        const string completion = "- Decision: ship local-only support.";

        var handler = new RecordingHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{" +
                    "\"choices\":[{" +
                    "\"message\":{\"content\":\"- Decision: ship local-only support.\"}" +
                    "}]}",
                    Encoding.UTF8,
                    "application/json")
            }));

        using var client = new HttpClient(handler);
        var settings = new TranscriptAiSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "phi3"
        };

        var service = new LocalOpenAiTranscriptAiService(client, settings);
        var result = await service.SummarizeAsync("Speaker: let's ship this offline feature.");

        Assert.True(result.Success);
        Assert.Equal(completion, result.OutputText);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://localhost:11434/v1/chat/completions", request.RequestUri.AbsoluteUri);
        Assert.NotNull(request.Body);

        using var payload = JsonDocument.Parse(request.Body!);
        Assert.Equal("phi3", payload.RootElement.GetProperty("model").GetString());

        var messages = payload.RootElement.GetProperty("messages");
        Assert.Equal(JsonValueKind.Array, messages.ValueKind);
        Assert.Equal(2, messages.GetArrayLength());

        var userMessage = messages[1].GetProperty("content").GetString();
        Assert.Contains("offline feature", userMessage);
    }

    [Fact]
    public async Task CleanUpAsync_WhenEndpointUnreachable_ReturnsFailure()
    {
        var handler = new RecordingHandler((_, _) => throw new HttpRequestException("connection refused"));
        using var client = new HttpClient(handler);
        var settings = new TranscriptAiSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "phi3"
        };

        var service = new LocalOpenAiTranscriptAiService(client, settings);
        var result = await service.CleanUpAsync("um this is, like, kinda noisy");

        Assert.False(result.Success);
        Assert.Contains("connection refused", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task AutoTitleAsync_WhenModelMissing_ReturnsFailureWithoutNetworkCall()
    {
        var handler = new RecordingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var client = new HttpClient(handler);
        var settings = new TranscriptAiSettings
        {
            Enabled = true,
            EndpointUrl = "http://localhost:11434",
            Model = "   "
        };

        var service = new LocalOpenAiTranscriptAiService(client, settings);
        var result = await service.AutoTitleAsync("weekly planning meeting notes");

        Assert.False(result.Success);
        Assert.Contains("not configured", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount);
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
