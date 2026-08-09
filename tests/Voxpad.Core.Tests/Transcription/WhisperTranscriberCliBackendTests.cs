using System.Diagnostics;
using Voxpad.Core.Audio;
using Voxpad.Core.Transcription;
using Xunit;

namespace Voxpad.Core.Tests.Transcription;

public sealed class WhisperTranscriberCliBackendTests
{
    [SkippableFact]
    public async Task TranscribeAsync_UsesCliFallback_AndBuildsOrderedTranscriptDocument()
    {
        Skip.If(OperatingSystem.IsWindows(), "This test uses a POSIX shell script as a fake whisper-cli.");
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "jfk.wav");
        Assert.True(File.Exists(fixturePath), $"Expected fixture at {fixturePath}");

        var tempDir = Directory.CreateTempSubdirectory("voxpad-cli-fake-");

        try
        {
            var fakeCliPath = Path.Combine(tempDir.FullName, "fake-whisper-cli.sh");
            await File.WriteAllTextAsync(fakeCliPath, FakeCliScript);
            var chmod = Process.Start(new ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{fakeCliPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            Assert.NotNull(chmod);
            await chmod!.WaitForExitAsync();
            Assert.Equal(0, chmod.ExitCode);

            var fakeModelPath = Path.Combine(tempDir.FullName, "ggml-tiny.en.bin");
            await File.WriteAllBytesAsync(fakeModelPath, [0x00]);

            var options = new WhisperTranscriptionOptions
            {
                ModelPath = fakeModelPath,
                WhisperCliPath = fakeCliPath,
                BackendPreference = WhisperBackendPreference.CliOnly,
                Language = "en"
            };

            var transcriber = new WhisperTranscriber(new WaveAudioDecoder(ffmpegPath: null));
            var transcript = await transcriber.TranscribeAsync(fixturePath, options);

            Assert.NotEmpty(transcript.Segments);

            long? previousEnd = null;
            foreach (var segment in transcript.Segments)
            {
                Assert.True(segment.EndMs >= segment.StartMs);
                if (previousEnd is not null)
                {
                    Assert.True(segment.StartMs >= previousEnd.Value);
                }

                previousEnd = segment.EndMs;
            }
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private const string FakeCliScript = "#!/usr/bin/env bash\n" +
                                         "set -euo pipefail\n" +
                                         "output_base=\"\"\n" +
                                         "while [[ $# -gt 0 ]]; do\n" +
                                         "  case \"$1\" in\n" +
                                         "    -of) output_base=\"$2\"; shift 2 ;;\n" +
                                         "    *) shift ;;\n" +
                                         "  esac\n" +
                                         "done\n" +
                                         "cat > \"${output_base}.json\" <<'JSON'\n" +
                                         "{\n" +
                                         "  \"result\": {\n" +
                                         "    \"transcription\": [\n" +
                                         "      {\"text\": \"hello world\", \"offsets\": {\"from\": 0, \"to\": 900}},\n" +
                                         "      {\"text\": \"voxpad test\", \"offsets\": {\"from\": 900, \"to\": 1750}}\n" +
                                         "    ]\n" +
                                         "  }\n" +
                                         "}\n" +
                                         "JSON\n";
}
