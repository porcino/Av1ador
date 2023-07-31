using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Av1ador
{
    internal static class Program
    {
        public static bool Log { get; set; }
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!Debugger.IsAttached)
            {
                AppDomain.CurrentDomain.UnhandledException += AllUnhandledExceptions;
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
        private static void AllUnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = (Exception)e.ExceptionObject;
            if (Log)
                System.IO.File.WriteAllText("log_" + string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now) + ".txt", ex.ToString());
            Environment.Exit(System.Runtime.InteropServices.Marshal.GetHRForException(ex));
        }
    }
}
