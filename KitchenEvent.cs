using TShockAPI;

namespace TerrariaKitchen
{
    public class KitchenEvent
    {
        public enum EventType
        {
            FullMoon,
            Bloodmoon,
            Eclipse,
            Sandstorm,
            Rain,
            SlimeRain,
            GoblinInvasion,
            WinterInvasion,
            PirateInvasion,
            AlienInvasion,
            PumpkinMoon,
            FrostMoon,
        }

        public string EventName { get; set; }

        public EventType EventId { get; set; }

        public int Price { get; set; }

        public List<string> EventAlias { get; set; }

        private void StartInvasion(int type)
        {
            int invasionSize = 0;
            invasionSize = 100 + (TShock.Config.Settings.InvasionMultiplier * Terraria.Main.player.Where(p => null != p && p.active).Count());

            // Order matters
            // StartInvasion will reset the invasion size

            Terraria.Main.StartInvasion(type);

            // Note: This is a workaround to previously providing the size as a parameter in StartInvasion
            // Have to set start size to report progress correctly
            Terraria.Main.invasionSizeStart = invasionSize;
            Terraria.Main.invasionSize = invasionSize;
        }

        public bool Start()
        {
            switch (EventId)
            {
                case EventType.FullMoon:
                    TSPlayer.Server.SetFullMoon();
                    TSPlayer.All.SendInfoMessage($"The chatters union has started a full moon event.");
                    return true;
                case EventType.Bloodmoon:
                    TSPlayer.Server.SetBloodMoon(true);
                    TSPlayer.All.SendInfoMessage("The chatters union has started a blood moon event.");
                    return true;
                case EventType.Eclipse:
                    TSPlayer.Server.SetFullMoon();
                    TSPlayer.All.SendInfoMessage("The chatters union has started a full moon event.");
                    return true;
                case EventType.Sandstorm:
                    Terraria.GameContent.Events.Sandstorm.StartSandstorm();
                    TSPlayer.All.SendInfoMessage("The chatters union has started a sandstorm event.");
                    return true;
                case EventType.Rain:
                    if (Terraria.Main.slimeRain)
                    {
                        Terraria.Main.StopSlimeRain();
                    }
                    Terraria.Main.StartRain();
                    TSPlayer.All.SendInfoMessage("The chatters union has started a rain event.");
                    return true;
                case EventType.SlimeRain:
                    if (Terraria.Main.raining)
                    {
                        Terraria.Main.StopRain();
                    }
                    Terraria.Main.StartSlimeRain();
                    return true;
                case EventType.GoblinInvasion:
                    StartInvasion(1);
                    TSPlayer.All.SendInfoMessage("The chatters union has started a goblin invasion event.");
                    return true;
                case EventType.WinterInvasion:
                    StartInvasion(2);
                    TSPlayer.All.SendInfoMessage("The chatters union has started a winter invasion event.");
                    return true;
                case EventType.PirateInvasion:
                    StartInvasion(3);
                    TSPlayer.All.SendInfoMessage("The chatters union has started a pirate invasion event.");
                    return true;
                case EventType.AlienInvasion:
                    StartInvasion(4);
                    TSPlayer.All.SendInfoMessage("The chatters union has started a alien invasion event.");
                    return true;
                case EventType.PumpkinMoon:
                    TSPlayer.Server.SetPumpkinMoon(pumpkinMoon: true);
                    Terraria.Main.bloodMoon = false;
                    Terraria.NPC.waveKills = 0f;
                    Terraria.NPC.waveNumber = 1;
                    TSPlayer.All.SendInfoMessage("The chatters union has started a pumpkin moon event.");
                    return true;
                case EventType.FrostMoon:
                    TSPlayer.Server.SetFrostMoon(snowMoon: true);
                    Terraria.Main.bloodMoon = false;
                    Terraria.NPC.waveKills = 0f;
                    Terraria.NPC.waveNumber = 1;
                    TSPlayer.All.SendInfoMessage("The chatters union has started a frost moon event.");
                    return true;

            }

            return false;
        }
    }
}
