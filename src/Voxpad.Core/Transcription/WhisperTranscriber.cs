using Voxpad.Core.Audio;
using Voxpad.Core.Transcription.Backends;

namespace Voxpad.Core.Transcription;

public sealed class WhisperTranscriber : ITranscriber
{
    private readonly IAudioDecoder audioDecoder;
    private readonly IReadOnlyList<IWhisperBackend> backends;

    public WhisperTranscriber(IAudioDecoder audioDecoder)
        : this(audioDecoder, new IWhisperBackend[] { new WhisperNetBackend(), new WhisperCliBackend() })
    {
    }

    internal WhisperTranscriber(IAudioDecoder audioDecoder, IReadOnlyList<IWhisperBackend> backends)
    {
        this.audioDecoder = audioDecoder ?? throw new ArgumentNullException(nameof(audioDecoder));
        this.backends = backends ?? throw new ArgumentNullException(nameof(backends));

        if (this.backends.Count == 0)
        {
            throw new ArgumentException("At least one whisper backend must be provided.", nameof(backends));
        }
    }

    public async Task<TranscriptDocument> TranscribeAsync(
        string audioPath,
        WhisperTranscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ModelPath))
        {
            throw new ArgumentException("ModelPath is required.", nameof(options));
        }

        var decodedAudio = await audioDecoder.DecodeToMono16KhzPcmAsync(audioPath, cancellationToken);
        var request = new WhisperTranscriptionRequest(decodedAudio, audioPath, options);

        var failures = new List<Exception>();
        var attemptedAnyBackend = false;

        foreach (var backend in GetBackendOrder(options.BackendPreference))
        {
            if (!backend.IsAvailable(options))
            {
                continue;
            }

            attemptedAnyBackend = true;

            try
            {
                var segments = await backend.TranscribeAsync(request, cancellationToken);
                return TranscriptDocument.FromSegments(segments);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new InvalidOperationException($"Backend '{backend.Name}' failed.", ex));
            }
        }

        if (!attemptedAnyBackend)
        {
            throw new InvalidOperationException(
                "No whisper backend is available. Ensure a model file exists, and configure whisper-cli path when using CLI fallback.");
        }

        throw new AggregateException("All available whisper backends failed.", failures);
    }

    private IEnumerable<IWhisperBackend> GetBackendOrder(WhisperBackendPreference preference)
    {
        var managed = backends.FirstOrDefault(b => b.Name == WhisperNetBackend.BackendName);
        var cli = backends.FirstOrDefault(b => b.Name == WhisperCliBackend.BackendName);

        switch (preference)
        {
            case WhisperBackendPreference.ManagedOnly:
                if (managed is not null)
                {
                    yield return managed;
                }

                yield break;

            case WhisperBackendPreference.CliOnly:
                if (cli is not null)
                {
                    yield return cli;
                }

                yield break;

            case WhisperBackendPreference.CliThenManaged:
                if (cli is not null)
                {
                    yield return cli;
                }

                if (managed is not null)
                {
                    yield return managed;
                }

                yield break;

            case WhisperBackendPreference.ManagedThenCli:
            default:
                if (managed is not null)
                {
                    yield return managed;
                }

                if (cli is not null)
                {
                    yield return cli;
                }

                yield break;
        }
    }
}
