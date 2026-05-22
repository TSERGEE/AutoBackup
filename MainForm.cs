using System.Runtime.InteropServices;
using Guna.UI2.WinForms;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoBackup
{
    public class MainForm : Form
    {
        // =====================================================
        // UI
        // =====================================================

        private Guna2DataGridView logGrid;
        private Guna2VScrollBar vScrollBar;
        private Guna2Button backupBtn;
        private Guna2Button restoreBtn;
        private Guna2Button verifyBtn;
        private Guna2Button settingsBtn;
        private Guna2Button pauseBtn;
        private Guna2Button exportBtn;
        private Guna2Button clearBtn;
        private Label statusLabel;
        private Label nextRunLabel;

        private Guna2TextBox searchBox;

        private Guna2ComboBox filterBox;

        private Guna2ProgressBar progressBar;

        private System.Windows.Forms.Timer refreshTimer;

        // =====================================================
        // CTOR
        // =====================================================

        public MainForm()
        {
            InitializeComponents();

            EnableDarkTitleBar();

            LoadLog();
            
            UpdateNextRunInfo();

            StartRefreshTimer();
        }

        // =====================================================
        // INIT
        // =====================================================

        private void InitializeComponents()
        {
            Text = "AutoBackup";

            Width = 1400;
            Height = 850;

            MinimumSize = new Size(1100, 700);

            StartPosition = FormStartPosition.CenterScreen;

            FormBorderStyle = FormBorderStyle.Sizable;

            DoubleBuffered = true;

            BackColor = Color.FromArgb(24, 24, 27);

            Font = new Font("Segoe UI", 9F);

            // =================================================
            // MAIN LAYOUT
            // =================================================

            TableLayoutPanel layout =
                new TableLayoutPanel();

            layout.Dock = DockStyle.Fill;

            layout.RowCount = 4;

            layout.ColumnCount = 1;

            layout.Padding = new Padding(12);

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 70));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 110));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 55));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            Controls.Add(layout);
            
            // =================================================
            // TOOLBAR
            // =================================================

            Guna2Panel toolbarPanel =
                CreatePanel();

            layout.Controls.Add(toolbarPanel);

            FlowLayoutPanel toolbar =
                new FlowLayoutPanel();

            toolbar.Dock = DockStyle.Fill;

            toolbar.Padding = new Padding(10);

            toolbar.BackColor = Color.Transparent;

            toolbarPanel.Controls.Add(toolbar);

            backupBtn =
                CreatePrimaryButton("▶ Backup");

            restoreBtn =
                CreateSecondaryButton("↺ Restore");

            verifyBtn =
                CreateSecondaryButton("✓ Verify");

            settingsBtn =
                CreateSecondaryButton("⚙ Settings");

            pauseBtn =
                CreateWarningButton("⏸ Pause");

            exportBtn =
                CreateSecondaryButton("💾 Export");

            clearBtn =
                CreateDangerButton("🗑 Clear");

            toolbar.Controls.AddRange(new Control[]
            {
                backupBtn,
                restoreBtn,
                verifyBtn,
                settingsBtn,
                pauseBtn,
                exportBtn,
                clearBtn
            });

            // =================================================
            // DASHBOARD
            // =================================================

            Guna2Panel dashboard =
                CreatePanel();

            dashboard.Padding = new Padding(20);

            layout.Controls.Add(dashboard);

            statusLabel = new Label();

            statusLabel.Text =
                "Статус: Готов";

            statusLabel.ForeColor =
                Color.White;

            statusLabel.Font =
                new Font(
                    "Segoe UI",
                    11F,
                    FontStyle.Bold);

            statusLabel.Location =
                new Point(20, 20);

            statusLabel.AutoSize = true;

            nextRunLabel = new Label();

            nextRunLabel.Text =
                "Следующий запуск: не запланирован";

            nextRunLabel.ForeColor =
                Color.Silver;

            nextRunLabel.Location =
                new Point(20, 55);

            nextRunLabel.AutoSize = true;

            progressBar =
                new Guna2ProgressBar();

            progressBar.Location =
                new Point(500, 30);

            progressBar.Size =
                new Size(400, 25);

            progressBar.BorderRadius = 10;

            progressBar.FillColor =
                Color.FromArgb(50, 50, 55);

            progressBar.ProgressColor =
                Color.FromArgb(0, 120, 215);

            dashboard.Controls.Add(statusLabel);

            dashboard.Controls.Add(nextRunLabel);

            dashboard.Controls.Add(progressBar);

            // =================================================
            // FILTER PANEL
            // =================================================

            Guna2Panel filterPanel =
                CreatePanel();

            filterPanel.Padding =
                new Padding(15, 10, 15, 10);

            layout.Controls.Add(filterPanel);

            searchBox =
                new Guna2TextBox();

            searchBox.PlaceholderText =
                "Поиск по логам...";

            searchBox.BorderRadius = 10;

            searchBox.FillColor =
                Color.FromArgb(35, 35, 40);

            searchBox.ForeColor =
                Color.White;

            searchBox.Size =
                new Size(300, 36);

            searchBox.Location =
                new Point(15, 8);

            searchBox.TextChanged +=
                (s, e) => LoadLog();

            filterBox =
                new Guna2ComboBox();

            filterBox.Items.AddRange(new object[]
            {
                "Все",
                "Info",
                "Warning",
                "Error"
            });

            filterBox.SelectedIndex = 0;

            filterBox.BorderRadius = 10;

            filterBox.FillColor =
                Color.FromArgb(35, 35, 40);

            filterBox.ForeColor =
                Color.White;

            filterBox.Size =
                new Size(180, 36);

            filterBox.Location =
                new Point(340, 8);

            filterBox.SelectedIndexChanged +=
                (s, e) => LoadLog();

            filterPanel.Controls.Add(searchBox);

            filterPanel.Controls.Add(filterBox);

            // =================================================
            // GRID
            // =================================================

            logGrid =
                new Guna2DataGridView();

            logGrid.Dock = DockStyle.Fill;

            logGrid.BackgroundColor =
                Color.FromArgb(24, 24, 27);

            logGrid.GridColor =
                Color.FromArgb(55, 55, 60);

            logGrid.BorderStyle =
                BorderStyle.None;

            logGrid.RowHeadersVisible = false;

            logGrid.EnableHeadersVisualStyles = false;

            logGrid.ColumnHeadersHeight = 40;

            logGrid.ThemeStyle.BackColor =
                Color.FromArgb(24, 24, 27);

            logGrid.ThemeStyle.GridColor =
                Color.FromArgb(55, 55, 60);

            logGrid.ThemeStyle.HeaderStyle.BackColor =
                Color.FromArgb(45, 45, 50);

            logGrid.ThemeStyle.HeaderStyle.ForeColor =
                Color.White;

            logGrid.ThemeStyle.RowsStyle.BackColor =
                Color.FromArgb(35, 35, 40);

            logGrid.ThemeStyle.RowsStyle.ForeColor =
                Color.White;

            logGrid.ThemeStyle.RowsStyle.SelectionBackColor =
                Color.FromArgb(0, 120, 215);

            logGrid.ThemeStyle.RowsStyle.SelectionForeColor =
                Color.White;

            logGrid.ThemeStyle.AlternatingRowsStyle.BackColor =
                Color.FromArgb(35, 35, 40);

            logGrid.DefaultCellStyle.BackColor =
                Color.FromArgb(35, 35, 40);

            logGrid.RowsDefaultCellStyle.BackColor =
                Color.FromArgb(35, 35, 40);

            logGrid.AlternatingRowsDefaultCellStyle.BackColor =
                Color.FromArgb(35, 35, 40);

            logGrid.DefaultCellStyle.ForeColor =
                Color.White;

            logGrid.RowTemplate.Height = 34;

            logGrid.Columns.Add(
                "Timestamp",
                "Дата/время");

            logGrid.Columns.Add(
                "Operation",
                "Операция");

            logGrid.Columns.Add(
                "Details",
                "Подробности");

            logGrid.Columns.Add(
                "Status",
                "Статус");
            logGrid.MouseWheel += (s, e) =>
            {
                try
                {
                    if (logGrid.Rows.Count == 0)
                        return;

                    int current =
                        logGrid.FirstDisplayedScrollingRowIndex;

                    if (e.Delta > 0)
                    {
                        current -= 3;
                    }
                    else
                    {
                        current += 3;
                    }

                    current = Math.Max(
                        0,
                        Math.Min(
                            current,
                            logGrid.Rows.Count - 1));

                    logGrid.FirstDisplayedScrollingRowIndex =
                        current;

                    vScrollBar.Value =
                        current;
                }
                catch
                {
                }
            };
            // =================================================
            // GRID CONTAINER
            // =================================================

            Panel gridContainer = new Panel();

            gridContainer.Dock = DockStyle.Fill;

            gridContainer.BackColor =
                Color.FromArgb(24, 24, 27);

            // скрываем системный scrollbar
            logGrid.ScrollBars = ScrollBars.None;

            // grid
            logGrid.Dock = DockStyle.Fill;

            // custom scrollbar
            vScrollBar = new Guna2VScrollBar();

            vScrollBar.Dock = DockStyle.Right;

            vScrollBar.Width = 14;

            vScrollBar.FillColor =
                Color.FromArgb(30, 30, 35);

            vScrollBar.ThumbColor =
                Color.FromArgb(80, 80, 90);

            vScrollBar.BorderRadius = 7;

            // sync scrollbar -> grid
            vScrollBar.Scroll += (s, e) =>
            {
                try
                {
                    if (logGrid.Rows.Count > 0)
                    {
                        logGrid.FirstDisplayedScrollingRowIndex =
                            Math.Min(
                                vScrollBar.Value,
                                logGrid.Rows.Count - 1);
                    }
                }
                catch
                {
                }
            };

            // sync mousewheel -> scrollbar
            logGrid.MouseWheel += (s, e) =>
            {
                try
                {
                    if (logGrid.FirstDisplayedScrollingRowIndex >= 0)
                    {
                        vScrollBar.Value =
                            logGrid.FirstDisplayedScrollingRowIndex;
                    }
                }
                catch
                {
                }
            };

            gridContainer.Controls.Add(logGrid);

            gridContainer.Controls.Add(vScrollBar);

            layout.Controls.Add(gridContainer);
            //EnableDarkScrollBar(logGrid);

            // =================================================
            // EVENTS
            // =================================================

            backupBtn.Click += BackupNow;

            restoreBtn.Click +=
                (s, e) =>
                {
                    new RestoreForm().ShowDialog();
                };

            settingsBtn.Click +=
                (s, e) =>
                {
                    new SettingsForm().ShowDialog();

                    UpdateNextRunInfo();
                };

            pauseBtn.Click += TogglePause;

            verifyBtn.Click += VerifyBackup;

            exportBtn.Click += ExportLog;

            clearBtn.Click += ClearLog;
        }

        // =====================================================
        // PANEL
        // =====================================================

        private Guna2Panel CreatePanel()
        {
            return new Guna2Panel
            {
                Dock = DockStyle.Fill,

                BorderRadius = 18,

                FillColor = Color.FromArgb(30, 30, 35)
            };
        }

        // =====================================================
        // BUTTONS
        // =====================================================

        private Guna2Button CreatePrimaryButton(
            string text)
        {
            return new Guna2Button
            {
                Text = text,

                Width = 130,

                Height = 42,

                BorderRadius = 12,

                FillColor =
                    Color.FromArgb(0, 120, 215),

                Font =
                    new Font(
                        "Segoe UI",
                        9F,
                        FontStyle.Bold),

                ForeColor = Color.White
            };
        }

        private Guna2Button CreateSecondaryButton(
            string text)
        {
            return new Guna2Button
            {
                Text = text,

                Width = 120,

                Height = 42,

                BorderRadius = 12,

                FillColor =
                    Color.FromArgb(45, 45, 50),

                Font =
                    new Font(
                        "Segoe UI",
                        9F),

                ForeColor = Color.White
            };
        }

        private Guna2Button CreateWarningButton(
            string text)
        {
            return new Guna2Button
            {
                Text = text,

                Width = 120,

                Height = 42,

                BorderRadius = 12,

                FillColor =
                    Color.Goldenrod,

                ForeColor = Color.White
            };
        }

        private Guna2Button CreateDangerButton(
            string text)
        {
            return new Guna2Button
            {
                Text = text,

                Width = 120,

                Height = 42,

                BorderRadius = 12,

                FillColor =
                    Color.Firebrick,

                ForeColor = Color.White
            };
        }

        // =====================================================
        // TIMER
        // =====================================================

        private void StartRefreshTimer()
        {
            refreshTimer =
                new System.Windows.Forms.Timer();

            refreshTimer.Interval = 30000;

            refreshTimer.Tick +=
                (s, e) =>
                {
                    LoadLog();

                    UpdateNextRunInfo();
                };

            refreshTimer.Start();
        }

        // =====================================================
        // LOAD LOG
        // =====================================================

        private void LoadLog()
        {
            if (logGrid.Columns.Count == 0)
                return;
            logGrid.Rows.Clear();

            string filter =
                filterBox.SelectedItem?.ToString();

            string search =
                searchBox.Text.ToLower();

            var entries =
                Logger.GetRecentEntries(300);

            if (filter != "Все")
            {
                entries = entries
                    .Where(x => x.Status == filter)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                entries = entries
                    .Where(x =>
                        x.Operation
                            .ToLower()
                            .Contains(search)
                        ||
                        x.Details
                            .ToLower()
                            .Contains(search))
                    .ToList();
            }

            foreach (var entry in entries)
            {
                int row =
                    logGrid.Rows.Add(
                        entry.Timestamp,
                        entry.Operation,
                        entry.Details,
                        entry.Status);

                if (entry.Status == "Error")
                {
                    logGrid.Rows[row]
                        .DefaultCellStyle
                        .BackColor =
                        Color.FromArgb(70, 25, 25);
                }
                else if (entry.Status == "Warning")
                {
                    logGrid.Rows[row]
                        .DefaultCellStyle
                        .BackColor =
                        Color.FromArgb(70, 60, 25);
                }
                
            }
            if (logGrid.Rows.Count > 0)
            {
                vScrollBar.Maximum =
                    Math.Max(0, logGrid.Rows.Count - 1);

                vScrollBar.LargeChange = 10;
            }
        }

        // =====================================================
        // UPDATE SCHEDULE
        // =====================================================

        private void UpdateNextRunInfo()
        {
            string schedule =
                Config.Current.BackupSchedule;

            string text = schedule switch
            {
                "Daily" =>
                    "ежедневно",

                "Weekly" =>
                    "еженедельно",

                "OnSystemStart" =>
                    "при запуске системы",

                "OnIdle" =>
                    $"при простое ({Config.Current.IdleMinutes} мин)",

                _ =>
                    "не задано"
            };

            nextRunLabel.Text =
                $"Расписание: {text}";
        }

        // =====================================================
        // BACKUP
        // =====================================================

        private async void BackupNow(
            object sender,
            EventArgs e)
        {
            try
            {
                backupBtn.Enabled = false;

                progressBar.Style =
                    ProgressBarStyle.Marquee;

                statusLabel.Text =
                    "Статус: Выполняется backup...";

                await BackupManager.RunBackup(true);

                LoadLog();

                statusLabel.Text =
                    "Статус: Backup завершён";
            }
            finally
            {
                progressBar.Style =
                    ProgressBarStyle.Blocks;

                backupBtn.Enabled = true;
            }
        }

        // =====================================================
        // PAUSE
        // =====================================================

        private void TogglePause(
            object sender,
            EventArgs e)
        {
            if (BackupManager.IsPaused())
            {
                BackupManager.Resume();

                pauseBtn.Text = "⏸ Pause";

                pauseBtn.FillColor =
                    Color.Goldenrod;

                statusLabel.Text =
                    "Статус: Активен";
            }
            else
            {
                BackupManager.PauseFor(60);

                pauseBtn.Text =
                    "▶ Resume";

                pauseBtn.FillColor =
                    Color.ForestGreen;

                statusLabel.Text =
                    "Статус: Пауза";
            }
        }

        // =====================================================
        // VERIFY
        // =====================================================

        private async void VerifyBackup(
            object sender,
            EventArgs e)
        {
            using FolderBrowserDialog dialog =
                new FolderBrowserDialog();

            dialog.Description =
                "Выберите backup folder";

            if (dialog.ShowDialog()
                != DialogResult.OK)
                return;

            string backupFolder =
                dialog.SelectedPath;

            List<string> mismatches =
                new List<string>();

            int totalFiles = 0;

            int verified = 0;

            progressBar.Style =
                ProgressBarStyle.Marquee;

            await Task.Run(() =>
            {
                foreach (string sourceRoot
                    in Config.Current.SourceFolders)
                {
                    string sourceDirName =
                        Path.GetFileName(sourceRoot);

                    string backupSourceDir =
                        Path.Combine(
                            backupFolder,
                            sourceDirName);

                    if (!Directory.Exists(
                        backupSourceDir))
                        continue;

                    VerifyDirectory(
                        sourceRoot,
                        backupSourceDir,
                        mismatches,
                        ref totalFiles,
                        ref verified);
                }
            });

            progressBar.Style =
                ProgressBarStyle.Blocks;

            MessageBox.Show(
                $"Проверено: {verified}\n" +
                $"Ошибок: {mismatches.Count}",
                "Verify",
                MessageBoxButtons.OK,
                mismatches.Count > 0
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Information);
        }

        // =====================================================
        // VERIFY DIR
        // =====================================================

        private void VerifyDirectory(
            string originalDir,
            string backupDir,
            List<string> mismatches,
            ref int totalFiles,
            ref int verified)
        {
            foreach (string file
                in Directory.GetFiles(originalDir))
            {
                string fileName =
                    Path.GetFileName(file);

                if (ShouldExclude(fileName))
                    continue;

                totalFiles++;

                string backupFile =
                    Path.Combine(
                        backupDir,
                        fileName);

                if (!File.Exists(backupFile))
                {
                    mismatches.Add(
                        $"Отсутствует: {file}");

                    continue;
                }

                if (!CompareFileHash(
                    file,
                    backupFile))
                {
                    mismatches.Add(
                        $"Hash mismatch: {file}");
                }

                verified++;
            }
        }

        // =====================================================
        // HASH
        // =====================================================

        private bool CompareFileHash(
            string file1,
            string file2)
        {
            using SHA256 sha =
                SHA256.Create();

            using FileStream fs1 =
                File.OpenRead(file1);

            using FileStream fs2 =
                File.OpenRead(file2);

            byte[] hash1 =
                sha.ComputeHash(fs1);

            byte[] hash2 =
                sha.ComputeHash(fs2);

            return StructuralComparisons
                .StructuralEqualityComparer
                .Equals(hash1, hash2);
        }

        // =====================================================
        // EXCLUDE
        // =====================================================

        private bool ShouldExclude(
            string fileName)
        {
            foreach (string mask
                in Config.Current.ExcludeMasks)
            {
                if (mask.StartsWith("*.") &&
                    fileName.EndsWith(mask.Substring(1)))
                    return true;
            }

            return false;
        }

        // =====================================================
        // EXPORT
        // =====================================================

        private void ExportLog(
            object sender,
            EventArgs e)
        {
            using SaveFileDialog sfd =
                new SaveFileDialog();

            sfd.Filter =
                "CSV files|*.csv";

            if (sfd.ShowDialog()
                != DialogResult.OK)
                return;

            List<string> lines =
                new List<string>();

            foreach (DataGridViewRow row
                in logGrid.Rows)
            {
                lines.Add(
                    string.Join(
                        ";",
                        row.Cells
                            .Cast<DataGridViewCell>()
                            .Select(c =>
                                c.Value?.ToString() ?? "")));
            }

            File.WriteAllLines(
                sfd.FileName,
                lines);

            MessageBox.Show(
                "Лог экспортирован.");
        }

        // =====================================================
        // CLEAR
        // =====================================================

        private void ClearLog(
            object sender,
            EventArgs e)
        {
            if (MessageBox.Show(
                "Очистить лог?",
                "Подтверждение",
                MessageBoxButtons.YesNo)
                != DialogResult.Yes)
                return;

            Logger.ClearLog();

            LoadLog();
        }
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private void EnableDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int dark = 1;

                DwmSetWindowAttribute(
                    this.Handle,
                    DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref dark,
                    sizeof(int));
            }
        }
        
    }
}