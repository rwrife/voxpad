using System.Collections.ObjectModel;
using System.Windows.Input;
using Voxpad.Core.Ai;
using Voxpad.Core.Transcription;
using Voxpad.Core.Translation;
using Voxpad.Desktop.Infrastructure;

namespace Voxpad.Desktop.ViewModels;

public sealed class PostProcessingViewModel : ViewModelBase
{
    private readonly ITranscriptAiService transcriptAiService;
    private readonly ITranslationService translationService;
    private string sourceText = string.Empty;
    private string targetLanguages = "es";
    private string? originalSourceText;
    private TranscriptDocument? selectedTranscript;
    private TranscriptDocument? originalTranscript;
    private PostProcessingVariantViewModel? selectedVariant;
    private string? errorMessage;
    private bool isBusy;
    private PostProcessingAction lastAction;

    public PostProcessingViewModel(
        ITranscriptAiService transcriptAiService,
        ITranslationService translationService)
    {
        this.transcriptAiService = transcriptAiService ?? throw new ArgumentNullException(nameof(transcriptAiService));
        this.translationService = translationService ?? throw new ArgumentNullException(nameof(translationService));

        CleanupCommand = new AsyncCommand(() => RunCleanupAsync());
        TranslationCommand = new AsyncCommand(() => RunTranslationAsync());
        RetryCommand = new AsyncCommand(() => RetryAsync());
        PromoteCommand = new RelayCommand(PromoteSelectedVariant);
        RevertCommand = new RelayCommand(RevertSource);
    }

    public ObservableCollection<PostProcessingVariantViewModel> Variants { get; } = [];

    public ICommand CleanupCommand { get; }

    public ICommand TranslationCommand { get; }

    public ICommand RetryCommand { get; }

    public ICommand PromoteCommand { get; }

    public ICommand RevertCommand { get; }

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
        SelectedVariant = null;
        ErrorMessage = null;
        lastAction = PostProcessingAction.None;
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

    private async Task RunTranslationCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(SourceText))
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

        var sourceSnapshot = SourceText;
        originalSourceText ??= sourceSnapshot;
        lastAction = PostProcessingAction.Translation;
        var source = selectedTranscript is not null &&
                     string.Equals(GetTranscriptText(selectedTranscript), sourceSnapshot, StringComparison.Ordinal)
            ? selectedTranscript
            : TranscriptDocument.FromSegments([new TranscriptSegment(sourceSnapshot, 0, 0)]);
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

        if (!string.Equals(SourceText, sourceSnapshot, StringComparison.Ordinal))
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

        if (result.Variants.Count == 0 || !string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            ErrorMessage = result.ErrorMessage ?? "Translation did not produce any text.";
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
        Translation
    }
}
