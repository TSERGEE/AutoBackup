using AutoBackup.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AutoBackup.Forms
{
    public class SettingsForm : Form
    {
        // Компоненты для источников
        private ListBox sourcesList;
        private Button addSourceBtn, removeSourceBtn;

        // Остальные настройки
        private NumericUpDown retentionDays;
        private TextBox excludeMasksBox;
        private CheckBox limitSpeedChk;
        private NumericUpDown speedLimit;
        private CheckBox pauseBatteryChk;
        private Button importBtn, exportBtn, saveBtn, cancelBtn;
        private Panel scrollPanel; // панель с прокруткой

        public SettingsForm()
        {
            InitializeComponents();
            LoadSettings();
        }

        private void InitializeComponents()
        {
            this.Text = "Настройки резервного копирования";
            this.Size = new System.Drawing.Size(650, 560);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Панель с автопрокруткой для всех настроек
            scrollPanel = new Panel
            {
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(615, 440),
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            int yOffset = 10;

            // ========== Группа: Папки для резервного копирования ==========
            var groupSources = new GroupBox { Text = "📁 Папки-источники", Location = new System.Drawing.Point(10, yOffset), Size = new System.Drawing.Size(580, 180) };
            sourcesList = new ListBox { Location = new System.Drawing.Point(10, 25), Width = 420, Height = 120, SelectionMode = SelectionMode.MultiExtended };
            addSourceBtn = new Button { Text = "➕ Добавить папку", Location = new System.Drawing.Point(440, 25), Width = 120 };
            removeSourceBtn = new Button { Text = "➖ Удалить выбранные", Location = new System.Drawing.Point(440, 60), Width = 120 };
            var sourcesHint = new Label { Text = "Выберите одну или несколько папок для резервного копирования.", Location = new System.Drawing.Point(10, 150), AutoSize = true, ForeColor = System.Drawing.Color.Gray, Font = new System.Drawing.Font("Segoe UI", 8) };

            addSourceBtn.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Выберите папку для резервного копирования";
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
            yOffset += 195;

            // ========== Группа: Управление версиями ==========
            var groupVersions = new GroupBox { Text = "💾 Управление версиями", Location = new System.Drawing.Point(10, yOffset), Size = new System.Drawing.Size(580, 80) };
            var lblRetention = new Label { Text = "Хранить версии (дней):", Location = new System.Drawing.Point(10, 30), AutoSize = true };
            retentionDays = new NumericUpDown { Location = new System.Drawing.Point(150, 28), Width = 80, Minimum = 1, Maximum = 365, Value = 30 };
            var lblDays = new Label { Text = "дней", Location = new System.Drawing.Point(240, 30), AutoSize = true };
            var lblHint = new Label { Text = "Старые версии будут автоматически удаляться", Location = new System.Drawing.Point(10, 55), ForeColor = System.Drawing.Color.Gray, Font = new System.Drawing.Font("Segoe UI", 8), AutoSize = true };
            groupVersions.Controls.AddRange(new Control[] { lblRetention, retentionDays, lblDays, lblHint });
            scrollPanel.Controls.Add(groupVersions);
            yOffset += 95;

            // ========== Группа: Исключения файлов ==========
            var groupExclude = new GroupBox { Text = "🚫 Исключения (маски файлов)", Location = new System.Drawing.Point(10, yOffset), Size = new System.Drawing.Size(580, 110) };
            var lblExcludeHint = new Label { Text = "Указывайте через запятую, например: *.tmp, *.log, thumbs.db", Location = new System.Drawing.Point(10, 20), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 8) };
            excludeMasksBox = new TextBox { Location = new System.Drawing.Point(10, 45), Width = 555, Height = 50, Multiline = true, ScrollBars = ScrollBars.Vertical };
            groupExclude.Controls.AddRange(new Control[] { lblExcludeHint, excludeMasksBox });
            scrollPanel.Controls.Add(groupExclude);
            yOffset += 125;

            // ========== Группа: Производительность ==========
            var groupPerformance = new GroupBox { Text = "⚡ Производительность", Location = new System.Drawing.Point(10, yOffset), Size = new System.Drawing.Size(580, 80) };
            limitSpeedChk = new CheckBox { Text = "Ограничить скорость копирования (МБ/с):", Location = new System.Drawing.Point(10, 25), AutoSize = true };
            speedLimit = new NumericUpDown { Location = new System.Drawing.Point(250, 23), Width = 60, Minimum = 1, Maximum = 1000, Value = 10, Enabled = false };
            limitSpeedChk.CheckedChanged += (s, e) => speedLimit.Enabled = limitSpeedChk.Checked;
            groupPerformance.Controls.AddRange(new Control[] { limitSpeedChk, speedLimit });
            scrollPanel.Controls.Add(groupPerformance);
            yOffset += 95;

            // ========== Группа: Энергосбережение ==========
            var groupPower = new GroupBox { Text = "🔋 Энергосбережение", Location = new System.Drawing.Point(10, yOffset), Size = new System.Drawing.Size(580, 60) };
            pauseBatteryChk = new CheckBox { Text = "Приостанавливать резервное копирование при работе от батареи", Location = new System.Drawing.Point(10, 25), AutoSize = true };
            groupPower.Controls.Add(pauseBatteryChk);
            scrollPanel.Controls.Add(groupPower);
            yOffset += 75;

            // Добавляем панель прокрутки на форму
            this.Controls.Add(scrollPanel);

            // ========== Кнопки импорта/экспорта и сохранения (внизу формы, не прокручиваются) ==========
            importBtn = new Button { Text = "📥 Импорт настроек", Location = new System.Drawing.Point(10, 460), Width = 130 };
            exportBtn = new Button { Text = "📤 Экспорт настроек", Location = new System.Drawing.Point(150, 460), Width = 130 };
            saveBtn = new Button { Text = "💾 Сохранить", Location = new System.Drawing.Point(450, 460), Width = 85, BackColor = System.Drawing.Color.LightGreen };
            cancelBtn = new Button { Text = "❌ Отмена", Location = new System.Drawing.Point(545, 460), Width = 80 };

            cancelBtn.Click += (s, e) => this.Close();

            importBtn.Click += (s, e) =>
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Filter = "JSON файлы|*.json";
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        Config.Import(ofd.FileName);
                        LoadSettings();
                        MessageBox.Show("Настройки импортированы. Не забудьте сохранить.", "Импорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            exportBtn.Click += (s, e) =>
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "JSON файлы|*.json";
                    sfd.FileName = "backup_settings.json";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        Config.Export(sfd.FileName);
                        MessageBox.Show("Настройки экспортированы.", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };
            saveBtn.Click += Save;

            this.Controls.AddRange(new Control[] { importBtn, exportBtn, saveBtn, cancelBtn });
        }

        private void LoadSettings()
        {
            sourcesList.Items.Clear();
            foreach (var folder in Config.Current.SourceFolders)
                sourcesList.Items.Add(folder);

            retentionDays.Value = Config.Current.VersionRetentionDays;
            excludeMasksBox.Text = string.Join(", ", Config.Current.ExcludeMasks);
            limitSpeedChk.Checked = Config.Current.LimitSpeed;
            speedLimit.Value = (int)(Config.Current.MaxBytesPerSecond / 1024 / 1024);
            speedLimit.Enabled = Config.Current.LimitSpeed;
            pauseBatteryChk.Checked = Config.Current.PauseOnBattery;
        }

        private void Save(object sender, EventArgs e)
        {
            var newSources = sourcesList.Items.Cast<string>().ToList();
            if (newSources.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну папку для резервного копирования.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Config.Current.SourceFolders = newSources;

            Config.Current.VersionRetentionDays = (int)retentionDays.Value;
            Config.Current.ExcludeMasks = excludeMasksBox.Text
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrEmpty(m))
                .ToList();
            Config.Current.LimitSpeed = limitSpeedChk.Checked;
            Config.Current.MaxBytesPerSecond = (long)speedLimit.Value * 1024 * 1024;
            Config.Current.PauseOnBattery = pauseBatteryChk.Checked;
            Config.Save();

            MessageBox.Show("Настройки сохранены.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
}