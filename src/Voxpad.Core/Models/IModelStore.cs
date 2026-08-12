namespace Voxpad.Core.Models;

public interface IModelStore
{
    IReadOnlyList<WhisperModelInfo> ListAvailableModels();

    Task<IReadOnlyList<InstalledWhisperModel>> ListInstalledModelsAsync(CancellationToken cancellationToken = default);

    Task<InstalledWhisperModel> DownloadModelAsync(
        string modelId,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default);

    Task<string?> GetSelectedModelIdAsync(CancellationToken cancellationToken = default);
}
