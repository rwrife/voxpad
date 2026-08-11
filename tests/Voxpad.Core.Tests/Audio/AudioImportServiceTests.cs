using Voxpad.Core.Audio;

namespace Voxpad.Core.Tests.Audio;

public sealed class AudioImportServiceTests
{
    [Fact]
    public async Task ImportAsync_UsesDecoderForSupportedFileTypes()
    {
        var inputPath = CreateTempFileWithExtension(".mp3");
        var expected = new DecodedAudioPcm(16_000, 1, new short[] { 1, -1, 2, -2 });
        var decoder = new FakeAudioDecoder(expected);
        var service = new AudioImportService(decoder);

        try
        {
            var result = await service.ImportAsync(inputPath);

            Assert.Same(expected, result);
            Assert.Equal(inputPath, decoder.LastInputPath);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ImportAsync_ThrowsForUnsupportedExtension()
    {
        var inputPath = CreateTempFileWithExtension(".flac");
        var service = new AudioImportService(new FakeAudioDecoder(new DecodedAudioPcm(16_000, 1, Array.Empty<short>())));

        try
        {
            var exception = await Assert.ThrowsAsync<NotSupportedException>(() => service.ImportAsync(inputPath));
            Assert.Contains(".flac", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(inputPath);
        }
    }

    [Fact]
    public async Task ImportAsync_ThrowsWhenFileMissing()
    {
        var service = new AudioImportService(new FakeAudioDecoder(new DecodedAudioPcm(16_000, 1, Array.Empty<short>())));

        await Assert.ThrowsAsync<FileNotFoundException>(() => service.ImportAsync(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.wav")));
    }

    [Theory]
    [InlineData("clip.wav")]
    [InlineData("clip.MP3")]
    [InlineData("clip.m4a")]
    [InlineData("clip.mp4")]
    [InlineData("clip.mov")]
    public void IsSupportedInputPath_ReturnsTrueForAllowedExtensions(string fileName)
    {
        var service = new AudioImportService(new FakeAudioDecoder(new DecodedAudioPcm(16_000, 1, Array.Empty<short>())));

        Assert.True(service.IsSupportedInputPath(fileName));
    }

    private static string CreateTempFileWithExtension(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"voxpad-audio-import-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01, 0x02, 0x03 });
        return path;
    }

    private sealed class FakeAudioDecoder : IAudioDecoder
    {
        private readonly DecodedAudioPcm output;

        public FakeAudioDecoder(DecodedAudioPcm output)
        {
            this.output = output;
        }

        public string? LastInputPath { get; private set; }

        public Task<DecodedAudioPcm> DecodeToMono16KhzPcmAsync(string inputPath, CancellationToken cancellationToken = default)
        {
            LastInputPath = inputPath;
            return Task.FromResult(output);
        }
    }
}
