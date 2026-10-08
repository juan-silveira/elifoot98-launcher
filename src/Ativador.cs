using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ElifootLauncher
{
    // Registro automatico do Elifoot 98 ("Registro para autor 2"):
    //   1. le a Senha do eli.cod (a mesma que o jogo mostra no menu Registo)
    //   2. gera a Contra-senha com o CRACK.EXE (CrackRunner)
    //   3. grava secondCode=<contra-senha> no elif98.ini, como o jogo faz
    public static class Ativador
    {
        public static string Ativar(GameLauncher launcher)
        {
            var dir = FindEliCodDir(launcher)
                ?? throw new FileNotFoundException(
                    "O jogo ainda não gerou a senha de registro.\n\n" +
                    "Abra o Elifoot uma vez, feche e tente de novo.");

            var senha = SenhaFromEliCod(File.ReadAllBytes(Path.Combine(dir, "eli.cod")));
            var contraSenha = CrackRunner.GerarContraSenha(launcher, senha);

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

        // eli.cod: strings Pascal ([len][dados]) cifradas 2x pela rotina do jogo
        // (seg14:0734): cada byte, a partir do 1o de dados, soma o byte anterior
        // (incluindo o de tamanho). Contem 2 strings: A (derivada das datas das
        // pastas WINDOWS/SYSTEM do otvdm) e B (Random(20000) sorteado pelo jogo).
        public static string SenhaFromEliCod(byte[] cod)
        {
            var strings = new List<byte[]>();
            for (int i = 0; i < cod.Length; )
            {
                int n = cod[i];
                if (i + 1 + n > cod.Length) throw new InvalidDataException("eli.cod corrompido.");
                var rec = new byte[n + 1];
                Array.Copy(cod, i, rec, 0, n + 1);
                Undo(rec);
                Undo(rec);
                var s = new byte[n];
                Array.Copy(rec, 1, s, 0, n);
                strings.Add(s);
                i += 1 + n;
            }
            if (strings.Count < 2) throw new InvalidDataException("eli.cod em formato inesperado.");

            var ab = new byte[strings[0].Length + strings[1].Length];
            strings[0].CopyTo(ab, 0);
            strings[1].CopyTo(ab, strings[0].Length);
            return "014-" + Formatar(ab);
        }

        // Desfaz uma passada da cifra numa string Pascal (rec[0] = tamanho)
        private static void Undo(byte[] rec)
        {
            for (int k = rec.Length - 1; k >= 1; k--)
                rec[k] = (byte)(rec[k] - rec[k - 1]);
        }

        // Replica seg12:2f8b: completa com '0' ate 19, comprime ate 17 digitos
        // com (3*proximo + atual + 3) mod 10, volta o tamanho pra 19 (os 2
        // ultimos sao sobras do buffer) e poe hifens nas posicoes 4, 8, 12 e 16.
        private static string Formatar(byte[] s)
        {
            var buf = new byte[256];
            int len = Math.Min(s.Length, 255);
            Array.Copy(s, 0, buf, 1, len);
            while (len < 19) buf[1 + len++] = (byte)'0';
            while (len > 17)
            {
                for (int i = 1; i < len; i++)
                    buf[i] = (byte)((buf[i + 1] * 3 + buf[i] + 3) % 10 + '0');
                len--;
            }
            foreach (var k in new[] { 4, 8, 12, 16 }) buf[k] = (byte)'-';
            return Encoding.ASCII.GetString(buf, 1, 19);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string section, string key, string value, string fileName);
    }
}
