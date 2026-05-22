using System;
using System.Windows.Forms;
using System.Linq;

namespace AutoBackup
{
    public class MainForm : Form
    {
        private DataGridView logGrid;
        private Button backupNowBtn, restoreBtn, settingsBtn;
        public MainForm()
        {
            this.Text = "Авторезервное копирование";
            this.Size = new System.Drawing.Size(800, 500);
            backupNowBtn = new Button { Text = "Запустить бэкап сейчас", Location = new System.Drawing.Point(10, 10), Width = 150 };
            restoreBtn = new Button { Text = "Восстановить...", Location = new System.Drawing.Point(170, 10), Width = 120 };
            settingsBtn = new Button { Text = "Настройки", Location = new System.Drawing.Point(300, 10), Width = 120 };
            logGrid = new DataGridView { Location = new System.Drawing.Point(10, 50), Width = 760, Height = 400, ReadOnly = true, AllowUserToAddRows = false };
            logGrid.Columns.Add("Timestamp", "Дата/время");
            logGrid.Columns.Add("Operation", "Операция");
            logGrid.Columns.Add("Details", "Подробности");
            logGrid.Columns.Add("Status", "Статус");
            this.Controls.Add(backupNowBtn);
            this.Controls.Add(restoreBtn);
            this.Controls.Add(settingsBtn);
            this.Controls.Add(logGrid);
            LoadLog();
            backupNowBtn.Click += async (s, e) => { await BackupManager.RunBackup(true); LoadLog(); };
            restoreBtn.Click += (s, e) => new RestoreForm().ShowDialog();
            settingsBtn.Click += (s, e) => new SettingsForm().ShowDialog();
        }
        private void LoadLog()
        {
            logGrid.Rows.Clear();
            foreach (var entry in Logger.GetRecentEntries(200))
                logGrid.Rows.Add(entry.Timestamp, entry.Operation, entry.Details, entry.Status);
        }
    }
}