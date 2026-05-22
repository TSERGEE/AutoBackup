using System;
using System.Windows.Forms;

namespace AutoBackup
{
    public class WizardForm : Form
    {
        private Button nextBtn, backBtn;
        private int step = 0;
        private Panel panel;
        private TextBox destBox;
        private ComboBox scheduleCombo;
        private ListBox sourceList;
        private Button addSourceBtn;

        public WizardForm()
        {
            this.Text = "Мастер настройки резервного копирования";
            this.Size = new System.Drawing.Size(600, 400);
            panel = new Panel { Dock = DockStyle.Fill };
            nextBtn = new Button { Text = "Далее", Dock = DockStyle.Bottom };
            backBtn = new Button { Text = "Назад", Dock = DockStyle.Bottom, Visible = false };
            nextBtn.Click += NextStep;
            backBtn.Click += PreviousStep;
            this.Controls.Add(panel);
            this.Controls.Add(nextBtn);
            this.Controls.Add(backBtn);
            ShowStep();
        }

        private void ShowStep()
        {
            panel.Controls.Clear();
            if (step == 0)
            {
                Label lbl = new Label { Text = "Выберите папки для резервного копирования:", AutoSize = true, Location = new System.Drawing.Point(10, 10) };
                sourceList = new ListBox { Location = new System.Drawing.Point(10, 40), Size = new System.Drawing.Size(400, 200), SelectionMode = SelectionMode.MultiExtended };
                addSourceBtn = new Button { Text = "Добавить папку", Location = new System.Drawing.Point(420, 40) };
                addSourceBtn.Click += (s, e) =>
                {
                    using (var fbd = new FolderBrowserDialog()) { if (fbd.ShowDialog() == DialogResult.OK) sourceList.Items.Add(fbd.SelectedPath); }
                };
                panel.Controls.Add(lbl); panel.Controls.Add(sourceList); panel.Controls.Add(addSourceBtn);
            }
            else if (step == 1)
            {
                Label lbl = new Label { Text = "Целевая папка для резервных копий:", AutoSize = true, Location = new System.Drawing.Point(10, 10) };
                destBox = new TextBox { Location = new System.Drawing.Point(10, 40), Width = 400 };
                Button browse = new Button { Text = "Обзор", Location = new System.Drawing.Point(420, 38) };
                browse.Click += (s, e) => { using (var fbd = new FolderBrowserDialog()) if (fbd.ShowDialog() == DialogResult.OK) destBox.Text = fbd.SelectedPath; };
                panel.Controls.Add(lbl); panel.Controls.Add(destBox); panel.Controls.Add(browse);
            }
            else if (step == 2)
            {
                Label lbl = new Label { Text = "Расписание:", AutoSize = true, Location = new System.Drawing.Point(10, 10) };
                scheduleCombo = new ComboBox { Location = new System.Drawing.Point(10, 40), Width = 200 };
                scheduleCombo.Items.AddRange(new[] { "Ежедневно", "Еженедельно", "При запуске системы", "При простое" });
                scheduleCombo.SelectedIndex = 0;
                panel.Controls.Add(lbl); panel.Controls.Add(scheduleCombo);
            }
            backBtn.Visible = step > 0;
            if (step == 2) nextBtn.Text = "Готово";
            else nextBtn.Text = "Далее";
        }

        private void NextStep(object sender, EventArgs e)
        {
            if (step == 0) Config.Current.SourceFolders = sourceList.Items.Cast<string>().ToList();
            else if (step == 1) Config.Current.DestinationFolder = destBox.Text;
            else if (step == 2)
            {
                Config.Current.BackupSchedule = scheduleCombo.SelectedItem.ToString() switch
                {
                    "Ежедневно" => "Daily",
                    "Еженедельно" => "Weekly",
                    "При запуске системы" => "OnSystemStart",
                    "При простое" => "OnIdle",
                    _ => "Daily"
                };
                Config.Save();
                this.Close();
                return;
            }
            step++;
            ShowStep();
        }

        private void PreviousStep(object sender, EventArgs e) { step--; ShowStep(); }
    }
}