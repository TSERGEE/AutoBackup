using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace AutoBackup.Utils
{
    public static class Logger
    {
        private static string dbPath;
        private static string connectionString;

        public static void Init()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoBackup");
            Directory.CreateDirectory(appData);
            dbPath = Path.Combine(appData, "backup_log.db");
            connectionString = $"Data Source={dbPath};Version=3;";
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string sql = @"CREATE TABLE IF NOT EXISTS Log (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Timestamp TEXT,
                                Operation TEXT,
                                Details TEXT,
                                Status TEXT
                              )";
                using (var cmd = new SQLiteCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        public static void Log(string operation, string details, string status = "Info")
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string sql = "INSERT INTO Log (Timestamp, Operation, Details, Status) VALUES (@ts, @op, @det, @stat)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ts", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@op", operation);
                    cmd.Parameters.AddWithValue("@det", details);
                    cmd.Parameters.AddWithValue("@stat", status);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void LogError(string operation, Exception ex)
        {
            Log(operation, ex.ToString(), "Error");
        }

        public static List<LogEntry> GetRecentEntries(int count = 100)
        {
            var list = new List<LogEntry>();
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                string sql = "SELECT Timestamp, Operation, Details, Status FROM Log ORDER BY Id DESC LIMIT @count";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@count", count);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new LogEntry
                            {
                                Timestamp = reader.GetString(0),
                                Operation = reader.GetString(1),
                                Details = reader.GetString(2),
                                Status = reader.GetString(3)
                            });
                        }
                    }
                }
            }
            return list;
        }
        public static void ClearLog()
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM Log", conn))
                    cmd.ExecuteNonQuery();
                // Опционально: вакуумирование базы данных
                using (var cmd = new SQLiteCommand("VACUUM", conn))
                    cmd.ExecuteNonQuery();
            }
        }
    }

    public class LogEntry
    {
        public string Timestamp { get; set; }
        public string Operation { get; set; }
        public string Details { get; set; }
        public string Status { get; set; }
    }
}