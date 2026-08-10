using System.Buffers.Binary;
using Voxpad.Core.Audio;

namespace Voxpad.Core.Tests.Audio;

public sealed class WaveAudioDecoderTests
{
    [Fact]
    public async Task DecodeToMono16KhzPcmAsync_ConvertsStereo44kToMono16k()
    {
        var inputPath = CreateStereoWavFixture(sampleRate: 44_100, durationSeconds: 1.0);

        try
        {
            var decoder = new WaveAudioDecoder(ffmpegPath: null);
            var decoded = await decoder.DecodeToMono16KhzPcmAsync(inputPath);

            Assert.Equal(16_000, decoded.SampleRateHz);
            Assert.Equal(1, decoded.Channels);
            Assert.Equal(16, decoded.BitsPerSample);
            Assert.InRange(decoded.Samples.Length, 15_900, 16_100);
            Assert.True(decoded.Duration > TimeSpan.FromMilliseconds(900));
        }
        finally
        {
            if (File.Exists(inputPath))
            {
                File.Delete(inputPath);
            }
        }
    }

    private static string CreateStereoWavFixture(int sampleRate, double durationSeconds)
    {
        var path = Path.Combine(Path.GetTempPath(), $"voxpad-test-{Guid.NewGuid():N}.wav");
        var channels = 2;
        var bitsPerSample = 16;
        var frameCount = (int)(sampleRate * durationSeconds);
        var bytesPerSample = bitsPerSample / 8;
        var blockAlign = channels * bytesPerSample;
        var byteRate = sampleRate * blockAlign;
        var dataSize = frameCount * blockAlign;

        var bytes = new byte[44 + dataSize];

        bytes[0] = (byte)'R';
        bytes[1] = (byte)'I';
        bytes[2] = (byte)'F';
        bytes[3] = (byte)'F';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), 36 + dataSize);
        bytes[8] = (byte)'W';
        bytes[9] = (byte)'A';
        bytes[10] = (byte)'V';
        bytes[11] = (byte)'E';

        bytes[12] = (byte)'f';
        bytes[13] = (byte)'m';
        bytes[14] = (byte)'t';
        bytes[15] = (byte)' ';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22, 2), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32, 2), (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34, 2), (short)bitsPerSample);

        bytes[36] = (byte)'d';
        bytes[37] = (byte)'a';
        bytes[38] = (byte)'t';
        bytes[39] = (byte)'a';
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40, 4), dataSize);

        const double twoPi = Math.PI * 2;
        const double leftFreq = 220.0;
        const double rightFreq = 330.0;

        var offset = 44;
        for (var i = 0; i < frameCount; i++)
        {
            var t = i / (double)sampleRate;
            var left = (short)(Math.Sin(twoPi * leftFreq * t) * short.MaxValue * 0.6);
            var right = (short)(Math.Sin(twoPi * rightFreq * t) * short.MaxValue * 0.6);

            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset, 2), left);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2, 2), right);
            offset += 4;
        }

        File.WriteAllBytes(path, bytes);
        return path;
    }
}
