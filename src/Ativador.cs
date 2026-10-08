using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ElifootLauncher
{
    // Registro automatico do Elifoot 98 ("Registro para autor 2"):
    //   1. le a Senha do eli.cod (a mesma que o jogo mostra no menu Registo)
    //   2. calcula a Contra-senha como a validacao do jogo (seg12:2d88)
    //   3. grava secondCode=<contra-senha> no elif98.ini, como o jogo faz
    public static class Ativador
    {
        public static string Ativar(GameLauncher launcher)
        {
            var dir = FindEliCodDir(launcher)
                ?? throw new FileNotFoundException(
                    "O jogo ainda não gerou a senha de registro.\n\n" +
                    "Abra o Elifoot uma vez, feche e tente de novo.");

            var senha = Registro.SenhaFromEliCod(File.ReadAllBytes(Path.Combine(dir, "eli.cod")));
            var contraSenha = Registro.ContraSenha(senha, Registro.TipoAutor2);

            var ini = Path.Combine(dir, "elif98.ini");
            if (!WritePrivateProfileString("System", "secondCode", contraSenha, ini))
                throw new IOException($"Não consegui gravar o registro em {ini}.");
            return contraSenha;
        }

        // O otvdm (32-bit sem manifest) tem gravacao redirecionada pra VirtualStore
        // quando o usuario nao pode escrever na pasta de instalacao. Se houver copia
        // la, e ela que o jogo usa.
        private static string? FindEliCodDir(GameLauncher launcher)
        {
            var real = launcher.OtvdmWindowsDir;
            var virtualStore = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VirtualStore",
                real.Substring(Path.GetPathRoot(real)!.Length));
            foreach (var dir in new[] { virtualStore, real })
                if (File.Exists(Path.Combine(dir, "eli.cod"))) return dir;
            return null;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string fileName);
    }
}
