using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ElifootLauncher
{
    // Opcoes do jogo escolhidas nas Configuracoes (cartoes amarelos, patrocinadores...).
    // Os EXEs do launcher trazem uma tabela "ELIFOPC1" (tools/divisoes/opcoes.py) com os
    // bytes que ligam ou desligam cada adicao; antes de abrir o jogo, o launcher poe nesses
    // bytes os valores escolhidos e recalcula a soma da autoverificacao (4 ultimos bytes).
    public static class OpcoesDoJogo
    {
        private static readonly byte[] Assinatura = Encoding.ASCII.GetBytes("ELIFOPC1");

        public static Dictionary<string, bool> DaConfig(LauncherConfig cfg) => new Dictionary<string, bool>
        {
            ["CARTOES"] = cfg.Cartoes,
            ["PATROC"] = cfg.Patrocinios,
        };

        // O EXE montado (tools/divisoes/diferencas.py): o pacote traz so o ELIFOOT.EXE e o
        // JOGO.DIF; ao abrir, o launcher aplica as partes das Configuracoes (MOD, Nnn,
        // idioma) e grava o ELIFJOGO.EXE, so se mudou
        public const string Montado = "ELIFJOGO.EXE";
        private static readonly byte[] AssinaturaDif = Encoding.ASCII.GetBytes("ELIFDIF1");

        /// Monta o EXE das Configuracoes com as opcoes e devolve o caminho. Sem o JOGO.DIF
        /// (pacote antigo), acerta o ELIFnn.EXE que existir.
        public static string Preparar(string pasta, LauncherConfig cfg)
        {
            var dif = Path.Combine(pasta, "JOGO.DIF");
            var original = Path.Combine(pasta, "ELIFOOT.EXE");
            if (!File.Exists(dif) || !File.Exists(original))
            {
                var exe = cfg.ExeExistente(pasta);
                try { Aplicar(exe, DaConfig(cfg)); } catch { /* sem permissao: abre como esta */ }
                return exe;
            }
            var partes = new List<string>();
            if (cfg.VersaoOriginal)
            {
                if (cfg.Idioma.Length == 0) return original;
                partes.Add("98" + cfg.Idioma);
            }
            else
            {
                partes.Add("MOD");
                if (cfg.EquipesPorDivisao != 8) partes.Add($"N{cfg.EquipesPorDivisao:00}");
                if (cfg.Idioma.Length > 0) partes.Add(cfg.Idioma);
            }
            var b = Montar(File.ReadAllBytes(original), File.ReadAllBytes(dif), partes);
            Aplicar(b, DaConfig(cfg));
            BitConverter.GetBytes(Soma(b)).CopyTo(b, b.Length - 4);
            var saida = Path.Combine(pasta, Montado);
            if (!File.Exists(saida) || !Iguais(File.ReadAllBytes(saida), b))
                File.WriteAllBytes(saida, b);
            return saida;
        }

        /// Aplica ao EXE base as partes do JOGO.DIF, na ordem (sem recalcular a soma)
        public static byte[] Montar(byte[] b, byte[] dif, IList<string> partes)
        {
            for (int k = 0; k < AssinaturaDif.Length; k++)
                if (dif[k] != AssinaturaDif[k]) throw new InvalidDataException("JOGO.DIF invalido");
            var achadas = new Dictionary<string, int>();
            int n = dif[8], o = 9;
            for (int k = 0; k < n; k++)
            {
                achadas[Encoding.ASCII.GetString(dif, o, 8).TrimEnd()] = o + 8;
                int trechos = BitConverter.ToInt32(dif, o + 12);
                o += 16;
                for (int t = 0; t < trechos; t++) o += 8 + BitConverter.ToInt32(dif, o + 4);
            }
            foreach (var nome in partes)
            {
                if (!achadas.TryGetValue(nome, out int p)) throw new InvalidDataException("JOGO.DIF sem " + nome);
                int tam = BitConverter.ToInt32(dif, p), trechos = BitConverter.ToInt32(dif, p + 4);
                if (tam != b.Length) Array.Resize(ref b, tam);      // o que cresce vem com zeros
                p += 8;
                for (int t = 0; t < trechos; t++)
                {
                    int pos = BitConverter.ToInt32(dif, p), len = BitConverter.ToInt32(dif, p + 4);
                    Buffer.BlockCopy(dif, p + 8, b, pos, len);
                    p += 8 + len;
                }
            }
            return b;
        }

        private static bool Iguais(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// Acerta o EXE; devolve true se mudou algo. EXE sem a tabela (o original) fica como esta.
        public static bool Aplicar(string exe, IDictionary<string, bool> opcoes)
        {
            if (!File.Exists(exe)) return false;
            var b = File.ReadAllBytes(exe);
            if (!Aplicar(b, opcoes)) return false;
            BitConverter.GetBytes(Soma(b)).CopyTo(b, b.Length - 4);
            File.WriteAllBytes(exe, b);
            return true;
        }

        /// Poe na memoria os bytes das opcoes (sem a soma); devolve true se mudou algo
        public static bool Aplicar(byte[] b, IDictionary<string, bool> opcoes)
        {
            int t = Achar(b);
            if (t < 0) return false;
            int n = b[t + 8], o = t + 9;
            bool mudou = false;
            for (int k = 0; k < n; k++)
            {
                var nome = Encoding.ASCII.GetString(b, o, 8).TrimEnd();
                int pos = BitConverter.ToInt32(b, o + 8);
                int len = b[o + 12];
                int lig = o + 13, des = o + 13 + len;
                o += 13 + 2 * len;
                if (!opcoes.TryGetValue(nome, out bool ligada)) continue;
                int origem = ligada ? lig : des;
                for (int i = 0; i < len; i++)
                    if (b[pos + i] != b[origem + i]) { b[pos + i] = b[origem + i]; mudou = true; }
            }
            return mudou;
        }

        // A soma que o jogo confere ao abrir (docs/elifoot98-interno.md, Autoverificacao)
        public static uint Soma(byte[] b)
        {
            uint s = 0;
            for (int i = 0; i < b.Length - 4; i++)
                s += (uint)(b[i] * ((i % 6000) % 5 + 1));
            return s;
        }

        private static int Achar(byte[] b)
        {
            for (int i = b.Length - Assinatura.Length; i >= 0; i--)
            {
                int k = 0;
                while (k < Assinatura.Length && b[i + k] == Assinatura[k]) k++;
                if (k == Assinatura.Length) return i;
            }
            return -1;
        }
    }
}
