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
            var diag = new StringBuilder();
            diag.AppendLine($"=== CrackHeadless.Generate at {DateTime.Now:HH:mm:ss} tipo={tipo} senha='{senhaComHifens}' ===");
            try
            {
                // Mata quaisquer DOSBox rodando de tentativas anteriores
                foreach (var name in new[] { "dosbox", "dosbox_with_debugger" })
                {
                    foreach (var old in Process.GetProcessesByName(name))
                    {
                        try { old.Kill(); old.WaitForExit(2000); diag.AppendLine($"killed leftover {name} pid={old.Id}"); }
                        catch (Exception ex) { diag.AppendLine($"couldn't kill {name}: {ex.Message}"); }
                    }
                }
                Thread.Sleep(300); // deixa filesystem soltar handles

                Directory.CreateDirectory(workDir);
                diag.AppendLine($"workDir={workDir}");

                // Retry no copy do CRACK.EXE caso esteja lockado
                Exception? copyErr = null;
                for (int i = 0; i < 5; i++)
                {
                    try { File.Copy(launcher.CrackExe, Path.Combine(workDir, "CRACK.EXE"), overwrite: true); copyErr = null; break; }
                    catch (Exception ex) { copyErr = ex; Thread.Sleep(200); }
                }
                if (copyErr != null) throw new IOException($"Nao consegui copiar CRACK.EXE: {copyErr.Message}");

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

                // 1) Espera as janelas do DOSBox aparecerem e acha a janela SDL principal.
                // DOSBox-Staging abre DUAS janelas:
                //   - 'DOSBox Status Window' (secundaria, logs)
                //   - 'DOSBox Staging - N cycles/ms' (principal SDL, recebe input)
                // proc.MainWindowHandle retorna a errada. Precisa filtrar por titulo/classe.
                IntPtr hwndMain = IntPtr.Zero;
                IntPtr hwndStatus = IntPtr.Zero;
                for (int i = 0; i < 40 && hwndMain == IntPtr.Zero; i++)
                {
                    Thread.Sleep(200);
                    FindDosBoxWindows(proc.Id, out hwndMain, out hwndStatus, diag: i == 5 ? diag : null);
                }
                if (hwndMain == IntPtr.Zero)
                {
                    result.Error = "Nao achei a janela SDL principal do DOSBox.";
                    return result;
                }
                diag.AppendLine($"hwndMain=0x{hwndMain.ToInt64():x}, hwndStatus=0x{hwndStatus.ToInt64():x}");

                // 2) Move AMBAS as janelas do DOSBox pra fora da tela
                SetWindowPos(hwndMain, IntPtr.Zero, -32000, -32000, 200, 150,
                    SWP_NOZORDER | SWP_NOACTIVATE);
                if (hwndStatus != IntPtr.Zero)
                {
                    SetWindowPos(hwndStatus, IntPtr.Zero, -32000, -32100, 200, 150,
                        SWP_NOZORDER | SWP_NOACTIVATE);
                }
                var hwnd = hwndMain; // resto do codigo usa 'hwnd' pra main

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

                // 7) Aguarda DOSBox terminar (max 15s)
                bool exited = proc.WaitForExit(15000);
                diag.AppendLine($"DOSBox exited={exited}, exitCode={(exited ? proc.ExitCode.ToString() : "n/a")}");
                if (!exited)
                {
                    KillTree(proc);
                    Thread.Sleep(500); // deixa handles soltarem
                }

                // 8) Le output com retry
                var outPath = Path.Combine(workDir, "output.txt");
                if (!File.Exists(outPath)) outPath = Path.Combine(workDir, "OUTPUT.TXT");
                diag.AppendLine($"outPath={outPath}, exists={File.Exists(outPath)}");
                if (!File.Exists(outPath))
                {
                    result.Error = "DOSBox nao gerou output.txt. Verifique se o DOSBox esta bloqueado por antivirus.";
                    WriteDiag(diag);
                    return result;
                }
                string? raw = null;
                Exception? readErr = null;
                for (int i = 0; i < 8; i++)
                {
                    try { raw = File.ReadAllText(outPath); readErr = null; break; }
                    catch (Exception ex) { readErr = ex; Thread.Sleep(250); }
                }
                if (raw == null) throw new IOException($"Nao consegui ler output.txt: {readErr?.Message}");
                result.RawOutput = raw;
                diag.AppendLine($"raw output length={raw.Length}");

                var cs = ExtractContraSenha(raw);
                if (cs == null)
                {
                    // Sem Contra-Senha: SendInput nao pegou. Salva RAW pro diag.
                    diag.AppendLine("!!! no XXX-XXX-XXX-XXX-XXX pattern found in output !!!");
                    diag.AppendLine("--- RAW OUTPUT (first 500 chars) ---");
                    diag.AppendLine(raw.Length > 500 ? raw.Substring(0, 500) : raw);
                    result.Error = "Nao consegui achar a Contra-Senha no output. As teclas nao chegaram no CRACK. Veja o log em %APPDATA%\\ElifootLauncher\\crack-debug.log";
                    WriteDiag(diag);
                    return result;
                }
                result.ContraSenha = cs;
                result.Ok = true;
                WriteDiag(diag);
                return result;
            }
            catch (Exception ex)
            {
                diag.AppendLine($"!!! EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                diag.AppendLine(ex.StackTrace ?? "");
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                WriteDiag(diag);
                return result;
            }
            finally
            {
                if (attachedFromThread != 0)
                {
                    try { AttachThreadInput(attachedFromThread, attachedToThread, false); } catch { }
                }
                if (proc != null) KillTree(proc);
                // Delete com retry
                for (int i = 0; i < 3; i++)
                {
                    try { Directory.Delete(workDir, recursive: true); break; }
                    catch { Thread.Sleep(300); }
                }
            }
        }

        private static void WriteDiag(StringBuilder diag)
        {
            try
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElifootLauncher", "crack-debug.log");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, diag.ToString() + Environment.NewLine);
            }
            catch { }
        }

        private static string? ExtractContraSenha(string raw)
        {
            var re = new System.Text.RegularExpressions.Regex(@"(\d{3}-\d{3}-\d{3}-\d{3}-\d{3})");
            System.Text.RegularExpressions.Match? last = null;
            foreach (System.Text.RegularExpressions.Match m in re.Matches(raw))
                last = m;
            return last?.Groups[1].Value;
        }

        // Retorna as duas janelas do DOSBox: main (SDL, recebe input) e status.
        // Main = titulo contem 'DOSBox Staging' e NAO contem 'Status'.
        // Se so encontrar uma, retorna essa como main.
        private static void FindDosBoxWindows(int processId, out IntPtr main, out IntPtr status, StringBuilder? diag)
        {
            IntPtr mainLocal = IntPtr.Zero;
            IntPtr statusLocal = IntPtr.Zero;
            long biggestArea = 0;
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid != (uint)processId) return true;

                var t = new StringBuilder(256);
                GetWindowText(h, t, t.Capacity);
                var title = t.ToString();

                var c = new StringBuilder(128);
                GetClassName(h, c, c.Capacity);
                var cls = c.ToString();

                GetWindowRect(h, out RECT r);
                long area = Math.Max(0, (long)(r.Right - r.Left) * (r.Bottom - r.Top));
                bool vis = IsWindowVisible(h);

                if (diag != null)
                    diag.AppendLine($"  dosbox window hwnd=0x{h.ToInt64():x} vis={vis} area={area} class='{cls}' title='{title}'");

                if (!vis) return true;

                bool isStatus = title.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isStatus) { statusLocal = h; return true; }

                // Main: preferir por classe SDL, senao pela maior area
                if (cls.StartsWith("SDL", StringComparison.OrdinalIgnoreCase))
                {
                    mainLocal = h;
                    biggestArea = area;
                }
                else if (mainLocal == IntPtr.Zero && area > biggestArea)
                {
                    mainLocal = h;
                    biggestArea = area;
                }
                return true;
            }, IntPtr.Zero);
            main = mainLocal;
            status = statusLocal;
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

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }
    }
}
