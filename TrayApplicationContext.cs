using System;
using System.Windows.Forms;

namespace AutoBackup
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private MainForm mainForm;
        private System.Threading.Timer schedulerTimer;
        private DateTime lastBackupTime = DateTime.MinValue;

        public TrayApplicationContext()
        {
            trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "Авторезервное копирование",
                Visible = true
            };
            trayIcon.DoubleClick += (s, e) => ShowMainForm();
            trayIcon.ContextMenuStrip = new ContextMenuStrip();
            trayIcon.ContextMenuStrip.Items.Add("Открыть главное окно", null, (s, e) => ShowMainForm());
            trayIcon.ContextMenuStrip.Items.Add("Запустить резервное копирование сейчас", null, (s, e) => BackupManager.RunManualBackup());
            trayIcon.ContextMenuStrip.Items.Add("Выход", null, (s, e) => Exit());

            // Запускаем главную логику
            Config.Load();
            // Добавляем в автозагрузку (только если включено в настройках)
            if (Config.Current.AutoStart)
            {
                Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                rk.SetValue("AutoBackup", System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            Logger.Init();
            BackupManager.Initialize();
            TrayIconHelper.TrayIcon = trayIcon;

            // Запускаем внутренний планировщик вместо Task Scheduler
            StartInternalScheduler();

            // Если первый запуск, показываем мастер
            if (Config.Current.FirstRun)
            {
                ShowWizard();
            }
        }
        private void StartInternalScheduler()
        {
            // Проверяем каждую минуту
            schedulerTimer = new System.Threading.Timer(CheckSchedule, null, 0, 60000);
        }

        private void CheckSchedule(object state)
        {
            if (BackupManager.IsPaused()) return;
            DateTime now = DateTime.Now;
            bool shouldRun = false;

            switch (Config.Current.BackupSchedule)
            {
                case "Daily":
                    if ((now - lastBackupTime).TotalHours >= 24)
                        shouldRun = true;
                    break;
                case "Weekly":
                    if (now.DayOfWeek == DayOfWeek.Monday && (now - lastBackupTime).TotalDays >= 7)
                        shouldRun = true;
                    break;
                case "OnSystemStart":
                    if (lastBackupTime == DateTime.MinValue)
                        shouldRun = true;
                    break;
                case "OnIdle":
                    if (IsSystemIdle() && (now - lastBackupTime).TotalMinutes >= Config.Current.IdleMinutes)
                        shouldRun = true;
                    break;
            }

            if (shouldRun)
            {
                lastBackupTime = now;
                Task.Run(() => BackupManager.RunBackup(false));
            }
        }

        private bool IsSystemIdle()
        {
            // Реализация через GetLastInputInfo (требует P/Invoke)
            // Для простоты пока возвращаем false, чтобы не мешать
            // Можно добавить настоящую проверку позже
            return false;
        }
        private void ShowMainForm()
        {
            if (mainForm == null || mainForm.IsDisposed)
                mainForm = new MainForm();
            mainForm.Show();
            mainForm.WindowState = FormWindowState.Normal;
            mainForm.BringToFront();
        }

        private void ShowWizard()
        {
            var wizard = new WizardForm();
            wizard.ShowDialog();
            Config.Current.FirstRun = false;
            Config.Save();
        }

        private void Exit()
        {
            trayIcon.Visible = false;
            Application.Exit();
        }
    }
}