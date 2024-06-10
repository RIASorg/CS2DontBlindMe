# CS2 Please Don't Blind Me

Protect your eyes with this one nifty trick :)

## Installation

- Download a release from the [Release](https://github.com/RIASorg/CS2DontBlindMe/releases) page.
- Run it.

## Usage

Make sure you only have one monitor connected, or otherwise make sure both monitors support the same API (listed below). 
Otherwise this app may choose the wrong API and thus dim the wrong monitor.
If you have multiple monitors connected that all use the same API, the app will dim all monitors.

When you run the app and get flashed in game, the app will automatically dim your monitors to not destroy your eyes in real-life. The result is usually a grey-like effect.

Since hardware manufacturers have a hard-on for making software developers lives' much much harder, there are currently 3(!) APIs implemented in this app, which can(!) control your brightness.

These are:
- DDC: This is using the internal bus from the GPU to the Monitor to change the brightness. Sometimes, your monitor may refuse to register as a DDC-compatible device. Usually this can be fixed by simply turning the monitor on/off again (and restarting this app).
- WMI: For monitors that are integrated (e.g. laptops), the manufacturer may have registered a few settings in the Windows-Management-Interface. This can be used to set the brightness, but will never(!) be available if you are running an external monitor or TV.
- Gamma Ramp: This one is the OG method that csgo_dont_blind_me used. It basically changes the way gamma works and thus interfers with other applications that make use of this (e.g. f.lux). If this app reports using this, then usually you should try the fix for DDC, since most monitors do support DDC and it's better than gamma ramp. Also, Windows may limit the minimum brightness to a higher threshold, so you should test if this works, and otherwise up the minimum brightness setting (described below).

The app will create two configuration files:
- A GSI (Game State Integration) config file in the CS2 game directory, which will tell CS2 where it can find the app
- A settings.json file in the app directory

You should never have to look at the GSI config file, since everything is automatically configured for you.

The settings.json should also work for you, but you can change some settings to optimize it or in case you run into issues, namely:
- port: That's the port the GSI listener is running on. By default that is 3456, which should be open, but may be occupied by another application. You can change it to whatever.
- minimumBrightness: Controls the minimum brightness your monitor will get dimmed to, and is 10% by default. Some monitors may require this to be upped, otherwise they may reject the setting.
- minimumThresholdForFlashAmount: Controls the threshold at which the minimum brightness for your monitor is set, and is 0 by default. You can change it in case you want to up the brightness earlier. In my tests it's sort of 50/50 what I prefer, but I wouldn't go higher than 80 in this setting. (Max 255)
- laptopBrightnessChanger/monitorBrightnessChanger: You can disable the one that isn't supported by your hardware. It automatically detects this anyways, so it wouldn't actually change anything, but maybe make the startup marginally faster.

## How it works internally

The app uses [C# GSI](https://github.com/antonpup/CounterStrike2GSI) to register as a GSI listener in CS2. CS2 will then connect to this app and send it game events, such as player killed, player flashed and so on.

The app uses these game events (in particular, player killed, provider changed, game over, disconnect, flashed) to determine when and how much it should dim your monitor.

The API to use to dim your monitor is automatically detected by querying Windows for information, and trying out the APIs to see if the monitor responds.

It then queries the API for the current brightness level, and then adjusts that down when the player is flashed. It will *never* turn the brightness up higher than the brightness set by the user.

