using System;
using System.Threading;
using System.Threading.Tasks;
using AutoBackup.Models;
using AutoBackup.Utils;
using NCrontab;

namespace AutoBackup.Services
{
    public static class SchedulerService
    {
        private static System.Threading.Timer _timer;  // явное указание
        private static CrontabSchedule _currentSchedule;
        private static DateTime _nextRun;

        public static void Initialize()
        {
            UpdateSchedule();
            StartTimer();
        }

        public static void UpdateSchedule()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Config.Current.BackupScheduleCron))
                {
                    _currentSchedule = null;
                    Config.Current.NextScheduledRun = null;
                    Config.Save();
                    return;
                }

                _currentSchedule = CrontabSchedule.Parse(Config.Current.BackupScheduleCron);
                _nextRun = _currentSchedule.GetNextOccurrence(DateTime.Now);
                Config.Current.NextScheduledRun = _nextRun;
                Config.Save();
            }
            catch (Exception ex)
            {
                Logger.LogError("Scheduler", ex);
                _currentSchedule = null;
                Config.Current.NextScheduledRun = null;
            }
            finally
            {
                StartTimer();
            }
        }

        private static void StartTimer()
        {
            _timer?.Dispose();
            _timer = null;

            if (_currentSchedule == null) return;

            var now = DateTime.Now;
            if (_nextRun < now)
                _nextRun = _currentSchedule.GetNextOccurrence(now);

            var delay = _nextRun - now;
            if (delay.TotalMilliseconds <= 0) delay = TimeSpan.FromSeconds(10);

            _timer = new System.Threading.Timer(async _ =>
            {
                await OnScheduleTick();
            }, null, delay, Timeout.InfiniteTimeSpan);
        }

        private static async Task OnScheduleTick()
        {
            if (BackupManager.IsRunning()) return;

            await BackupManager.RunBackup(isManual: false);

            try
            {
                _nextRun = _currentSchedule.GetNextOccurrence(DateTime.Now);
                Config.Current.NextScheduledRun = _nextRun;
                Config.Save();
            }
            catch (Exception ex)
            {
                Logger.LogError("Scheduler next run", ex);
            }

            StartTimer();
        }
    }
}