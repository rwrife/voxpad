using Voxpad.Core.Models;
using Voxpad.Desktop.ViewModels;

namespace Voxpad.Core.Tests.Desktop;

public sealed class ModelManagerViewModelTests
{
    [Fact]
    public async Task RefreshAsync_WhenNoModelsInstalled_ShowsFirstRunGuidanceWithBaseRecommendation()
    {
        var available = new[]
        {
            CreateModel("tiny", 10),
            CreateModel("base", 20)
        };

        var store = new FakeModelStore(available, installed: Array.Empty<InstalledWhisperModel>(), selectedModelId: null);
        var viewModel = new ModelManagerViewModel(store);

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasFirstRunGuidance);
        Assert.Contains("base", viewModel.FirstRunGuidance, StringComparison.OrdinalIgnoreCase);
        Assert.All(viewModel.Models, model => Assert.False(model.IsInstalled));
    }

    [Fact]
    public async Task RefreshAsync_WhenModelInstalled_TracksInstalledAndSelectedState()
    {
        var tiny = CreateModel("tiny", 10);
        var baseModel = CreateModel("base", 20);

        var installed = new[]
        {
            new InstalledWhisperModel(baseModel, "/tmp/base.bin", baseModel.SizeBytes)
        };

        var store = new FakeModelStore(new[] { tiny, baseModel }, installed, selectedModelId: "base");
        var viewModel = new ModelManagerViewModel(store);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.HasFirstRunGuidance);

        var baseRow = viewModel.Models.Single(m => m.ModelId == "base");
        Assert.True(baseRow.IsInstalled);
        Assert.True(baseRow.IsSelected);

        var tinyRow = viewModel.Models.Single(m => m.ModelId == "tiny");
        Assert.False(tinyRow.IsInstalled);
        Assert.False(tinyRow.IsSelected);
    }

    private static WhisperModelInfo CreateModel(string id, long sizeBytes)
    {
        return new WhisperModelInfo(
            id: id,
            displayName: id,
            fileName: $"{id}.bin",
            downloadUrl: $"https://models.invalid/{id}.bin",
            sha256: new string('a', 64),
            sizeBytes: sizeBytes,
            language: "en",
            isMultilingual: false);
    }

    private sealed class FakeModelStore : IModelStore
    {
        private readonly IReadOnlyList<WhisperModelInfo> available;
        private readonly List<InstalledWhisperModel> installed;
        private string? selectedModelId;

        public FakeModelStore(
            IReadOnlyList<WhisperModelInfo> available,
            IReadOnlyList<InstalledWhisperModel> installed,
            string? selectedModelId)
        {
            this.available = available;
            this.installed = installed.ToList();
            this.selectedModelId = selectedModelId;
        }

        public IReadOnlyList<WhisperModelInfo> ListAvailableModels() => available;

        public Task<IReadOnlyList<InstalledWhisperModel>> ListInstalledModelsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<InstalledWhisperModel>>(installed.ToList());
        }

        public Task<InstalledWhisperModel> DownloadModelAsync(
            string modelId,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var model = available.Single(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
            var record = new InstalledWhisperModel(model, $"/tmp/{model.FileName}", model.SizeBytes);
            installed.RemoveAll(m => string.Equals(m.Model.Id, modelId, StringComparison.OrdinalIgnoreCase));
            installed.Add(record);
            progress?.Report(1d);
            return Task.FromResult(record);
        }

        public Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default)
        {
            installed.RemoveAll(m => string.Equals(m.Model.Id, modelId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(selectedModelId, modelId, StringComparison.OrdinalIgnoreCase))
            {
                selectedModelId = null;
            }

            return Task.CompletedTask;
        }

        public Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default)
        {
            selectedModelId = modelId;
            return Task.CompletedTask;
        }

        public Task<string?> GetSelectedModelIdAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(selectedModelId);
        }
    }
}
