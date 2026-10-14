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

        // Tamanho do jogo (Linux/macOS): escala em % (100 = normal; 125 a 200 = o jogo
        // desenha as telas maiores, com DPI alta no Wine e o texto nitido; 0 = a maior
        // que cabe no monitor)
        public static readonly int[] OpcoesEscala = { 100, 125, 150, 175, 200, 0 };
        public int Escala { get; set; } = 100;

        // Janela redimensionavel (so Linux): o gamescope amplia ou reduz a imagem do
        // jogo ao puxar a borda da janela
        public bool JanelaLivre { get; set; } = false;

        // Equipes por divisao: o pacote traz um EXE para cada opcao (ELIF08.EXE a
        // ELIF20.EXE, gerados por tools/divisoes/patch_divisoes.py). Um jogo gravado
        // so continua com o mesmo numero de equipes.
        public static readonly int[] OpcoesEquipes = { 8, 10, 12, 14, 16, 18, 20 };
        public int EquipesPorDivisao { get; set; } = 8;
        public string ExeDoJogo => $"ELIF{EquipesPorDivisao:00}{Idioma}.EXE";
        public string ExeOriginal => $"ELIF{EquipesPorDivisao:00}.EXE";

        // Idioma do jogo: "" = o original (portugues de Portugal); "BR" = portugues do
        // Brasil (ELIFnnBR.EXE, traduzidos por tools/idiomas a partir de game/IDIOMAS/pt-BR.txt)
        public static readonly string[] OpcoesIdioma = { "", "BR" };
        public static readonly string[] NomesIdioma = { "Original (Portugal)", "Português do Brasil" };
        public string Idioma { get; set; } = "";

        // Versao do jogo: a modificada (padrao: cartoes amarelos, Nota e Risco de lesao,
        // equipes por divisao) ou a original (ELIFOOT.EXE, o 98.003; ELIF98BR.EXE traduzido),
        // que so tem 8 equipes por divisao
        public bool VersaoOriginal { get; set; } = false;
        public int EquipesDoJogo => VersaoOriginal ? 8 : EquipesPorDivisao;

        // Registro: o launcher grava a contra-senha (Registro para autor) no elif98.ini antes
        // de abrir o jogo; nao registrado, tira (copia de avaliacao)
        public bool Registrado { get; set; }

        // Jogadores estrangeiros: true = Liberado (todos os paises no BOSMAN.TXE), false =
        // Original, null = ainda nao escolhido (vale o arquivo). Guardado aqui porque uma
        // atualizacao copia os arquivos do pacote por cima (Patches.Manter refaz a escolha)
        public bool? Estrangeiros { get; set; }

        // Adicoes do jogo (OpcoesDoJogo acerta o EXE antes de abrir). Por padrao desligadas:
        // o jogo comeca como o original e o usuario liga nas Configuracoes
        public bool Cartoes { get; set; }
        public bool Patrocinios { get; set; }

        // O EXE que existe na pasta do jogo: o da versao e do idioma, senao o sem traducao,
        // senao o ELIFOOT.EXE
        public string ExeExistente(string pastaDoJogo)
        {
            var nomes = VersaoOriginal
                ? new[] { Idioma.Length > 0 ? $"ELIF98{Idioma}.EXE" : "ELIFOOT.EXE", "ELIFOOT.EXE" }
                : new[] { ExeDoJogo, ExeOriginal };
            foreach (var nome in nomes)
                if (File.Exists(Path.Combine(pastaDoJogo, nome))) return Path.Combine(pastaDoJogo, nome);
            return Path.Combine(pastaDoJogo, "ELIFOOT.EXE");
        }
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
                        case "janela":
                            cfg.JanelaLivre = val == "1";
                            break;
                        case "versao":
                            cfg.VersaoOriginal = val == "original";
                            break;
                        case "registrado":
                            cfg.Registrado = val == "1";
                            break;
                        case "cartoes":
                            cfg.Cartoes = val == "1";
                            break;
                        case "patrocinios":
                            cfg.Patrocinios = val == "1";
                            break;
                        case "estrangeiros":
                            if (val == "liberado") cfg.Estrangeiros = true;
                            else if (val == "original") cfg.Estrangeiros = false;
                            break;
                        case "idioma":
                            if (Array.IndexOf(OpcoesIdioma, val) >= 0) cfg.Idioma = val;
                            break;
                        case "escala":
                            if (int.TryParse(val, out var e) && Array.IndexOf(OpcoesEscala, e) >= 0) cfg.Escala = e;
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
                "escala=" + Escala,
                "janela=" + (JanelaLivre ? "1" : "0"),
                "equipes=" + EquipesPorDivisao,
                "idioma=" + Idioma,
                "versao=" + (VersaoOriginal ? "original" : "modificada"),
                "registrado=" + (Registrado ? "1" : "0"),
                "cartoes=" + (Cartoes ? "1" : "0"),
                "patrocinios=" + (Patrocinios ? "1" : "0"),
            };
            if (Estrangeiros.HasValue) lines.Add("estrangeiros=" + (Estrangeiros.Value ? "liberado" : "original"));
            File.WriteAllLines(ConfigPath, lines);
        }
    }
}
