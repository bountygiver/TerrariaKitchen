using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using System.Text;
using Terraria.Localization;
using System;
using Terraria.DataStructures;
using TShockAPI.DB;

namespace TerrariaKitchen
{
    [ApiVersion(2, 1)]
    public class KitchenPlugin : TerrariaPlugin
    {
        public override string Author => "bountygiver";

        public override string Description => "Connect to some sort of chat so chatter can buy mob spawns!";

        public override string Name => "Terraria Kitchen";

        public override Version Version => new Version(1, 0, 0, 0);

        private TwitchConnection _connection;

        private string? kitchenTargetID;

        private KitchenCreditsStore Store;

        private KitchenOverlay Overlay;

        private static readonly string KitchenConfigPath = Path.Combine(TShock.SavePath, "kitchen.json");

        public KitchenConfig Config { get; private set; }

        private static readonly string ApiPath = Path.Combine(TShock.SavePath, "kitchenapi.txt");

        public static Random randomSeed = new Random();

        public KitchenPlugin(Main game) : base(game)
        {
            if (File.Exists(KitchenConfigPath))
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<KitchenConfig>(File.ReadAllText(KitchenConfigPath));
                    Console.WriteLine($"(Terraria Kitchen) Config found! Loaded menu with {Config.Entries?.Count ?? 0} items!");
                }
                catch
                {
                    // No config, use default.
                    Config = new KitchenConfig();
                    Console.WriteLine($"(Terraria Kitchen) Error: Unable to load config properly. Default values used.");
                }
            }
            else
            {
                Config = new KitchenConfig();
            }
            Store = new KitchenCreditsStore(Config);
            Overlay = new KitchenOverlay(Config);
            _connection = new TwitchConnection(Store, Config, Overlay);
            _connection.SpawnUnit = SpawnFunc;

            if (File.Exists(ApiPath))
            {
                var apiText = File.ReadAllText(ApiPath);
                if (apiText.Length > 0)
                {
                    _connection.SetAPIKey(apiText);
                }
            }
            if (!_connection.HasAPIKey())
            {
                Console.WriteLine("(Terraria Kitchen) Twitch API key not set. You will need to run /setkitchenkey <API key> before you can start kitchen. You can place the apikey in a tshock\\kitchenapi.txt to have it set automatically.");
                Console.WriteLine("(Terraria Kitchen) Visit https://dev.twitch.tv/docs/irc/authenticate-bot/ for more information about getting an API key.");
            }
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(Permissions.spawn, StartKitchen, "startkitchen") { HelpText = "Start up the kitchen server." });
            Commands.ChatCommands.Add(new Command(Permissions.spawn, SetupKitchen, "setkitchenkey") { HelpText = "Set Twitch API key for kitchen." });
            Commands.ChatCommands.Add(new Command(Permissions.spawn, (args) =>
            {
                Store.ResetChatterBalance();
                Overlay.SendPacket(new { @event = "reset" });
            }, "resetkitchen") { HelpText = "Reset all chatter balance in the current world." });
            Commands.ChatCommands.Add(new Command(Permissions.spawn, GiveMoney, "kitchengive") { HelpText = "Give a chatter an amount of credits." });
            Commands.ChatCommands.Add(new Command(Permissions.spawn, (args) =>
            {
                try
                {
                    var newConfig = JsonConvert.DeserializeObject<KitchenConfig>(File.ReadAllText(KitchenConfigPath));
                    Console.WriteLine($"(Terraria Kitchen) Config found! Loaded menu with {newConfig.Entries?.Count ?? 0} items!");
                    if (newConfig.Entries?.Count >= 0)
                    {
                        Config.Entries = newConfig.Entries;
                    }
                    if (newConfig.Events?.Count >= 0)
                    {
                        Config.Events = newConfig.Events;
                    }
                    Config.Income = newConfig.Income;
                    Config.MaxBalance = newConfig.MaxBalance;
                    Config.StartingMoney = newConfig.StartingMoney;
                    Config.SubscriberPriceMultiplier = newConfig.SubscriberPriceMultiplier;
                    Config.ModAbuse = newConfig.ModAbuse;
                    Config.XRange = newConfig.XRange;
                    Config.YRange = newConfig.YRange;
                    if (Config.IncomeInterval != newConfig.IncomeInterval)
                    {
                        Config.IncomeInterval = newConfig.IncomeInterval;
                        _connection?.UpdateTime();
                    }
                }
                catch
                {
                    Console.WriteLine($"(Terraria Kitchen) Error reading config.");
                }
            }, "kitchenmenuupdate") { HelpText = "Re-read kitchen.json and update settings." });

            ServerApi.Hooks.WorldSave.Register(this, (e) =>
            {
                Store.UpdatePlayersToDb();
            });
            ServerApi.Hooks.NpcSpawn.Register(this, RaffleTownNPC);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            Overlay.StartServer(Config.OverlayPort, Config.OverlayWsPort);
        }

        private void OnGetData(GetDataEventArgs args)
        {
            if (!args.Handled && args.MsgID == PacketTypes.PlayerDeathV2)
            {
                using (BinaryReader reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
                {
                    int? mobType = null;

                    byte playerid = reader.ReadByte();
                    byte deathReason = reader.ReadByte();
                    short killerId;
                    short killerNPCIdx;
                    short projectileIdx;
                    byte deathType;
                    short projectileType;
                    short itemType;
                    byte itemPrefix;
                    string deathMsg;
                    if ((deathReason & 0b1) != 0)
                    {
                        killerId = reader.ReadByte();
                    }
                    if ((deathReason & 0b10) != 0)
                    {
                        killerNPCIdx = reader.ReadInt16();
                        if (Main.npc.Length > killerNPCIdx)
                        {
                            var npc = Main.npc[killerNPCIdx];
                            if (npc != null)
                            {
                                mobType = npc.type;
                            }
                        }
                    }
                    if ((deathReason & 0b100) != 0)
                    {
                        projectileIdx = reader.ReadInt16();
                        var proj = Main.projectile[projectileIdx];
                        if (proj.owner >= 0)
                        {
                            if (Main.npc.Length > proj.owner)
                            {
                                var npc = Main.npc[proj.owner];
                                if (npc != null)
                                {
                                    mobType = npc.type;
                                }
                            }
                        }
                    }
                    if ((deathReason & 0b1000) != 0)
                    {
                        deathType = reader.ReadByte();
                    }
                    if ((deathReason & 0b10000) != 0)
                    {
                        projectileType = reader.ReadInt16();
                    }
                    if ((deathReason & 0b100000) != 0)
                    {
                        itemType = reader.ReadInt16();
                    }
                    if ((deathReason & 0b1000000) != 0)
                    {
                        itemPrefix = reader.ReadByte();
                    }
                    if ((deathReason & 0b10000000) != 0)
                    {
                        deathMsg = reader.ReadString();
                    }
                    short damage = reader.ReadInt16();
                    byte hitDirection = reader.ReadByte();
                    byte flags = reader.ReadByte();

                    var player = Main.player[playerid];

                    var distributeAmt = Config.PlayerDeathIncome;

                    if (mobType != null && Config.Entries.FirstOrDefault(e => e.InternalName == mobType) is KitchenConfig.KitchenEntry entry)
                    {
                        distributeAmt += (int) (entry.Price * Config.PlayerDeathCreditRefund);
                    }

                    if (distributeAmt > 0)
                    {
                        Store.DistributeIncome(distributeAmt);
                        _connection?.Announce($"{player.name} died. Distributed {distributeAmt} credits to all chatters.");
                    }
                }
            }
        }

        private void RaffleTownNPC(NpcSpawnEventArgs args)
        {
            var npc = Main.npc[args.NpcId];
            if (npc != null && npc.townNPC)
            {
                var existingNames = new HashSet<string>();
                for (int i = 0; i < Main.npc.Length; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].netID != npc.netID && Main.npc[i].townNPC)
                    {
                        existingNames.Add(Main.npc[i].GivenName.ToLower());
                    }
                }
                var raffleNames = Store.CurrentChatters.Select(c => c.ToLower()).Except(existingNames);

                if (raffleNames.Any())
                {
                    var raffled = raffleNames.ElementAt(randomSeed.Next(raffleNames.Count()));
                    TSPlayer.All.SendSuccessMessage($"{raffled} has been raffled as {npc.TypeName}!");
                    npc.GivenName = raffled;
                    NetMessage.SendData(56, -1, -1, NetworkText.FromLiteral(npc.GivenName), args.NpcId);
                }
            }
        }

        private void GiveMoney(CommandArgs args)
        {
            if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
            {
                args.Player.SendErrorMessage("Invalid syntax, use /kitchengive <playername> <amount>");
                return;
            }

            if (int.TryParse(args.Parameters[1], out var amount))
            {
                Store.ModBalance(args.Parameters[0], amount);
                args.Player.SendSuccessMessage($"Given {args.Parameters[0]} {amount} credits!");
                return;
            }

            args.Player.SendErrorMessage("Invalid amount, please use only number.");
        }

        private void SetupKitchen(CommandArgs args)
        {
            if (args.Parameters.Count != 1)
            {
                args.Player.SendErrorMessage("Invalid syntax, use /setkitchenkey <twitch oauth key>");
                return;
            }

            _connection.SetAPIKey(args.Parameters[0]);
        }

        private void StartKitchen(CommandArgs args)
        {
            if (args.Parameters.Count < 1 || args.Parameters.Count > 2)
            {
                args.Player.SendErrorMessage("Invalid syntax, use /startkitchen <channelname> <optional playername>");
                return;
            }
            if (!_connection.HasAPIKey())
            {
                args.Player.SendErrorMessage("No Twitch API key set, use /setkitchenkey <twitch oauth key>");
            }

            var targetPlayer = args.Player;

            if (args.Parameters.Count == 2)
            {
                List<TSPlayer> list = TSPlayer.FindByNameOrID(args.Parameters[1]);
                targetPlayer = list.FirstOrDefault(p => p.RealPlayer);
                if (targetPlayer == null)
                {
                    args.Player.SendErrorMessage($"Can't find player {args.Parameters[1]}.");
                }

            }

            if (kitchenTargetID != null && kitchenTargetID != targetPlayer?.UUID)
            {
                args.Player.SendInfoMessage("A previous player was already being served by the kitchen, changing customer...");
            }
            kitchenTargetID = targetPlayer?.UUID;
            if (kitchenTargetID == null)
            {
                args.Player.SendInfoMessage("No players specified, all players are now eligible customers...");
            }
            _connection.Initialize(args.Parameters[0], () =>
            {
                args.Player.SendInfoMessage("(Terraria Kitchen) Connected to twitch chat.");
            }, () =>
            {
                args.Player.SendInfoMessage("(Terraria Kitchen) Disconnected from twitch chat.");
            });

            Overlay?.SendPacket(new { @event = "initialize" });
        }
        private void SpawnNPC(int type, string name, int amount, int startTileX, int startTileY, int tileXRange, int tileYRange)
        {
            Region? zone = null;
            if (Config.SafeSpawnZoneName != null)
            {
                zone = TShock.Regions.GetRegionByName(Config.SafeSpawnZoneName);
            }
            var inSafeZone = zone?.InArea(startTileX, startTileY) == true;
            for (int i = 0; i < amount; i++)
            {
                int tileX, tileY;
                if (inSafeZone)
                {
                    if (randomSeed.Next(2) == 0)
                    {
                        TShock.Utils.GetRandomClearTileWithInRange(zone.Area.Left - tileXRange, startTileY, tileXRange, tileYRange, out tileX, out tileY);
                    }
                    else
                    {
                        TShock.Utils.GetRandomClearTileWithInRange(zone.Area.Right + tileXRange, startTileY, tileXRange, tileYRange, out tileX, out tileY);
                    }
                }
                else
                {
                    TShock.Utils.GetRandomClearTileWithInRange(startTileX, startTileY, tileXRange, tileYRange, out tileX, out tileY);

                    int tries = 10;
                    while (zone?.InArea(tileX, tileY) == true && tries > 0)
                    {
                        if (startTileX < tileX)
                        {
                            startTileX -= tileXRange;
                        }
                        else
                        {
                            startTileX += tileXRange;
                        }
                        TShock.Utils.GetRandomClearTileWithInRange(startTileX, startTileY, tileXRange, tileYRange, out tileX, out tileY);
                        tries--;
                    }
                }
                NPC.NewNPC(new EntitySource_DebugCommand(), tileX * 16, tileY * 16, type);
            }
        }

        private bool SpawnFunc(int mobId, int count, string? sender = null, string? targetPlayer = null, bool silent = false)
        {
            var result = Math.Min(count, 200);
            if (result <= 0)
            {
                return false;
            }

            var players = TShock.Players.Where(p => p?.RealPlayer == true);

            if (!players.Any())
            {
                return false;
            }


            if (!silent && Config.Entries.FirstOrDefault(e => e.InternalName == mobId) is KitchenConfig.KitchenEntry entry)
            {
                if (entry.NoDay && Main.dayTime)
                {
                    return false;
                }

                if (entry.NoNight && !Main.dayTime)
                {
                    return false;
                }
            }

            if (mobId == 113)
            {
                // WOF Do not use target player, and will always attempt to spawn on whoever that is eligible.

                if (Main.wofNPCIndex != -1)
                {
                    return false;
                }

                float wofTargetY = Main.maxTilesY - 205;
                var wofTarget = players.FirstOrDefault(p => p.Y / 16f >= wofTargetY);

                if (wofTarget != null)
                {
                    NPC.SpawnWOF(new Vector2(wofTarget.X, wofTarget.Y));

                    TSPlayer.All.SendSuccessMessage("ONE WALL OF FLESH, SERVING RIGHT UP");

                    return true;
                }

                return false;
            }

            var targetedPlayer = false;
            TSPlayer? spawnPlayer = players.FirstOrDefault(p => p.UUID == kitchenTargetID);
            if (spawnPlayer == null)
            {
                // Use a random active player if kitchen has no target.
                spawnPlayer = players.ElementAt(randomSeed.Next(players.Count()));
            }

            if (targetPlayer != null && players.FirstOrDefault(p => p.Name.ToLower() == targetPlayer.ToLower()) is TSPlayer target)
            {
                // If target is specified, override
                targetedPlayer = true;
                spawnPlayer = target;
            }

            NPC nPC = TShock.Utils.GetNPCById(mobId);
            if (nPC.type >= -65 && nPC.type < NPCID.Count && nPC.type != 113)
            {
                var successMsg = new StringBuilder($"{sender ?? "Someone"} ordered {result} {nPC.FullName}{(result > 1 ? "s" : "")}");
                if (targetedPlayer)
                {
                    successMsg.Append($" for {targetPlayer}");
                }
                successMsg.Append("!");
                SpawnNPC(nPC.netID, nPC.FullName, result, spawnPlayer.TileX, spawnPlayer.TileY, Config.XRange ?? 50, Config.YRange ?? 20);
                if (!silent)
                {
                    TSPlayer.All.SendSuccessMessage(successMsg.ToString());
                }

                return true;
            }

            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Deregister hooks here
                _connection.Dispose();
                Overlay?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
