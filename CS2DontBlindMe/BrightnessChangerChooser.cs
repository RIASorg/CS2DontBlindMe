#region using

using System.Runtime.InteropServices;
using CS2DontBlindMe.BrightnessChangers;
using CS2DontBlindMe.SubSettings;
using Microsoft.Extensions.Logging;

#endregion

namespace CS2DontBlindMe;

public static class BrightnessChangerChooser
{
    public static IBrightnessChanger? GetBrightnessChanger(ILogger logger, LaptopBrightnessChanger laptop, MonitorBrightnessChanger monitor)
    {
        IBrightnessChanger? brightnessChanger = null;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (monitor.Enabled && brightnessChanger == null)
            {
                brightnessChanger = new WindowsExternalMonitorBrightnessChanger(logger);
                if (!TryBrightnessChanger(brightnessChanger, logger))
                {
                    brightnessChanger = null;
                }
            }

            if (laptop.Enabled && brightnessChanger == null)
            {
                brightnessChanger = new WindowsLaptopBrightnessChanger(logger);
                if (!TryBrightnessChanger(brightnessChanger, logger))
                {
                    brightnessChanger = null;
                }
            }

            return brightnessChanger;
        }

        logger.LogCritical("Unfortunately changing brightness on anything but Windows is currently not supported");
        return null;
    }

    private static bool TryBrightnessChanger(IBrightnessChanger changer, ILogger log)
    {
        if (!changer.CanWork())
        {
            log.LogError("Brightness changer does not work with your hardware configuration: {changer}", changer.GetType().Name);
            return false;
        }

        return true;
    }
}