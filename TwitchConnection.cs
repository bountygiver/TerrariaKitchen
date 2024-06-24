using Newtonsoft.Json;
using System.Net.Sockets;
using Terraria;
using TShockAPI;
using TwitchLib.Api.Core.Models.Undocumented.Chatters;
using TwitchLib.Api.Helix.Models.Charity.GetCharityCampaign;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;
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

        public Func<int, int, string, string?, bool, bool>? SpawnUnit { get; set; }

        private TwitchClient? TwitchClient;

        private string? apiKey;

        private readonly KitchenCreditsStore Store;

        private readonly KitchenConfig Config;

        private readonly KitchenOverlay Overlay;

        private int MessageRateLimit;

        private DateTime? NextRateLimitRefresh;

        public Dictionary<string, DateTime> ChatTimeout { get; private set; }

        private Timer? incomeTimer;

        public TwitchConnection(KitchenCreditsStore store, KitchenConfig config, KitchenOverlay overlay)
        {
            Store = store;
            Config = config;
            Overlay = overlay;
            ChatTimeout = new Dictionary<string, DateTime>();
            Config = config;
            MessageRateLimit = 0;
            Overlay.NewConnection += UpdateOverlay;
        }

        private void UpdateOverlay(object? sender, NetworkStream stream)
        {
            if (stream.CanWrite && TwitchClient?.IsConnected == true)
            {
                try
                {
                    var response = KitchenOverlay.GetFrameFromString(JsonConvert.SerializeObject(new
                    {
                        @event = "initialize",
                        pools = Store.Pools.Select(p => new { idx = p.Index, name = p.PoolName, current = p.TotalContributions(), target = p.TargetValue() }),
                        waves = Store.Waves.Select(w => new { chatter = w.Chatter, current = w.CurrentMobs.Count, target = w.TargetNumber }),
                    }));
                    stream.Write(response, 0, response.Length);
                }
                catch
                {

                }
            }
        }

        private void Reply(ChatMessage msg, string txt, int Threshold = 95)
        {
            if (NextRateLimitRefresh == null || NextRateLimitRefresh < DateTime.Now)
            {
                NextRateLimitRefresh = DateTime.Now.AddSeconds(30);
                MessageRateLimit = 0;
            }

            if (MessageRateLimit >= Threshold)
            {
                return;
            }

            ++MessageRateLimit;
            TwitchClient?.SendReply(msg.Channel, msg.Id, txt);
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
                MessagesAllowedInPeriod = 95,
                ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            TwitchClient = new TwitchClient(customClient);
            TwitchClient.Initialize(new ConnectionCredentials(ChannelName, apiKey, disableUsernameCheck: true), ChannelName);

            Store.CurrentChatters.Clear();

            TwitchClient.OnConnected += (sender, e) => 
            { 
                onConnected();
            };
            TwitchClient.OnJoinedChannel += (sender, e) =>
            {
                TwitchClient.SendMessage(e.Channel, "Terraria Kitchen started! Use !t buy <name> <quantity> to spawn mobs!");
            };
            TwitchClient.OnDisconnected += (sender, e) =>
            {
                onDisconnected();
                incomeTimer?.Dispose();
                incomeTimer = null;
                Overlay.SendPacket(new { @event = "disconnect" });
            };
            TwitchClient.OnMessageReceived += HandleMessage;
            TwitchClient.OnExistingUsersDetected += (sender, e) => { 
                foreach (var user in e.Users)
                {
                    Store.CurrentChatters.Add(user.ToLower());
                }
            };
            TwitchClient.OnUserJoined += (sender, e) => Store.CurrentChatters.Add(e.Username.ToLower());
            TwitchClient.OnUserLeft += (sender, e) => Store.CurrentChatters.Remove(e.Username.ToLower());

            TwitchClient.Connect();
            Store.ChangeWorld(Main.worldID);

            Console.WriteLine($"(Terraria Kitchen) Kitchen started for worldID: {Main.worldID}");

            incomeTimer?.Dispose();
            if (Config.IncomeInterval < 5)
            {
                // Minimum 5 seconds interval.
                Config.IncomeInterval = 5;
            }
            incomeTimer = new Timer(Store.Income, null, TimeSpan.FromSeconds(Config.IncomeInterval), TimeSpan.FromSeconds(Config.IncomeInterval));
        }

        public void UpdateTime()
        {
            if (incomeTimer != null)
            {
                if (Config.IncomeInterval < 5)
                {
                    // Minimum 5 seconds interval.
                    Config.IncomeInterval = 5;
                }
                incomeTimer.Change(TimeSpan.FromSeconds(Config.IncomeInterval), TimeSpan.FromSeconds(Config.IncomeInterval));
            }
        }

        private void HandleMessage(object? sender, OnMessageReceivedArgs e)
        {
            try
            {
                string chatter = e.ChatMessage.Username.ToLower();
                Store.CurrentChatters.Add(chatter);
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
                            Buy(e.ChatMessage);
                            break;
                        case "pool":
                            Pool(e.ChatMessage);
                            break;
                        case "wave":
                            Wave(e.ChatMessage);
                            break;
                        case "event":
                            PoolEvent(e.ChatMessage);
                            break;
                        case "balance":
                        case "bal":
                            var amount = Store.GetBalance(chatter);
                            if (amount != -1)
                            {
                                Reply(e.ChatMessage, $"You have {amount} kitchen credits.", Threshold: 80);
                            }
                            break;
                        case "give":
                            if (buyParams.Length == 4)
                            {
                                if (Store.Give(chatter, buyParams[2].ToLower(), buyParams[3]))
                                {
                                    Reply(e.ChatMessage, $"Sent {buyParams[3]} credits to {buyParams[2]}. You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                                }
                                else
                                {
                                    Reply(e.ChatMessage, $"Failed to give credits. You have {Store.GetBalance(chatter)} kitchen credits.");
                                }
                            }
                            break;
                        case "say":
                            for (int i = 0; i < Main.npc.Length; i++)
                            {
                                if (Main.npc[i].active && Main.npc[i].townNPC)
                                {
                                    if (Main.npc[i].GivenName.ToLower() == chatter.ToLower())
                                    {
                                        NetMessage.SendData(119, -1, -1, Terraria.Localization.NetworkText.FromLiteral(string.Join(" ", buyParams.Skip(2))), number: 255, number2: Main.npc[i].position.X, number3: Main.npc[i].position.Y);
                                    }
                                }
                            }
                            break;
                        default:
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.Error("(Terraria Kitchen Error) - " + ex.Message);
                TShock.Log.Error(ex.StackTrace);
                Console.WriteLine("(Terraria Kitchen) Invalid Chat message detected. Unable to process.");
            }
        }

        private void PoolEvent(ChatMessage chatMessage)
        {
            var buyParams = chatMessage.Message.Split(' ');
            if (buyParams.Length != 4)
            {
                return;
            }
            if (int.TryParse(buyParams[3], out var amount) && amount > 0 && Config.FindEvent(buyParams[2]) is KitchenEvent kEvent)
            {
                string chatter = chatMessage.Username.ToLower();
                if (amount > Store.GetBalance(chatter))
                {
                    Reply(chatMessage, $"Not enough credits! You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                    return;
                }
                var newPool = false;
                var existingPool = Store.Pools.Where(p => p.TargetEvent != null).FirstOrDefault(p => p.TargetEvent?.EventId == kEvent.EventId);
                if (existingPool == null)
                {
                    existingPool = new KitchenPool(kEvent);
                    Store.Pools.Add(existingPool);
                    newPool = true;
                    Overlay.SendPacket(new { @event = "poolStart", idx = existingPool.Index, name = existingPool.PoolName, target = existingPool.TargetValue() });
                }
                var contributed = Store.ContributePool(chatter, amount, existingPool.Index);
                if (existingPool.TargetReached)
                {
                    Overlay.SendPacket(new { @event = "poolEnd", idx = existingPool.Index });
                    if (existingPool.TargetEvent?.Start() == true)
                    {
                        Store.ResolvePoolPayment(existingPool);
                    }
                }
                else
                {
                    Overlay.SendPacket(new { @event = "poolUpdate", idx = existingPool.Index, lastContribution = contributed, lastContributor = chatter, current = existingPool.TotalContributions() });
                    if (newPool)
                    {
                        Reply(chatMessage, $"Started a new {existingPool.PoolName} event pool with id {existingPool.Index}, you now have {Store.GetBalance(chatter)} kitchen credits remaining. Contribution: {contributed}/{existingPool.TargetValue()}.");
                    }
                }
            }
        }

        private void Pool(ChatMessage chatMessage)
        {
            var buyParams = chatMessage.Message.Split(' ');
            if (buyParams.Length != 4)
            {
                return;
            }

            if (int.TryParse(buyParams[2], out var poolIdx) && int.TryParse(buyParams[3], out var amt) && amt > 0)
            {
                if (Store.GetPool(poolIdx) is KitchenPool existingPool)
                {
                    var chatter = chatMessage.Username.ToLower();
                    if (amt > Store.GetBalance(chatter))
                    {
                        Reply(chatMessage, $"Not enough credits! You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                        return;
                    }
                    var contributed = Store.ContributePool(chatter, amt, existingPool.Index);
                    if (existingPool.TargetReached)
                    {
                        Overlay.SendPacket(new { @event = "poolEnd", idx = existingPool.Index });
                        if (existingPool.TargetEntry is KitchenConfig.KitchenEntry entry)
                        {
                            if (SpawnUnit?.Invoke(entry.InternalName, 1, $"{existingPool.Contributions.Count(c => c.Value > 0)} customers have ", existingPool.Customer, false) == true)
                            {
                                Store.ResolvePoolPayment(existingPool);
                            }
                        }
                        else if (existingPool.TargetEvent is KitchenEvent kitchenEvent)
                        {
                            if (kitchenEvent.Start())
                            {
                                Store.ResolvePoolPayment(existingPool);
                            }
                        }
                    }
                    else
                    {
                        Overlay.SendPacket(new { @event = "poolUpdate", idx = existingPool.Index, lastContribution = contributed, lastContributor = chatter, current = existingPool.TotalContributions() });
                    }
                }
            }
        }

        private void Wave(ChatMessage chatMessage)
        {
            var buyParams = chatMessage.Message.Split(' ');
            if (buyParams.Length < 3 || buyParams.Length > 6)
            {
                return;
            }
            var chatter = chatMessage.Username.ToLower();
            var waveCommand = buyParams[2];
            switch (waveCommand)
            {
                case "start":
                    if (Store.Waves.Any(a => a.Chatter == chatter)) 
                    {
                        Reply(chatMessage, $"You cannot start a wave when you have an existing wave in progress. You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                    }
                    if (int.TryParse(buyParams[3], out var targetNumber) && targetNumber > 0 && targetNumber < 100)
                    {
                        Store.Waves.Add(new KitchenWave(chatter, targetNumber));
                        Overlay.SendPacket(new { @event = "waveStart", chatter, current = 0, target = targetNumber });
                        Reply(chatMessage, $"A new wave of size {targetNumber} has been started. You have {Store.GetBalance(chatter)} kitchen credits remaining. Chatters may use '!t wave buy {chatter} <mob_name> <amount>' to add mobs to your wave.");
                    }
                    break;
                case "buy":
                    if (buyParams.Length != 6)
                    {
                        return;
                    }
                    if (int.TryParse(buyParams[5], out var amt) && amt > 0 && Store.Waves.FirstOrDefault(a => a.Chatter == buyParams[3]) is KitchenWave wave)
                    {
                        var mob = Config.FindEntry(buyParams[4]);
                        if (mob == null || mob.InternalName == 113)
                        {
                            Reply(chatMessage, $"Invalid mob. You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                            return;
                        }

                        if (amt > mob.MaxBuys)
                        {
                            amt = mob.MaxBuys;
                        }

                        var price = mob.Price * amt;
                        if (chatMessage.IsSubscriber)
                        {
                            price = (int)Math.Floor(price * Config.SubscriberPriceMultiplier);
                        }

                        if (price < 0)
                        {
                            // Overflow protection
                            return;
                        }

                        if (Store.GetBalance(chatter) < price)
                        {
                            Reply(chatMessage, $"Not enough credits! You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                            return;
                        }
                        wave.AddContribution(chatter, price);
                        wave.AddMobs(mob.InternalName, amt);

                        if (wave.TargetHit)
                        {
                            foreach (var mobId in wave.CurrentMobs)
                            {
                                SpawnUnit?.Invoke(mobId, 1, "", null, true);
                            }
                            Store.ResolveWavePayment(wave);

                            TSPlayer.All.SendInfoMessage($"A wave of {wave.CurrentMobs.Count} has been spawned by {wave.Contributions.Count(c => c.Value > 0)} chatters.");
                            Overlay.SendPacket(new { @event = "waveEnd", chatter });
                        }
                        else
                        {
                            Overlay.SendPacket(new { @event = "waveUpdate", chatter = wave.Chatter, by = chatter, current = wave.CurrentMobs.Count, increment = amt, mob = mob.MobName });
                            //Reply(chatMessage, $"Added {amt} {buyParams[4]} to {wave.Chatter}'s wave ({wave.CurrentMobs.Count}/{wave.TargetNumber})! You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                        }
                    }
                    else
                    {
                        Reply(chatMessage, $"Wave purchase failed. The player may not be starting a wave or invalid amount. You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                        return;
                    }
                    break;
                case "cancel":
                    if (Store.Waves.FirstOrDefault(a => a.Chatter == chatter) is KitchenWave cancelWave)
                    {
                        Store.Waves.Remove(cancelWave);
                        Reply(chatMessage, $"Wave request cancelled, a total of {cancelWave.TotalContributions()} credits has been refunded. You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                        Overlay.SendPacket(new { @event = "waveEnd", chatter });
                    }
                    break;
            }
        }

        private void Buy(ChatMessage chatMessage)
        {
            var buyParams = chatMessage.Message.Split(' ');
            if (buyParams.Length < 4 || buyParams.Length > 5)
            {
                return;
            }
            var mobName = buyParams[2];
            var mobAmount = buyParams[3];
            var buyTarget = buyParams.Length == 5 ? buyParams[4] : null;
            var chatter = chatMessage.Username.ToLower();
            //Console.WriteLine($"(Terraria Kitchen) {chatter} attempts to buy {mobAmount}, {mobName}");
            if (int.TryParse(mobAmount, out var amount))
            {
                if (amount <= 0)
                {
                    // Invalid number
                    return;
                }

                var mob = Config.FindEntry(mobName);
                if (mob == null)
                {
                    // Mob not found.
                    Reply(chatMessage, $"Unknown mob. You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                    return;
                }

                if (mob.Pooling)
                {
                    if (amount > Store.GetBalance(chatter))
                    {
                        Reply(chatMessage, $"Not enough credits! You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                        return;
                    }
                    var newPool = false;
                    var existingPool = Store.Pools.Where(p => p.TargetEntry != null).FirstOrDefault(p => p.TargetEntry?.InternalName == mob.InternalName);
                    if (existingPool == null)
                    {
                        existingPool = new KitchenPool(mob);
                        existingPool.Customer = buyTarget;
                        Store.Pools.Add(existingPool);
                        newPool = true;
                        Overlay.SendPacket(new { @event = "poolStart", idx = existingPool.Index, name = existingPool.PoolName, target = existingPool.TargetValue() });
                    }
                    var contributed = Store.ContributePool(chatter, amount, existingPool.Index);
                    if (existingPool.TargetReached)
                    {
                        Overlay.SendPacket(new { @event = "poolEnd", idx = existingPool.Index });
                        if (SpawnUnit?.Invoke(mob.InternalName, 1, $"{existingPool.Contributions.Count(c => c.Value > 0)} customers have ", existingPool.Customer, false) == true)
                        {
                            Store.ResolvePoolPayment(existingPool);
                        }
                    }
                    else
                    {
                        Overlay.SendPacket(new { @event = "poolUpdate", idx = existingPool.Index, lastContribution = contributed, lastContributor = chatter, current = existingPool.TotalContributions() });
                        if (newPool)
                        {
                            Reply(chatMessage, $"Started a new pool for {mob.MobName} with id {existingPool.Index}, you now have {Store.GetBalance(chatter)} kitchen credits remaining. Contribution: {contributed}/{existingPool.TargetValue()}.");
                        }
                        else
                        {
                            //Reply(chatMessage, $"Contributed {contributed} credits to pool id {existingPool.Index}, you now have {Store.GetBalance(chatter)} kitchen credits remaining. Contribution: {existingPool.TotalContributions()}/{existingPool.TargetValue()}.");
                        }
                    }
                }
                else
                {
                    if (amount > mob.MaxBuys)
                    {
                        amount = mob.MaxBuys;
                    }

                    var price = mob.Price * amount;
                    if (chatMessage.IsSubscriber)
                    {
                        price = (int)Math.Floor(price * Config.SubscriberPriceMultiplier);
                    }

                    if (price < 0)
                    {
                        // Overflow protection
                        return;
                    }

                    if (Config.ModAbuse && chatMessage.UserType == TwitchLib.Client.Enums.UserType.Moderator)
                    {
                        SpawnUnit?.Invoke(mob.InternalName, amount, chatter, buyTarget, false);
                    }
                    else
                    {
                        if (Store.GetBalance(chatter) < price)
                        {
                            // Not enough money.
                            Reply(chatMessage, $"Not enough credits! You have {Store.GetBalance(chatter)} kitchen credits remaining.");
                            return;
                        }

                        if (SpawnUnit?.Invoke(mob.InternalName, amount, chatter, buyTarget, false) == true)
                        {
                            Store.ModBalance(chatter, -price);
                        }
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
