package io.github.juansilveira.elifoot98;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.HashMap;
import java.util.Map;

import android.content.Context;

/**
 * Opcoes do jogo escolhidas nas Configuracoes (cartoes amarelos, patrocinadores...).
 * Os EXEs do launcher trazem uma tabela "ELIFOPC1" (tools/divisoes/opcoes.py) com os bytes
 * que ligam ou desligam cada adicao; antes de abrir o jogo, o launcher poe nesses bytes os
 * valores escolhidos e recalcula a soma da autoverificacao (4 ultimos bytes). Mesmo
 * formato de src/OpcoesDoJogo.cs.
 */
final class OpcoesDoJogo {
    private static final byte[] ASSINATURA = "ELIFOPC1".getBytes(StandardCharsets.US_ASCII);

    static Map<String, Boolean> daConfig(Context c) {
        Map<String, Boolean> m = new HashMap<>();
        m.put("CARTOES", Config.cartoes(c));
        m.put("PATROC", Config.patrocinios(c));
        return m;
    }

    /** Acerta o EXE; devolve true se mudou algo. EXE sem a tabela fica como esta. */
    static boolean aplicar(File exe, Map<String, Boolean> opcoes) throws IOException {
        if (!exe.exists()) return false;
        byte[] b = Files.readAllBytes(exe.toPath());
        int t = achar(b);
        if (t < 0) return false;
        int n = b[t + 8] & 0xFF, o = t + 9;
        boolean mudou = false;
        for (int k = 0; k < n; k++) {
            String nome = new String(b, o, 8, StandardCharsets.US_ASCII).trim();
            int pos = (b[o + 8] & 0xFF) | (b[o + 9] & 0xFF) << 8 | (b[o + 10] & 0xFF) << 16 | (b[o + 11] & 0xFF) << 24;
            int len = b[o + 12] & 0xFF;
            int lig = o + 13, des = o + 13 + len;
            o += 13 + 2 * len;
            Boolean ligada = opcoes.get(nome);
            if (ligada == null) continue;
            int origem = ligada ? lig : des;
            for (int i = 0; i < len; i++)
                if (b[pos + i] != b[origem + i]) { b[pos + i] = b[origem + i]; mudou = true; }
        }
        if (!mudou) return false;
        long s = soma(b);
        int f = b.length - 4;
        for (int i = 0; i < 4; i++) b[f + i] = (byte) (s >>> (8 * i));
        Files.write(exe.toPath(), b);
        return true;
    }

    /** A soma que o jogo confere ao abrir (docs/elifoot98-interno.md, Autoverificacao). */
    static long soma(byte[] b) {
        long s = 0;
        for (int i = 0; i < b.length - 4; i++) s += (long) (b[i] & 0xFF) * ((i % 6000) % 5 + 1);
        return s & 0xFFFFFFFFL;
    }

    private static int achar(byte[] b) {
        for (int i = b.length - ASSINATURA.length; i >= 0; i--) {
            int k = 0;
            while (k < ASSINATURA.length && b[i + k] == ASSINATURA[k]) k++;
            if (k == ASSINATURA.length) return i;
        }
        return -1;
    }
}
