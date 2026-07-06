using System;
using System.Diagnostics;
using System.IO;

namespace ElifootLauncher
{
    public class GameLauncher
    {
        private readonly string _appDir;

        public GameLauncher()
        {
            _appDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        public string VendorDir => Path.Combine(_appDir, "vendor");
        public string OtvdmDir => Path.Combine(VendorDir, "otvdm");
        public string OtvdmExe => Path.Combine(OtvdmDir, "otvdmw.exe");
        public string DosBoxDir => Path.Combine(VendorDir, "dosbox");
        public string DosBoxExe => Path.Combine(DosBoxDir, "dosbox.exe");
        public string GameDir => Path.Combine(_appDir, "game");
        public string ElifootExe => Path.Combine(GameDir, "ELIFOOT.EXE");
        public string EditeqExe => Path.Combine(GameDir, "EDITEQ.EXE");
        public string CrackExe => Path.Combine(GameDir, "CRACK.EXE");

        public bool VerifyInstall(out string missing)
        {
            missing = "";
            if (!File.Exists(OtvdmExe)) { missing = OtvdmExe; return false; }
            if (!File.Exists(DosBoxExe)) { missing = DosBoxExe; return false; }
            if (!File.Exists(ElifootExe)) { missing = ElifootExe; return false; }
            return true;
        }

        public void LaunchElifoot(LauncherConfig cfg) => LaunchWithOtvdm(ElifootExe, cfg);
        public void LaunchEditor(LauncherConfig cfg) => LaunchWithOtvdm(EditeqExe, cfg);

        private void LaunchWithOtvdm(string exePath, LauncherConfig cfg)
        {
            if (!File.Exists(exePath))
                throw new FileNotFoundException($"Executável não encontrado: {exePath}");

            WriteOtvdmIni(cfg);

            var psi = new ProcessStartInfo
            {
                FileName = OtvdmExe,
                Arguments = $"\"{exePath}\"",
                WorkingDirectory = GameDir,
                UseShellExecute = false,
            };
            Process.Start(psi);
        }

        private void WriteOtvdmIni(LauncherConfig cfg)
        {
            // Escreve otvdm.ini com preferências do usuário.
            var iniPath = Path.Combine(OtvdmDir, "otvdm.ini");
            var ini = string.Join(Environment.NewLine, new[]
            {
                "[otvdm]",
                "EnableVisualStyle=0",
                "DisableAero=1",
                cfg.Fullscreen ? "FixScreenSize=0" : "FixScreenSize=1",
                $"Width={cfg.ResolutionWidth}",
                $"Height={cfg.ResolutionHeight}",
            });
            File.WriteAllText(iniPath, ini);
        }

        public void LaunchCrack()
        {
            if (!File.Exists(CrackExe))
                throw new FileNotFoundException($"CRACK.EXE não encontrado: {CrackExe}");

            var confPath = Path.Combine(Path.GetTempPath(), "elifoot_crack.conf");
            var conf = string.Join(Environment.NewLine, new[]
            {
                "[sdl]",
                "fullscreen=false",
                "windowresolution=800x600",
                "output=texture",
                "",
                "[autoexec]",
                $"mount C \"{GameDir}\"",
                "C:",
                "CRACK.EXE",
            });
            File.WriteAllText(confPath, conf);

            var psi = new ProcessStartInfo
            {
                FileName = DosBoxExe,
                Arguments = $"-conf \"{confPath}\"",
                WorkingDirectory = DosBoxDir,
                UseShellExecute = false,
            };
            Process.Start(psi);
        }
    }
}
