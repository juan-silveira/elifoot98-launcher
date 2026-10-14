using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ElifootLauncher
{
    // Biblioteca de patches (como no Android): cada patch aplicado fica guardado em
    // <biblioteca>/NOME.zip e as Configuracoes trocam entre eles e os times originais
    // quando quiser. Trocar volta a pasta do jogo aos arquivos originais do pacote
    // (sem tocar nos saves, JOGOS) e aplica o escolhido; nada de pastas EQUIPAS_OLD.
    // O patch em uso fica anotado em <pasta do jogo>/.patch.
    public static class Patches
    {
        public static List<string> Listar(string biblioteca)
        {
            if (!Directory.Exists(biblioteca)) return new List<string>();
            return Directory.GetFiles(biblioteca, "*.zip")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => !string.IsNullOrEmpty(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// Guarda o .zip na biblioteca e devolve o nome dele la.
        public static string Adicionar(string biblioteca, string zip)
        {
            Directory.CreateDirectory(biblioteca);
            var nome = Path.GetFileNameWithoutExtension(zip);
            foreach (var c in Path.GetInvalidFileNameChars()) nome = nome.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(nome)) nome = "patch";
            File.Copy(zip, Path.Combine(biblioteca, nome + ".zip"), overwrite: true);
            return nome;
        }

        public static void Remover(string biblioteca, string nome)
        {
            var zip = Path.Combine(biblioteca, nome + ".zip");
            if (File.Exists(zip)) File.Delete(zip);
        }

        public static string EmUso(string gameDir)
        {
            try { return File.ReadAllText(Path.Combine(gameDir, ".patch")).Trim(); }
            catch { return ""; }
        }

        /// Poe na pasta do jogo os arquivos originais (de <paramref name="originais"/>) e,
        /// se <paramref name="nome"/> nao for vazio, o patch da biblioteca. JOGOS fica; a
        /// regra de estrangeiros liberada e refeita depois.
        public static PatchResult Trocar(string gameDir, string originais, string biblioteca, string nome)
        {
            if (!Directory.Exists(originais))
                return new PatchResult { Error = $"Não achei os arquivos originais do jogo em {originais}." };
            bool liberado = false;
            try { liberado = TeamCodec.BosmanLiberado(gameDir); } catch { }

            foreach (var item in Directory.GetFileSystemEntries(gameDir))
            {
                if (string.Equals(Path.GetFileName(item), "JOGOS", StringComparison.OrdinalIgnoreCase)) continue;
                if (Directory.Exists(item)) Directory.Delete(item, recursive: true);
                else File.Delete(item);
            }
            Copiar(originais, gameDir);

            var resultado = new PatchResult { Ok = true };
            if (!string.IsNullOrEmpty(nome))
            {
                resultado = PatchApplier.Apply(Path.Combine(biblioteca, nome + ".zip"), gameDir);
                if (!resultado.Ok) return resultado;
            }
            if (liberado)
                try { TeamCodec.DefinirBosman(gameDir, true); } catch { }
            File.WriteAllText(Path.Combine(gameDir, ".patch"), nome);
            return resultado;
        }

        /// Depois que uma atualizacao copiou os arquivos do pacote por cima da pasta do jogo
        /// (versaoMudou), refaz o patch em uso; e deixa a regra de estrangeiros como a
        /// escolhida nas Configuracoes (liberado: null = nunca escolhida, nao mexe).
        public static void Manter(string gameDir, string originais, string biblioteca, bool versaoMudou, bool? liberado)
        {
            if (versaoMudou)
            {
                var nome = EmUso(gameDir);
                if (!string.IsNullOrEmpty(nome) && File.Exists(Path.Combine(biblioteca, nome + ".zip")))
                    try { Trocar(gameDir, originais, biblioteca, nome); } catch { }
            }
            if (liberado.HasValue)
                try
                {
                    if (TeamCodec.BosmanLiberado(gameDir) != liberado.Value)
                        TeamCodec.DefinirBosman(gameDir, liberado.Value);
                }
                catch { }
            // a tabela dos patrocinadores que o jogo le (PATROCIN/MARCAS.TXT -> .DAT)
            try { Patrocinadores.Preparar(gameDir); } catch { }
        }

        private static void Copiar(string de, string para)
        {
            foreach (var arq in Directory.EnumerateFiles(de, "*", SearchOption.AllDirectories))
            {
                var rel = arq.Substring(de.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var topo = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                if (string.Equals(topo, "JOGOS", StringComparison.OrdinalIgnoreCase)) continue;
                var alvo = Path.Combine(para, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(alvo)!);
                File.Copy(arq, alvo, overwrite: true);
            }
        }
    }
}
