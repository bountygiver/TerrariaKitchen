using TShockAPI;
using Terraria;
using TerrariaApi.Server;
using Terraria.ID;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

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

        private static readonly string KitchenConfigPath = Path.Combine(TShock.SavePath, "kitchen.json");

        public KitchenConfig Config { get; private set; }

        private static readonly string ApiPath = Path.Combine(TShock.SavePath, "kitchenapi.txt");

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
            Console.WriteLine($"(Terraria Kitchen) Config: {JsonConvert.SerializeObject(Config)}");
            Store = new KitchenCreditsStore(Config);
            _connection = new TwitchConnection(Store, Config);
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
            Commands.ChatCommands.Add(new Command(Permissions.spawn, (args) => Store.ResetChatterBalance(), "resetkitchen") { HelpText = "Reset all chatter balance in the current world." });
            Commands.ChatCommands.Add(new Command(Permissions.spawn, GiveMoney, "kitchengive") { HelpText = "Give a chatter an amount of credits." });

            ServerApi.Hooks.WorldSave.Register(this, (e) =>
            {
                Store.UpdatePlayersToDb();
            });
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
                if (list.Count == 0)
                {
                    args.Player.SendErrorMessage($"Can't find player {args.Parameters[1]}");
                    return;
                }

                targetPlayer = list[0];
            }

            if (kitchenTargetID != null && kitchenTargetID != targetPlayer.UUID)
            {
                args.Player.SendInfoMessage("A previous player was already being served by the kitchen, changing customer...");
            }
            kitchenTargetID = targetPlayer.UUID;
            _connection.Initialize(args.Parameters[0], () =>
            {
                args.Player.SendInfoMessage("(Terraria Kitchen) Connected to twitch chat.");
            }, () =>
            {
                args.Player.SendInfoMessage("(Terraria Kitchen) Disconnected from twitch chat.");
            });
        }

        private bool SpawnFunc(int mobId, int count, string? sender = null)
        {
            if (kitchenTargetID == null)
            {
                return false;
            }

            var result = Math.Min(count, 200);
            if (result < 0)
            {
                return false;
            }

            TSPlayer? spawnPlayer = TShock.Players.FirstOrDefault(p => p.UUID == kitchenTargetID);
            if (spawnPlayer == null)
            {
                TShock.Utils.Broadcast("(Terraria Kitchen) Customer not found, kitchen is closed...", Convert.ToByte(255), 0, 0);
                kitchenTargetID = null;
                return false;
            }

            NPC nPC = TShock.Utils.GetNPCById(mobId);
            if (nPC.type >= 1 && nPC.type < NPCID.Count && nPC.type != 113)
            {
                TSPlayer.Server.SpawnNPC(nPC.netID, nPC.FullName, result, spawnPlayer.TileX, spawnPlayer.TileY, 50, 20);
                TSPlayer.All.SendSuccessMessage($"{sender ?? "Someone"} ordered {result} {nPC.FullName}{(result > 1 ? "s" : "")}!");

                return true;
            }
            else if (nPC.type == 113)
            {
                if (Main.wofNPCIndex != -1 || spawnPlayer.Y / 16f < (float)(Main.maxTilesY - 205))
                {
                    return false;
                }

                NPC.SpawnWOF(new Vector2(spawnPlayer.X, spawnPlayer.Y));

                TSPlayer.All.SendSuccessMessage("ONE WALL OF FLESH, SERVING RIGHT UP");

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
            }
            base.Dispose(disposing);
        }
    }
}
