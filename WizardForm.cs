using AutoBackup.Models;
using AutoBackup.Services;
using System;
using System.Linq;
using System.Windows.Forms;

namespace AutoBackup
{
    public class WizardForm : Form
    {
        private Button nextBtn, backBtn;
        private int step = 0;
        private Panel panel;
        private TextBox destBox;
        private ComboBox scheduleCombo;
        private ListBox sourceList;
        private Button addSourceBtn;

        public WizardForm()
        {
            Text = "Мастер настройки резервного копирования";
            Size = new System.Drawing.Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            panel = new Panel { Dock = DockStyle.Fill };
            nextBtn = new Button { Text = "Далее", Dock = DockStyle.Bottom, Height = 35 };
            backBtn = new Button { Text = "Назад", Dock = DockStyle.Bottom, Height = 35, Visible = false };
            nextBtn.Click += NextStep;
            backBtn.Click += PreviousStep;

            Controls.Add(panel);
            Controls.Add(nextBtn);
            Controls.Add(backBtn);
            ShowStep();
        }

        private void ShowStep()
        {
            panel.Controls.Clear();
            if (step == 0)
            {
                // Шаг 1: выбор папок-источников
                var lbl = new Label
                {
                    Text = "Выберите папки, которые нужно резервировать:",
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, 10),
                    Font = new System.Drawing.Font("Segoe UI", 9F)
                };
                sourceList = new ListBox
                {
                    Location = new System.Drawing.Point(10, 40),
                    Size = new System.Drawing.Size(440, 200),
                    SelectionMode = SelectionMode.MultiExtended
                };
                addSourceBtn = new Button
                {
                    Text = "➕ Добавить папку",
                    Location = new System.Drawing.Point(460, 40),
                    Size = new System.Drawing.Size(110, 30)
                };
                addSourceBtn.Click += (s, e) =>
                {
                    using (var fbd = new FolderBrowserDialog())
                    {
                        if (fbd.ShowDialog() == DialogResult.OK && !sourceList.Items.Contains(fbd.SelectedPath))
                            sourceList.Items.Add(fbd.SelectedPath);
                    }
                };
                panel.Controls.Add(lbl);
                panel.Controls.Add(sourceList);
                panel.Controls.Add(addSourceBtn);
            }
            else if (step == 1)
            {
                // Шаг 2: выбор целевой папки
                var lbl = new Label
                {
                    Text = "Выберите папку для хранения резервных копий:",
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, 10)
                };
                destBox = new TextBox
                {
                    Location = new System.Drawing.Point(10, 40),
                    Width = 440,
                    ReadOnly = true,
                    BackColor = System.Drawing.Color.WhiteSmoke
                };
                var browse = new Button
                {
                    Text = "Обзор",
                    Location = new System.Drawing.Point(460, 38),
                    Size = new System.Drawing.Size(110, 30)
                };
                browse.Click += (s, e) =>
                {
                    using (var fbd = new FolderBrowserDialog())
                    {
                        if (fbd.ShowDialog() == DialogResult.OK)
                            destBox.Text = fbd.SelectedPath;
                    }
                };
                panel.Controls.Add(lbl);
                panel.Controls.Add(destBox);
                panel.Controls.Add(browse);
            }
            else if (step == 2)
            {
                // Шаг 3: расписание (старые варианты, но мы преобразуем в cron)
                var lbl = new Label
                {
                    Text = "Как часто выполнять резервное копирование?",
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, 10)
                };
                scheduleCombo = new ComboBox
                {
                    Location = new System.Drawing.Point(10, 40),
                    Width = 250,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                scheduleCombo.Items.AddRange(new[] { "Ежедневно (в 02:00)", "Еженедельно (в воскресенье, 02:00)", "При запуске системы", "При простое (каждые 10 минут)" });
                scheduleCombo.SelectedIndex = 0;

                var hint = new Label
                {
                    Text = "Более гибкие настройки расписания (cron) доступны в настройках программы.",
                    AutoSize = true,
                    Location = new System.Drawing.Point(10, 80),
                    ForeColor = System.Drawing.Color.Gray,
                    Font = new System.Drawing.Font("Segoe UI", 8F)
                };
                panel.Controls.Add(lbl);
                panel.Controls.Add(scheduleCombo);
                panel.Controls.Add(hint);
            }

            backBtn.Visible = step > 0;
            nextBtn.Text = (step == 2) ? "Готово" : "Далее";
        }

        private void NextStep(object sender, EventArgs e)
        {
            // Валидация шагов
            if (step == 0)
            {
                var sources = sourceList.Items.Cast<string>().ToList();
                if (sources.Count == 0)
                {
                    MessageBox.Show("Добавьте хотя бы одну папку для резервного копирования.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Config.Current.SourceFolders = sources;
            }
            else if (step == 1)
            {
                if (string.IsNullOrWhiteSpace(destBox.Text) || !System.IO.Directory.Exists(destBox.Text))
                {
                    MessageBox.Show("Выберите существующую целевую папку для бэкапов.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Config.Current.DestinationFolder = destBox.Text;
            }
            else if (step == 2)
            {
                // Преобразуем выбранное расписание в cron-выражение
                string selected = scheduleCombo.SelectedItem.ToString();
                string cron = selected switch
                {
                    "Ежедневно (в 02:00)" => "0 2 * * *",
                    "Еженедельно (в воскресенье, 02:00)" => "0 2 * * 0",
                    "При запуске системы" => "0 2 * * *", // нет прямого аналога, оставим ежедневно, пользователь сможет изменить в настройках
                    "При простое (каждые 10 минут)" => "*/10 * * * *",
                    _ => "0 2 * * *"
                };
                Config.Current.BackupScheduleCron = cron;
                Config.Current.BackupSchedule = selected switch
                {
                    "Ежедневно (в 02:00)" => "Daily",
                    "Еженедельно (в воскресенье, 02:00)" => "Weekly",
                    "При запуске системы" => "OnSystemStart",
                    "При простое (каждые 10 минут)" => "OnIdle",
                    _ => "Daily"
                };

                // Установка разумных значений по умолчанию для новых параметров
                Config.Current.MaxParallelCopies = 4;
                Config.Current.VerifyAfterBackup = true;
                Config.Current.UseFastHash = true;
                Config.Current.MinFreeSpacePercent = 10;
                Config.Current.FullBackupIntervalDays = 7;
                Config.Current.KeepFullBackupsCount = 4;
                Config.Current.VersionRetentionDays = 30;

                // Сохраняем конфигурацию
                Config.Save();

                // Перезапускаем планировщик (если он уже инициализирован)
                SchedulerService.UpdateSchedule();

                MessageBox.Show("Настройки сохранены. Резервное копирование будет выполняться по расписанию.", "Мастер завершён", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            step++;
            ShowStep();
        }

        private void PreviousStep(object sender, EventArgs e)
        {
            step--;
            ShowStep();
        }
    }
}