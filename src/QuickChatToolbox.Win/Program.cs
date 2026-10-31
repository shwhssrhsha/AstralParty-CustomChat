using System;
using System.Threading;
using System.Windows.Forms;

namespace QuickChatToolboxWin
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "QuickChatToolboxWin_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("工具已经在运行（任务栏里找一下）。", "快捷短语工具箱", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Ipc.Log("UI exception: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Ipc.Log("domain exception: " + e.ExceptionObject);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Ipc.Log("main start, base=" + AppContext.BaseDirectory);
                try
                {
                    Application.Run(new MainForm());
                    Ipc.Log("main returned");
                }
                catch (Exception ex)
                {
                    Ipc.Log("main exception: " + ex);
                    throw;
                }
            }
        }
    }
}
