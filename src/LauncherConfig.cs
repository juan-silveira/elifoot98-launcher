using System;
using System.Collections.Generic;
using System.IO;

namespace ElifootLauncher
{
    // Formato: INI simples key=value. Zero dependencia externa (evita
    // problema do System.Text.Json em Framework Dependent netfx).
    public class LauncherConfig
    {
        // Compat layer '640X480' faz o Elifoot desenhar tudo em 640x480.
        // Combina com janela 640x480 = zero corte, jogo cabe perfeito.
        public int ResolutionWidth { get; set; } = 640;
        public int ResolutionHeight { get; set; } = 480;
        public bool Fullscreen { get; set; } = false;

        // Equipes por divisao: o pacote traz um EXE para cada opcao (ELIF08.EXE a
        // ELIF20.EXE, gerados por tools/divisoes/patch_divisoes.py). Um jogo gravado
        // so continua com o mesmo numero de equipes.
        public static readonly int[] OpcoesEquipes = { 8, 10, 12, 14, 16, 18, 20 };
        public int EquipesPorDivisao { get; set; } = 8;
        public string ExeDoJogo => $"ELIF{EquipesPorDivisao:00}.EXE";
        // Minimo de equipes no jogo novo (4N + as que sobem do Distrital)
        public static int MinimoDeEquipes(int n) => 4 * n + (n >= 16 ? 4 : 2);

        private static string ConfigPath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ElifootLauncher");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "config.ini");
            }
        }

        public static LauncherConfig Load()
        {
            var cfg = new LauncherConfig();
            try
            {
                if (!File.Exists(ConfigPath)) return cfg;
                foreach (var raw in File.ReadAllLines(ConfigPath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("[")) continue;
                    var eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    var key = line.Substring(0, eq).Trim();
                    var val = line.Substring(eq + 1).Trim();
                    switch (key.ToLowerInvariant())
                    {
                        case "width":
                            if (int.TryParse(val, out var w)) cfg.ResolutionWidth = w;
                            break;
                        case "height":
                            if (int.TryParse(val, out var h)) cfg.ResolutionHeight = h;
                            break;
                        case "equipes":
                            if (int.TryParse(val, out var n) && Array.IndexOf(OpcoesEquipes, n) >= 0) cfg.EquipesPorDivisao = n;
                            break;
                        case "fullscreen":
                            cfg.Fullscreen = val == "1" || string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                }
            }
            catch { }
            return cfg;
        }

        public void Save()
        {
            var lines = new List<string>
            {
                "; Elifoot 98 Launcher config",
                "width=" + ResolutionWidth,
                "height=" + ResolutionHeight,
                "fullscreen=" + (Fullscreen ? "1" : "0"),
                "equipes=" + EquipesPorDivisao,
            };
            File.WriteAllLines(ConfigPath, lines);
        }
    }
}
