using Voxpad.Core.Audio;

namespace Voxpad.Core.Capture;

public interface IAudioCapture : IAsyncDisposable
{
    bool IsRecording { get; }

    Task StartAsync(Func<AudioLevelSample, ValueTask>? onLevelSample = null, CancellationToken cancellationToken = default);

    Task<DecodedAudioPcm> StopAsync(CancellationToken cancellationToken = default);
}
