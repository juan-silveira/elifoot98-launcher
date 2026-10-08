using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ElifootLauncher
{
    // Registro automatico do Elifoot 98 ("Registro para autor 2"):
    //   1. le a Senha do eli.cod (a mesma que o jogo mostra no menu Registo)
    //   2. calcula a Contra-senha como a validacao do jogo (seg12:2d88)
    //   3. grava secondCode=<contra-senha> no elif98.ini, como o jogo faz
    public static class Ativador
    {
        // 9 = "Registro para autor 2" (contra-senha comeca com 9)
        private const int TipoAutor2 = 9;

        public static string Ativar(GameLauncher launcher)
        {
            var dir = FindEliCodDir(launcher)
                ?? throw new FileNotFoundException(
                    "O jogo ainda não gerou a senha de registro.\n\n" +
                    "Abra o Elifoot uma vez, feche e tente de novo.");

            var senha = SenhaFromEliCod(File.ReadAllBytes(Path.Combine(dir, "eli.cod")));
            var contraSenha = ContraSenha(senha, TipoAutor2);

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

        // O jogo aceita a Contra-senha do tipo N quando ela e igual a senha
        // transformada N vezes por seg12:3645.
        public static string ContraSenha(string senha, int tipo)
        {
            var s = Encoding.ASCII.GetBytes(senha);
            for (int n = 0; n < tipo; n++) s = Transformar(s);
            return Encoding.ASCII.GetString(s);
        }

        // seg12:3645: escolhe a regra pelo 1o digito e depois soma 1 a ele
        private static byte[] Transformar(byte[] s)
        {
            byte c = s[0];
            byte[] r;
            switch (c - '0')
            {
                case 0: r = Ascii(Formatar(Concat(Ascii("***"), s, s))); break;
                case 1: r = (byte[])s.Clone(); break;
                case 2:
                    r = (byte[])s.Clone();
                    r[4] = r[4] < '9' ? (byte)(r[4] + 1) : (byte)'0';
                    break;
                case 3:
                case 4:
                case 5: r = Ascii(Formatar(Concat(Ascii("+++"), s, Ascii("***"), s))); break;
                default: r = Ascii(Formatar(Concat(Ascii("1213"), s, s, Ascii("XXX"), s))); break;
            }
            r[0] = (byte)(c + 1);
            return r;
        }

        private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

        private static byte[] Concat(params byte[][] parts)
        {
            var all = new List<byte>();
            foreach (var p in parts) all.AddRange(p);
            return all.ToArray();
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
