using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ElifootLauncher
{
    // Patrocinadores: PATROCIN/MARCAS.TXT (editavel a mao) vira PATROCIN/MARCAS.DAT, a
    // tabela binaria que o jogo le. Mesmo formato de tools/patrocinios/gerar_dat.py
    // (la esta a descricao dos campos); o launcher gera antes de abrir o jogo.
    public static class Patrocinadores
    {
        public const string Pasta = "PATROCIN";
        public const int Registro = 80;
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        public class Marca
        {
            public string Nome = "", Logo = "";
            public int Tier, DivisaoMaxima, ReservaPct, Modo;
            public int Verba, CotaMinima;
            public List<string> Paises = new List<string>();
        }

        public static List<Marca> Ler(string txt)
        {
            var lista = new List<Marca>();
            int n = 0;
            foreach (var bruta in File.ReadAllLines(txt, Latin1))
            {
                n++;
                var linha = bruta.Trim();
                if (linha.Length == 0 || linha.StartsWith("#")) continue;
                var c = linha.Split(';').Select(x => x.Trim()).ToList();
                while (c.Count < 9) c.Add("");
                var paises = c[7] == "" || c[7] == "*"
                    ? new List<string>()
                    : c[7].Split(',').Select(p => p.Trim().ToUpperInvariant()).Where(p => p.Length > 0).ToList();
                var modo = paises.Count == 0 ? "" : c[8].ToLowerInvariant();
                if (paises.Count > 0 && modo == "") modo = "exclusivo";
                if (modo != "" && modo != "exclusivo" && modo != "preferido" && modo != "exceto")
                    throw new FormatException($"MARCAS.TXT linha {n}: modo \"{c[8]}\" (use exclusivo, preferido ou exceto)");
                if (paises.Count > 8 || paises.Any(p => p.Length != 3))
                    throw new FormatException($"MARCAS.TXT linha {n}: até 8 países de 3 letras");
                lista.Add(new Marca
                {
                    Nome = c[0].ToUpperInvariant(), Logo = c[1].ToUpperInvariant(),
                    Tier = int.Parse(c[2]), Verba = int.Parse(c[3]), CotaMinima = int.Parse(c[4]),
                    DivisaoMaxima = int.Parse(c[5]), ReservaPct = c[6] == "" ? 0 : int.Parse(c[6]),
                    Paises = paises, Modo = modo == "exclusivo" ? 1 : modo == "preferido" ? 2 : modo == "exceto" ? 3 : 0,
                });
            }
            return lista;
        }

        public static byte[] Gerar(List<Marca> marcas)
        {
            var s = new MemoryStream();
            var w = new BinaryWriter(s);
            w.Write(Encoding.ASCII.GetBytes("PAT1"));
            w.Write((ushort)1);
            w.Write((ushort)marcas.Count);
            foreach (var m in marcas)
            {
                var r = new byte[Registro];
                Texto(r, 0, m.Nome, 24);
                Texto(r, 25, m.Logo, 12);
                r[38] = (byte)m.Tier;
                r[39] = (byte)m.DivisaoMaxima;
                BitConverter.GetBytes(m.Verba).CopyTo(r, 40);
                BitConverter.GetBytes(m.CotaMinima).CopyTo(r, 44);
                r[48] = (byte)m.ReservaPct;
                r[49] = (byte)m.Modo;
                r[50] = (byte)m.Paises.Count;
                for (int i = 0; i < m.Paises.Count; i++)
                    Latin1.GetBytes(m.Paises[i]).CopyTo(r, 51 + 3 * i);
                w.Write(r);
            }
            return s.ToArray();
        }

        // Gera o MARCAS.DAT da pasta do jogo se o MARCAS.TXT existir e for mais novo
        public static void Preparar(string gameDir)
        {
            var pasta = TeamCodec.Caminho(gameDir, Pasta);
            var txt = TeamCodec.Caminho(pasta, "MARCAS.TXT");
            if (!File.Exists(txt)) return;
            var dat = Path.Combine(pasta, "MARCAS.DAT");
            if (File.Exists(dat) && File.GetLastWriteTimeUtc(dat) >= File.GetLastWriteTimeUtc(txt)) return;
            File.WriteAllBytes(dat, Gerar(Ler(txt)));
        }

        private static void Texto(byte[] r, int o, string s, int max)
        {
            var b = Latin1.GetBytes(s);
            int n = Math.Min(b.Length, max);
            r[o] = (byte)n;
            Array.Copy(b, 0, r, o + 1, n);
        }
    }
}
