using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

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

        // otvdm emula Win 3.x com sua propria pasta WINDOWS.
        // Elifoot chama GetWindowsDirectory() e escreve/le eli.cod nela.
        public string OtvdmWindowsDir => Path.Combine(OtvdmDir, "WINDOWS");
        public string EliCodPath => Path.Combine(OtvdmWindowsDir, "eli.cod");

        public bool VerifyInstall(out string missing)
        {
            missing = "";
            if (!File.Exists(OtvdmExe)) { missing = OtvdmExe; return false; }
            if (!File.Exists(DosBoxExe)) { missing = DosBoxExe; return false; }
            if (!File.Exists(ElifootExe)) { missing = ElifootExe; return false; }
            return true;
        }

        public void LaunchElifoot(LauncherConfig cfg) => LaunchWithOtvdm(ElifootExe, cfg, "Elifoot 98");
        public void LaunchEditor(LauncherConfig cfg) => LaunchWithOtvdm(EditeqExe, cfg, "Editeq");

        private void LaunchWithOtvdm(string exePath, LauncherConfig cfg, string expectedTitleHint)
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
            var proc = Process.Start(psi);

            if (!cfg.Fullscreen && proc != null)
            {
                var w = cfg.ResolutionWidth;
                var h = cfg.ResolutionHeight;
                Task.Run(() => ResizeWhenReady(proc, expectedTitleHint, w, h));
            }
        }

        // Log de diagnostico das janelas visiveis. Ajuda a descobrir qual eh a
        // hwnd do Elifoot quando o match automatico falha.
        private static string DebugLogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElifootLauncher", "window-debug.log");

        // Aguarda a janela do jogo aparecer e forca tamanho/posicao.
        // Elifoot maximiza sozinho por default (DFM WindowState=wsMaximized),
        // entao acompanhamos por ~15s e tambem re-aplicamos se ele maximizar depois.
        private void ResizeWhenReady(Process proc, string titleHint, int width, int height)
        {
            const int maxAttempts = 40; // ~20s
            IntPtr hwnd = IntPtr.Zero;
            var log = new StringBuilder();
            log.AppendLine($"=== ResizeWhenReady start pid={proc.Id} hint='{titleHint}' target={width}x{height} at {DateTime.Now:HH:mm:ss} ===");

            for (int i = 0; i < maxAttempts; i++)
            {
                Thread.Sleep(500);
                if (proc.HasExited) { log.AppendLine("process exited"); break; }

                hwnd = FindGameWindow(proc.Id, titleHint, log, dumpAll: i == 4 || i == 10);
                if (hwnd != IntPtr.Zero)
                {
                    log.AppendLine($"attempt {i}: found hwnd 0x{hwnd.ToInt64():x}");
                    break;
                }
            }

            if (hwnd == IntPtr.Zero)
            {
                log.AppendLine("!!! no matching window found after 20s !!!");
                WriteLog(log.ToString());
                return;
            }

            ForceWindowed(hwnd, width, height, log);

            // Re-aplica algumas vezes caso o app maximize depois de carregar
            for (int i = 0; i < 8; i++)
            {
                Thread.Sleep(500);
                if (proc.HasExited) break;
                ForceWindowed(hwnd, width, height, log);
            }
            WriteLog(log.ToString());
        }

        private static void WriteLog(string content)
        {
            try
            {
                var dir = Path.GetDirectoryName(DebugLogPath);
                if (dir != null) Directory.CreateDirectory(dir);
                File.AppendAllText(DebugLogPath, content + Environment.NewLine);
            }
            catch { }
        }

        private static IntPtr FindGameWindow(int processId, string titleHint, StringBuilder log, bool dumpAll)
        {
            IntPtr result = IntPtr.Zero;
            EnumWindows((h, _) =>
            {
                if (!IsWindowVisible(h)) return true;

                var sb = new StringBuilder(256);
                GetWindowText(h, sb, sb.Capacity);
                var title = sb.ToString();
                if (title.Length == 0) return true;

                GetWindowThreadProcessId(h, out uint pid);

                if (dumpAll)
                    log.AppendLine($"  visible hwnd=0x{h.ToInt64():x} pid={pid} title='{title}'");

                if (title.IndexOf(titleHint, StringComparison.OrdinalIgnoreCase) >= 0
                    || title.IndexOf("ELIFOOT", StringComparison.OrdinalIgnoreCase) >= 0
                    || title.IndexOf("EDITEQ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    log.AppendLine($"  MATCH hwnd=0x{h.ToInt64():x} title='{title}'");
                    result = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static void ForceWindowed(IntPtr hwnd, int width, int height, StringBuilder log)
        {
            // Remove estilo WS_MAXIMIZE se estiver setado
            var style = GetWindowLong(hwnd, GWL_STYLE);
            if ((style & WS_MAXIMIZE) != 0)
            {
                SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZE);
                log.AppendLine($"  removed WS_MAXIMIZE from style=0x{style:x}");
            }

            // Restaura de qualquer forma (desmaximiza se estiver assim)
            ShowWindow(hwnd, SW_RESTORE);

            var screen = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1024, 768);
            int x = screen.X + Math.Max(0, (screen.Width - width) / 2);
            int y = screen.Y + Math.Max(0, (screen.Height - height) / 2);
            bool ok = SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
                SWP_NOZORDER | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            log.AppendLine($"  SetWindowPos({x},{y},{width}x{height}) => {ok}");
        }

        private void WriteOtvdmIni(LauncherConfig cfg)
        {
            var iniPath = Path.Combine(OtvdmDir, "otvdm.ini");
            var ini = string.Join(Environment.NewLine, new[]
            {
                "[otvdm]",
                "EnableVisualStyle=0",
                "DisableAero=1",
                cfg.Fullscreen ? "FixScreenSize=0" : "FixScreenSize=1",
            });
            try
            {
                File.WriteAllText(iniPath, ini);
            }
            catch (UnauthorizedAccessException)
            {
                // Sem permissao: segue com ini padrao do bundle.
            }
        }

        public void LaunchCrack()
        {
            if (!File.Exists(CrackExe))
                throw new FileNotFoundException($"CRACK.EXE não encontrado: {CrackExe}");

            // Garante que a pasta WINDOWS do otvdm existe e tem uma copia
            // do CRACK.EXE. eli.cod que o CRACK gerar vai parar exatamente
            // onde o Elifoot procura ao rodar via otvdm.
            Directory.CreateDirectory(OtvdmWindowsDir);
            var crackDest = Path.Combine(OtvdmWindowsDir, "CRACK.EXE");
            try
            {
                File.Copy(CrackExe, crackDest, overwrite: true);
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback: se nao pode copiar, ao menos mostra erro claro no throw abaixo
                if (!File.Exists(crackDest))
                    throw new IOException($"Nao consigo copiar CRACK.EXE para {OtvdmWindowsDir}");
            }

            var confPath = Path.Combine(Path.GetTempPath(), "elifoot_crack.conf");
            var conf = string.Join(Environment.NewLine, new[]
            {
                "[sdl]",
                "fullscreen=false",
                "windowresolution=800x600",
                "output=texture",
                "",
                "[autoexec]",
                // Monta a pasta WINDOWS do otvdm como C:. O eli.cod que
                // CRACK escrever fica visivel pro Elifoot na hora de rodar.
                $"mount C \"{OtvdmWindowsDir}\"",
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

        public bool EliCodExists() => File.Exists(EliCodPath);

        // ------------ P/Invoke ------------
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const int SW_RESTORE = 9;
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZE = 0x01000000;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
