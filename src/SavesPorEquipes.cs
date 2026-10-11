using System;
using System.IO;
using System.Linq;

namespace ElifootLauncher
{
    // Um save so abre no EXE com o mesmo numero de equipes por divisao em que foi
    // comecado (com outro, o jogo da "List index out of bounds."). Antes de abrir o
    // jogo, a pasta JOGOS fica so com os saves do numero escolhido; os outros vao
    // para JOGOS/<N> equipes e voltam quando esse numero for escolhido de novo.
    // O jogo so lista os *.e98 da propria pasta JOGOS.
    public static class SavesPorEquipes
    {
        public static string Pasta(string jogosDir, int n) => Path.Combine(jogosDir, $"{n} equipes");

        // Equipes por divisao de um save (as da 1a divisao); 0 se nao der para ler
        public static int EquipesDoSave(string path)
        {
            try
            {
                var sf = SaveCodec.Read(path);
                return sf.Teams.Count(t => t.Divisao.StartsWith("1"));
            }
            catch { return 0; }
        }

        public class Save
        {
            public string Caminho { get; set; } = "";
            public int Equipes { get; set; }       // por divisao; 0 se nao deu para ler
            public string Rotulo => Path.GetFileNameWithoutExtension(Caminho) + (Equipes > 0 ? $" — {Equipes} equipes" : "");
            public override string ToString() => Rotulo;
        }

        // Saves da pasta JOGOS e das pastas "<N> equipes", com quantas equipes por
        // divisao cada um tem (para os editores mostrarem de que jogo e cada save)
        public static System.Collections.Generic.List<Save> Listar(string jogosDir)
        {
            var lista = new System.Collections.Generic.List<Save>();
            if (!Directory.Exists(jogosDir)) return lista;
            var pastas = new[] { jogosDir }.Concat(Directory.GetDirectories(jogosDir, "* equipes"));
            foreach (var pasta in pastas)
                foreach (var f in Saves(pasta))
                    lista.Add(new Save { Caminho = f, Equipes = EquipesDoSave(f) });
            return lista.OrderBy(s => Path.GetFileName(s.Caminho), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(s => s.Equipes).ToList();
        }

        // Nome no padrao do jogo para um save importado: ate 8 letras/numeros e .e98
        public static string NomeDoJogo(string nome)
        {
            var b = System.Text.RegularExpressions.Regex.Replace(nome ?? "", @"\.e98$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            b = System.Text.RegularExpressions.Regex.Replace(b, "[^A-Za-z0-9_]", "");
            if (b.Length > 8) b = b.Substring(0, 8);
            return (b.Length == 0 ? "IMPORT" : b) + ".e98";
        }

        // Save ja existente com esse nome (sem diferenciar maiusculas), em JOGOS ou
        // numa pasta "<N> equipes"; null se nao houver
        public static string? Achar(string jogosDir, string nome) =>
            Listar(jogosDir).Select(s => s.Caminho)
                .FirstOrDefault(c => string.Equals(Path.GetFileName(c), nome, StringComparison.OrdinalIgnoreCase));

        public static void Separar(string jogosDir, int n)
        {
            if (!Directory.Exists(jogosDir)) return;
            foreach (var f in Saves(jogosDir))
            {
                int m = EquipesDoSave(f);
                if (m <= 0 || m == n) continue;
                Mover(f, Pasta(jogosDir, m));
            }
            var minha = Pasta(jogosDir, n);
            if (!Directory.Exists(minha)) return;
            foreach (var f in Saves(minha))
                Mover(f, jogosDir);
            if (!Directory.EnumerateFileSystemEntries(minha).Any()) Directory.Delete(minha);
        }

        // *.e98 sem diferenciar maiusculas (no Linux o GetFiles diferencia; ha saves .E98)
        private static string[] Saves(string pasta) =>
            Directory.GetFiles(pasta).Where(f => f.EndsWith(".e98", StringComparison.OrdinalIgnoreCase)).ToArray();

        // Nunca sobrescreve: se ja existe um save com o mesmo nome, acrescenta um numero
        private static void Mover(string arquivo, string pasta)
        {
            Directory.CreateDirectory(pasta);
            var nome = Path.GetFileNameWithoutExtension(arquivo);
            var ext = Path.GetExtension(arquivo);
            var destino = Path.Combine(pasta, nome + ext);
            for (int i = 2; File.Exists(destino); i++)
                destino = Path.Combine(pasta, $"{nome}{i}{ext}");
            File.Move(arquivo, destino);
        }
    }
}
