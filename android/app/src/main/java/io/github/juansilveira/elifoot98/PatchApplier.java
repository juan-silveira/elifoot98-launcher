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
 * Aplica um patch .zip na pasta do jogo: substitui os arquivos, troca a pasta EQUIPAS
 * inteira pela do patch (quando ele traz uma) e nunca mexe em JOGOS (saves). O
 * original volta pelo APK (Patches.trocar), entao nao fica copia da EQUIPAS antiga.
 */
public final class PatchApplier {
    public static final class Resultado {
        public int substituidos, ignorados;
    }

    private PatchApplier() {}

    public static Resultado aplicar(File zip, File jogo) throws IOException {
        Resultado r = new Resultado();
        try (ZipFile z = new ZipFile(zip)) {
            boolean temEquipas = false;
            for (Enumeration<? extends ZipEntry> e = z.entries(); e.hasMoreElements(); )
                if (e.nextElement().getName().toUpperCase().startsWith("EQUIPAS/")) temEquipas = true;
            File equipas = new File(jogo, "EQUIPAS");
            if (temEquipas && equipas.isDirectory()) apagar(equipas);
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

    private static void apagar(File f) throws IOException {
        File[] filhos = f.isDirectory() ? f.listFiles() : null;
        if (filhos != null) for (File c : filhos) apagar(c);
        if (!f.delete() && f.exists()) throw new IOException("Não consegui apagar " + f.getName());
    }
}
