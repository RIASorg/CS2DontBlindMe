#region using

using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

#endregion

namespace CS2DontBlindMe.BrightnessChangers;

// Adapted from https://stackoverflow.com/a/61417175
[SupportedOSPlatform("windows")]
public class WindowsLaptopBrightnessChanger : IBrightnessChanger
{
    private int nominalBrightness;
    private ManagementObject monitorBrightnessMethods = null!;
    private ILogger logger = null!;

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

    public void SetLogger(ILogger logger)
    {
        this.logger = logger;
    }

    public bool TryInitialize()
    {
        nominalBrightness = GetInitialBrightness();
        var monitor = GetMonitor();
        if (monitor is { } mon)
        {
            monitorBrightnessMethods = mon;
        }

        return nominalBrightness > 0 && monitor is not null && UpdateBrightness(1.0f);
    }

    public void TestResponsiveness()
    {
        var sw = Stopwatch.StartNew();
        UpdateBrightness(0.0f);
        UpdateBrightness(1.0f);
        sw.Stop();
        logger.LogInformation("Changing brightness of laptop took {time}ms", sw.ElapsedMilliseconds);
    }

    public void PrintConfiguration()
    {
        logger.LogInformation("Initial brightness recorded as: {brightness}", nominalBrightness);
    }

    public void DiagnoseIssues()
    {
        logger.LogDebug("This brightness changer only works with Laptops. Check if your manufacturer supports controlling the laptop screen via software.");
    }

    public void Dispose()
    {
        monitorBrightnessMethods.Dispose();
        GC.SuppressFinalize(this);
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
            logger.LogDebug("Could not get current brightness setting: {message}", e.Message);
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
            logger.LogDebug("Could not get monitor: {message}", e.Message);
        }

        return null;
    }
}