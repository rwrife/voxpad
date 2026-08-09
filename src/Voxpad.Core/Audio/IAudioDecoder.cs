namespace Voxpad.Core.Audio;

public interface IAudioDecoder
{
    Task<DecodedAudioPcm> DecodeToMono16KhzPcmAsync(string inputPath, CancellationToken cancellationToken = default);
}
