using System;
using System.Windows.Forms;

namespace AutoBackup
{
    public class TrayApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private MainForm mainForm;

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
            Logger.Init();
            BackupManager.Initialize();
            Scheduler.Start(); // свой таймер или задачи планировщика – для простоты используем таймер

            // Если первый запуск, показываем мастер
            if (Config.Current.FirstRun)
            {
                ShowWizard();
            }
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