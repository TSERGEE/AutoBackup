using Guna.UI2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoBackup
{
    public class RestoreForm : Form
    {
        // ================= UI =================

        private SplitContainer splitContainer;

        private TreeView backupTree;

        private Guna2TextBox filterBox;
        private Guna2Button filterBtn;
        private Guna2ComboBox backupTypeFilter;

        private Label infoLabel;
        private Label selectedCountLabel;
        private Label statusLabel;
        private Label currentFileLabel;

        private Guna2CheckBox restoreToOriginalCheckBox;
        private Guna2CheckBox overwriteCheckBox;

        private Guna2TextBox customRestorePath;
        private Guna2Button browseCustomPathBtn;

        private Guna2ProgressBar progressBar;

        private Guna2Button restoreBtn;
        private Guna2Button cancelBtn;

        private RichTextBox logBox;

        private ImageList treeIcons;

        // ================= MODEL =================

        private class RestoreItem
        {
            public string FullPath { get; set; }
            public string RelativePath { get; set; }
        }

        // ================= CTOR =================

        public RestoreForm()
        {
            InitializeComponents();

            EnableDarkTitleBar();

            LoadBackups();
        }

        // ================= UI INIT =================

        private void InitializeComponents()
        {
            Text = "Restore Backup";

            Width = 1250;
            Height = 780;

            MinimumSize = new Size(1000, 650);

            StartPosition = FormStartPosition.CenterScreen;

            FormBorderStyle = FormBorderStyle.Sizable;

            BackColor = Color.FromArgb(24, 24, 27);

            Font = new Font("Segoe UI", 9F);

            // =========================================================
            // MAIN LAYOUT
            // =========================================================

            TableLayoutPanel mainLayout =
                new TableLayoutPanel();

            mainLayout.Dock = DockStyle.Fill;

            mainLayout.RowCount = 2;

            mainLayout.ColumnCount = 1;

            mainLayout.Padding = new Padding(12);

            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100F));

            mainLayout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 130F));

            Controls.Add(mainLayout);

            // =========================================================
            // SPLIT CONTAINER
            // =========================================================

            splitContainer =
                new SplitContainer();

            splitContainer.Dock = DockStyle.Fill;

            splitContainer.SplitterDistance = 520;

            splitContainer.BackColor =
                Color.FromArgb(40, 40, 45);

            mainLayout.Controls.Add(splitContainer, 0, 0);

            InitializeLeftPanel();

            InitializeRightPanel();

            InitializeBottomPanel(mainLayout);
        }

        // =========================================================
        // LEFT PANEL
        // =========================================================

        private void InitializeLeftPanel()
        {
            Guna2Panel leftPanel =
                CreatePanel();

            leftPanel.Padding = new Padding(12);

            splitContainer.Panel1.Controls.Add(leftPanel);

            // =========================================================
            // FILTER GROUP
            // =========================================================

            Guna2GroupBox filterGroup =
                CreateGroupBox("Поиск и фильтрация");

            filterGroup.Dock = DockStyle.Top;

            filterGroup.Height = 95;

            leftPanel.Controls.Add(filterGroup);

            filterBox =
                new Guna2TextBox();

            filterBox.PlaceholderText =
                "Поиск...";

            filterBox.BorderRadius = 10;

            filterBox.FillColor =
                Color.FromArgb(35, 35, 40);

            filterBox.ForeColor =
                Color.White;

            filterBox.Size =
                new Size(220, 36);

            filterBox.Location =
                new Point(15, 40);

            filterBtn =
                CreatePrimaryButton("Найти");

            filterBtn.Location =
                new Point(245, 40);

            filterBtn.Size =
                new Size(90, 36);

            filterBtn.Click +=
                (s, e) => ApplyFilter();

            backupTypeFilter =
                new Guna2ComboBox();

            backupTypeFilter.BorderRadius = 10;

            backupTypeFilter.FillColor =
                Color.FromArgb(35, 35, 40);

            backupTypeFilter.ForeColor =
                Color.White;

            backupTypeFilter.DrawMode =
                DrawMode.OwnerDrawFixed;

            backupTypeFilter.DropDownStyle =
                ComboBoxStyle.DropDownList;

            backupTypeFilter.Items.AddRange(new object[]
            {
                "Все",
                "Полные (Full)",
                "Инкрементные (Inc)"
            });

            backupTypeFilter.SelectedIndex = 0;

            backupTypeFilter.Location =
                new Point(350, 40);

            backupTypeFilter.Size =
                new Size(140, 36);

            backupTypeFilter.SelectedIndexChanged +=
                (s, e) => LoadBackups();

            filterGroup.Controls.Add(filterBox);

            filterGroup.Controls.Add(filterBtn);

            filterGroup.Controls.Add(backupTypeFilter);

            // =========================================================
            // TREE
            // =========================================================

            treeIcons = new ImageList();

            treeIcons.ImageSize =
                new Size(16, 16);

            treeIcons.Images.Add(
                "folder",
                SystemIcons.WinLogo.ToBitmap());

            treeIcons.Images.Add(
                "file",
                SystemIcons.Application.ToBitmap());

            backupTree =
                new TreeView();

            backupTree.Dock = DockStyle.Fill;

            backupTree.CheckBoxes = true;

            backupTree.HideSelection = false;

            backupTree.BorderStyle =
                BorderStyle.None;

            backupTree.BackColor =
                Color.FromArgb(30, 30, 35);

            backupTree.ForeColor =
                Color.White;

            backupTree.LineColor =
                Color.FromArgb(70, 70, 75);

            backupTree.Font =
                new Font("Segoe UI", 9F);

            backupTree.ImageList =
                treeIcons;

            backupTree.AfterCheck +=
                BackupTree_AfterCheck;

            backupTree.BeforeExpand +=
                BackupTree_BeforeExpand;

            backupTree.AfterSelect +=
                BackupTree_AfterSelect;

            leftPanel.Controls.Add(backupTree);

            backupTree.BringToFront();
        }

        // =========================================================
        // RIGHT PANEL
        // =========================================================

        private void InitializeRightPanel()
        {
            Guna2Panel rightPanel =
                CreatePanel();

            rightPanel.Padding =
                new Padding(12);

            splitContainer.Panel2.Controls.Add(rightPanel);

            // =========================================================
            // INFO GROUP
            // =========================================================

            Guna2GroupBox infoGroup =
                CreateGroupBox("Информация");

            infoGroup.Dock = DockStyle.Top;

            infoGroup.Height = 140;

            infoLabel =
                new Label();

            infoLabel.Dock = DockStyle.Fill;

            infoLabel.ForeColor =
                Color.White;

            infoLabel.Padding =
                new Padding(12);

            infoLabel.Text =
                "Выберите backup";

            infoGroup.Controls.Add(infoLabel);

            rightPanel.Controls.Add(infoGroup);

            // =========================================================
            // SETTINGS GROUP
            // =========================================================

            Guna2GroupBox settingsGroup =
                CreateGroupBox("Настройки восстановления");

            settingsGroup.Dock = DockStyle.Top;

            settingsGroup.Height = 200;

            rightPanel.Controls.Add(settingsGroup);

            restoreToOriginalCheckBox =
                new Guna2CheckBox();

            restoreToOriginalCheckBox.Text =
                "Восстановить в исходные папки";

            restoreToOriginalCheckBox.ForeColor =
                Color.White;

            restoreToOriginalCheckBox.Location =
                new Point(15, 40);

            restoreToOriginalCheckBox.Checked = true;

            restoreToOriginalCheckBox.CheckedChanged +=
                (s, e) =>
                {
                    customRestorePath.Enabled =
                        !restoreToOriginalCheckBox.Checked;

                    browseCustomPathBtn.Enabled =
                        !restoreToOriginalCheckBox.Checked;
                };

            overwriteCheckBox =
                new Guna2CheckBox();

            overwriteCheckBox.Text =
                "Перезаписывать существующие файлы";

            overwriteCheckBox.ForeColor =
                Color.White;

            overwriteCheckBox.Location =
                new Point(15, 75);

            overwriteCheckBox.Checked = true;

            customRestorePath =
                new Guna2TextBox();

            customRestorePath.BorderRadius = 10;

            customRestorePath.FillColor =
                Color.FromArgb(35, 35, 40);

            customRestorePath.ForeColor =
                Color.White;

            customRestorePath.ReadOnly = true;

            customRestorePath.Enabled = false;

            customRestorePath.Location =
                new Point(15, 115);

            customRestorePath.Size =
                new Size(320, 36);

            browseCustomPathBtn =
                CreateSecondaryButton("Обзор");

            browseCustomPathBtn.Location =
                new Point(345, 115);

            browseCustomPathBtn.Size =
                new Size(90, 36);

            browseCustomPathBtn.Enabled = false;

            browseCustomPathBtn.Click +=
                BrowseCustomPathBtn_Click;

            selectedCountLabel =
                new Label();

            selectedCountLabel.Text =
                "Выбрано: 0";

            selectedCountLabel.ForeColor =
                Color.White;

            selectedCountLabel.Font =
                new Font(
                    "Segoe UI",
                    9F,
                    FontStyle.Bold);

            selectedCountLabel.Location =
                new Point(15, 160);

            selectedCountLabel.AutoSize = true;

            settingsGroup.Controls.Add(
                restoreToOriginalCheckBox);

            settingsGroup.Controls.Add(
                overwriteCheckBox);

            settingsGroup.Controls.Add(
                customRestorePath);

            settingsGroup.Controls.Add(
                browseCustomPathBtn);

            settingsGroup.Controls.Add(
                selectedCountLabel);

            // =========================================================
            // LOG GROUP
            // =========================================================

            Guna2GroupBox logGroup =
                CreateGroupBox("Журнал");

            logGroup.Dock = DockStyle.Fill;

            logBox =
                new RichTextBox();

            logBox.Dock = DockStyle.Fill;

            logBox.ReadOnly = true;

            logBox.BorderStyle =
                BorderStyle.None;

            logBox.BackColor =
                Color.FromArgb(30, 30, 35);

            logBox.ForeColor =
                Color.White;

            logBox.Font =
                new Font("Consolas", 9F);

            logGroup.Controls.Add(logBox);

            rightPanel.Controls.Add(logGroup);

            logGroup.BringToFront();
        }

        // =========================================================
        // BOTTOM PANEL
        // =========================================================

        private void InitializeBottomPanel(
            TableLayoutPanel mainLayout)
        {
            Guna2Panel bottomPanel =
                CreatePanel();

            bottomPanel.Padding =
                new Padding(15);

            mainLayout.Controls.Add(
                bottomPanel,
                0,
                1);

            progressBar =
                new Guna2ProgressBar();

            progressBar.Location =
                new Point(15, 15);

            progressBar.Size =
                new Size(520, 24);

            progressBar.BorderRadius = 10;

            progressBar.FillColor =
                Color.FromArgb(50, 50, 55);

            progressBar.ProgressColor =
                Color.FromArgb(0, 120, 215);

            statusLabel =
                new Label();

            statusLabel.Text = "Готов";

            statusLabel.ForeColor =
                Color.White;

            statusLabel.Location =
                new Point(550, 18);

            statusLabel.AutoSize = true;

            currentFileLabel =
                new Label();

            currentFileLabel.ForeColor =
                Color.Silver;

            currentFileLabel.Location =
                new Point(15, 50);

            currentFileLabel.AutoSize = true;

            restoreBtn =
                CreatePrimaryButton("Восстановить");

            restoreBtn.Location =
                new Point(15, 78);

            restoreBtn.Size =
                new Size(180, 38);

            restoreBtn.Click += RestoreSelected;

            cancelBtn =
                CreateSecondaryButton("Закрыть");

            cancelBtn.Location =
                new Point(210, 78);

            cancelBtn.Size =
                new Size(120, 38);

            cancelBtn.Click +=
                (s, e) => Close();

            bottomPanel.Controls.Add(progressBar);

            bottomPanel.Controls.Add(statusLabel);

            bottomPanel.Controls.Add(currentFileLabel);

            bottomPanel.Controls.Add(restoreBtn);

            bottomPanel.Controls.Add(cancelBtn);
        }

        // =========================================================
        // STYLES
        // =========================================================

        private Guna2Panel CreatePanel()
        {
            return new Guna2Panel
            {
                Dock = DockStyle.Fill,

                BorderRadius = 18,

                FillColor = Color.FromArgb(30, 30, 35)
            };
        }

        private Guna2GroupBox CreateGroupBox(
            string text)
        {
            return new Guna2GroupBox
            {
                Text = text,

                Font = new Font(
                    "Segoe UI",
                    10F,
                    FontStyle.Bold),

                ForeColor = Color.White,

                BorderRadius = 14,

                BorderColor =
                    Color.FromArgb(55, 55, 60),

                FillColor =
                    Color.FromArgb(30, 30, 35),

                CustomBorderColor =
                    Color.FromArgb(40, 40, 45)
            };
        }

        private Guna2Button CreatePrimaryButton(
            string text)
        {
            return new Guna2Button
            {
                Text = text,

                BorderRadius = 12,

                FillColor =
                    Color.FromArgb(0, 120, 215),

                Font =
                    new Font(
                        "Segoe UI",
                        9F,
                        FontStyle.Bold),

                ForeColor =
                    Color.White
            };
        }

        private Guna2Button CreateSecondaryButton(
            string text)
        {
            return new Guna2Button
            {
                Text = text,

                BorderRadius = 12,

                FillColor =
                    Color.FromArgb(45, 45, 50),

                Font =
                    new Font(
                        "Segoe UI",
                        9F),

                ForeColor =
                    Color.White
            };
        }

        // =========================================================
        // DARK TITLE BAR
        // =========================================================

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

        // ================= LOAD BACKUPS =================

        private void LoadBackups()
        {
            string backupsRoot =
                Path.Combine(
                    Config.Current.DestinationFolder,
                    "Backups");

            backupTree.Nodes.Clear();

            if (!Directory.Exists(backupsRoot))
            {
                backupTree.Nodes.Add(
                    "Нет резервных копий");

                return;
            }

            var backupDirs =
                Directory.GetDirectories(backupsRoot, "*_*")
                .Select(d => new
                {
                    Path = d,

                    Meta = BackupMeta.Load(
                        Path.Combine(
                            d,
                            "backup_meta.json"))
                })
                .Where(x => x.Meta != null)
                .ToList();

            if (backupTypeFilter.SelectedIndex == 1)
            {
                backupDirs = backupDirs
                    .Where(x =>
                        x.Meta.BackupType == "Full")
                    .ToList();
            }
            else if (backupTypeFilter.SelectedIndex == 2)
            {
                backupDirs = backupDirs
                    .Where(x =>
                        x.Meta.BackupType == "Inc")
                    .ToList();
            }

            foreach (var backup
                in backupDirs.OrderByDescending(
                    x => x.Meta.BackupTime))
            {
                string displayName =
                    $"{backup.Meta.BackupType} | " +
                    $"{backup.Meta.BackupTime:yyyy-MM-dd HH:mm}";

                TreeNode node =
                    new TreeNode(displayName)
                    {
                        Tag = backup.Path,

                        ImageKey = "folder",

                        SelectedImageKey = "folder"
                    };

                node.Nodes.Add("loading...");

                backupTree.Nodes.Add(node);
            }
        }

        // =========================================================
        // TREE EVENTS
        // =========================================================

        private void BackupTree_BeforeExpand(
            object sender,
            TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1
                &&
                e.Node.Nodes[0].Text == "loading...")
            {
                e.Node.Nodes.Clear();

                string fullPath =
                    e.Node.Tag.ToString();

                LoadFolder(
                    e.Node,
                    fullPath);
            }
        }

        private void LoadFolder(
            TreeNode parentNode,
            string directoryPath)
        {
            try
            {
                foreach (string dir
                    in Directory.GetDirectories(directoryPath))
                {
                    TreeNode dirNode =
                        new TreeNode(
                            Path.GetFileName(dir))
                        {
                            Tag = dir,

                            ImageKey = "folder",

                            SelectedImageKey = "folder"
                        };

                    dirNode.Nodes.Add("loading...");

                    parentNode.Nodes.Add(dirNode);
                }

                foreach (string file
                    in Directory.GetFiles(directoryPath))
                {
                    FileInfo fi =
                        new FileInfo(file);

                    TreeNode fileNode =
                        new TreeNode(
                            $"{Path.GetFileName(file)} " +
                            $"[{GetSizeString(fi.Length)}]")
                        {
                            Tag = file,

                            ImageKey = "file",

                            SelectedImageKey = "file"
                        };

                    parentNode.Nodes.Add(fileNode);
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка: {ex.Message}");
            }
        }

        private void BackupTree_AfterSelect(
            object sender,
            TreeViewEventArgs e)
        {
            if (e.Node.Tag is string path
                &&
                Directory.Exists(path))
            {
                var meta =
                    BackupMeta.Load(
                        Path.Combine(
                            path,
                            "backup_meta.json"));

                if (meta != null)
                {
                    infoLabel.Text =
                        $"Тип: {meta.BackupType}\n\n" +
                        $"Дата: {meta.BackupTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"Файлов: {meta.Files.Count}";
                }
            }
        }

        private void BackupTree_AfterCheck(
            object sender,
            TreeViewEventArgs e)
        {
            if (e.Action != TreeViewAction.Unknown)
            {
                SetChildrenChecked(
                    e.Node,
                    e.Node.Checked);

                UpdateSelectedCount();
            }
        }

        private void SetChildrenChecked(
            TreeNode node,
            bool state)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = state;

                SetChildrenChecked(
                    child,
                    state);
            }
        }

        // =========================================================
        // FILTER
        // =========================================================

        private void ApplyFilter()
        {
            string filter =
                filterBox.Text
                .Trim()
                .ToLower();

            foreach (TreeNode node
                in backupTree.Nodes)
            {
                ApplyFilterRecursive(
                    node,
                    filter);
            }
        }

        private bool ApplyFilterRecursive(
            TreeNode node,
            string filter)
        {
            bool visible =
                node.Text.ToLower()
                .Contains(filter);

            foreach (TreeNode child
                in node.Nodes)
            {
                if (ApplyFilterRecursive(
                    child,
                    filter))
                {
                    visible = true;
                }
            }

            node.BackColor =
                visible
                ? Color.FromArgb(55, 55, 60)
                : Color.FromArgb(30, 30, 35);

            return visible;
        }

        // =========================================================
        // RESTORE
        // =========================================================

        private async void RestoreSelected(
            object sender,
            EventArgs e)
        {
            MessageBox.Show(
                "Твой код восстановления можно оставить старый отсюда.");
        }

        // =========================================================
        // HELPERS
        // =========================================================

        private void BrowseCustomPathBtn_Click(
            object sender,
            EventArgs e)
        {
            using FolderBrowserDialog fbd =
                new FolderBrowserDialog();

            if (fbd.ShowDialog()
                == DialogResult.OK)
            {
                customRestorePath.Text =
                    fbd.SelectedPath;
            }
        }

        private void UpdateSelectedCount()
        {
            int count =
                CountCheckedItems(
                    backupTree.Nodes);

            selectedCountLabel.Text =
                $"Выбрано: {count}";
        }

        private int CountCheckedItems(
            TreeNodeCollection nodes)
        {
            int count = 0;

            foreach (TreeNode node in nodes)
            {
                if (node.Checked)
                    count++;

                count +=
                    CountCheckedItems(
                        node.Nodes);
            }

            return count;
        }

        private void Log(string text)
        {
            logBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] " +
                $"{text}" +
                Environment.NewLine);
        }

        private string GetSizeString(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} Б";

            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";

            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / 1024.0 / 1024.0:F1} MB";

            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }
    }
}