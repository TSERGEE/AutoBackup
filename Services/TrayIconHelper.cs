using System;
using System.Windows.Forms;

namespace AutoBackup.Services
{
    public static class TrayIconHelper
    {
        public static NotifyIcon TrayIcon { get; set; }

        public static void Initialize(NotifyIcon icon)
        {
            TrayIcon = icon;
        }

        public static void UpdateStatus(string status)
        {
            if (TrayIcon != null && !string.IsNullOrEmpty(status))
                TrayIcon.Text = $"AutoBackup - {status}";
        }

        public static void ShowBalloon(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (TrayIcon != null)
                TrayIcon.ShowBalloonTip(3000, title, text, icon);
        }
    }
}