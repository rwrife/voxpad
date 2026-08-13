namespace Voxpad.Core.Models;

public sealed record InstalledWhisperModel(
    WhisperModelInfo Model,
    string FilePath,
    long FileSizeBytes);
