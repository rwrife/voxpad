namespace Voxpad.Core.Ai;

public interface ITranscriptAiService
{
    TranscriptAiSettings Settings { get; }

    Task<TranscriptAiProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<TranscriptAiResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default);

    Task<TranscriptAiResult> CleanUpAsync(string transcriptText, CancellationToken cancellationToken = default);

    Task<TranscriptAiResult> AutoTitleAsync(string transcriptText, CancellationToken cancellationToken = default);
}
