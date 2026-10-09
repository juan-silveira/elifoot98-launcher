package io.github.juansilveira.elifoot98;

import java.util.Locale;

// Score do jogador a partir dos atributos que vem do nome (nota, lesao, comportamento, estrela).
// Mesma conta de src/Score.cs; o metodo esta em docs/score.md.
public final class Score {
    private Score() { }

    // Jogos perdidos por temporada (14 jogos) por ponto de lesao e de comportamento, por posicao G/D/M/A
    private static final double[] FORA_POR_LESAO = {0.105, 0.13, 0.13, 0.13};
    private static final double[] FORA_POR_COMPORTAMENTO = {0.17, 0.21, 0.21, 0.21};
    private static final int JOGOS_TEMPORADA = 14;

    public static int posicao(String pos) {
        switch (pos) { case "G": return 0; case "D": return 1; case "M": return 2; case "A": return 3; default: return -1; }
    }

    public static double jogosFora(int pos, int lesao, int comportamento) {
        return pos < 0 ? 0 : FORA_POR_LESAO[pos] * Math.max(0, lesao) + FORA_POR_COMPORTAMENTO[pos] * Math.max(0, comportamento);
    }

    // Score (0 a 1000), uma escala so para todas as posicoes: Nota x 100, descontando a parte da
    // temporada que ele deve perder por lesao e suspensao. Nao depende da forca.
    public static int rendimento(String posicao, int nota, int lesao, int comportamento) {
        int p = posicao(posicao);
        if (p < 0) return 0;
        double disp = Math.max(0, 1 - jogosFora(p, lesao, comportamento) / JOGOS_TEMPORADA);
        return (int) Math.round(100 * Math.max(0, Math.min(10, nota)) * disp);
    }

    public static final String AJUDA =
            "Score (0 a 1000): Nota × 100, descontando os jogos que ele deve perder na temporada por lesão e por suspensão " +
            "(cerca de 0,13 jogo por ponto de Lesão e 0,21 por ponto de Comportamento, em 14 jogos). Não depende da força.\n\n" +
            "✱ Estrela: Nota 8 ou mais em meia ou atacante. Soma 2 à força do setor dele enquanto está em campo.\n\n" +
            "Nota (1 a 10): decide quem marca os gols do time (atacante pesa 9, meia 4, defesa 1; goleiro nunca marca) e quem bate os pênaltis. " +
            "Não muda quantos gols o time faz: isso vem da força dos setores.\n\n" +
            "Lesão (0 a 10): chance de ser ele o lesionado quando o time sofre uma lesão. 0 nunca se lesiona. Cada lesão: 1 a 20 jogos fora e 10 de força a menos.\n\n" +
            "Comport.: chance de ser ele o expulso. Fair Play nunca é expulso. Suspensão de 1 a 4 jogos; goleiro expulso dá pênalti certo, defesa dá em 1 de 3.\n\n" +
            "Impacto: pontos por temporada (14 jogos) que o time ganha ou perde com ele, comparado a um jogador da mesma posição sem estrela, " +
            "Lesão 0 e Fair Play (medido em simulações). O jogo sorteia por minuto se o time terá uma lesão ou expulsão; Lesão e Comportamento " +
            "só escolhem quem sofre. Por isso um atacante violento ou um defesa frágil podem até ajudar: atraem os problemas que iriam para o goleiro ou para as estrelas.\n\n" +
            "Jogos, Gols, Lesões, Expuls.: o histórico do jogador no save (gols desta temporada).\n\n" +
            "Sit.: S = suspenso por N jogos, L = lesionado por N jogos; Estrang. = conta como estrangeiro (Bosman e PLOP não contam).";

    // Impacto no time: pontos por temporada contra um jogador de referencia da mesma posicao
    public static double impacto(String posicao, int nota, int lesao, int comportamento) {
        int p = posicao(posicao);
        boolean estrela = p >= 2 && nota >= 8;
        switch (p) {
            case 0: return -0.016 * lesao - 0.065 * comportamento;
            case 1: return 0.02 * lesao;
            case 2: return (estrela ? 0.26 : 0) + 0.019 * comportamento;
            case 3: return (estrela ? 0.27 : 0) - 0.013 * lesao + 0.021 * comportamento;
            default: return 0;
        }
    }

    public static String textoImpacto(double v) {
        return (v > 0.004 ? "+" : v < -0.004 ? "−" : "") + String.format(new Locale("pt", "BR"), "%.2f", Math.abs(v));
    }
}
