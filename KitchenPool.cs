using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaKitchen
{
    public class KitchenPool
    {
        public Dictionary<string, int> Contributions { get; set; }

        public string PoolName { get; set; }

        public string? Customer { get; set; }

        public KitchenConfig.KitchenEntry? TargetEntry { get; set; }

        public KitchenEvent? TargetEvent { get; set; }

        public int Index { get; private set; }

        public static int PoolIdx = 0;

        private KitchenPool()
        {
            Contributions = new Dictionary<string, int>();
            Index = ++PoolIdx;
            PoolName = $"Pool {Index}";
        }

        public KitchenPool(KitchenConfig.KitchenEntry entry) : this()
        {
            TargetEntry = entry;
            PoolName = $"Pool {Index} - {entry.MobName}";
        }

        public KitchenPool(KitchenEvent kitchenEvent) : this()
        {
            TargetEvent = kitchenEvent;
            PoolName = $"Event Pool {Index} - {kitchenEvent.EventName}";
        }

        public int TargetValue()
        {
            if (TargetEntry != null)
            {
                return TargetEntry.Price;
            }
            if (TargetEvent != null)
            {
                return TargetEvent.Price;
            }

            return -1;
        }

        public bool TargetReached => TotalContributions() >= TargetValue();

        public int TotalContributions()
        {
            return Contributions.Sum(c => c.Value);
        }

        public int Contribute(string userName, int amount)
        {
            lock (Contributions)
            {
                amount = Math.Min(amount, TargetValue() - TotalContributions());
                if (Contributions.ContainsKey(userName))
                {
                    if (amount + Contributions[userName] < 0)
                    {
                        return 0;
                    }
                    Contributions[userName] += amount;
                }
                else
                {
                    if (amount <= 0)
                    {
                        return 0;
                    }
                    Contributions[userName] = amount;
                }

                return amount;
            }
        }
    }
}
