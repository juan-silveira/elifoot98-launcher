package io.github.juansilveira.elifoot98;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Arquivos de equipe (.EFT), listas .TXE (paises, Lei Bosman, PLOP) e as regras
 * do jogo para uma equipe (mesmo codigo do src/TeamCodec.cs). Detalhes em
 * docs/elifoot98-interno.md, secao 7.
 */
final class TeamCodec {
    private TeamCodec() {}

    // Regras de seg12:275e (mensagens iguais as do Editor de Equipas)
    static final int MIN_JOGADORES = 14, MAX_JOGADORES = 20, MIN_CAMPO = 10, MAX_ESTRANGEIROS = 5;

    /** No arquivo os jogadores vem agrupados G, D, M, A: o novo entra no fim do grupo
     *  da posicao dele (nao no fim da lista). Devolve o indice. */
    static int inserirNaPosicao(List<Jogador> lista, Jogador j) {
        int i = 0;
        for (int k = 0; k < lista.size(); k++) if (lista.get(k).posicao <= j.posicao) i = k + 1;
        lista.add(i, j);
        return i;
    }
    static final int NIVEL_MIN = 1, NIVEL_MAX = 20;
    // Maiores tamanhos das 282 equipes originais
    static final int MAX_NOME_COMPLETO = 40, MAX_NOME = 20;
    static final String[] POSICOES = {"Guarda-redes", "Defesa", "Médio", "Avançado"};
    static final String[] POSICOES_CURTAS = {"G", "D", "M", "A"};
    private static final byte[] MARCA = {'E', 'F', 'a', 0};

    static final class Jogador {
        String pais = "", nome = "";
        int posicao;                              // 0 G, 1 D, 2 M, 3 A

        // Atributos que o jogo tira do nome (seg03:7516, secao 2 do documento)
        int soma() { return somaNome(nome); }
        int nota() { return soma() % 10 + 1; }
        int lesao() { return soma() % 11; }
        int comportamento() { return TeamCodec.comportamento(soma(), posicao); }
        boolean estrela() { return nota() >= 8 && (posicao == 2 || posicao == 3); }

        Jogador copia() {
            Jogador j = new Jogador();
            j.pais = pais; j.nome = nome; j.posicao = posicao;
            return j;
        }
    }

    static final class Equipe {
        File arquivo;
        String nomeCompleto = "", nomeAbreviado = "", pais = "", treinador = "";
        int corLetra, corFundo = 0xFFFFFF, nivel = 10;
        final List<Jogador> jogadores = new ArrayList<>();
    }

    static final class Pais {
        final String codigo, nome;
        Pais(String codigo, String nome) { this.codigo = codigo; this.nome = nome; }
        @Override public String toString() { return nome; }
    }

    // ---- .EFT ----

    static Equipe ler(File f) throws IOException {
        byte[] b = Files.readAllBytes(f.toPath());
        if (b.length < 0x32 || b[0] != 'E' || b[1] != 'F' || b[2] != 'a' || b[3] != 0)
            throw new IOException("Não é um arquivo de equipe do Elifoot 98.");
        Equipe t = new Equipe();
        t.arquivo = f;
        int[] o = {0x32};
        t.nomeCompleto = texto(b, o);
        t.nomeAbreviado = texto(b, o);
        t.corLetra = rgb(b, o[0]);
        t.corFundo = rgb(b, o[0] + 4);
        o[0] += 8;
        t.pais = texto(b, o);
        t.nivel = b[o[0]] & 0xff;
        int n = u16(b, o[0] + 1);
        o[0] += 3;
        for (int k = 0; k < n; k++) {
            Jogador j = new Jogador();
            j.pais = texto(b, o);
            j.nome = texto(b, o);
            j.posicao = u16(b, o[0]);
            o[0] += 2;
            t.jogadores.add(j);
        }
        t.treinador = texto(b, o);
        return t;
    }

    static void gravar(Equipe t, File f) throws IOException {
        ByteArrayOutputStream s = new ByteArrayOutputStream();
        s.write(MARCA, 0, 4);
        s.write(new byte[0x32 - 4], 0, 0x32 - 4);
        porTexto(s, t.nomeCompleto);
        porTexto(s, t.nomeAbreviado);
        porRgb(s, t.corLetra);
        porRgb(s, t.corFundo);
        porTexto(s, t.pais);
        s.write(t.nivel);
        s.write(t.jogadores.size() & 0xff);
        s.write(t.jogadores.size() >> 8);
        for (Jogador j : t.jogadores) {
            porTexto(s, j.pais);
            porTexto(s, j.nome);
            s.write(j.posicao & 0xff);
            s.write(j.posicao >> 8);
        }
        porTexto(s, t.treinador);
        Files.write(f.toPath(), s.toByteArray());
    }

    // Cada string e cifrada sozinha: tamanho em claro e cada letra somada ao
    // byte cifrado anterior (o primeiro soma o tamanho)
    private static String texto(byte[] b, int[] o) {
        int n = b[o[0]] & 0xff, anterior = n;
        byte[] p = new byte[n];
        for (int i = 0; i < n; i++) {
            int c = b[o[0] + 1 + i] & 0xff;
            p[i] = (byte) (c - anterior);
            anterior = c;
        }
        o[0] += 1 + n;
        return new String(p, StandardCharsets.ISO_8859_1);
    }

    private static void porTexto(ByteArrayOutputStream s, String texto) throws IOException {
        byte[] p = texto.getBytes(StandardCharsets.ISO_8859_1);
        if (p.length > 255) throw new IOException("Texto longo demais: " + texto);
        s.write(p.length);
        int anterior = p.length;
        for (byte c : p) {
            anterior = ((c & 0xff) + anterior) & 0xff;
            s.write(anterior);
        }
    }

    private static int rgb(byte[] b, int o) { return (b[o] & 0xff) << 16 | (b[o + 1] & 0xff) << 8 | (b[o + 2] & 0xff); }

    private static void porRgb(ByteArrayOutputStream s, int rgb) {
        s.write(rgb >> 16 & 0xff);
        s.write(rgb >> 8 & 0xff);
        s.write(rgb & 0xff);
        s.write(0);
    }

    private static int u16(byte[] b, int o) { return (b[o] & 0xff) | (b[o + 1] & 0xff) << 8; }

    // ---- atributos pelo nome ----

    // S = soma dos codigos Latin-1 do nome com a 1a letra maiuscula
    static int somaNome(String nome) {
        if (nome == null || nome.isEmpty()) return 0;
        byte[] p = nome.getBytes(StandardCharsets.ISO_8859_1);
        if (p[0] >= 'a' && p[0] <= 'z') p[0] -= 0x20;
        int s = 0;
        for (byte c : p) s += c & 0xff;
        return s;
    }

    static int comportamento(int soma, int posicao) {
        int c = 5 - (int) Math.floor(Math.sqrt(soma % 36));
        return posicao == 1 ? (c + 2) % 6 : c;
    }

    // ---- listas .TXE (cifra do REFEREE.TXE) ----

    static List<String> lerTxe(File f) throws IOException {
        byte[] b = Files.readAllBytes(f.toPath());
        List<String> lista = new ArrayList<>();
        int o = 0;
        while (o < b.length) {
            int n = b[o] & 0xff;
            if (o + 1 + n > b.length) break;
            byte[] p = new byte[n];
            for (int i = 0; i < n; i++) {
                int k = (i + 2) * n;
                for (int j = 0; j < i; j++) k += (i + 1 - j) * (p[j] & 0xff);
                p[i] = (byte) ((b[o + 1 + i] & 0xff) - k);
            }
            lista.add(new String(p, StandardCharsets.ISO_8859_1));
            o += 1 + n;
        }
        return lista;
    }

    static void gravarTxe(File f, List<String> lista) throws IOException {
        ByteArrayOutputStream s = new ByteArrayOutputStream();
        for (String texto : lista) {
            byte[] p = texto.getBytes(StandardCharsets.ISO_8859_1);
            int n = p.length;
            s.write(n);
            for (int i = 0; i < n; i++) {
                int k = (i + 2) * n;
                for (int j = 0; j < i; j++) k += (i + 1 - j) * (p[j] & 0xff);
                s.write(((p[i] & 0xff) + k) & 0xff);
            }
        }
        Files.write(f.toPath(), s.toByteArray());
    }

    // Arquivo do jogo sem ligar para maiusculas: o Wine e o Editor de Equipas
    // regravam alguns em minusculas (country.txe)
    static File caminho(File dir, String... partes) {
        File atual = dir;
        for (String parte : partes) {
            File exato = new File(atual, parte);
            if (!exato.exists() && atual.isDirectory()) {
                File[] l = atual.listFiles();
                if (l != null) for (File f : l) if (f.getName().equalsIgnoreCase(parte)) { exato = f; break; }
            }
            atual = exato;
        }
        return atual;
    }

    // COUNTRY.TXE: 1o registro "Country", depois "BRA Brasil"
    static List<Pais> lerPaises(File jogo) throws IOException {
        List<Pais> lista = new ArrayList<>();
        List<String> r = lerTxe(caminho(jogo, "COUNTRY.TXE"));
        for (int i = 1; i < r.size(); i++) {
            String s = r.get(i);
            if (s.length() > 4 && s.charAt(3) == ' ') lista.add(new Pais(s.substring(0, 3), s.substring(4)));
        }
        return lista;
    }

    // ---- Lei Bosman / PLOP ----

    // Lista original do BOSMAN.TXE (regravada fica igual byte a byte)
    private static final String[] BOSMAN_ORIGINAL =
        {"POR", "ESP", "FRA", "ITA", "ALE", "AUT", "GRE", "ING", "ESC", "WAL", "ILN", "IRL", "DIN", "BEL", "HOL", "LUX", "FIN", "SUE"};

    private static File bosman(File jogo) { return caminho(jogo, "CTRGROUP", "BOSMAN.TXE"); }

    // "Liberado": o BOSMAN.TXE tem todos os paises do COUNTRY.TXE, entao
    // ninguem conta como estrangeiro (no editor e no jogo)
    static boolean bosmanLiberado(File jogo) {
        try {
            Set<String> b = new HashSet<>(lerTxe(bosman(jogo)));
            for (Pais p : lerPaises(jogo)) if (!b.contains(p.codigo)) return false;
            return true;
        } catch (IOException e) {
            return false;
        }
    }

    static void definirBosman(File jogo, boolean liberado) throws IOException {
        List<String> lista = new ArrayList<>();
        lista.add("Bosman");
        if (liberado) for (Pais p : lerPaises(jogo)) lista.add(p.codigo);
        else lista.addAll(Arrays.asList(BOSMAN_ORIGINAL));
        lista.add("");
        gravarTxe(bosman(jogo), lista);
    }

    /** Regras da equipe com as listas Bosman e PLOP lidas do jogo. */
    static final class Regras {
        private final Set<String> bosman, plop, paises = new HashSet<>();

        Regras(File jogo, List<Pais> lista) {
            bosman = codigos(caminho(jogo, "CTRGROUP", "BOSMAN.TXE"));
            plop = codigos(caminho(jogo, "CTRGROUP", "PLOP.TXE"));
            for (Pais p : lista) paises.add(p.codigo);
        }

        private static Set<String> codigos(File f) {
            Set<String> s = new HashSet<>();
            try {
                List<String> r = lerTxe(f);
                for (int i = 1; i < r.size(); i++) if (r.get(i).length() == 3) s.add(r.get(i));
            } catch (IOException ignorado) { }
            return s;
        }

        // seg12:0a2f: "" nacional; "Bosman"/"PLOP" nao conta como estrangeiro
        // (os dois paises na mesma lista); "Estrangeiro"
        String situacao(String paisJogador, String paisEquipe) {
            if (paisJogador.equals(paisEquipe)) return "";
            if (bosman.contains(paisJogador) && bosman.contains(paisEquipe)) return "Bosman";
            if (plop.contains(paisJogador) && plop.contains(paisEquipe)) return "PLOP";
            return "Estrangeiro";
        }

        int estrangeiros(Equipe t) {
            int n = 0;
            for (Jogador j : t.jogadores) if (situacao(j.pais, t.pais).equals("Estrangeiro")) n++;
            return n;
        }

        boolean paisValido(String codigo) { return paises.contains(codigo); }

        // Motivos para o jogo nao aceitar a equipe (seg12:275e); vazio = pronta
        List<String> validar(Equipe t) {
            List<String> erros = new ArrayList<>();
            if (t.nomeCompleto.trim().isEmpty() || t.nomeAbreviado.trim().isEmpty())
                erros.add("Não está definido o nome da equipa");
            if (!paises.contains(t.pais)) erros.add("Não está definido o país da equipa");
            if (t.nivel < NIVEL_MIN || t.nivel > NIVEL_MAX) erros.add("Não está definido o nível da equipa");
            int gr = 0, campo = 0;
            for (Jogador j : t.jogadores) if (j.posicao == 0) gr++; else if (j.posicao <= 3) campo++;
            if (t.jogadores.size() < MIN_JOGADORES) erros.add("Não tem jogadores suficientes (mínimo " + MIN_JOGADORES + ")");
            if (t.jogadores.size() > MAX_JOGADORES) erros.add("Tem demasiados jogadores (máximo " + MAX_JOGADORES + ")");
            if (gr == 0) erros.add("Não tem guarda-redes");
            if (campo < MIN_CAMPO) erros.add("Não tem jogadores de campo suficientes (mínimo " + MIN_CAMPO + ")");
            if (estrangeiros(t) > MAX_ESTRANGEIROS)
                erros.add("Tem demasiados jogadores estrangeiros (máximo " + MAX_ESTRANGEIROS + ")");
            if (t.treinador.trim().isEmpty()) erros.add("Não está definido o treinador");
            return erros;
        }
    }
}
