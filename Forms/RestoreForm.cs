using AutoBackup.Models;
using AutoBackup.Services;
using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoBackup.Forms
{
    public class RestoreForm : Form
    {
        // =====================================================
        // UI
        // =====================================================
        private TreeView backupTree;
        private Guna2TextBox filterBox;
        private Guna2ComboBox backupTypeFilter;
        private Guna2HtmlLabel infoLabel;
        private Guna2HtmlLabel selectedCountLabel;
        private Guna2HtmlLabel statusLabel;
        private Guna2HtmlLabel currentFileLabel;
        private Guna2CheckBox restoreToOriginalCheckBox;
        private Guna2CheckBox overwriteCheckBox;
        private Guna2TextBox customRestorePath;
        private Guna2Button browseCustomPathBtn;
        private Guna2ProgressBar progressBar;
        private Guna2Button restoreBtn;
        private Guna2Button cancelBtn;
        private Guna2Button closeBtn;
        private RichTextBox logBox;

        private CancellationTokenSource _cts;
        private string _selectedBackupRoot; // корень выбранного бэкапа (папка с метафайлом)

        // =====================================================
        // CTOR
        // =====================================================
        public RestoreForm()
        {
            InitializeComponents();
            EnableDarkTitleBar();
            LoadBackups();
        }

        // =====================================================
        // INIT
        // =====================================================
        private void InitializeComponents()
        {
            Text = "Восстановление из резервной копии";
            Width = 1400;
            Height = 850;
            MinimumSize = new Size(1200, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 24, 27);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.Sizable;

            // MAIN LAYOUT
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(12)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            Controls.Add(layout);

            // CONTENT LAYOUT
            var contentLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            layout.Controls.Add(contentLayout);

            // LEFT CARD
            var leftCard = CreateCard();
            leftCard.Padding = new Padding(18);
            contentLayout.Controls.Add(leftCard, 0, 0);

            // FILTER PANEL
            var filterPanel = new Panel { Dock = DockStyle.Top, Height = 65 };
            leftCard.Controls.Add(filterPanel);

            filterBox = new Guna2TextBox
            {
                PlaceholderText = "Поиск...",
                BorderRadius = 10,
                FillColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                Size = new Size(250, 36),
                Location = new Point(0, 5)
            };
            var searchBtn = CreatePrimaryButton("Найти");
            searchBtn.Location = new Point(265, 5);
            searchBtn.Size = new Size(100, 36);
            searchBtn.Click += (s, e) => ApplyFilter();

            backupTypeFilter = new Guna2ComboBox
            {
                Items = { "Все", "Full", "Diff" },
                SelectedIndex = 0,
                BorderRadius = 10,
                FillColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                Size = new Size(170, 36),
                Location = new Point(380, 5)
            };
            backupTypeFilter.SelectedIndexChanged += (s, e) => LoadBackups();

            filterPanel.Controls.Add(filterBox);
            filterPanel.Controls.Add(searchBtn);
            filterPanel.Controls.Add(backupTypeFilter);

            // TREE CONTAINER
            var treeContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
            leftCard.Controls.Add(treeContainer);
            treeContainer.BringToFront();

            // TREEVIEW
            backupTree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 27),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9F),
                CheckBoxes = false, // не выбираем файлы по отдельности, восстанавливаем целиком бэкап
                HideSelection = false,
                LineColor = Color.FromArgb(60, 60, 65),
                ItemHeight = 24,
                DrawMode = TreeViewDrawMode.OwnerDrawText
            };
            backupTree.DrawNode += (s, e) =>
            {
                Color bg = e.Node.IsSelected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(24, 24, 27);
                using (SolidBrush back = new SolidBrush(bg))
                using (SolidBrush fore = new SolidBrush(Color.White))
                {
                    e.Graphics.FillRectangle(back, e.Bounds);
                    e.Graphics.DrawString(e.Node.Text, backupTree.Font, fore, e.Bounds.Location);
                }
            };
            backupTree.BeforeExpand += BackupTree_BeforeExpand;
            backupTree.AfterSelect += BackupTree_AfterSelect;
            EnableDarkScrollBar(backupTree);

            treeContainer.Controls.Add(backupTree);

            // RIGHT PANEL
            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            contentLayout.Controls.Add(rightPanel, 1, 0);

            // SETTINGS CARD
            var settingsCard = CreateCard();
            settingsCard.Dock = DockStyle.Top;
            settingsCard.Height = 230;
            settingsCard.Padding = new Padding(18);
            rightPanel.Controls.Add(settingsCard);

            var settingsTitle = CreateTitle("Настройки восстановления");
            settingsCard.Controls.Add(settingsTitle);

            restoreToOriginalCheckBox = new Guna2CheckBox
            {
                Text = "Восстановить в оригинальные папки",
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(18, 50)
            };
            overwriteCheckBox = new Guna2CheckBox
            {
                Text = "Перезаписывать существующие файлы",
                Checked = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(18, 85)
            };
            customRestorePath = new Guna2TextBox
            {
                PlaceholderText = "Папка для восстановления",
                BorderRadius = 10,
                FillColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                Location = new Point(18, 125),
                Size = new Size(220, 36),
                Enabled = false
            };
            browseCustomPathBtn = CreateSecondaryButton("Обзор");
            browseCustomPathBtn.Location = new Point(245, 125);
            browseCustomPathBtn.Size = new Size(75, 36);
            browseCustomPathBtn.Enabled = false;
            browseCustomPathBtn.Click += BrowseCustomPathBtn_Click;

            restoreToOriginalCheckBox.CheckedChanged += (s, e) =>
            {
                customRestorePath.Enabled = !restoreToOriginalCheckBox.Checked;
                browseCustomPathBtn.Enabled = !restoreToOriginalCheckBox.Checked;
            };

            selectedCountLabel = new Guna2HtmlLabel
            {
                Text = "Выбрана резервная копия: нет",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(18, 180),
                AutoSize = true
            };

            settingsCard.Controls.Add(restoreToOriginalCheckBox);
            settingsCard.Controls.Add(overwriteCheckBox);
            settingsCard.Controls.Add(customRestorePath);
            settingsCard.Controls.Add(browseCustomPathBtn);
            settingsCard.Controls.Add(selectedCountLabel);

            // INFO CARD
            var infoCard = CreateCard();
            infoCard.Dock = DockStyle.Top;
            infoCard.Height = 170;
            infoCard.Padding = new Padding(18);
            infoCard.Margin = new Padding(0, 12, 0, 12);
            rightPanel.Controls.Add(infoCard);
            infoCard.BringToFront();

            var infoTitle = CreateTitle("Информация");
            infoCard.Controls.Add(infoTitle);

            infoLabel = new Guna2HtmlLabel
            {
                Text = "Выберите резервную копию из списка слева",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(18, 55),
                Size = new Size(280, 100)
            };
            infoCard.Controls.Add(infoLabel);

            // LOG CARD
            var logCard = CreateCard();
            logCard.Dock = DockStyle.Fill;
            logCard.Padding = new Padding(18);
            rightPanel.Controls.Add(logCard);
            logCard.BringToFront();

            var logTitle = CreateTitle("Журнал восстановления");
            logCard.Controls.Add(logTitle);

            logBox = new RichTextBox
            {
                Location = new Point(18, 50),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Size = new Size(300, 300),
                BackColor = Color.FromArgb(22, 22, 28),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };
            logCard.Controls.Add(logBox);

            // BOTTOM PANEL
            var bottomPanel = CreateCard();
            bottomPanel.Padding = new Padding(18);
            layout.Controls.Add(bottomPanel);

            progressBar = new Guna2ProgressBar
            {
                Location = new Point(18, 20),
                Size = new Size(350, 18),
                BorderRadius = 8,
                FillColor = Color.FromArgb(50, 50, 55),
                ProgressColor = Color.FromArgb(0, 120, 215)
            };
            statusLabel = new Guna2HtmlLabel
            {
                Text = "Готов",
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(390, 18)
            };
            currentFileLabel = new Guna2HtmlLabel
            {
                ForeColor = Color.Silver,
                BackColor = Color.Transparent,
                Location = new Point(18, 45),
                AutoSize = true
            };
            restoreBtn = CreatePrimaryButton("Восстановить");
            restoreBtn.Location = new Point(900, 15);
            restoreBtn.Size = new Size(170, 38);
            restoreBtn.Click += RestoreSelected;

            cancelBtn = CreateSecondaryButton("Отмена");
            cancelBtn.Location = new Point(1080, 15);
            cancelBtn.Size = new Size(120, 38);
            cancelBtn.Visible = false;
            cancelBtn.Click += CancelRestore;

            closeBtn = CreateSecondaryButton("Закрыть");
            closeBtn.Location = new Point(1210, 15);
            closeBtn.Size = new Size(100, 38);
            closeBtn.Click += (s, e) => Close();

            bottomPanel.Controls.Add(progressBar);
            bottomPanel.Controls.Add(statusLabel);
            bottomPanel.Controls.Add(currentFileLabel);
            bottomPanel.Controls.Add(restoreBtn);
            bottomPanel.Controls.Add(cancelBtn);
            bottomPanel.Controls.Add(closeBtn);
        }

        private Guna2Panel CreateCard()
        {
            return new Guna2Panel
            {
                Dock = DockStyle.Fill,
                BorderRadius = 18,
                FillColor = Color.FromArgb(30, 30, 35)
            };
        }

        private Label CreateTitle(string text)
        {
            return new Label
            {
                Text = text,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(18, 18)
            };
        }

        private Guna2Button CreatePrimaryButton(string text)
        {
            return new Guna2Button
            {
                Text = text,
                BorderRadius = 12,
                FillColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
        }

        private Guna2Button CreateSecondaryButton(string text)
        {
            return new Guna2Button
            {
                Text = text,
                BorderRadius = 12,
                FillColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
        }

        // =====================================================
        // LOAD BACKUPS
        // =====================================================
        private void LoadBackups()
        {
            string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
            backupTree.Nodes.Clear();

            if (!Directory.Exists(backupsRoot))
                return;

            var allDirs = Directory.GetDirectories(backupsRoot, "*_*");
            string filterType = backupTypeFilter.SelectedItem?.ToString();

            foreach (string dir in allDirs)
            {
                string dirName = Path.GetFileName(dir);
                if (filterType != "Все" && !dirName.StartsWith(filterType))
                    continue;

                TreeNode node = new TreeNode(dirName);
                node.Tag = dir;
                node.Nodes.Add("loading");
                backupTree.Nodes.Add(node);
            }
        }

        private void BackupTree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "loading")
            {
                e.Node.Nodes.Clear();
                string path = e.Node.Tag.ToString();

                foreach (string dir in Directory.GetDirectories(path))
                {
                    TreeNode node = new TreeNode(Path.GetFileName(dir));
                    node.Tag = dir;
                    node.Nodes.Add("loading");
                    e.Node.Nodes.Add(node);
                }
                foreach (string file in Directory.GetFiles(path))
                {
                    TreeNode node = new TreeNode(Path.GetFileName(file));
                    node.Tag = file;
                    e.Node.Nodes.Add(node);
                }
            }
        }

        private void BackupTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // Определяем корневую папку бэкапа (ту, что содержит backup_meta.json)
            TreeNode node = e.Node;
            while (node.Parent != null)
                node = node.Parent;

            string backupRoot = node.Tag?.ToString();
            if (string.IsNullOrEmpty(backupRoot) || !File.Exists(Path.Combine(backupRoot, "backup_meta.json")))
            {
                _selectedBackupRoot = null;
                selectedCountLabel.Text = "Выбрана резервная копия: нет (некорректная)";
                infoLabel.Text = "Выбранный узел не является действительной резервной копией.";
                return;
            }

            _selectedBackupRoot = backupRoot;
            var meta = BackupMeta.Load(Path.Combine(backupRoot, "backup_meta.json"));
            string info = $"Папка: {Path.GetFileName(backupRoot)}\nТип: {meta?.BackupType ?? "Неизвестно"}\nДата: {meta?.BackupTime:yyyy-MM-dd HH:mm:ss}\nФайлов: {meta?.Files.Count ?? 0}";
            infoLabel.Text = info;
            selectedCountLabel.Text = $"Выбрана резервная копия: {Path.GetFileName(backupRoot)}";
        }

        private void ApplyFilter()
        {
            string filter = filterBox.Text.Trim().ToLower();
            foreach (TreeNode node in backupTree.Nodes)
            {
                node.BackColor = node.Text.ToLower().Contains(filter)
                    ? Color.FromArgb(60, 60, 65)
                    : Color.Transparent;
            }
        }

        // =====================================================
        // RESTORE
        // =====================================================
        private async void RestoreSelected(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedBackupRoot))
            {
                MessageBox.Show("Сначала выберите резервную копию в дереве слева.", "Восстановление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string targetPath = null;
            if (restoreToOriginalCheckBox.Checked)
            {
                // оригинальные папки определяются автоматически внутри BackupManager.RestoreFromBackup
                targetPath = null; // специальный маркер для восстановления в оригинал
            }
            else
            {
                if (string.IsNullOrWhiteSpace(customRestorePath.Text) || !Directory.Exists(customRestorePath.Text))
                {
                    MessageBox.Show("Укажите существующую папку для восстановления.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                targetPath = customRestorePath.Text;
            }

            try
            {
                restoreBtn.Enabled = false;
                cancelBtn.Visible = true;
                _cts = new CancellationTokenSource();
                progressBar.Value = 0;
                progressBar.Visible = true;
                statusLabel.Text = "Восстановление...";
                logBox.Clear();

                // Подписываемся на прогресс
                BackupManager.ProgressChanged += OnRestoreProgress;
                await BackupManager.RestoreFromBackup(_selectedBackupRoot, targetPath, overwriteCheckBox.Checked, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                Log("Операция восстановления отменена пользователем.");
                MessageBox.Show("Восстановление отменено.", "Отмена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"ОШИБКА: {ex.Message}");
                MessageBox.Show($"Ошибка восстановления: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                BackupManager.ProgressChanged -= OnRestoreProgress;
                restoreBtn.Enabled = true;
                cancelBtn.Visible = false;
                cancelBtn.Enabled = true;
                _cts?.Dispose();
                _cts = null;
                progressBar.Visible = false;
                statusLabel.Text = "Готов";
                currentFileLabel.Text = "";
            }
        }

        private void CancelRestore(object sender, EventArgs e)
        {
            _cts?.Cancel();
            cancelBtn.Enabled = false;
            cancelBtn.Text = "Отмена...";
        }

        private void OnRestoreProgress(int percent, string fileName)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnRestoreProgress(percent, fileName));
                return;
            }
            progressBar.Value = percent;
            currentFileLabel.Text = $"{percent}% - {Path.GetFileName(fileName)}";
            if (percent == 100)
                currentFileLabel.Text = "";
        }

        private void Log(string text)
        {
            if (InvokeRequired)
            {
                Invoke(() => Log(text));
                return;
            }
            logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        }

        private void BrowseCustomPathBtn_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                    customRestorePath.Text = dialog.SelectedPath;
            }
        }

        // =====================================================
        // DARK THEME SUPPORT
        // =====================================================
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        private void EnableDarkTitleBar()
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int dark = 1;
                DwmSetWindowAttribute(this.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);
        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(int appMode);
        [DllImport("uxtheme.dll", EntryPoint = "#133")]
        private static extern int AllowDarkModeForWindow(IntPtr hWnd, bool allow);

        private void EnableDarkScrollBar(Control control)
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                SetPreferredAppMode(2);
                AllowDarkModeForWindow(control.Handle, true);
                SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            }
        }
    }
}