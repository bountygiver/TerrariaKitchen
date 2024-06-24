using Microsoft.Data.Sqlite;
using TwitchLib.Api.Helix.Models.Chat.GetChatters;

namespace TerrariaKitchen
{
    public class KitchenCreditsStore
    {
        private static readonly string connectionString = "Data Source=terrariaKitchen.db";

        private Dictionary<string, int> Credits;

        public HashSet<string> CurrentChatters { get; private set; }

        public List<KitchenPool> Pools { get; private set; }

        public List<KitchenWave> Waves { get; private set; }

        private int? CurrentWorld;

        private readonly KitchenConfig Config;

        public KitchenCreditsStore(KitchenConfig config)
        {
            Config = config;
            Credits = new Dictionary<string, int>();
            CurrentChatters = new HashSet<string>();
            Pools = new List<KitchenPool>();
            Waves = new List<KitchenWave>();
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText =
                @"
                CREATE TABLE IF NOT EXISTS kitchenMonies (
                    id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    world INTEGER NOT NULL,
                    amount INTEGER NOT NULL,
                    UNIQUE(name, world)
                );
                ";
                command.ExecuteNonQuery();
            }
        }

        public void ChangeWorld(int? newWorld)
        {
            if (newWorld == CurrentWorld)
            {
                return;
            }

            UpdatePlayersToDb();

            CurrentWorld = newWorld;
            ResetInternal();

            if (CurrentWorld == null)
            {
                return;
            }

            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText =
                @"
                SELECT name, amount FROM kitchenMonies WHERE world = $world;
                ";
                command.Parameters.AddWithValue("$world", CurrentWorld);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var chatterName = reader.GetString(0);
                        var amount = reader.GetInt32(1);

                        Credits[chatterName] = amount;
                    }
                }
            }
            Console.WriteLine($"(Terraria Kitchen) Credits Loaded from DB ({Credits.Count})");
        }

        public void UpdatePlayersToDb()
        {
            if (CurrentWorld == null)
            {
                return;
            }
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                lock (Credits)
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var balance in Credits)
                        {
                            var command = connection.CreateCommand();
                            command.CommandText = @"INSERT OR REPLACE INTO kitchenMonies (name, world, amount) 
                            VALUES($name, $world, $amount);";
                            command.Parameters.AddWithValue("$name", balance.Key);
                            command.Parameters.AddWithValue("$world", CurrentWorld);
                            command.Parameters.AddWithValue("$amount", balance.Value);
                            command.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
            }
            Console.WriteLine("(Terraria Kitchen) Credits Saved to DB...");
        }

        public void Income(object? state)
        {
            lock (Credits)
            {
                foreach (var player in CurrentChatters)
                {
                    if (!Credits.ContainsKey(player))
                    {
                        Credits[player] = Config.StartingMoney;
                    }

                    Credits[player] += Config.Income;
                }
            }
        }

        public void ModBalance(string chatter, int amount)
        {
            lock (Credits)
            {
                if (!Credits.ContainsKey(chatter))
                {
                    Credits[chatter] = Config.StartingMoney;
                }

                Credits[chatter] += amount;

                if (Credits[chatter] < 0)
                {
                    Credits[chatter] = 0;
                }
            }
        }

        public int GetBalance(string chatter)
        {
            if (!Credits.ContainsKey(chatter))
            {
                Credits[chatter] = Config.StartingMoney;
            }

            return Credits[chatter] - Pools.Sum(p => p.Contributions.Where(c => c.Key == chatter).Sum(c => c.Value)) - Waves.Sum(p => p.Contributions.Where(c => c.Key == chatter).Sum(c => c.Value));
        }

        public KitchenPool? GetPool(int PoolIndex)
        {
            return Pools.FirstOrDefault(p => p.Index == PoolIndex);
        }

        public int ContributePool(string chatter, int amount, int PoolIndex)
        {
            if (GetPool(PoolIndex) is KitchenPool pool)
            {
                var amt = pool.Contribute(chatter, amount);

                return amt;
            }
            return 0;
        }

        public void ResolvePoolPayment(KitchenPool pool)
        {
            lock (Pools)
            {
                Pools.Remove(pool);

                foreach (var contribution in pool.Contributions)
                {
                    if (!Credits.ContainsKey(contribution.Key))
                    {
                        Credits[contribution.Key] = Config.StartingMoney - contribution.Value;
                    }
                    else
                    {
                        Credits[contribution.Key] -= contribution.Value;
                    }
                }
            }
        }

        public void ResolveWavePayment(KitchenWave wave)
        {
            lock (Waves)
            {
                Waves.Remove(wave);

                foreach (var contribution in wave.Contributions)
                {
                    if (!Credits.ContainsKey(contribution.Key))
                    {
                        Credits[contribution.Key] = Config.StartingMoney - contribution.Value;
                    }
                    else
                    {
                        Credits[contribution.Key] -= contribution.Value;
                    }
                }
            }
        }

        public void ResetInternal()
        {
            lock (Credits)
            {
                Credits.Clear();
            }
            lock (Pools)
            {
                KitchenPool.PoolIdx = 0;
                Pools.Clear();
            }
        }

        public void ResetChatterBalance()
        {
            ResetInternal();
            if (CurrentWorld == null)
            {
                return;
            }
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = @"DELETE FROM kitchenMonies WHERE world = $world;";
                command.Parameters.AddWithValue("$world", CurrentWorld);
                command.ExecuteNonQuery();
            }
        }

        public bool Give(string chatter, string target, string amount)
        {
            lock (Credits)
            {
                if (!Credits.ContainsKey(chatter))
                {
                    Credits[chatter] = Config.StartingMoney;
                }

                if (!CurrentChatters.Contains(target))
                {
                    return false;
                }

                if (!Credits.ContainsKey(target))
                {
                    Credits[target] = Config.StartingMoney;
                }

                if (int.TryParse(amount, out var amt) && Credits[chatter] >= amt)
                {
                    Credits[chatter] -= amt;
                    Credits[target] += amt;
                    return true;
                }

                return false;
            }
        }
    }
}
