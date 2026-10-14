package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.database.Cursor;
import android.graphics.Color;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.text.TextUtils;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.Spinner;
import android.widget.TextView;

import java.util.ArrayList;
import java.util.List;

/**
 * Configuracoes: cada opcao num seletor, e nada muda ate o Salvar. Versao do jogo,
 * equipes por divisao, idioma, registro, estrangeiros e os times (originais ou de um
 * patch da biblioteca, Patches.java).
 */
public class ConfiguracoesActivity extends Activity {
    private static final int ESCOLHER_PATCH = 1;
    private static final int VERDE = 0xFF007200, VERDE_CARTAO = 0xFF145214, AMARELO = 0xFFFCFE04,
        CINZA = 0xFFC8E6C8;

    private Jogo jogo;
    private Spinner versao, equipes, cartoes, patrocinios, idioma, registro, estrangeiros, times;
    // escolhas de cartoes/patrocinadores da versao modificada (na original ficam travadas)
    private int cartoesMod, patrociniosMod;
    private final List<String> patches = new ArrayList<>();
    private Button removerPatch, salvar, cancelar;
    private ProgressBar progresso;
    private TextView status;
    private boolean liberadoAntes;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        jogo = new Jogo(this);

        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 20);
        col.setPadding(pad, pad, pad, pad);

        TextView titulo = texto("Configurações", 24, AMARELO);
        titulo.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        col.addView(titulo);

        List<String> opcoesEquipes = new ArrayList<>();
        for (int n : Config.OPCOES_EQUIPES)
            opcoesEquipes.add(n == 8 ? "8 (original)" : n + " — " + 2 * (n - 1) + " rodadas, mínimo de " + Config.minimoDeEquipes(n) + " equipes");

        versao = seletor(col, "Versão do jogo", new String[] {
            "Modificada — Nota e Risco de lesão, equipes por divisão e as adições abaixo",
            "Original — o Elifoot 98 como era (98.003)"},
            "Na original só há 8 equipes por divisão. Um jogo gravado na modificada mostra números estranhos de lesões e cartões na original.");
        versao.setSelection(Config.versaoOriginal(this) ? 1 : 0);

        equipes = seletor(col, "Equipes por divisão", opcoesEquipes.toArray(new String[0]),
            "Vale para o próximo jogo novo: um jogo gravado só continua com o mesmo número de equipes em que foi começado.");
        equipes.setSelection(Math.max(0, indice(Config.OPCOES_EQUIPES, Config.equipes(this))));

        // adicoes que podem ser desligadas (OpcoesDoJogo acerta o EXE antes de abrir)
        cartoes = seletor(col, "Cartões amarelos", new String[] {
            "Ligados — amarelos, 3 acumulados suspendem", "Desligados — só vermelhos, como no original"}, null);
        cartoes.setSelection(Config.cartoes(this) ? 0 : 1);
        patrocinios = seletor(col, "Patrocinadores", new String[] {
            "Ligados — propostas e contratos de patrocínio master", "Desligados"}, "Valem na versão modificada.");
        patrocinios.setSelection(Config.patrocinios(this) ? 0 : 1);
        cartoesMod = cartoes.getSelectedItemPosition();
        patrociniosMod = patrocinios.getSelectedItemPosition();

        idioma = seletor(col, "Idioma do jogo", Config.NOMES_IDIOMA, null);
        idioma.setSelection(Math.max(0, java.util.Arrays.asList(Config.OPCOES_IDIOMA).indexOf(Config.idioma(this))));

        registro = seletor(col, "Registro", new String[] {
            "Registrado — todos os recursos (Registro para autor)", "Não registrado — como a cópia de avaliação"}, null);
        registro.setSelection(Config.registrado(this) ? 0 : 1);

        liberadoAntes = TeamCodec.bosmanLiberado(jogo.jogo);
        estrangeiros = seletor(col, "Estrangeiros", new String[] {
            "Original — até 5 por equipe (Lei Bosman e língua portuguesa não contam)",
            "Liberado — sem limite de estrangeiros"}, "Vale no jogo e no Editor de Equipes.");
        Boolean escolhido = Config.estrangeiros(this);
        estrangeiros.setSelection((escolhido != null ? escolhido : liberadoAntes) ? 1 : 0);

        times = seletor(col, "Times", new String[0],
            "Trocar os times volta o jogo aos arquivos originais e aplica o patch escolhido: equipes editadas e árbitros são desfeitos. Os jogos salvos ficam.");
        listarPatches(Config.patch(this));
        LinearLayout linha = new LinearLayout(this);
        linha.setOrientation(LinearLayout.HORIZONTAL);
        linha.addView(botao("Adicionar patch…", v -> escolherPatch()), pesado());
        removerPatch = botao("Remover da lista", v -> removerPatch());
        linha.addView(removerPatch, pesado());
        col.addView(linha);
        times.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override public void onItemSelected(AdapterView<?> p, View v, int pos, long id) {
                removerPatch.setEnabled(pos > 0);
                removerPatch.setAlpha(pos > 0 ? 1f : 0.4f);
            }
            @Override public void onNothingSelected(AdapterView<?> p) { }
        });
        versao.setOnItemSelectedListener(new AdapterView.OnItemSelectedListener() {
            @Override public void onItemSelected(AdapterView<?> p, View v, int pos, long id) {
                equipes.setEnabled(pos == 0);
                equipes.setAlpha(pos == 0 ? 1f : 0.5f);
                adicoes(pos == 0);
            }
            @Override public void onNothingSelected(AdapterView<?> p) { }
        });

        col.addView(rotulo("Arquivos do jogo"));
        col.addView(botao("Restaurar os arquivos originais…", v -> restaurar()),
            new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, Ui.dp(this, 48)));
        col.addView(nota("Desfaz equipes editadas ou criadas, patches, árbitros e a regra de estrangeiros. Os jogos salvos ficam."));

        progresso = new ProgressBar(this);
        progresso.setVisibility(View.GONE);
        col.addView(progresso);
        status = texto("", 14, Color.WHITE);
        status.setGravity(Gravity.CENTER);
        col.addView(status);

        LinearLayout fim = new LinearLayout(this);
        fim.setOrientation(LinearLayout.HORIZONTAL);
        fim.setPadding(0, Ui.dp(this, 16), 0, 0);
        salvar = botao("Salvar", v -> salvar());
        cancelar = botao("Cancelar", v -> finish());
        fim.addView(salvar, pesado());
        fim.addView(cancelar, pesado());
        col.addView(fim);

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(VERDE);
        scroll.setFillViewport(true);
        scroll.addView(col);
        setContentView(scroll);
        Ui.areaSegura(this, VERDE);
    }

    // Na versao original as adicoes nao existem: aparecem desligadas e travadas, e a escolha
    // da modificada volta quando ela e selecionada de novo
    private void adicoes(boolean modificada) {
        if (!modificada && cartoes.isEnabled()) {
            cartoesMod = cartoes.getSelectedItemPosition();
            patrociniosMod = patrocinios.getSelectedItemPosition();
        }
        for (Spinner s : new Spinner[] {cartoes, patrocinios}) {
            s.setEnabled(modificada);
            s.setAlpha(modificada ? 1f : 0.5f);
        }
        cartoes.setSelection(modificada ? cartoesMod : 1);
        patrocinios.setSelection(modificada ? patrociniosMod : 1);
    }

    private void salvar() {
        boolean original = versao.getSelectedItemPosition() == 1;
        Config.prefs(this).edit()
            .putBoolean("versaoOriginal", original)
            .putInt("equipes", Config.OPCOES_EQUIPES[equipes.getSelectedItemPosition()])
            .putString("idioma", Config.OPCOES_IDIOMA[idioma.getSelectedItemPosition()])
            .putBoolean("registrado", registro.getSelectedItemPosition() == 0)
            .putBoolean("cartoes", (cartoes.isEnabled() ? cartoes.getSelectedItemPosition() : cartoesMod) == 0)
            .putBoolean("patrocinios", (patrocinios.isEnabled() ? patrocinios.getSelectedItemPosition() : patrociniosMod) == 0)
            .putString("estrangeiros", estrangeiros.getSelectedItemPosition() == 1 ? "liberado" : "original")
            .apply();
        boolean liberado = estrangeiros.getSelectedItemPosition() == 1;
        String patch = times.getSelectedItemPosition() == 0 ? "" : patches.get(times.getSelectedItemPosition() - 1);
        if (!patch.equals(Config.patch(this))) {
            trocarTimes(patch, liberado);
            return;
        }
        if (liberado != liberadoAntes) {
            try {
                TeamCodec.definirBosman(jogo.jogo, liberado);
            } catch (Exception e) {
                Ui.mensagem(this, "Erro", "Não consegui gravar o BOSMAN.TXE:\n" + e.getMessage());
                return;
            }
        }
        finish();
    }

    // Troca os times numa thread (extrai o jogo de novo do app) e depois aplica a regra
    // de estrangeiros escolhida
    private void trocarTimes(String patch, boolean liberado) {
        ocupado(true, patch.isEmpty() ? "Voltando aos times originais..." : "Aplicando " + patch + "...");
        new Thread(() -> {
            String erro = null;
            try {
                Patches.trocar(this, jogo, patch);
                TeamCodec.definirBosman(jogo.jogo, liberado);
            } catch (Exception e) {
                erro = e.getMessage();
            }
            String fim = erro;
            runOnUiThread(() -> {
                ocupado(false, "");
                if (fim != null) Ui.mensagem(this, "Erro", "Não consegui trocar os times:\n" + fim);
                else finish();
            });
        }).start();
    }

    private void restaurar() {
        new AlertDialog.Builder(this)
            .setTitle("Restaurar os arquivos originais")
            .setMessage("Todas as equipes voltam a ser as originais do jogo: equipes editadas ou criadas, "
                + "patches aplicados, árbitros e a regra de estrangeiros são desfeitos.\n\n"
                + "Os jogos salvos (saves) não são apagados.\n\nRestaurar?")
            .setPositiveButton("Restaurar", (d, w) -> {
                ocupado(true, "Restaurando os arquivos originais...");
                new Thread(() -> {
                    String erro = null;
                    try {
                        Patches.trocar(this, jogo, "");
                        TeamCodec.definirBosman(jogo.jogo, false);
                        Config.prefs(this).edit().putString("estrangeiros", "original").apply();
                    } catch (Exception e) {
                        erro = e.getMessage();
                    }
                    String fim = erro;
                    runOnUiThread(() -> {
                        ocupado(false, "");
                        listarPatches("");
                        estrangeiros.setSelection(0);
                        liberadoAntes = false;
                        if (fim == null) Ui.mensagem(this, "Pronto", "Arquivos originais restaurados.");
                        else Ui.mensagem(this, "Erro", "Não consegui restaurar:\n" + fim);
                    });
                }).start();
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    private void escolherPatch() {
        Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        i.addCategory(Intent.CATEGORY_OPENABLE);
        i.setType("application/zip");
        i.putExtra(Intent.EXTRA_MIME_TYPES, new String[] {"application/zip", "application/x-zip-compressed", "application/octet-stream"});
        startActivityForResult(i, ESCOLHER_PATCH);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != ESCOLHER_PATCH || resultCode != RESULT_OK || data == null || data.getData() == null) return;
        try {
            String nome = Patches.adicionar(this, data.getData(), nomeDoArquivo(data.getData()));
            listarPatches(nome);
            Ui.mensagem(this, "Patch na lista", "\"" + nome + "\" está selecionado em Times. Toque em Salvar para aplicar.");
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Não consegui guardar o patch:\n" + e.getMessage());
        }
    }

    private void removerPatch() {
        int i = times.getSelectedItemPosition();
        if (i <= 0) return;
        String nome = patches.get(i - 1);
        new AlertDialog.Builder(this)
            .setTitle("Remover da lista")
            .setMessage("Tirar \"" + nome + "\" da lista de patches?"
                + (nome.equals(Config.patch(this)) ? "\n\nEle está em uso: os times continuam os dele até você escolher outros." : ""))
            .setPositiveButton("Remover", (d, w) -> {
                Patches.remover(this, nome);
                if (nome.equals(Config.patch(this))) Config.prefs(this).edit().putString("patch", "").apply();
                listarPatches("");
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    private void listarPatches(String selecionado) {
        patches.clear();
        patches.addAll(Patches.listar(this));
        List<String> itens = new ArrayList<>();
        itens.add("Originais do jogo");
        for (String p : patches) itens.add(p.equals(Config.patch(this)) ? p + " (em uso)" : p);
        times.setAdapter(adaptador(itens));
        times.setSelection(Math.max(0, patches.indexOf(selecionado) + 1));
    }

    private String nomeDoArquivo(Uri uri) {
        try (Cursor c = getContentResolver().query(uri, new String[] {OpenableColumns.DISPLAY_NAME}, null, null, null)) {
            if (c != null && c.moveToFirst()) return c.getString(0);
        } catch (Exception ignorado) {
        }
        return uri.getLastPathSegment();
    }

    private void ocupado(boolean sim, String texto) {
        progresso.setVisibility(sim ? View.VISIBLE : View.GONE);
        status.setText(texto);
        salvar.setEnabled(!sim);
        cancelar.setEnabled(!sim);
    }

    // ---- aparencia ----

    private Spinner seletor(LinearLayout col, String rotulo, String[] opcoes, String nota) {
        col.addView(rotulo(rotulo));
        Spinner s = new Spinner(this, Spinner.MODE_DROPDOWN);
        s.setBackground(SeletorCor.fundo(0xFFFFFFFF, 0, 6));
        s.setPopupBackgroundDrawable(SeletorCor.fundo(VERDE_CARTAO, AMARELO, 6));
        s.setAdapter(adaptador(java.util.Arrays.asList(opcoes)));
        s.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> { if (r - l > 0) s.setDropDownWidth(r - l); });
        col.addView(s, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, Ui.dp(this, 48)));
        if (nota != null) col.addView(nota(nota));
        return s;
    }

    private ArrayAdapter<String> adaptador(List<String> itens) {
        return new ArrayAdapter<String>(this, android.R.layout.simple_spinner_item, new ArrayList<>(itens)) {
            @Override public View getView(int pos, View v, ViewGroup pai) {
                TextView t = texto(getItem(pos), 15, Color.BLACK);
                t.setSingleLine(true);
                t.setEllipsize(TextUtils.TruncateAt.END);
                t.setGravity(Gravity.CENTER_VERTICAL);
                t.setPadding(Ui.dp(ConfiguracoesActivity.this, 12), 0, Ui.dp(ConfiguracoesActivity.this, 12), 0);
                t.setMinHeight(Ui.dp(ConfiguracoesActivity.this, 48));
                return t;
            }
            @Override public View getDropDownView(int pos, View v, ViewGroup pai) {
                TextView t = texto(getItem(pos), 15, Color.WHITE);
                t.setPadding(Ui.dp(ConfiguracoesActivity.this, 14), Ui.dp(ConfiguracoesActivity.this, 12),
                    Ui.dp(ConfiguracoesActivity.this, 14), Ui.dp(ConfiguracoesActivity.this, 12));
                return t;
            }
        };
    }

    private TextView rotulo(String s) {
        TextView t = texto(s, 16, AMARELO);
        t.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
        t.setPadding(0, Ui.dp(this, 18), 0, Ui.dp(this, 6));
        return t;
    }

    private TextView nota(String s) {
        TextView t = texto(s, 13, CINZA);
        t.setPadding(0, Ui.dp(this, 4), 0, 0);
        return t;
    }

    private TextView texto(String s, int sp, int cor) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        t.setTextColor(cor);
        return t;
    }

    private Button botao(String texto, View.OnClickListener acao) {
        return Ui.botao(this, texto, acao);
    }

    private LinearLayout.LayoutParams pesado() {
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, Ui.dp(this, 48), 1);
        p.setMargins(Ui.dp(this, 4), Ui.dp(this, 8), Ui.dp(this, 4), 0);
        return p;
    }

    private static int indice(int[] a, int v) {
        for (int i = 0; i < a.length; i++) if (a[i] == v) return i;
        return -1;
    }
}
