namespace Voxpad.Core.Capture;

public readonly record struct AudioLevelSample(float Rms, float Peak)
{
    public static readonly AudioLevelSample Silence = new(0f, 0f);
}
