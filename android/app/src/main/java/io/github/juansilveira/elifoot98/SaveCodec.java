package io.github.juansilveira.elifoot98;

import java.io.File;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;

/**
 * Codec dos saves .e98 do Elifoot 98 (porte de src/SaveCodec.cs; ver os
 * comentarios la sobre o formato: corpo dos EFT com Caesar rolante, records de
 * jogador marcados por 0x03 + nacionalidade, verba em first_record + 0x8E).
 */
public final class SaveCodec {
    public static final int FORCA_MIN = 1, FORCA_MAX = 9999, FORCA_AVISO_ACIMA = 50;
    public static final int SALARIO_MIN = 50, SALARIO_MAX = 99999;
    public static final String[] COMPORTAMENTOS = {
        "Fair Play", "Cordeirinho", "Cavalheiro", "Caneleiro", "Caceteiro", "Sarrafeiro"
    };
    private static final byte[] EFT_MAGIC = {'E', 'F', 'a', 0};

    public static final class Jogador {
        public String nome = "", posicao = "G";
        public boolean estrela;
        public int comportamento, forca, salario;
        int forcaOff, salarioOff;
    }

    public static final class Time {
        public String nome = "";
        public long verba;
        int verbaOff = -1;
        public final List<Jogador> jogadores = new ArrayList<>();
    }

    public static final class Save {
        byte[] bytes;
        public final List<Time> times = new ArrayList<>();
    }

    private SaveCodec() {}

    public static Save ler(File f) throws IOException {
        byte[] b = Files.readAllBytes(f.toPath());
        Save sf = new Save();
        sf.bytes = b;
        List<Integer> efts = new ArrayList<>();
        for (int i = indexOf(b, EFT_MAGIC, 0); i >= 0; i = indexOf(b, EFT_MAGIC, i + 1)) efts.add(i);

        for (int e = 0; e < efts.size(); e++) {
            int inicio = efts.get(e) + 4;
            int fim = e + 1 < efts.size() ? efts.get(e + 1) : b.length;
            byte[] dec = caesar(b, inicio, fim);
            Time t = new Time();
            t.nome = nomeDoTime(dec);

            List<int[]> recs = new ArrayList<>();
            for (int i = 0; i < (fim - inicio) - 5; i++) {
                if (b[inicio + i] != 0x03 || i + 4 >= dec.length) continue;
                if (!minuscula(dec[i + 1]) || !minuscula(dec[i + 2]) || !minuscula(dec[i + 3])) continue;
                int nl = b[inicio + i + 4] & 0xff;
                if (nl <= 0 || nl >= 40) continue;
                recs.add(new int[] {i, nl});
            }
            if (recs.isEmpty()) {
                sf.times.add(t);
                continue;
            }
            t.verbaOff = inicio + recs.get(0)[0] + 0x8E;
            if (t.verbaOff + 4 <= b.length) t.verba = u32(b, t.verbaOff);

            for (int k = 1; k < recs.size(); k++) {
                int off = recs.get(k)[0], nl = recs.get(k)[1], tam = nl + 55;
                if (off + tam > dec.length) continue;
                int base = inicio + off + tam;
                if (base - 25 + 2 > b.length) break;
                Jogador j = new Jogador();
                j.nome = nomeDoJogador(dec, off, nl);
                j.posicao = posicao(b[base - 50]);
                j.estrela = b[base - 49] != 0;
                j.forcaOff = base - 48;
                j.forca = u16(b, j.forcaOff);
                j.comportamento = b[base - 33] & 0xff;
                j.salarioOff = base - 25;
                j.salario = u16(b, j.salarioOff);
                t.jogadores.add(j);
            }
            sf.times.add(t);
        }
        return sf;
    }

    public static void gravar(File f, Save sf) throws IOException {
        byte[] b = sf.bytes.clone();
        for (Time t : sf.times) {
            if (t.verbaOff > 0 && t.verbaOff + 4 <= b.length) {
                long v = Math.max(0, Math.min(0xFFFFFFFFL, t.verba));
                for (int i = 0; i < 4; i++) b[t.verbaOff + i] = (byte) (v >> (8 * i));
            }
            for (Jogador j : t.jogadores) {
                put16(b, j.forcaOff, Math.max(FORCA_MIN, Math.min(FORCA_MAX, j.forca)));
                put16(b, j.salarioOff, Math.max(SALARIO_MIN, Math.min(SALARIO_MAX, j.salario)));
            }
        }
        Files.write(f.toPath(), b);
    }

    private static byte[] caesar(byte[] b, int inicio, int fim) {
        fim = Math.min(fim, b.length);
        if (fim <= inicio) return new byte[0];
        byte[] p = new byte[fim - inicio];
        int delta = 0;
        for (int i = 0; i < p.length; i++) {
            int x = ((b[inicio + i] & 0xff) - delta) & 0xff;
            p[i] = (byte) x;
            delta = (delta + x - 0x20) & 0xff;
        }
        return p;
    }

    private static String nomeDoTime(byte[] dec) {
        for (int pos = 0x28; pos <= 0x32 && pos + 1 < dec.length; pos++) {
            int lb = dec[pos] & 0xff;
            if (lb < 0x22 || lb > 0x50) continue;
            int n = lb - 0x20;
            if (pos + 1 + n > dec.length) continue;
            StringBuilder sb = new StringBuilder();
            int validos = 0;
            for (int i = pos + 1; i < pos + 1 + n; i++) {
                Character c = caractere(dec[i] & 0xff);
                if (c != null) { sb.append(c); validos++; } else sb.append('?');
            }
            if (validos >= n * 3 / 4) return sb.toString().trim().toUpperCase();
        }
        return "?";
    }

    private static String nomeDoJogador(byte[] dec, int rec, int nl) {
        if (dec.length < rec + 5 + nl) return "?";
        StringBuilder sb = new StringBuilder();
        boolean aposEspaco = false;
        for (int i = 0; i < nl; i++) {
            Character c = caractere(dec[rec + 5 + i] & 0xff);
            if (c == null) continue;
            sb.append(i == 0 || aposEspaco ? Character.toUpperCase(c) : c);
            aposEspaco = c == ' ';
        }
        return sb.toString().trim();
    }

    private static Character caractere(int b) {
        if (b >= 0x61 && b <= 0x7A) return (char) b;
        if (b >= 0x81 && b <= 0x9A) return (char) (b - 0x20);
        if (b >= 0xA1 && b <= 0xBA) return (char) (b - 0x40);
        if (b >= 0x50 && b <= 0x59) return (char) (b - 0x20);
        if (b >= 0x30 && b <= 0x39) return (char) b;
        if (b == 0x40) return ' ';
        if (b == 0x4D || b == 0x4B || b == 0x2D) return '-';
        if (b == 0x2E) return '.';
        switch (b) {
            case 0xE1: case 0x01: return 'á';
            case 0xE3: case 0x03: return 'ã';
            case 0xE7: case 0x07: return 'ç';
            case 0xE9: case 0x09: return 'é';
            case 0xED: case 0x0D: return 'í';
            case 0xF3: case 0x13: return 'ó';
            case 0xF4: case 0x14: return 'ô';
            case 0xF5: case 0x15: return 'õ';
            case 0xFA: case 0x1A: return 'ú';
            case 0xF1: case 0x11: return 'ñ';
            default: return null;
        }
    }

    private static String posicao(byte b) {
        switch (b) {
            case 0: return "G";
            case 1: return "D";
            case 2: return "M";
            case 3: return "A";
            default: return "?";
        }
    }

    private static boolean minuscula(byte b) {
        return b >= 'a' && b <= 'z';
    }

    private static int u16(byte[] b, int o) {
        return (b[o] & 0xff) | (b[o + 1] & 0xff) << 8;
    }

    private static long u32(byte[] b, int o) {
        return (b[o] & 0xffL) | (b[o + 1] & 0xffL) << 8 | (b[o + 2] & 0xffL) << 16 | (b[o + 3] & 0xffL) << 24;
    }

    private static void put16(byte[] b, int o, int v) {
        if (o <= 0 || o + 2 > b.length) return;
        b[o] = (byte) v;
        b[o + 1] = (byte) (v >> 8);
    }

    private static int indexOf(byte[] h, byte[] n, int de) {
        fora:
        for (int i = de; i <= h.length - n.length; i++) {
            for (int j = 0; j < n.length; j++) if (h[i + j] != n[j]) continue fora;
            return i;
        }
        return -1;
    }
}
