package io.github.juansilveira.elifoot98;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.Enumeration;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

/**
 * Aplica um patch .zip na pasta do jogo (porte de src/PatchApplier.cs):
 * substitui tudo, mas renomeia a EQUIPAS atual pra EQUIPAS_OLD(_N) antes e
 * nunca mexe em JOGOS (saves).
 */
public final class PatchApplier {
    public static final class Resultado {
        public int substituidos, ignorados;
        public String backupEquipas;
    }

    private PatchApplier() {}

    public static Resultado aplicar(File zip, File jogo) throws IOException {
        Resultado r = new Resultado();
        try (ZipFile z = new ZipFile(zip)) {
            boolean temEquipas = false;
            for (Enumeration<? extends ZipEntry> e = z.entries(); e.hasMoreElements(); )
                if (e.nextElement().getName().toUpperCase().startsWith("EQUIPAS/")) temEquipas = true;
            File equipas = new File(jogo, "EQUIPAS");
            if (temEquipas && equipas.isDirectory()) {
                String nome = "EQUIPAS_OLD";
                for (int n = 2; new File(jogo, nome).exists(); n++) nome = "EQUIPAS_OLD_" + n;
                if (!equipas.renameTo(new File(jogo, nome))) throw new IOException("Não consegui renomear a pasta EQUIPAS.");
                r.backupEquipas = nome;
            }
            byte[] buf = new byte[1 << 16];
            for (Enumeration<? extends ZipEntry> e = z.entries(); e.hasMoreElements(); ) {
                ZipEntry en = e.nextElement();
                if (en.isDirectory()) continue;
                String nome = en.getName();
                if (nome.toUpperCase().startsWith("JOGOS/")) { r.ignorados++; continue; }
                File destino = new File(jogo, nome);
                if (!destino.getCanonicalPath().startsWith(jogo.getCanonicalPath() + File.separator))
                    continue;  // entradas com "../" ficam de fora
                File pai = destino.getParentFile();
                if (pai != null) pai.mkdirs();
                try (InputStream in = z.getInputStream(en); OutputStream out = new FileOutputStream(destino)) {
                    int n;
                    while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
                }
                r.substituidos++;
            }
        }
        return r;
    }
}
