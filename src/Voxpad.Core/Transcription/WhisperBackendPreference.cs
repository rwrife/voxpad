namespace Voxpad.Core.Transcription;

public enum WhisperBackendPreference
{
    ManagedThenCli = 0,
    ManagedOnly = 1,
    CliOnly = 2,
    CliThenManaged = 3
}
