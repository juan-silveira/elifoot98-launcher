package io.github.juansilveira.elifoot98;

import java.io.File;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Um save so abre no EXE com o mesmo numero de equipes por divisao em que foi
 * comecado (com outro, o jogo da "List index out of bounds."). Antes de abrir o
 * jogo, a pasta JOGOS fica so com os saves do numero escolhido; os outros vao
 * para JOGOS/&lt;N&gt; equipes e voltam quando esse numero for escolhido de novo.
 * O jogo so lista os *.e98 da propria pasta JOGOS. Igual ao src/SavesPorEquipes.cs.
 */
final class SavesPorEquipes {
    private SavesPorEquipes() {}

    static final class Item {
        final File arquivo;
        final int equipes;   // por divisao; 0 se nao deu para ler

        Item(File arquivo, int equipes) {
            this.arquivo = arquivo;
            this.equipes = equipes;
        }

        String rotulo() {
            String nome = arquivo.getName();
            return nome.substring(0, nome.length() - 4) + (equipes > 0 ? " — " + equipes + " equipes" : "");
        }
    }

    static File pasta(File jogos, int n) {
        return new File(jogos, n + " equipes");
    }

    // Equipes por divisao de um save (as da 1a divisao); 0 se nao der para ler
    static int equipesDoSave(File f) {
        try {
            int n = 0;
            for (SaveCodec.Time t : SaveCodec.ler(f).times) if (t.divisao.startsWith("1")) n++;
            return n;
        } catch (Exception e) {
            return 0;
        }
    }

    private static File[] saves(File pasta) {
        File[] fs = pasta.listFiles((d, n) -> n.toLowerCase(Locale.ROOT).endsWith(".e98"));
        return fs != null ? fs : new File[0];
    }

    // Saves da pasta JOGOS e das pastas "<N> equipes", para os editores mostrarem
    // de que jogo e cada save
    static List<Item> listar(File jogos) {
        List<Item> lista = new ArrayList<>();
        List<File> pastas = new ArrayList<>();
        pastas.add(jogos);
        File[] subs = jogos.listFiles(f -> f.isDirectory() && f.getName().endsWith(" equipes"));
        if (subs != null) for (File s : subs) pastas.add(s);
        for (File p : pastas)
            for (File f : saves(p)) lista.add(new Item(f, equipesDoSave(f)));
        lista.sort((a, b) -> {
            int c = a.arquivo.getName().compareToIgnoreCase(b.arquivo.getName());
            return c != 0 ? c : Integer.compare(a.equipes, b.equipes);
        });
        return lista;
    }

    static void separar(File jogos, int n) {
        if (!jogos.isDirectory()) return;
        for (File f : saves(jogos)) {
            int m = equipesDoSave(f);
            if (m <= 0 || m == n) continue;
            mover(f, pasta(jogos, m));
        }
        File minha = pasta(jogos, n);
        if (!minha.isDirectory()) return;
        for (File f : saves(minha)) mover(f, jogos);
        String[] resto = minha.list();
        if (resto == null || resto.length == 0) minha.delete();
    }

    // Nunca sobrescreve: se ja existe um save com o mesmo nome, acrescenta um numero
    private static void mover(File arquivo, File pasta) {
        pasta.mkdirs();
        String nome = arquivo.getName().substring(0, arquivo.getName().length() - 4);
        String ext = arquivo.getName().substring(arquivo.getName().length() - 4);
        File destino = new File(pasta, nome + ext);
        for (int i = 2; destino.exists(); i++) destino = new File(pasta, nome + i + ext);
        arquivo.renameTo(destino);
    }
}
