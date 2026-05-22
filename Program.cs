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
            // Глобальный перехват необработанных исключений (п.27)
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception ex = e.ExceptionObject as Exception;
                string msg = ex?.Message ?? "Критическая ошибка. Подробности в логе.";
                Logger.LogError("Unhandled", ex);
                MessageBox.Show(msg, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.Run(new TrayApplicationContext());
        }
    }
}