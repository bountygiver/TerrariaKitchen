# TerrariaKitchen

TShock plugin that enables twitch integration to allow chatters to spend credits to buy mobs.

## Installation

⚠ Firstly, this plugin is not vetted by official TShock staff, please read their [plugin safety guidelines](https://tshock.readme.io/docs/plugin-safety) ⚠

You will need [tshock](https://tshock.readme.io/docs/getting-started), and a way to compile this mod. You will need Visual Studio, and copy `TerrariaServer.dll`, `TShockAPI.dll`, and `OTAPI.dll` from the tshock installation to the root of this project directory.

Then, after downloading all dependencies using NuGet, which are:
- [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [TwitchLib](https://github.com/TwitchLib/TwitchLib)

you can then build the plugin locally. In order to make sure all dependencies are present, you will need to create a publishing profile by using Build -> Publish, and select the "Folder" option, after publishing copy everything in the folder to `ServerPlugins` folder of TShock.

Afterwards you will need to configure your twitch api, you can either do it through console command `\setkitchenkey <api key>` or put the key in `kitchenapi.txt` inside the `tshock` subfolder within Tshock installation. And also configure your settings by adding a `kitchen.json` in the same folder with the format in the [Configuration section](#Configuration).

Once it's all done, load into a world and you can use `/startkitchen <twitch channle name> <player name>` and twitch integration will start accepting orders from chatters.

## Additional Console Commands

### /resetkitchen
Reset the remaining balance of all chatters for the current world.

### /kitchengive \<username> \<amount>
Give twitch chat user with \<username> as their display name \<amount> of chat credits for the current world. Number can be negative to deduct credits.

## Twitch Chat commands
Twitch chat commands are throttled so only 1 command will be processed per unique user every 5 seconds.

### !t balance
Shows the current remaining balance of the chatter

### !t buy \<item> \<amount>
Buy \<amount> of \<item> spawns, item name is defined in configuration and is case sensitive. 

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
    ],
    "StartingMoney": 500,
    "Income": 10,
    "IncomeInterval": 60,
    "SubscriberPriceMultiplier": 0.9,
    "ModAbuse": false
}
```
With each purchasable mob as a separate entry. 
- `InternalName` is the ID of the NPC found in [this list](https://tshock.readme.io/docs/npc-list)
- `MobName` is the name that will be entered into the `!t buy` command, case sensitive
- `MobAlias` is a list of other similar names that will also be treated as ordering for this entry, also case sensitive
- `Price` is the credits cost per item
- `MaxBuys` is the maximum number of purchases that can be made in a single order
- `StartingMoney` is the credits new chatters will start with
- `Income` is the amount of credits each chatters still in the channel chat room will receive periodically
- `IncomeInterval` is the number of seconds between each income payout, minimum is 5 seconds.
- `SubscriberPriceMultiplier` is the price multiplier for channel subscribers
- `ModAbuse` being set to true will allow mods to buy any mobs among the entries for free