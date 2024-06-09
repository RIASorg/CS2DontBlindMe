namespace CS2DontBlindMe;

public interface IBrightnessChanger : IDisposable
{
    public bool UpdateBrightness(float percentage);
    public bool CanWork();
}