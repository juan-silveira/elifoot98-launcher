using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    // Lado Linux/macOS do launcher. O jogo roda pelo elifoot98.sh (Wine + otvdm),
    // que copia os arquivos do pacote (somente leitura) pra pasta gravavel —
    // ~/.local/share/elifoot98 ou ~/Library/Application Support/Elifoot98 —
    // e la que o launcher le e grava.
    public class LinuxGame
    {
        public string AppDir { get; } = AppContext.BaseDirectory;
        public string Script => Path.Combine(AppDir, "elifoot98.sh");
        public string DataDir { get; } = PastaDeDados();

        // Mesma regra do elifoot98.sh
        private static string PastaDeDados()
        {
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

        // Copia/atualiza os arquivos do jogo na pasta gravavel (sem abrir o Wine)
        public void Preparar()
        {
            using var p = StartScript("preparar", null) ?? throw new IOException("Não consegui executar o elifoot98.sh.");
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new IOException("Não consegui preparar os arquivos do jogo em " + DataDir);
        }

        public void LaunchElifoot(LauncherConfig cfg) => StartScript("jogo", cfg);
        public void LaunchEditor(LauncherConfig cfg) => StartScript("editor", cfg);

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
                var (w, h) = cfg.ResolutionWidth < 800 ? (800, 600) : (cfg.ResolutionWidth, cfg.ResolutionHeight);
                psi.Environment["ELIFOOT_RES"] = $"{w}x{h}";
                // Sem tela cheia vale o padrao do script (no macOS o jogo abre maximizado)
                if (cfg.Fullscreen) psi.Environment["ELIFOOT_FULLSCREEN"] = "1";
            }
            return Process.Start(psi);
        }

        // "Ativar todos os recursos": Registro para autor 2, como no Windows
        public string Ativar()
        {
            Preparar();
            var senha = Registro.SenhaFromEliCod(File.ReadAllBytes(Path.Combine(OtvdmWindowsDir, "eli.cod")));
            var contraSenha = Registro.ContraSenha(senha, Registro.TipoAutor2);
            GravarIni(Path.Combine(OtvdmWindowsDir, "elif98.ini"), "System", "secondCode", contraSenha);
            return contraSenha;
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
