using AutoBackup.Models;
using AutoBackup.Utils;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoBackup.Services
{
    public static class BackupManager
    {
        private static bool isRunning = false;
        private static DateTime pauseUntil = DateTime.MinValue;
        private static CancellationTokenSource _currentCts;
        public static event Action<int, string> ProgressChanged;
        public static event Action<string> StatusChanged;
        public static event Action<string, string> Notification;
        public static void Initialize()
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }
        private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (Config.Current.PauseOnBattery && e.Mode == PowerModes.StatusChange)
            {
                if (SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline)
                {
                    Logger.Log("System", "Ноутбук перешёл на батарею, резервное копирование приостановлено.", "Warning");
                    pauseUntil = DateTime.MaxValue;
                    StatusChanged?.Invoke("Пауза (батарея)");
                }
                else
                {
                    pauseUntil = DateTime.MinValue;
                    Logger.Log("System", "Питание от сети восстановлено.", "Info");
                    StatusChanged?.Invoke("Готов");
                }
            }
        }
        public static bool IsRunning() => isRunning;
        public static bool IsPaused() => pauseUntil > DateTime.Now;
        public static void PauseFor(int minutes)
        {
            pauseUntil = DateTime.Now.AddMinutes(minutes);
            Logger.Log("User", $"Пауза на {minutes} минут.", "Info");
            StatusChanged?.Invoke($"Пауза до {pauseUntil:HH:mm}");
        }
        public static void Resume()
        {
            pauseUntil = DateTime.MinValue;
            StatusChanged?.Invoke("Готов");
        }
        public static void CancelCurrentOperation()
        {
            _currentCts?.Cancel();
        }
        public static async Task RunBackup(bool isManual = false, CancellationToken externalToken = default)
        {
            if (isRunning)
            {
                Logger.Log("Backup", "Бэкап уже выполняется. Пропуск.", "Warning");
                Notification?.Invoke("Резервное копирование", "Уже выполняется");
                return;
            }

            if (!isManual && IsPaused())
            {
                Logger.Log("Backup", "Автоматический бэкап отложен (пауза/батарея).", "Info");
                return;
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            _currentCts = cts;
            isRunning = true;
            StatusChanged?.Invoke("Резервное копирование...");
            ProgressChanged?.Invoke(0, "Подготовка...");

            try
            {
                await RunBackupInternal(isManual, cts.Token);
            }
            finally
            {
                isRunning = false;
                _currentCts = null;
                StatusChanged?.Invoke("Готов");
                ProgressChanged?.Invoke(100, "Завершено");
            }
        }
        private static async Task RunBackupInternal(bool isManual, CancellationToken token)
        {
            // 1. Проверка прав на запись
            string destFolder = Config.Current.DestinationFolder;
            if (!TestWriteAccess(destFolder))
                throw new UnauthorizedAccessException($"Нет прав на запись в {destFolder}");
            string backupsRoot = Path.Combine(destFolder, "Backups");
            Directory.CreateDirectory(backupsRoot);
            // 2. Оценка требуемого места
            long requiredSpace = await EstimateRequiredSpace(backupsRoot, token);
            long availableSpace = GetAvailableFreeSpace(destFolder);
            long minFreeSpace = (long)(availableSpace * Config.Current.MinFreeSpacePercent / 100.0);
            if (availableSpace < requiredSpace + minFreeSpace)
            {
                string msg = $"Недостаточно места. Требуется: {FormatSize(requiredSpace)}, доступно: {FormatSize(availableSpace)}";
                Logger.Log("Backup", msg, "Error");
                Notification?.Invoke("Ошибка", msg);
                throw new Exception(msg);
            }
            // 3. Определяем тип бэкапа
            bool needFull = NeedFullBackup(backupsRoot);
            string backupType = needFull ? "Full" : "Inc";
            // 4. Временная папка
            string tempFolderName = $"{backupType}_temp_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            string tempBackupPath = Path.Combine(backupsRoot, tempFolderName);
            Directory.CreateDirectory(tempBackupPath);
            try
            {
                // 5. Загружаем эталонный метафайл (для инкремента)
                BackupMeta referenceMeta = null;
                if (!needFull)
                {
                    var lastBackup = GetLastAnyBackup(backupsRoot);
                    if (lastBackup != null)
                    {
                        referenceMeta = BackupMeta.Load(Path.Combine(lastBackup.Value.Path, "backup_meta.json"));
                        if (referenceMeta == null) needFull = true;
                    }
                    else needFull = true;
                }
                // 6. Создаём новый метафайл
                BackupMeta newMeta;
                if (needFull)
                {
                    newMeta = new BackupMeta
                    {
                        BackupTime = DateTime.Now,
                        BackupType = "Full",
                        FullBackupRef = null,
                        Files = new List<FileEntry>()
                    };
                }
                else
                {
                    string json = JsonConvert.SerializeObject(referenceMeta);
                    newMeta = JsonConvert.DeserializeObject<BackupMeta>(json);
                    newMeta.BackupTime = DateTime.Now;
                    newMeta.BackupType = "Inc";
                    newMeta.FullBackupRef = GetLastFullBackupPath(backupsRoot);
                }
                // 7. Копирование файлов
                int errors = 0;
                int warnings = 0;
                var errorList = new List<string>();
                var warningList = new List<string>();
                // Собираем все файлы для копирования
                var filesToCopy = new List<(string source, string dest, string relativePath)>();
                foreach (string srcFolder in Config.Current.SourceFolders)
                {
                    if (!Directory.Exists(srcFolder))
                    {
                        warningList.Add($"Папка-источник не найдена: {srcFolder}");
                        warnings++;
                        continue;
                    }
                    string relativeRoot = Path.GetFileName(srcFolder);
                    if (string.IsNullOrEmpty(relativeRoot))
                    {
                        relativeRoot = "Drive_" + srcFolder.TrimEnd('\\').TrimEnd(':');
                    }
                    string destSub = Path.Combine(tempBackupPath, relativeRoot);
                    Directory.CreateDirectory(destSub);
                    if (needFull)
                    {
                        CollectFilesToCopy(srcFolder, destSub, relativeRoot, filesToCopy, token);
                    }
                    else
                    {
                        CollectChangedFiles(srcFolder, destSub, relativeRoot, referenceMeta, filesToCopy, token);
                    }
                }
                int totalFiles = filesToCopy.Count;
                if (totalFiles == 0)
                {
                    Logger.Log("Backup", "Нет файлов для копирования.", "Info");
                    // Удаляем временную папку, так как она пустая
                    try
                    {
                        if (Directory.Exists(tempBackupPath))
                            Directory.Delete(tempBackupPath, true);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Cleanup empty temp folder", ex);
                    }

                    if (!needFull)
                    {
                        Logger.Log("Backup", "Инкремент не создаётся, так как нет изменений.", "Info");
                        Notification?.Invoke("Резервное копирование", "Изменений не обнаружено.");
                        return;
                    }
                    else
                    {
                        Logger.Log("Backup", "Полный бэкап не содержит файлов. Все источники пусты или полностью исключены.", "Warning");
                        return;
                    }
                }
                await ParallelCopyFilesAsync(filesToCopy, newMeta, (src, dest) =>
                {
                    int processed = Interlocked.Increment(ref filesToCopyProcessed);
                    int percent = processed * 100 / totalFiles;
                    ProgressChanged?.Invoke(percent, Path.GetFileName(src));
                }, errorList, token);
                // 8. Сохраняем метафайл во временную папку
                BackupMeta.Save(Path.Combine(tempBackupPath, "backup_meta.json"), newMeta);
                // 9. Атомарное перемещение
                string finalFolderName = $"{backupType}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
                string finalBackupPath = Path.Combine(backupsRoot, finalFolderName);
                Directory.Move(tempBackupPath, finalBackupPath);
                // 10. Очистка старых бэкапов
                CleanupOldBackups();
                // 11. Автоматическая верификация
                if (Config.Current.VerifyAfterBackup)
                {
                    StatusChanged?.Invoke("Верификация...");
                    var mismatches = await VerifyBackupIntegrityAsync(finalBackupPath, token);
                    if (mismatches.Any())
                    {
                        string errorMsg = $"Верификация выявила {mismatches.Count} ошибок: " + string.Join("; ", mismatches.Take(5));
                        Logger.Log("Verify", errorMsg, "Warning");
                        Notification?.Invoke("Верификация завершена", $"Обнаружено {mismatches.Count} расхождений");
                    }
                    else
                    {
                        Logger.Log("Verify", "Верификация пройдена успешно", "Info");
                    }
                }
                // 12. Логируем результат
                if (errors > 0)
                {
                    string errorMsg = string.Join("; ", errorList.Take(5));
                    Logger.Log("Backup", $"Завершено с {errors} ошибками: {errorMsg}", "Error");
                    Notification?.Invoke("Ошибка резервного копирования", errorMsg);
                }
                else if (warnings > 0)
                {
                    string warnMsg = string.Join("; ", warningList.Take(5));
                    Logger.Log("Backup", $"Завершено с {warnings} предупреждениями: {warnMsg}", "Warning");
                    Notification?.Invoke("Резервное копирование завершено с предупреждениями", warnMsg);
                }
                else
                {
                    Logger.Log("Backup", $"Успешно скопировано {totalFiles} файлов", "Success");
                    Notification?.Invoke("Резервное копирование завершено", $"Скопировано {totalFiles} файлов");
                }
            }
            catch (Exception)
            {
                // При ошибке или отмене удаляем временную папку
                if (Directory.Exists(tempBackupPath))
                {
                    try { Directory.Delete(tempBackupPath, true); }
                    catch (Exception ex) { Logger.LogError("Cleanup temp folder", ex); }
                }
                throw;
            }
        }
        private static int filesToCopyProcessed = 0;
        private static async Task ParallelCopyFilesAsync(
            List<(string source, string dest, string relativePath)> filesToCopy,
            BackupMeta newMeta,
            Action<string, string> onFileCopied,
            List<string> errors,
            CancellationToken token)
        {
            int total = filesToCopy.Count;
            int processed = 0;
            var semaphore = new SemaphoreSlim(Config.Current.MaxParallelCopies);
            var tasks = filesToCopy.Select(async file =>
            {
                await semaphore.WaitAsync(token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    if (await CopyFileWithRetry(file.source, file.dest, token))
                    {
                        var entry = CreateFileEntry(file.source, file.relativePath);
                        lock (newMeta.Files) newMeta.Files.Add(entry);
                        onFileCopied?.Invoke(file.source, file.dest);
                    }
                    else
                    {
                        lock (errors) errors.Add($"Не удалось скопировать {file.source}");
                    }
                }
                finally
                {
                    semaphore.Release();
                    Interlocked.Increment(ref processed);
                    int percent = processed * 100 / total;
                    ProgressChanged?.Invoke(percent, Path.GetFileName(file.source));
                }
            }).ToArray();

            await Task.WhenAll(tasks);
        }
        private static void CollectFilesToCopy(string srcDir, string destDir, string relativePrefix,
            List<(string, string, string)> files, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            foreach (string file in Directory.GetFiles(srcDir))
            {
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                string destFile = Path.Combine(destDir, fileName);
                string relativePath = Path.Combine(relativePrefix, fileName);
                files.Add((file, destFile, relativePath));
            }
            foreach (string dir in Directory.GetDirectories(srcDir))
            {
                string subDirName = Path.GetFileName(dir);
                string destSub = Path.Combine(destDir, subDirName);
                Directory.CreateDirectory(destSub);
                string newRel = Path.Combine(relativePrefix, subDirName);
                CollectFilesToCopy(dir, destSub, newRel, files, token);
            }
        }
        private static void CollectChangedFiles(string srcDir, string destDir, string relativePrefix,
            BackupMeta referenceMeta, List<(string, string, string)> files, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            foreach (string file in Directory.GetFiles(srcDir))
            {
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                string relativePath = Path.Combine(relativePrefix, fileName);
                var refEntry = referenceMeta?.Files.FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                if (IsFileChanged(file, refEntry))
                {
                    string destFile = Path.Combine(destDir, fileName);
                    files.Add((file, destFile, relativePath));
                }
            }
            foreach (string dir in Directory.GetDirectories(srcDir))
            {
                string subDirName = Path.GetFileName(dir);
                string destSub = Path.Combine(destDir, subDirName);
                Directory.CreateDirectory(destSub);
                string newRel = Path.Combine(relativePrefix, subDirName);
                CollectChangedFiles(dir, destSub, newRel, referenceMeta, files, token);
            }
        }
        public static async Task<List<string>> VerifyBackupIntegrityAsync(string backupFolderPath,
            CancellationToken token = default)
        {
            var mismatches = new List<string>();
            string metaFile = Path.Combine(backupFolderPath, "backup_meta.json");
            if (!File.Exists(metaFile))
                throw new FileNotFoundException("Метафайл не найден");

            BackupMeta meta = BackupMeta.Load(metaFile);
            if (meta == null)
                throw new Exception("Не удалось загрузить метафайл");

            // Для Inc-бэкапа – проверяем только файлы, которые хранятся в этой папке (изменённые)
            // Для Full – проверяем все файлы из этой папки
            var fileMap = new Dictionary<string, string>();
            CollectFilesFromBackup(backupFolderPath, "", fileMap);

            // Исключаем сам метафайл из проверки
            fileMap.Remove("backup_meta.json");

            // Дополнительно: если это Inc и нет файлов, то просто успех
            if (meta.BackupType == "Inc" && fileMap.Count == 0)
            {
                Logger.Log("Verify", "Инкрементальный бэкап не содержит файлов (пустая проверка). Верификация успешна.", "Info");
                return mismatches;
            }

            int total = fileMap.Count;
            int checkedCount = 0;
            int percent = 0;

            foreach (var kvp in fileMap)
            {
                token.ThrowIfCancellationRequested();
                string relativePath = kvp.Key;
                string backupFile = kvp.Value;

                string originalFile = FindOriginalFile(relativePath);
                if (string.IsNullOrEmpty(originalFile) || !File.Exists(originalFile))
                {
                    mismatches.Add($"[Missing original] {relativePath}");
                }
                else if (!CompareFiles(originalFile, backupFile, meta.BackupType == "Full"))
                {
                    mismatches.Add($"[Hash mismatch] {relativePath}");
                }

                int newPercent = Interlocked.Increment(ref checkedCount) * 100 / total;
                if (newPercent != percent)
                {
                    percent = newPercent;
                    ProgressChanged?.Invoke(percent, relativePath);
                }
                await Task.Yield();
            }
            return mismatches;
        }
        private static void CollectFilesFromBackup(string backupRoot, string currentRelative,
            Dictionary<string, string> fileMap, bool overwriteExisting = false)
        {
            foreach (string file in Directory.GetFiles(backupRoot))
            {
                string fileName = Path.GetFileName(file);
                if (fileName == "backup_meta.json") continue; // пропускаем метафайл
                if (ShouldExclude(fileName)) continue;
                string rel = string.IsNullOrEmpty(currentRelative) ? fileName : Path.Combine(currentRelative, fileName);
                if (!overwriteExisting && fileMap.ContainsKey(rel)) continue;
                fileMap[rel] = file;
            }
            foreach (string dir in Directory.GetDirectories(backupRoot))
            {
                string dirName = Path.GetFileName(dir);
                string newRel = string.IsNullOrEmpty(currentRelative) ? dirName : Path.Combine(currentRelative, dirName);
                CollectFilesFromBackup(dir, newRel, fileMap, overwriteExisting);
            }
        }
        public static string FindOriginalFile(string relativePath)
        {
            string[] parts = relativePath.Split(Path.DirectorySeparatorChar);
            if (parts.Length == 0) return null;
            string sourceRootName = parts[0];
            string relativeInside = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(1));
            var sourceFolder = Config.Current.SourceFolders
                .FirstOrDefault(f => Path.GetFileName(f).Equals(sourceRootName, StringComparison.OrdinalIgnoreCase));
            if (sourceFolder == null) return null;
            return Path.Combine(sourceFolder, relativeInside);
        }
        private static bool CompareFiles(string file1, string file2, bool useFullHash = false)
        {
            if (useFullHash && !Config.Current.UseFastHash)
            {
                return CompareFileHashFull(file1, file2);
            }
            else
            {
                var fi1 = new FileInfo(file1);
                var fi2 = new FileInfo(file2);
                if (fi1.Length != fi2.Length) return false;
                if (fi1.LastWriteTimeUtc != fi2.LastWriteTimeUtc) return false;
                string hash1 = ComputeSimpleHash(file1);
                string hash2 = ComputeSimpleHash(file2);
                return hash1 == hash2;
            }
        }
        private static bool CompareFileHashFull(string file1, string file2)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs1 = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            using (var fs2 = new FileStream(file2, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
            {
                byte[] hash1 = sha.ComputeHash(fs1);
                byte[] hash2 = sha.ComputeHash(fs2);
                return StructuralComparisons.StructuralEqualityComparer.Equals(hash1, hash2);
            }
        }
        private static async Task<bool> CopyFileWithRetry(string source, string destination, CancellationToken token)
        {
            int retries = Config.Current.RetryCount;
            int delaySec = Config.Current.RetryInitialDelaySec;
            for (int i = 0; i <= retries; i++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                    using (var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        if (Config.Current.LimitSpeed)
                            await ThrottledCopyAsync(sourceStream, destStream, Config.Current.MaxBytesPerSecond, token);
                        else
                            await sourceStream.CopyToAsync(destStream, 81920, token);
                    }
                    return true;
                }
                catch (Exception ex) when (i < retries)
                {
                    Logger.Log("Retry", $"Ошибка копирования {source}, попытка {i + 1}: {ex.Message}", "Warning");
                    await Task.Delay(delaySec * 1000, token);
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
        private static async Task ThrottledCopyAsync(Stream source, Stream dest, long maxBytesPerSecond,
            CancellationToken token)
        {
            byte[] buffer = new byte[81920];
            long totalRead = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                int read = await source.ReadAsync(buffer, 0, buffer.Length, token);
                if (read == 0) break;
                await dest.WriteAsync(buffer, 0, read, token);
                totalRead += read;
                if (maxBytesPerSecond > 0)
                {
                    double elapsed = stopwatch.Elapsed.TotalSeconds;
                    double expectedTime = totalRead / (double)maxBytesPerSecond;
                    if (elapsed < expectedTime)
                        await Task.Delay((int)((expectedTime - elapsed) * 1000), token);
                }
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
        private static bool TestWriteAccess(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string testFile = Path.Combine(folder, "write_test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch { return false; }
        }
        private static bool NeedFullBackup(string backupsRoot)
        {
            var lastFull = GetLastFullBackup(backupsRoot);
            if (lastFull == null) return true;
            return (DateTime.Now - lastFull.Value.Meta.BackupTime).TotalDays >= Config.Current.FullBackupIntervalDays;
        }
        private static (string Path, BackupMeta Meta)? GetLastFullBackup(string backupsRoot)
        {
            var dirs = Directory.GetDirectories(backupsRoot, "Full_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null && x.Meta.BackupType == "Full")
                .OrderByDescending(x => x.Meta.BackupTime)
                .FirstOrDefault();
            if (dirs == null) return null;
            return (dirs.Path, dirs.Meta);
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
            var full = GetLastFullBackup(backupsRoot);
            return full?.Path;
        }
        private static async Task<long> EstimateRequiredSpace(string backupsRoot, CancellationToken token)
        {
            bool needFull = NeedFullBackup(backupsRoot);
            if (needFull)
            {
                long total = 0;
                foreach (var src in Config.Current.SourceFolders)
                {
                    if (!Directory.Exists(src)) continue;
                    total += await Task.Run(() => GetDirectorySize(src, token), token);
                }
                return total;
            }
            else
            {
                var lastBackup = GetLastAnyBackup(backupsRoot);
                if (lastBackup == null) return 0;
                var refMeta = BackupMeta.Load(Path.Combine(lastBackup.Value.Path, "backup_meta.json"));
                if (refMeta == null) return 0;

                long changedSize = 0;
                foreach (var src in Config.Current.SourceFolders)
                {
                    if (!Directory.Exists(src)) continue;
                    changedSize += await Task.Run(() => GetChangedFilesSize(src, "", refMeta, token), token);
                }
                return changedSize;
            }
        }
        private static long GetDirectorySize(string path, CancellationToken token)
        {
            long size = 0;
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                token.ThrowIfCancellationRequested();
                if (ShouldExclude(Path.GetFileName(file))) continue;
                try { size += new FileInfo(file).Length; } catch { }
            }
            return size;
        }
        private static long GetChangedFilesSize(string dir, string relativePrefix, BackupMeta refMeta,
            CancellationToken token)
        {
            long size = 0;
            foreach (var file in Directory.GetFiles(dir))
            {
                token.ThrowIfCancellationRequested();
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                string relativePath = Path.Combine(relativePrefix, fileName);
                var refEntry = refMeta.Files.FirstOrDefault(f => f.RelativePath.Equals(relativePath, StringComparison.OrdinalIgnoreCase));
                if (IsFileChanged(file, refEntry))
                {
                    try { size += new FileInfo(file).Length; } catch { }
                }
            }
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                string subName = Path.GetFileName(subDir);
                size += GetChangedFilesSize(subDir, Path.Combine(relativePrefix, subName), refMeta, token);
            }
            return size;
        }
        private static long GetAvailableFreeSpace(string path)
        {
            DriveInfo drive = new DriveInfo(Path.GetPathRoot(path));
            return drive.AvailableFreeSpace;
        }
        private static string FormatSize(long bytes)
        {
            string[] sizes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
            return $"{len:0.##} {sizes[order]}";
        }
        public static void CleanupOldBackups()
        {
            string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
            if (!Directory.Exists(backupsRoot)) return;

            var fullBackups = Directory.GetDirectories(backupsRoot, "Full_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null && x.Meta.BackupType == "Full")
                .OrderByDescending(x => x.Meta.BackupTime)
                .ToList();

            int keepCount = Config.Current.KeepFullBackupsCount;
            if (fullBackups.Count <= keepCount) return;

            var toDelete = fullBackups.Skip(keepCount);
            foreach (var full in toDelete)
            {
                var incToDelete = Directory.GetDirectories(backupsRoot, "Inc_*")
                    .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                    .Where(x => x.Meta != null && x.Meta.FullBackupRef == full.Path)
                    .ToList();

                foreach (var inc in incToDelete)
                {
                    try { Directory.Delete(inc.Path, true); }
                    catch (Exception ex) { Logger.LogError($"Ошибка удаления {inc.Path}", ex); }
                }

                try { Directory.Delete(full.Path, true); }
                catch (Exception ex) { Logger.LogError($"Ошибка удаления {full.Path}", ex); }
            }
        }
        public static async Task RestoreFromBackup(string backupFolderPath, string targetPath, bool overwrite,
            CancellationToken token = default)
        {
            string metaFile = Path.Combine(backupFolderPath, "backup_meta.json");
            BackupMeta targetMeta = BackupMeta.Load(metaFile);
            if (targetMeta == null) throw new Exception("Метафайл не найден или повреждён.");

            var fileMap = new Dictionary<string, string>();
            if (targetMeta.BackupType == "Full")
            {
                CollectFilesFromBackup(backupFolderPath, "", fileMap);
            }
            else if (targetMeta.BackupType == "Inc")
            {
                if (string.IsNullOrEmpty(targetMeta.FullBackupRef) || !Directory.Exists(targetMeta.FullBackupRef))
                    throw new Exception("Полный бэкап, на который ссылается инкремент, не найден.");
                CollectFilesFromBackup(targetMeta.FullBackupRef, "", fileMap);
                CollectFilesFromBackup(backupFolderPath, "", fileMap, true);
            }
            else throw new Exception("Неизвестный тип бэкапа");

            int restored = 0;
            int errors = 0;
            int skipped = 0;
            int total = fileMap.Count;
            int percent = 0;

            foreach (var kvp in fileMap)
            {
                token.ThrowIfCancellationRequested();
                string relativePath = kvp.Key;
                string sourceFile = kvp.Value;

                string destFile;
                if (string.IsNullOrEmpty(targetPath))
                {
                    // Восстановление в оригинальные папки
                    destFile = FindOriginalFile(relativePath);
                    if (string.IsNullOrEmpty(destFile))
                    {
                        skipped++;
                        Logger.Log("Restore", $"Не удалось определить оригинальный путь для {relativePath}", "Warning");
                        continue;
                    }
                }
                else
                {
                    destFile = Path.Combine(targetPath, relativePath);
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                    if (File.Exists(destFile) && !overwrite)
                    {
                        skipped++;
                        continue;
                    }
                    File.Copy(sourceFile, destFile, overwrite);
                    restored++;
                }
                catch (Exception ex)
                {
                    errors++;
                    Logger.LogError($"Восстановление {relativePath}", ex);
                }

                int newPercent = (restored + errors + skipped) * 100 / total;
                if (newPercent != percent)
                {
                    percent = newPercent;
                    ProgressChanged?.Invoke(percent, relativePath);
                }
                await Task.Delay(1, token);
            }

            string resultMsg = $"Восстановлено: {restored}, пропущено: {skipped}, ошибок: {errors}";
            Notification?.Invoke("Восстановление завершено", resultMsg);
            Logger.Log("Restore", resultMsg, errors > 0 ? "Error" : "Info");
        }
        public static (DateTime? LastBackupTime, int TotalFiles, int TotalErrors) GetLastBackupInfo()
        {
            string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
            if (!Directory.Exists(backupsRoot)) return (null, 0, 0);
            var last = GetLastAnyBackup(backupsRoot);
            if (last == null) return (null, 0, 0);
            var meta = last.Value.Meta;
            return (meta.BackupTime, meta.Files.Count, 0);
        }
        public static long GetTotalBackupSize()
        {
            string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
            if (!Directory.Exists(backupsRoot)) return 0;
            long size = 0;
            var files = Directory.GetFiles(backupsRoot, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try { size += new FileInfo(file).Length; }
                catch { }
            }
            return size;
        }
        public static void RunManualBackup() => Task.Run(() => RunBackup(true));
    }
}