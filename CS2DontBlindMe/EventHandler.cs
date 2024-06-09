#region using

using CounterStrike2GSI;
using CounterStrike2GSI.EventMessages;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

#endregion

namespace CS2DontBlindMe;

public class EventHandler : IDisposable
{
    private string ProviderId = "";

    private int _currentFlashAmount;

    private int CurrentFlashAmount
    {
        get => _currentFlashAmount;
        set
        {
            _currentFlashAmount = value;
            UpdateBrightness();
        }
    }

    private float CurrentBrightness;

    /// <summary>
    ///     In case someone wants to up the brightness earlier.
    /// </summary>
    private readonly int MinimumFlashAmount;

    /// <summary>
    ///     To counteract some of the "heuristics" of Windows
    /// </summary>
    private readonly float MinBrightness;

    /// <summary>
    ///     Just the max amount that can be flashed. It's a byte internally, probably, so 0-255
    /// </summary>
    private const int MaxFlashAmount = 255;

    private DateTime LastUpdate;

    private Timer UpdateWatcher = null!;

    private readonly IBrightnessChanger BrightnessChanger = null!;
    private readonly ILogger Logger;

    public EventHandler(GameStateListener gsl, int minimumFlashAmount, int minimumBrightness, IBrightnessChanger brightnessChanger, ILogger logger)
    {
        Logger = logger;

        gsl.PlayerFlashAmountChanged += GslOnPlayerFlashAmountChanged;
        gsl.ProviderUpdated += GslOnProviderUpdated;
        gsl.Gameover += GslOnGameover;
        gsl.PlayerDied += GslOnPlayerDied;
        gsl.PlayerDisconnected += GslOnPlayerDisconnected;
        gsl.GameEvent += GslOnGameEvent;

        if (minimumFlashAmount is > 255 or < 0)
        {
            Logger.LogError("Minimum flash amount needs to be between 0 and 255");
            return;
        }

        MinimumFlashAmount = minimumFlashAmount;

        if (minimumBrightness is > 100 or < 0)
        {
            Logger.LogError("Minimum Brightness needs to be between 0 and 100");
            return;
        }

        MinBrightness = minimumBrightness / 100f;

        BrightnessChanger = brightnessChanger;
        Logger.LogInformation("Brightness Changer: {changer}", brightnessChanger.GetType().Name);

        StartUpdateWatcher();
    }

    public IBrightnessChanger GetBrightnessChanger()
    {
        return BrightnessChanger;
    }

    public void Dispose()
    {
        Logger.LogDebug("Resetting EventHandler");
        UpdateWatcher.Stop();
        UpdateWatcher.Dispose();
        BrightnessChanger.UpdateBrightness(1.0f);
        BrightnessChanger.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StartUpdateWatcher()
    {
        Logger.LogDebug("Starting update watcher");

        UpdateWatcher = new Timer(1000);
        UpdateWatcher.Elapsed += (sender, args) =>
        {
            // If current flash amount is more than 0, and the last update is more than two seconds ago, then we stopped receiving updates and should restore brightness
            if (CurrentFlashAmount > 0 && DateTime.Now.AddSeconds(-2) > LastUpdate)
            {
                Logger.LogDebug("Resetting flash amount to zero due to not receiving further events");
                CurrentFlashAmount = 0;
            }
        };
        UpdateWatcher.AutoReset = true;
        UpdateWatcher.Enabled = true;
        UpdateWatcher.Start();
    }

    public void GslOnPlayerFlashAmountChanged(PlayerFlashAmountChanged gameEvent)
    {
        if (gameEvent.Player.SteamID != ProviderId)
        {
            return;
        }

        // Sometimes it sends -1
        CurrentFlashAmount = Math.Max(gameEvent.New, 0);

        Logger.LogDebug("Current flash amount: {amount}", CurrentFlashAmount);
    }

    public void GslOnProviderUpdated(ProviderUpdated gameEvent)
    {
        ProviderId = gameEvent.New.SteamID;
        CurrentFlashAmount = 0;

        Logger.LogDebug("Got new provider ID: {id}", ProviderId);
    }

    public void GslOnGameover(Gameover gameEvent)
    {
        CurrentFlashAmount = 0;

        Logger.LogDebug("Got Game Over");
    }

    public void GslOnPlayerDied(PlayerDied gameEvent)
    {
        if (gameEvent.Player.SteamID == ProviderId)
        {
            CurrentFlashAmount = 0;

            Logger.LogDebug("Current player died");
        }
    }

    public void GslOnPlayerDisconnected(PlayerDisconnected gameEvent)
    {
        if (gameEvent.Value.SteamID == ProviderId)
        {
            CurrentFlashAmount = 0;

            Logger.LogDebug("Current player disconnected");
        }
    }

    public void GslOnGameEvent(CS2GameEvent gameEvent)
    {
        LastUpdate = DateTime.Now;
    }

    private void UpdateBrightness()
    {
        var wantedBrightness = 1.0f;

        if (CurrentFlashAmount > MinimumFlashAmount)
        {
            // "Negate" the flash amount to get the brightness since it's inversely proportional
            var flashBrightness = MaxFlashAmount - CurrentFlashAmount;
            // Scale flash brightness to min/max values for flashes (since a low value may already mean you're barely flashed anymore)
            var scaledBrightness = Map(0, MaxFlashAmount, MinimumFlashAmount, MaxFlashAmount, flashBrightness);
            // Convert to percentage
            var percentageBrightness = scaledBrightness / (float)MaxFlashAmount;
            wantedBrightness = MathF.Max(percentageBrightness, MinBrightness);
        }

        if (Math.Abs(CurrentBrightness - wantedBrightness) > 0.001f)
        {
            Logger.LogInformation("Changing brightness: {brightness}", wantedBrightness);

            if (BrightnessChanger.UpdateBrightness(wantedBrightness))
            {
                CurrentBrightness = wantedBrightness;
            }
            else
            {
                Logger.LogError("Could not change brightness");
            }
        }
    }

    private static int Map(int a1, int a2, int b1, int b2, int s)
    {
        return b1 + (s - a1) * (b2 - b1) / (a2 - a1);
    }
}