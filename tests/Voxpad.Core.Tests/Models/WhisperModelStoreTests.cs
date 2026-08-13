using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Voxpad.Core.Models;

namespace Voxpad.Core.Tests.Models;

public sealed class WhisperModelStoreTests
{
    [Fact]
    public void ListAvailableModels_ContainsTinyBaseAndSmallVariants()
    {
        var store = new WhisperModelStore(new HttpClient(new StaticContentHandler(Array.Empty<byte>())), "/tmp/voxpad-models");

        var ids = store.ListAvailableModels().Select(m => m.Id).ToArray();

        Assert.Contains("tiny", ids);
        Assert.Contains("tiny.en", ids);
        Assert.Contains("base", ids);
        Assert.Contains("base.en", ids);
        Assert.Contains("small", ids);
        Assert.Contains("small.en", ids);
    }

    [Fact]
    public async Task DownloadModelAsync_WritesModelToCacheWhenChecksumMatches()
    {
        var payload = "voxpad-model-binary"u8.ToArray();
        var model = CreateModel("base.en", payload);

        using var tempDirectory = new TemporaryDirectory();
        using var client = CreateHttpClient(payload);
        var store = new WhisperModelStore(client, tempDirectory.Path, new[] { model });
        var progressValues = new List<double>();

        var installed = await store.DownloadModelAsync(model.Id, new Progress<double>(p => progressValues.Add(p)));

        Assert.Equal(model.Id, installed.Model.Id);
        Assert.True(File.Exists(installed.FilePath));
        Assert.Equal(payload, await File.ReadAllBytesAsync(installed.FilePath));
        Assert.Contains(progressValues, v => v > 0);
        Assert.Equal(1d, progressValues.Last());
    }

    [Fact]
    public async Task DownloadModelAsync_RejectsCorruptDownloads()
    {
        var payload = "voxpad-model-binary"u8.ToArray();
        var model = new WhisperModelInfo(
            id: "base.en",
            displayName: "Base English",
            fileName: "ggml-base.en.bin",
            downloadUrl: "https://models.invalid/base.en.bin",
            sha256: new string('0', 64),
            sizeBytes: payload.Length,
            language: "en",
            isMultilingual: false);

        using var tempDirectory = new TemporaryDirectory();
        using var client = CreateHttpClient(payload);
        var store = new WhisperModelStore(client, tempDirectory.Path, new[] { model });

        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadModelAsync(model.Id));

        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, model.FileName)));
        Assert.False(File.Exists(Path.Combine(tempDirectory.Path, model.FileName + ".download")));
    }

    [Fact]
    public async Task SelectModelAsync_PersistsSelectedModelAcrossStoreInstances()
    {
        var payload = "voxpad-model-binary"u8.ToArray();
        var model = CreateModel("small", payload);

        using var tempDirectory = new TemporaryDirectory();
        using var client = CreateHttpClient(payload);
        var store = new WhisperModelStore(client, tempDirectory.Path, new[] { model });

        await store.DownloadModelAsync(model.Id);
        await store.SelectModelAsync(model.Id);

        var secondStore = new WhisperModelStore(CreateHttpClient(Array.Empty<byte>()), tempDirectory.Path, new[] { model });
        var selectedModelId = await secondStore.GetSelectedModelIdAsync();

        Assert.Equal(model.Id, selectedModelId);
    }

    [Fact]
    public async Task DeleteModelAsync_RemovesModelFileAndSelectionState()
    {
        var payload = "voxpad-model-binary"u8.ToArray();
        var model = CreateModel("tiny.en", payload);

        using var tempDirectory = new TemporaryDirectory();
        using var client = CreateHttpClient(payload);
        var store = new WhisperModelStore(client, tempDirectory.Path, new[] { model });

        var installed = await store.DownloadModelAsync(model.Id);
        await store.SelectModelAsync(model.Id);

        await store.DeleteModelAsync(model.Id);

        Assert.False(File.Exists(installed.FilePath));
        Assert.Null(await store.GetSelectedModelIdAsync());
    }

    private static WhisperModelInfo CreateModel(string id, byte[] payload)
    {
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return new WhisperModelInfo(
            id: id,
            displayName: id,
            fileName: $"{id}.bin",
            downloadUrl: $"https://models.invalid/{id}.bin",
            sha256: hash,
            sizeBytes: payload.Length,
            language: "en",
            isMultilingual: !id.EndsWith(".en", StringComparison.Ordinal));
    }

    private static HttpClient CreateHttpClient(byte[] payload)
    {
        return new HttpClient(new StaticContentHandler(payload));
    }

    private sealed class StaticContentHandler(byte[] payload) : HttpMessageHandler
    {
        private readonly byte[] payload = payload;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Headers.ContentLength = payload.Length;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };

            return Task.FromResult(response);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"voxpad-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
