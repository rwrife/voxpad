using System.Collections.ObjectModel;
using System.Windows.Input;
using Voxpad.Core.Ai;
using Voxpad.Core.Transcription;
using Voxpad.Core.Translation;
using Voxpad.Core.Voice;
using Voxpad.Desktop.Infrastructure;

namespace Voxpad.Desktop.ViewModels;

public sealed class PostProcessingViewModel : ViewModelBase
{
    private readonly ITranscriptAiService transcriptAiService;
    private readonly ITranslationService translationService;
    private readonly IVoiceGenerationService voiceGenerationService;
    private string sourceText = string.Empty;
    private string targetLanguages = "es";
    private string? originalSourceText;
    private TranscriptDocument? selectedTranscript;
    private TranscriptDocument? originalTranscript;
    private PostProcessingVariantViewModel? selectedVariant;
    private string? errorMessage;
    private bool isBusy;
    private PostProcessingAction lastAction;
    private bool cleanupEnabled = true;
    private bool translationEnabled;
    private bool voiceEnabled;
    private string pipelineStatus = "Ready";
    private string cleanupStatus = "Ready";
    private string translationStatus = "Ready";
    private string voiceStatus = "Ready";
    private string voiceProfileName = "Default";
    private string voiceId = "default";
    private string outputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "voxpad-exports");
    private string exportStatus = "No artifacts exported.";

    public PostProcessingViewModel(
        ITranscriptAiService transcriptAiService,
        ITranslationService translationService)
        : this(
            transcriptAiService,
            translationService,
            new LocalOpenAiVoiceGenerationService(new HttpClient()))
    {
    }

    public PostProcessingViewModel(
        ITranscriptAiService transcriptAiService,
        ITranslationService translationService,
        IVoiceGenerationService voiceGenerationService)
    {
        this.transcriptAiService = transcriptAiService ?? throw new ArgumentNullException(nameof(transcriptAiService));
        this.translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));
        this.voiceGenerationService = voiceGenerationService ?? throw new ArgumentNullException(nameof(voiceGenerationService));

        RunPipelineCommand = new AsyncCommand(() => RunPipelineAsync());
        CleanupCommand = new AsyncCommand(() => RunCleanupAsync());
        TranslationCommand = new AsyncCommand(() => RunTranslationAsync());
        VoiceCommand = new AsyncCommand(() => RunVoiceAsync());
        ExportArtifactsCommand = new AsyncCommand(() => ExportArtifactsAsync());
        RetryCommand = new AsyncCommand(() => RetryAsync());
        PromoteCommand = new RelayCommand(PromoteSelectedVariant);
        RevertCommand = new RelayCommand(RevertSource);
    }

    public ObservableCollection<PostProcessingVariantViewModel> Variants { get; } = [];

    public ObservableCollection<PipelineArtifactViewModel> Artifacts { get; } = [];

    public ICommand RunPipelineCommand { get; }

    public ICommand CleanupCommand { get; }

    public ICommand TranslationCommand { get; }

    public ICommand VoiceCommand { get; }

    public ICommand ExportArtifactsCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand PromoteCommand { get; }

    public ICommand RevertCommand { get; }

    public bool CleanupEnabled
    {
        get => cleanupEnabled;
        set => SetProperty(ref cleanupEnabled, value);
    }

    public bool TranslationEnabled
    {
        get => translationEnabled;
        set => SetProperty(ref translationEnabled, value);
    }

    public bool VoiceEnabled
    {
        get => voiceEnabled;
        set => SetProperty(ref voiceEnabled, value);
    }

    public string PipelineStatus
    {
        get => pipelineStatus;
        private set => SetProperty(ref pipelineStatus, value);
    }

    public string CleanupStatus
    {
        get => cleanupStatus;
        private set => SetProperty(ref cleanupStatus, value);
    }

    public string TranslationStatus
    {
        get => translationStatus;
        private set => SetProperty(ref translationStatus, value);
    }

    public string VoiceStatus
    {
        get => voiceStatus;
        private set => SetProperty(ref voiceStatus, value);
    }

    public string VoiceProfileName
    {
        get => voiceProfileName;
        set => SetProperty(ref voiceProfileName, value ?? string.Empty);
    }

    public string VoiceId
    {
        get => voiceId;
        set => SetProperty(ref voiceId, value ?? string.Empty);
    }

    public string OutputDirectory
    {
        get => outputDirectory;
        set => SetProperty(ref outputDirectory, value ?? string.Empty);
    }

    public string ExportStatus
    {
        get => exportStatus;
        private set => SetProperty(ref exportStatus, value);
    }

    public string SourceText
    {
        get => sourceText;
        set => SetProperty(ref sourceText, value ?? string.Empty);
    }

    public string TargetLanguages
    {
        get => targetLanguages;
        set => SetProperty(ref targetLanguages, value ?? string.Empty);
    }

    public PostProcessingVariantViewModel? SelectedVariant
    {
        get => selectedVariant;
        set
        {
            if (SetProperty(ref selectedVariant, value))
            {
                RaisePropertyChanged(nameof(SelectedOutputText));
                RaisePropertyChanged(nameof(SelectedProvenance));
            }
        }
    }

    public string SelectedOutputText => SelectedVariant?.OutputText ?? string.Empty;

    public string SelectedProvenance => SelectedVariant?.Provenance ?? "No generated variant selected.";

    public string? ErrorMessage
    {
        get => errorMessage;
        private set
        {
            if (SetProperty(ref errorMessage, value))
            {
                RaisePropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsBusy
    {
        get => isBusy;
        private set
        {
            if (SetProperty(ref isBusy, value))
            {
                RaisePropertyChanged(nameof(IsNotBusy));
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public void LoadSelectedTranscript(TranscriptDocument transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        if (IsBusy)
        {
            ErrorMessage = "Wait for the running stage to finish before loading another transcript.";
            return;
        }

        selectedTranscript = transcript;
        originalTranscript = transcript;
        SourceText = GetTranscriptText(transcript);
        originalSourceText = SourceText;
        Variants.Clear();
        Artifacts.Clear();
        SelectedVariant = null;
        ErrorMessage = null;
        lastAction = PostProcessingAction.None;
    }

    public async Task RunPipelineAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginOperation())
        {
            return;
        }

        lastAction = PostProcessingAction.Pipeline;
        Artifacts.Clear();
        PipelineStatus = "Running";
        CleanupStatus = CleanupEnabled ? "Pending" : "Skipped";
        TranslationStatus = TranslationEnabled ? "Pending" : "Skipped";
        VoiceStatus = VoiceEnabled ? "Pending" : "Skipped";

        try
        {
            if (!CleanupEnabled && !TranslationEnabled && !VoiceEnabled)
            {
                PipelineStatus = "No stages enabled";
                ErrorMessage = "Enable at least one optional stage before running the pipeline.";
                return;
            }

            PostProcessingVariantViewModel? pipelineVariant = null;

            if (CleanupEnabled)
            {
                CleanupStatus = "Running";
                await RunCleanupCoreAsync(cancellationToken);
                CleanupStatus = HasError ? "Failed" : "Completed";
                if (CleanupStatus == "Completed")
                {
                    pipelineVariant = SelectedVariant;
                }
            }

            if (TranslationEnabled)
            {
                var errorBeforeStage = ErrorMessage;
                TranslationStatus = "Running";
                await RunTranslationCoreAsync(cancellationToken, pipelineVariant);
                TranslationStatus = string.Equals(errorBeforeStage, ErrorMessage, StringComparison.Ordinal)
                    ? "Completed"
                    : "Failed";
                if (TranslationStatus == "Completed")
                {
                    pipelineVariant = SelectedVariant;
                }
            }

            if (VoiceEnabled)
            {
                var errorBeforeStage = ErrorMessage;
                VoiceStatus = "Running";
                await RunVoiceCoreAsync(cancellationToken, pipelineVariant);
                VoiceStatus = string.Equals(errorBeforeStage, ErrorMessage, StringComparison.Ordinal)
                    ? "Completed"
                    : "Failed";
            }

            lastAction = PostProcessingAction.Pipeline;
            PipelineStatus = HasError ? "Completed with issues" : "Completed";
        }
        finally
        {
            EndOperation();
        }
    }

    public async Task RunCleanupAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginOperation())
        {
            return;
        }

        try
        {
            await RunCleanupCoreAsync(cancellationToken);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task RunCleanupCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SourceText))
        {
            ErrorMessage = "Enter or select transcript text before running cleanup.";
            return;
        }

        var sourceSnapshot = SourceText;
        originalSourceText ??= sourceSnapshot;
        lastAction = PostProcessingAction.Cleanup;

        TranscriptAiResult result;
        try
        {
            result = await transcriptAiService.CleanUpAsync(sourceSnapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cleanup failed: {ex.Message} Check the local provider and retry.";
            return;
        }

        if (!string.Equals(SourceText, sourceSnapshot, StringComparison.Ordinal))
        {
            ErrorMessage = "The source transcript changed while cleanup was running. Retry cleanup for the current text.";
            return;
        }

        if (!result.Success || string.IsNullOrWhiteSpace(result.OutputText))
        {
            ErrorMessage = result.ErrorMessage ?? "Cleanup did not produce any text.";
            return;
        }

        var variant = new PostProcessingVariantViewModel(
            DisplayName: $"Cleaned {Variants.Count(static item => item.Stage == "Cleanup") + 1}",
            Stage: "Cleanup",
            LanguageCode: null,
            OutputText: result.OutputText,
            Provider: "local-openai",
            Model: transcriptAiService.Settings.Model);

        Variants.Add(variant);
        SelectedVariant = variant;
    }

    public async Task RunTranslationAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginOperation())
        {
            return;
        }

        try
        {
            await RunTranslationCoreAsync(cancellationToken);
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task RunTranslationCoreAsync(
        CancellationToken cancellationToken,
        PostProcessingVariantViewModel? sourceVariant = null)
    {
        var sourceSnapshot = sourceVariant?.OutputText ?? SourceText;
        var displayedSourceSnapshot = SourceText;
        if (string.IsNullOrWhiteSpace(sourceSnapshot))
        {
            ErrorMessage = "Enter or select transcript text before running translation.";
            return;
        }

        var languages = TargetLanguages
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (languages.Length == 0)
        {
            ErrorMessage = "Enter at least one target language code, such as es or fr.";
            return;
        }

        originalSourceText ??= displayedSourceSnapshot;
        lastAction = PostProcessingAction.Translation;
        var source = sourceVariant?.Transcript ?? (selectedTranscript is not null &&
                     string.Equals(GetTranscriptText(selectedTranscript), sourceSnapshot, StringComparison.Ordinal)
            ? selectedTranscript
            : TranscriptDocument.FromSegments([new TranscriptSegment(sourceSnapshot, 0, 0)]));
        TranslationStageResult result;
        try
        {
            result = await translationService.TranslateAsync(source, languages, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Translation failed: {ex.Message} Check the translation provider and retry.";
            return;
        }

        if (!string.Equals(SourceText, displayedSourceSnapshot, StringComparison.Ordinal))
        {
            ErrorMessage = "The source transcript changed while translation was running. Retry translation for the current text.";
            return;
        }

        foreach (var translated in result.Variants)
        {
            var outputText = string.Join(Environment.NewLine, translated.Transcript.Segments.Select(static segment => segment.Text));
            var variant = new PostProcessingVariantViewModel(
                DisplayName: $"{translated.LanguageDisplayName} ({translated.LanguageCode})",
                Stage: "Translation",
                LanguageCode: translated.LanguageCode,
                OutputText: outputText,
                Provider: translated.Provider,
                Model: translated.Model,
                Transcript: translated.Transcript);

            Variants.Add(variant);
            SelectedVariant = variant;
        }

        foreach (var artifact in result.SubtitleArtifacts)
        {
            Artifacts.Add(PipelineArtifactViewModel.Subtitle(
                artifact.LanguageCode,
                artifact.Format,
                artifact.FileExtension,
                artifact.Content));
        }

        if (result.Variants.Count == 0 || !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ErrorMessage = result.ErrorMessage ?? "Translation did not produce any text.";
        }
    }

    public async Task RunVoiceAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginOperation())
        {
            return;
        }

        VoiceStatus = "Running";
        try
        {
            await RunVoiceCoreAsync(cancellationToken);
            VoiceStatus = HasError ? "Failed" : "Completed";
        }
        finally
        {
            EndOperation();
        }
    }

    private async Task RunVoiceCoreAsync(
        CancellationToken cancellationToken,
        PostProcessingVariantViewModel? sourceVariant = null)
    {
        var sourceSnapshot = sourceVariant?.OutputText ?? SourceText;
        var displayedSourceSnapshot = SourceText;
        if (string.IsNullOrWhiteSpace(sourceSnapshot))
        {
            ErrorMessage = "Enter or select transcript text before running voice generation.";
            return;
        }

        var source = sourceVariant?.Transcript ?? (selectedTranscript is not null &&
                     string.Equals(GetTranscriptText(selectedTranscript), sourceSnapshot, StringComparison.Ordinal)
            ? selectedTranscript
            : TranscriptDocument.FromSegments([new TranscriptSegment(sourceSnapshot, 0, 0)]));
        var profile = new VoiceProfile(VoiceProfileName, VoiceId);
        lastAction = PostProcessingAction.Voice;

        VoiceGenerationStageResult result;
        try
        {
            result = await voiceGenerationService.GenerateAsync(
                source,
                profile,
                sourceVariant?.LanguageCode,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Voice generation failed: {ex.Message} Check the voice provider and retry.";
            return;
        }

        if (!string.Equals(SourceText, displayedSourceSnapshot, StringComparison.Ordinal))
        {
            ErrorMessage = "The source transcript changed while voice generation was running. Retry for the current text.";
            return;
        }

        foreach (var artifact in result.Artifacts)
        {
            Artifacts.Add(PipelineArtifactViewModel.Audio(
                artifact.LanguageCode,
                artifact.Format,
                artifact.FileExtension,
                artifact.AudioBytes));
        }

        if (result.Artifacts.Count == 0 || !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ErrorMessage = result.ErrorMessage ?? "Voice generation did not produce an audio artifact.";
        }
    }

    public async Task ExportArtifactsAsync(CancellationToken cancellationToken = default)
    {
        if (Artifacts.Count == 0)
        {
            ExportStatus = "No artifacts are available to export.";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            ExportStatus = "Choose an output directory before exporting.";
            return;
        }

        try
        {
            var directory = Path.GetFullPath(OutputDirectory);
            Directory.CreateDirectory(directory);

            foreach (var artifact in Artifacts)
            {
                var path = Path.Combine(directory, artifact.FileName);
                if (artifact.BinaryContent is not null)
                {
                    await File.WriteAllBytesAsync(path, artifact.BinaryContent, cancellationToken);
                }
                else
                {
                    await File.WriteAllTextAsync(path, artifact.TextContent ?? string.Empty, cancellationToken);
                }
            }

            ExportStatus = Artifacts.Count == 1
                ? "Exported 1 artifact."
                : $"Exported {Artifacts.Count} artifacts.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ExportStatus = $"Artifact export failed: {ex.Message}";
        }
    }

    public async Task RetryAsync(CancellationToken cancellationToken = default)
    {
        switch (lastAction)
        {
            case PostProcessingAction.Cleanup:
                await RunCleanupAsync(cancellationToken);
                break;
            case PostProcessingAction.Translation:
                await RunTranslationAsync(cancellationToken);
                break;
            case PostProcessingAction.Voice:
                await RunVoiceAsync(cancellationToken);
                break;
            case PostProcessingAction.Pipeline:
                await RunPipelineAsync(cancellationToken);
                break;
            default:
                ErrorMessage = "Run cleanup or translation before retrying.";
                break;
        }
    }

    public void PromoteSelectedVariant()
    {
        if (IsBusy)
        {
            ErrorMessage = "Wait for the running stage to finish before promoting a variant.";
            return;
        }

        if (SelectedVariant is null)
        {
            ErrorMessage = "Select a generated variant before promoting it.";
            return;
        }

        originalSourceText ??= SourceText;
        SourceText = SelectedVariant.OutputText;
        if (SelectedVariant.Transcript is not null)
        {
            selectedTranscript = SelectedVariant.Transcript;
        }
        ErrorMessage = null;
    }

    public void RevertSource()
    {
        if (IsBusy)
        {
            ErrorMessage = "Wait for the running stage to finish before restoring the source.";
            return;
        }

        if (originalSourceText is null)
        {
            ErrorMessage = "No original source is available to restore.";
            return;
        }

        SourceText = originalSourceText;
        selectedTranscript = originalTranscript;
        ErrorMessage = null;
    }

    private bool TryBeginOperation()
    {
        if (IsBusy)
        {
            ErrorMessage = "Another post-processing stage is already running. Wait for it to finish, then retry.";
            return false;
        }

        ErrorMessage = null;
        IsBusy = true;
        return true;
    }

    private void EndOperation()
    {
        IsBusy = false;
    }

    private static string GetTranscriptText(TranscriptDocument transcript) =>
        string.Join(Environment.NewLine, transcript.Segments.Select(static segment => segment.Text));

    private enum PostProcessingAction
    {
        None,
        Cleanup,
        Translation,
        Voice,
        Pipeline
    }
}
