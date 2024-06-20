namespace TerrariaKitchen
{
    public class KitchenConfig
    {
        public class KitchenEntry
        {
            public string MobName { get; set; }

            public int InternalName { get; set; }

            public List<string> MobAlias { get; set; }

            public int Price { get; set; }

            public int MaxBuys { get; set; }
        }

        public List<KitchenEntry> Entries { get; set; } = new List<KitchenEntry>();

        public int StartingMoney { get; set; } = 500;

        public int Income { get; set; } = 10;

        public int IncomeInterval { get; set; } = 60;

        public int MaxBalance { get; set; } = 5000;

        public float SubscriberPriceMultiplier { get; set; } = 1;

        public bool ModAbuse { get; set; } = false;
    }
}
