using Whisper.net;

namespace Voxpad.Core.Transcription.Backends;

internal sealed class WhisperNetBackend : IWhisperBackend
{
    public const string BackendName = "whisper-net";

    public string Name => BackendName;

    public bool IsAvailable(WhisperTranscriptionOptions options)
    {
        return !string.IsNullOrWhiteSpace(options.ModelPath) && File.Exists(options.ModelPath);
    }

    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        WhisperTranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(request.Options.ModelPath))
        {
            throw new FileNotFoundException("Whisper model file not found.", request.Options.ModelPath);
        }

        using var factory = WhisperFactory.FromPath(request.Options.ModelPath);
        var builder = factory.CreateBuilder().WithThreads(Math.Max(1, request.Options.Threads));

        if (request.Options.TranslateToEnglish)
        {
            builder = builder.WithTranslate();
        }

        if (request.Options.EnableWordTimestamps)
        {
            builder = builder.WithTokenTimestamps();
        }

        if (!string.IsNullOrWhiteSpace(request.Options.Language) &&
            !string.Equals(request.Options.Language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            builder = builder.WithLanguage(request.Options.Language);
        }
        else
        {
            builder = builder.WithLanguageDetection();
        }

        using var processor = builder.Build();
        var segments = new List<TranscriptSegment>();

        await foreach (var segment in processor.ProcessAsync(request.Audio.ToFloatSamples(), cancellationToken))
        {
            segments.Add(MapSegment(segment, request.Options.EnableWordTimestamps));
        }

        return segments;
    }

    private static TranscriptSegment MapSegment(SegmentData segment, bool mapWordTimestamps)
    {
        var words = mapWordTimestamps ? MapWords(segment.Tokens) : Array.Empty<TranscriptWord>();

        return new TranscriptSegment(
            text: segment.Text.Trim(),
            startMs: (long)Math.Round(segment.Start.TotalMilliseconds),
            endMs: (long)Math.Round(segment.End.TotalMilliseconds),
            words: words);
    }

    private static IReadOnlyList<TranscriptWord> MapWords(WhisperToken[]? tokens)
    {
        if (tokens is null || tokens.Length == 0)
        {
            return Array.Empty<TranscriptWord>();
        }

        var words = new List<TranscriptWord>(tokens.Length);

        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token.Text) || token.End <= token.Start)
            {
                continue;
            }

            words.Add(new TranscriptWord(
                token.Text.Trim(),
                StartMs: token.Start * 10,
                EndMs: token.End * 10));
        }

        return words;
    }
}
