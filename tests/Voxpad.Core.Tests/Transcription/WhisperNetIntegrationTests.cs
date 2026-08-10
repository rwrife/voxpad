using Voxpad.Core.Audio;
using Voxpad.Core.Transcription;
using Xunit;

namespace Voxpad.Core.Tests.Transcription;

public sealed class WhisperNetIntegrationTests
{
    [SkippableFact]
    public async Task WhisperNetManagedBackend_TranscribesBundledWav_WithMonotonicTimestamps()
    {
        var modelPath = Environment.GetEnvironmentVariable("VOXPAD_WHISPER_MODEL_PATH");
        Skip.If(string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath),
            "Set VOXPAD_WHISPER_MODEL_PATH to a local tiny Whisper model file to run this integration test.");

        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jfk.wav");
        Skip.If(!File.Exists(fixturePath), $"Fixture missing at {fixturePath}");

        var options = new WhisperTranscriptionOptions
        {
            ModelPath = modelPath!,
            BackendPreference = WhisperBackendPreference.ManagedOnly,
            Language = "en",
            EnableWordTimestamps = true
        };

        var transcriber = new WhisperTranscriber(new WaveAudioDecoder(ffmpegPath: null));
        var transcript = await transcriber.TranscribeAsync(fixturePath, options);

        Assert.NotEmpty(transcript.Segments);

        long? previousEnd = null;
        foreach (var segment in transcript.Segments)
        {
            Assert.False(string.IsNullOrWhiteSpace(segment.Text));
            Assert.True(segment.EndMs >= segment.StartMs);
            if (previousEnd is not null)
            {
                Assert.True(segment.StartMs >= previousEnd.Value);
            }

            previousEnd = segment.EndMs;
        }
    }
}
