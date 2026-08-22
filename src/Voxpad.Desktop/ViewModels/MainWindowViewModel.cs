using Voxpad.Core.Ai;
using Voxpad.Core.Models;
using Voxpad.Core.Playback;
using Voxpad.Core.Transcription;
using Voxpad.Core.Translation;
using Voxpad.Core.Voice;
using Voxpad.Desktop.Playback;

namespace Voxpad.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
        : this(
            WhisperModelStore.CreateDefault(new HttpClient()),
            CreateMediaPlayback())
    {
    }

    public MainWindowViewModel(IModelStore modelStore)
        : this(modelStore, CreateMediaPlayback())
    {
    }

    public MainWindowViewModel(IModelStore modelStore, IMediaPlayback mediaPlayback)
    {
        ModelManager = new ModelManagerViewModel(modelStore ?? throw new ArgumentNullException(nameof(modelStore)));
        PostProcessing = CreatePostProcessingWorkspace();
        Media = new TranscriptMediaViewModel(
            mediaPlayback ?? throw new ArgumentNullException(nameof(mediaPlayback)),
            PostProcessing.UpdateSelectedTranscript);
    }

    public ModelManagerViewModel ModelManager { get; }

    public PostProcessingViewModel PostProcessing { get; }

    public TranscriptMediaViewModel Media { get; }

    public void LoadTranscript(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        PostProcessing.LoadSelectedTranscript(transcript);
        Media.LoadTranscript(transcript);
    }

    public Task InitializeAsync()
    {
        return ModelManager.RefreshAsync();
    }

    private static IMediaPlayback CreateMediaPlayback() =>
        MediaPlaybackFactory.Create(
            static () => new LibVlcMediaPlayback(),
            "Media playback is unavailable. Install VLC 3.x or use a packaged voxpad build with LibVLC included; transcript editing and local pipeline stages remain available.");

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
