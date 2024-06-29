
namespace TerrariaKitchen
{
    public partial class KitchenConfig
    {
        public class KitchenEntry
        {
            public string MobName { get; set; }

            public int InternalName { get; set; }

            public List<string> MobAlias { get; set; }

            public int Price { get; set; }

            public int MaxBuys { get; set; }

            public bool NoDay { get; set; }

            public bool NoNight { get; set; }

            public bool PriorityUnderground { get; set; }

            public bool Pooling { get; set; }
        }

        public List<KitchenEntry> Entries { get; set; } = new List<KitchenEntry>();

        public List<KitchenEvent> Events { get; set; } = new List<KitchenEvent>();

        public int StartingMoney { get; set; } = 500;

        public int Income { get; set; } = 10;

        public int IncomeInterval { get; set; } = 60;

        public int PlayerDeathIncome { get; set; } = 0;

        public float PlayerDeathCreditRefund { get; set; } = 0f;

        public string? SafeSpawnZoneName { get; set; }

        public int MaxBalance { get; set; } = 5000;

        public float SubscriberPriceMultiplier { get; set; } = 1;

        public bool ModAbuse { get; set; } = false;

        public int? XRange { get; set; }

        public int? YRange { get; set; }

        public int OverlayPort { get; set; } = 7770;

        public int OverlayWsPort { get; set; } = 7771;

        public KitchenEntry? FindEntry(string mobName)
        {
            var mob = Entries.FirstOrDefault(e => e.MobName == mobName);

            if (mob == null)
            {
                mob = Entries.Where(e => e.MobAlias != null && e.MobAlias.Count > 0).FirstOrDefault(e => e.MobAlias.Contains(mobName) == true);
            }

            return mob;
        }

        public KitchenEvent? FindEvent(string eventName)
        {
            var kEvent = Events.FirstOrDefault(e => e.EventName == eventName);

            if (kEvent == null)
            {
                kEvent = Events.Where(e => e.EventAlias != null && e.EventAlias.Count > 0).FirstOrDefault(e => e.EventAlias.Contains(eventName) == true);
            }

            return kEvent;
        }
    }
}
