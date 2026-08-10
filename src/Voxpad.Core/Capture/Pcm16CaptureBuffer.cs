using System.Buffers;
using System.Buffers.Binary;
using Voxpad.Core.Audio;

namespace Voxpad.Core.Capture;

public sealed class Pcm16CaptureBuffer
{
    private readonly ArrayBufferWriter<byte> writer = new();

    public int ByteCount => writer.WrittenCount;

    public int SampleCount => ByteCount / sizeof(short);

    public void Append(ReadOnlySpan<byte> pcmBytes)
    {
        if (pcmBytes.IsEmpty)
        {
            return;
        }

        var destination = writer.GetSpan(pcmBytes.Length);
        pcmBytes.CopyTo(destination);
        writer.Advance(pcmBytes.Length);
    }

    public DecodedAudioPcm ToDecodedAudioPcm(int sampleRateHz = DecodedAudioPcm.WhisperSampleRateHz)
    {
        if (sampleRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        }

        var written = writer.WrittenSpan;
        var evenByteCount = written.Length - (written.Length % sizeof(short));
        var samples = new short[evenByteCount / sizeof(short)];

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = BinaryPrimitives.ReadInt16LittleEndian(written.Slice(i * sizeof(short), sizeof(short)));
        }

        return new DecodedAudioPcm(sampleRateHz, channels: 1, samples);
    }
}
