using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ElifootLauncher
{
    // O jogo de cada sistema. No Linux e no macOS roda pelo elifoot98.sh (Wine +
    // otvdm), que copia os arquivos do pacote (somente leitura) pra pasta gravavel —
    // ~/.local/share/elifoot98 ou ~/Library/Application Support/Elifoot98 — e la que
    // o launcher le e grava. No Windows (versao portatil) quem faz isso e o WindowsJogo,
    // em %LOCALAPPDATA%\Elifoot98.
    public class LinuxGame
    {
        public string AppDir { get; } = PastaDoPacote();
        // No macOS os arquivos do pacote (elifoot98.sh, game, vendor, linux) ficam em
        // Contents/Resources, como a assinatura de codigo exige; o executavel fica em
        // Contents/MacOS. Pacotes antigos tinham tudo em Contents/MacOS.
        private static string PastaDoPacote()
        {
            var b = AppContext.BaseDirectory;
            if (OperatingSystem.IsMacOS())
            {
                var r = Path.GetFullPath(Path.Combine(b, "..", "Resources"));
                if (File.Exists(Path.Combine(r, "elifoot98.sh"))) return r;
            }
            return b;
        }
        public string Script => Path.Combine(AppDir, "elifoot98.sh");
        public string DataDir { get; } = PastaDeDados();
        public static string PastaDeDadosPublica() => PastaDeDados();

        // Mesma regra do elifoot98.sh
        private static string PastaDeDados()
        {
            if (OperatingSystem.IsWindows())
                return WindowsJogo.PastaDeDados();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (OperatingSystem.IsMacOS())
                return Path.Combine(home, "Library", "Application Support", "Elifoot98");
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".local", "share") : xdg, "elifoot98");
        }
        public string GameDir => Path.Combine(DataDir, "game");
        public string JogosDir => Path.Combine(GameDir, "JOGOS");
        public string RefereeTxePath => Path.Combine(GameDir, "REFEREE.TXE");
        public string OtvdmWindowsDir => Path.Combine(DataDir, "vendor", "otvdm", "WINDOWS");
        // Arquivos originais do jogo (a pasta game do pacote, somente leitura; no Windows,
        // a do pacote extraido do .exe) e a biblioteca de patches (Patches.cs)
        public string OriginaisDir => OperatingSystem.IsWindows()
            ? Path.Combine(WindowsJogo.OrigemDoPacote(this), "game")
            : Path.Combine(AppDir, "game");
        public string BibliotecaDir => Path.Combine(DataDir, "patches");

        // Copia/atualiza os arquivos do jogo na pasta gravavel (sem abrir o Wine). Se a versao
        // mudou (os arquivos do pacote foram copiados por cima), refaz o patch em uso; e deixa
        // a regra de estrangeiros como a escolhida nas Configuracoes
        public void Preparar()
        {
            var marca = Path.Combine(DataDir, ".versao");
            string? antes = File.Exists(marca) ? File.ReadAllText(marca).Trim() : null;
            if (OperatingSystem.IsWindows())
                WindowsJogo.Sincronizar(this);
            else
            {
                using var p = StartScript("preparar", null) ?? throw new IOException("Não consegui executar o elifoot98.sh.");
                p.WaitForExit();
                if (p.ExitCode != 0)
                    throw new IOException("Não consegui preparar os arquivos do jogo em " + DataDir);
            }
            string? depois = File.Exists(marca) ? File.ReadAllText(marca).Trim() : null;
            Patches.Manter(GameDir, OriginaisDir, BibliotecaDir, antes != null && antes != depois,
                LauncherConfig.Load().Estrangeiros);
        }

        public void LaunchElifoot(LauncherConfig cfg)
        {
            if (OperatingSystem.IsWindows())
            {
                Preparar();
                SavesPorEquipes.Separar(JogosDir, cfg.EquipesDoJogo);
                DefinirRegistro(cfg.Registrado);
                AplicarOpcoes(cfg);
                WindowsJogo.Abrir(this, cfg.ExeExistente(GameDir), cfg);
                return;
            }
            Preparar();
            SavesPorEquipes.Separar(JogosDir, cfg.EquipesDoJogo);
            DefinirRegistro(cfg.Registrado);
            AplicarOpcoes(cfg);
            StartScript("jogo", cfg);
        }
        // Cartoes amarelos, patrocinadores...: os bytes do EXE conforme as Configuracoes
        private void AplicarOpcoes(LauncherConfig cfg)
        {
            try { OpcoesDoJogo.Aplicar(cfg.ExeExistente(GameDir), OpcoesDoJogo.DaConfig(cfg)); }
            catch { /* abre como esta */ }
        }

        public void LaunchEditor(LauncherConfig cfg)
        {
            Preparar();
            if (OperatingSystem.IsWindows())
                WindowsJogo.Abrir(this, Path.Combine(GameDir, "EDITEQ.EXE"), cfg);
            else
                StartScript("editor", cfg);
        }

        private Process? StartScript(string modo, LauncherConfig? cfg)
        {
            if (!File.Exists(Script))
                throw new FileNotFoundException($"elifoot98.sh não encontrado: {Script}");
            var psi = new ProcessStartInfo("/bin/bash") { UseShellExecute = false };
            psi.ArgumentList.Add(Script);
            psi.ArgumentList.Add(modo);
            if (cfg != null)
            {
                // Config padrao (compartilhada com o Windows) e 640x480; no
                // desktop virtual do Wine as telas do jogo precisam de 800x600
                // A janela do Wine tem o tamanho das telas do jogo (800x600, ou mais com
                // a escala); antes uma janela maior so punha fundo verde em volta
                psi.Environment["ELIFOOT_RES"] = "800x600";
                // Sem tela cheia vale o padrao do script (no macOS o jogo abre maximizado)
                if (cfg.Fullscreen) psi.Environment["ELIFOOT_FULLSCREEN"] = "1";
                // Tamanho do jogo: escala em % (0 = o script escolhe a maior que cabe)
                psi.Environment["ELIFOOT_ESCALA"] = cfg.Escala == 0 ? "auto" : cfg.Escala.ToString();
                if (cfg.JanelaLivre) psi.Environment["ELIFOOT_JANELA_LIVRE"] = "1";
                // EXE das equipes por divisao e do idioma escolhidos nas Configuracoes
                if (modo == "jogo") psi.Environment["ELIFOOT_EXE"] = Path.GetFileName(cfg.ExeExistente(GameDir));
            }
            return Process.Start(psi);
        }

        // "Ativar todos os recursos": Registro para autor 2, como no Windows
        public string Ativar()
        {
            Preparar();
            var eliCod = Path.Combine(OtvdmWindowsDir, "eli.cod");
            // No Windows o proprio jogo cria o eli.cod na 1a vez que abre (no Linux e no
            // macOS ele vem no pacote, feito para as datas fixas)
            if (!File.Exists(eliCod))
                throw new FileNotFoundException("Abra o jogo uma vez (até a tela inicial) e depois ative os recursos.");
            var senha = Registro.SenhaFromEliCod(File.ReadAllBytes(eliCod));
            var contraSenha = Registro.ContraSenha(senha, Registro.TipoAutor2);
            GravarIni(Path.Combine(OtvdmWindowsDir, "elif98.ini"), "System", "secondCode", contraSenha);
            return contraSenha;
        }

        // Registro escolhido nas Configuracoes, antes de abrir o jogo: registrado grava a
        // contra-senha; nao registrado tira (copia de avaliacao). Sem o eli.cod (no Windows
        // o jogo o cria na 1a vez que abre), fica como esta.
        private void DefinirRegistro(bool registrado)
        {
            try
            {
                var ini = Path.Combine(OtvdmWindowsDir, "elif98.ini");
                var eliCod = Path.Combine(OtvdmWindowsDir, "eli.cod");
                if (registrado && File.Exists(eliCod))
                    GravarIni(ini, "System", "secondCode",
                        Registro.ContraSenha(Registro.SenhaFromEliCod(File.ReadAllBytes(eliCod)), Registro.TipoAutor2));
                else if (!registrado && File.Exists(ini))
                {
                    var linhas = File.ReadAllText(ini, Encoding.Latin1).Replace("\r\n", "\n").TrimEnd('\n').Split('\n')
                        .Where(l => !l.TrimStart().StartsWith("secondCode=", StringComparison.OrdinalIgnoreCase));
                    File.WriteAllText(ini, string.Join("\r\n", linhas) + "\r\n", Encoding.Latin1);
                }
            }
            catch { /* sem permissao: abre como esta */ }
        }

        // Equivalente ao WritePrivateProfileString (o jogo usa CRLF)
        private static void GravarIni(string path, string secao, string chave, string valor)
        {
            var linhas = File.Exists(path)
                ? new System.Collections.Generic.List<string>(File.ReadAllText(path, Encoding.Latin1)
                    .Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
                : new System.Collections.Generic.List<string>();
            int inicio = linhas.FindIndex(l => l.Trim().Equals($"[{secao}]", StringComparison.OrdinalIgnoreCase));
            if (inicio < 0)
            {
                linhas.Insert(0, $"[{secao}]");
                inicio = 0;
            }
            int fim = linhas.FindIndex(inicio + 1, l => l.TrimStart().StartsWith("["));
            if (fim < 0) fim = linhas.Count;
            int existente = linhas.FindIndex(inicio + 1, fim - inicio - 1,
                l => l.StartsWith(chave + "=", StringComparison.OrdinalIgnoreCase));
            if (existente >= 0) linhas[existente] = $"{chave}={valor}";
            else linhas.Insert(inicio + 1, $"{chave}={valor}");
            File.WriteAllText(path, string.Join("\r\n", linhas) + "\r\n", Encoding.Latin1);
        }
    }
}
