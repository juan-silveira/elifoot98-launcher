package io.github.juansilveira.elifoot98;

import android.content.Context;
import android.net.Uri;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Biblioteca de patches: cada patch aplicado fica guardado (files/patches/NOME.zip) e as
 * Configuracoes trocam entre eles e os times originais quando quiser. Trocar volta o
 * jogo ao original do app (Jogo.restaurarOriginal, sem tocar nos saves) e aplica o
 * escolhido; nada de pastas EQUIPAS_OLD.
 */
final class Patches {
    private Patches() {}

    static File pasta(Context c) {
        File p = new File(c.getFilesDir(), "patches");
        p.mkdirs();
        return p;
    }

    static List<String> listar(Context c) {
        List<String> nomes = new ArrayList<>();
        File[] fs = pasta(c).listFiles((d, n) -> n.toLowerCase().endsWith(".zip"));
        if (fs != null) for (File f : fs) nomes.add(f.getName().substring(0, f.getName().length() - 4));
        java.util.Collections.sort(nomes, String.CASE_INSENSITIVE_ORDER);
        return nomes;
    }

    /** Guarda o .zip escolhido na biblioteca e devolve o nome dele la. */
    static String adicionar(Context c, Uri uri, String nomeDoArquivo) throws IOException {
        String nome = nomeDoArquivo == null ? "patch" : nomeDoArquivo.replaceAll("(?i)\\.zip$", "");
        nome = nome.replaceAll("[^A-Za-z0-9À-ÿ _().-]", "_").trim();
        if (nome.isEmpty()) nome = "patch";
        File destino = new File(pasta(c), nome + ".zip");
        try (InputStream in = c.getContentResolver().openInputStream(uri); OutputStream out = new FileOutputStream(destino)) {
            if (in == null) throw new IOException("Não consegui ler o arquivo escolhido.");
            byte[] buf = new byte[1 << 16];
            for (int n; (n = in.read(buf)) > 0; ) out.write(buf, 0, n);
        }
        return nome;
    }

    static void remover(Context c, String nome) {
        new File(pasta(c), nome + ".zip").delete();
    }

    /**
     * Poe no jogo os times do patch (ou os originais, com nome ""). Os saves (JOGOS), as
     * opcoes e o registro ficam; a regra de estrangeiros liberada e refeita depois.
     */
    static PatchApplier.Resultado trocar(Context c, Jogo jogo, String nome) throws IOException {
        boolean liberado = TeamCodec.bosmanLiberado(jogo.jogo);
        jogo.restaurarOriginal(c);
        PatchApplier.Resultado r = null;
        if (!nome.isEmpty()) {
            File zip = new File(pasta(c), nome + ".zip");
            if (!zip.exists()) throw new IOException("O patch " + nome + " não está mais na lista.");
            r = PatchApplier.aplicar(zip, jogo.jogo);
        }
        if (liberado) TeamCodec.definirBosman(jogo.jogo, true);
        Config.prefs(c).edit().putString("patch", nome).apply();
        return r;
    }

    static boolean existe(Context c, String nome) {
        return nome.isEmpty() || Arrays.asList(pasta(c).list()).contains(nome + ".zip");
    }
}
