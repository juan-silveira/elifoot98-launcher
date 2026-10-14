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

        /// Acerta o EXE; devolve true se mudou algo. EXE sem a tabela (o original) fica como esta.
        public static bool Aplicar(string exe, IDictionary<string, bool> opcoes)
        {
            if (!File.Exists(exe)) return false;
            var b = File.ReadAllBytes(exe);
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
            if (!mudou) return false;
            BitConverter.GetBytes(Soma(b)).CopyTo(b, b.Length - 4);
            File.WriteAllBytes(exe, b);
            return true;
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
