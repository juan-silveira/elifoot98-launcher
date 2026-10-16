package io.github.juansilveira.elifoot98;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
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

    // O EXE montado (tools/divisoes/diferencas.py): o pacote traz so o ELIFOOT.EXE e o
    // JOGO.DIF; ao abrir, aplica as partes das Configuracoes (MOD, Nnn, idioma) e grava o
    // ELIFJOGO.EXE, so se mudou. Mesmo formato de src/OpcoesDoJogo.cs.
    static final String MONTADO = "ELIFJOGO.EXE";
    private static final byte[] ASSINATURA_DIF = "ELIFDIF1".getBytes(StandardCharsets.US_ASCII);

    /** Monta o EXE das Configuracoes com as opcoes e devolve o nome. Sem o JOGO.DIF
     *  (pacote antigo), acerta o ELIFnn.EXE que existir. */
    static String preparar(Context c, File jogo) throws IOException {
        File dif = new File(jogo, "JOGO.DIF"), original = new File(jogo, "ELIFOOT.EXE");
        if (!dif.exists() || !original.exists()) {
            String exe = Config.exeDoJogo(c, jogo);
            aplicar(new File(jogo, exe), daConfig(c));
            return exe;
        }
        List<String> partes = new ArrayList<>();
        String idioma = Config.idioma(c);
        if (Config.versaoOriginal(c)) {
            if (idioma.isEmpty()) return "ELIFOOT.EXE";
            partes.add("98" + idioma);
        } else {
            partes.add("MOD");
            int n = Config.equipes(c);
            if (n != 8) partes.add(String.format(Locale.ROOT, "N%02d", n));
            if (!idioma.isEmpty()) partes.add(idioma);
        }
        byte[] b = montar(Files.readAllBytes(original.toPath()), Files.readAllBytes(dif.toPath()), partes);
        aplicar(b, daConfig(c));
        fecharSoma(b);
        File saida = new File(jogo, MONTADO);
        if (!saida.exists() || !Arrays.equals(Files.readAllBytes(saida.toPath()), b))
            Files.write(saida.toPath(), b);
        return MONTADO;
    }

    /** Aplica ao EXE base as partes do JOGO.DIF, na ordem (sem recalcular a soma). */
    static byte[] montar(byte[] b, byte[] dif, List<String> partes) throws IOException {
        for (int k = 0; k < ASSINATURA_DIF.length; k++)
            if (dif[k] != ASSINATURA_DIF[k]) throw new IOException("JOGO.DIF invalido");
        Map<String, Integer> achadas = new HashMap<>();
        int n = dif[8] & 0xFF, o = 9;
        for (int k = 0; k < n; k++) {
            achadas.put(new String(dif, o, 8, StandardCharsets.US_ASCII).trim(), o + 8);
            int trechos = int32(dif, o + 12);
            o += 16;
            for (int t = 0; t < trechos; t++) o += 8 + int32(dif, o + 4);
        }
        for (String nome : partes) {
            Integer achada = achadas.get(nome);
            if (achada == null) throw new IOException("JOGO.DIF sem " + nome);
            int p = achada, tam = int32(dif, p), trechos = int32(dif, p + 4);
            if (tam != b.length) b = Arrays.copyOf(b, tam);      // o que cresce vem com zeros
            p += 8;
            for (int t = 0; t < trechos; t++) {
                int pos = int32(dif, p), len = int32(dif, p + 4);
                System.arraycopy(dif, p + 8, b, pos, len);
                p += 8 + len;
            }
        }
        return b;
    }

    private static int int32(byte[] b, int o) {
        return (b[o] & 0xFF) | (b[o + 1] & 0xFF) << 8 | (b[o + 2] & 0xFF) << 16 | (b[o + 3] & 0xFF) << 24;
    }

    private static void fecharSoma(byte[] b) {
        long s = soma(b);
        int f = b.length - 4;
        for (int i = 0; i < 4; i++) b[f + i] = (byte) (s >>> (8 * i));
    }

    /** Acerta o EXE; devolve true se mudou algo. EXE sem a tabela fica como esta. */
    static boolean aplicar(File exe, Map<String, Boolean> opcoes) throws IOException {
        if (!exe.exists()) return false;
        byte[] b = Files.readAllBytes(exe.toPath());
        if (!aplicar(b, opcoes)) return false;
        fecharSoma(b);
        Files.write(exe.toPath(), b);
        return true;
    }

    /** Poe na memoria os bytes das opcoes (sem a soma); devolve true se mudou algo. */
    static boolean aplicar(byte[] b, Map<String, Boolean> opcoes) {
        int t = achar(b);
        if (t < 0) return false;
        int n = b[t + 8] & 0xFF, o = t + 9;
        boolean mudou = false;
        for (int k = 0; k < n; k++) {
            String nome = new String(b, o, 8, StandardCharsets.US_ASCII).trim();
            int pos = int32(b, o + 8);
            int len = b[o + 12] & 0xFF;
            int lig = o + 13, des = o + 13 + len;
            o += 13 + 2 * len;
            Boolean ligada = opcoes.get(nome);
            if (ligada == null) continue;
            int origem = ligada ? lig : des;
            for (int i = 0; i < len; i++)
                if (b[pos + i] != b[origem + i]) { b[pos + i] = b[origem + i]; mudou = true; }
        }
        return mudou;
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
