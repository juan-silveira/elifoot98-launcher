using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ElifootRegistrador
{
    // Payload.zip embutido como EmbeddedResource contém CRACK.EXE +
    // vendor/dosbox/*. Ao rodar em modo PORTABLE (sem CRACK.EXE do lado
    // do exe), extraímos pro %TEMP% e usamos.
    public static class Payload
    {
        public static string ResolveInstallDir(string appDir)
        {
            var installedCrack = Path.Combine(appDir, "CRACK.EXE");
            if (File.Exists(installedCrack))
            {
                // Modo instalado — usa arquivos do lado do exe
                return appDir;
            }

            // Modo portable — extrai payload embutido pra %TEMP%
            var target = Path.Combine(Path.GetTempPath(), "ElifootRegistrador");
            var doneMarker = Path.Combine(target, ".extracted-v1");
            if (File.Exists(doneMarker))
                return target;

            Directory.CreateDirectory(target);
            ExtractEmbeddedZip("ElifootRegistrador.payload.zip", target);
            File.WriteAllText(doneMarker, DateTime.UtcNow.ToString("O"));
            return target;
        }

        private static void ExtractEmbeddedZip(string resourceName, string target)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        $"Recurso embutido '{resourceName}' não encontrado. Este .exe foi buildado sem payload — use o instalador oficial.");

                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // pasta
                        var destPath = Path.Combine(target, entry.FullName);
                        var destDir = Path.GetDirectoryName(destPath);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);
                        entry.ExtractToFile(destPath, overwrite: true);
                    }
                }
            }
        }
    }
}
