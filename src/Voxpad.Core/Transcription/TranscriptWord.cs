namespace Voxpad.Core.Transcription;

public sealed record TranscriptWord(string Text, long StartMs, long EndMs);
