using System;
using System.Windows.Forms;

namespace AutoBackup
{
    public class SettingsForm : Form
    {
        private TextBox excludeMasksBox;
        private CheckBox limitSpeedChk, pauseBatteryChk;
        private NumericUpDown retentionDays, speedLimit;
        private Button importBtn, exportBtn;
        public SettingsForm()
        {
            this.Text = "Настройки";
            this.Size = new System.Drawing.Size(500, 400);
            retentionDays = new NumericUpDown { Location = new System.Drawing.Point(10, 10), Minimum = 1, Maximum = 365, Value = Config.Current.VersionRetentionDays };
            excludeMasksBox = new TextBox { Location = new System.Drawing.Point(10, 60), Width = 400, Height = 100, Multiline = true, Text = string.Join(",", Config.Current.ExcludeMasks) };
            limitSpeedChk = new CheckBox { Text = "Ограничить скорость копирования", Location = new System.Drawing.Point(10, 180), Checked = Config.Current.LimitSpeed };
            speedLimit = new NumericUpDown { Location = new System.Drawing.Point(250, 178), Width = 100, Minimum = 1, Maximum = 1000, Value = (int)(Config.Current.MaxBytesPerSecond / 1024 / 1024) };
            pauseBatteryChk = new CheckBox { Text = "Пауза при питании от батареи", Location = new System.Drawing.Point(10, 210), Checked = Config.Current.PauseOnBattery };
            importBtn = new Button { Text = "Импорт настроек", Location = new System.Drawing.Point(10, 250) };
            exportBtn = new Button { Text = "Экспорт настроек", Location = new System.Drawing.Point(130, 250) };
            Button saveBtn = new Button { Text = "Сохранить", Location = new System.Drawing.Point(10, 300) };
            saveBtn.Click += Save;
            importBtn.Click += (s, e) => { using (OpenFileDialog ofd = new OpenFileDialog()) if (ofd.ShowDialog() == DialogResult.OK) { Config.Import(ofd.FileName); LoadSettings(); } };
            exportBtn.Click += (s, e) => { using (SaveFileDialog sfd = new SaveFileDialog()) if (sfd.ShowDialog() == DialogResult.OK) Config.Export(sfd.FileName); };
            this.Controls.AddRange(new Control[] { retentionDays, excludeMasksBox, limitSpeedChk, speedLimit, pauseBatteryChk, importBtn, exportBtn, saveBtn });
            LoadSettings();
        }
        private void LoadSettings()
        {
            retentionDays.Value = Config.Current.VersionRetentionDays;
            excludeMasksBox.Text = string.Join(",", Config.Current.ExcludeMasks);
            limitSpeedChk.Checked = Config.Current.LimitSpeed;
            speedLimit.Value = (int)(Config.Current.MaxBytesPerSecond / 1024 / 1024);
            pauseBatteryChk.Checked = Config.Current.PauseOnBattery;
        }
        private void Save(object s, EventArgs e)
        {
            Config.Current.VersionRetentionDays = (int)retentionDays.Value;
            Config.Current.ExcludeMasks = excludeMasksBox.Text.Split(',').Select(m => m.Trim()).ToList();
            Config.Current.LimitSpeed = limitSpeedChk.Checked;
            Config.Current.MaxBytesPerSecond = (long)speedLimit.Value * 1024 * 1024;
            Config.Current.PauseOnBattery = pauseBatteryChk.Checked;
            Config.Save();
            MessageBox.Show("Настройки сохранены");
        }
    }
}