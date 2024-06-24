# TerrariaKitchen

TShock plugin that enables twitch integration to allow chatters to spend credits to buy mobs.

## Installation

⚠ Firstly, this plugin is not vetted by official TShock staff, please read their [plugin safety guidelines](https://tshock.readme.io/docs/plugin-safety) ⚠

You will need [tshock](https://tshock.readme.io/docs/getting-started), and a way to compile this mod. You will need Visual Studio, and copy `TerrariaServer.dll`, `TShockAPI.dll`, and `OTAPI.dll` from the tshock installation to the root of this project directory.

Then, after downloading all dependencies using NuGet, which are:
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [TwitchLib](https://github.com/TwitchLib/TwitchLib)

you can then build the plugin locally. In order to make sure all dependencies are present, you will need to create a publishing profile by using Build -> Publish, and select the "Folder" option, after publishing copy the following files in the folder to `ServerPlugins` folder of TShock:
- `Microsoft.Extensions.Logging.Abstractions.dll`
- `TerrariaKitchen.dll`
- `TwitchLib.Client.dll`
- `TwitchLib.Client.Enums.dll`
- `TwitchLib.Client.Models.dll`
- `TwitchLib.Communication.dll`

Afterwards you will need to configure your twitch api, you can either do it through console command `/setkitchenkey <api key>` or put the key in `kitchenapi.txt` inside the `tshock` subfolder within Tshock installation. And also configure your settings by adding a `kitchen.json` in the same folder with the format in the [Configuration section](#Configuration).

Once it's all done, load into a world and you can use `/startkitchen <twitch channle name> <player name>` and twitch integration will start accepting orders from chatters.

## Additional Console Commands

### /resetkitchen
Reset the remaining balance of all chatters for the current world.

### /kitchengive \<username> \<amount>
Give twitch chat user with \<username> as their display name \<amount> of chat credits for the current world. Number can be negative to deduct credits.

### /kitchenmenuupdate
Refresh most settings by re-reading from `kitchen.json` without restarting the server. Note that ports changes will not be updated and existing pools and waves will also not be affected.

## Twitch Chat commands
Twitch chat commands are throttled so only 1 command will be processed per unique user every 5 seconds.

### !t balance
Shows the current remaining balance of the chatter

### !t buy \<item> \<amount>
Buy \<amount> of \<item> spawns, item name is defined in configuration and is case sensitive. 

### !t pool \<index> \<amount>
Add \<amount> of credits to a pool with index of \<index>, if \<amount> would bring the over the pool cap, it will automatically be adjusted to match. 

### !t event \<event name> \<amount>
Add \<amount> of credits to a pool with an event associated with \<event name>, if \<amount> would go over the event target, it will automatically be adjusted to match. Event name is defined in configuration and is case sensitive. 
If no event is found, a new pool will start automatically.

### !t wave start \<target number>
Start a wave that will spawn all mobs when the number of mobs in it reaches or go above \<target number>. 

### !t wave buy \<wave name> \<item> \<amount>
Buy \<amount> of \<item> spawns and add them to a wave started by \<wave name>. \<wave name> is the username of the chatter whose wave you will need to add into. This will not automatically start a new wave if such wave is not found. 

### !t wave cancel
Cancel a wave started by you and refund all credits spent on it to everyone.

### !t give \<target> \<amount>
Give \<amount> of credits to \<target>. \<target> is the username of the recipient chatter.

### !t say \<message>
NPCs will be raffled as a random chatter when they spawn. If a chatter's username matches an NPC, they can use this command to make the NPC say \<message> in game!

## Configuration
Configuration is done by `kitchen.json`

The following is an example of the file
```
{
    "Entries": [
        {
            "MobName": "slime",
            "MobAlias": ["goo"],
            "InternalName": 1,
            "Price": 100,
            "MaxBuys": 10
        },
        {
            "MobName": "eyeofcthulu",
            "InternalName": 4,
            "Price": 1000,
            "Pooling": true
        }
    ],
    "Events": [
        {
            "EventName": "goblins",
            "EventId": 6,
            "EventAlias": ["goblin", "goblininvasion"],
            "Price": 1500
        }
    ],
    "StartingMoney": 500,
    "Income": 10,
    "IncomeInterval": 60,
    "SubscriberPriceMultiplier": 0.9,
    "ModAbuse": false,
    "XRange": 200,
    "YRange": 50,
    "OverlayPort": 7770,
    "OverlayWsPort": 7771
}
```
With each purchasable mob as a separate entry. 
- `Entries` is a list of mobs that can be bought by chatters:
    - `InternalName` is the ID of the NPC found in [this list](https://tshock.readme.io/docs/npc-list)
    - `MobName` is the name that will be entered into the `!t buy` command, case sensitive
    - `MobAlias` is a list of other similar names that will also be treated as ordering for this entry, also case sensitive
    - `Price` is the credits cost per item
    - `MaxBuys` is the maximum number of purchases that can be made in a single order
    - `Pooling` is whether the mob allows pooling credits from multiple chatters
- `Events` is a list of events that can be bought by chatters:
    - `EventName` is the name that will be entered into the `!t event` command, case sensitive
    - `EventAlias` is a list of other similar names that will also be treated as ordering for this entry, also case sensitive
    - `Price` is the credits cost to reach to start the event
    - `EventId` with a number of the following which will correspond to each event:
        - `0`: Full Moon
        - `1`: Blood Moon
        - `2`: Solar Eclipse
        - `3`: Sandstorm
        - `4`: Rain
        - `5`: Slime Rain
        - `6`: Goblin Army
        - `7`: Frost Legion
        - `8`: Pirate Invasion
        - `9`: Martian Madness
        - `10`: Pumpkin Moon
        - `11`: Frost Moon
- `StartingMoney` is the credits new chatters will start with
- `Income` is the amount of credits each chatters still in the channel chat room will receive periodically
- `IncomeInterval` is the number of seconds between each income payout, minimum is 5 seconds.
- `SubscriberPriceMultiplier` is the price multiplier for channel subscribers
- `ModAbuse` being set to true will allow mods to buy any mobs among the entries for free
- `XRange` is the number of tiles horizontally around the player the mobs can spawn
- `YRange` is the number of tiles vertically around the player the mobs can spawn
- `OverlayPort` is the port number for the streaming overlay and menu page (Only change if you want to use this port for something else)
- `OverlayWsPort` is the port number for the websocket port used by the streaming overlay (Only change if you want to use this port for something else)

## Overlay
You can view the overlay by entering `http://localhost:7770`, `7770` may be replaced by whatever port you set for `OverlayPort`, this may be added as a browser source in your streaming software to notify chat of current pools and waves by using green chroma key.
If you visit `http://localhost:7770/menu`, a formatted menu of mob and event entries will be rendered.