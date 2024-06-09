#region using

using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

#endregion

namespace CS2DontBlindMe.BrightnessChangers;

// Adapted from https://stackoverflow.com/a/61417175
[SupportedOSPlatform("windows")]
public class WindowsLaptopBrightnessChanger : IBrightnessChanger
{
    private readonly int nominalBrightness;
    private readonly ManagementObject monitorBrightnessMethods = null!;
    private readonly ILogger logger;

    public WindowsLaptopBrightnessChanger(ILogger logger)
    {
        this.logger = logger;
        nominalBrightness = GetInitialBrightness();
        if (nominalBrightness > 0)
        {
            var monitor = GetMonitor();
            if (monitor is { } mon)
            {
                monitorBrightnessMethods = mon;
            }
        }

        logger.LogInformation("Initial brightness recorded as: {brightness}", nominalBrightness);
    }

    private int GetInitialBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi",
                "select CurrentBrightness from WmiMonitorBrightness");
            using var instances = searcher.Get();
            foreach (var instance in searcher.Get())
            {
                return (byte)instance.GetPropertyValue("CurrentBrightness");
            }
        }
        catch (Exception e)
        {
            logger.LogCritical("Could not get current brightness setting: {message}", e.Message);
        }

        return 0;
    }

    private ManagementObject? GetMonitor()
    {
        try
        {
            using var mclass = new ManagementClass("WmiMonitorBrightnessMethods")
            {
                Scope = new ManagementScope(@"\\.\root\wmi")
            };
            using var instances = mclass.GetInstances();
            foreach (ManagementObject instance in instances)
            {
                return instance;
            }
        }
        catch (Exception e)
        {
            logger.LogCritical("Could not get monitor: {message}", e.Message);
        }

        return null;
    }

    private void Set(int brightness)
    {
        var args = new object[] { 1, brightness };
        monitorBrightnessMethods.InvokeMethod("WmiSetBrightness", args);
    }

    public bool UpdateBrightness(float percentage)
    {
        Set((int)(nominalBrightness * percentage));
        return true;
    }

    public bool CanWork()
    {
        return nominalBrightness > 0;
    }

    public void Dispose()
    {
        monitorBrightnessMethods.Dispose();
        GC.SuppressFinalize(this);
    }
}