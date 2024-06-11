#region using

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

#endregion

namespace CS2DontBlindMe.BrightnessChangers;

// Adapted from https://stackoverflow.com/a/61417175
[SupportedOSPlatform("windows")]
public class WindowsExternalMonitorBrightnessChanger : IBrightnessChanger
{
    private readonly ILogger logger;

    #region DllImport

    [DllImport("dxva2.dll", EntryPoint = "GetNumberOfPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PhysicalMonitor[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", EntryPoint = "GetMonitorBrightness")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(IntPtr handle, ref uint minimumBrightness, ref uint currentBrightness, ref uint maxBrightness);

    [DllImport("dxva2.dll", EntryPoint = "SetMonitorBrightness")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetMonitorBrightness(IntPtr handle, uint newBrightness);

    [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitor")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitors")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PhysicalMonitor[] pPhysicalMonitorArray);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

    private delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("dxva2.dll", EntryPoint = "GetMonitorCapabilities")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorCapabilities(IntPtr handle, ref uint pdwMonitorCapabilities, ref uint pdwSupportedColorTemperatures);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct GammaRamp
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Red;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Green;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public ushort[] Blue;
    }

    [DllImport("gdi32.dll")]
    private static extern bool GetDeviceGammaRamp(IntPtr handle, ref GammaRamp lpRamp);

    [DllImport("gdi32.dll")]
    private static extern bool SetDeviceGammaRamp(IntPtr handle, ref GammaRamp lpRamp);

    #endregion

    private IReadOnlyCollection<MonitorInfo> Monitors { get; set; } = new List<MonitorInfo>();

    public WindowsExternalMonitorBrightnessChanger(ILogger logger)
    {
        this.logger = logger;
        UpdateMonitors();
    }

    public bool CanWork()
    {
        return Monitors.Count > 0;
    }

    public bool UpdateBrightness(float percentage)
    {
        var success = true;

        foreach (var monitor in Monitors)
        {
            if (!SetBrightness(monitor, percentage))
            {
                logger.LogError("Could not set brightness of monitor: {name}", monitor.Description);
                success = false;
            }
        }

        return success;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool SetBrightness(MonitorInfo monitor, float brightness)
    {
        if (monitor.InitialGamma is { } initialGamma)
        {
            var currentGama = monitor.CurrentGama!.Value;

            for (ushort i = 0; i < 256; i++)
            {
                currentGama.Red[i] = (ushort)(i * (ushort)(initialGamma.Red[i] * brightness));
                currentGama.Green[i] = (ushort)(i * (ushort)(initialGamma.Green[i] * brightness));
                currentGama.Blue[i] = (ushort)(i * (ushort)(initialGamma.Blue[i] * brightness));
            }

            return SetDeviceGammaRamp(monitor.Handle, ref currentGama);
        }

        return SetMonitorBrightness(monitor.Handle, (uint)(monitor.InitialValue * brightness));
    }

    private void UpdateMonitors()
    {
        DisposeMonitors(Monitors);

        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
        {
            uint physicalMonitorsCount = 0;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalMonitorsCount))
            {
                logger.LogCritical("Cannot get monitor count");
                return true;
            }

            var physicalMonitors = new PhysicalMonitor[physicalMonitorsCount];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorsCount, physicalMonitors))
            {
                logger.LogCritical("Cannot get monitor handle");
                return true;
            }

            foreach (var physicalMonitor in physicalMonitors)
            {
                uint monitorCapabilities = 0;
                uint colourTemperatures = 0;
                uint minValue = 0, currentValue = 0, maxValue = 0;
                if (
                    GetMonitorCapabilities(physicalMonitor.hPhysicalMonitor, ref monitorCapabilities, ref colourTemperatures)
                    && GetMonitorBrightness(physicalMonitor.hPhysicalMonitor, ref minValue, ref currentValue, ref maxValue)
                )
                {
                    logger.LogInformation("Using DDC/CI for monitor, brightness(initial/min/max): {name}, {initial}/{min}/{max}",
                        physicalMonitor.szPhysicalMonitorDescription, currentValue, minValue, maxValue);
                    var info = new MonitorInfo
                    {
                        Description = physicalMonitor.szPhysicalMonitorDescription,
                        Handle = physicalMonitor.hPhysicalMonitor,
                        MinValue = minValue,
                        MaxValue = maxValue,
                        InitialValue = currentValue
                    };
                    monitors.Add(info);
                    continue;
                }

                logger.LogDebug("DDC/CI not supported, trying gamma ramp, for monitor: {name}", physicalMonitor.szPhysicalMonitorDescription);
                var gammaRamp = new GammaRamp();
                if (GetDeviceGammaRamp(physicalMonitor.hPhysicalMonitor, ref gammaRamp))
                {
                    logger.LogInformation("Using gamma ramp for monitor: {name}", physicalMonitor.szPhysicalMonitorDescription);
                    var info = new MonitorInfo
                    {
                        Description = physicalMonitor.szPhysicalMonitorDescription,
                        Handle = physicalMonitor.hPhysicalMonitor,
                        InitialGamma = gammaRamp
                    };
                    monitors.Add(info);
                    continue;
                }

                logger.LogError("DDC/CI and gamma ramp not supported, ignoring monitor: {name}", physicalMonitor.szPhysicalMonitorDescription);
                DestroyPhysicalMonitor(physicalMonitor.hPhysicalMonitor);
            }

            return true;
        }, IntPtr.Zero);

        Monitors = monitors;
    }

    public void Dispose()
    {
        DisposeMonitors(Monitors);
        GC.SuppressFinalize(this);
    }

    private static void DisposeMonitors(IEnumerable<MonitorInfo> monitors)
    {
        var monitorArray = monitors.Select(m => new PhysicalMonitor { hPhysicalMonitor = m.Handle }).ToArray();

        if (monitorArray.Length > 0)
        {
            DestroyPhysicalMonitors((uint)monitorArray.Length, monitorArray);
        }
    }

    #region Classes

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PhysicalMonitor
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    private class MonitorInfo
    {
        public string Description { get; set; }
        public uint MinValue { get; set; }
        public uint MaxValue { get; set; }
        public IntPtr Handle { get; set; }
        public uint InitialValue { get; set; }
        public GammaRamp? InitialGamma { get; set; }
        public GammaRamp? CurrentGama { get; }
    }

    #endregion
}