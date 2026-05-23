using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AutoBackup.Models;
using AutoBackup.Services;
using AutoBackup.Controls;
using Guna.UI2.WinForms;

namespace AutoBackup
{
    public class MainForm : Form
    {
        private MenuStrip mainMenu;
        private Guna2Panel sideMenuPanel;
        private Guna2Panel contentPanel;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel nextRunLabel;
        private ToolStripProgressBar toolProgressBar; // для статус-бара

        private Guna2Button dashboardBtn, settingsBtn, aboutBtn, pauseBtn;
        private UserControl activeControl;

        // Токен отмены для текущей операции (бэкап или восстановление)
        private CancellationTokenSource currentCts;

        // Цвета для кнопок меню
        private readonly Color activeColor = Color.FromArgb(0, 120, 215);
        private readonly Color normalColor = Color.Transparent;

        public MainForm()
        {
            InitializeComponents();
            EnableDarkTitleBar();
            SubscribeToBackupManagerEvents();
            ShowDashboard();
            UpdateNextRunInfo();

            // Таймер обновления статуса расписания
            var timer = new System.Windows.Forms.Timer { Interval = 30000 };
            timer.Tick += (s, e) => UpdateNextRunInfo();
            timer.Start();
        }

        private void InitializeComponents()
        {
            Text = "AutoBackup Professional";
            Width = 1400;
            Height = 850;
            MinimumSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(24, 24, 27);
            Font = new Font("Segoe UI", 9F);

            // ========== ГЛАВНОЕ МЕНЮ ==========
            mainMenu = new MenuStrip
            {
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };

            var fileMenu = new ToolStripMenuItem("Файл");
            fileMenu.DropDownItems.Add("Экспорт конфигурации", null, (s, e) => ExportConfig());
            fileMenu.DropDownItems.Add("Импорт конфигурации", null, (s, e) => ImportConfig());
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Выход", null, (s, e) => Close());

            var toolsMenu = new ToolStripMenuItem("Сервис");
            toolsMenu.DropDownItems.Add("Очистить старые бэкапы", null, (s, e) => CleanOldBackups());
            toolsMenu.DropDownItems.Add("Проверить целостность бэкапов", null, (s, e) => VerifyAllBackups());
            toolsMenu.DropDownItems.Add(new ToolStripSeparator());
            toolsMenu.DropDownItems.Add("Статистика", null, (s, e) => ShowStatistics());
            toolsMenu.DropDownItems.Add("Открыть папку бэкапов", null, (s, e) => OpenBackupFolder());

            var helpMenu = new ToolStripMenuItem("Справка");
            helpMenu.DropDownItems.Add("О программе", null, (s, e) => ShowAbout());

            mainMenu.Items.Add(fileMenu);
            mainMenu.Items.Add(toolsMenu);
            mainMenu.Items.Add(helpMenu);

            // ========== ЛЕВОЕ МЕНЮ (ПАНЕЛЬ КНОПОК) ==========
            sideMenuPanel = new Guna2Panel
            {
                Dock = DockStyle.Left,
                Width = 220,
                FillColor = Color.FromArgb(32, 32, 38),
                BorderRadius = 0
            };

            int btnY = 20;
            dashboardBtn = CreateSideButton("  Главная", "🏠", btnY, true); btnY += 60;
            settingsBtn = CreateSideButton("  Настройки", "⚙️", btnY, false); btnY += 60;
            aboutBtn = CreateSideButton("  О программе", "ℹ️", btnY, false); btnY += 60;
            pauseBtn = CreateSideButton("  Пауза", "⏸", btnY, false);
            pauseBtn.FillColor = Color.Goldenrod;
            pauseBtn.Click += TogglePause;

            sideMenuPanel.Controls.Add(dashboardBtn);
            sideMenuPanel.Controls.Add(settingsBtn);
            sideMenuPanel.Controls.Add(aboutBtn);
            sideMenuPanel.Controls.Add(pauseBtn);

            // ========== ЦЕНТРАЛЬНАЯ ОБЛАСТЬ ==========
            contentPanel = new Guna2Panel
            {
                Dock = DockStyle.Fill,
                FillColor = Color.FromArgb(24, 24, 27),
                Padding = new Padding(15)
            };

            // ========== СТРОКА СОСТОЯНИЯ ==========
            statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(30, 30, 35),
                ForeColor = Color.White
            };
            statusLabel = new ToolStripStatusLabel(" Статус: Готов");
            nextRunLabel = new ToolStripStatusLabel(" Расписание: не задано");
            toolProgressBar = new ToolStripProgressBar
            {
                Size = new Size(200, 18),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(nextRunLabel);
            statusStrip.Items.Add(new ToolStripStatusLabel("  "));
            statusStrip.Items.Add(toolProgressBar);

            // Сборка формы
            Controls.Add(contentPanel);
            Controls.Add(sideMenuPanel);
            Controls.Add(mainMenu);
            Controls.Add(statusStrip);

            mainMenu.Padding = new Padding(5, 2, 0, 2);
            statusStrip.Padding = new Padding(10, 0, 10, 0);
        }

        private Guna2Button CreateSideButton(string text, string icon, int top, bool isActive)
        {
            var btn = new Guna2Button
            {
                Text = $"{icon} {text}",
                Dock = DockStyle.Top,
                Height = 50,
                Margin = new Padding(10, 5, 10, 0),
                FillColor = isActive ? activeColor : normalColor,
                HoverState = { FillColor = Color.FromArgb(60, 60, 70) },
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                TextAlign = HorizontalAlignment.Left,
                BorderRadius = 8
            };
            btn.Click += (s, e) => ActivateButton(btn, text);
            return btn;
        }

        private void ActivateButton(Guna2Button clickedBtn, string text)
        {
            foreach (var btn in new[] { dashboardBtn, settingsBtn, aboutBtn, pauseBtn })
                btn.FillColor = normalColor;

            clickedBtn.FillColor = activeColor;

            switch (text)
            {
                case "  Главная": ShowDashboard(); break;
                case "  Настройки": ShowSettings(); break;
                case "  О программе": ShowAbout(); break;
                    // pauseBtn не переключает вьюху
            }
        }

        private void ShowDashboard()
        {
            SetActiveControl(new BackupControl());
        }

        private void ShowSettings()
        {
            SetActiveControl(new SettingsControl());
        }

        private void ShowAbout()
        {
            SetActiveControl(new AboutControl());
        }

        private void SetActiveControl(UserControl control)
        {
            if (activeControl != null)
                contentPanel.Controls.Remove(activeControl);

            activeControl = control;
            control.Dock = DockStyle.Fill;
            control.BackColor = Color.FromArgb(24, 24, 27);
            contentPanel.Controls.Add(control);
        }

        // =====================================================
        // Обработчики меню и кнопок
        // =====================================================
        private void TogglePause(object sender, EventArgs e)
        {
            if (BackupManager.IsPaused())
            {
                BackupManager.Resume();
                pauseBtn.Text = "⏸  Пауза";
                pauseBtn.FillColor = Color.Goldenrod;
                statusLabel.Text = "Статус: Активен";
            }
            else
            {
                BackupManager.PauseFor(60);
                pauseBtn.Text = "▶  Возобновить";
                pauseBtn.FillColor = Color.ForestGreen;
                statusLabel.Text = "Статус: Пауза";
            }
        }

        private void ExportConfig()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "JSON файлы|*.json";
                sfd.FileName = $"autobackup_config_{DateTime.Now:yyyyMMdd}.json";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    Config.Export(sfd.FileName);
                    MessageBox.Show("Конфигурация экспортирована.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void ImportConfig()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "JSON файлы|*.json";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Config.Import(ofd.FileName);
                    MessageBox.Show("Конфигурация импортирована. Перезапустите программу для полного применения.", "Успех",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void CleanOldBackups()
        {
            if (MessageBox.Show("Удалить все бэкапы старше указанного в настройках срока?\n(Данные будут безвозвратно удалены)",
                "Очистка старых версий", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                BackupManager.CleanupOldBackups();
                MessageBox.Show("Очистка завершена.", "Готово", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void VerifyAllBackups()
        {
            MessageBox.Show("Для проверки целостности используйте кнопку 'Verify' на главной панели.\n" +
                "Или выберите папку с бэкапом через меню Сервис → Открыть папку бэкапов.",
                "Подсказка", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void ShowStatistics()
        {
            var info = BackupManager.GetLastBackupInfo();
            long totalSize = BackupManager.GetTotalBackupSize();
            string sizeStr = FormatSize(totalSize);
            string lastTime = info.LastBackupTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "никогда";
            MessageBox.Show(
                $"📊 Статистика резервного копирования\n\n" +
                $"✅ Успешных бэкапов: {info.TotalFiles}\n" +
                $"📅 Последний бэкап: {lastTime}\n" +
                $"💾 Общий размер бэкапов: {sizeStr}\n" +
                $"📁 Папка хранения: {Config.Current.DestinationFolder}",
                "Статистика", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OpenBackupFolder()
        {
            if (Directory.Exists(Config.Current.DestinationFolder))
                System.Diagnostics.Process.Start("explorer.exe", Config.Current.DestinationFolder);
            else
                MessageBox.Show("Папка назначения не существует.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        // =====================================================
        // Подписка на события BackupManager
        // =====================================================
        private void SubscribeToBackupManagerEvents()
        {
            BackupManager.ProgressChanged += OnProgressChanged;
            BackupManager.StatusChanged += OnStatusChanged;
            BackupManager.Notification += (title, text) =>
            {
                if (InvokeRequired) { Invoke(() => BackupManager_Notification(title, text)); }
                else BackupManager_Notification(title, text);
            };
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Отписываемся от событий, чтобы избежать вызовов после уничтожения формы
            BackupManager.ProgressChanged -= OnProgressChanged;
            BackupManager.StatusChanged -= OnStatusChanged;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BackupManager.ProgressChanged -= OnProgressChanged;
                BackupManager.StatusChanged -= OnStatusChanged;
                //components?.Dispose();
            }
            base.Dispose(disposing);
        }
        private void OnProgressChanged(int percent, string fileName)
        {
            // Если форма уже уничтожена – игнорируем
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                Invoke(() => OnProgressChanged(percent, fileName));
                return;
            }

            // Повторная проверка, т.к. за время Invoke форма могла быть закрыта
            if (IsDisposed || toolProgressBar == null || statusLabel == null) return;

            if (percent >= 0 && percent <= 100)
            {
                toolProgressBar.Visible = true;
                toolProgressBar.Value = percent;
                if (percent == 100)
                {
                    toolProgressBar.Visible = false;
                    statusLabel.Text = "Статус: Готов";
                }
                else
                {
                    statusLabel.Text = $"Статус: Копирование ({percent}%) - {Path.GetFileName(fileName)}";
                }
            }
        }

        private void OnStatusChanged(string status)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                Invoke(() => OnStatusChanged(status));
                return;
            }
            if (IsDisposed || statusLabel == null) return;
            statusLabel.Text = $"Статус: {status}";
        }

        private void BackupManager_Notification(string title, string text)
        {
            TrayIconHelper.ShowBalloon(title, text);
        }

        // =====================================================
        // Обновление информации о расписании
        // =====================================================
        private void UpdateNextRunInfo()
        {
            string schedule = Config.Current.BackupSchedule;
            string text = schedule switch
            {
                "Daily" => "ежедневно",
                "Weekly" => "еженедельно",
                "OnSystemStart" => "при запуске системы",
                "OnIdle" => $"при простое ({Config.Current.IdleMinutes} мин)",
                _ => "не задано"
            };
            nextRunLabel.Text = $" Расписание: {text}";
        }

        // =====================================================
        // Тёмный заголовок окна
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
    }
}