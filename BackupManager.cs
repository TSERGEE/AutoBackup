using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace AutoBackup
{
    public static class BackupManager
    {
        private static bool manualRun = false;
        private static DateTime pauseUntil = DateTime.MinValue;

        public static void Initialize()
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        private static void OnPowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
        {
            if (Config.Current.PauseOnBattery && e.Mode == Microsoft.Win32.PowerModes.StatusChange)
            {
                if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline)
                {
                    Logger.Log("System", "Ноутбук перешёл на батарею, резервное копирование приостановлено.", "Warning");
                    pauseUntil = DateTime.MaxValue;
                }
                else
                {
                    pauseUntil = DateTime.MinValue;
                    Logger.Log("System", "Питание от сети восстановлено, бэкап будет выполняться по расписанию.", "Info");
                }
            }
        }

        public static bool IsPaused() => pauseUntil > DateTime.Now;
        public static void PauseFor(int minutes) { pauseUntil = DateTime.Now.AddMinutes(minutes); Logger.Log("User", $"Пауза на {minutes} минут.", "Info"); }
        public static void Resume() => pauseUntil = DateTime.MinValue;

        public static async Task RunBackup(bool isManual = false)
        {
            manualRun = isManual;
            if (IsPaused() && !manualRun)
            {
                Logger.Log("Backup", "Автоматический бэкап отложен (пауза/батарея).", "Info");
                return;
            }
            // Проверка прав на запись
            string testFile = Path.Combine(Config.Current.DestinationFolder, "write_test.tmp");
            try
            {
                Directory.CreateDirectory(Config.Current.DestinationFolder);
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Log("Backup", $"Нет прав на запись в папку {Config.Current.DestinationFolder}. Бэкап отменён.", "Error");
                ShowNotification("Ошибка доступа", "Нет прав на запись в выбранную папку для бэкапов.");
                return;
            }
            Logger.Log("Backup", "Начало резервного копирования", "Info");
            UpdateTrayStatus("Копирование...");

            try
            {
                string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
                Directory.CreateDirectory(backupsRoot);

                // Определяем, нужен ли полный бэкап (по интервалу или отсутствию полных)
                bool needFull = NeedFullBackup(backupsRoot);
                BackupMeta referenceMeta = null; // метаданные последнего бэкапа (для сравнения)
                string referencePath = null;
                BackupMeta newMeta = null;

                if (!needFull)
                {
                    var lastBackup = GetLastAnyBackup(backupsRoot);
                    if (lastBackup != null)
                    {
                        referencePath = lastBackup.Value.Path;
                        referenceMeta = BackupMeta.Load(Path.Combine(referencePath, "backup_meta.json"));
                        if (referenceMeta == null)
                        {
                            Logger.Log("Backup", "Метафайл последнего бэкапа повреждён. Будет создан полный бэкап.", "Warning");
                            needFull = true;
                        }
                        else
                        {
                            // Создаём копию метаданных последнего бэкапа (будем обновлять изменённые файлы)
                            string json = JsonConvert.SerializeObject(referenceMeta);
                            newMeta = JsonConvert.DeserializeObject<BackupMeta>(json);
                            newMeta.BackupTime = DateTime.Now;
                            newMeta.BackupType = "Inc";
                            newMeta.FullBackupRef = GetLastFullBackupPath(backupsRoot);
                        }
                    }
                    else needFull = true;
                }

                if (needFull)
                {
                    newMeta = new BackupMeta
                    {
                        BackupTime = DateTime.Now,
                        BackupType = "Full",
                        FullBackupRef = null
                    };
                }

                string backupType = needFull ? "Full" : "Inc";
                string backupFolderName = $"{backupType}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                string backupFolderPath = Path.Combine(backupsRoot, backupFolderName);
                Directory.CreateDirectory(backupFolderPath);

                var copiedFiles = new List<string>();
                var errors = new List<string>();

                foreach (string srcFolder in Config.Current.SourceFolders)
                {
                    if (!Directory.Exists(srcFolder))
                    {
                        errors.Add($"Папка-источник не найдена: {srcFolder}");
                        continue;
                    }
                    string relativeRoot = Path.GetFileName(srcFolder);
                    string destSub = Path.Combine(backupFolderPath, relativeRoot);
                    Directory.CreateDirectory(destSub);

                    if (needFull)
                        await CopyDirectoryFullAsync(srcFolder, destSub, relativeRoot, newMeta, copiedFiles, errors);
                    else
                        await CopyDirectoryIncAsync(srcFolder, destSub, relativeRoot, referenceMeta, newMeta, copiedFiles, errors);
                }

                // Сохраняем метафайл (всегда полный список файлов)
                BackupMeta.Save(Path.Combine(backupFolderPath, "backup_meta.json"), newMeta);
                CleanupOldBackups(backupsRoot);

                if (errors.Count > 0)
                {
                    string errorMsg = string.Join("; ", errors.Take(5));
                    Logger.Log("Backup", $"Завершено с ошибками: {errorMsg}", "Warning");
                    UpdateTrayStatus("Ошибка");
                    ShowNotification("Ошибка резервного копирования", errorMsg);
                }
                else
                {
                    Logger.Log("Backup", $"Успешно скопировано {copiedFiles.Count} файлов", "Success");
                    UpdateTrayStatus("Успех");
                    ShowNotification("Резервное копирование завершено", $"Скопировано {copiedFiles.Count} файлов");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Backup", ex);
                UpdateTrayStatus("Ошибка");
                ShowNotification("Ошибка резервного копирования", ex.Message);
            }
            finally { UpdateTrayStatus("Ожидание"); }
        }

        private static bool NeedFullBackup(string backupsRoot)
        {
            var lastFull = GetLastFullBackup(backupsRoot);
            if (lastFull == null) return true;
            return (DateTime.Now - lastFull.Value.Meta.BackupTime).TotalDays >= Config.Current.FullBackupIntervalDays;
        }

        private static (string Path, BackupMeta Meta)? GetLastFullBackup(string backupsRoot)
        {
            var fullDirs = Directory.GetDirectories(backupsRoot, "Full_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null && x.Meta.BackupType == "Full")
                .OrderByDescending(x => x.Meta.BackupTime)
                .FirstOrDefault();
            if (fullDirs == null) return null;
            return (fullDirs.Path, fullDirs.Meta);
        }

        private static (string Path, BackupMeta Meta)? GetLastAnyBackup(string backupsRoot)
        {
            var dirs = Directory.GetDirectories(backupsRoot, "*_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null)
                .OrderByDescending(x => x.Meta.BackupTime)
                .FirstOrDefault();
            if (dirs == null) return null;
            return (dirs.Path, dirs.Meta);
        }

        private static string GetLastFullBackupPath(string backupsRoot)
        {
            var full = Directory.GetDirectories(backupsRoot, "Full_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null)
                .OrderByDescending(x => x.Meta.BackupTime)
                .FirstOrDefault();
            return full?.Path;
        }

        private static async Task CopyDirectoryFullAsync(string srcDir, string destDir, string relativePrefix, BackupMeta meta, List<string> copiedFiles, List<string> errors)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(srcDir))
            {
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                string destFile = Path.Combine(destDir, fileName);
                if (await CopyFileWithRetry(file, destFile))
                {
                    copiedFiles.Add(file);
                    meta.Files.Add(CreateFileEntry(file, Path.Combine(relativePrefix, fileName)));
                }
                else errors.Add($"Не удалось скопировать {file}");
            }
            foreach (string dir in Directory.GetDirectories(srcDir))
            {
                string subDirName = Path.GetFileName(dir);
                string destSub = Path.Combine(destDir, subDirName);
                await CopyDirectoryFullAsync(dir, destSub, Path.Combine(relativePrefix, subDirName), meta, copiedFiles, errors);
            }
        }

        private static async Task CopyDirectoryIncAsync(string srcDir, string destDir, string relativePrefix, BackupMeta referenceMeta, BackupMeta newMeta, List<string> copiedFiles, List<string> errors)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(srcDir))
            {
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                string relativePath = Path.Combine(relativePrefix, fileName);
                // Ищем запись в эталонном метафайле (последний бэкап)
                var refEntry = referenceMeta?.Files.FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                if (IsFileChanged(file, refEntry))
                {
                    string destFile = Path.Combine(destDir, fileName);
                    if (await CopyFileWithRetry(file, destFile))
                    {
                        copiedFiles.Add(file);
                        // Обновляем запись в новом метафайле (копия эталонного)
                        var existingEntry = newMeta.Files.FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                        if (existingEntry != null)
                        {
                            existingEntry.Size = new FileInfo(file).Length;
                            existingEntry.LastWriteTime = new FileInfo(file).LastWriteTimeUtc;
                            existingEntry.Hash = ComputeSimpleHash(file);
                        }
                        else
                        {
                            // Файл новый – добавляем запись
                            newMeta.Files.Add(CreateFileEntry(file, relativePath));
                        }
                    }
                    else errors.Add($"Не удалось скопировать {file}");
                }
                // Если файл не изменился, запись в newMeta уже есть (скопирована из referenceMeta) – ничего не делаем
            }
            foreach (string dir in Directory.GetDirectories(srcDir))
            {
                string subDirName = Path.GetFileName(dir);
                string destSub = Path.Combine(destDir, subDirName);
                await CopyDirectoryIncAsync(dir, destSub, Path.Combine(relativePrefix, subDirName), referenceMeta, newMeta, copiedFiles, errors);
            }
        }

        private static FileEntry CreateFileEntry(string fullPath, string relativePath)
        {
            var fi = new FileInfo(fullPath);
            return new FileEntry
            {
                RelativePath = relativePath,
                Size = fi.Length,
                LastWriteTime = fi.LastWriteTimeUtc,
                Hash = ComputeSimpleHash(fullPath)
            };
        }

        private static string ComputeSimpleHash(string filePath)
        {
            var fi = new FileInfo(filePath);
            return $"{fi.Length}_{fi.LastWriteTimeUtc.Ticks}";
        }

        private static bool IsFileChanged(string filePath, FileEntry refEntry)
        {
            if (refEntry == null) return true;
            var fi = new FileInfo(filePath);
            if (fi.Length != refEntry.Size) return true;
            if (fi.LastWriteTimeUtc != refEntry.LastWriteTime) return true;
            return ComputeSimpleHash(filePath) != refEntry.Hash;
        }

        private static void CleanupOldBackups(string backupsRoot)
        {
            // Удаляем старые полные бэкапы вместе со всеми зависимыми инкрементальными
            var fullBackups = Directory.GetDirectories(backupsRoot, "Full_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null)
                .OrderByDescending(x => x.Meta.BackupTime)
                .ToList();

            int keepCount = Config.Current.KeepFullBackupsCount;
            if (fullBackups.Count > keepCount)
            {
                var toDelete = fullBackups.Skip(keepCount);
                foreach (var fb in toDelete)
                {
                    // Удаляем все инкрементальные бэкапы, которые ссылаются на этот полный (или созданы после него)
                    var incToDelete = Directory.GetDirectories(backupsRoot, "Inc_*")
                        .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                        .Where(x => x.Meta != null && x.Meta.FullBackupRef == fb.Path)
                        .ToList();
                    foreach (var inc in incToDelete)
                    {
                        try { Directory.Delete(inc.Path, true); } catch { }
                    }
                    try { Directory.Delete(fb.Path, true); } catch { }
                }
            }
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

        private static bool ShouldExclude(string fileName)
        {
            foreach (string mask in Config.Current.ExcludeMasks)
            {
                if (mask.StartsWith("*.") && fileName.EndsWith(mask.Substring(1))) return true;
                if (mask.Contains('*'))
                {
                    if (mask.StartsWith("*") && fileName.EndsWith(mask.Substring(1))) return true;
                    if (mask.EndsWith("*") && fileName.StartsWith(mask.Substring(0, mask.Length - 1))) return true;
                }
                if (fileName.Equals(mask, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static async void RunManualBackup() => await RunBackup(true);

        private static void UpdateTrayStatus(string status) => TrayIconHelper.UpdateStatus(status);
        private static void ShowNotification(string title, string text) => TrayIconHelper.ShowBalloon(title, text);
    }

    public static class TrayIconHelper
    {
        public static NotifyIcon TrayIcon;
        public static void UpdateStatus(string status) { /* реализация по желанию */ }
        public static void ShowBalloon(string title, string text) => TrayIcon?.ShowBalloonTip(3000, title, text, ToolTipIcon.Info);
    }
}