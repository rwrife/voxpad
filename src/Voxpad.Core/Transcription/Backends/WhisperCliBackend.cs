using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Voxpad.Core.Transcription.Backends;

internal sealed class WhisperCliBackend : IWhisperBackend
{
    public const string BackendName = "whisper-cli";

    public string Name => BackendName;

    public bool IsAvailable(WhisperTranscriptionOptions options)
    {
        var cli = ResolveCliPath(options.WhisperCliPath);
        return !string.IsNullOrWhiteSpace(cli) &&
               !string.IsNullOrWhiteSpace(options.ModelPath) &&
               File.Exists(options.ModelPath);
    }

    public async Task<IReadOnlyList<TranscriptSegment>> TranscribeAsync(
        WhisperTranscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var cli = ResolveCliPath(request.Options.WhisperCliPath)
            ?? throw new InvalidOperationException("No whisper-cli path was provided.");

        if (!File.Exists(request.Options.ModelPath))
        {
            throw new FileNotFoundException("Whisper model file not found.", request.Options.ModelPath);
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"voxpad-whisper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var wavPath = Path.Combine(tempDir, "input.wav");
            await File.WriteAllBytesAsync(wavPath, request.Audio.ToWaveBytes(), cancellationToken);

            var outputBase = Path.Combine(tempDir, "result");
            var outputJson = outputBase + ".json";

            var psi = new ProcessStartInfo
            {
                FileName = cli,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add(request.Options.ModelPath);
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(wavPath);
            psi.ArgumentList.Add("-oj");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add(outputBase);

            if (!string.IsNullOrWhiteSpace(request.Options.Language) &&
                !string.Equals(request.Options.Language, "auto", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("-l");
                psi.ArgumentList.Add(request.Options.Language);
            }

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Unable to start whisper-cli.");

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"whisper-cli failed with exit code {process.ExitCode}: {stderr}");
            }

            if (!File.Exists(outputJson))
            {
                throw new FileNotFoundException("whisper-cli did not produce a JSON output file.", outputJson);
            }

            await using var fs = File.OpenRead(outputJson);
            using var doc = await JsonDocument.ParseAsync(fs, cancellationToken: cancellationToken);
            return ParseSegments(doc.RootElement);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static string? ResolveCliPath(string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (Path.IsPathRooted(configuredPath) || configuredPath.Contains(Path.DirectorySeparatorChar) || configuredPath.Contains(Path.AltDirectorySeparatorChar))
            {
                return File.Exists(configuredPath) ? configuredPath : null;
            }

            return configuredPath;
        }

        return null;
    }

    private static IReadOnlyList<TranscriptSegment> ParseSegments(JsonElement root)
    {
        var rawSegments = ExtractSegmentArray(root);
        var segments = new List<TranscriptSegment>();

        foreach (var item in rawSegments)
        {
            var text = item.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : string.Empty;

            var startMs = TryReadSegmentStart(item, out var start) ? start : 0;
            var endMs = TryReadSegmentEnd(item, out var end) ? end : startMs;

            if (endMs < startMs)
            {
                endMs = startMs;
            }

            segments.Add(new TranscriptSegment(text, startMs, endMs, ParseWords(item)));
        }

        return segments;
    }

    private static IReadOnlyList<JsonElement> ExtractSegmentArray(JsonElement root)
    {
        if (root.TryGetProperty("segments", out var directSegments) && directSegments.ValueKind == JsonValueKind.Array)
        {
            return directSegments.EnumerateArray().ToArray();
        }

        if (root.TryGetProperty("result", out var resultElement))
        {
            if (resultElement.TryGetProperty("segments", out var resultSegments) && resultSegments.ValueKind == JsonValueKind.Array)
            {
                return resultSegments.EnumerateArray().ToArray();
            }

            if (resultElement.TryGetProperty("transcription", out var transcription) && transcription.ValueKind == JsonValueKind.Array)
            {
                return transcription.EnumerateArray().ToArray();
            }
        }

        if (root.TryGetProperty("transcription", out var directTranscription) && directTranscription.ValueKind == JsonValueKind.Array)
        {
            return directTranscription.EnumerateArray().ToArray();
        }

        return Array.Empty<JsonElement>();
    }

    private static bool TryReadSegmentStart(JsonElement segment, out long milliseconds)
    {
        if (segment.TryGetProperty("start", out var start) && TryReadTimeValue(start, treatNumbersAsSeconds: true, out milliseconds))
        {
            return true;
        }

        if (segment.TryGetProperty("offsets", out var offsets) && offsets.TryGetProperty("from", out var fromOffset) &&
            TryReadTimeValue(fromOffset, treatNumbersAsSeconds: false, out milliseconds))
        {
            return true;
        }

        if (segment.TryGetProperty("timestamps", out var timestamps) && timestamps.TryGetProperty("from", out var fromTimestamp) &&
            TryReadTimeValue(fromTimestamp, treatNumbersAsSeconds: false, out milliseconds))
        {
            return true;
        }

        milliseconds = 0;
        return false;
    }

    private static bool TryReadSegmentEnd(JsonElement segment, out long milliseconds)
    {
        if (segment.TryGetProperty("end", out var end) && TryReadTimeValue(end, treatNumbersAsSeconds: true, out milliseconds))
        {
            return true;
        }

        if (segment.TryGetProperty("offsets", out var offsets) && offsets.TryGetProperty("to", out var toOffset) &&
            TryReadTimeValue(toOffset, treatNumbersAsSeconds: false, out milliseconds))
        {
            return true;
        }

        if (segment.TryGetProperty("timestamps", out var timestamps) && timestamps.TryGetProperty("to", out var toTimestamp) &&
            TryReadTimeValue(toTimestamp, treatNumbersAsSeconds: false, out milliseconds))
        {
            return true;
        }

        milliseconds = 0;
        return false;
    }

    private static bool TryReadTimeValue(JsonElement element, bool treatNumbersAsSeconds, out long milliseconds)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
            {
                var value = element.GetDouble();
                milliseconds = treatNumbersAsSeconds
                    ? (long)Math.Round(value * 1000d)
                    : (long)Math.Round(value);
                return true;
            }
            case JsonValueKind.String:
            {
                var text = element.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    milliseconds = 0;
                    return false;
                }

                text = text.Trim();

                if (text.Contains(':'))
                {
                    text = text.Replace(',', '.');
                    if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out var ts) ||
                        TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts))
                    {
                        milliseconds = (long)Math.Round(ts.TotalMilliseconds);
                        return true;
                    }
                }

                if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
                {
                    milliseconds = treatNumbersAsSeconds
                        ? (long)Math.Round(numeric * 1000d)
                        : (long)Math.Round(numeric);
                    return true;
                }

                break;
            }
        }

        milliseconds = 0;
        return false;
    }

    private static IReadOnlyList<TranscriptWord> ParseWords(JsonElement segment)
    {
        if (!segment.TryGetProperty("words", out var wordsElement) || wordsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<TranscriptWord>();
        }

        var words = new List<TranscriptWord>();

        foreach (var word in wordsElement.EnumerateArray())
        {
            var text = word.TryGetProperty("text", out var textElement)
                ? textElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            long startMs = 0;
            long endMs = 0;

            var hasStart = word.TryGetProperty("start", out var startElement) &&
                           TryReadTimeValue(startElement, treatNumbersAsSeconds: true, out startMs);
            var hasEnd = word.TryGetProperty("end", out var endElement) &&
                         TryReadTimeValue(endElement, treatNumbersAsSeconds: true, out endMs);

            if (!hasStart)
            {
                startMs = 0;
            }

            if (!hasEnd)
            {
                endMs = startMs;
            }

            if (endMs < startMs)
            {
                endMs = startMs;
            }

            words.Add(new TranscriptWord(text.Trim(), startMs, endMs));
        }

        return words;
    }
}
