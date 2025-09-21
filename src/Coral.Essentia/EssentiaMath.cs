namespace Coral.Essentia;

public class EssentiaMath
{
    public static float HzToMel(float hz) => 1127.01048f * (float)Math.Log(hz / 700.0f + 1.0f);
    public static float MelToHz(float mel) => 700.0f * ((float)Math.Exp(mel / 1127.01048f) - 1.0f);
}