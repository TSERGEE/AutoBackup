using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AutoBackup
{
    public class Config
    {
        public static Config Current { get; private set; } = new Config();
        private static string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoBackup", "config.json");

        public bool FirstRun { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public List<string> SourceFolders { get; set; } = new List<string>();
        public string DestinationFolder { get; set; } = "";
        public string BackupSchedule { get; set; } = "Daily"; // Daily, Weekly, OnSystemStart, OnIdle
        public int IdleMinutes { get; set; } = 10;
        public int VersionRetentionDays { get; set; } = 30;
        public List<string> ExcludeMasks { get; set; } = new List<string> { "thumbs.db", "desktop.ini", "~$*", "*.tmp", "*.log" };
        public bool LimitSpeed { get; set; } = false;
        public long MaxBytesPerSecond { get; set; } = 10 * 1024 * 1024; // 10 MB/s
        public bool PauseOnBattery { get; set; } = true;
        public int RetryCount { get; set; } = 5;
        public int RetryInitialDelaySec { get; set; } = 1;
        public int FullBackupIntervalDays { get; set; } = 7;
        public int KeepFullBackupsCount { get; set; } = 4;
        public static void Load()
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                Current = JsonConvert.DeserializeObject<Config>(json);
            }
            else
            {
                Current = new Config();
                Save();
            }
        }

        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));
            string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }

        public static void Export(string filePath) => File.WriteAllText(filePath, JsonConvert.SerializeObject(Current, Formatting.Indented));
        public static void Import(string filePath)
        {
            var json = File.ReadAllText(filePath);
            Current = JsonConvert.DeserializeObject<Config>(json);
            Save();
        }
    }
}