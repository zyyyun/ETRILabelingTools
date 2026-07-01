using System;
using System.Runtime.InteropServices;

namespace WinFormsApp1
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            CudaEnvironmentHelper.EnsureCudaPathOnProcess();
            WinmmTimer.BeginPeriod(1);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new Form1());
            }
            finally
            {
                WinmmTimer.EndPeriod(1);
            }
        }
    }

    internal static class WinmmTimer
    {
        [DllImport("winmm.dll", ExactSpelling = true)]
        public static extern int timeBeginPeriod(int period);

        [DllImport("winmm.dll", ExactSpelling = true)]
        public static extern int timeEndPeriod(int period);

        public static void BeginPeriod(int period)
        {
            try { timeBeginPeriod(period); } catch { }
        }

        public static void EndPeriod(int period)
        {
            try { timeEndPeriod(period); } catch { }
        }
    }
}
