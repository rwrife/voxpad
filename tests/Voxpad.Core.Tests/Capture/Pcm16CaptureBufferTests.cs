using System.Buffers.Binary;
using Voxpad.Core.Capture;

namespace Voxpad.Core.Tests.Capture;

public sealed class Pcm16CaptureBufferTests
{
    [Fact]
    public void ToDecodedAudioPcm_BuildsWhisperFormatMonoPcm()
    {
        var original = new short[] { 0, 1000, -1000, short.MaxValue, short.MinValue };
        var bytes = new byte[(original.Length * sizeof(short)) + 1];

        for (var i = 0; i < original.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * sizeof(short), sizeof(short)), original[i]);
        }

        bytes[^1] = 0xFF; // odd trailing byte should be ignored

        var captureBuffer = new Pcm16CaptureBuffer();
        captureBuffer.Append(bytes);

        var decoded = captureBuffer.ToDecodedAudioPcm();

        Assert.Equal(16_000, decoded.SampleRateHz);
        Assert.Equal(1, decoded.Channels);
        Assert.Equal(original, decoded.Samples);
    }
}
