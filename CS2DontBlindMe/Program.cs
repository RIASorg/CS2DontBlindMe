#region using

using CounterStrike2GSI;
using CounterStrike2GSI.Utils;
using CS2DontBlindMe;
using Lunet.Extensions.Logging.SpectreConsole;
using Microsoft.Extensions.Logging;
using NReco.Logging.File;
using EventHandler = CS2DontBlindMe.EventHandler;

#endregion

try
{
    var settings = Settings.LoadSettings();
    var logger = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel((LogLevel)settings.LogLevel);
        builder.AddSpectreConsole(new SpectreConsoleLoggerOptions
        {
            IncludeCategory = false,
            IncludeEventId = false,
            IncludeNewLineBeforeMessage = false
        });
        builder.AddFile("app.log", false);
    }).CreateLogger("DontBlindMe");
    settings.PrintInformation(logger);

    var brightnessChanger = BrightnessChangerChooser.GetBrightnessChanger(logger, settings.LaptopBrightnessChanger, settings.MonitorBrightnessChanger);
    if (brightnessChanger is null)
    {
        logger.LogCritical("Brightness changer does not work with your hardware configuration");
        return ErrorExit(5);
    }

    var gsl = new GameStateListener(settings.Port);
    using var eventHandler = new EventHandler(gsl, settings.MinimumThresholdForFlashAmount, settings.MinimumBrightness, brightnessChanger, logger);

    // TODO: Linux path
    const string gsiName = "CS2DontBlindMe";
    var configPath = SteamUtils.GetGamePath(730) + @"\game\csgo\cfg\" + "gamestate_integration_" + gsiName + ".cfg";
    if (!File.Exists(configPath))
    {
        if (!gsl.GenerateGSIConfigFile(gsiName))
        {
            logger.LogCritical("Could not generate integration config file. Tried path: {path}", configPath);
            return ErrorExit(1);
        }
    }

    logger.LogInformation("Integration config file: {path}", configPath);

    if (!gsl.Start())
    {
        logger.LogCritical("Could not start GSI server on port: {port}", settings.Port);
        return ErrorExit(2);
    }

    logger.LogInformation("Listening for GSI events. Press any key to quit...");
    Console.ReadKey();
    gsl.Stop();
    logger.LogInformation("Detected key press. Quitting..");
    return 0;
}
catch (Exception e)
{
    Console.WriteLine("Caught exception {0}", e.Message);
    return ErrorExit(-1);
}

int ErrorExit(int exitCode)
{
    Console.WriteLine(
        "Waiting for key press after encountering error, copy any relevant lines from the console above or from this file: {0}",
        Path.Combine(AppContext.BaseDirectory, "app.log")
    );
    Console.ReadKey();
    return exitCode;
}