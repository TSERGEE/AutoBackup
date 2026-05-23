using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AutoBackup.Models
{
    public class Config
    {
        private static string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoBackup", "config.json");

        public static Config Current { get; private set; } = new Config();

        // ========== СУЩЕСТВУЮЩИЕ ПОЛЯ (сохранены) ==========
        public bool FirstRun { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public List<string> SourceFolders { get; set; } = new List<string>();
        public string DestinationFolder { get; set; } = "";
        public string BackupSchedule { get; set; } = "Daily"; // устаревшее, для совместимости
        public int IdleMinutes { get; set; } = 10;
        public int VersionRetentionDays { get; set; } = 30;
        public List<string> ExcludeMasks { get; set; } = new List<string> { "thumbs.db", "desktop.ini", "~$*", "*.tmp", "*.log" };
        public bool LimitSpeed { get; set; } = false;
        public long MaxBytesPerSecond { get; set; } = 10 * 1024 * 1024; // 10 МБ/с
        public bool PauseOnBattery { get; set; } = true;
        public int RetryCount { get; set; } = 5;
        public int RetryInitialDelaySec { get; set; } = 1;
        public int FullBackupIntervalDays { get; set; } = 7;
        public int KeepFullBackupsCount { get; set; } = 4;

        // ========== НОВЫЕ ПОЛЯ ==========
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public string BackupScheduleCron { get; set; } = "0 2 * * *";     // cron-выражение (ежедневно в 2:00)
        public DateTime? NextScheduledRun { get; set; } = null;           // следующий плановый запуск (для отображения)
        public int MaxParallelCopies { get; set; } = 4;                   // количество параллельных копирований
        public bool VerifyAfterBackup { get; set; } = false;              // автоматическая верификация после бэкапа
        public bool UseFastHash { get; set; } = true;                     // true = быстрый хеш (размер+дата), false = SHA256
        public int MinFreeSpacePercent { get; set; } = 10;                // минимальный свободный процент на диске перед бэкапом

        // ========== МЕТОДЫ ЗАГРУЗКИ/СОХРАНЕНИЯ ==========
        public static void Load()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    Current = JsonConvert.DeserializeObject<Config>(json) ?? new Config();

                    // МИГРАЦИЯ старых настроек расписания на cron (если поле cron пустое или не задано)
                    if (string.IsNullOrWhiteSpace(Current.BackupScheduleCron))
                    {
                        Current.BackupScheduleCron = Current.BackupSchedule switch
                        {
                            "Daily" => "0 2 * * *",
                            "Weekly" => "0 2 * * 0",        // воскресенье в 2:00
                            "OnSystemStart" => "0 2 * * *",  // нет прямого аналога, ставим ежедневно
                            "OnIdle" => "*/5 * * * *",       // каждые 5 минут (проверка idle внутри)
                            _ => "0 2 * * *"
                        };
                        Save(); // сохраняем обновлённый конфиг
                    }
                }
                catch
                {
                    Current = new Config();
                }
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

        public static void Export(string filePath)
        {
            string json = JsonConvert.SerializeObject(Current, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public static void Import(string filePath)
        {
            var json = File.ReadAllText(filePath);
            Current = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            Save();
        }
    }
}