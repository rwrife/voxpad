using Voxpad.Core.Ai;
using Voxpad.Core.Models;
using Voxpad.Core.Playback;
using Voxpad.Core.Transcription;
using Voxpad.Core.Translation;
using Voxpad.Core.Voice;
using Voxpad.Desktop.ViewModels;

namespace Voxpad.Core.Tests.Desktop;

public sealed class PostProcessingViewModelTests
{
    [Fact]
    public async Task RunPipelineAsync_OnlyRunsEnabledStages()
    {
        var cleanup = new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned text"));
        var translation = new FakeTranslationService();
        var voice = new FakeVoiceGenerationService();
        var viewModel = new PostProcessingViewModel(cleanup, translation, voice)
        {
            SourceText = "Original text",
            CleanupEnabled = true,
            TranslationEnabled = false,
            VoiceEnabled = false
        };

        await viewModel.RunPipelineAsync();

        Assert.Equal(1, cleanup.Calls);
        Assert.Equal(0, translation.Calls);
        Assert.Equal(0, voice.Calls);
        Assert.Equal("Completed", viewModel.PipelineStatus);
        Assert.Equal("Completed", viewModel.CleanupStatus);
        Assert.Equal("Skipped", viewModel.TranslationStatus);
        Assert.Equal("Skipped", viewModel.VoiceStatus);
        Assert.Equal("Original text", viewModel.SourceText);
        Assert.Equal("Cleaned text", Assert.Single(viewModel.Variants).OutputText);
    }

    [Fact]
    public async Task RunPipelineAsync_WhenNoStageIsEnabled_ReportsActionableStatus()
    {
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            new FakeTranslationService(),
            new FakeVoiceGenerationService())
        {
            SourceText = "Source",
            CleanupEnabled = false,
            TranslationEnabled = false,
            VoiceEnabled = false
        };

        await viewModel.RunPipelineAsync();

        Assert.Equal("No stages enabled", viewModel.PipelineStatus);
        Assert.Contains("enable", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RunPipelineAsync_TranslationUsesCleanupVariantWithoutReplacingSource()
    {
        var translationResult = TranslationStageResult.FromVariants(
            TranscriptDocument.FromSegments([new TranscriptSegment("Cleaned text", 0, 0)]),
            [
                new TranslatedTranscriptVariant(
                    "es",
                    "Spanish",
                    TranscriptDocument.FromSegments([new TranscriptSegment("Texto limpio", 0, 0)]),
                    "test-provider",
                    "translation-model")
            ],
            []);
        var translation = new FakeTranslationService(translationResult);
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned text")),
            translation,
            new FakeVoiceGenerationService())
        {
            SourceText = "Um, original text",
            CleanupEnabled = true,
            TranslationEnabled = true,
            VoiceEnabled = false,
            TargetLanguages = "es"
        };

        await viewModel.RunPipelineAsync();

        Assert.Equal("Cleaned text", Assert.Single(translation.SourceTranscript!.Segments).Text);
        Assert.Equal("Um, original text", viewModel.SourceText);
        Assert.Collection(
            viewModel.Variants,
            cleaned => Assert.Equal("Cleaned text", cleaned.OutputText),
            translated => Assert.Equal("Texto limpio", translated.OutputText));
        Assert.Equal("Completed", viewModel.TranslationStatus);
    }

    [Fact]
    public async Task RunPipelineAsync_VoiceStagePublishesGeneratedAudioArtifact()
    {
        var source = TranscriptDocument.FromSegments([new TranscriptSegment("Narrate me", 0, 1_000)]);
        var voiceResult = VoiceGenerationStageResult.FromArtifacts(
            source,
            [
                new VoiceGenerationArtifact(
                    "source",
                    "Source language",
                    "mp3",
                    "audio/mpeg",
                    ".mp3",
                    "test-provider",
                    "voice-model",
                    "Narrator",
                    "voice-1",
                    [1, 2, 3, 4])
            ]);
        var voice = new FakeVoiceGenerationService(voiceResult);
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            new FakeTranslationService(),
            voice)
        {
            SourceText = "Narrate me",
            CleanupEnabled = false,
            TranslationEnabled = false,
            VoiceEnabled = true,
            VoiceProfileName = "Narrator",
            VoiceId = "voice-1"
        };

        await viewModel.RunPipelineAsync();

        Assert.Equal("Narrate me", Assert.Single(voice.SourceTranscript!.Segments).Text);
        Assert.Equal("Narrator", voice.RequestedProfile!.ProfileName);
        var artifact = Assert.Single(viewModel.Artifacts);
        Assert.Equal("Audio", artifact.Kind);
        Assert.Equal("narration-source.mp3", artifact.FileName);
        Assert.Equal(4, artifact.SizeBytes);
        Assert.Equal("Completed", viewModel.VoiceStatus);
        Assert.Equal("Completed", viewModel.PipelineStatus);
    }

    [Fact]
    public async Task RunPipelineAsync_TranslationPublishesSubtitleArtifacts()
    {
        var source = TranscriptDocument.FromSegments([new TranscriptSegment("Hello", 0, 1_000)]);
        var translated = TranscriptDocument.FromSegments([new TranscriptSegment("Hola", 0, 1_000)]);
        var translation = new FakeTranslationService(
            TranslationStageResult.FromVariants(
                source,
                [new TranslatedTranscriptVariant("es", "Spanish", translated, "test-provider", "translation-model")],
                [new LocalizedSubtitleArtifact("es", "Spanish", "srt", ".srt", "1\n00:00:00,000 --> 00:00:01,000\nHola")]));
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            translation,
            new FakeVoiceGenerationService())
        {
            SourceText = "Hello",
            CleanupEnabled = false,
            TranslationEnabled = true,
            VoiceEnabled = false,
            TargetLanguages = "es"
        };

        await viewModel.RunPipelineAsync();

        var artifact = Assert.Single(viewModel.Artifacts);
        Assert.Equal("Subtitle", artifact.Kind);
        Assert.Equal("transcript-es.srt", artifact.FileName);
        Assert.Contains("Hola", artifact.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportArtifactsAsync_WritesGeneratedFilesToSelectedDirectory()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"voxpad-artifacts-{Guid.NewGuid():N}");
        try
        {
            var source = TranscriptDocument.FromSegments([new TranscriptSegment("Hello", 0, 1_000)]);
            var translated = TranscriptDocument.FromSegments([new TranscriptSegment("Hola", 0, 1_000)]);
            var translation = new FakeTranslationService(
                TranslationStageResult.FromVariants(
                    source,
                    [new TranslatedTranscriptVariant("es", "Spanish", translated, "test-provider", "translation-model")],
                    [new LocalizedSubtitleArtifact("es", "Spanish", "srt", ".srt", "subtitle content")]));
            var viewModel = new PostProcessingViewModel(
                new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
                translation,
                new FakeVoiceGenerationService())
            {
                SourceText = "Hello",
                TranslationEnabled = true,
                OutputDirectory = outputDirectory
            };
            await viewModel.RunPipelineAsync();

            await viewModel.ExportArtifactsAsync();

            Assert.Equal("subtitle content", await File.ReadAllTextAsync(Path.Combine(outputDirectory, "transcript-es.srt")));
            Assert.Equal("Exported 1 artifact.", viewModel.ExportStatus);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RunCleanupAsync_AddsNonDestructiveVariantWithProvenance()
    {
        var ai = new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Hello world."));
        var viewModel = new PostProcessingViewModel(ai, new FakeTranslationService());
        viewModel.SourceText = "Um, hello world.";

        await viewModel.RunCleanupAsync();

        Assert.Equal("Um, hello world.", viewModel.SourceText);
        var variant = Assert.Single(viewModel.Variants);
        Assert.Equal("Cleanup", variant.Stage);
        Assert.Equal("Hello world.", variant.OutputText);
        Assert.Contains("test-model", variant.Provenance, StringComparison.Ordinal);
        Assert.Same(variant, viewModel.SelectedVariant);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RunTranslationAsync_ParsesTargetsAndAddsLanguageVariants()
    {
        var source = TranscriptDocument.FromSegments([new TranscriptSegment("Hello", 0, 1_000)]);
        var translatedEs = TranscriptDocument.FromSegments([new TranscriptSegment("Hola", 0, 1_000)]);
        var translatedFr = TranscriptDocument.FromSegments([new TranscriptSegment("Bonjour", 0, 1_000)]);
        var result = TranslationStageResult.FromVariants(
            source,
            [
                new TranslatedTranscriptVariant("es", "Spanish", translatedEs, "test-provider", "translation-model"),
                new TranslatedTranscriptVariant("fr", "French", translatedFr, "test-provider", "translation-model")
            ],
            []);
        var translation = new FakeTranslationService(result);
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            translation)
        {
            SourceText = "Hello",
            TargetLanguages = "es, fr"
        };

        await viewModel.RunTranslationAsync();

        Assert.Equal(["es", "fr"], translation.RequestedLanguages);
        Assert.Equal(2, viewModel.Variants.Count);
        Assert.Equal("Hola", viewModel.Variants[0].OutputText);
        Assert.Equal("fr", viewModel.Variants[1].LanguageCode);
        Assert.Equal("Hello", viewModel.SourceText);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RunTranslationAsync_WhenServiceThrows_SurfacesRetryGuidance()
    {
        var translation = new FakeTranslationService
        {
            ExceptionToThrow = new InvalidOperationException("provider unavailable")
        };
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            translation)
        {
            SourceText = "Hello",
            TargetLanguages = "es"
        };

        await viewModel.RunTranslationAsync();

        Assert.Contains("provider unavailable", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retry", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(viewModel.Variants);
    }

    [Fact]
    public async Task RunTranslationAsync_WhenCallerCancels_DoesNotSwallowCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var translation = new FakeTranslationService
        {
            ExceptionToThrow = new OperationCanceledException(cancellation.Token)
        };
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            translation)
        {
            SourceText = "Hello"
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => viewModel.RunTranslationAsync(cancellation.Token));
    }

    [Fact]
    public async Task LoadSelectedTranscript_SecondDocumentResetsSessionAndRevertUsesNewSource()
    {
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned first")),
            new FakeTranslationService());
        viewModel.LoadSelectedTranscript(
            TranscriptDocument.FromSegments([new TranscriptSegment("First", 0, 1_000)]));
        await viewModel.RunCleanupAsync();
        Assert.Single(viewModel.Variants);

        var second = TranscriptDocument.FromSegments([new TranscriptSegment("Second", 2_000, 3_000)]);
        viewModel.LoadSelectedTranscript(second);

        Assert.Equal("Second", viewModel.SourceText);
        Assert.Empty(viewModel.Variants);
        Assert.Null(viewModel.SelectedVariant);
        viewModel.SourceText = "Edited second";
        viewModel.RevertSource();
        Assert.Equal("Second", viewModel.SourceText);
    }

    [Fact]
    public async Task RunTranslationAsync_LoadedDocumentPreservesTimestampedHandoffOnVariant()
    {
        var source = TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("Hello", 100, 900),
            new TranscriptSegment("world", 1_200, 2_300)
        ]);
        var translated = TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("Hola", 100, 900),
            new TranscriptSegment("mundo", 1_200, 2_300)
        ]);
        var translation = new FakeTranslationService(
            TranslationStageResult.FromVariants(
                source,
                [new TranslatedTranscriptVariant("es", "Spanish", translated, "test-provider", "translation-model")],
                []));
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            translation);
        viewModel.LoadSelectedTranscript(source);

        await viewModel.RunTranslationAsync();

        Assert.Same(source, translation.SourceTranscript);
        var variant = Assert.Single(viewModel.Variants);
        Assert.Same(translated, variant.Transcript);
        Assert.Collection(
            variant.Transcript!.Segments,
            segment => Assert.Equal((100L, 900L), (segment.StartMs, segment.EndMs)),
            segment => Assert.Equal((1_200L, 2_300L), (segment.StartMs, segment.EndMs)));
    }

    [Fact]
    public async Task RunTranslationAsync_AfterEditingLoadedTranscript_UsesDisplayedTextSnapshot()
    {
        var source = TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("First", 100, 900),
            new TranscriptSegment("Second", 1_000, 2_000)
        ]);
        var translated = TranscriptDocument.FromSegments([new TranscriptSegment("Editado", 0, 0)]);
        var translation = new FakeTranslationService(
            TranslationStageResult.FromVariants(
                source,
                [new TranslatedTranscriptVariant("es", "Spanish", translated, "test-provider", "translation-model")],
                []));
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused")),
            translation);
        viewModel.LoadSelectedTranscript(source);
        viewModel.SourceText = "Edited source";

        await viewModel.RunTranslationAsync();

        var sent = Assert.IsType<TranscriptDocument>(translation.SourceTranscript);
        var segment = Assert.Single(sent.Segments);
        Assert.Equal("Edited source", segment.Text);
        Assert.Equal((0L, 0L), (segment.StartMs, segment.EndMs));
    }

    [Fact]
    public async Task RunTranslationAsync_AfterPromotingCleanup_UsesCleanedText()
    {
        var translation = new FakeTranslationService();
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned source")),
            translation);
        viewModel.LoadSelectedTranscript(
            TranscriptDocument.FromSegments([new TranscriptSegment("Original source", 100, 900)]));
        await viewModel.RunCleanupAsync();
        viewModel.PromoteSelectedVariant();

        await viewModel.RunTranslationAsync();

        var sent = Assert.IsType<TranscriptDocument>(translation.SourceTranscript);
        Assert.Equal("Cleaned source", Assert.Single(sent.Segments).Text);
    }

    [Fact]
    public async Task StageOperations_RejectConcurrentRunsAndExposeBusyState()
    {
        var cleanup = new BlockingTranscriptAiService();
        var translation = new FakeTranslationService();
        var viewModel = new PostProcessingViewModel(cleanup, translation)
        {
            SourceText = "Source"
        };

        var cleanupTask = viewModel.RunCleanupAsync();
        Assert.True(viewModel.IsBusy);

        await viewModel.RunTranslationAsync();

        Assert.Equal(0, translation.Calls);
        Assert.Contains("already running", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        cleanup.Complete(TranscriptAiResult.FromOutput("Cleaned"));
        await cleanupTask;
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task PromoteAndRevert_PreserveOriginalSource()
    {
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned text")),
            new FakeTranslationService())
        {
            SourceText = "Original text"
        };
        await viewModel.RunCleanupAsync();

        viewModel.PromoteSelectedVariant();
        Assert.Equal("Cleaned text", viewModel.SourceText);

        viewModel.RevertSource();
        Assert.Equal("Original text", viewModel.SourceText);
        Assert.Single(viewModel.Variants);
    }

    [Fact]
    public async Task RetryAsync_RepeatsFailedCleanupWithGuidanceClearedOnSuccess()
    {
        var ai = new FakeTranscriptAiService(TranscriptAiResult.Failure("Endpoint timed out. Retry cleanup."));
        var viewModel = new PostProcessingViewModel(ai, new FakeTranslationService())
        {
            SourceText = "Original text"
        };

        await viewModel.RunCleanupAsync();
        Assert.True(viewModel.HasError);
        Assert.Contains("Retry", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        ai.Result = TranscriptAiResult.FromOutput("Cleaned text");
        await viewModel.RetryAsync();

        Assert.Equal(2, ai.Calls);
        Assert.Single(viewModel.Variants);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public async Task RunCleanupAsync_WhenServiceThrows_SurfacesRetryGuidance()
    {
        var ai = new FakeTranscriptAiService(TranscriptAiResult.FromOutput("unused"))
        {
            ExceptionToThrow = new HttpRequestException("connection refused")
        };
        var viewModel = new PostProcessingViewModel(ai, new FakeTranslationService())
        {
            SourceText = "Original text"
        };

        await viewModel.RunCleanupAsync();

        Assert.True(viewModel.HasError);
        Assert.Contains("connection refused", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retry", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(viewModel.Variants);
    }

    [Fact]
    public async Task PromotionCommands_ApplyAndRestoreSelectedVariant()
    {
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned text")),
            new FakeTranslationService())
        {
            SourceText = "Original text"
        };
        await viewModel.RunCleanupAsync();

        viewModel.PromoteCommand.Execute(null);
        Assert.Equal("Cleaned text", viewModel.SourceText);

        viewModel.RevertCommand.Execute(null);
        Assert.Equal("Original text", viewModel.SourceText);
        Assert.NotNull(viewModel.CleanupCommand);
        Assert.NotNull(viewModel.TranslationCommand);
        Assert.NotNull(viewModel.RetryCommand);
    }

    [Fact]
    public async Task UpdateSelectedTranscript_PreservesVariantsAndUsesEditedTimestampedSource()
    {
        var translation = new FakeTranslationService();
        var viewModel = new PostProcessingViewModel(
            new FakeTranscriptAiService(TranscriptAiResult.FromOutput("Cleaned")),
            translation);
        viewModel.LoadSelectedTranscript(
            TranscriptDocument.FromSegments([new TranscriptSegment("Original", 100, 900)]));
        await viewModel.RunCleanupAsync();
        var edited = TranscriptDocument.FromSegments([new TranscriptSegment("Edited", 100, 900)]);

        viewModel.UpdateSelectedTranscript(edited);
        await viewModel.RunTranslationAsync();

        Assert.Equal("Edited", viewModel.SourceText);
        Assert.Equal("Cleaned", Assert.Single(viewModel.Variants).OutputText);
        Assert.Same(edited, translation.SourceTranscript);
    }

    [Fact]
    public void MainWindowViewModel_ExposesPostProcessingWorkspace()
    {
        var viewModel = new MainWindowViewModel(
            new EmptyModelStore(),
            new UnavailableMediaPlayback("Test playback unavailable."));

        Assert.NotNull(viewModel.PostProcessing);
        Assert.NotNull(viewModel.Media);
    }

    [Fact]
    public void MainWindowViewModel_LoadTranscriptSynchronizesEditorAndPipelineSource()
    {
        var viewModel = new MainWindowViewModel(
            new EmptyModelStore(),
            new UnavailableMediaPlayback("Test playback unavailable."));
        var transcript = TranscriptDocument.FromSegments(
        [
            new TranscriptSegment("First", 100, 900),
            new TranscriptSegment("Second", 1_200, 2_300)
        ]);

        viewModel.LoadTranscript(transcript);

        Assert.Equal(2, viewModel.Media.Segments.Count);
        Assert.Equal("First", viewModel.Media.Segments[0].Text);
        Assert.Equal($"First{Environment.NewLine}Second", viewModel.PostProcessing.SourceText);
    }

    private sealed class EmptyModelStore : IModelStore
    {
        public IReadOnlyList<WhisperModelInfo> ListAvailableModels() => [];

        public Task<IReadOnlyList<InstalledWhisperModel>> ListInstalledModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InstalledWhisperModel>>([]);

        public Task<InstalledWhisperModel> DownloadModelAsync(
            string modelId,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task DeleteModelAsync(string modelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SelectModelAsync(string modelId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetSelectedModelIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeTranscriptAiService : ITranscriptAiService
    {
        public FakeTranscriptAiService(TranscriptAiResult result)
        {
            Result = result;
        }

        public TranscriptAiResult Result { get; set; }

        public Exception? ExceptionToThrow { get; set; }

        public int Calls { get; private set; }

        public TranscriptAiSettings Settings { get; } = new()
        {
            Enabled = true,
            Model = "test-model"
        };

        public Task<TranscriptAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TranscriptAiResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TranscriptAiResult> CleanUpAsync(string transcriptText, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }

        public Task<TranscriptAiResult> AutoTitleAsync(string transcriptText, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class BlockingTranscriptAiService : ITranscriptAiService
    {
        private readonly TaskCompletionSource<TranscriptAiResult> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TranscriptAiSettings Settings { get; } = new()
        {
            Enabled = true,
            Model = "blocking-model"
        };

        public void Complete(TranscriptAiResult result) => completion.SetResult(result);

        public Task<TranscriptAiResult> CleanUpAsync(string transcriptText, CancellationToken cancellationToken = default) =>
            completion.Task;

        public Task<TranscriptAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TranscriptAiResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TranscriptAiResult> AutoTitleAsync(string transcriptText, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTranslationService(TranslationStageResult? result = null) : ITranslationService
    {
        public TranslationSettings Settings { get; } = new()
        {
            Enabled = true,
            Model = "translation-model",
            Provider = "test-provider"
        };

        public IReadOnlyList<string> RequestedLanguages { get; private set; } = [];

        public TranscriptDocument? SourceTranscript { get; private set; }

        public Exception? ExceptionToThrow { get; set; }

        public int Calls { get; private set; }

        public Task<TranslationStageResult> TranslateAsync(
            TranscriptDocument sourceTranscript,
            IReadOnlyList<string> targetLanguages,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            SourceTranscript = sourceTranscript;
            RequestedLanguages = targetLanguages.ToArray();
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(result ?? TranslationStageResult.Failure(sourceTranscript, "No fake result configured."));
        }
    }

    private sealed class FakeVoiceGenerationService(VoiceGenerationStageResult? result = null) : IVoiceGenerationService
    {
        public VoiceGenerationSettings Settings { get; } = new()
        {
            Enabled = true,
            Model = "voice-model",
            Provider = "test-provider"
        };

        public int Calls { get; private set; }

        public TranscriptDocument? SourceTranscript { get; private set; }

        public VoiceProfile? RequestedProfile { get; private set; }

        public Task<VoiceGenerationStageResult> GenerateAsync(
            TranscriptDocument transcriptVariant,
            VoiceProfile voiceProfile,
            string? languageCode = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            SourceTranscript = transcriptVariant;
            RequestedProfile = voiceProfile;
            return Task.FromResult(result ?? VoiceGenerationStageResult.Failure(transcriptVariant, "No fake result configured."));
        }
    }
}
