using System.Buffers.Binary;

namespace Voxpad.Core.Capture;

public static class AudioLevelMeter
{
    public static AudioLevelSample CalculateFromPcm16Mono(ReadOnlySpan<byte> pcm16)
    {
        var sampleCount = pcm16.Length / 2;
        if (sampleCount <= 0)
        {
            return AudioLevelSample.Silence;
        }

        var peak = 0f;
        var sumSquares = 0d;

        for (var i = 0; i < sampleCount; i++)
        {
            var sample = BinaryPrimitives.ReadInt16LittleEndian(pcm16.Slice(i * 2, 2));
            var normalized = sample / 32768f;
            var abs = Math.Abs(normalized);

            if (abs > peak)
            {
                peak = abs;
            }

            sumSquares += normalized * normalized;
        }

        var rms = (float)Math.Sqrt(sumSquares / sampleCount);
        return new AudioLevelSample(rms, peak);
    }

    public static AudioLevelSample CalculateFromPcm16Interleaved(ReadOnlySpan<byte> interleavedPcm16, int channels)
    {
        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        var frameSize = channels * 2;
        var frameCount = interleavedPcm16.Length / frameSize;
        if (frameCount <= 0)
        {
            return AudioLevelSample.Silence;
        }

        var peak = 0f;
        var sumSquares = 0d;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * frameSize;
            var mono = 0f;

            for (var channel = 0; channel < channels; channel++)
            {
                var sample = BinaryPrimitives.ReadInt16LittleEndian(interleavedPcm16.Slice(frameOffset + (channel * 2), 2));
                mono += sample / 32768f;
            }

            mono /= channels;
            var abs = Math.Abs(mono);
            if (abs > peak)
            {
                peak = abs;
            }

            sumSquares += mono * mono;
        }

        var rms = (float)Math.Sqrt(sumSquares / frameCount);
        return new AudioLevelSample(rms, peak);
    }

    public static AudioLevelSample CalculateFromFloat32Interleaved(ReadOnlySpan<byte> interleavedFloat32, int channels)
    {
        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        var frameSize = channels * sizeof(float);
        var frameCount = interleavedFloat32.Length / frameSize;
        if (frameCount <= 0)
        {
            return AudioLevelSample.Silence;
        }

        var peak = 0f;
        var sumSquares = 0d;

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * frameSize;
            var mono = 0f;

            for (var channel = 0; channel < channels; channel++)
            {
                var offset = frameOffset + (channel * sizeof(float));
                var bits = BinaryPrimitives.ReadInt32LittleEndian(interleavedFloat32.Slice(offset, sizeof(float)));
                mono += BitConverter.Int32BitsToSingle(bits);
            }

            mono /= channels;
            mono = Math.Clamp(mono, -1f, 1f);

            var abs = Math.Abs(mono);
            if (abs > peak)
            {
                peak = abs;
            }

            sumSquares += mono * mono;
        }

        var rms = (float)Math.Sqrt(sumSquares / frameCount);
        return new AudioLevelSample(rms, peak);
    }
}
