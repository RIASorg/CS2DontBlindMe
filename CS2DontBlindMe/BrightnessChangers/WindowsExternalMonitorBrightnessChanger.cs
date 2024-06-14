#region using

using System.Diagnostics;
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
    private ILogger logger;

    #region DllImport

    [DllImport("kernel32.dll", EntryPoint = "GetLastError")]
    private static extern int GetLastError();

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
        public string Description { get; init; }
        public uint MinValue { get; init; }
        public uint MaxValue { get; init; }
        public IntPtr Handle { get; init; }
        public uint InitialValue { get; init; }
        public GammaRamp? InitialGamma { get; init; }
        public GammaRamp? CurrentGamma { get; init; }
    }

    #endregion

    private IReadOnlyCollection<MonitorInfo> Monitors { get; set; } = new List<MonitorInfo>();

    public void SetLogger(ILogger logger)
    {
        this.logger = logger;
    }

    public bool TryInitialize()
    {
        UpdateMonitors();
        return Monitors.Count > 0;
    }

    public void TestResponsiveness()
    {
        var sw = Stopwatch.StartNew();
        UpdateBrightness(0.0f);
        UpdateBrightness(1.0f);
        sw.Stop();
        logger.LogInformation("Changing brightness of monitor{s} took {time}ms", Monitors.Count > 1 ? "s" : "", sw.ElapsedMilliseconds);
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

    public void DiagnoseIssues()
    {
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
        {
            uint physicalMonitorsCount = 0;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalMonitorsCount))
            {
                logger.LogError("Cannot get monitor count, error code: {code}", GetLastError());
                logger.LogError("Do you have multiple monitors connected? Try with only one of them");
                return true;
            }

            var physicalMonitors = new PhysicalMonitor[physicalMonitorsCount];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorsCount, physicalMonitors))
            {
                logger.LogError("Cannot get physical monitors from HMONITOR, error code: {code}", GetLastError());
                logger.LogError("Do you have multiple monitors connected? Try with only one of them");
                return true;
            }

            foreach (var physicalMonitor in physicalMonitors)
            {
                uint monitorCapabilities = 0;
                uint colourTemperatures = 0;
                uint minValue = 0, currentValue = 0, maxValue = 0;
                var gammaRamp = new GammaRamp();
                if (
                    (
                        GetMonitorCapabilities(physicalMonitor.hPhysicalMonitor, ref monitorCapabilities, ref colourTemperatures)
                        && GetMonitorBrightness(physicalMonitor.hPhysicalMonitor, ref minValue, ref currentValue, ref maxValue)
                    )
                    || GetDeviceGammaRamp(physicalMonitor.hPhysicalMonitor, ref gammaRamp)
                )
                {
                    continue;
                }

                logger.LogError(
                    "DDC not supported by the monitor, error code: {name}, {code}",
                    physicalMonitor.szPhysicalMonitorDescription, GetLastError()
                );
                logger.LogError("Gamma ramp not supported by the monitor: {name}", physicalMonitor.szPhysicalMonitorDescription);


                if (physicalMonitor.szPhysicalMonitorDescription.StartsWith("Generic"))
                {
                    logger.LogError(
                        "The monitor {name} may be a TV, in which case the TV manufacturer does not support programmatically setting brightness. If it's a monitor, it may have failed initializing with Windows. Try turning the monitor off and on again while Windows is running to see if the issue persists.",
                        physicalMonitor.szPhysicalMonitorDescription
                    );
                }
            }

            DestroyPhysicalMonitors(physicalMonitorsCount, physicalMonitors);
            return true;
        }, IntPtr.Zero);
    }

    public void PrintConfiguration()
    {
        foreach (var monitor in Monitors)
        {
            if (monitor.InitialGamma is not null)
            {
                logger.LogInformation("Using gamma ramp for monitor: {name}", monitor.Description);
            }
            else
            {
                logger.LogInformation(
                    "Using DDC for monitor, values(current, min, max): {name}, ({current}, {min}, {max})",
                    monitor.Description, monitor.InitialValue, monitor.MinValue, monitor.MaxValue
                );
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool SetBrightness(MonitorInfo monitor, float brightness)
    {
        if (monitor.InitialGamma is { } initialGamma)
        {
            var currentGama = monitor.CurrentGamma!.Value;

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
                logger.LogDebug("Cannot get monitor count, error code: {code}", GetLastError());
                return true;
            }

            var physicalMonitors = new PhysicalMonitor[physicalMonitorsCount];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorsCount, physicalMonitors))
            {
                logger.LogDebug("Cannot get physical monitors from HMONITOR, error code: {code}", GetLastError());
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
                    logger.LogDebug("Using DDC for monitor, brightness(initial/min/max): {name}, {initial}/{min}/{max}",
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

                logger.LogDebug("DDC not supported, trying gamma ramp, for monitor, error code: {name}, {code}", physicalMonitor.szPhysicalMonitorDescription,
                    GetLastError());

                var gammaRamp = new GammaRamp();
                if (GetDeviceGammaRamp(physicalMonitor.hPhysicalMonitor, ref gammaRamp))
                {
                    logger.LogDebug("Using gamma ramp for monitor: {name}", physicalMonitor.szPhysicalMonitorDescription);
                    var info = new MonitorInfo
                    {
                        Description = physicalMonitor.szPhysicalMonitorDescription,
                        Handle = physicalMonitor.hPhysicalMonitor,
                        InitialGamma = gammaRamp,
                        CurrentGamma = new GammaRamp()
                    };
                    monitors.Add(info);
                    continue;
                }

                logger.LogError("DDC and gamma ramp not supported, ignoring monitor: {name}", physicalMonitor.szPhysicalMonitorDescription);

                DestroyPhysicalMonitor(physicalMonitor.hPhysicalMonitor);
            }

            return true;
        }, IntPtr.Zero);

        Monitors = monitors;
        UpdateBrightness(1.0f);
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
}