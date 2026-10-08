package io.github.juansilveira.elifoot98;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;

/**
 * Codec do REFEREE.TXE (porte de src/RefereeCodec.cs):
 *   cipher[i] = (plain[i] + (i+2)*L + soma_{j<i}(i+1-j)*plain[j]) mod 256
 * Sequencia de strings Pascal; a 1a e a marca "Referee", as demais "PAI nome".
 */
public final class RefereeCodec {
    public static final class Arbitro {
        public String pais = "";
        public String nome = "";
    }

    public static final class Arquivo {
        public String marca = "Referee";
        public final List<Arbitro> arbitros = new ArrayList<>();
    }

    private RefereeCodec() {}

    public static Arquivo ler(File f) throws IOException {
        byte[] b = Files.readAllBytes(f.toPath());
        Arquivo a = new Arquivo();
        int pos = 0;
        boolean primeiro = true;
        while (pos < b.length) {
            int len = b[pos++] & 0xff;
            if (pos + len > b.length) break;
            String texto = new String(decodificar(b, pos, len), StandardCharsets.ISO_8859_1);
            pos += len;
            if (primeiro) {
                a.marca = texto;
                primeiro = false;
                continue;
            }
            Arbitro r = new Arbitro();
            if (texto.length() >= 4 && texto.charAt(3) == ' ') {
                r.pais = texto.substring(0, 3);
                r.nome = texto.substring(4);
            } else {
                r.pais = "???";
                r.nome = texto;
            }
            a.arbitros.add(r);
        }
        return a;
    }

    public static void gravar(Arquivo a, File f) throws IOException {
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        registro(out, a.marca);
        for (Arbitro r : a.arbitros) registro(out, r.pais + " " + r.nome);
        try (FileOutputStream fo = new FileOutputStream(f)) {
            out.writeTo(fo);
        }
    }

    private static void registro(ByteArrayOutputStream out, String texto) throws IOException {
        byte[] plain = texto.getBytes(StandardCharsets.ISO_8859_1);
        if (plain.length > 255) throw new IOException("Texto longo demais (>255): " + texto);
        out.write(plain.length);
        int len = plain.length;
        for (int i = 0; i < len; i++) {
            int k = (i + 2) * len;
            for (int j = 0; j < i; j++) k += (i + 1 - j) * (plain[j] & 0xff);
            out.write(((plain[i] & 0xff) + k) & 0xff);
        }
    }

    private static byte[] decodificar(byte[] c, int off, int len) {
        byte[] p = new byte[len];
        for (int i = 0; i < len; i++) {
            int k = (i + 2) * len;
            for (int j = 0; j < i; j++) k += (i + 1 - j) * (p[j] & 0xff);
            p[i] = (byte) (((c[off + i] & 0xff) - k) & 0xff);
        }
        return p;
    }
}
