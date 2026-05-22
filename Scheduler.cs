using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoBackup
{
    public static class Scheduler
    {
        private static System.Threading.Timer timer;
        private static DateTime lastCheck;

        public static void Start()
        {
            lastCheck = DateTime.MinValue;
            timer = new System.Threading.Timer(_ => CheckSchedule(), null, 0, 60000); // каждую минуту
        }

        private static void CheckSchedule()
        {
            if (BackupManager.IsPaused()) return;
            DateTime now = DateTime.Now;
            if (Config.Current.BackupSchedule == "Daily" && (now - lastCheck).TotalHours >= 24)
            {
                lastCheck = now;
                _ = BackupManager.RunBackup(false);
            }
            else if (Config.Current.BackupSchedule == "Weekly" && now.DayOfWeek == DayOfWeek.Monday && (now - lastCheck).TotalDays >= 7)
            {
                lastCheck = now;
                _ = BackupManager.RunBackup(false);
            }
            else if (Config.Current.BackupSchedule == "OnSystemStart")
            {
                // при запуске программы уже вызвано – дополнительно не нужно
            }
            else if (Config.Current.BackupSchedule == "OnIdle")
            {
                if (IsSystemIdle(Config.Current.IdleMinutes) && (now - lastCheck).TotalMinutes >= Config.Current.IdleMinutes)
                {
                    lastCheck = now;
                    _ = BackupManager.RunBackup(false);
                }
            }
        }

        private static bool IsSystemIdle(int idleMinutes)
        {
            var lastInput = GetLastInputTime();
            return (DateTime.Now - lastInput).TotalMinutes >= idleMinutes;
        }

        private static DateTime GetLastInputTime()
        {
            // упрощённо – всегда false для краткости, реализуйте через GetLastInputInfo
            return DateTime.Now;
        }
    }
}