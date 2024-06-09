#region using

using System.Reflection;
using CS2DontBlindMe.SubSettings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

#endregion

namespace CS2DontBlindMe;

public class Settings
{
    public static string SettingsPath = Path.Combine(System.AppContext.BaseDirectory, "settings.json");

    public int MinimumThresholdForFlashAmount { get; set; } = 0;
    public LaptopBrightnessChanger LaptopBrightnessChanger { get; set; } = new();
    public MonitorBrightnessChanger MonitorBrightnessChanger { get; set; } = new();
    public int Port { get; set; } = 3456;
    public int MinimumBrightness { get; set; } = 10;

    /// <summary>
    ///     1 = Debug
    ///     2 = Information
    ///     3 = Warning
    /// </summary>
    public int LogLevel { get; set; } = 2;

    public void PrintInformation(ILogger logger)
    {
        logger.LogInformation("Settings path: {path}", SettingsPath);
        logger.LogInformation("Minimum Flash Amount: {amount}/255", MinimumThresholdForFlashAmount);
        logger.LogInformation("Minimum Monitor Brightness: {amount}/100", MinimumBrightness);
        logger.LogInformation("Log Level: {level}({name}) (possible values: 0 (highest) - 6 (lowest))", LogLevel, (LogLevel)LogLevel);
        logger.LogInformation("GSI Listener Port: {port}", Port);
    }

    public static Settings LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(new Settings()));
        }

        var settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(SettingsPath));

        if (settings is null)
        {
            throw new InvalidDataException($"Invalid settings file encountered in {SettingsPath}");
        }

        return settings;
    }
}