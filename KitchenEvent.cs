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
            QueueEyeOfCthulu,
            QueueRoboTwin,
            QueueRoboWorm,
            QueueRoboSkelly,
        }

        public string EventName { get; set; }

        public EventType EventId { get; set; }

        public int Price { get; set; }

        public List<string> EventAlias { get; set; }

        private bool StartInvasion(int type)
        {
            int num = 0;
            for (int i = 0; i < 255; i++)
            {
                if (Terraria.Main.player[i].active)
                {
                    num++;
                    if (Terraria.Main.player[i].statLifeMax >= 200)
                    {
                        num++;
                    }
                }
            }

            if (num == 0)
            {
                return false;
            }

            var invasionMult = TShock.Config.Settings.InvasionMultiplier * num / 2;

            Terraria.Main.invasionType = type;
            Terraria.Main.invasionSize = 80 + 40 * invasionMult;
            if (type == 3)
            {
                Terraria.Main.invasionSize += 40 + 20 * invasionMult;
            }

            if (type == 4)
            {
                Terraria.Main.invasionSize = 160 + 40 * invasionMult;
            }

            Terraria.Main.invasionSizeStart = Terraria.Main.invasionSize;
            Terraria.Main.invasionProgress = 0;
            Terraria.Main.invasionProgressIcon = type + 3;
            Terraria.Main.invasionProgressWave = 0;
            Terraria.Main.invasionProgressMax = Terraria.Main.invasionSizeStart;
            Terraria.Main.invasionWarn = 0;
            if (type == 4)
            {
                Terraria.Main.invasionX = Terraria.Main.spawnTileX - 1;
                Terraria.Main.invasionWarn = 2;
            }
            else if (KitchenPlugin.randomSeed.Next(2) == 0)
            {
                Terraria.Main.invasionX = 0.0;
            }
            else
            {
                Terraria.Main.invasionX = Terraria.Main.maxTilesX;
            }

            return true;
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
                    if (StartInvasion(1))
                    {
                        TSPlayer.All.SendInfoMessage("The chatters union has started a goblin invasion event.");
                        return true;
                    }
                    break;
                case EventType.WinterInvasion:
                    if (StartInvasion(2))
                    {
                        TSPlayer.All.SendInfoMessage("The chatters union has started a winter invasion event.");
                        return true;
                    }
                    break;
                case EventType.PirateInvasion:
                    if (StartInvasion(3))
                    {
                        TSPlayer.All.SendInfoMessage("The chatters union has started a pirate invasion event.");
                        return true;
                    }
                    break;
                case EventType.AlienInvasion:
                    if (StartInvasion(4))
                    {
                        TSPlayer.All.SendInfoMessage("The chatters union has started a alien invasion event.");
                        return true;
                    }
                    break;
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
                case EventType.QueueEyeOfCthulu:
                    var eye = TShock.Utils.GetNPCById(4);
                    return QueueNightBoss(eye, () =>
                    {
                        if (Terraria.WorldGen.spawnEye)
                        {
                            TSPlayer.All.SendInfoMessage($"An {eye.FullName} will already be spawned tonight. Please complete the event later.");
                            return false;
                        }
                        Terraria.WorldGen.spawnEye = true;
                        return true;
                    });
                case EventType.QueueRoboSkelly:
                    var skelly = TShock.Utils.GetNPCById(127);
                    return QueueNightBoss(skelly, () =>
                    {
                        if (Terraria.WorldGen.spawnEye)
                        {
                            TSPlayer.All.SendInfoMessage($"Another mechanical boss will already be spawned tonight. Please complete the event later.");
                            return false;
                        }
                        Terraria.WorldGen.spawnHardBoss = 3;
                        return true;
                    });
                case EventType.QueueRoboTwin:
                    var twins = TShock.Utils.GetNPCById(125);
                    return QueueNightBoss(twins, () =>
                    {
                        if (Terraria.WorldGen.spawnEye)
                        {
                            TSPlayer.All.SendInfoMessage($"Another mechanical boss will already be spawned tonight. Please complete the event later.");
                            return false;
                        }
                        Terraria.WorldGen.spawnHardBoss = 2;
                        return true;
                    });
                case EventType.QueueRoboWorm:
                    var worm = TShock.Utils.GetNPCById(134);
                    return QueueNightBoss(worm, () =>
                    {
                        if (Terraria.WorldGen.spawnEye)
                        {
                            TSPlayer.All.SendInfoMessage($"Another mechanical boss will already be spawned tonight. Please complete the event later.");
                            return false;
                        }
                        Terraria.WorldGen.spawnHardBoss = 1;
                        return true;
                    });
            }

            return false;
        }

        private bool QueueNightBoss(Terraria.NPC npc, Func<bool> queueAction)
        {
            if (Terraria.Main.dayTime)
            {
                if (!queueAction())
                {
                    return false;
                }
                TSPlayer.All.SendInfoMessage($"The chatters union has purchased an {npc.FullName} which will spawn tonight.");
            }
            else
            {
                for (int j = 0; j < 255; j++)
                {
                    if (Terraria.Main.player[j].active)
                    {
                        var lastPlayer = Terraria.Main.player[j];
                        if (!lastPlayer.dead && (lastPlayer.position.Y < Terraria.Main.worldSurface * 16.0))
                        {
                            Terraria.NPC.SpawnOnPlayer(j, npc.type);
                            if (npc.type == 125)
                            {
                                Terraria.NPC.SpawnOnPlayer(j, 126);
                            }
                            TSPlayer.All.SendInfoMessage($"The chatters union has purchased an {npc.FullName}.");
                            return true;
                        }
                    }
                }

                var players = TShock.Players.Where(p => p?.RealPlayer == true).ToArray();
                if (players.Any())
                {
                    var spawnPlayer = players[KitchenPlugin.randomSeed.Next(players.Length)];
                    TSPlayer.Server.SpawnNPC(npc.netID, npc.FullName, 1, spawnPlayer.TileX, spawnPlayer.TileY, 50, 20);
                    if (npc.type == 125)
                    {
                        var eye2 = TShock.Utils.GetNPCById(126);
                        TSPlayer.Server.SpawnNPC(eye2.netID, eye2.FullName, 1, spawnPlayer.TileX, spawnPlayer.TileY, 50, 20);
                        TSPlayer.All.SendInfoMessage($"The chatters union has purchased the mechanical twins.");
                    }
                    else
                    {
                        TSPlayer.All.SendInfoMessage($"The chatters union has purchased the {npc.FullName}.");
                    }
                    return true;
                }
            }

            return false;
        }
    }
}
