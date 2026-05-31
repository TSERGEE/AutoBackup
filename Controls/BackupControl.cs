using AutoBackup.Forms;
using AutoBackup.Services;
using AutoBackup.Utils;
using Guna.UI2.WinForms;
using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace AutoBackup.Controls
{
    public class BackupControl : UserControl
    {
        private Guna2Button backupBtn, restoreBtn, verifyBtn, exportBtn, clearBtn, cancelBtn;
        private Guna2DataGridView logGrid;
        private Guna2TextBox searchBox;
        private Guna2ComboBox filterBox;
        private Guna2ProgressBar progressBar;
        private Label currentFileLabel;

        private CancellationTokenSource _currentCts;

        public BackupControl()
        {
            InitializeComponents();
            LoadLog();
            SubscribeToEvents();
        }

        private void InitializeComponents()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.FromArgb(24, 24, 27);
            this.Padding = new Padding(10);

            // ===== Верхняя панель с кнопками =====
            var topPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(5),
                BackColor = Color.Transparent
            };

            backupBtn = CreateButton("Запуск бэкапа", Color.FromArgb(0, 120, 215));
            restoreBtn = CreateButton("Восстановить файлы", Color.FromArgb(45, 45, 50));
            verifyBtn = CreateButton("Проверить файлы", Color.FromArgb(45, 45, 50));
            exportBtn = CreateButton("Экспорт логов", Color.FromArgb(45, 45, 50));
            clearBtn = CreateButton("Очистка логов", Color.Firebrick);
            cancelBtn = CreateButton("Отмена", Color.Firebrick);
            cancelBtn.Visible = false;
            cancelBtn.Click += CancelCurrentOperation;

            topPanel.Controls.AddRange(new Control[] { backupBtn, restoreBtn, verifyBtn, exportBtn, clearBtn, cancelBtn });

            // ===== Панель поиска и фильтрации =====
            var filterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 55,
                BackColor = Color.Transparent
            };

            searchBox = new Guna2TextBox
            {
                PlaceholderText = "Поиск по логам...",
                Size = new Size(280, 38),
                Location = new Point(10, 8),
                BorderRadius = 10,
                FillColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            searchBox.TextChanged += (s, e) => LoadLog();

            filterBox = new Guna2ComboBox
            {
                Items = { "Все", "Info", "Warning", "Error" },
                SelectedIndex = 0,
                Size = new Size(160, 38),
                Location = new Point(310, 8),
                BorderRadius = 10,
                FillColor = Color.FromArgb(35, 35, 40),
                ForeColor = Color.White
            };
            filterBox.SelectedIndexChanged += (s, e) => LoadLog();

            filterPanel.Controls.Add(searchBox);
            filterPanel.Controls.Add(filterBox);

            // ===== Прогресс-бар и метка текущего файла =====
            var progressPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                BackColor = Color.Transparent
            };
            progressBar = new Guna2ProgressBar
            {
                Location = new Point(10, 8),
                Size = new Size(400, 24),
                FillColor = Color.FromArgb(50, 50, 55),
                ProgressColor = Color.FromArgb(0, 120, 215),
                Visible = false
            };
            currentFileLabel = new Label
            {
                Location = new Point(420, 12),
                AutoSize = true,
                ForeColor = Color.Silver,
                Font = new Font("Segoe UI", 8F),
                Text = ""
            };
            progressPanel.Controls.Add(progressBar);
            progressPanel.Controls.Add(currentFileLabel);

            // ===== Таблица логов =====
            logGrid = new Guna2DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(24, 24, 27),
                GridColor = Color.FromArgb(55, 55, 60),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersHeight = 40,
                RowTemplate = { Height = 34 },
                DefaultCellStyle = { BackColor = Color.FromArgb(35, 35, 40), ForeColor = Color.White, SelectionBackColor = Color.FromArgb(0, 120, 215) },
                AlternatingRowsDefaultCellStyle = { BackColor = Color.FromArgb(35, 35, 40) }
            };
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                SetWindowTheme(logGrid.Handle, "DarkMode_ItemsView", null);
            }
            logGrid.Columns.Add("Timestamp", "Дата/время");
            logGrid.Columns.Add("Operation", "Операция");
            logGrid.Columns.Add("Details", "Подробности");
            logGrid.Columns.Add("Status", "Статус");

            foreach (DataGridViewColumn col in logGrid.Columns)
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            logGrid.Columns[0].FillWeight = 15; // Дата
            logGrid.Columns[1].FillWeight = 15; // Операция
            logGrid.Columns[2].FillWeight = 60; // Подробности
            logGrid.Columns[3].FillWeight = 10; // Статус

            // ===== Сборка =====
            this.Controls.Add(logGrid);
            this.Controls.Add(progressPanel);
            this.Controls.Add(filterPanel);
            this.Controls.Add(topPanel);

            // ===== Привязка событий =====
            backupBtn.Click += async (s, e) => await RunBackup();
            restoreBtn.Click += (s, e) => new RestoreForm().ShowDialog();
            verifyBtn.Click += VerifyBackupAsync;
            exportBtn.Click += ExportLog;
            clearBtn.Click += ClearLog;
        }

        private Guna2Button CreateButton(string text, Color color)
        {
            return new Guna2Button
            {
                Text = text,
                Width = 120,
                Height = 44,
                BorderRadius = 12,
                FillColor = color,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Margin = new Padding(5)
            };
        }

        private void SubscribeToEvents()
        {
            BackupManager.ProgressChanged += OnProgressChanged;
            BackupManager.StatusChanged += OnStatusChanged;
        }

        private void OnProgressChanged(int percent, string fileName)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnProgressChanged(percent, fileName));
                return;
            }
            if (percent >= 0 && percent <= 100)
            {
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = percent;
                currentFileLabel.Text = $"{percent}% - {System.IO.Path.GetFileName(fileName)}";
                if (percent == 100)
                {
                    progressBar.Visible = false;
                    currentFileLabel.Text = "";
                }
            }
        }

        private void OnStatusChanged(string status)
        {
            if (InvokeRequired)
            {
                Invoke(() => OnStatusChanged(status));
                return;
            }
            // можно отобразить статус в строке состояния, но у нас есть отдельный статус-бар в MainForm
        }

        private async Task RunBackup()
        {
            try
            {
                backupBtn.Enabled = false;
                cancelBtn.Visible = true;
                _currentCts = new CancellationTokenSource();

                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Marquee;
                currentFileLabel.Text = "Подготовка...";

                await BackupManager.RunBackup(true, _currentCts.Token);

                LoadLog();
                MessageBox.Show("Резервное копирование завершено.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Операция отменена пользователем.", "Отмена", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                backupBtn.Enabled = true;
                cancelBtn.Visible = false;
                cancelBtn.Enabled = true;
                _currentCts?.Dispose();
                _currentCts = null;
                progressBar.Visible = false;
                currentFileLabel.Text = "";
            }
        }

        private void CancelCurrentOperation(object sender, EventArgs e)
        {
            _currentCts?.Cancel();
            cancelBtn.Enabled = false;
            cancelBtn.Text = "Отмена...";
        }

        private async void VerifyBackupAsync(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с резервной копией (Full_* или Diff_*)";
                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                string backupFolder = dialog.SelectedPath;
                if (!System.IO.File.Exists(System.IO.Path.Combine(backupFolder, "backup_meta.json")))
                {
                    MessageBox.Show("Выбранная папка не содержит backup_meta.json", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    verifyBtn.Enabled = false;
                    cancelBtn.Visible = true;
                    _currentCts = new CancellationTokenSource();
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Blocks;
                    progressBar.Value = 0;

                    var mismatches = await BackupManager.VerifyBackupIntegrityAsync(backupFolder, _currentCts.Token);

                    if (mismatches.Count == 0)
                    {
                        MessageBox.Show("Верификация пройдена успешно. Все файлы совпадают.", "Результат", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        string msg = $"Найдено {mismatches.Count} несовпадений:\n" + string.Join("\n", mismatches.Take(10));
                        if (mismatches.Count > 10) msg += $"\n... и ещё {mismatches.Count - 10}";
                        MessageBox.Show(msg, "Ошибки верификации", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("Верификация отменена.", "Отмена", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка верификации: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    verifyBtn.Enabled = true;
                    cancelBtn.Visible = false;
                    cancelBtn.Enabled = true;
                    _currentCts?.Dispose();
                    _currentCts = null;
                    progressBar.Visible = false;
                    currentFileLabel.Text = "";
                }
            }
        }

        private void LoadLog()
        {
            logGrid.Rows.Clear();
            var entries = Logger.GetRecentEntries(500);
            string filter = filterBox.SelectedItem?.ToString();
            string search = searchBox.Text.ToLower();

            if (filter != "Все")
                entries = entries.Where(x => x.Status == filter).ToList();
            if (!string.IsNullOrWhiteSpace(search))
                entries = entries.Where(x => x.Operation.ToLower().Contains(search) || x.Details.ToLower().Contains(search)).ToList();

            foreach (var entry in entries)
            {
                int rowIndex = logGrid.Rows.Add(entry.Timestamp, entry.Operation, entry.Details, entry.Status);
                if (entry.Status == "Error")
                    logGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(70, 25, 25);
                else if (entry.Status == "Warning")
                    logGrid.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(70, 60, 25);
            }
        }

        private void ExportLog(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV файлы|*.csv";
                sfd.FileName = $"backup_log_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var lines = new System.Collections.Generic.List<string>();
                    foreach (DataGridViewRow row in logGrid.Rows)
                        lines.Add(string.Join(";", row.Cells.Cast<DataGridViewCell>().Select(c => c.Value?.ToString() ?? "")));
                    System.IO.File.WriteAllLines(sfd.FileName, lines);
                    MessageBox.Show("Лог экспортирован.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ClearLog(object sender, EventArgs e)
        {
            if (MessageBox.Show("Очистить весь журнал?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Logger.ClearLog();
                LoadLog();
            }
        }
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(
            IntPtr hWnd,
            string pszSubAppName,
            string pszSubIdList);
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (Environment.OSVersion.Version.Build >= 22000) // Windows 11
            {
                SetWindowTheme(logGrid.Handle, "DarkMode_ItemsView", null);
            }
        }
    }
}