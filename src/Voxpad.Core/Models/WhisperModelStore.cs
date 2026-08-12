using System.Security.Cryptography;
using System.Text.Json;

namespace Voxpad.Core.Models;

public sealed class WhisperModelStore : IModelStore
{
    private const string SelectedModelStateFileName = "selected-model.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly HttpClient httpClient;
    private readonly string modelsDirectoryPath;
    private readonly IReadOnlyList<WhisperModelInfo> availableModels;
    private readonly Dictionary<string, WhisperModelInfo> modelsById;

    public WhisperModelStore(
        HttpClient httpClient,
        string modelsDirectoryPath,
        IEnumerable<WhisperModelInfo>? availableModels = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentException.ThrowIfNullOrWhiteSpace(modelsDirectoryPath);

        this.modelsDirectoryPath = modelsDirectoryPath;
        this.availableModels = (availableModels ?? WhisperModelCatalog.Default).ToArray();
        modelsById = this.availableModels.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
    }

    public static WhisperModelStore CreateDefault(HttpClient httpClient)
    {
        return new WhisperModelStore(httpClient, ModelCachePathResolver.ResolveForCurrentPlatform());
    }

    public IReadOnlyList<WhisperModelInfo> ListAvailableModels() => availableModels;

    public Task<IReadOnlyList<InstalledWhisperModel>> ListInstalledModelsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(modelsDirectoryPath))
        {
            return Task.FromResult<IReadOnlyList<InstalledWhisperModel>>(Array.Empty<InstalledWhisperModel>());
        }

        var installed = new List<InstalledWhisperModel>();
        foreach (var model in availableModels)
        {
            var modelPath = GetModelPath(model);
            if (!File.Exists(modelPath))
            {
                continue;
            }

            var fileInfo = new FileInfo(modelPath);
            installed.Add(new InstalledWhisperModel(model, modelPath, fileInfo.Length));
        }

        return Task.FromResult<IReadOnlyList<InstalledWhisperModel>>(installed);
    }

    public async Task<InstalledWhisperModel> DownloadModelAsync(
        string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var model = GetModelOrThrow(modelId);
        Directory.CreateDirectory(modelsDirectoryPath);

        var destinationPath = GetModelPath(model);
        var temporaryPath = destinationPath + ".download";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, model.DownloadUrl);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var expectedChecksum = model.Sha256.ToLowerInvariant();
            var totalBytes = response.Content.Headers.ContentLength;
            long bytesRead = 0;

            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            var buffer = new byte[81920];
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hasher.AppendData(buffer, 0, read);
                bytesRead += read;

                if (totalBytes is > 0)
                {
                    progress?.Report((double)bytesRead / totalBytes.Value);
                }
            }

            var actualChecksum = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Checksum mismatch for '{model.Id}'. Expected {expectedChecksum}, got {actualChecksum}.");
            }

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(temporaryPath, destinationPath);
            progress?.Report(1d);

            var destinationInfo = new FileInfo(destinationPath);
            return new InstalledWhisperModel(model, destinationPath, destinationInfo.Length);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public async Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = GetModelOrThrow(modelId);

        cancellationToken.ThrowIfCancellationRequested();

        var modelPath = GetModelPath(model);
        if (File.Exists(modelPath))
        {
            File.Delete(modelPath);
        }

        var selectedModelId = await GetSelectedModelIdAsync(cancellationToken);
        if (string.Equals(selectedModelId, model.Id, StringComparison.OrdinalIgnoreCase))
        {
            var statePath = GetSelectedModelStatePath();
            if (File.Exists(statePath))
            {
                File.Delete(statePath);
            }
        }
    }

    public async Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default)
    {
        var model = GetModelOrThrow(modelId);
        var modelPath = GetModelPath(model);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Model '{model.Id}' is not installed. Download it before selecting it.",
                modelPath);
        }

        Directory.CreateDirectory(modelsDirectoryPath);

        var statePath = GetSelectedModelStatePath();
        await using var stream = new FileStream(statePath, FileMode.Create, FileAccess.Write, FileShare.None);
        var state = new SelectedModelState(model.Id);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }

    public async Task<string?> GetSelectedModelIdAsync(CancellationToken cancellationToken = default)
    {
        var statePath = GetSelectedModelStatePath();
        if (!File.Exists(statePath))
        {
            return null;
        }

        await using var stream = new FileStream(statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var state = await JsonSerializer.DeserializeAsync<SelectedModelState>(stream, JsonOptions, cancellationToken);
        if (state is null || string.IsNullOrWhiteSpace(state.ModelId))
        {
            return null;
        }

        return modelsById.ContainsKey(state.ModelId) ? state.ModelId : null;
    }

    private WhisperModelInfo GetModelOrThrow(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (!modelsById.TryGetValue(modelId, out var model))
        {
            throw new KeyNotFoundException($"Unknown model id '{modelId}'.");
        }

        return model;
    }

    private string GetModelPath(WhisperModelInfo model) => Path.Combine(modelsDirectoryPath, model.FileName);

    private string GetSelectedModelStatePath() => Path.Combine(modelsDirectoryPath, SelectedModelStateFileName);

    private sealed record SelectedModelState(string ModelId);
}
