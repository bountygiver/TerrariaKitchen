using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TerrariaKitchen
{
    public class KitchenWave
    {
        public int TargetNumber { get; set; }

        public string Chatter { get; set; }

        public List<int> CurrentMobs { get; set; }

        public Dictionary<string, int> Contributions { get; set; }

        public KitchenWave(string chatter, int target) 
        {
            Chatter = chatter;
            TargetNumber = target;
            CurrentMobs = new List<int>();
            Contributions = new Dictionary<string, int>();
        }

        public bool TargetHit => CurrentMobs.Count >= TargetNumber;

        public bool AddMobs(int mobID, int amt)
        {
            int i = amt;
            while (i > 0)
            {
                CurrentMobs.Add(mobID);
                --i;
            }

            return TargetHit;
        }

        public int AddContribution(string userName, int amount)
        {

            lock (Contributions)
            {
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

        public int TotalContributions()
        {
            return Contributions.Sum(c => c.Value);
        }
    }
}
