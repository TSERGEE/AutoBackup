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

        private Guna2Button closeBtn;

        private RichTextBox logBox;

        // =====================================================
        // MODEL
        // =====================================================

        private class RestoreItem
        {
            public string FullPath { get; set; }

            public string RelativePath { get; set; }
        }

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
            Text = "Restore Backup";

            Width = 1400;

            Height = 850;

            MinimumSize = new Size(1200, 700);

            StartPosition = FormStartPosition.CenterScreen;

            BackColor = Color.FromArgb(24, 24, 27);

            Font = new Font("Segoe UI", 9F);

            FormBorderStyle = FormBorderStyle.Sizable;

            // =================================================
            // MAIN LAYOUT
            // =================================================

            TableLayoutPanel layout =
                new TableLayoutPanel();

            layout.Dock = DockStyle.Fill;

            layout.RowCount = 2;

            layout.ColumnCount = 1;

            layout.Padding = new Padding(12);

            layout.RowStyles.Add(
                new RowStyle(SizeType.Percent, 100));

            layout.RowStyles.Add(
                new RowStyle(SizeType.Absolute, 78));

            Controls.Add(layout);

            // =================================================
            // CONTENT LAYOUT
            // =================================================

            TableLayoutPanel contentLayout =
                new TableLayoutPanel();

            contentLayout.Dock = DockStyle.Fill;

            contentLayout.ColumnCount = 2;

            contentLayout.RowCount = 1;

            contentLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 72));

            contentLayout.ColumnStyles.Add(
                new ColumnStyle(SizeType.Percent, 28));

            layout.Controls.Add(contentLayout);

            // =================================================
            // LEFT CARD
            // =================================================

            Guna2Panel leftCard =
                CreateCard();

            leftCard.Padding =
                new Padding(18);

            contentLayout.Controls.Add(leftCard, 0, 0);

            // =================================================
            // FILTER PANEL
            // =================================================

            Panel filterPanel =
                new Panel();

            filterPanel.Dock =
                DockStyle.Top;

            filterPanel.Height = 65;

            leftCard.Controls.Add(filterPanel);

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
                new Size(250, 36);

            filterBox.Location =
                new Point(0, 5);

            Guna2Button searchBtn =
                CreatePrimaryButton("Найти");

            searchBtn.Location =
                new Point(265, 5);

            searchBtn.Size = new Size(100, 36);

            searchBtn.Click += (s, e) => ApplyFilter();

            backupTypeFilter =
                new Guna2ComboBox();

            backupTypeFilter.Items.AddRange(
                new object[]
                {
                    "Все",
                    "Full",
                    "Inc"
                });

            backupTypeFilter.SelectedIndex = 0;

            backupTypeFilter.BorderRadius = 10;

            backupTypeFilter.FillColor =
                Color.FromArgb(35, 35, 40);

            backupTypeFilter.ForeColor =
                Color.White;

            backupTypeFilter.Size =
                new Size(170, 36);

            backupTypeFilter.Location =
                new Point(380, 5);

            backupTypeFilter.SelectedIndexChanged +=
                (s, e) => LoadBackups();

            filterPanel.Controls.Add(filterBox);

            filterPanel.Controls.Add(searchBtn);

            filterPanel.Controls.Add(backupTypeFilter);

            // =================================================
            // TREE CONTAINER
            // =================================================

            Panel treeContainer =
                new Panel();

            treeContainer.Dock =
                DockStyle.Fill;

            treeContainer.Padding =
                new Padding(0, 10, 0, 0);

            leftCard.Controls.Add(treeContainer);

            treeContainer.BringToFront();

            // =================================================
            // TREEVIEW
            // =================================================

            backupTree =
                new TreeView();

            backupTree.Dock =
                DockStyle.Fill;

            backupTree.BackColor =
                Color.FromArgb(24, 24, 27);

            backupTree.ForeColor =
                Color.White;

            backupTree.BorderStyle =
                BorderStyle.None;

            backupTree.Font =
                new Font("Segoe UI", 9F);

            backupTree.CheckBoxes = true;

            backupTree.HideSelection = false;

            backupTree.LineColor =
                Color.FromArgb(60, 60, 65);

            backupTree.ItemHeight = 24;

            backupTree.DrawMode =
                TreeViewDrawMode.OwnerDrawText;

            backupTree.DrawNode += (s, e) =>
            {
                Color bg =
                    e.Node.IsSelected
                    ? Color.FromArgb(0, 120, 215)
                    : Color.FromArgb(24, 24, 27);

                using SolidBrush back =
                    new SolidBrush(bg);

                using SolidBrush fore =
                    new SolidBrush(Color.White);

                e.Graphics.FillRectangle(
                    back,
                    e.Bounds);

                e.Graphics.DrawString(
                    e.Node.Text,
                    backupTree.Font,
                    fore,
                    e.Bounds.Location);
            };

            backupTree.AfterCheck +=
                BackupTree_AfterCheck;

            backupTree.BeforeExpand +=
                BackupTree_BeforeExpand;

            backupTree.AfterSelect +=
                BackupTree_AfterSelect;
            EnableDarkScrollBar(backupTree);

            treeContainer.Controls.Add(backupTree);

            // =================================================
            // RIGHT PANEL
            // =================================================

            Panel rightPanel =
                new Panel();

            rightPanel.Dock =
                DockStyle.Fill;

            rightPanel.BackColor =
                Color.Transparent;

            contentLayout.Controls.Add(
                rightPanel,
                1,
                0);

            // =================================================
            // SETTINGS CARD
            // =================================================

            Guna2Panel settingsCard =
                CreateCard();

            settingsCard.Dock =
                DockStyle.Top;

            settingsCard.Height = 230;

            settingsCard.Padding =
                new Padding(18);

            rightPanel.Controls.Add(settingsCard);

            Label settingsTitle =
                CreateTitle("Настройки");

            settingsCard.Controls.Add(settingsTitle);

            restoreToOriginalCheckBox =
                new Guna2CheckBox();

            restoreToOriginalCheckBox.Text =
                "Восстановить в оригинал";

            restoreToOriginalCheckBox.Checked = true;

            restoreToOriginalCheckBox.ForeColor =
                Color.White;

            restoreToOriginalCheckBox.BackColor =
                Color.Transparent;

            restoreToOriginalCheckBox.Location =
                new Point(18, 50);

            overwriteCheckBox =
                new Guna2CheckBox();

            overwriteCheckBox.Text =
                "Перезаписывать файлы";

            overwriteCheckBox.Checked = true;

            overwriteCheckBox.ForeColor =
                Color.White;

            overwriteCheckBox.BackColor =
                Color.Transparent;

            overwriteCheckBox.Location =
                new Point(18, 85);

            customRestorePath =
                new Guna2TextBox();

            customRestorePath.PlaceholderText =
                "Папка восстановления";

            customRestorePath.BorderRadius = 10;

            customRestorePath.FillColor =
                Color.FromArgb(35, 35, 40);

            customRestorePath.ForeColor =
                Color.White;

            customRestorePath.Location =
                new Point(18, 125);

            customRestorePath.Size =
                new Size(220, 36);

            customRestorePath.Enabled = false;

            browseCustomPathBtn =
                CreateSecondaryButton("Обзор");

            browseCustomPathBtn.Location =
                new Point(245, 125);

            browseCustomPathBtn.Size =
                new Size(75, 36);

            browseCustomPathBtn.Enabled = false;

            browseCustomPathBtn.Click +=
                BrowseCustomPathBtn_Click;

            restoreToOriginalCheckBox.CheckedChanged +=
                (s, e) =>
                {
                    customRestorePath.Enabled =
                        !restoreToOriginalCheckBox.Checked;

                    browseCustomPathBtn.Enabled =
                        !restoreToOriginalCheckBox.Checked;
                };

            selectedCountLabel =
                new Guna2HtmlLabel();

            selectedCountLabel.Text =
                "Выбрано: 0";

            selectedCountLabel.ForeColor =
                Color.White;

            selectedCountLabel.BackColor =
                Color.Transparent;

            selectedCountLabel.Location =
                new Point(18, 180);

            settingsCard.Controls.Add(
                restoreToOriginalCheckBox);

            settingsCard.Controls.Add(
                overwriteCheckBox);

            settingsCard.Controls.Add(
                customRestorePath);

            settingsCard.Controls.Add(
                browseCustomPathBtn);

            settingsCard.Controls.Add(
                selectedCountLabel);

            // =================================================
            // INFO CARD
            // =================================================

            Guna2Panel infoCard =
                CreateCard();

            infoCard.Dock =
                DockStyle.Top;

            infoCard.Height = 170;

            infoCard.Padding =
                new Padding(18);

            infoCard.Margin =
                new Padding(0, 12, 0, 12);

            rightPanel.Controls.Add(infoCard);

            infoCard.BringToFront();

            Label infoTitle =
                CreateTitle("Информация");

            infoCard.Controls.Add(infoTitle);

            infoLabel =
                new Guna2HtmlLabel();

            infoLabel.Text =
                "Выберите backup";

            infoLabel.ForeColor =
                Color.White;

            infoLabel.BackColor =
                Color.Transparent;

            infoLabel.Location =
                new Point(18, 55);

            infoLabel.Size =
                new Size(280, 100);

            infoCard.Controls.Add(infoLabel);

            // =================================================
            // LOG CARD
            // =================================================

            Guna2Panel logCard =
                CreateCard();

            logCard.Dock =
                DockStyle.Fill;

            logCard.Padding =
                new Padding(18);

            rightPanel.Controls.Add(logCard);

            logCard.BringToFront();

            Label logTitle =
                CreateTitle("Журнал");

            logCard.Controls.Add(logTitle);

            logBox =
                new RichTextBox();

            logBox.Location =
                new Point(18, 50);

            logBox.Anchor =
                AnchorStyles.Top |
                AnchorStyles.Bottom |
                AnchorStyles.Left |
                AnchorStyles.Right;

            logBox.Size =
                new Size(300, 300);

            logBox.BackColor =
                Color.FromArgb(22, 22, 28);

            logBox.ForeColor =
                Color.White;

            logBox.BorderStyle =
                BorderStyle.None;

            logBox.ReadOnly = true;

            logCard.Controls.Add(logBox);

            // =================================================
            // BOTTOM PANEL
            // =================================================

            Guna2Panel bottomPanel =
                CreateCard();

            bottomPanel.Padding =
                new Padding(18);

            layout.Controls.Add(bottomPanel);

            progressBar =
                new Guna2ProgressBar();

            progressBar.Location =
                new Point(18, 20);

            progressBar.Size =
                new Size(350, 18);

            progressBar.BorderRadius = 8;

            progressBar.FillColor =
                Color.FromArgb(50, 50, 55);

            progressBar.ProgressColor =
                Color.FromArgb(0, 120, 215);

            statusLabel =
                new Guna2HtmlLabel();

            statusLabel.Text =
                "Готов";

            statusLabel.ForeColor =
                Color.White;

            statusLabel.BackColor =
                Color.Transparent;

            statusLabel.Location =
                new Point(390, 18);

            currentFileLabel =
                new Guna2HtmlLabel();

            currentFileLabel.ForeColor =
                Color.Silver;

            currentFileLabel.BackColor =
                Color.Transparent;

            currentFileLabel.Location =
                new Point(18, 45);

            restoreBtn =
                CreatePrimaryButton(
                    "Восстановить");

            restoreBtn.Location =
                new Point(900, 15);

            restoreBtn.Size =
                new Size(170, 38);

            restoreBtn.Click +=
                RestoreSelected;

            closeBtn =
                CreateSecondaryButton(
                    "Закрыть");

            closeBtn.Location =
                new Point(1080, 15);

            closeBtn.Size =
                new Size(120, 38);

            closeBtn.Click +=
                (s, e) => Close();

            bottomPanel.Controls.Add(progressBar);

            bottomPanel.Controls.Add(statusLabel);

            bottomPanel.Controls.Add(currentFileLabel);

            bottomPanel.Controls.Add(restoreBtn);

            bottomPanel.Controls.Add(closeBtn);
        }

        // =====================================================
        // CARD
        // =====================================================

        private Guna2Panel CreateCard()
        {
            return new Guna2Panel
            {
                Dock = DockStyle.Fill,

                BorderRadius = 18,

                FillColor = Color.FromArgb(30, 30, 35),

                //Margin = new Padding(0, 0, 12, 12)
            };
        }

        // =====================================================
        // TITLE
        // =====================================================

        private Label CreateTitle(string text)
        {
            return new Label
            {
                Text = text,

                ForeColor = Color.White,

                Font = new Font(
                    "Segoe UI",
                    11F,
                    FontStyle.Bold),

                AutoSize = true,

                Location = new Point(18, 18)
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

                BorderRadius = 12,

                FillColor =
                    Color.FromArgb(0, 120, 215),

                ForeColor =
                    Color.White,

                Font =
                    new Font(
                        "Segoe UI",
                        9F,
                        FontStyle.Bold)
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

                ForeColor =
                    Color.White,

                Font =
                    new Font(
                        "Segoe UI",
                        9F)
            };
        }

        // =====================================================
        // LOAD BACKUPS
        // =====================================================

        private void LoadBackups()
        {
            string backupsRoot =
                Path.Combine(
                    Config.Current.DestinationFolder,
                    "Backups");

            backupTree.Nodes.Clear();

            if (!Directory.Exists(backupsRoot))
                return;

            var backupDirs =
                Directory.GetDirectories(
                    backupsRoot,
                    "*_*");

            foreach (string dir in backupDirs)
            {
                TreeNode node =
                    new TreeNode(
                        Path.GetFileName(dir));

                node.Tag = dir;

                node.Nodes.Add("loading");

                backupTree.Nodes.Add(node);
            }
        }

        // =====================================================
        // TREE
        // =====================================================

        private void BackupTree_BeforeExpand(
            object sender,
            TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 &&
                e.Node.Nodes[0].Text == "loading")
            {
                e.Node.Nodes.Clear();

                string path =
                    e.Node.Tag.ToString();

                foreach (string dir
                    in Directory.GetDirectories(path))
                {
                    TreeNode node =
                        new TreeNode(
                            Path.GetFileName(dir));

                    node.Tag = dir;

                    node.Nodes.Add("loading");

                    e.Node.Nodes.Add(node);
                }

                foreach (string file
                    in Directory.GetFiles(path))
                {
                    TreeNode node =
                        new TreeNode(
                            Path.GetFileName(file));

                    node.Tag = file;

                    e.Node.Nodes.Add(node);
                }
            }
        }

        private void BackupTree_AfterSelect(
            object sender,
            TreeViewEventArgs e)
        {
            if (e.Node.Tag == null)
                return;

            string path =
                e.Node.Tag.ToString();

            infoLabel.Text =
                $"<div style='color:white'>" +
                $"{path}</div>";
        }

        private void BackupTree_AfterCheck(
            object sender,
            TreeViewEventArgs e)
        {
            SetChildrenChecked(
                e.Node,
                e.Node.Checked);

            UpdateSelectedCount();
        }

        private void SetChildrenChecked(
            TreeNode node,
            bool state)
        {
            foreach (TreeNode child
                in node.Nodes)
            {
                child.Checked = state;

                SetChildrenChecked(
                    child,
                    state);
            }
        }

        // =====================================================
        // FILTER
        // =====================================================

        private void ApplyFilter()
        {
            string filter =
                filterBox.Text
                    .Trim()
                    .ToLower();

            foreach (TreeNode node
                in backupTree.Nodes)
            {
                node.BackColor =
                    node.Text.ToLower().Contains(filter)
                    ? Color.FromArgb(60, 60, 65)
                    : Color.Transparent;
            }
        }

        // =====================================================
        // RESTORE
        // =====================================================

        private async void RestoreSelected(
    object sender,
    EventArgs e)
        {
            List<RestoreItem> selectedItems =
                new List<RestoreItem>();

            CollectCheckedItems(
                backupTree.Nodes,
                "",
                selectedItems);

            if (selectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Ничего не выбрано.",
                    "Restore",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            string restoreRoot = null;

            if (!restoreToOriginalCheckBox.Checked)
            {
                if (string.IsNullOrWhiteSpace(
                        customRestorePath.Text)
                    ||
                    !Directory.Exists(
                        customRestorePath.Text))
                {
                    MessageBox.Show(
                        "Выберите папку восстановления.",
                        "Ошибка",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);

                    return;
                }

                restoreRoot =
                    customRestorePath.Text;
            }

            restoreBtn.Enabled = false;

            progressBar.Value = 0;

            progressBar.Maximum =
                selectedItems.Count;

            int success = 0;

            int errors = 0;

            statusLabel.Text =
                "Восстановление...";

            foreach (var item in selectedItems)
            {
                try
                {
                    if (!File.Exists(item.FullPath))
                        continue;

                    currentFileLabel.Text =
                        item.RelativePath;

                    string targetPath =
                        GetTargetPath(
                            item.FullPath,
                            restoreRoot,
                            restoreToOriginalCheckBox.Checked);

                    if (string.IsNullOrWhiteSpace(
                            targetPath))
                    {
                        errors++;

                        Log(
                            $"ERROR: invalid target path");

                        continue;
                    }

                    string targetDir =
                        Path.GetDirectoryName(targetPath);

                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(
                            targetDir);
                    }

                    File.Copy(
                        item.FullPath,
                        targetPath,
                        overwriteCheckBox.Checked);

                    success++;

                    Log(
                        $"OK: {item.RelativePath}");
                }
                catch (Exception ex)
                {
                    errors++;

                    Log(
                        $"ERROR: {ex.Message}");
                }

                progressBar.Value++;

                await Task.Delay(10);
            }

            currentFileLabel.Text = "";

            statusLabel.Text =
                $"Готово | OK: {success} | ERR: {errors}";

            restoreBtn.Enabled = true;

            MessageBox.Show(
                $"Восстановление завершено\n\n" +
                $"Успешно: {success}\n" +
                $"Ошибок: {errors}",
                "Restore",
                MessageBoxButtons.OK,
                errors > 0
                    ? MessageBoxIcon.Warning
                    : MessageBoxIcon.Information);
        }

        // =====================================================
        // SELECTED
        // =====================================================

        private void UpdateSelectedCount()
        {
            int count =
                CountCheckedItems(
                    backupTree.Nodes);

            selectedCountLabel.Text =
                $"Выбрано: {count}";
        }
        private void CollectCheckedItems(
    TreeNodeCollection nodes,
    string currentPath,
    List<RestoreItem> items)
        {
            foreach (TreeNode node in nodes)
            {
                string nodePath =
                    string.IsNullOrEmpty(currentPath)
                    ? node.Text
                    : Path.Combine(
                        currentPath,
                        node.Text);

                if (node.Checked &&
                    node.Tag != null)
                {
                    string fullPath =
                        node.Tag.ToString();

                    if (File.Exists(fullPath))
                    {
                        items.Add(
                            new RestoreItem
                            {
                                FullPath = fullPath,

                                RelativePath = nodePath
                            });
                    }
                }

                CollectCheckedItems(
                    node.Nodes,
                    nodePath,
                    items);
            }
        }

        private string GetTargetPath(
            string sourcePath,
            string restoreRoot,
            bool restoreToOriginal)
        {
            string backupsRoot =
                Path.Combine(
                    Config.Current.DestinationFolder,
                    "Backups");

            string relative =
                GetRelativePath(
                    sourcePath,
                    backupsRoot);

            if (restoreToOriginal)
            {
                string[] parts =
                    relative.Split(
                        Path.DirectorySeparatorChar);

                if (parts.Length < 2)
                    return null;

                string sourceRootName =
                    parts[1];

                string originalFolder =
                    Config.Current.SourceFolders
                        .FirstOrDefault(
                            f =>
                                Path.GetFileName(f)
                                .Equals(
                                    sourceRootName,
                                    StringComparison.OrdinalIgnoreCase));

                if (originalFolder == null)
                    return null;

                string rest =
                    string.Join(
                        Path.DirectorySeparatorChar.ToString(),
                        parts.Skip(2));

                return Path.Combine(
                    originalFolder,
                    rest);
            }
            else
            {
                int firstSep =
                    relative.IndexOf(
                        Path.DirectorySeparatorChar);

                if (firstSep >= 0)
                {
                    relative =
                        relative.Substring(firstSep + 1);
                }

                return Path.Combine(
                    restoreRoot,
                    relative);
            }
        }

        private string GetRelativePath(
            string fullPath,
            string basePath)
        {
            if (!basePath.EndsWith(
                    Path.DirectorySeparatorChar.ToString()))
            {
                basePath +=
                    Path.DirectorySeparatorChar;
            }

            Uri baseUri =
                new Uri(basePath);

            Uri fullUri =
                new Uri(fullPath);

            return Uri.UnescapeDataString(
                baseUri.MakeRelativeUri(fullUri)
                    .ToString())
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar);
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

        // =====================================================
        // LOG
        // =====================================================

        private void Log(string text)
        {
            logBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {text}" +
                Environment.NewLine);
        }

        // =====================================================
        // PATH
        // =====================================================

        private void BrowseCustomPathBtn_Click(
            object sender,
            EventArgs e)
        {
            using FolderBrowserDialog dialog =
                new FolderBrowserDialog();

            if (dialog.ShowDialog()
                == DialogResult.OK)
            {
                customRestorePath.Text =
                    dialog.SelectedPath;
            }
        }

        // =====================================================
        // DARK TITLEBAR
        // =====================================================

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);

        private const int
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

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
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(
    IntPtr hWnd,
    string pszSubAppName,
    string pszSubIdList);

        [DllImport("uxtheme.dll", EntryPoint = "#135")]
        private static extern int SetPreferredAppMode(
            int appMode);

        [DllImport("uxtheme.dll", EntryPoint = "#133")]
        private static extern int AllowDarkModeForWindow(
            IntPtr hWnd,
            bool allow);

        private void EnableDarkScrollBar(
            Control control)
        {
            if (Environment.OSVersion.Version.Major >= 10)
            {
                SetPreferredAppMode(2);

                AllowDarkModeForWindow(
                    control.Handle,
                    true);

                SetWindowTheme(
                    control.Handle,
                    "DarkMode_Explorer",
                    null);
            }
        }
    }
}