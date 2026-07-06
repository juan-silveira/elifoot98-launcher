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

            // otvdm respeita AppCompat Layers do Windows: se registro
            // HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\
            // Layers tiver o path do exe com valor '640X480', o otvdm faz
            // GetSystemMetrics(SM_CXSCREEN)=640 e SM_CYSCREEN=480 pro processo
            // emulado. Elifoot desenha tudo em 640x480, cabe na janela sem
            // cortar conteudo.
            if (!cfg.Fullscreen)
            {
                SetCompatLayer(exePath, "640X480");
            }
            else
            {
                RemoveCompatLayer(exePath);
            }

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

        // Escreve entrada no registro do Windows pra ativar o AppCompat layer
        // que o otvdm entende. Escreve com MULTIPLAS variantes do path porque
        // otvdm chama GetShortPathNameA em algumas rotas — se o registry so
        // tiver a long-path e otvdm procurar short-path, nao bate.
        private static void SetCompatLayer(string exePath, string layer)
        {
            var log = new StringBuilder();
            log.AppendLine($"=== SetCompatLayer at {DateTime.Now:HH:mm:ss} ===");
            log.AppendLine($"exePath (long): '{exePath}'");
            var shortPath = GetShortPath(exePath);
            log.AppendLine($"exePath (short): '{shortPath}'");
            var variants = new System.Collections.Generic.HashSet<string> { exePath };
            if (!string.IsNullOrEmpty(shortPath)) variants.Add(shortPath);
            variants.Add(exePath.ToUpperInvariant());
            variants.Add(exePath.ToLowerInvariant());

            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"))
                {
                    if (key == null)
                    {
                        log.AppendLine("!!! CreateSubKey returned null !!!");
                    }
                    else
                    {
                        foreach (var v in variants)
                        {
                            try
                            {
                                key.SetValue(v, layer, Microsoft.Win32.RegistryValueKind.String);
                                log.AppendLine($"  wrote '{v}' = '{layer}'");
                            }
                            catch (Exception ex)
                            {
                                log.AppendLine($"  FAIL '{v}': {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine($"!!! outer exception: {ex.Message}");
            }
            WriteLog(log.ToString());
        }

        private static void RemoveCompatLayer(string exePath)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", writable: true))
                {
                    if (key != null)
                    {
                        var shortPath = GetShortPath(exePath);
                        foreach (var v in new[] { exePath, shortPath, exePath.ToUpperInvariant(), exePath.ToLowerInvariant() })
                        {
                            if (string.IsNullOrEmpty(v)) continue;
                            try { key.DeleteValue(v, throwOnMissingValue: false); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);

        private static string GetShortPath(string longPath)
        {
            try
            {
                var sb = new StringBuilder(260);
                uint r = GetShortPathName(longPath, sb, sb.Capacity);
                return r > 0 ? sb.ToString() : "";
            }
            catch { return ""; }
        }

        // Log de diagnostico das janelas visiveis. Ajuda a descobrir qual eh a
        // hwnd do Elifoot quando o match automatico falha.
        private static string DebugLogPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ElifootLauncher", "window-debug.log");

        // Monitora as janelas do jogo respeitando move/resize manual do usuario.
        // Regras:
        // - Se janela esta "fullscreen" (area >= 85% da tela): forca windowed
        //   (centraliza e resize pro target). Isso pega o re-maximize do Delphi.
        // - Se janela nao esta fullscreen: DEIXA EM PAZ. Usuario pode mover/resize.
        // - Popups pequenos: SetWindowPos com HWND_TOP a cada iter pra ficarem
        //   sempre acima da janela principal (evita bug do alt+tab).
        private void ResizeWhenReady(Process proc, string titleHint, int width, int height)
        {
            var log = new StringBuilder();
            log.AppendLine($"=== ResizeWhenReady start pid={proc.Id} target={width}x{height} at {DateTime.Now:HH:mm:ss} ===");

            var tracked = new System.Collections.Generic.HashSet<IntPtr>();
            int loop = 0;

            var screen = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1024, 768);
            long screenArea = (long)screen.Width * screen.Height;
            long fullscreenThreshold = (long)(screenArea * 0.85);

            while (!proc.HasExited)
            {
                bool logDump = loop < 3 || loop == 10 || loop % 300 == 0;
                var hwnds = FindAllGameWindows(logDump ? log : null);

                // Separa em "principais" (grandes) e "popups" (pequenas)
                // para dar tratamento diferente.
                var mains = new System.Collections.Generic.List<IntPtr>();
                var popups = new System.Collections.Generic.List<IntPtr>();

                foreach (var hwnd in hwnds)
                {
                    bool firstTime = tracked.Add(hwnd);
                    GetWindowRect(hwnd, out RECT r);
                    long area = (long)(r.Right - r.Left) * (r.Bottom - r.Top);
                    int style = GetWindowLong(hwnd, GWL_STYLE);
                    bool isMaxStyle = (style & WS_MAXIMIZE) != 0;

                    // Categoriza
                    if (area >= screenArea * 0.20) mains.Add(hwnd);
                    else popups.Add(hwnd);

                    if (area >= fullscreenThreshold || isMaxStyle)
                    {
                        // Fullscreen OU flag de maximize setada: força windowed
                        ForceWindowed(hwnd, width, height, log, verbose: firstTime || loop % 50 == 0);
                        if (firstTime) log.AppendLine($"  [+ hwnd=0x{hwnd.ToInt64():x} was {(area >= fullscreenThreshold ? "fullscreen" : "WS_MAXIMIZE style set")}]");
                    }
                    else if (firstTime)
                    {
                        // Primeira vez que vejo, e nao esta fullscreen: LIMPA estilos
                        // de maximize (mas mantem WS_MAXIMIZEBOX pra usuario poder
                        // maximizar depois). Isso destrava o drag da titlebar.
                        int cleanStyle = style & ~WS_MAXIMIZE;
                        if (cleanStyle != style)
                        {
                            SetWindowLong(hwnd, GWL_STYLE, cleanStyle);
                            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                            log.AppendLine($"  [+ hwnd=0x{hwnd.ToInt64():x} cleared WS_MAXIMIZE style (was 0x{style:x})]");
                        }
                        else if (logDump)
                        {
                            log.AppendLine($"  [+ hwnd=0x{hwnd.ToInt64():x} already ok, {r.Right - r.Left}x{r.Bottom - r.Top}]");
                        }
                    }
                }

                // Popups (dialogs, "Acerca", etc.) — trazer pro topo sem roubar
                // foco. Isso resolve o bug do alt+tab que os deixava atras da tela.
                if (popups.Count > 0 && mains.Count > 0)
                {
                    foreach (var pop in popups)
                    {
                        SetWindowPos(pop, HWND_TOP, 0, 0, 0, 0,
                            SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    }
                }

                // Cleanup: janelas que morreram
                tracked.RemoveWhere(h => !IsWindow(h));

                loop++;
                Thread.Sleep(200);

                if (loop % 300 == 0)
                {
                    log.AppendLine($"--- flush at loop={loop}, tracked={tracked.Count}, mains={mains.Count}, popups={popups.Count} ---");
                    WriteLog(log.ToString());
                    log.Clear();
                }
            }
            log.AppendLine($"=== process exited after {loop} loops ===");
            WriteLog(log.ToString());
        }

        // Retorna TODAS as janelas top-level relevantes (do processo otvdm com area util).
        // Nao usa titulo — pega qualquer form Delphi grande.
        private static System.Collections.Generic.List<IntPtr> FindAllGameWindows(StringBuilder? log)
        {
            var results = new System.Collections.Generic.List<IntPtr>();
            uint launcherPid = (uint)Process.GetCurrentProcess().Id;

            var otvdmPids = new System.Collections.Generic.HashSet<uint>();
            foreach (var pname in new[] { "otvdmw", "otvdm" })
                foreach (var p in Process.GetProcessesByName(pname))
                {
                    try { otvdmPids.Add((uint)p.Id); } catch { }
                }
            if (otvdmPids.Count == 0) return results;

            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == launcherPid) return true;
                if (!otvdmPids.Contains(pid)) return true;
                if (!IsWindowVisible(h)) return true;

                GetWindowRect(h, out RECT r);
                int w = r.Right - r.Left;
                int hgt = r.Bottom - r.Top;
                if (w < 300 || hgt < 200) return true; // ignora barra de ferramentas / tooltips

                var sb = new StringBuilder(256);
                GetWindowText(h, sb, sb.Capacity);
                var cls = new StringBuilder(128);
                GetClassName(h, cls, cls.Capacity);

                if (log != null)
                    log.AppendLine($"  candidate hwnd=0x{h.ToInt64():x} pid={pid} {w}x{hgt} class='{cls}' title='{sb}'");

                results.Add(h);
                return true;
            }, IntPtr.Zero);

            return results;
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

        // Match "estrito": title == "Elifoot 98" exato ou classe TApplication/TForm de Delphi.
        // Se nao achar por titulo, retorna a janela do processo com maior area
        // (heuristica para pegar a janela fullscreen sem titulo).
        private static IntPtr FindGameWindow(string titleHint, StringBuilder log, bool dumpAll)
        {
            IntPtr byTitle = IntPtr.Zero;
            IntPtr byArea = IntPtr.Zero;
            long bestArea = 0;
            uint launcherPid = (uint)Process.GetCurrentProcess().Id;

            // Descobrir pids relacionados ao otvdm (processo que a gente lancou)
            var otvdmPids = new System.Collections.Generic.HashSet<uint>();
            foreach (var p in Process.GetProcessesByName("otvdmw"))
            {
                try { otvdmPids.Add((uint)p.Id); } catch { }
            }
            foreach (var p in Process.GetProcessesByName("otvdm"))
            {
                try { otvdmPids.Add((uint)p.Id); } catch { }
            }

            EnumWindows((h, _) =>
            {
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, sb.Capacity);
                var title = sb.ToString();

                var cls = new StringBuilder(128);
                GetClassName(h, cls, cls.Capacity);
                var className = cls.ToString();

                GetWindowThreadProcessId(h, out uint pid);
                bool vis = IsWindowVisible(h);
                GetWindowRect(h, out RECT r);
                long area = Math.Max(0, (long)(r.Right - r.Left) * (r.Bottom - r.Top));

                // Pula meu proprio processo pra nao virar match falso
                if (pid == launcherPid) return true;

                if (dumpAll)
                {
                    log.AppendLine($"  {(vis ? "vis" : "hid")} hwnd=0x{h.ToInt64():x} pid={pid} area={area} rect=({r.Left},{r.Top})-({r.Right},{r.Bottom}) class='{className}' title='{title}'");
                }

                // Match estrito por titulo EXATO ou classes-chave de Delphi/Elifoot
                bool titleExact = string.Equals(title, "Elifoot 98", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(title, "Editeq", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(title, "ELIFOOT", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(title, "ELIFOOT.EXE", StringComparison.OrdinalIgnoreCase);
                if (titleExact && vis && area > 0)
                {
                    if (byTitle == IntPtr.Zero)
                    {
                        log.AppendLine($"  MATCH title hwnd=0x{h.ToInt64():x} area={area} title='{title}'");
                        byTitle = h;
                    }
                    else
                    {
                        log.AppendLine($"  ALT title hwnd=0x{h.ToInt64():x} area={area} title='{title}'");
                    }
                }

                // Heuristica: janela do otvdm com maior area (mesmo sem titulo)
                if (otvdmPids.Contains(pid) && vis && area > bestArea)
                {
                    bestArea = area;
                    byArea = h;
                }

                return true;
            }, IntPtr.Zero);

            // Prefere match por titulo com area > 0; se falhar, usa a maior do otvdm.
            IntPtr chosen = byTitle != IntPtr.Zero ? byTitle : byArea;
            if (chosen != IntPtr.Zero)
                log.AppendLine($"  === chosen hwnd=0x{chosen.ToInt64():x} via {(byTitle != IntPtr.Zero ? "title" : "biggest-otvdm-area")} ===");
            return chosen;
        }

        private static void ForceWindowed(IntPtr hwnd, int width, int height, StringBuilder log, bool verbose)
        {
            // 1) Remove APENAS WS_MAXIMIZE (mantem WS_MAXIMIZEBOX pra usuario
            //    poder maximizar de novo se quiser). Ter WS_MAXIMIZEBOX removido
            //    tirava o botao de maximizar.
            var style = GetWindowLong(hwnd, GWL_STYLE);
            int newStyle = style & ~WS_MAXIMIZE;
            if (newStyle != style)
            {
                SetWindowLong(hwnd, GWL_STYLE, newStyle);
                if (verbose) log.AppendLine($"  style 0x{style:x} -> 0x{newStyle:x} (removed WS_MAXIMIZE)");
            }

            // 2) Força restore via SysCommand
            PostMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);

            // 3) SetWindowPos com posicao/tamanho desejado
            var screen = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1024, 768);
            int x = screen.X + Math.Max(0, (screen.Width - width) / 2);
            int y = screen.Y + Math.Max(0, (screen.Height - height) / 2);
            bool ok = SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            if (verbose)
            {
                GetWindowRect(hwnd, out RECT r);
                log.AppendLine($"  SetWindowPos({x},{y},{width}x{height})=>{ok}, actual rect=({r.Left},{r.Top})-({r.Right},{r.Bottom})");
            }
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
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private static readonly IntPtr HWND_TOP = new IntPtr(0);
        private const int SW_RESTORE = 9;
        private const int GWL_STYLE = -16;
        private const int WS_MAXIMIZE = 0x01000000;
        private const int WS_MAXIMIZEBOX = 0x00010000;
        private const uint WM_SYSCOMMAND = 0x0112;
        private const int SC_RESTORE = 0xF120;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

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
