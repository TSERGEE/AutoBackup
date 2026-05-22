using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoBackup
{
    public class MainForm : Form
    {
        private DataGridView logGrid;
        private Button backupNowBtn, restoreBtn, settingsBtn, pauseBtn, exportLogBtn, clearLogBtn;
        private Label statusLabel, nextRunLabel;
        private System.Windows.Forms.Timer refreshTimer;
        private ContextMenuStrip logContextMenu;

        public MainForm()
        {
            InitializeComponents();
            LoadLog();
            StartRefreshTimer();
            UpdateNextRunInfo();
            this.FormClosing += (s, e) => refreshTimer?.Stop();
        }

        private void InitializeComponents()
        {
            this.Text = "Авторезервное копирование";
            this.Size = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(800, 500);

            // --- Верхняя панель с кнопками ---
            backupNowBtn = new Button { Text = "▶ Запустить сейчас", Location = new Point(10, 10), Width = 130 };
            restoreBtn = new Button { Text = "↺ Восстановить...", Location = new Point(150, 10), Width = 120 };
            settingsBtn = new Button { Text = "⚙ Настройки", Location = new Point(280, 10), Width = 100 };
            pauseBtn = new Button { Text = "⏸ Пауза", Location = new Point(390, 10), Width = 90, BackColor = Color.LightYellow };
            var verifyBtn = new Button { Text = "✓ Проверить бэкап", Location = new Point(490, 10), Width = 120 };
            exportLogBtn = new Button { Text = "💾 Экспорт лога", Location = new Point(620, 10), Width = 110 };
            clearLogBtn = new Button { Text = "🗑 Очистить лог", Location = new Point(740, 10), Width = 100, BackColor = Color.LightCoral };

            // Статусная строка
            statusLabel = new Label
            {
                Text = "Готов",
                Location = new Point(10, 45),
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.Green
            };
            nextRunLabel = new Label
            {
                Text = "Следующий запуск: не запланирован",
                Location = new Point(10, 65),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            // Журнал операций
            logGrid = new DataGridView
            {
                Location = new Point(10, 95),
                Width = 860,
                Height = 440,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            logGrid.Columns.Add("Timestamp", "Дата/время");
            logGrid.Columns.Add("Operation", "Операция");
            logGrid.Columns.Add("Details", "Подробности");
            logGrid.Columns.Add("Status", "Статус");
            logGrid.Columns["Timestamp"].Width = 140;
            logGrid.Columns["Operation"].Width = 120;
            logGrid.Columns["Status"].Width = 80;

            // Контекстное меню для лога
            logContextMenu = new ContextMenuStrip();
            logContextMenu.Items.Add("Копировать строку", null, (s, e) => CopyLogRow());
            logContextMenu.Items.Add("Копировать всё", null, (s, e) => CopyAllLog());
            logGrid.ContextMenuStrip = logContextMenu;

            // Обработчики событий
            backupNowBtn.Click += async (s, e) => { await BackupManager.RunBackup(true); LoadLog(); UpdateNextRunInfo(); };
            restoreBtn.Click += (s, e) => new RestoreForm().ShowDialog();
            settingsBtn.Click += (s, e) => { new SettingsForm().ShowDialog(); UpdateNextRunInfo(); };
            pauseBtn.Click += TogglePause;
            verifyBtn.Click += VerifyBackup;
            exportLogBtn.Click += ExportLog;
            clearLogBtn.Click += ClearLog;

            this.Controls.AddRange(new Control[] { backupNowBtn, restoreBtn, settingsBtn, pauseBtn, verifyBtn, exportLogBtn, clearLogBtn, statusLabel, nextRunLabel, logGrid });
        }

        private void StartRefreshTimer()
        {
            refreshTimer = new System.Windows.Forms.Timer { Interval = 30000 }; // 30 секунд
            refreshTimer.Tick += (s, e) => { LoadLog(); UpdateNextRunInfo(); };
            refreshTimer.Start();
        }

        private void LoadLog()
        {
            logGrid.Rows.Clear();
            foreach (var entry in Logger.GetRecentEntries(200))
                logGrid.Rows.Add(entry.Timestamp, entry.Operation, entry.Details, entry.Status);
            // Обновляем индикатор статуса
            var lastEntry = Logger.GetRecentEntries(1).FirstOrDefault();
            if (lastEntry != null && lastEntry.Status == "Error")
                statusLabel.ForeColor = Color.Red;
            else if (lastEntry != null && lastEntry.Status == "Warning")
                statusLabel.ForeColor = Color.Orange;
            else
                statusLabel.ForeColor = Color.Green;
        }

        private void UpdateNextRunInfo()
        {
            string schedule = Config.Current.BackupSchedule;
            if (schedule == "Never")
                nextRunLabel.Text = "Автоматическое копирование отключено";
            else
            {
                // Простое отображение расписания (более точный расчёт можно добавить)
                string nextInfo = schedule switch
                {
                    "Daily" => "ежедневно",
                    "Weekly" => "еженедельно по понедельникам",
                    "OnSystemStart" => "при каждом запуске системы",
                    "OnIdle" => $"при простое системы ({Config.Current.IdleMinutes} мин.)",
                    _ => "не задано"
                };
                nextRunLabel.Text = $"Расписание: {nextInfo}";
            }
        }

        private void TogglePause(object sender, EventArgs e)
        {
            if (BackupManager.IsPaused())
            {
                BackupManager.Resume();
                pauseBtn.Text = "⏸ Пауза";
                pauseBtn.BackColor = Color.LightYellow;
                statusLabel.Text = "Автоматический режим активен";
                Logger.Log("User", "Автоматическое резервное копирование возобновлено", "Info");
            }
            else
            {
                using (var dialog = new Form
                {
                    Text = "Пауза",
                    Size = new Size(300, 150),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterParent
                })
                {
                    var numUpDown = new NumericUpDown { Location = new Point(10, 20), Minimum = 1, Maximum = 1440, Value = 60 };
                    var lbl = new Label { Text = "Приостановить на (минут):", Location = new Point(10, 0), AutoSize = true };
                    var btnOk = new Button { Text = "OK", Location = new Point(100, 60), DialogResult = DialogResult.OK };
                    var btnCancel = new Button { Text = "Отмена", Location = new Point(180, 60), DialogResult = DialogResult.Cancel };
                    dialog.Controls.AddRange(new Control[] { lbl, numUpDown, btnOk, btnCancel });
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        int minutes = (int)numUpDown.Value;
                        BackupManager.PauseFor(minutes);
                        pauseBtn.Text = "▶ Возобновить";
                        pauseBtn.BackColor = Color.LightGreen;
                        statusLabel.Text = $"Пауза до {DateTime.Now.AddMinutes(minutes):HH:mm}";
                        Logger.Log("User", $"Автоматическое копирование приостановлено на {minutes} минут", "Info");
                    }
                }
            }
            UpdateNextRunInfo();
        }

        private async void VerifyBackup(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с резервной копией (например, Backups\\Full_2025-01-20_15-30)";
                if (dialog.ShowDialog() != DialogResult.OK) return;
                string backupFolder = dialog.SelectedPath;
                if (!Directory.Exists(backupFolder))
                {
                    MessageBox.Show("Папка не найдена.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var mismatches = new List<string>();
                int totalFiles = 0, verified = 0;
                statusLabel.Text = "Проверка целостности...";
                statusLabel.ForeColor = Color.Blue;
                await Task.Run(() =>
                {
                    foreach (string sourceRoot in Config.Current.SourceFolders)
                    {
                        string sourceDirName = Path.GetFileName(sourceRoot);
                        string backupSourceDir = Path.Combine(backupFolder, sourceDirName);
                        if (!Directory.Exists(backupSourceDir)) continue;
                        VerifyDirectory(sourceRoot, backupSourceDir, mismatches, ref totalFiles, ref verified);
                    }
                });
                statusLabel.Text = "Готов";
                statusLabel.ForeColor = Color.Green;
                string resultMsg = $"Проверено файлов: {verified}\nНесовпадений: {mismatches.Count}";
                if (mismatches.Count > 0)
                    resultMsg += "\n\nОшибки:\n" + string.Join("\n", mismatches.Take(20));
                MessageBox.Show(resultMsg, "Результат верификации", MessageBoxButtons.OK,
                    mismatches.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        private void VerifyDirectory(string originalDir, string backupDir, List<string> mismatches, ref int totalFiles, ref int verified)
        {
            foreach (string file in Directory.GetFiles(originalDir))
            {
                string fileName = Path.GetFileName(file);
                if (ShouldExclude(fileName)) continue;
                totalFiles++;
                string backupFile = Path.Combine(backupDir, fileName);
                if (!File.Exists(backupFile))
                {
                    mismatches.Add($"Отсутствует в бэкапе: {file}");
                    continue;
                }
                if (!CompareFileHash(file, backupFile))
                    mismatches.Add($"Контрольная сумма не совпадает: {file}");
                verified++;
            }
            foreach (string dir in Directory.GetDirectories(originalDir))
            {
                string backupSubDir = Path.Combine(backupDir, Path.GetFileName(dir));
                if (Directory.Exists(backupSubDir))
                    VerifyDirectory(dir, backupSubDir, mismatches, ref totalFiles, ref verified);
                else
                    mismatches.Add($"Отсутствует папка в бэкапе: {dir}");
            }
        }

        private bool CompareFileHash(string file1, string file2)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs1 = File.OpenRead(file1))
            using (var fs2 = File.OpenRead(file2))
            {
                byte[] hash1 = sha.ComputeHash(fs1);
                byte[] hash2 = sha.ComputeHash(fs2);
                return StructuralComparisons.StructuralEqualityComparer.Equals(hash1, hash2);
            }
        }

        private bool ShouldExclude(string fileName)
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

        private void CopyLogRow()
        {
            if (logGrid.CurrentRow != null)
            {
                string row = string.Join("\t", logGrid.CurrentRow.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString() ?? ""));
                Clipboard.SetText(row);
            }
        }

        private void CopyAllLog()
        {
            var lines = new List<string>();
            foreach (DataGridViewRow row in logGrid.Rows)
            {
                lines.Add(string.Join("\t", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString() ?? "")));
            }
            Clipboard.SetText(string.Join("\n", lines));
        }

        private void ExportLog(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV файлы|*.csv";
                sfd.FileName = $"backup_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var lines = new List<string>();
                    lines.Add("Дата/время;Операция;Подробности;Статус");
                    foreach (DataGridViewRow row in logGrid.Rows)
                    {
                        lines.Add(string.Join(";", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString() ?? "").Select(v => v.Contains(';') ? $"\"{v}\"" : v)));
                    }
                    File.WriteAllLines(sfd.FileName, lines);
                    MessageBox.Show($"Лог экспортирован в {sfd.FileName}", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ClearLog(object sender, EventArgs e)
        {
            if (MessageBox.Show("Очистить все записи журнала?", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Logger.ClearLog(); 
                LoadLog();
            }
        }
    }
}