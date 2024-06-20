using Microsoft.Data.Sqlite;

namespace TerrariaKitchen
{
    public class KitchenCreditsStore
    {
        private static readonly string connectionString = "Data Source=terrariaKitchen.db";

        private Dictionary<string, int> Credits;

        public HashSet<string> CurrentChatters { get; private set; }

        private int? CurrentWorld;

        private readonly KitchenConfig Config;

        public KitchenCreditsStore(KitchenConfig config)
        {
            Config = config;
            Credits = new Dictionary<string, int>();
            CurrentChatters = new HashSet<string>();
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
            Credits.Clear();

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
            Console.WriteLine("(Terraria Kitchen) Credits Saved to DB...");
        }

        public void Income(object? state)
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

        public void ModBalance(string chatter, int amount)
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

        public int GetBalance(string chatter)
        {
            if (!Credits.ContainsKey(chatter))
            {
                Credits[chatter] = Config.StartingMoney;
            }

            return Credits[chatter];
        }

        public void ResetChatterBalance()
        {
            Credits.Clear();

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
    }
}
