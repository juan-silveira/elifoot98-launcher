using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace ElifootLauncher
{
    // Lado Windows do launcher multiplataforma (versao portatil, com o .NET dentro do
    // .exe: nao depende do .NET Framework 4.8 do launcher classico). O .exe unico
    // leva o pacote (jogo, otvdm, fontes, ScreenHook) embutido; no zip, o pacote fica
    // ao lado do .exe. O jogo e copiado para %LOCALAPPDATA%\Elifoot98 e roda de la,
    // pelo otvdm (a build 2703, a mesma do Linux e do macOS), com o ScreenHook.dll
    // injetado e o Tamanho do jogo do Linux.
    [SupportedOSPlatform("windows")]
    public static class WindowsJogo
    {
        // Datas fixas da pasta WINDOWS do otvdm: o jogo deriva o eli.cod delas
        // (docs/elifoot98-interno.md). O jogo cria o eli.cod na 1a abertura; com as
        // datas fixas ele continua valendo quando o jogo cria arquivos na pasta.
        private static readonly DateTime DataFixa = new DateTime(2000, 1, 1, 14, 0, 0, DateTimeKind.Utc);

        public static string PastaDeDados() =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Elifoot98");

        // Versao do launcher e o tamanho do pacote embutido: um .exe novo (mesmo de
        // teste, com a mesma versao) sempre atualiza o jogo
        private static string Versao()
        {
            var a = Assembly.GetExecutingAssembly();
            var v = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? a.GetName().Version?.ToString() ?? "0";
            using var pacote = a.GetManifestResourceStream("pacote.zip");
            return pacote == null ? v : $"{v} {pacote.Length}";
        }

        // Pasta com game, vendor\otvdm, fontes-hd e ScreenHook.dll: a extracao do
        // pacote embutido no .exe ou, no zip, a pasta do .exe
        private static string? origem;
        private static string Origem(LinuxGame g)
        {
            if (origem != null) return origem;
            using var pacote = Assembly.GetExecutingAssembly().GetManifestResourceStream("pacote.zip");
            if (pacote == null) return origem = g.AppDir;
            var dir = Path.Combine(PastaDeDados(), "pacote");
            var marca = Path.Combine(dir, ".versao");
            var versao = Versao();
            if (!File.Exists(marca) || File.ReadAllText(marca).Trim() != versao)
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
                System.IO.Compression.ZipFile.ExtractToDirectory(pacote, dir);
                File.WriteAllText(marca, versao);
            }
            return origem = dir;
        }

        // Na 1a vez e quando o launcher muda de versao: copia o jogo e o otvdm do
        // pacote. Saves (game\JOGOS) e o Windows do jogo (elif98.ini, eli.cod) ficam.
        public static void Sincronizar(LinuxGame g)
        {
            var marca = Path.Combine(g.DataDir, ".versao");
            var versao = Versao();
            bool primeira = !File.Exists(marca);
            if (primeira || File.ReadAllText(marca).Trim() != versao)
            {
                var origemJogo = Path.Combine(Origem(g), "game");
                var origemOtvdm = Path.Combine(Origem(g), "vendor", "otvdm");
                if (!Directory.Exists(origemJogo) || !Directory.Exists(origemOtvdm))
                    throw new DirectoryNotFoundException(
                        "Faltam as pastas \"game\" e \"vendor\" ao lado do ElifootLauncher.exe. Extraia o zip inteiro e abra o launcher de dentro da pasta extraída.");
                Copiar(origemJogo, g.GameDir, rel => !rel.StartsWith("JOGOS", StringComparison.OrdinalIgnoreCase));
                Copiar(origemOtvdm, Path.Combine(g.DataDir, "vendor", "otvdm"),
                    rel => !rel.StartsWith("WINDOWS", StringComparison.OrdinalIgnoreCase));
                if (primeira) ImportarSaves(g);
                Directory.CreateDirectory(g.DataDir);
                File.WriteAllText(marca, versao);
            }
            Directory.CreateDirectory(Path.Combine(g.OtvdmWindowsDir, "SYSTEM"));
            Directory.CreateDirectory(g.JogosDir);
        }

        // sobrescreve(caminho relativo) = false: so copia se ainda nao existir
        private static void Copiar(string origem, string destino, Func<string, bool> sobrescreve)
        {
            foreach (var arq in Directory.EnumerateFiles(origem, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(origem, arq);
                var alvo = Path.Combine(destino, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(alvo)!);
                if (File.Exists(alvo) && !sobrescreve(rel)) continue;
                File.Copy(arq, alvo, overwrite: true);
            }
        }

        // Saves do launcher classico (C:\Elifoot98Launcher, e a VirtualStore de quem
        // gravou ali sem permissao), sem sobrescrever nada
        private static void ImportarSaves(LinuxGame g)
        {
            var locais = new[]
            {
                @"C:\Elifoot98Launcher\game\JOGOS",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VirtualStore", "Elifoot98Launcher", "game", "JOGOS"),
            };
            foreach (var pasta in locais)
            {
                try
                {
                    if (Directory.Exists(pasta)) Copiar(pasta, g.JogosDir, _ => false);
                }
                catch { /* sem permissao: segue sem esses saves */ }
            }
        }

        private static void FixarDatas(LinuxGame g)
        {
            foreach (var pasta in new[] { Path.Combine(g.OtvdmWindowsDir, "SYSTEM"), g.OtvdmWindowsDir })
            {
                Directory.SetLastWriteTimeUtc(pasta, DataFixa);
                Directory.SetCreationTimeUtc(pasta, DataFixa);
            }
        }

        public static void Abrir(LinuxGame g, string exe, LauncherConfig cfg)
        {
            Sincronizar(g);
            if (!File.Exists(exe))
                throw new FileNotFoundException($"Executável não encontrado: {exe}");
            FixarDatas(g);
            var otvdmDir = Path.Combine(g.DataDir, "vendor", "otvdm");
            var otvdmExe = Path.Combine(otvdmDir, "otvdmw.exe");

            // Tamanho do jogo, como no Linux: o jogo enxerga uma tela de 800x600 x
            // escala e desenha as telas maiores (AdjustDPI do otvdm = DPI do Wine no
            // Linux), com o texto na MS Sans Serif TrueType. Em tela cheia, a tela toda.
            var (telaL, telaA) = AreaDeTrabalho();
            int escala = EscolherEscala(cfg, telaA);
            int largura = cfg.Fullscreen ? telaL : 800 * escala / 100;
            int altura = cfg.Fullscreen ? telaA : 600 * escala / 100;
            EscreverOtvdmIni(otvdmDir, cfg, 96 * escala / 100);

            // Sem a camada 640X480 do launcher classico: o jogo ve a tela do ScreenHook
            // (800x600 x escala), como o desktop do Wine no Linux. HIGHDPIAWARE no otvdm:
            // com a escala do Windows acima de 100% ele nao amplia a imagem (a escala e a
            // do Tamanho do jogo)
            RemoverCamada(exe);
            GravarCamada(otvdmExe, "HIGHDPIAWARE");

            var env = new Dictionary<string, string>
            {
                ["ELIFOOT_FAKE_WIDTH"] = largura.ToString(),
                ["ELIFOOT_FAKE_HEIGHT"] = altura.ToString(),
            };
            if (escala > 100)
            {
                var pastaFontes = Path.Combine(Origem(g), "fontes-hd");
                var fontes = Directory.Exists(pastaFontes)
                    ? Directory.GetFiles(pastaFontes, "*.ttf") : Array.Empty<string>();
                if (fontes.Length > 0) env["ELIFOOT_FONTES"] = string.Join(";", fontes);
            }

            // O ScreenHook.dll vai dentro do otvdmw.exe (32 bits): o launcher e win-x86
            var hook = Path.Combine(Origem(g), "ScreenHook.dll");
            if (File.Exists(hook) && RuntimeInformation.ProcessArchitecture == Architecture.X86)
            {
                try
                {
                    Acordar(DllInjector.LaunchWithInjectedDll(otvdmExe, $"\"{exe}\"", g.GameDir, hook, env));
                    return;
                }
                catch (Exception ex)
                {
                    Log("injecao falhou, abrindo sem o ScreenHook: " + ex.Message);
                }
            }
            var psi = new ProcessStartInfo
            {
                FileName = otvdmExe,
                Arguments = $"\"{exe}\"",
                WorkingDirectory = g.GameDir,
                UseShellExecute = false,
            };
            foreach (var kv in env) psi.Environment[kv.Key] = kv.Value;
            Acordar(Process.Start(psi));
        }

        // O jogo so mostra a 1a janela (Acerca) depois de algum evento de entrada:
        // sem o mouse por cima, fica a tela verde. Como o iniciar.exe do Linux, manda
        // um WM_MOUSEMOVE as janelas do jogo assim que aparecem
        private static void Acordar(Process? p)
        {
            if (p == null) return;
            int pid = p.Id;
            System.Threading.Tasks.Task.Run(async () =>
            {
                for (int i = 0; i < 60; i++)
                {
                    await System.Threading.Tasks.Task.Delay(500);
                    bool achou = false;
                    EnumWindows((h, _) =>
                    {
                        GetWindowThreadProcessId(h, out var dono);
                        if (dono == pid && IsWindowVisible(h) && GetWindowRect(h, out var r) && r.Dir > r.Esq)
                        {
                            PostMessageW(h, 0x0200, IntPtr.Zero, (IntPtr)(10 | (10 << 16)));
                            achou = true;
                        }
                        return true;
                    }, IntPtr.Zero);
                    if (achou) return;
                }
            });
        }

        private delegate bool EnumWindowsProc(IntPtr janela, IntPtr param);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr param);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr janela, out uint pid);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr janela);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr janela, out Retangulo r);
        [DllImport("user32.dll")]
        private static extern bool PostMessageW(IntPtr janela, uint msg, IntPtr w, IntPtr l);

        // Escala em %: a das Configuracoes ou, no Automatico (0), a maior que cabe
        // (como o elifoot98.sh): 200, 175, 150, 125 ou 100
        private static int EscolherEscala(LauncherConfig cfg, int alturaTela)
        {
            if (cfg.Escala >= 100) return cfg.Escala;
            int margem = cfg.Fullscreen ? 0 : 80;   // barra de titulo da janela
            foreach (var e in new[] { 200, 175, 150, 125 })
                if (600 * e / 100 + margem <= alturaTela) return e;
            return 100;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Retangulo { public int Esq, Topo, Dir, Base; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfoW(uint acao, uint param, ref Retangulo r, uint ini);

        // Area de trabalho do monitor principal, em pixels de verdade (o launcher e
        // ciente da DPI)
        private static (int L, int A) AreaDeTrabalho()
        {
            var r = new Retangulo();
            if (SystemParametersInfoW(0x0030 /* SPI_GETWORKAREA */, 0, ref r, 0))
                return (r.Dir - r.Esq, r.Base - r.Topo);
            return (1024, 768);
        }

        // Opcoes do otvdm (as do launcher classico) e a DPI do Tamanho do jogo
        private static void EscreverOtvdmIni(string otvdmDir, LauncherConfig cfg, int dpi)
        {
            var linhas = new List<string>
            {
                "[otvdm]",
                "EnableVisualStyle=0",
                "DisableAero=1",
                cfg.Fullscreen ? "FixScreenSize=0" : "FixScreenSize=1",
            };
            if (dpi > 96)
            {
                linhas.Add("AdjustDPI=" + dpi);
                // letras suavizadas (tons de cinza; a ClearType deixa franjas coloridas no verde)
                linhas.Add("ForceFontQuality=4");
            }
            File.WriteAllText(Path.Combine(otvdmDir, "otvdm.ini"), string.Join("\r\n", linhas) + "\r\n");
        }

        private const string Camadas = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers";

        // Varias formas do caminho: o otvdm as vezes procura pelo caminho curto
        private static IEnumerable<string> Variantes(string exe)
        {
            var v = new HashSet<string> { exe, exe.ToUpperInvariant(), exe.ToLowerInvariant() };
            var curto = CaminhoCurto(exe);
            if (!string.IsNullOrEmpty(curto)) v.Add(curto);
            return v;
        }

        private static void GravarCamada(string exe, string camada)
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(Camadas);
                foreach (var v in Variantes(exe)) k.SetValue(v, camada, Microsoft.Win32.RegistryValueKind.String);
            }
            catch (Exception ex) { Log("camada de compatibilidade: " + ex.Message); }
        }

        private static void RemoverCamada(string exe)
        {
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(Camadas, writable: true);
                if (k == null) return;
                foreach (var v in Variantes(exe)) k.DeleteValue(v, throwOnMissingValue: false);
            }
            catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, int tamanho);

        private static string CaminhoCurto(string caminho)
        {
            var sb = new StringBuilder(260);
            return GetShortPathName(caminho, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }

        public static void Log(string texto)
        {
            try
            {
                Directory.CreateDirectory(PastaDeDados());
                File.AppendAllText(Path.Combine(PastaDeDados(), "launcher.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {texto}\r\n");
            }
            catch { }
        }
    }
}
