using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MacroRecorderPro.UI;

namespace MacroRecorderPro
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MacroForm());
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}