using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoBackup
{
    public class RestoreForm : Form
    {
        // Элементы управления
        private TreeView backupTree;
        private Button restoreBtn;
        private Button cancelBtn;
        private CheckBox restoreToOriginalCheckBox;
        private TextBox customRestorePath;
        private Button browseCustomPathBtn;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label infoLabel;
        private TextBox filterBox;
        private Button filterBtn;
        private ComboBox backupTypeFilter;
        private CheckBox overwriteCheckBox;
        private Label selectedCountLabel;

        private class RestoreItem
        {
            public string FullPath { get; set; }
            public string RelativePath { get; set; }
        }

        public RestoreForm()
        {
            InitializeComponents();
            LoadBackups();
        }

        private void InitializeComponents()
        {
            this.Text = "Восстановление из резервных копий";
            this.Size = new Size(1000, 700);
            this.MinimumSize = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterParent;
            this.AutoScroll = false;
            // === Корневая панель с прокруткой ===
            Panel scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Location = new Point(0, 0)
            };
            // Верхняя панель (фильтры)
            Panel topPanel = new Panel
            {
                Width = scrollPanel.Width - 20, // учтём полосу прокрутки
                Height = 45,
                Location = new Point(10, 10)
            };
            topPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblFilter = new Label { Text = "Фильтр:", Location = new Point(0, 12), AutoSize = true };
            filterBox = new TextBox { Location = new Point(50, 9), Width = 200 };
            filterBtn = new Button { Text = "Найти", Location = new Point(260, 8), Width = 70 };
            filterBtn.Click += (s, e) => ApplyFilter();

            Label lblType = new Label { Text = "Тип бэкапа:", Location = new Point(350, 12), AutoSize = true };
            backupTypeFilter = new ComboBox
            {
                Location = new Point(430, 9),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            backupTypeFilter.Items.AddRange(new object[] { "Все", "Полные (Full)", "Инкрементные (Inc)" });
            backupTypeFilter.SelectedIndex = 0;
            backupTypeFilter.SelectedIndexChanged += (s, e) => LoadBackups();

            topPanel.Controls.AddRange(new Control[] { lblFilter, filterBox, filterBtn, lblType, backupTypeFilter });

            // Дерево бэкапов
            backupTree = new TreeView
            {
                Location = new Point(10, topPanel.Bottom + 10),
                Width = scrollPanel.Width - 30,
                Height = 350,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                CheckBoxes = true,
                HideSelection = false,
                Font = new Font("Segoe UI", 9)
            };
            backupTree.AfterCheck += BackupTree_AfterCheck;
            backupTree.BeforeExpand += BackupTree_BeforeExpand;
            backupTree.AfterSelect += BackupTree_AfterSelect;

            // Нижняя панель с настройками и кнопками (без Dock, просто расположена ниже дерева)
            int bottomY = backupTree.Bottom + 10;
            Panel bottomPanel = new Panel
            {
                Location = new Point(10, bottomY),
                Width = scrollPanel.Width - 30,
                Height = 200,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            restoreToOriginalCheckBox = new CheckBox
            {
                Text = "Восстановить в исходные папки",
                Location = new Point(0, 5),
                AutoSize = true,
                Checked = true
            };
            restoreToOriginalCheckBox.CheckedChanged += (s, e) =>
            {
                customRestorePath.Enabled = !restoreToOriginalCheckBox.Checked;
                browseCustomPathBtn.Enabled = !restoreToOriginalCheckBox.Checked;
                if (!restoreToOriginalCheckBox.Checked && string.IsNullOrWhiteSpace(customRestorePath.Text))
                    customRestorePath.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            };

            customRestorePath = new TextBox
            {
                Location = new Point(190, 3),
                Width = bottomPanel.Width - 280,
                Enabled = false,
                ReadOnly = true,
                BackColor = SystemColors.Window,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            browseCustomPathBtn = new Button
            {
                Text = "Обзор...",
                Location = new Point(customRestorePath.Right + 5, 2),
                Width = 75,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false
            };
            browseCustomPathBtn.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Выберите папку для восстановления";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        customRestorePath.Text = fbd.SelectedPath;
                }
            };

            overwriteCheckBox = new CheckBox
            {
                Text = "Перезаписывать существующие файлы",
                Location = new Point(0, 35),
                AutoSize = true,
                Checked = true
            };

            selectedCountLabel = new Label
            {
                Text = "Выбрано: 0 элементов",
                Location = new Point(0, 65),
                AutoSize = true
            };

            infoLabel = new Label
            {
                Text = "Выберите резервную копию и отметьте файлы/папки для восстановления",
                Location = new Point(0, 90),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            progressBar = new ProgressBar
            {
                Location = new Point(0, 115),
                Width = 350,
                Height = 20,
                Style = ProgressBarStyle.Marquee,
                Visible = false
            };

            statusLabel = new Label
            {
                Text = "Готов",
                Location = new Point(360, 118),
                AutoSize = true
            };

            restoreBtn = new Button
            {
                Text = "Восстановить выбранное",
                Location = new Point(0, 145),
                Width = 160,
                Height = 30,
                BackColor = Color.LightGreen,
                FlatStyle = FlatStyle.Flat
            };
            restoreBtn.Click += RestoreSelected;

            cancelBtn = new Button
            {
                Text = "Отмена",
                Location = new Point(170, 145),
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat
            };
            cancelBtn.Click += (s, e) => this.Close();

            bottomPanel.Controls.AddRange(new Control[]
            {
                restoreToOriginalCheckBox, customRestorePath, browseCustomPathBtn,
                overwriteCheckBox, selectedCountLabel, infoLabel, progressBar, statusLabel,
                restoreBtn, cancelBtn
            });
            // Динамическое изменение размеров при изменении окна
            this.Resize += (s, e) =>
            {
                int newWidth = scrollPanel.ClientSize.Width - 30;
                backupTree.Width = newWidth;
                bottomPanel.Width = newWidth;
                if (customRestorePath != null)
                {
                    customRestorePath.Width = newWidth - 280;
                    browseCustomPathBtn.Location = new Point(customRestorePath.Right + 5, 2);
                }
            };

            scrollPanel.Controls.Add(topPanel);
            scrollPanel.Controls.Add(backupTree);
            scrollPanel.Controls.Add(bottomPanel);
            // Устанавливаем минимальную высоту содержимого, чтобы прокрутка появлялась
            scrollPanel.AutoScrollMinSize = new Size(0, bottomPanel.Bottom + 30);

            this.Controls.Add(scrollPanel);
        }

        // ======================== ОСНОВНАЯ ЛОГИКА (БЕЗ ИЗМЕНЕНИЙ) ========================

        private void LoadBackups()
        {
            string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
            if (!Directory.Exists(backupsRoot))
            {
                backupTree.Nodes.Clear();
                backupTree.Nodes.Add("Нет доступных резервных копий");
                return;
            }

            backupTree.Nodes.Clear();
            var backupDirs = Directory.GetDirectories(backupsRoot, "*_*")
                .Select(d => new { Path = d, Meta = BackupMeta.Load(Path.Combine(d, "backup_meta.json")) })
                .Where(x => x.Meta != null)
                .ToList();

            if (backupTypeFilter.SelectedIndex == 1)
                backupDirs = backupDirs.Where(x => x.Meta.BackupType == "Full").ToList();
            else if (backupTypeFilter.SelectedIndex == 2)
                backupDirs = backupDirs.Where(x => x.Meta.BackupType == "Inc").ToList();

            foreach (var backup in backupDirs.OrderByDescending(x => x.Meta.BackupTime))
            {
                string displayName = $"{backup.Meta.BackupType} - {backup.Meta.BackupTime:yyyy-MM-dd HH:mm:ss}";
                TreeNode node = new TreeNode(displayName) { Tag = backup.Path };
                node.Nodes.Add("загрузка...");
                backupTree.Nodes.Add(node);
            }

            if (backupTree.Nodes.Count == 0)
                backupTree.Nodes.Add("Нет бэкапов, соответствующих фильтру");
        }

        private void BackupTree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Text == "загрузка...")
            {
                e.Node.Nodes.Clear();
                string fullPath = e.Node.Tag.ToString();
                LoadFolder(e.Node, fullPath);
            }
        }

        private void LoadFolder(TreeNode parentNode, string directoryPath)
        {
            try
            {
                foreach (string dir in Directory.GetDirectories(directoryPath))
                {
                    TreeNode dirNode = new TreeNode(Path.GetFileName(dir)) { Tag = dir };
                    dirNode.Nodes.Add("загрузка...");
                    parentNode.Nodes.Add(dirNode);
                }
                foreach (string file in Directory.GetFiles(directoryPath))
                {
                    string fileName = Path.GetFileName(file);
                    var fi = new FileInfo(file);
                    string sizeStr = fi.Length > 0 ? $"{GetSizeString(fi.Length)}" : "";
                    TreeNode fileNode = new TreeNode($"{fileName}  [{sizeStr}]") { Tag = file };
                    parentNode.Nodes.Add(fileNode);
                }
            }
            catch (Exception ex)
            {
                parentNode.Nodes.Add($"Ошибка: {ex.Message}");
            }
        }

        private string GetSizeString(long bytes)
        {
            if (bytes < 1024) return $"{bytes} Б";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} КБ";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024)} МБ";
            return $"{bytes / (1024 * 1024 * 1024)} ГБ";
        }

        private void ApplyFilter()
        {
            string filter = filterBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                LoadBackups();
                return;
            }
            SearchTree(backupTree.Nodes, filter);
        }

        private bool SearchTree(TreeNodeCollection nodes, string filter)
        {
            bool found = false;
            foreach (TreeNode node in nodes)
            {
                bool matches = node.Text.ToLower().Contains(filter);
                if (matches)
                {
                    node.BackColor = Color.LightYellow;
                    found = true;
                    TreeNode parent = node.Parent;
                    while (parent != null)
                    {
                        parent.Expand();
                        parent = parent.Parent;
                    }
                    node.EnsureVisible();
                }
                else
                {
                    node.BackColor = SystemColors.Window;
                }
                if (node.Nodes.Count > 0)
                {
                    bool childFound = SearchTree(node.Nodes, filter);
                    if (childFound) found = true;
                }
            }
            return found;
        }

        private void BackupTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Tag is string path && Directory.Exists(path))
            {
                var meta = BackupMeta.Load(Path.Combine(path, "backup_meta.json"));
                if (meta != null)
                {
                    infoLabel.Text = $"Тип: {meta.BackupType}, Файлов: {meta.Files.Count}, Дата: {meta.BackupTime:yyyy-MM-dd HH:mm:ss}";
                    if (meta.BackupType == "Inc" && !string.IsNullOrEmpty(meta.FullBackupRef))
                        infoLabel.Text += $", Основа: {Path.GetFileName(meta.FullBackupRef)}";
                }
            }
        }

        private void BackupTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Action != TreeViewAction.Unknown)
            {
                SetChildrenChecked(e.Node, e.Node.Checked);
                UpdateParentCheckState(e.Node.Parent);
                UpdateSelectedCount();
            }
        }

        private void SetChildrenChecked(TreeNode node, bool checkedState)
        {
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = checkedState;
                SetChildrenChecked(child, checkedState);
            }
        }

        private void UpdateParentCheckState(TreeNode parent)
        {
            if (parent == null) return;
            int totalChildren = parent.Nodes.Count;
            int checkedChildren = 0;
            foreach (TreeNode child in parent.Nodes)
                if (child.Checked) checkedChildren++;

            if (checkedChildren == totalChildren)
                parent.Checked = true;
            else if (checkedChildren == 0)
                parent.Checked = false;
            UpdateParentCheckState(parent.Parent);
        }

        private void UpdateSelectedCount()
        {
            int count = CountCheckedItems(backupTree.Nodes);
            selectedCountLabel.Text = $"Выбрано: {count} элементов";
        }

        private int CountCheckedItems(TreeNodeCollection nodes)
        {
            int count = 0;
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag != null) count++;
                count += CountCheckedItems(node.Nodes);
            }
            return count;
        }

        private async void RestoreSelected(object sender, EventArgs e)
        {
            var selectedItems = new List<RestoreItem>();
            CollectCheckedItems(backupTree.Nodes, "", selectedItems);

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("Не выбрано ни одного файла или папки для восстановления.", "Восстановление", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string restoreRoot;
            if (restoreToOriginalCheckBox.Checked)
            {
                restoreRoot = null;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(customRestorePath.Text) || !Directory.Exists(customRestorePath.Text))
                {
                    MessageBox.Show("Укажите существующую папку для восстановления или отключите опцию 'Восстановить в исходные папки'.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                restoreRoot = customRestorePath.Text;
            }

            restoreBtn.Enabled = false;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = "Восстановление...";

            int successCount = 0;
            int errorCount = 0;

            try
            {
                foreach (var item in selectedItems)
                {
                    string sourcePath = item.FullPath;
                    bool isBackupFolder = Directory.Exists(sourcePath) && File.Exists(Path.Combine(sourcePath, "backup_meta.json"));
                    if (isBackupFolder)
                    {
                        await RestoreFromChainAsync(sourcePath, restoreRoot, restoreToOriginalCheckBox.Checked, overwriteCheckBox.Checked);
                        successCount++;
                    }
                    else
                    {
                        string targetPath = GetTargetPath(sourcePath, restoreRoot, restoreToOriginalCheckBox.Checked);
                        if (string.IsNullOrEmpty(targetPath)) continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        try
                        {
                            if (File.Exists(sourcePath))
                            {
                                if (!overwriteCheckBox.Checked && File.Exists(targetPath))
                                    continue;
                                File.Copy(sourcePath, targetPath, overwriteCheckBox.Checked);
                                successCount++;
                            }
                            else if (Directory.Exists(sourcePath))
                            {
                                CopyDirectoryRecursive(sourcePath, targetPath, overwriteCheckBox.Checked);
                                successCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;
                            Logger.LogError($"Restore {sourcePath}", ex);
                        }
                    }
                }
            }
            finally
            {
                progressBar.Visible = false;
                statusLabel.Text = $"Готово: {successCount} успешно, {errorCount} ошибок";
                restoreBtn.Enabled = true;
                MessageBox.Show($"Восстановление завершено.\nУспешно: {successCount}\nОшибок: {errorCount}", "Результат", MessageBoxButtons.OK, errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                this.Close();
            }
        }

        private async Task RestoreFromChainAsync(string backupFolderPath, string restoreRoot, bool restoreToOriginal, bool overwrite)
        {
            var meta = BackupMeta.Load(Path.Combine(backupFolderPath, "backup_meta.json"));
            if (meta == null) return;

            if (meta.BackupType == "Full")
            {
                RestoreFromMeta(meta, backupFolderPath, restoreRoot, restoreToOriginal, overwrite);
            }
            else if (meta.BackupType == "Inc")
            {
                if (!string.IsNullOrEmpty(meta.FullBackupRef) && Directory.Exists(meta.FullBackupRef))
                {
                    var fullMeta = BackupMeta.Load(Path.Combine(meta.FullBackupRef, "backup_meta.json"));
                    if (fullMeta != null)
                    {
                        RestoreFromMeta(fullMeta, meta.FullBackupRef, restoreRoot, restoreToOriginal, overwrite);
                    }
                }
                RestoreFromMeta(meta, backupFolderPath, restoreRoot, restoreToOriginal, overwrite);
            }
            await Task.CompletedTask;
        }

        private void RestoreFromMeta(BackupMeta meta, string backupFolderPath, string restoreRoot, bool restoreToOriginal, bool overwrite)
        {
            foreach (var entry in meta.Files)
            {
                string sourceFile = Path.Combine(backupFolderPath, entry.RelativePath);
                if (!File.Exists(sourceFile)) continue;

                string targetFile;
                if (restoreToOriginal)
                {
                    string rootName = entry.RelativePath.Split(Path.DirectorySeparatorChar)[0];
                    string originalRoot = Config.Current.SourceFolders.FirstOrDefault(f => Path.GetFileName(f).Equals(rootName, StringComparison.OrdinalIgnoreCase));
                    if (originalRoot == null) continue;
                    string restPath = entry.RelativePath.Substring(rootName.Length).TrimStart(Path.DirectorySeparatorChar);
                    targetFile = Path.Combine(originalRoot, restPath);
                }
                else
                {
                    targetFile = Path.Combine(restoreRoot, entry.RelativePath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                if (!overwrite && File.Exists(targetFile)) continue;
                try
                {
                    File.Copy(sourceFile, targetFile, true);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Restore {sourceFile}", ex);
                }
            }
        }

        private string GetTargetPath(string sourcePath, string restoreRoot, bool restoreToOriginal)
        {
            if (restoreToOriginal)
            {
                string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
                string relative = GetRelativePath(sourcePath, backupsRoot);
                var parts = relative.Split(Path.DirectorySeparatorChar);
                if (parts.Length < 2) return null;
                string sourceRootName = parts[1];
                string originalSourceFolder = Config.Current.SourceFolders.FirstOrDefault(f => Path.GetFileName(f).Equals(sourceRootName, StringComparison.OrdinalIgnoreCase));
                if (originalSourceFolder == null) return null;
                string restOfPath = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Skip(2));
                return Path.Combine(originalSourceFolder, restOfPath);
            }
            else
            {
                string backupsRoot = Path.Combine(Config.Current.DestinationFolder, "Backups");
                string relative = GetRelativePath(sourcePath, backupsRoot);
                int firstSep = relative.IndexOf(Path.DirectorySeparatorChar);
                if (firstSep >= 0)
                    relative = relative.Substring(firstSep + 1);
                return Path.Combine(restoreRoot, relative);
            }
        }

        private void CopyDirectoryRecursive(string sourceDir, string targetDir, bool overwrite)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                if (!overwrite && File.Exists(destFile)) continue;
                File.Copy(file, destFile, overwrite);
            }
            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectoryRecursive(dir, destSubDir, overwrite);
            }
        }

        private string GetRelativePath(string fullPath, string basePath)
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;
            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private void CollectCheckedItems(TreeNodeCollection nodes, string currentRelativePath, List<RestoreItem> items)
        {
            foreach (TreeNode node in nodes)
            {
                string nodePath = string.IsNullOrEmpty(currentRelativePath) ? node.Text : Path.Combine(currentRelativePath, node.Text);
                if (node.Checked && node.Tag != null)
                {
                    items.Add(new RestoreItem
                    {
                        FullPath = node.Tag.ToString(),
                        RelativePath = nodePath
                    });
                }
                else if (node.Nodes.Count > 0)
                {
                    CollectCheckedItems(node.Nodes, nodePath, items);
                }
            }
        }
    }
}