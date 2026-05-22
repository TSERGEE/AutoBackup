using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace AutoBackup
{
    public static class BackupManager
    {
        private static bool manualRun = false;
        private static DateTime pauseUntil = DateTime.MinValue;

        public static void Initialize()
        {
            // подписка на изменение источника питания
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        private static void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (Config.Current.PauseOnBattery && e.Mode == Microsoft.Win32.PowerModes.StatusChange)
            {
                if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline)
                {
                    Logger.Log("System", "Ноутбук перешёл на батарею, резервное копирование приостановлено.", "Warning");
                    pauseUntil = DateTime.MaxValue; // бесконечная пауза до восстановления питания
                }
                else
                {
                    pauseUntil = DateTime.MinValue;
                    Logger.Log("System", "Питание от сети восстановлено, бэкап будет выполняться по расписанию.", "Info");
                }
            }
        }

        public static bool IsPaused()
        {
            if (pauseUntil > DateTime.Now) return true;
            return false;
        }

        public static void PauseFor(int minutes)
        {
            pauseUntil = DateTime.Now.AddMinutes(minutes);
            Logger.Log("User", $"Резервное копирование приостановлено на {minutes} минут.", "Info");
        }

        public static void Resume() => pauseUntil = DateTime.MinValue;

        public static async Task RunBackup(bool isManual = false)
        {
            manualRun = isManual;
            if (IsPaused() && !manualRun)
            {
                Logger.Log("Backup", "Автоматический бэкап отложен из-за паузы или батареи.", "Info");
                return;
            }

            Logger.Log("Backup", "Начало резервного копирования", "Info");
            UpdateTrayStatus("Копирование...");

            try
            {
                string backupRoot = Path.Combine(Config.Current.DestinationFolder, "Backups", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(backupRoot);
                var copiedFiles = new List<string>();
                var errors = new List<string>();

                foreach (string srcFolder in Config.Current.SourceFolders)
                {
                    if (!Directory.Exists(srcFolder))
                    {
                        errors.Add($"Папка-источник не найдена: {srcFolder}");
                        continue;
                    }
                    string destSub = Path.Combine(backupRoot, Path.GetFileName(srcFolder));
                    await CopyDirectoryAsync(srcFolder, destSub, copiedFiles, errors);
                }

                // Очистка старых версий
                CleanupOldBackups();

                if (errors.Count > 0)
                {
                    string errorMsg = string.Join("; ", errors.Take(5));
                    Logger.Log("Backup", $"Завершено с ошибками: {errorMsg}", "Warning");
                    UpdateTrayStatus("Ошибка");
                    ShowNotification("Резервное копирование завершено с ошибками", errorMsg);
                }
                else
                {
                    Logger.Log("Backup", $"Успешно скопировано {copiedFiles.Count} файлов", "Success");
                    UpdateTrayStatus("Успех");
                    ShowNotification("Резервное копирование успешно завершено", $"Скопировано {copiedFiles.Count} файлов");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Backup", ex);
                UpdateTrayStatus("Ошибка");
                ShowNotification("Ошибка резервного копирования", ex.Message);
            }
        }

        private static async Task CopyDirectoryAsync(string srcDir, string destDir, List<string> copiedFiles, List<string> errors)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(srcDir))
            {
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                string destFile = Path.Combine(destDir, fileName);
                bool copied = await CopyFileWithRetry(file, destFile);
                if (copied) copiedFiles.Add(file);
                else errors.Add($"Не удалось скопировать {file}");
            }
            foreach (string subDir in Directory.GetDirectories(srcDir))
            {
                string destSub = Path.Combine(destDir, Path.GetFileName(subDir));
                await CopyDirectoryAsync(subDir, destSub, copiedFiles, errors);
            }
        }

        private static bool ShouldExclude(string fileName)
        {
            foreach (string mask in Config.Current.ExcludeMasks)
            {
                if (mask.StartsWith("*.") && fileName.EndsWith(mask.Substring(1))) return true;
                if (mask.Contains('*'))
                {
                    // простая поддержка wildcard – только * в конце или начале
                    if (mask.StartsWith("*") && fileName.EndsWith(mask.Substring(1))) return true;
                    if (mask.EndsWith("*") && fileName.StartsWith(mask.Substring(0, mask.Length - 1))) return true;
                }
                if (fileName.Equals(mask, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static async Task<bool> CopyFileWithRetry(string source, string destination)
        {
            int retries = Config.Current.RetryCount;
            int delaySec = Config.Current.RetryInitialDelaySec;
            for (int i = 0; i <= retries; i++)
            {
                try
                {
                    using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write))
                    {
                        if (Config.Current.LimitSpeed)
                            await ThrottledCopyAsync(sourceStream, destStream, Config.Current.MaxBytesPerSecond);
                        else
                            await sourceStream.CopyToAsync(destStream);
                    }
                    return true;
                }
                catch (Exception ex) when (i < retries)
                {
                    Logger.Log("Retry", $"Ошибка копирования {source}, попытка {i + 1}: {ex.Message}", "Warning");
                    await Task.Delay(delaySec * 1000);
                    delaySec *= 2;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Copy {source}", ex);
                    return false;
                }
            }
            return false;
        }

        private static async Task ThrottledCopyAsync(Stream source, Stream dest, long maxBytesPerSecond)
        {
            byte[] buffer = new byte[8192];
            long totalRead = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                int read = await source.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break;
                await dest.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (maxBytesPerSecond > 0)
                {
                    double elapsed = stopwatch.Elapsed.TotalSeconds;
                    double expectedTime = totalRead / (double)maxBytesPerSecond;
                    if (elapsed < expectedTime)
                        await Task.Delay((int)((expectedTime - elapsed) * 1000));
                }
            }
        }

        private static void CleanupOldBackups()
        {
            string backupsDir = Path.Combine(Config.Current.DestinationFolder, "Backups");
            if (!Directory.Exists(backupsDir)) return;
            var cutoff = DateTime.Now.AddDays(-Config.Current.VersionRetentionDays);
            foreach (var dir in Directory.GetDirectories(backupsDir))
            {
                if (Directory.GetCreationTime(dir) < cutoff)
                {
                    try { Directory.Delete(dir, true); }
                    catch (Exception ex) { Logger.LogError("Cleanup", ex); }
                }
            }
        }

        public static async void RunManualBackup()
        {
            await RunBackup(true);
        }

        private static void UpdateTrayStatus(string status)
        {
            // иконку можно менять через событие – просто для примера
            TrayIconHelper.UpdateStatus(status);
        }

        private static void ShowNotification(string title, string text)
        {
            TrayIconHelper.ShowBalloon(title, text);
        }
    }

    // вспомогательный класс для обновления трея из статического контекста
    public static class TrayIconHelper
    {
        public static NotifyIcon TrayIcon;
        public static void UpdateStatus(string status) { /* меняем иконку, если есть */ }
        public static void ShowBalloon(string title, string text)
        {
            TrayIcon?.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
        }
    }
}