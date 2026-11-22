using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace LotteryTool
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // 高 DPI 支持：避免高分屏下界面模糊、控件错位
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 全局异常兜底：WinExe 无控制台，未处理异常原本会直接终止进程，
            // 用户只会看到"程序不能运行、没报任何错误"。这里统一写日志并弹窗，
            // 保证任何启动失败都可见、可定位。
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ReportFatal("界面线程异常", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ReportFatal("未处理异常", e.ExceptionObject as Exception);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ReportFatal("启动失败", ex);
            }
        }

        private static void ReportFatal(string title, Exception? ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}");
            sb.AppendLine(ex?.ToString() ?? "(无异常对象)");

            try
            {
                string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                File.AppendAllText(log, sb + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // 日志写失败也不能掩盖原始错误
            }

            MessageBox.Show(
                $"{title}，程序已停止。\r\n\r\n{ex?.Message}\r\n\r\n" +
                "详细信息已写入程序目录下的 startup_error.log",
                "大乐透分析工具 - 运行错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
