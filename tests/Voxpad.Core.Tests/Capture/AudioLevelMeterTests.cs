using System.Buffers.Binary;
using Voxpad.Core.Capture;

namespace Voxpad.Core.Tests.Capture;

public sealed class AudioLevelMeterTests
{
    [Fact]
    public void CalculateFromPcm16Mono_ComputesPeakAndRms()
    {
        var samples = new short[] { 0, 16384, -16384 };
        var bytes = new byte[samples.Length * sizeof(short)];

        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * sizeof(short), sizeof(short)), samples[i]);
        }

        var meter = AudioLevelMeter.CalculateFromPcm16Mono(bytes);

        Assert.InRange(meter.Peak, 0.49f, 0.51f);
        Assert.InRange(meter.Rms, 0.40f, 0.42f);
    }

    [Fact]
    public void CalculateFromPcm16Interleaved_DownmixesChannelsBeforeMetering()
    {
        // 2 channels, 2 frames: [0.5, 0.5], [-0.5, -0.5]
        var interleaved = new short[] { 16384, 16384, -16384, -16384 };
        var bytes = new byte[interleaved.Length * sizeof(short)];
        for (var i = 0; i < interleaved.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * sizeof(short), sizeof(short)), interleaved[i]);
        }

        var meter = AudioLevelMeter.CalculateFromPcm16Interleaved(bytes, channels: 2);

        Assert.InRange(meter.Peak, 0.49f, 0.51f);
        Assert.InRange(meter.Rms, 0.49f, 0.51f);
    }
}
