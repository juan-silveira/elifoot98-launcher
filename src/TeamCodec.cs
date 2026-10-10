using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ElifootLauncher
{
    // Arquivos de equipe (.EFT), listas .TXE (paises, Lei Bosman, PLOP) e as
    // regras do jogo para uma equipe. Tudo em docs/elifoot98-interno.md, secao 7.
    public class EftPlayer
    {
        public string Pais { get; set; } = "";
        public string Nome { get; set; } = "";
        public int Posicao { get; set; }          // 0 G, 1 D, 2 M, 3 A

        // Atributos que o jogo tira do nome (seg03:7516, secao 2 do documento)
        public int Soma => TeamCodec.SomaNome(Nome);
        public int Nota => Soma % 10 + 1;
        public int Lesao => Soma % 11;
        public int Comportamento => TeamCodec.Comportamento(Soma, Posicao);
        public bool Estrela => Nota >= 8 && (Posicao == 2 || Posicao == 3);
    }

    public class EftTeam
    {
        public string Arquivo { get; set; } = "";
        public string NomeCompleto { get; set; } = "";
        public string NomeAbreviado { get; set; } = "";
        public int CorLetra { get; set; }          // 0xRRGGBB
        public int CorFundo { get; set; } = 0xFFFFFF;
        public string Pais { get; set; } = "";
        public int Nivel { get; set; } = 10;       // 1 a 20
        public string Treinador { get; set; } = "";
        public List<EftPlayer> Jogadores { get; } = new List<EftPlayer>();
    }

    // Pais do COUNTRY.TXE: codigo de 3 letras e nome ("BRA", "Brasil")
    public class Pais
    {
        public string Codigo { get; set; } = "";
        public string Nome { get; set; } = "";
        public override string ToString() => Nome;
    }

    public static class TeamCodec
    {
        private static readonly byte[] Marca = { (byte)'E', (byte)'F', (byte)'a', 0 };
        private static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");

        // Regras de seg12:275e (mensagens iguais as do Editor de Equipas)
        public const int MIN_JOGADORES = 14, MAX_JOGADORES = 20, MIN_CAMPO = 10, MAX_ESTRANGEIROS = 5;

        // No arquivo os jogadores vem agrupados G, D, M, A: o novo entra no fim do grupo
        // da posicao dele (nao no fim da lista). Devolve o indice.
        public static int InserirNaPosicao(List<EftPlayer> lista, EftPlayer j)
        {
            int i = 0;
            for (int k = 0; k < lista.Count; k++) if (lista[k].Posicao <= j.Posicao) i = k + 1;
            lista.Insert(i, j);
            return i;
        }
        public const int NIVEL_MIN = 1, NIVEL_MAX = 20;
        // Maiores tamanhos das 282 equipes originais
        public const int MAX_NOME_COMPLETO = 40, MAX_NOME = 20;

        public static readonly string[] Posicoes = { "Guarda-redes", "Defesa", "Médio", "Avançado" };
        public static readonly string[] PosicoesCurtas = { "G", "D", "M", "A" };

        // ---- .EFT ----

        public static EftTeam Read(string path)
        {
            var b = File.ReadAllBytes(path);
            if (b.Length < 0x32 || !b.Take(4).SequenceEqual(Marca))
                throw new InvalidDataException("Não é um arquivo de equipe do Elifoot 98.");
            var t = new EftTeam { Arquivo = path };
            int o = 0x32;
            t.NomeCompleto = Texto(b, ref o);
            t.NomeAbreviado = Texto(b, ref o);
            t.CorLetra = Rgb(b, o);
            t.CorFundo = Rgb(b, o + 4);
            o += 8;
            t.Pais = Texto(b, ref o);
            t.Nivel = b[o];
            int n = BitConverter.ToUInt16(b, o + 1);
            o += 3;
            for (int k = 0; k < n; k++)
            {
                var j = new EftPlayer { Pais = Texto(b, ref o), Nome = Texto(b, ref o) };
                j.Posicao = BitConverter.ToUInt16(b, o);
                o += 2;
                t.Jogadores.Add(j);
            }
            t.Treinador = Texto(b, ref o);
            return t;
        }

        public static void Write(EftTeam t, string path)
        {
            var s = new MemoryStream();
            s.Write(Marca, 0, 4);
            s.Write(new byte[0x32 - 4], 0, 0x32 - 4);
            PorTexto(s, t.NomeCompleto);
            PorTexto(s, t.NomeAbreviado);
            PorRgb(s, t.CorLetra);
            PorRgb(s, t.CorFundo);
            PorTexto(s, t.Pais);
            s.WriteByte((byte)t.Nivel);
            s.Write(BitConverter.GetBytes((ushort)t.Jogadores.Count), 0, 2);
            foreach (var j in t.Jogadores)
            {
                PorTexto(s, j.Pais);
                PorTexto(s, j.Nome);
                s.Write(BitConverter.GetBytes((ushort)j.Posicao), 0, 2);
            }
            PorTexto(s, t.Treinador);
            File.WriteAllBytes(path, s.ToArray());
        }

        // Cada string e cifrada sozinha: tamanho em claro e cada letra somada ao
        // byte cifrado anterior (o primeiro soma o tamanho)
        private static string Texto(byte[] b, ref int o)
        {
            int n = b[o], anterior = n;
            var p = new byte[n];
            for (int i = 0; i < n; i++)
            {
                p[i] = (byte)(b[o + 1 + i] - anterior);
                anterior = b[o + 1 + i];
            }
            o += 1 + n;
            return Latin1.GetString(p);
        }

        private static void PorTexto(Stream s, string texto)
        {
            var p = Latin1.GetBytes(texto);
            if (p.Length > 255) throw new InvalidOperationException($"Texto longo demais: {texto}");
            s.WriteByte((byte)p.Length);
            int anterior = p.Length;
            foreach (var c in p)
            {
                anterior = (c + anterior) & 0xFF;
                s.WriteByte((byte)anterior);
            }
        }

        private static int Rgb(byte[] b, int o) => b[o] << 16 | b[o + 1] << 8 | b[o + 2];

        private static void PorRgb(Stream s, int rgb)
        {
            s.WriteByte((byte)(rgb >> 16));
            s.WriteByte((byte)(rgb >> 8));
            s.WriteByte((byte)rgb);
            s.WriteByte(0);
        }

        // ---- atributos pelo nome ----

        // S = soma dos codigos Latin-1 do nome com a 1a letra maiuscula
        public static int SomaNome(string nome)
        {
            if (string.IsNullOrEmpty(nome)) return 0;
            var p = Latin1.GetBytes(nome);
            if (p[0] >= 'a' && p[0] <= 'z') p[0] -= 0x20;
            int s = 0;
            foreach (var c in p) s += c;
            return s;
        }

        public static int Comportamento(int soma, int posicao)
        {
            int c = 5 - (int)Math.Floor(Math.Sqrt(soma % 36));
            return posicao == 1 ? (c + 2) % 6 : c;
        }

        // ---- listas .TXE (cifra do REFEREE.TXE) ----

        public static List<string> LerTxe(string path)
        {
            var b = File.ReadAllBytes(path);
            var lista = new List<string>();
            int o = 0;
            while (o < b.Length)
            {
                int n = b[o];
                if (o + 1 + n > b.Length) break;
                var p = new byte[n];
                for (int i = 0; i < n; i++)
                {
                    int k = (i + 2) * n;
                    for (int j = 0; j < i; j++) k += (i + 1 - j) * p[j];
                    p[i] = (byte)(b[o + 1 + i] - k);
                }
                lista.Add(Latin1.GetString(p));
                o += 1 + n;
            }
            return lista;
        }

        public static void GravarTxe(string path, IEnumerable<string> lista)
        {
            var s = new MemoryStream();
            foreach (var texto in lista)
            {
                var p = Latin1.GetBytes(texto);
                int n = p.Length;
                s.WriteByte((byte)n);
                for (int i = 0; i < n; i++)
                {
                    int k = (i + 2) * n;
                    for (int j = 0; j < i; j++) k += (i + 1 - j) * p[j];
                    s.WriteByte((byte)(p[i] + k));
                }
            }
            File.WriteAllBytes(path, s.ToArray());
        }

        // Arquivo do jogo sem ligar para maiusculas: no Linux/Android o Wine e o
        // Editor de Equipas regravam alguns em minusculas (country.txe)
        public static string Caminho(string dir, params string[] partes)
        {
            var atual = dir;
            foreach (var parte in partes)
            {
                var exato = Path.Combine(atual, parte);
                if (File.Exists(exato) || Directory.Exists(exato) || !Directory.Exists(atual)) { atual = exato; continue; }
                var achado = Directory.EnumerateFileSystemEntries(atual)
                    .FirstOrDefault(e => string.Equals(Path.GetFileName(e), parte, StringComparison.OrdinalIgnoreCase));
                atual = achado ?? exato;
            }
            return atual;
        }

        // COUNTRY.TXE: 1o registro "Country", depois "BRA Brasil"
        public static List<Pais> LerPaises(string gameDir)
        {
            var lista = new List<Pais>();
            foreach (var r in LerTxe(Caminho(gameDir, "COUNTRY.TXE")).Skip(1))
                if (r.Length > 4 && r[3] == ' ')
                    lista.Add(new Pais { Codigo = r.Substring(0, 3), Nome = r.Substring(4) });
            return lista;
        }

        // ---- Lei Bosman / PLOP ----

        // Lista original do BOSMAN.TXE (regravada fica igual byte a byte)
        private static readonly string[] BosmanOriginal =
            { "ALE", "AUT", "BEL", "BUL", "RCH", "CHP", "CRO", "DIN", "EVQ", "EVN", "ESP", "EST", "FIN", "FRA", "GRE", "HUN", "IRL", "ITA", "LET", "LIT", "LUX", "MLT", "HOL", "POL", "POR", "ROM", "SUE", "GBR", "ING", "ESC", "WAL", "ILN" };

        private static string BosmanPath(string gameDir) => Caminho(gameDir, "CTRGROUP", "BOSMAN.TXE");

        // "Liberado": o BOSMAN.TXE tem todos os paises do COUNTRY.TXE, entao
        // ninguem conta como estrangeiro (no editor e no jogo)
        public static bool BosmanLiberado(string gameDir)
        {
            try
            {
                var bosman = new HashSet<string>(LerTxe(BosmanPath(gameDir)));
                return LerPaises(gameDir).All(p => bosman.Contains(p.Codigo));
            }
            catch { return false; }
        }

        public static void DefinirBosman(string gameDir, bool liberado)
        {
            var codigos = liberado ? LerPaises(gameDir).Select(p => p.Codigo) : BosmanOriginal;
            var lista = new List<string> { "Bosman" };
            lista.AddRange(codigos);
            lista.Add("");
            GravarTxe(BosmanPath(gameDir), lista);
        }
    }

    // Regras da equipe com as listas Bosman e PLOP lidas do jogo
    public class RegrasEquipe
    {
        private readonly HashSet<string> _bosman, _plop, _paises;

        public RegrasEquipe(string gameDir, IEnumerable<Pais> paises)
        {
            _bosman = Ler(TeamCodec.Caminho(gameDir, "CTRGROUP", "BOSMAN.TXE"));
            _plop = Ler(TeamCodec.Caminho(gameDir, "CTRGROUP", "PLOP.TXE"));
            _paises = new HashSet<string>(paises.Select(p => p.Codigo));
        }

        private static HashSet<string> Ler(string path)
        {
            try { return new HashSet<string>(TeamCodec.LerTxe(path).Skip(1).Where(s => s.Length == 3)); }
            catch { return new HashSet<string>(); }
        }

        // seg12:0a2f: 0 nacional; 1 nao conta como estrangeiro (os dois paises no
        // BOSMAN ou os dois no PLOP); 2 estrangeiro
        public int Classificar(string paisJogador, string paisEquipe)
        {
            if (paisJogador == paisEquipe) return 0;
            if (_bosman.Contains(paisJogador) && _bosman.Contains(paisEquipe)) return 1;
            if (_plop.Contains(paisJogador) && _plop.Contains(paisEquipe)) return 1;
            return 2;
        }

        public string Situacao(string paisJogador, string paisEquipe)
        {
            if (paisJogador == paisEquipe) return "";
            if (_bosman.Contains(paisJogador) && _bosman.Contains(paisEquipe)) return "Bosman";
            if (_plop.Contains(paisJogador) && _plop.Contains(paisEquipe)) return "PLOP";
            return "Estrangeiro";
        }

        public int Estrangeiros(EftTeam t) => t.Jogadores.Count(j => Classificar(j.Pais, t.Pais) == 2);

        // Motivos para o jogo nao aceitar a equipe (seg12:275e); vazio = pronta
        public List<string> Validar(EftTeam t)
        {
            var erros = new List<string>();
            if (t.NomeCompleto.Trim().Length == 0 || t.NomeAbreviado.Trim().Length == 0)
                erros.Add("Não está definido o nome da equipa");
            if (!_paises.Contains(t.Pais)) erros.Add("Não está definido o país da equipa");
            if (t.Nivel < TeamCodec.NIVEL_MIN || t.Nivel > TeamCodec.NIVEL_MAX) erros.Add("Não está definido o nível da equipa");
            if (t.Jogadores.Count < TeamCodec.MIN_JOGADORES) erros.Add($"Não tem jogadores suficientes (mínimo {TeamCodec.MIN_JOGADORES})");
            if (t.Jogadores.Count > TeamCodec.MAX_JOGADORES) erros.Add($"Tem demasiados jogadores (máximo {TeamCodec.MAX_JOGADORES})");
            if (!t.Jogadores.Any(j => j.Posicao == 0)) erros.Add("Não tem guarda-redes");
            if (t.Jogadores.Count(j => j.Posicao >= 1 && j.Posicao <= 3) < TeamCodec.MIN_CAMPO)
                erros.Add($"Não tem jogadores de campo suficientes (mínimo {TeamCodec.MIN_CAMPO})");
            if (Estrangeiros(t) > TeamCodec.MAX_ESTRANGEIROS)
                erros.Add($"Tem demasiados jogadores estrangeiros (máximo {TeamCodec.MAX_ESTRANGEIROS})");
            if (t.Treinador.Trim().Length == 0) erros.Add("Não está definido o treinador");
            return erros;
        }
    }
}
