namespace Voxpad.Core.Voice;

public sealed record VoiceProfile(
    string ProfileName,
    string VoiceId,
    string? ReferenceInstructions = null)
{
    public bool TryValidate(out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            errorMessage = "Voice profile name is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(VoiceId))
        {
            errorMessage = "Voice profile id is required.";
            return false;
        }

        return true;
    }
}
