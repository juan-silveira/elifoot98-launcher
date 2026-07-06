using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ElifootLauncher
{
    // Roda o CRACK.EXE via DOSBox off-screen, injeta teclas via SendInput
    // (que funciona com SDL do DOSBox, diferente de PostMessage).
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
            Process? proc = null;
            uint attachedFromThread = 0;
            uint attachedToThread = 0;
            IntPtr previousForeground = IntPtr.Zero;
            try
            {
                Directory.CreateDirectory(workDir);
                File.Copy(launcher.CrackExe, Path.Combine(workDir, "CRACK.EXE"), overwrite: true);

                var confPath = Path.Combine(workDir, "dosbox.conf");
                var conf = string.Join(Environment.NewLine, new[]
                {
                    "[sdl]",
                    "fullscreen=false",
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

                var psi = new ProcessStartInfo
                {
                    FileName = launcher.DosBoxExe,
                    Arguments = $"-conf \"{confPath}\"",
                    WorkingDirectory = launcher.DosBoxDir,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    CreateNoWindow = false,
                };
                proc = Process.Start(psi);
                if (proc == null)
                {
                    result.Error = "Nao consegui iniciar o DOSBox.";
                    return result;
                }

                // 1) Espera janela do DOSBox aparecer
                IntPtr hwnd = IntPtr.Zero;
                for (int i = 0; i < 40 && hwnd == IntPtr.Zero; i++)
                {
                    Thread.Sleep(200);
                    proc.Refresh();
                    hwnd = proc.MainWindowHandle;
                    if (hwnd == IntPtr.Zero) hwnd = FindDosBoxWindow(proc.Id);
                }
                if (hwnd == IntPtr.Zero)
                {
                    result.Error = "Nao achei a janela do DOSBox.";
                    return result;
                }

                // 2) Move o DOSBox pra fora da tela (invisivel pro usuario)
                SetWindowPos(hwnd, IntPtr.Zero, -32000, -32000, 200, 150,
                    SWP_NOZORDER | SWP_NOACTIVATE);

                // 3) Espera CRACK subir e desenhar o menu
                Thread.Sleep(1500);

                // 4) Attach input queue + traz foco pra janela off-screen
                previousForeground = GetForegroundWindow();
                uint targetThread = GetWindowThreadProcessId(hwnd, out _);
                attachedFromThread = GetCurrentThreadId();
                attachedToThread = targetThread;
                AttachThreadInput(attachedFromThread, attachedToThread, true);
                SetForegroundWindow(hwnd);
                SetFocus(hwnd);
                Thread.Sleep(200);

                // 5) Injeta teclas via SendInput
                InjectDigit(tipo);
                Thread.Sleep(300);

                foreach (var ch in senhaComHifens)
                {
                    if (ch == '-') InjectHyphen();
                    else if (ch >= '0' && ch <= '9') InjectDigit(ch - '0');
                    else if (char.IsLetter(ch)) InjectLetter(ch);
                    Thread.Sleep(40);
                }
                InjectEnter();
                Thread.Sleep(700);
                InjectLetter('S');
                Thread.Sleep(200);
                InjectEnter();

                // 6) Detach + restaura foreground original
                AttachThreadInput(attachedFromThread, attachedToThread, false);
                attachedFromThread = 0;
                attachedToThread = 0;
                if (previousForeground != IntPtr.Zero)
                    SetForegroundWindow(previousForeground);

                // 7) Aguarda DOSBox terminar
                if (!proc.WaitForExit(10000))
                {
                    KillTree(proc);
                }

                // 8) Le output
                var outPath = File.Exists(Path.Combine(workDir, "output.txt"))
                    ? Path.Combine(workDir, "output.txt")
                    : Path.Combine(workDir, "OUTPUT.TXT");
                if (!File.Exists(outPath))
                {
                    result.Error = "DOSBox nao gerou output.txt.";
                    return result;
                }
                var raw = File.ReadAllText(outPath);
                result.RawOutput = raw;

                var cs = ExtractContraSenha(raw);
                if (cs == null)
                {
                    result.Error = "Nao consegui achar a Contra-Senha no output. Verifique se a Senha tem 6 grupos de 3 digitos.";
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
                if (attachedFromThread != 0)
                {
                    try { AttachThreadInput(attachedFromThread, attachedToThread, false); } catch { }
                }
                if (proc != null) KillTree(proc);
                try { Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private static string? ExtractContraSenha(string raw)
        {
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

        // ---- SendInput helpers ----
        private static void InjectDigit(int d)
        {
            // '0' = VK 0x30, '1' = 0x31 ...
            SendKey((ushort)(0x30 + d));
        }

        private static void InjectLetter(char c)
        {
            // Letras maiusculas: 'A'=0x41, 'B'=0x42, ... 'S'=0x53
            var up = char.ToUpper(c);
            if (up < 'A' || up > 'Z') return;
            SendKey((ushort)up);
        }

        private static void InjectHyphen()
        {
            // '-' = VK_OEM_MINUS = 0xBD
            SendKey(0xBD);
        }

        private static void InjectEnter()
        {
            SendKey(0x0D); // VK_RETURN
        }

        private static void SendKey(ushort vk)
        {
            var inputs = new INPUT[2];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = vk;
            inputs[0].u.ki.dwFlags = 0;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = vk;
            inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void KillTree(Process p)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
        }

        // ---- P/Invoke ----
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL, wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    }
}
