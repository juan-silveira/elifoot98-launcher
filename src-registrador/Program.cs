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
            try
            {
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var installDir = Payload.ResolveInstallDir(appDir);
                var launcher = new LocalLauncher(installDir);
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
            catch (Exception ex)
            {
                MessageBox.Show("Erro fatal na inicialização:\n" + ex.Message,
                    "Registrador Elifoot 98", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    public class LocalLauncher
    {
        private readonly string _dir;
        public LocalLauncher(string installDir) { _dir = installDir; }
        public string CrackExe => Path.Combine(_dir, "CRACK.EXE");
        public string DosBoxDir => Path.Combine(_dir, "vendor", "dosbox");
        public string DosBoxExe => Path.Combine(DosBoxDir, "dosbox.exe");
    }
}
