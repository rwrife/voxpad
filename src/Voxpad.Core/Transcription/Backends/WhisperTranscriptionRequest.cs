using Voxpad.Core.Audio;

namespace Voxpad.Core.Transcription.Backends;

internal sealed record WhisperTranscriptionRequest(
    DecodedAudioPcm Audio,
    string SourcePath,
    WhisperTranscriptionOptions Options);
