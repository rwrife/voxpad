using Voxpad.Core.Models;

namespace Voxpad.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        var modelStoreHttpClient = new HttpClient();
        ModelManager = new ModelManagerViewModel(WhisperModelStore.CreateDefault(modelStoreHttpClient));
    }

    internal MainWindowViewModel(IModelStore modelStore)
    {
        ModelManager = new ModelManagerViewModel(modelStore);
    }

    public ModelManagerViewModel ModelManager { get; }

    public Task InitializeAsync()
    {
        return ModelManager.RefreshAsync();
    }
}
