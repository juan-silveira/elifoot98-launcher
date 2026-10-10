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
    // Salario: inteiro de 4 bytes (raw[sz-25..sz-22])
    public static final int SALARIO_MIN = 50, SALARIO_MAX = 9999999;
    public static final String[] COMPORTAMENTOS = {
        "Fair Play", "Cordeirinho", "Cavalheiro", "Caneleiro", "Caceteiro", "Sarrafeiro"
    };
    // Estadio: o jogo so deixa construir ate N = 24 (120.000 lugares; botao "Construir"
    // desativado em N >= 0x18, seg07:29dd). Na leitura aceita ate 40 (saves ja editados).
    public static final int ESTADIO_MAX = 24, ESTADIO_LEITURA_MAX = 40;
    public static final long DINHEIRO_MAX = 999_999_999L;
    // Suspensao sorteada de 1 a 4 jogos, lesao de 1 a 20 (seg03:48be, seg03:4940)
    public static final int SUSPENSAO_MAX = 4, JOGOS_LESIONADO_MAX = 20;
    public static final double INFLACAO_MIN = 0.5, INFLACAO_MAX = 10.0, MORAL_MAX = 2.0;
    private static final byte[] EFT_MAGIC = {'E', 'F', 'a', 0};

    public static final class Jogador {
        public String nome = "", posicao = "G", pais = "";
        public boolean estrela;
        public int comportamento, forca, salario;
        // Nota, Lesao e comportamento: o jogo le do save (docs/elifoot98-interno.md)
        public int nota, lesao, suspensao, jogosLesionado;
        // Historial do jogador (so leitura): t32 jogos, t36 golos na epoca, t40 lesoes, t44 vermelhos
        public int jogos, gols, lesoes, expulsoes;
        int forcaOff, salarioOff, notaOff = -1, lesaoOff = -1, compOff = -1, estrelaOff = -1, suspensaoOff = -1, jogosLesionadoOff = -1;
    }

    // Regra do jogo (seg03:7516): estrela = Nota >= 8 e posicao Meio ou Avancado
    // Ordem da tela do time no jogo: G, D, M, A e, dentro de cada posicao, nome em ordem alfabetica
    public static java.util.List<Jogador> ordemDoJogo(java.util.List<Jogador> jogadores) {
        java.text.Collator col = java.text.Collator.getInstance(new java.util.Locale("pt", "BR"));
        col.setStrength(java.text.Collator.PRIMARY);
        java.util.List<Jogador> l = new java.util.ArrayList<>(jogadores);
        l.sort((a, b) -> {
            int pa = "GDMA".indexOf(a.posicao), pb = "GDMA".indexOf(b.posicao);
            if (pa != pb) return Integer.compare(pa < 0 ? 9 : pa, pb < 0 ? 9 : pb);
            return col.compare(a.nome, b.nome);
        });
        return l;
    }

    public static boolean temEstrela(String posicao, int nota) {
        return nota >= 8 && ("M".equals(posicao) || "A".equals(posicao));
    }

    public static final class Time {
        public String nome = "", pais = "";
        public long verba;
        int verbaOff = -1;
        public int corLetra, corFundo, estadio;   // RGB 0xRRGGBB; capacidade = 5000 x estadio
        public double moral;                       // 0..2
        int coresOff = -1, moralOff = -1, estadioOff = -1;
        public boolean temCores() { return coresOff > 0; }
        public boolean temMoral() { return moralOff > 0; }
        public boolean temEstadio() { return estadioOff > 0; }
        public final List<Jogador> jogadores = new ArrayList<>();
        // Identificador da equipe (2 bytes antes do "EFa") e do seu treinador
        // (2 bytes logo depois do moral; docs/elifoot98-interno.md, Treinadores)
        public int id = -1, tecnicoId = -1;
        int tecnicoOff = -1;
        // Divisao em que joga ("1ª Divisão" ... ou "Distrital"); vazio se nao achou
        public String divisao = "";
        // So equipes das divisoes podem ter treinador humano: no Distrital o jogo
        // da "List index out of bounds" (testado no Android)
        public boolean podeTerHumano() { return !divisao.isEmpty() && !divisao.startsWith("Distrital"); }
    }

    public static final class Tecnico {
        public int id;
        public String nome = "";
        public boolean humano;
    }

    public static final class Save {
        byte[] bytes;
        public int ano;
        public double inflacao;                    // o jogo mostra inflacao x 10
        int inflacaoOff = -1;
        public boolean temInflacao() { return inflacaoOff > 0; }
        public final List<Time> times = new ArrayList<>();
        public final List<Tecnico> tecnicos = new ArrayList<>();
        // Equipe do humano "da vez" (cabecalho, 1 + tamanho + 2): se apontar para
        // uma equipe do computador, o jogo carrega e ja disputa a rodada
        int humanoDaVez = -1, humanoDaVezOff = -1;

        public Time timeDoTecnico(Tecnico tec) {
            for (Time t : times) if (t.tecnicoId == tec.id) return t;
            return null;
        }

        public Tecnico tecnico(int id) {
            for (Tecnico t : tecnicos) if (t.id == id) return t;
            return null;
        }
    }

    private SaveCodec() {}

    public static Save ler(File f) throws IOException {
        byte[] b = Files.readAllBytes(f.toPath());
        Save sf = new Save();
        sf.bytes = b;
        // Texto cifrado de tamanho variavel no inicio; 7 bytes depois: ano e inflacao
        int anoOff = b.length > 0 ? 1 + (b[0] & 0xff) + 7 : 0;
        if (anoOff + 14 <= b.length) {
            int ano = (int) u32(b, anoOff);
            double inf = ext80(b, anoOff + 4);
            if (ano >= 1900 && ano <= 3000 && inf >= 0.3 && inf <= 12) {
                sf.ano = ano;
                sf.inflacao = inf;
                sf.inflacaoOff = anoOff + 4;
            }
        }
        lerTecnicos(b, sf);
        List<Integer> efts = new ArrayList<>();
        for (int i = indexOf(b, EFT_MAGIC, 0); i >= 0; i = indexOf(b, EFT_MAGIC, i + 1)) efts.add(i);

        for (int e = 0; e < efts.size(); e++) {
            int inicio = efts.get(e) + 4;
            int fim = e + 1 < efts.size() ? efts.get(e + 1) : b.length;
            byte[] dec = caesar(b, inicio, fim);
            Time t = new Time();
            t.nome = nomeDoTime(dec);
            if (efts.get(e) >= 2) t.id = u16(b, efts.get(e) - 2);

            // Cores: depois dos dois nomes (textos Pascal) em 0x32
            int o = efts.get(e) + 0x32;
            if (o < fim && (b[o] & 0xff) < 60) {
                o += 1 + (b[o] & 0xff);
                if (o < fim && (b[o] & 0xff) < 60) {
                    o += 1 + (b[o] & 0xff);
                    if (o + 8 <= fim && b[o + 3] == 0 && b[o + 7] == 0) {
                        t.coresOff = o;
                        t.corLetra = rgb(b, o);
                        t.corFundo = rgb(b, o + 4);
                    }
                }
            }
            // Estadio: N 9 bytes antes do EFa seguinte; na ultima equipe, 7 antes
            // da lista "quantidade + identificadores"
            int nOff = e + 1 < efts.size() ? efts.get(e + 1) - 9 : ultimoEstadio(b, efts);
            if (nOff > efts.get(e) && (b[nOff] & 0xff) >= 1 && (b[nOff] & 0xff) <= ESTADIO_LEITURA_MAX) {
                t.estadioOff = nOff;
                t.estadio = b[nOff] & 0xff;
            }

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
            t.pais = paisDoRegistro(dec, recs.get(0)[0]);
            t.verbaOff = inicio + recs.get(0)[0] + 0x8E;
            if (t.verbaOff + 4 <= b.length) t.verba = u32(b, t.verbaOff);
            int moralOff = inicio + recs.get(0)[0] + 74;
            if (moralOff + 6 <= b.length) {
                double m = real48(b, moralOff);
                if (m >= 0 && m <= 10) { t.moral = m; t.moralOff = moralOff; }
            }
            int tecOff = moralOff + 6;
            if (!sf.tecnicos.isEmpty() && tecOff + 2 <= b.length && sf.tecnico(u16(b, tecOff)) != null) {
                t.tecnicoOff = tecOff;
                t.tecnicoId = u16(b, tecOff);
            }

            for (int k = 1; k < recs.size(); k++) {
                int off = recs.get(k)[0], nl = recs.get(k)[1], tam = nl + 55;
                if (off + tam > dec.length) continue;
                int base = inicio + off + tam;
                if (base - 25 + 4 > b.length) break;
                Jogador j = new Jogador();
                j.nome = nomeDoJogador(dec, off, nl);
                j.pais = paisDoRegistro(dec, off);
                j.posicao = posicao(b[base - 50]);
                j.estrelaOff = base - 49;
                j.estrela = b[base - 49] != 0;
                j.forcaOff = base - 48;
                j.forca = u16(b, j.forcaOff);
                j.compOff = base - 33;
                j.comportamento = b[base - 33] & 0xff;
                j.notaOff = base - 27;
                j.nota = (short) u16(b, j.notaOff);
                j.lesaoOff = base - 29;
                j.lesao = (short) u16(b, j.lesaoOff);
                j.suspensaoOff = base - 35;
                j.suspensao = (short) u16(b, j.suspensaoOff);
                j.jogosLesionadoOff = base - 31;
                j.jogosLesionado = (short) u16(b, j.jogosLesionadoOff);
                j.salarioOff = base - 25;
                j.salario = (int) u32(b, j.salarioOff);
                j.jogos = (int) u32(b, base - 18);
                j.gols = (int) u32(b, base - 14);
                j.lesoes = (int) u32(b, base - 10);
                j.expulsoes = (int) u32(b, base - 6);
                t.jogadores.add(j);
            }
            sf.times.add(t);
        }
        lerDivisoes(b, efts, sf);
        return sf;
    }

    // Depois da lista ordenada de equipes: quantidade de divisoes e, para cada uma,
    // 4 bytes, D (2 bytes), nome (string de 20), quantidade e identificadores
    private static void lerDivisoes(byte[] b, List<Integer> efts, Save sf) {
        java.util.Set<Integer> conhecidos = new java.util.HashSet<>();
        for (int st : efts) if (st >= 2) conhecidos.add(u16(b, st - 2));
        java.util.Map<Integer, String> div = null;
        int p = listaOrdenada(b, efts);
        if (p >= 0) div = tabelaDivisoes(b, p + 2 + 2 * u16(b, p), conhecidos);
        // sem a lista das equipes (ou ela nao bate): procura a tabela depois da ultima equipe
        for (int o = efts.isEmpty() ? b.length : efts.get(efts.size() - 1); div == null && o + 2 < b.length; o++)
            div = tabelaDivisoes(b, o, conhecidos);
        if (div == null) return;
        for (Time t : sf.times) {
            String nome = div.get(t.id);
            if (nome != null) t.divisao = nome;
        }
    }

    // Tabela de divisoes em o: quantidade e, de cada uma, 6 bytes, nome (Pascal, ate
    // 20 letras), quantidade de equipes e os ids. Vale so se todos os ids forem de
    // equipes do save, sem repetir, e cobrirem quase todas (null se nao for a tabela).
    private static java.util.Map<Integer, String> tabelaDivisoes(byte[] b, int o, java.util.Set<Integer> conhecidos) {
        if (o + 2 > b.length) return null;
        int nd = u16(b, o);
        o += 2;
        if (nd <= 0 || nd > 50) return null;
        java.util.Map<Integer, String> div = new java.util.HashMap<>();
        for (int d = 0; d < nd; d++) {
            if (o + 4 + 2 + 21 + 2 > b.length) return null;
            int no = o + 6, nl = b[no] & 0xff;
            if (nl == 0 || nl > 20) return null;
            String nome = new String(b, no + 1, nl, java.nio.charset.StandardCharsets.ISO_8859_1);
            int n = u16(b, no + 21);
            o = no + 23;
            if (n > conhecidos.size() || o + 2 * n > b.length) return null;
            for (int k = 0; k < n; k++) {
                int id = u16(b, o + 2 * k);
                if (!conhecidos.contains(id) || div.put(id, nome) != null) return null;
            }
            o += 2 * n;
        }
        return div.size() >= conhecidos.size() - 4 ? div : null;
    }

    // Lista de treinadores: contador em 1 + tamanho do cabecalho + 132; cada um tem
    // id, nome (cada byte = letra + byte anterior), 1 byte humano, 14 bytes,
    // 2 bytes, quantidade do historico e 7 bytes por entrada. Se algo nao fechar,
    // fica sem treinadores (o editor esconde a troca de equipe).
    private static void lerTecnicos(byte[] b, Save sf) {
        if (b.length < 2) return;
        int o = 1 + (b[0] & 0xff) + 132;
        int primeiraEquipe = indexOf(b, EFT_MAGIC, 0);
        if (o + 2 > b.length || primeiraEquipe < 0) return;
        int n = u16(b, o);
        o += 2;
        List<Tecnico> lidos = new ArrayList<>();
        for (int k = 0; k < n; k++) {
            if (o + 3 > primeiraEquipe) return;
            Tecnico t = new Tecnico();
            t.id = u16(b, o);
            int nl = b[o + 2] & 0xff, p = o + 3 + nl;
            if (nl == 0 || nl > 40 || p + 19 > primeiraEquipe) return;
            StringBuilder nome = new StringBuilder();
            int anterior = nl;
            for (int i = o + 3; i < p; i++) {
                int c = ((b[i] & 0xff) - anterior) & 0xff;
                if (c < 0x20) return;
                nome.append((char) c);
                anterior = b[i] & 0xff;
            }
            t.nome = nome.toString();
            if ((b[p] & 0xff) > 1) return;
            t.humano = b[p] == 1;
            o = p + 19 + 7 * u16(b, p + 17);
            lidos.add(t);
        }
        if (o > primeiraEquipe) return;
        sf.tecnicos.addAll(lidos);
        sf.humanoDaVezOff = 1 + (b[0] & 0xff) + 2;
        sf.humanoDaVez = u16(b, sf.humanoDaVezOff);
    }

    /**
     * Poe o treinador na equipe destino como a "chicotada psicologica" do jogo
     * (seg03:4f40): as duas equipes trocam de treinador e ficam com moral 1,0.
     * O humano "da vez" acompanha a troca.
     */
    public static void trocarEquipe(Save sf, Tecnico tec, Time destino) {
        Time origem = sf.timeDoTecnico(tec);
        if (origem == null) throw new IllegalArgumentException(tec.nome + " não treina nenhuma equipe.");
        if (origem == destino) return;
        if (!destino.podeTerHumano())
            throw new IllegalArgumentException(destino.nome + " não está numa divisão (o jogo quebra com treinador humano no Distrital).");
        if (destino.tecnicoOff < 0) throw new IllegalArgumentException("Não achei o treinador de " + destino.nome + ".");
        int outro = destino.tecnicoId;
        destino.tecnicoId = tec.id;
        origem.tecnicoId = outro;
        if (origem.temMoral()) origem.moral = 1.0;
        if (destino.temMoral()) destino.moral = 1.0;
        if (sf.humanoDaVez == origem.id) sf.humanoDaVez = destino.id;
        else if (sf.humanoDaVez == destino.id) sf.humanoDaVez = origem.id;
    }

    public static void gravar(File f, Save sf) throws IOException {
        byte[] b = sf.bytes.clone();
        if (sf.humanoDaVezOff > 0) put16(b, sf.humanoDaVezOff, sf.humanoDaVez);
        // So regrava se mudou: o real de 10 bytes tem mais precisao que double
        if (sf.inflacaoOff > 0 && Math.abs(sf.inflacao - ext80(b, sf.inflacaoOff)) > 1e-9)
            putExt80(b, sf.inflacaoOff, Math.max(INFLACAO_MIN, Math.min(INFLACAO_MAX, sf.inflacao)));
        for (Time t : sf.times) {
            if (t.coresOff > 0) { putRgb(b, t.coresOff, t.corLetra); putRgb(b, t.coresOff + 4, t.corFundo); }
            if (t.moralOff > 0 && Math.abs(t.moral - real48(b, t.moralOff)) > 1e-9)
                putReal48(b, t.moralOff, Math.max(0, Math.min(MORAL_MAX, t.moral)));
            if (t.tecnicoOff > 0) put16(b, t.tecnicoOff, t.tecnicoId);
            if (t.estadioOff > 0) b[t.estadioOff] = (byte) Math.max(1, Math.min(ESTADIO_MAX, t.estadio));
            if (t.verbaOff > 0 && t.verbaOff + 4 <= b.length) {
                long v = Math.max(0, Math.min(DINHEIRO_MAX, t.verba));
                for (int i = 0; i < 4; i++) b[t.verbaOff + i] = (byte) (v >> (8 * i));
            }
            for (Jogador j : t.jogadores) {
                put16(b, j.forcaOff, Math.max(FORCA_MIN, Math.min(FORCA_MAX, j.forca)));
                put32(b, j.salarioOff, Math.max(SALARIO_MIN, Math.min(SALARIO_MAX, j.salario)));
                put16(b, j.notaOff, Math.max(1, Math.min(10, j.nota)));
                if (j.estrelaOff > 0) b[j.estrelaOff] = (byte) (j.estrela ? 1 : 0);
                put16(b, j.lesaoOff, Math.max(0, Math.min(10, j.lesao)));
                put16(b, j.suspensaoOff, Math.max(0, Math.min(SUSPENSAO_MAX, j.suspensao)));
                put16(b, j.jogosLesionadoOff, Math.max(0, Math.min(JOGOS_LESIONADO_MAX, j.jogosLesionado)));
                put16(b, j.compOff, Math.max(0, Math.min(5, j.comportamento)));
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

    // Cada registro comeca pelo pais: 0x03 + 3 letras minusculas
    private static String paisDoRegistro(byte[] dec, int rec) {
        if (rec + 4 > dec.length) return "";
        return new String(dec, rec + 1, 3, java.nio.charset.StandardCharsets.ISO_8859_1).toUpperCase(java.util.Locale.ROOT);
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

    private static int ultimoEstadio(byte[] b, List<Integer> starts) {
        int p = listaOrdenada(b, starts);
        return p > 7 ? p - 7 : -1;
    }

    // Depois da ultima equipe: quantidade + identificadores em ordem crescente
    private static int listaOrdenada(byte[] b, List<Integer> starts) {
        if (starts.isEmpty()) return -1;
        List<Integer> ids = new ArrayList<>();
        for (int st : starts) if (st >= 2) ids.add(u16(b, st - 2));
        java.util.Collections.sort(ids);
        byte[] pat = new byte[2 + 2 * ids.size()];
        pat[0] = (byte) starts.size(); pat[1] = (byte) (starts.size() >> 8);
        for (int i = 0; i < ids.size(); i++) { pat[2 + 2 * i] = (byte) (int) ids.get(i); pat[3 + 2 * i] = (byte) (ids.get(i) >> 8); }
        return indexOf(b, pat, starts.get(starts.size() - 1));
    }

    private static int rgb(byte[] b, int o) {
        return (b[o] & 0xff) << 16 | (b[o + 1] & 0xff) << 8 | (b[o + 2] & 0xff);
    }

    private static void putRgb(byte[] b, int o, int rgb) {
        b[o] = (byte) (rgb >> 16); b[o + 1] = (byte) (rgb >> 8); b[o + 2] = (byte) rgb; b[o + 3] = 0;
    }

    // Real de 6 bytes do Delphi 1: expoente (vies 129), 39 bits de mantissa, sinal no bit 47
    static double real48(byte[] b, int o) {
        if (b[o] == 0) return 0;
        long m = 0;
        for (int i = 5; i >= 1; i--) m = (m << 8) | (b[o + i] & 0xff);
        double sinal = (m & (1L << 39)) != 0 ? -1 : 1;
        m &= (1L << 39) - 1;
        return sinal * (1 + m / (double) (1L << 39)) * Math.pow(2, (b[o] & 0xff) - 129);
    }

    static void putReal48(byte[] b, int o, double v) {
        if (v == 0) { for (int i = 0; i < 6; i++) b[o + i] = 0; return; }
        int e = (int) Math.floor(Math.log(Math.abs(v)) / Math.log(2));
        double mant = Math.abs(v) / Math.pow(2, e);
        if (mant >= 2) { mant /= 2; e++; }
        if (mant < 1) { mant *= 2; e--; }
        long m = Math.round((mant - 1) * (1L << 39));
        if (m >= 1L << 39) { m = 0; e++; }
        if (v < 0) m |= 1L << 39;
        b[o] = (byte) (e + 129);
        for (int i = 1; i <= 5; i++) { b[o + i] = (byte) m; m >>= 8; }
    }

    // Real de 10 bytes (extended): 64 bits de mantissa com o 1 explicito, expoente com vies 16383
    static double ext80(byte[] b, int o) {
        long m = 0;
        for (int i = 7; i >= 0; i--) m = (m << 8) | (b[o + i] & 0xff);
        int ex = u16(b, o + 8);
        if (m == 0) return 0;
        double mant = (m >>> 11) / (double) (1L << 52);   // os 53 bits de cima
        double v = mant * Math.pow(2, (ex & 0x7fff) - 16383);
        return (ex & 0x8000) != 0 ? -v : v;
    }

    static void putExt80(byte[] b, int o, double v) {
        int e = (int) Math.floor(Math.log(Math.abs(v)) / Math.log(2));
        double mant = Math.abs(v) / Math.pow(2, e);
        if (mant >= 2) { mant /= 2; e++; }
        if (mant < 1) { mant *= 2; e--; }
        long m = Math.round(mant * (1L << 52)) << 11;
        for (int i = 0; i < 8; i++) { b[o + i] = (byte) m; m >>>= 8; }
        int ex = (e + 16383) | (v < 0 ? 0x8000 : 0);
        b[o + 8] = (byte) ex; b[o + 9] = (byte) (ex >> 8);
    }

    private static void put32(byte[] b, int o, long v) {
        if (o <= 0 || o + 4 > b.length) return;
        for (int i = 0; i < 4; i++) b[o + i] = (byte) (v >> (8 * i));
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
