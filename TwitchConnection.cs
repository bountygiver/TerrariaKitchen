using Terraria;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace TerrariaKitchen
{
    public class TwitchConnection
    {
        public string? ChannelName { get; private set; }

        public Func<int, int, string, bool>? SpawnUnit { get; set; }

        private TwitchClient? TwitchClient;

        private string? apiKey;

        private readonly KitchenCreditsStore Store;

        private readonly KitchenConfig Config;

        public Dictionary<string, DateTime> ChatTimeout { get; private set; }

        private Timer? incomeTimer;

        public TwitchConnection(KitchenCreditsStore store, KitchenConfig config)
        {
            Store = store;
            Config = config;
            ChatTimeout = new Dictionary<string, DateTime>();
            Config = config;
        }

        public void Initialize(string channelName, Action onConnected, Action onDisconnected)
        {
            if (TwitchClient?.IsConnected == true)
            {
                if (TwitchClient.ConnectionCredentials.TwitchUsername == channelName)
                {
                    return;
                }
                TwitchClient.Disconnect();
            }
            ChannelName = channelName;

            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = 750,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            TwitchClient = new TwitchClient(customClient);
            TwitchClient.Initialize(new ConnectionCredentials(ChannelName, apiKey, disableUsernameCheck: true), ChannelName);

            Store.CurrentChatters.Clear();

            TwitchClient.OnConnected += (sender, e) => onConnected();
            TwitchClient.OnDisconnected += (sender, e) =>
            {
                onDisconnected();
                incomeTimer?.Dispose();
                incomeTimer = null;
            };
            TwitchClient.OnMessageReceived += HandleMessage;
            TwitchClient.OnExistingUsersDetected += (sender, e) => { 
                foreach (var user in e.Users)
                {
                    Store.CurrentChatters.Add(user);
                }
            };
            TwitchClient.OnUserJoined += (sender, e) => Store.CurrentChatters.Add(e.Username);
            TwitchClient.OnUserLeft += (sender, e) => Store.CurrentChatters.Remove(e.Username);

            TwitchClient.Connect();
            Store.ChangeWorld(Main.worldID);

            Console.WriteLine($"Kitchen started for worldID: {Main.worldID}");

            incomeTimer?.Dispose();
            if (Config.IncomeInterval < 5)
            {
                // Minimum 5 seconds interval.
                Config.IncomeInterval = 5;
            }
            incomeTimer = new Timer(Store.Income, null, TimeSpan.FromSeconds(Config.IncomeInterval), TimeSpan.FromSeconds(Config.IncomeInterval));
        }

        private void HandleMessage(object? sender, OnMessageReceivedArgs e)
        {
            try
            {
                string chatter = e.ChatMessage.DisplayName;
                if (e.ChatMessage.Message.StartsWith("!t "))
                {
                    if (ChatTimeout.TryGetValue(chatter, out var expiry))
                    {
                        if (expiry > DateTime.Now)
                        {
                            return;
                        }
                    }
                    ChatTimeout[chatter] = DateTime.Now.AddSeconds(5);

                    var buyParams = e.ChatMessage.Message.Split(' ');

                    if (buyParams.Length < 2)
                    {
                        return;
                    }

                    switch (buyParams[1])
                    {
                        case "buy":
                            if (buyParams.Length == 4)
                            {
                                Buy(e.ChatMessage, buyParams[2], buyParams[3]);
                            }
                            break;
                        case "balance":
                            var amount = Store.GetBalance(chatter);
                            if (amount != -1)
                            {
                                TwitchClient?.SendReply(e.ChatMessage.Channel, e.ChatMessage.Id, $"You have {amount} kitchen credits.");
                            }
                            break;
                        default:
                            return;
                    }
                }
            }
            catch
            {
                Console.WriteLine("(Terraria Kitchen) Invalid Chat message detected. Unable to process.");
            }
        }

        private void Buy(ChatMessage chatMessage, string mobName, string mobAmount)
        {
            var chatter = chatMessage.Username;
            Console.WriteLine($"(Terraria Kitchen) {chatter} attempts to buy {mobAmount}, {mobName}");
            if (int.TryParse(mobAmount, out var amount))
            {
                if (amount <= 0)
                {
                    // Invalid number
                    return;
                }

                var mob = Config.Entries.FirstOrDefault(e => e.MobName == mobName);

                if (mob == null)
                {
                    mob = Config.Entries.Where(e => e.MobAlias != null && e.MobAlias.Count > 0).FirstOrDefault(e => e.MobAlias.Contains(mobName) == true);
                }
                if (mob == null)
                {
                    // Mob not found.
                    return;
                }
                if (amount > mob.MaxBuys)
                {
                    amount = mob.MaxBuys;
                }

                var price = mob.Price * amount;
                if (chatMessage.IsSubscriber)
                {
                    price = (int) Math.Floor(price * Config.SubscriberPriceMultiplier);
                }

                if (Config.ModAbuse && chatMessage.UserType == TwitchLib.Client.Enums.UserType.Moderator)
                {
                    SpawnUnit?.Invoke(mob.InternalName, amount, chatter);
                }
                else
                {
                    if (Store.GetBalance(chatter) < price)
                    {
                        // Not enough money.
                        return;
                    }

                    if (SpawnUnit?.Invoke(mob.InternalName, amount, chatter) == true)
                    {
                        Store.ModBalance(chatter, -price);
                    }
                }
            }

        }

        public void SetAPIKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public bool HasAPIKey() => apiKey != null;

        public void Dispose()
        {
            if (TwitchClient?.IsConnected == true)
            {
                TwitchClient.Disconnect();
            }
            incomeTimer?.Dispose();
        }
    }
}
