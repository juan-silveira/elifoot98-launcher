package io.github.juansilveira.elifoot98;

import android.content.Context;
import android.content.SharedPreferences;

import java.io.File;
import java.util.Arrays;
import java.util.Locale;

/**
 * As escolhas das Configuracoes (como o LauncherConfig dos launchers de desktop) e o
 * EXE do jogo que elas pedem.
 */
final class Config {
    private Config() {}

    // Equipes por divisao: o pacote traz um EXE para cada opcao (ELIF08.EXE a ELIF20.EXE,
    // gerados por tools/divisoes/patch_divisoes.py). Um jogo gravado so continua com o
    // mesmo numero de equipes.
    static final int[] OPCOES_EQUIPES = {8, 10, 12, 14, 16, 18, 20};

    // Idioma do jogo: "" = o original (portugues de Portugal); "BR" = portugues do Brasil
    // (ELIFnnBR.EXE e ELIF98BR.EXE, traduzidos por tools/idiomas a partir de game/IDIOMAS/pt-BR.txt)
    static final String[] OPCOES_IDIOMA = {"", "BR"};
    static final String[] NOMES_IDIOMA = {"Original (Portugal)", "Português do Brasil"};

    static SharedPreferences prefs(Context c) {
        return c.getSharedPreferences("config", Context.MODE_PRIVATE);
    }

    static int equipes(Context c) {
        return prefs(c).getInt("equipes", 8);
    }

    static String idioma(Context c) {
        String i = prefs(c).getString("idioma", "");
        return Arrays.asList(OPCOES_IDIOMA).contains(i) ? i : "";
    }

    /** Versao original: o ELIFOOT.EXE (98.003), sem os cartoes, a Nota e as equipes por divisao. */
    static boolean versaoOriginal(Context c) {
        return prefs(c).getBoolean("versaoOriginal", false);
    }

    /** Registrado: o launcher grava a contra-senha (Registro para autor) antes de abrir o jogo. */
    static boolean registrado(Context c) {
        return prefs(c).getBoolean("registrado", false);
    }

    /**
     * Jogadores estrangeiros: TRUE = Liberado, FALSE = Original, null = nunca escolhido
     * (vale o BOSMAN.TXE). Guardado aqui porque uma atualizacao extrai o jogo de novo.
     */
    static Boolean estrangeiros(Context c) {
        String v = prefs(c).getString("estrangeiros", "");
        return v.equals("liberado") ? Boolean.TRUE : v.equals("original") ? Boolean.FALSE : null;
    }

    /** Cartoes amarelos ligados (OpcoesDoJogo acerta o EXE antes de abrir). Padrao: desligado, como o original. */
    static boolean cartoes(Context c) {
        return prefs(c).getBoolean("cartoes", false);
    }

    /** Patrocinadores ligados (OpcoesDoJogo acerta o EXE antes de abrir). Padrao: desligado, como o original. */
    static boolean patrocinios(Context c) {
        return prefs(c).getBoolean("patrocinios", false);
    }

    /** Patch em uso (nome na biblioteca, Patches.java), ou "" para os times originais. */
    static String patch(Context c) {
        return prefs(c).getString("patch", "");
    }

    /** Equipes por divisao do jogo que vai abrir (a versao original so tem 8). */
    static int equipesDoJogo(Context c) {
        return versaoOriginal(c) ? 8 : equipes(c);
    }

    /** O EXE das escolhas; sem o traduzido, o original; senao o ELIFOOT.EXE. */
    static String exeDoJogo(Context c, File jogo) {
        String base = versaoOriginal(c) ? "ELIF98" : String.format(Locale.ROOT, "ELIF%02d", equipes(c));
        String semIdioma = versaoOriginal(c) ? "ELIFOOT.EXE" : base + ".EXE";
        for (String exe : new String[] {base + idioma(c) + ".EXE", semIdioma})
            if (!exe.equals("ELIF98.EXE") && new File(jogo, exe).exists()) return exe;
        return "ELIFOOT.EXE";
    }

    // Minimo de equipes no jogo novo (4N + as que sobem do Distrital)
    static int minimoDeEquipes(int n) {
        return 4 * n + (n >= 16 ? 4 : 2);
    }
}
