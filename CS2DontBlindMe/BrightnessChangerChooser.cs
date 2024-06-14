#region using

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using CS2DontBlindMe.BrightnessChangers;
using CS2DontBlindMe.SubSettings;
using Microsoft.Extensions.Logging;

#endregion

namespace CS2DontBlindMe;

public static class BrightnessChangerChooser
{
    [SupportedOSPlatform("windows")]
    public static IBrightnessChanger? GetBrightnessChanger(ILoggerFactory factory, LaptopBrightnessChanger laptop, MonitorBrightnessChanger monitor)
    {
        if (TryBrightnessChanger<WindowsExternalMonitorBrightnessChanger>(factory, out var monitorBrightnessChanger))
        {
            return monitorBrightnessChanger;
        }


        if (TryBrightnessChanger<WindowsLaptopBrightnessChanger>(factory, out var laptopBrightnessChanger))
        {
            return laptopBrightnessChanger;
        }

        monitorBrightnessChanger.DiagnoseIssues();
        laptopBrightnessChanger.DiagnoseIssues();

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool TryBrightnessChanger<TBrightnessChanger>(ILoggerFactory factory, out TBrightnessChanger brightnessChanger)
        where TBrightnessChanger : IBrightnessChanger
    {
        brightnessChanger = Activator.CreateInstance<TBrightnessChanger>();
        brightnessChanger.SetLogger(factory.CreateLogger(typeof(TBrightnessChanger).Name));

        return brightnessChanger.TryInitialize();
    }
}