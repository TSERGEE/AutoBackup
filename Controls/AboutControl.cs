using System;
using System.Drawing;
using System.Windows.Forms;

namespace AutoBackup.Controls
{
    public class AboutControl : UserControl
    {
        public AboutControl()
        {
            this.BackColor = Color.FromArgb(24, 24, 27);
            var lbl = new Label
            {
                Text = "AutoBackup Professional\nВерсия 2.0\n\n© 2025\n\nРазработано с использованием Guna.UI2",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                AutoSize = false
            };
            this.Controls.Add(lbl);
        }
    }
}