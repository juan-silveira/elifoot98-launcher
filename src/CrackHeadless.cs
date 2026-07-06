using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Roda o CRACK.EXE via DOSBox oculto, injeta teclas por PostMessage/SendKeys
    // e captura a Contra-Senha do output.txt.
    // O usuario nao ve o DOSBox — comporta como o eli98kg3.web.app.
    public static class CrackHeadless
    {
        public class Result
        {
            public bool Ok;
            public string ContraSenha = "";
            public string RawOutput = "";
            public string Error = "";
        }

        public static Result Generate(GameLauncher launcher, int tipo, string senhaComHifens)
        {
            var result = new Result();
            var workDir = Path.Combine(Path.GetTempPath(), "elifoot_crack_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(workDir);
                File.Copy(launcher.CrackExe, Path.Combine(workDir, "CRACK.EXE"), overwrite: true);

                // conf: DOSBox oculto (posicao off-screen + tamanho minimo)
                var confPath = Path.Combine(workDir, "dosbox.conf");
                var conf = string.Join(Environment.NewLine, new[]
                {
                    "[sdl]",
                    "fullscreen=false",
                    "windowposition=-9999,-9999",
                    "windowresolution=200x150",
                    "output=texture",
                    "priority=lowest,lowest",
                    "",
                    "[autoexec]",
                    $"mount C \"{workDir}\"",
                    "C:",
                    "CRACK.EXE > output.txt",
                    "exit",
                });
                File.WriteAllText(confPath, conf);

                // Lança DOSBox
                var psi = new ProcessStartInfo
                {
                    FileName = launcher.DosBoxExe,
                    Arguments = $"-conf \"{confPath}\"",
                    WorkingDirectory = launcher.DosBoxDir,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = true,
                };
                var proc = Process.Start(psi);
                if (proc == null)
                {
                    result.Error = "Não consegui iniciar o DOSBox.";
                    return result;
                }

                // Espera janela do DOSBox aparecer (para PostMessage funcionar)
                IntPtr hwnd = IntPtr.Zero;
                for (int i = 0; i < 30 && hwnd == IntPtr.Zero; i++)
                {
                    Thread.Sleep(300);
                    proc.Refresh();
                    hwnd = proc.MainWindowHandle;
                    if (hwnd == IntPtr.Zero)
                        hwnd = FindDosBoxWindow(proc.Id);
                }
                if (hwnd == IntPtr.Zero)
                {
                    KillTree(proc);
                    result.Error = "Não achei a janela do DOSBox pra injetar teclas.";
                    return result;
                }

                // Dá um tempo pro CRACK subir e mostrar o menu
                Thread.Sleep(1500);

                // Sequencia: tipo, senha, S
                InjectString(hwnd, tipo.ToString());
                Thread.Sleep(300);
                InjectString(hwnd, senhaComHifens);
                InjectEnter(hwnd);
                Thread.Sleep(600);
                InjectString(hwnd, "S");
                InjectEnter(hwnd);

                // Aguarda DOSBox terminar (max 8s)
                if (!proc.WaitForExit(8000))
                {
                    KillTree(proc);
                }

                // Lê output.txt
                var outPath = Path.Combine(workDir, "output.txt");
                if (!File.Exists(outPath))
                {
                    outPath = Path.Combine(workDir, "OUTPUT.TXT");
                }
                if (!File.Exists(outPath))
                {
                    result.Error = "DOSBox não gerou output.txt.";
                    return result;
                }
                var raw = File.ReadAllText(outPath);
                result.RawOutput = raw;

                // Procura "Contra-senha:" seguida de padrão XXX-XXX-XXX-XXX-XXX
                var cs = ExtractContraSenha(raw);
                if (cs == null)
                {
                    result.Error = "Não consegui achar a Contra-Senha no output do CRACK. Talvez a Senha esteja em formato errado.";
                    return result;
                }
                result.ContraSenha = cs;
                result.Ok = true;
                return result;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private static string? ExtractContraSenha(string raw)
        {
            // Formato: XXX-XXX-XXX-XXX-XXX (5 grupos de 3 dígitos)
            var re = new System.Text.RegularExpressions.Regex(@"(\d{3}-\d{3}-\d{3}-\d{3}-\d{3})");
            System.Text.RegularExpressions.Match? last = null;
            foreach (System.Text.RegularExpressions.Match m in re.Matches(raw))
                last = m;
            return last?.Groups[1].Value;
        }

        private static IntPtr FindDosBoxWindow(int processId)
        {
            IntPtr result = IntPtr.Zero;
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == (uint)processId && IsWindowVisible(h))
                {
                    result = h;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static void InjectString(IntPtr hwnd, string s)
        {
            foreach (var ch in s)
            {
                PostMessage(hwnd, WM_CHAR, (IntPtr)ch, IntPtr.Zero);
                Thread.Sleep(30);
            }
        }

        private static void InjectEnter(IntPtr hwnd)
        {
            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
            PostMessage(hwnd, WM_CHAR, (IntPtr)0x0D, IntPtr.Zero);
            PostMessage(hwnd, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
            Thread.Sleep(50);
        }

        private static void KillTree(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        // ---- P/Invoke ----
        private const int WM_CHAR = 0x0102;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_RETURN = 0x0D;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
    }
}
