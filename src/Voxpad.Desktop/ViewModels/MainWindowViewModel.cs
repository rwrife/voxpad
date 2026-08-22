using Voxpad.Core.Ai;
using Voxpad.Core.Models;
using Voxpad.Core.Translation;
using Voxpad.Core.Voice;

namespace Voxpad.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        var modelStoreHttpClient = new HttpClient();
        ModelManager = new ModelManagerViewModel(WhisperModelStore.CreateDefault(modelStoreHttpClient));
        PostProcessing = CreatePostProcessingWorkspace();
    }

    public MainWindowViewModel(IModelStore modelStore)
    {
        ModelManager = new ModelManagerViewModel(modelStore);
        PostProcessing = CreatePostProcessingWorkspace();
    }

    public ModelManagerViewModel ModelManager { get; }

    public PostProcessingViewModel PostProcessing { get; }

    public Task InitializeAsync()
    {
        return ModelManager.RefreshAsync();
    }

    private static PostProcessingViewModel CreatePostProcessingWorkspace()
    {
        var httpClient = new HttpClient();
        var cleanup = new LocalOpenAiTranscriptAiService(
            httpClient,
            new TranscriptAiSettings { Enabled = true });
        var translation = new LocalOpenAiTranslationService(
            httpClient,
            new TranslationSettings { Enabled = true });
        var voice = new LocalOpenAiVoiceGenerationService(
            httpClient,
            new VoiceGenerationSettings { Enabled = true });

        return new PostProcessingViewModel(cleanup, translation, voice);
    }
}
