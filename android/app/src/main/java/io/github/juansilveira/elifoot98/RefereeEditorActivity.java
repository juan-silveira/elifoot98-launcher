package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.os.Bundle;
import android.text.InputFilter;
import android.text.InputType;
import android.widget.ArrayAdapter;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;

import java.io.File;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;

/** Editor de Arbitros (REFEREE.TXE): toque num arbitro pra editar, mover ou remover. */
public class RefereeEditorActivity extends Activity {
    private File arquivo;
    private RefereeCodec.Arquivo dados = new RefereeCodec.Arquivo();
    private ArrayAdapter<String> adapter;
    private final List<String> linhas = new ArrayList<>();
    private TextView status;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setTitle("Editor de Árbitros");
        arquivo = new Jogo(this).refereeTxe();

        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 12);
        col.setPadding(pad, pad, pad, pad);

        LinearLayout barra = new LinearLayout(this);
        barra.addView(Ui.botao(this, "Adicionar", v -> editar(-1)), metade());
        barra.addView(Ui.botao(this, "Salvar arquivo", v -> salvar()), metade());
        barra.addView(Ui.botao(this, "Recarregar original", v -> carregar()), metade());
        col.addView(barra);

        status = new TextView(this);
        col.addView(status);

        ListView lista = new ListView(this);
        adapter = new ArrayAdapter<>(this, android.R.layout.simple_list_item_1, linhas);
        lista.setAdapter(adapter);
        lista.setOnItemClickListener((p, v, pos, id) -> editar(pos));
        col.addView(lista, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(col);
        Ui.areaSegura(this, android.graphics.Color.WHITE);  // tema claro, com a barra de titulo
        carregar();
    }

    private LinearLayout.LayoutParams metade() {
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, Ui.dp(this, 48), 1);
        p.setMargins(Ui.dp(this, 3), 0, Ui.dp(this, 3), 0);
        return p;
    }

    private void carregar() {
        try {
            if (!arquivo.exists()) {
                status.setText("REFEREE.TXE não encontrado. Abra o launcher uma vez antes.");
                return;
            }
            dados = RefereeCodec.ler(arquivo);
            atualizar();
            status.setText("Carregados " + dados.arbitros.size() + " árbitros");
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao ler REFEREE.TXE: " + e.getMessage());
        }
    }

    private void atualizar() {
        linhas.clear();
        for (int i = 0; i < dados.arbitros.size(); i++) {
            RefereeCodec.Arbitro a = dados.arbitros.get(i);
            linhas.add((i + 1) + ".  " + a.pais + "  " + a.nome);
        }
        adapter.notifyDataSetChanged();
    }

    // pos = -1: novo arbitro (entra no fim)
    private void editar(int pos) {
        RefereeCodec.Arbitro atual = pos >= 0 ? dados.arbitros.get(pos) : null;
        EditText pais = new EditText(this);
        pais.setHint("Código país (3 letras)");
        pais.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_CHARACTERS);
        pais.setFilters(new InputFilter[] {new InputFilter.LengthFilter(3), new InputFilter.AllCaps()});
        EditText nome = new EditText(this);
        nome.setHint("Nome");
        if (atual != null) {
            pais.setText(atual.pais);
            nome.setText(atual.nome);
        }
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 20);
        l.setPadding(pad, Ui.dp(this, 8), pad, 0);
        l.addView(pais);
        l.addView(nome);
        if (atual != null) {
            LinearLayout mover = new LinearLayout(this);
            mover.addView(Ui.botao(this, "↑ Subir", v -> mover(pos, -1)), metade());
            mover.addView(Ui.botao(this, "↓ Descer", v -> mover(pos, +1)), metade());
            l.addView(mover);
        }

        AlertDialog.Builder b = new AlertDialog.Builder(this)
            .setTitle(atual == null ? "Adicionar árbitro" : "Árbitro " + (pos + 1))
            .setView(l)
            .setPositiveButton(atual == null ? "Adicionar" : "Atualizar", (d, w) -> {
                String p = pais.getText().toString().trim().toUpperCase();
                String n = nome.getText().toString().trim();
                if (p.length() != 3) { status.setText("Código de país deve ter 3 letras."); return; }
                if (n.isEmpty()) { status.setText("Nome vazio."); return; }
                RefereeCodec.Arbitro a = atual != null ? atual : new RefereeCodec.Arbitro();
                a.pais = p;
                a.nome = n;
                if (atual == null) dados.arbitros.add(a);
                atualizar();
                status.setText(atual == null ? "Adicionado. Total: " + dados.arbitros.size()
                                             : "Atualizado (não salvo em disco ainda)");
            })
            .setNeutralButton("Cancelar", null);
        if (atual != null)
            b.setNegativeButton("Remover", (d, w) -> {
                dados.arbitros.remove(pos);
                atualizar();
                status.setText("Removido. Total: " + dados.arbitros.size());
            });
        dialogo = b.show();
    }

    private AlertDialog dialogo;

    private void mover(int pos, int direcao) {
        int j = pos + direcao;
        if (j < 0 || j >= dados.arbitros.size()) return;
        RefereeCodec.Arbitro t = dados.arbitros.get(pos);
        dados.arbitros.set(pos, dados.arbitros.get(j));
        dados.arbitros.set(j, t);
        atualizar();
        if (dialogo != null) dialogo.dismiss();
        status.setText("Movido (não salvo em disco ainda)");
    }

    private void salvar() {
        try {
            File backup = new File(arquivo.getPath() + ".bak");
            if (!backup.exists()) Files.copy(arquivo.toPath(), backup.toPath());
            RefereeCodec.gravar(dados, arquivo);
            Ui.mensagem(this, "Salvo", "Arquivo REFEREE.TXE salvo com " + dados.arbitros.size() + " árbitros.\n"
                + "Backup do original em: REFEREE.TXE.bak\n\n"
                + "Da próxima vez que abrir o Elifoot, os novos árbitros aparecem.");
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao salvar: " + e.getMessage());
        }
    }
}
