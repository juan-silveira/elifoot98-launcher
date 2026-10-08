package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.os.Bundle;
import android.text.InputType;
import android.view.View;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.Spinner;
import android.widget.TextView;

import java.io.File;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;

/** Editor de Save (.e98): verba do clube e forca/salario dos jogadores. */
public class SaveEditorActivity extends Activity {
    private File pasta;
    private final List<String> saves = new ArrayList<>();
    private final List<SaveCodec.Time> times = new ArrayList<>();
    private final List<String> linhasJogadores = new ArrayList<>();
    private ArrayAdapter<String> adapterSaves, adapterTimes, adapterJogadores;
    private Spinner spSave, spTime;
    private EditText verba;
    private SaveCodec.Save atual;
    private SaveCodec.Time timeAtual;
    private File arquivoAtual;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setTitle("Editor de Save");
        pasta = new Jogo(this).pastaJogos();

        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 12);
        col.setPadding(pad, pad, pad, pad);

        spSave = new Spinner(this);
        adapterSaves = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, saves);
        spSave.setAdapter(adapterSaves);
        col.addView(rotulo("Save:"));
        col.addView(spSave);

        spTime = new Spinner(this);
        adapterTimes = new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, new ArrayList<>());
        spTime.setAdapter(adapterTimes);
        col.addView(rotulo("Time:"));
        col.addView(spTime);

        verba = new EditText(this);
        verba.setInputType(InputType.TYPE_CLASS_NUMBER);
        col.addView(rotulo("Verba do clube:"));
        col.addView(verba);

        col.addView(rotulo("Jogadores (toque pra editar força e salário):"));
        ListView lista = new ListView(this);
        adapterJogadores = new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, linhasJogadores);
        lista.setAdapter(adapterJogadores);
        lista.setOnItemClickListener((p, v, pos, id) -> editarJogador(pos));
        col.addView(lista, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));

        col.addView(Ui.botao(this, "Salvar", v -> salvar()));
        TextView nota = new TextView(this);
        nota.setText("Backup .bak criado na primeira gravação. Força >50 emite aviso (jogo aceita até 9999).");
        col.addView(nota);
        setContentView(col);

        spSave.setOnItemSelectedListener(new Selecao(this::carregarSave));
        spTime.setOnItemSelectedListener(new Selecao(this::carregarTime));
        listarSaves();
    }

    private TextView rotulo(String t) {
        TextView v = new TextView(this);
        v.setText(t);
        v.setPadding(0, Ui.dp(this, 8), 0, 0);
        return v;
    }

    private void listarSaves() {
        saves.clear();
        String[] nomes = pasta.list((d, n) -> n.toLowerCase(Locale.ROOT).endsWith(".e98"));
        if (nomes != null) {
            Arrays.sort(nomes);
            saves.addAll(Arrays.asList(nomes));
        }
        adapterSaves.notifyDataSetChanged();
        if (saves.isEmpty()) Ui.mensagem(this, "Sem saves", "Nenhum jogo gravado em JOGOS ainda.");
    }

    private void carregarSave(int pos) {
        if (pos < 0 || pos >= saves.size()) return;
        arquivoAtual = new File(pasta, saves.get(pos));
        try {
            atual = SaveCodec.ler(arquivoAtual);
            times.clear();
            times.addAll(atual.times);
            times.sort((a, b) -> a.nome.compareToIgnoreCase(b.nome));
            adapterTimes.clear();
            for (SaveCodec.Time t : times) adapterTimes.add(String.format(Locale.ROOT, "%s — %,d", t.nome, t.verba));
            adapterTimes.notifyDataSetChanged();
            if (!times.isEmpty()) { spTime.setSelection(0); carregarTime(0); }
        } catch (Exception e) {
            atual = null;
            Ui.mensagem(this, "Erro", "Falha ao ler save:\n" + e.getMessage());
        }
    }

    private void carregarTime(int pos) {
        if (pos < 0 || pos >= times.size()) return;
        SaveCodec.Time t = times.get(pos);
        if (t != timeAtual) {
            timeAtual = t;
            verba.setText(Long.toString(t.verba));
        }
        linhasJogadores.clear();
        int n = 1;
        for (SaveCodec.Jogador j : t.jogadores) {
            String comp = j.comportamento >= 0 && j.comportamento < SaveCodec.COMPORTAMENTOS.length
                ? SaveCodec.COMPORTAMENTOS[j.comportamento] : "?";
            linhasJogadores.add(String.format(Locale.ROOT, "%d. %s %s%s — força %d, salário %d, %s",
                n++, j.posicao, j.nome, j.estrela ? "*" : "", j.forca, j.salario, comp));
        }
        adapterJogadores.notifyDataSetChanged();
    }

    private void editarJogador(int pos) {
        if (timeAtual == null || pos >= timeAtual.jogadores.size()) return;
        SaveCodec.Jogador j = timeAtual.jogadores.get(pos);
        Ui.pedirInteiro(this, "Força", "Nova força para " + j.nome + "\n(normal 1-50, jogo aceita até 9999):",
            j.forca, SaveCodec.FORCA_MIN, SaveCodec.FORCA_MAX, forca -> {
                j.forca = forca;
                if (forca > SaveCodec.FORCA_AVISO_ACIMA)
                    Ui.mensagem(this, "Aviso", "Força " + forca + " é bem acima do normal (1-" + SaveCodec.FORCA_AVISO_ACIMA + ").");
                Ui.pedirInteiro(this, "Salário", "Novo salário para " + j.nome + ":",
                    j.salario, SaveCodec.SALARIO_MIN, SaveCodec.SALARIO_MAX, salario -> {
                        j.salario = salario;
                        carregarTime(times.indexOf(timeAtual));
                    });
                carregarTime(times.indexOf(timeAtual));
            });
    }

    private void salvar() {
        if (atual == null || timeAtual == null || arquivoAtual == null) return;
        long v;
        try {
            v = Long.parseLong(verba.getText().toString().trim());
            if (v < 0) throw new NumberFormatException();
        } catch (NumberFormatException e) {
            Ui.mensagem(this, "Valor inválido", "Verba deve ser número inteiro positivo.");
            return;
        }
        timeAtual.verba = v;
        try {
            File bak = new File(arquivoAtual.getPath() + ".bak");
            if (!bak.exists()) Files.copy(arquivoAtual.toPath(), bak.toPath());
            SaveCodec.gravar(arquivoAtual, atual);
            Ui.mensagem(this, "OK", "Save gravado com sucesso.");
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao gravar:\n" + e.getMessage());
        }
    }

    private static final class Selecao implements AdapterView.OnItemSelectedListener {
        interface Acao { void em(int pos); }
        private final Acao acao;
        Selecao(Acao a) { acao = a; }
        @Override public void onItemSelected(AdapterView<?> p, View v, int pos, long id) { acao.em(pos); }
        @Override public void onNothingSelected(AdapterView<?> p) {}
    }
}
