#region using

using Microsoft.Extensions.Logging;

#endregion

namespace CS2DontBlindMe;

public interface IBrightnessChanger : IDisposable
{
    public void SetLogger(ILogger logger);
    public bool TryInitialize();
    public bool UpdateBrightness(float percentage);
    public void DiagnoseIssues();
    public void PrintConfiguration();
    public void TestResponsiveness();
}