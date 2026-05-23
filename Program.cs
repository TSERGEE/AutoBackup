using AutoBackup.Services;
using AutoBackup.Utils;
using System;
using System.Windows.Forms;

namespace AutoBackup
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception ex = e.ExceptionObject as Exception;
                Logger.LogError("Unhandled", ex);
                MessageBox.Show(ex?.Message ?? "Критическая ошибка.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            Application.Run(new TrayApplicationContext());
        }
    }
}