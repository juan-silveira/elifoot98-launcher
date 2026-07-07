using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ElifootLauncher
{
    public class PatchResult
    {
        public bool Ok;
        public string Error = "";
        public int FilesReplaced;
        public string? EquipasBackupName;
        public List<string> IgnoredEntries { get; } = new List<string>();
    }

    // Aplica um patch .zip na pasta game do Elifoot 98.
    //
    // Regras:
    // 1. Todos os arquivos e pastas do zip sao extraidos SUBSTITUINDO o
    //    conteudo atual da pasta game.
    // 2. EXCECAO: a pasta EQUIPAS/ nao substitui a atual. Antes de extrair a
    //    EQUIPAS do patch, a EQUIPAS atual eh RENOMEADA pra EQUIPAS_OLD.
    //    Se ja existir EQUIPAS_OLD, tenta EQUIPAS_OLD_2, EQUIPAS_OLD_3, ...
    // 3. Preserva JOGOS/ (saves do usuario) — nunca sobrescreve.
    public static class PatchApplier
    {
        private static readonly string[] PreserveFolders = { "JOGOS" };

        public static PatchResult Apply(string zipPath, string gameDir)
        {
            var result = new PatchResult();
            if (!File.Exists(zipPath))
            { result.Error = $"Zip nao encontrado: {zipPath}"; return result; }
            if (!Directory.Exists(gameDir))
            { result.Error = $"Pasta game nao encontrada: {gameDir}"; return result; }

            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    // 1) Se o patch traz EQUIPAS/, faz backup da atual antes
                    bool patchHasEquipas = archive.Entries.Any(e =>
                        e.FullName.StartsWith("EQUIPAS/", StringComparison.OrdinalIgnoreCase));
                    var currentEquipas = Path.Combine(gameDir, "EQUIPAS");
                    if (patchHasEquipas && Directory.Exists(currentEquipas))
                    {
                        string backupName = FindNextEquipasBackupName(gameDir);
                        var backupDir = Path.Combine(gameDir, backupName);
                        Directory.Move(currentEquipas, backupDir);
                        result.EquipasBackupName = backupName;
                    }

                    // 2) Extrai todas as entries do zip
                    foreach (var entry in archive.Entries)
                    {
                        // Pula pastas puras (Length=0 e FullName termina em /)
                        if (string.IsNullOrEmpty(entry.Name)) continue;

                        var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                        var topFolder = SplitTop(entry.FullName);

                        // Preserva pastas do usuario (JOGOS)
                        if (PreserveFolders.Any(p =>
                            string.Equals(topFolder, p, StringComparison.OrdinalIgnoreCase)))
                        {
                            result.IgnoredEntries.Add(entry.FullName);
                            continue;
                        }

                        var destPath = Path.Combine(gameDir, relative);
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);

                        entry.ExtractToFile(destPath, overwrite: true);
                        result.FilesReplaced++;
                    }
                }
                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
            }
            return result;
        }

        private static string FindNextEquipasBackupName(string gameDir)
        {
            var candidate = "EQUIPAS_OLD";
            if (!Directory.Exists(Path.Combine(gameDir, candidate))) return candidate;
            int n = 2;
            while (true)
            {
                candidate = $"EQUIPAS_OLD_{n}";
                if (!Directory.Exists(Path.Combine(gameDir, candidate))) return candidate;
                n++;
                if (n > 999) throw new Exception("Muitos backups EQUIPAS_OLD_*.");
            }
        }

        private static string SplitTop(string fullName)
        {
            int slash = fullName.IndexOf('/');
            return slash >= 0 ? fullName.Substring(0, slash) : fullName;
        }
    }
}
