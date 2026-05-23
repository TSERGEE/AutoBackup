using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AutoBackup.Models;
using AutoBackup.Services;
using Guna.UI2.WinForms;
using NCrontab;

namespace AutoBackup.Controls
{
    public class SettingsControl : UserControl
    {
        // Компоненты для папок-источников
        private ListBox sourcesList;
        private Button addSourceBtn, removeSourceBtn;

        // Остальные настройки
        private NumericUpDown retentionDays;
        private TextBox excludeMasksBox;
        private CheckBox limitSpeedChk;
        private NumericUpDown speedLimit;
        private CheckBox pauseBatteryChk;

        // Новые элементы
        private Guna2TextBox cronBox;
        private Label nextRunPreviewLabel;
        private NumericUpDown parallelCopies;
        private CheckBox verifyAfterBackupChk;
        private CheckBox useFastHashChk;
        private NumericUpDown minFreeSpacePercent;
        private NumericUpDown fullBackupIntervalDays;
        private NumericUpDown keepFullBackupsCount;

        private Panel scrollPanel;

        public SettingsControl()
        {
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(24, 24, 27);
            this.Padding = new Padding(15);

            // Панель с прокруткой
            scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent
            };

            int y = 10;

            // ========== Группа: Папки-источники ==========
            var groupSources = new GroupBox
            {
                Text = "📁 Папки для резервного копирования",
                Location = new Point(10, y),
                Size = new Size(800, 190),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            sourcesList = new ListBox
            {
                Location = new Point(10, 25),
                Width = 540,
                Height = 120,
                SelectionMode = SelectionMode.MultiExtended,
                BackColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White
            };
            addSourceBtn = new Button
            {
                Text = "➕ Добавить папку",
                Location = new Point(560, 25),
                Width = 120,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            removeSourceBtn = new Button
            {
                Text = "➖ Удалить выбранные",
                Location = new Point(560, 65),
                Width = 120,
                BackColor = Color.Firebrick,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            var sourcesHint = new Label
            {
                Text = "Выберите папки, которые нужно резервировать.",
                Location = new Point(10, 155),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F)
            };
            addSourceBtn.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK && !sourcesList.Items.Contains(fbd.SelectedPath))
                        sourcesList.Items.Add(fbd.SelectedPath);
                }
            };
            removeSourceBtn.Click += (s, e) =>
            {
                var selected = sourcesList.SelectedItems.Cast<string>().ToList();
                foreach (var item in selected) sourcesList.Items.Remove(item);
            };
            groupSources.Controls.AddRange(new Control[] { sourcesList, addSourceBtn, removeSourceBtn, sourcesHint });
            scrollPanel.Controls.Add(groupSources);
            y += 200;

            // ========== Группа: Управление версиями ==========
            var groupVersions = new GroupBox
            {
                Text = "💾 Управление версиями",
                Location = new Point(10, y),
                Size = new Size(700, 110),
                ForeColor = Color.White
            };
            var lblRetention = new Label { Text = "Хранить версии (дней):", Location = new Point(10, 25), AutoSize = true, ForeColor = Color.White };
            retentionDays = new NumericUpDown { Location = new Point(250, 23), Width = 80, Minimum = 1, Maximum = 365, Value = 30 };
            var lblDays = new Label { Text = "дней", Location = new Point(240, 25), AutoSize = true, ForeColor = Color.White };
            var lblFullInterval = new Label { Text = "Полный бэкап каждые (дней):", Location = new Point(10, 55), AutoSize = true, ForeColor = Color.White };
            fullBackupIntervalDays = new NumericUpDown { Location = new Point(250, 53), Width = 80, Minimum = 1, Maximum = 365, Value = 7 };
            var lblKeepFull = new Label { Text = "Хранить полных бэкапов (шт):", Location = new Point(10, 85), AutoSize = true, ForeColor = Color.White };
            keepFullBackupsCount = new NumericUpDown { Location = new Point(250, 83), Width = 80, Minimum = 1, Maximum = 30, Value = 3 };
            groupVersions.Controls.AddRange(new Control[] { lblRetention, retentionDays, lblDays, lblFullInterval, fullBackupIntervalDays, lblKeepFull, keepFullBackupsCount });
            scrollPanel.Controls.Add(groupVersions);
            y += 120;

            // ========== Группа: Исключения файлов ==========
            var groupExclude = new GroupBox
            {
                Text = "🚫 Исключения (маски файлов)",
                Location = new Point(10, y),
                Size = new Size(700, 110),
                ForeColor = Color.White
            };
            var lblExcludeHint = new Label
            {
                Text = "Указывайте через запятую, например: *.tmp, *.log, thumbs.db",
                Location = new Point(10, 20),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F)
            };
            excludeMasksBox = new TextBox
            {
                Location = new Point(10, 45),
                Width = 670,
                Height = 50,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            groupExclude.Controls.AddRange(new Control[] { lblExcludeHint, excludeMasksBox });
            scrollPanel.Controls.Add(groupExclude);
            y += 120;

            // ========== Группа: Производительность ==========
            var groupPerformance = new GroupBox
            {
                Text = "⚡ Производительность",
                Location = new Point(10, y),
                Size = new Size(700, 130),
                ForeColor = Color.White
            };
            limitSpeedChk = new CheckBox
            {
                Text = "Ограничить скорость копирования (МБ/с):",
                Location = new Point(10, 25),
                AutoSize = true,
                ForeColor = Color.White
            };
            speedLimit = new NumericUpDown
            {
                Location = new Point(300, 23),
                Width = 60,
                Minimum = 1,
                Maximum = 1000,
                Value = 10,
                Enabled = false
            };
            limitSpeedChk.CheckedChanged += (s, e) => speedLimit.Enabled = limitSpeedChk.Checked;

            var parallelLabel = new Label
            {
                Text = "Максимум параллельных копий:",
                Location = new Point(10, 55),
                AutoSize = true,
                ForeColor = Color.White
            };
            parallelCopies = new NumericUpDown
            {
                Location = new Point(250, 53),
                Width = 60,
                Minimum = 1,
                Maximum = 16,
                Value = Config.Current.MaxParallelCopies
            };

            groupPerformance.Controls.AddRange(new Control[] { limitSpeedChk, speedLimit, parallelLabel, parallelCopies });
            scrollPanel.Controls.Add(groupPerformance);
            y += 140;

            // ========== Группа: Энергосбережение ==========
            var groupPower = new GroupBox
            {
                Text = "🔋 Энергосбережение",
                Location = new Point(10, y),
                Size = new Size(700, 60),
                ForeColor = Color.White
            };
            pauseBatteryChk = new CheckBox
            {
                Text = "Приостанавливать резервное копирование при работе от батареи",
                Location = new Point(10, 25),
                AutoSize = true,
                ForeColor = Color.White
            };
            groupPower.Controls.Add(pauseBatteryChk);
            scrollPanel.Controls.Add(groupPower);
            y += 70;

            // ========== Группа: Расширенное расписание (cron) ==========
            var groupCron = new GroupBox
            {
                Text = "⏰ Расширенное расписание (cron-выражение)",
                Location = new Point(10, y),
                Size = new Size(700, 100),
                ForeColor = Color.White
            };
            var cronLabel = new Label
            {
                Text = "Примеры: 0 2 * * * (ежедневно в 2:00), */30 * * * * (каждые 30 мин), 0 9 * * 1 (каждый понедельник в 9:00)",
                Location = new Point(10, 20),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F)
            };
            cronBox = new Guna2TextBox
            {
                PlaceholderText = "0 2 * * *",
                Location = new Point(10, 45),
                Size = new Size(350, 36),
                FillColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                BorderRadius = 8
            };
            cronBox.TextChanged += UpdateCronPreview;
            nextRunPreviewLabel = new Label
            {
                Location = new Point(370, 50),
                AutoSize = true,
                ForeColor = Color.LightGray,
                Font = new Font("Segoe UI", 8F)
            };
            groupCron.Controls.Add(cronLabel);
            groupCron.Controls.Add(cronBox);
            groupCron.Controls.Add(nextRunPreviewLabel);
            scrollPanel.Controls.Add(groupCron);
            y += 110;

            // ========== Группа: Проверка дискового места ==========
            var groupSpace = new GroupBox
            {
                Text = "💾 Проверка дискового места",
                Location = new Point(10, y),
                Size = new Size(700, 70),
                ForeColor = Color.White
            };
            var minSpaceLabel = new Label
            {
                Text = "Минимальный свободный процент перед бэкапом:",
                Location = new Point(10, 30),
                AutoSize = true,
                ForeColor = Color.White
            };
            minFreeSpacePercent = new NumericUpDown
            {
                Location = new Point(330, 28),
                Width = 60,
                Minimum = 1,
                Maximum = 50,
                Value = Config.Current.MinFreeSpacePercent
            };
            var percentLabel = new Label { Text = "%", Location = new Point(345, 30), AutoSize = true, ForeColor = Color.White };
            groupSpace.Controls.Add(minSpaceLabel);
            groupSpace.Controls.Add(minFreeSpacePercent);
            groupSpace.Controls.Add(percentLabel);
            scrollPanel.Controls.Add(groupSpace);
            y += 80;

            // ========== Группа: Верификация ==========
            var groupVerify = new GroupBox
            {
                Text = "🔒 Верификация целостности",
                Location = new Point(10, y),
                Size = new Size(700, 80),
                ForeColor = Color.White
            };
            verifyAfterBackupChk = new CheckBox
            {
                Text = "Автоматически проверять целостность после бэкапа",
                Location = new Point(10, 25),
                AutoSize = true,
                ForeColor = Color.White,
                Checked = Config.Current.VerifyAfterBackup
            };
            useFastHashChk = new CheckBox
            {
                Text = "Использовать полный SHA256 (медленнее, но надёжнее)",
                Location = new Point(10, 50),
                AutoSize = true,
                ForeColor = Color.White,
                Checked = !Config.Current.UseFastHash
            };
            groupVerify.Controls.Add(verifyAfterBackupChk);
            groupVerify.Controls.Add(useFastHashChk);
            scrollPanel.Controls.Add(groupVerify);
            y += 90;

            // Кнопки внизу (не прокручиваются)
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = Color.Transparent };
            var saveBtn = new Button
            {
                Text = "💾 Сохранить",
                Location = new Point(520, 10),
                Size = new Size(100, 32),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            var cancelBtn = new Button
            {
                Text = "Отмена",
                Location = new Point(630, 10),
                Size = new Size(80, 32),
                BackColor = Color.FromArgb(60, 60, 70),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveBtn.Click += SaveSettings;
            cancelBtn.Click += (s, e) => LoadSettings();

            bottomPanel.Controls.Add(saveBtn);
            bottomPanel.Controls.Add(cancelBtn);

            this.Controls.Add(scrollPanel);
            this.Controls.Add(bottomPanel);
        }

        private void LoadSettings()
        {
            // Источники
            sourcesList.Items.Clear();
            foreach (var folder in Config.Current.SourceFolders)
                sourcesList.Items.Add(folder);

            // Версии
            retentionDays.Value = Config.Current.VersionRetentionDays;
            fullBackupIntervalDays.Value = Config.Current.FullBackupIntervalDays;
            keepFullBackupsCount.Value = Config.Current.KeepFullBackupsCount;

            // Исключения
            excludeMasksBox.Text = string.Join(", ", Config.Current.ExcludeMasks);

            // Производительность
            limitSpeedChk.Checked = Config.Current.LimitSpeed;
            speedLimit.Value = (int)(Config.Current.MaxBytesPerSecond / 1024 / 1024);
            speedLimit.Enabled = Config.Current.LimitSpeed;
            parallelCopies.Value = Config.Current.MaxParallelCopies;

            // Энергосбережение
            pauseBatteryChk.Checked = Config.Current.PauseOnBattery;

            // Расписание
            cronBox.Text = Config.Current.BackupScheduleCron;
            UpdateCronPreview(null, null);

            // Место
            minFreeSpacePercent.Value = Config.Current.MinFreeSpacePercent;

            // Верификация
            verifyAfterBackupChk.Checked = Config.Current.VerifyAfterBackup;
            useFastHashChk.Checked = !Config.Current.UseFastHash;
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            var newSources = sourcesList.Items.Cast<string>().ToList();
            if (newSources.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну папку для резервного копирования.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Config.Current.SourceFolders = newSources;
            Config.Current.VersionRetentionDays = (int)retentionDays.Value;
            Config.Current.FullBackupIntervalDays = (int)fullBackupIntervalDays.Value;
            Config.Current.KeepFullBackupsCount = (int)keepFullBackupsCount.Value;

            Config.Current.ExcludeMasks = excludeMasksBox.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .ToList();

            Config.Current.LimitSpeed = limitSpeedChk.Checked;
            Config.Current.MaxBytesPerSecond = (long)speedLimit.Value * 1024 * 1024;
            Config.Current.MaxParallelCopies = (int)parallelCopies.Value;
            Config.Current.PauseOnBattery = pauseBatteryChk.Checked;

            Config.Current.BackupScheduleCron = cronBox.Text;
            Config.Current.MinFreeSpacePercent = (int)minFreeSpacePercent.Value;

            Config.Current.VerifyAfterBackup = verifyAfterBackupChk.Checked;
            Config.Current.UseFastHash = !useFastHashChk.Checked;

            Config.Save();

            // Обновляем планировщик (если используется)
            SchedulerService.UpdateSchedule();

            MessageBox.Show("Настройки сохранены.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateCronPreview(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(cronBox.Text))
                    throw new Exception();
                var schedule = CrontabSchedule.Parse(cronBox.Text);
                var next = schedule.GetNextOccurrence(DateTime.Now);
                nextRunPreviewLabel.Text = $"Следующий запуск: {next:yyyy-MM-dd HH:mm:ss}";
                nextRunPreviewLabel.ForeColor = Color.LightGreen;
            }
            catch
            {
                nextRunPreviewLabel.Text = "Ошибка в cron-выражении";
                nextRunPreviewLabel.ForeColor = Color.Firebrick;
            }
        }
    }
}