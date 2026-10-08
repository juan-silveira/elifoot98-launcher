package io.github.juansilveira.elifoot98;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Senha e Contra-senha do Elifoot 98, replicadas do ELIFOOT.EXE.
 * Porte de src/Registro.cs (launcher do Windows/Linux/macOS).
 */
public final class Registro {
    /** 9 = "Registro para autor 2" (contra-senha comeca com 9) */
    public static final int TIPO_AUTOR_2 = 9;

    private Registro() {}

    /**
     * eli.cod: strings Pascal ([len][dados]) cifradas 2x pela rotina do jogo
     * (seg14:0734): cada byte, a partir do 1o de dados, soma o byte anterior
     * (incluindo o de tamanho). Contem 2 strings: A (derivada das datas das
     * pastas WINDOWS/SYSTEM) e B (Random(20000) sorteado pelo jogo).
     */
    public static String senhaDoEliCod(byte[] cod) throws IOException {
        List<byte[]> strings = new ArrayList<>();
        for (int i = 0; i < cod.length; ) {
            int n = cod[i] & 0xff;
            if (i + 1 + n > cod.length) throw new IOException("eli.cod corrompido.");
            byte[] rec = new byte[n + 1];
            System.arraycopy(cod, i, rec, 0, n + 1);
            desfazer(rec);
            desfazer(rec);
            byte[] s = new byte[n];
            System.arraycopy(rec, 1, s, 0, n);
            strings.add(s);
            i += 1 + n;
        }
        if (strings.size() < 2) throw new IOException("eli.cod em formato inesperado.");
        return "014-" + formatar(concat(strings.get(0), strings.get(1)));
    }

    /** O jogo aceita a Contra-senha do tipo N quando ela e a senha transformada N vezes (seg12:3645). */
    public static String contraSenha(String senha, int tipo) {
        byte[] s = senha.getBytes(StandardCharsets.US_ASCII);
        for (int n = 0; n < tipo; n++) s = transformar(s);
        return new String(s, StandardCharsets.US_ASCII);
    }

    // seg12:3645: escolhe a regra pelo 1o digito e depois soma 1 a ele
    private static byte[] transformar(byte[] s) {
        int c = s[0] & 0xff;
        byte[] r;
        switch (c - '0') {
            case 0: r = ascii(formatar(concat(ascii("***"), s, s))); break;
            case 1: r = s.clone(); break;
            case 2:
                r = s.clone();
                r[4] = (r[4] & 0xff) < '9' ? (byte) (r[4] + 1) : (byte) '0';
                break;
            case 3:
            case 4:
            case 5: r = ascii(formatar(concat(ascii("+++"), s, ascii("***"), s))); break;
            default: r = ascii(formatar(concat(ascii("1213"), s, s, ascii("XXX"), s))); break;
        }
        r[0] = (byte) (c + 1);
        return r;
    }

    // Desfaz uma passada da cifra numa string Pascal (rec[0] = tamanho)
    private static void desfazer(byte[] rec) {
        for (int k = rec.length - 1; k >= 1; k--) rec[k] = (byte) (rec[k] - rec[k - 1]);
    }

    // Replica seg12:2f8b: completa com '0' ate 19, comprime ate 17 digitos com
    // (3*proximo + atual + 3) mod 10, volta o tamanho pra 19 (os 2 ultimos sao
    // sobras do buffer) e poe hifens nas posicoes 4, 8, 12 e 16.
    private static String formatar(byte[] s) {
        int[] buf = new int[256];
        int len = Math.min(s.length, 255);
        for (int i = 0; i < len; i++) buf[1 + i] = s[i] & 0xff;
        while (len < 19) buf[1 + len++] = '0';
        while (len > 17) {
            for (int i = 1; i < len; i++) buf[i] = (buf[i + 1] * 3 + buf[i] + 3) % 10 + '0';
            len--;
        }
        for (int k : new int[] {4, 8, 12, 16}) buf[k] = '-';
        StringBuilder sb = new StringBuilder(19);
        for (int i = 1; i <= 19; i++) sb.append((char) buf[i]);
        return sb.toString();
    }

    private static byte[] ascii(String s) {
        return s.getBytes(StandardCharsets.US_ASCII);
    }

    private static byte[] concat(byte[]... partes) {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        for (byte[] p : partes) out.write(p, 0, p.length);
        return out.toByteArray();
    }
}
