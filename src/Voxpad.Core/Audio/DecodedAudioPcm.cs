using System.Buffers.Binary;

namespace Voxpad.Core.Audio;

public sealed class DecodedAudioPcm
{
    public const int WhisperSampleRateHz = 16_000;
    public const short WhisperBitsPerSample = 16;

    public DecodedAudioPcm(int sampleRateHz, int channels, short[] samples)
    {
        if (sampleRateHz <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        }

        if (channels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        ArgumentNullException.ThrowIfNull(samples);

        SampleRateHz = sampleRateHz;
        Channels = channels;
        Samples = samples;
    }

    public int SampleRateHz { get; }

    public int Channels { get; }

    public short[] Samples { get; }

    public short BitsPerSample => WhisperBitsPerSample;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / (SampleRateHz * Channels));

    public float[] ToFloatSamples()
    {
        var floats = new float[Samples.Length];
        for (var i = 0; i < Samples.Length; i++)
        {
            floats[i] = Samples[i] / 32768f;
        }

        return floats;
    }

    public byte[] ToWaveBytes()
    {
        var bytesPerSample = BitsPerSample / 8;
        var byteRate = SampleRateHz * Channels * bytesPerSample;
        var blockAlign = (short)(Channels * bytesPerSample);
        var dataBytes = Samples.Length * bytesPerSample;

        var buffer = new byte[44 + dataBytes];

        buffer[0] = (byte)'R';
        buffer[1] = (byte)'I';
        buffer[2] = (byte)'F';
        buffer[3] = (byte)'F';
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4, 4), 36 + dataBytes);
        buffer[8] = (byte)'W';
        buffer[9] = (byte)'A';
        buffer[10] = (byte)'V';
        buffer[11] = (byte)'E';

        buffer[12] = (byte)'f';
        buffer[13] = (byte)'m';
        buffer[14] = (byte)'t';
        buffer[15] = (byte)' ';
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(22, 2), (short)Channels);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(24, 4), SampleRateHz);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(32, 2), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(34, 2), BitsPerSample);

        buffer[36] = (byte)'d';
        buffer[37] = (byte)'a';
        buffer[38] = (byte)'t';
        buffer[39] = (byte)'a';
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(40, 4), dataBytes);

        var sampleOffset = 44;
        for (var i = 0; i < Samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(sampleOffset + (i * 2), 2), Samples[i]);
        }

        return buffer;
    }
}
