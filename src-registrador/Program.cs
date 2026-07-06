using System;
using System.IO;
using System.Windows.Forms;

namespace ElifootRegistrador
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var launcher = new LocalLauncher();
            if (!File.Exists(launcher.CrackExe))
            {
                MessageBox.Show(
                    "CRACK.EXE não encontrado em:\n" + launcher.CrackExe,
                    "Registrador Elifoot 98",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Application.Run(new KeygenForm(launcher));
        }
    }

    public class LocalLauncher
    {
        private readonly string _appDir = AppDomain.CurrentDomain.BaseDirectory;
        public string CrackExe => Path.Combine(_appDir, "CRACK.EXE");
        public string DosBoxDir => Path.Combine(_appDir, "vendor", "dosbox");
        public string DosBoxExe => Path.Combine(DosBoxDir, "dosbox.exe");
    }
}
