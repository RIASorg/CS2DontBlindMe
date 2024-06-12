# CS2 Please Don't Blind Me

Inspired by the old [CSGO Don't Blind Me](https://github.com/dev7355608/csgo_dont_blind_me)

Protect your eyes with this one nifty trick :)

## Anti-Cheat

It's just using ready-made CS2/CSGO Game State Integration API. This API is also used by tournament organizers or third-party clients.

- VAC: SAFE (tested)
- FaceIt: SAFE and should be compatible (any confirmation appreciated)
- EAC (from other game): Doesn't complain about it either

## Installation

- Download a release from the [Release](https://github.com/RIASorg/CS2DontBlindMe/releases) page.
- Run it.

## Usage

Make sure you only have one monitor connected, or otherwise make sure both monitors support the same API (listed below). 
Otherwise this app may choose the wrong API and thus dim the wrong monitor.
If you have multiple monitors connected that all use the same API, the app will dim all monitors.

When you run the app and get flashed in game, the app will automatically dim your monitors to not destroy your eyes in real-life. The result is usually a grey-like effect. 
The brightness is relative to your chosen brightness, so when you normally have your monitor at 70% brightness, when it sets brightness to "1" it will set it to 70%.

````bash
info: DontBlindMe[0] Settings path: C:\Users\XXX\Downloads\CS2DontBlindMe\settings.json
info: DontBlindMe[0] Minimum Flash Amount: 0/255                                          
info: DontBlindMe[0] Minimum Monitor Brightness: 10/100                                   
info: DontBlindMe[0] Log Level: 2(Information) (possible values: 0 (highest) - 6 (lowest))
info: DontBlindMe[0] GSI Listener Port: 3456                                              
info: DontBlindMe[0] Using DDC/CI for monitor, brightness(initial/min/max): Dell P2422H(DisplayPort), 75/0/100
info: DontBlindMe[0] Brightness Changer: WindowsExternalMonitorBrightnessChanger
info: DontBlindMe[0] Integration config file: T:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg\gamestate_integration_CS2DontBlindMe.cfg
info: DontBlindMe[0] Listening for GSI events. Press any key to quit...                                                                                              
info: DontBlindMe[0] Changing brightness: 1
info: DontBlindMe[0] Changing brightness: 0.1
info: DontBlindMe[0] Changing brightness: 0.21176471
info: DontBlindMe[0] Changing brightness: 0.33333334
info: DontBlindMe[0] Changing brightness: 0.47058824
info: DontBlindMe[0] Changing brightness: 0.57254905
info: DontBlindMe[0] Changing brightness: 0.6666667
info: DontBlindMe[0] Changing brightness: 0.7647059
info: DontBlindMe[0] Changing brightness: 0.8392157
info: DontBlindMe[0] Changing brightness: 0.89411765
info: DontBlindMe[0] Changing brightness: 0.9372549
info: DontBlindMe[0] Changing brightness: 0.96862745
info: DontBlindMe[0] Changing brightness: 0.9882353
info: DontBlindMe[0] Changing brightness: 1

````

Here's a deep-fried GIF demo (upload restrictions by Github), but there's also a proper [video](doc/demo.mp4):
![UnbenanntesProjekt-ezgif com-cut](https://github.com/RIASorg/CS2DontBlindMe/assets/9307432/5837f577-8c9e-4541-8ac1-b4144ee1f6ca)


## How it controls brightness

Since hardware manufacturers have a hard-on for making software developers lives' much much harder, there are currently 3(!) APIs implemented in this app, which can(!) control your brightness.

These are:
- DDC: This is using the internal bus from the GPU to the Monitor to change the brightness. Sometimes, your monitor may refuse to register as a DDC-compatible device. Usually this can be fixed by simply turning the monitor on/off again (and restarting this app).
- WMI: For monitors that are integrated (e.g. laptops), the manufacturer may have registered a few settings in the Windows-Management-Interface. This can be used to set the brightness, but will never(!) be available if you are running an external monitor or TV.
- Gamma Ramp: This one is the OG method that csgo_dont_blind_me used. It basically changes the way gamma works and thus interfers with other applications that make use of this (e.g. f.lux). If this app reports using this, then usually you should try the fix for DDC, since most monitors do support DDC and it's better than gamma ramp. Also, Windows may limit the minimum brightness to a higher threshold, so you should test if this works, and otherwise up the minimum brightness setting (described below).

## Settings

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

## Building it yourself

Check out this repo, then either build it with dotnet, or use the [dotnet-releaser](https://github.com/xoofx/dotnet-releaser) tool I use.
````powershell
dotnet tool install --global dotnet-releaser
dotnet-releaser build --force dotnet-releaser.toml
````
The resulting zip will be in the subfolder `artifacts-dotnet-releaser`.

## Contributing

I'd welcome any PRs or suggestions. I'm also open to expanding this to more than just "anti-flash", but I don't have any big ideas right now (besides a bomb timer overlay or something).
There's two issues already open about LGTV API support and Linux support. If anyone's particularly interested in either a PR would be welcomed.

