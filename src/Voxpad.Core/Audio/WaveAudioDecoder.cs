using System.Buffers.Binary;
using System.Diagnostics;

namespace Voxpad.Core.Audio;

public sealed class WaveAudioDecoder : IAudioDecoder
{
    private readonly string? ffmpegPath;

    public WaveAudioDecoder(string? ffmpegPath = "ffmpeg")
    {
        this.ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? null : ffmpegPath;
    }

    public async Task<DecodedAudioPcm> DecodeToMono16KhzPcmAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Audio input not found.", inputPath);
        }

        if (Path.GetExtension(inputPath).Equals(".wav", StringComparison.OrdinalIgnoreCase))
        {
            await using var stream = File.OpenRead(inputPath);
            return DecodeWaveStream(stream);
        }

        if (ffmpegPath is null)
        {
            throw new NotSupportedException("Only WAV decoding is available unless ffmpeg is configured.");
        }

        return await DecodeWithFfmpegAsync(inputPath, cancellationToken);
    }

    private static DecodedAudioPcm DecodeWaveStream(Stream wavStream)
    {
        using var reader = new BinaryReader(wavStream, System.Text.Encoding.UTF8, leaveOpen: true);

        var riff = new string(reader.ReadChars(4));
        if (!string.Equals(riff, "RIFF", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected RIFF header.");
        }

        _ = reader.ReadInt32();

        var wave = new string(reader.ReadChars(4));
        if (!string.Equals(wave, "WAVE", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected WAVE header.");
        }

        WavFormat? fmt = null;
        byte[]? data = null;

        while (wavStream.Position + 8 <= wavStream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            if (chunkId.Length < 4)
            {
                break;
            }

            var chunkSize = reader.ReadInt32();
            if (chunkSize < 0)
            {
                throw new InvalidDataException("Invalid chunk size in WAV file.");
            }

            var chunkData = reader.ReadBytes(chunkSize);
            if (chunkData.Length != chunkSize)
            {
                throw new EndOfStreamException("Unexpected end of WAV stream.");
            }

            if ((chunkSize & 1) == 1 && wavStream.Position < wavStream.Length)
            {
                _ = reader.ReadByte();
            }

            if (string.Equals(chunkId, "fmt ", StringComparison.Ordinal))
            {
                fmt = ParseWavFormat(chunkData);
            }
            else if (string.Equals(chunkId, "data", StringComparison.Ordinal))
            {
                data = chunkData;
            }
        }

        if (fmt is null)
        {
            throw new InvalidDataException("WAV file missing format chunk.");
        }

        if (data is null)
        {
            throw new InvalidDataException("WAV file missing data chunk.");
        }

        var monoSamples = ConvertToMonoFloatSamples(data, fmt.Value);
        var resampled = ResampleLinear(monoSamples, fmt.Value.SampleRate, DecodedAudioPcm.WhisperSampleRateHz);
        var pcm16 = ConvertFloatToPcm16(resampled);

        return new DecodedAudioPcm(DecodedAudioPcm.WhisperSampleRateHz, channels: 1, pcm16);
    }

    private async Task<DecodedAudioPcm> DecodeWithFfmpegAsync(string inputPath, CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"voxpad-{Guid.NewGuid():N}.wav");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath!,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add("1");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(DecodedAudioPcm.WhisperSampleRateHz.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("wav");
            startInfo.ArgumentList.Add(tempPath);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start ffmpeg process.");

            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var stderr = await stderrTask;
                throw new InvalidOperationException($"ffmpeg failed with exit code {process.ExitCode}: {stderr}");
            }

            await using var decoded = File.OpenRead(tempPath);
            return DecodeWaveStream(decoded);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static WavFormat ParseWavFormat(byte[] chunkData)
    {
        if (chunkData.Length < 16)
        {
            throw new InvalidDataException("Invalid fmt chunk in WAV file.");
        }

        var audioFormat = BinaryPrimitives.ReadUInt16LittleEndian(chunkData.AsSpan(0, 2));
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(chunkData.AsSpan(2, 2));
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunkData.AsSpan(4, 4));
        var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(chunkData.AsSpan(14, 2));

        return new WavFormat(audioFormat, channels, sampleRate, bitsPerSample);
    }

    private static float[] ConvertToMonoFloatSamples(byte[] interleavedData, WavFormat format)
    {
        if (format.Channels <= 0)
        {
            throw new InvalidDataException("WAV channels value must be > 0.");
        }

        if (format.SampleRate <= 0)
        {
            throw new InvalidDataException("WAV sample rate must be > 0.");
        }

        var bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample <= 0)
        {
            throw new InvalidDataException($"Unsupported bits-per-sample: {format.BitsPerSample}.");
        }

        var frameSize = bytesPerSample * format.Channels;
        if (frameSize <= 0 || interleavedData.Length < frameSize)
        {
            return Array.Empty<float>();
        }

        var frameCount = interleavedData.Length / frameSize;
        var mono = new float[frameCount];

        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            var baseOffset = frame * frameSize;

            for (var channel = 0; channel < format.Channels; channel++)
            {
                var sampleOffset = baseOffset + (channel * bytesPerSample);
                sum += ReadSampleAsFloat(interleavedData, sampleOffset, format);
            }

            mono[frame] = sum / format.Channels;
        }

        return mono;
    }

    private static float ReadSampleAsFloat(byte[] source, int offset, WavFormat format)
    {
        if (format.AudioFormat == 1)
        {
            return format.BitsPerSample switch
            {
                8 => (source[offset] - 128) / 128f,
                16 => BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset, 2)) / 32768f,
                24 => Read24BitPcm(source, offset) / 8388608f,
                32 => BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset, 4)) / 2147483648f,
                _ => throw new InvalidDataException($"Unsupported PCM bit depth: {format.BitsPerSample}.")
            };
        }

        if (format.AudioFormat == 3)
        {
            if (format.BitsPerSample != 32)
            {
                throw new InvalidDataException("IEEE float WAV must be 32-bit.");
            }

            return BitConverter.ToSingle(source, offset);
        }

        throw new InvalidDataException($"Unsupported WAV encoding format: {format.AudioFormat}.");
    }

    private static int Read24BitPcm(byte[] source, int offset)
    {
        var value = source[offset] | (source[offset + 1] << 8) | (source[offset + 2] << 16);
        if ((value & 0x00800000) != 0)
        {
            value |= unchecked((int)0xFF000000);
        }

        return value;
    }

    private static float[] ResampleLinear(float[] source, int sourceSampleRate, int targetSampleRate)
    {
        if (source.Length == 0)
        {
            return source;
        }

        if (sourceSampleRate == targetSampleRate)
        {
            return source;
        }

        var outputLength = Math.Max(1, (int)Math.Round(source.Length * targetSampleRate / (double)sourceSampleRate));
        var output = new float[outputLength];

        if (source.Length == 1)
        {
            Array.Fill(output, source[0]);
            return output;
        }

        var ratio = sourceSampleRate / (double)targetSampleRate;

        for (var i = 0; i < outputLength; i++)
        {
            var position = i * ratio;
            var left = (int)Math.Floor(position);
            var right = Math.Min(left + 1, source.Length - 1);
            var fraction = position - left;
            output[i] = (float)(source[left] + ((source[right] - source[left]) * fraction));
        }

        return output;
    }

    private static short[] ConvertFloatToPcm16(float[] samples)
    {
        var output = new short[samples.Length];

        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1f, 0.9999695f);
            output[i] = (short)Math.Round(clamped * short.MaxValue);
        }

        return output;
    }

    private readonly record struct WavFormat(ushort AudioFormat, ushort Channels, int SampleRate, ushort BitsPerSample);
}
