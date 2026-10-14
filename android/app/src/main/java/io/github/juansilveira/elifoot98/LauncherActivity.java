package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Bundle;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.TextView;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.List;

/** Tela inicial: o mesmo menu do launcher do Windows/Linux/macOS. */
public class LauncherActivity extends Activity {
    private static final int ESCOLHER_PATCH = 1;

    private Jogo jogo;
    private final List<Button> botoes = new ArrayList<>();
    private TextView status;
    private ProgressBar progresso;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        jogo = new Jogo(this);

        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        col.setGravity(Gravity.CENTER_HORIZONTAL);
        int pad = Ui.dp(this, 24);
        col.setPadding(pad, pad, pad, pad);

        col.addView(Ui.logo(this, 260));

        progresso = new ProgressBar(this);
        progresso.setVisibility(View.GONE);
        col.addView(progresso);
        status = new TextView(this);
        status.setTextColor(Color.WHITE);
        status.setGravity(Gravity.CENTER);
        col.addView(status);

        col.addView(botao("Jogar Elifoot 98", v -> abrir()));
        col.addView(botao("Editor de Equipes", v -> startActivity(new Intent(this, TeamEditorActivity.class))));
        col.addView(botao("Editor de Árbitros", v -> startActivity(new Intent(this, RefereeEditorActivity.class))));
        col.addView(botao("Editor de Save", v -> startActivity(new Intent(this, SaveEditorActivity.class))));
        col.addView(botao("Scout", v -> startActivity(new Intent(this, ScoutActivity.class))));
        col.addView(botao("Aplicar Patch", v -> escolherPatch()));
        View espaco = new View(this);
        col.addView(espaco, new LinearLayout.LayoutParams(1, Ui.dp(this, 16)));
        col.addView(botao("Configurações", v -> startActivity(new Intent(this, ConfiguracoesActivity.class))));
        // Versao do app (versionName do build.gradle)
        try {
            TextView versao = new TextView(this);
            versao.setText("v" + getPackageManager().getPackageInfo(getPackageName(), 0).versionName);
            versao.setTextColor(Color.rgb(200, 230, 200));
            versao.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
            versao.setGravity(Gravity.CENTER);
            versao.setPadding(0, Ui.dp(this, 12), 0, 0);
            col.addView(versao);
        } catch (Exception ignorado) {
        }

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(Color.rgb(0, 114, 0));
        scroll.setFillViewport(true);
        scroll.addView(col);
        setContentView(scroll);
        Ui.areaSegura(this, Color.rgb(0, 114, 0));

        preparar();
        Atualizacao.verificar(this, col, 1);   // faixa de versao nova logo abaixo do logo
    }

    // 1a execucao (ou app atualizado): copia o Wine e extrai o jogo (~50 MB)
    private void preparar() {
        habilitar(false);
        progresso.setVisibility(View.VISIBLE);
        status.setText("Preparando os arquivos do jogo...");
        new Thread(() -> {
            String erro = null;
            try {
                jogo.preparar(this);
            } catch (Exception e) {
                erro = e.getMessage();
            }
            String fim = erro;
            runOnUiThread(() -> {
                progresso.setVisibility(View.GONE);
                status.setText(fim == null ? "" : "Erro ao preparar: " + fim);
                habilitar(true);
            });
        }).start();
    }

    // Antes de abrir: o registro escolhido nas Configuracoes vai para o elif98.ini, e a
    // pasta JOGOS fica so com os saves do numero de equipes do jogo
    private void abrir() {
        try {
            jogo.definirRegistro(this, Config.registrado(this));
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Não consegui preparar o registro:\n" + e.getMessage());
            return;
        }
        SavesPorEquipes.separar(jogo.pastaJogos(), Config.equipesDoJogo(this));
        // cartoes amarelos, patrocinadores...: os bytes do EXE conforme as Configuracoes
        try {
            OpcoesDoJogo.aplicar(new java.io.File(jogo.jogo, Config.exeDoJogo(this, jogo.jogo)), OpcoesDoJogo.daConfig(this));
        } catch (java.io.IOException ignored) { }
        startActivity(new Intent(this, ElifootActivity.class)
            .putExtra(ElifootActivity.EXTRA_EXE, Config.exeDoJogo(this, jogo.jogo)));
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
        String nome;
        try {
            nome = Patches.adicionar(this, data.getData(), nomeDoArquivo(data.getData()));
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Não consegui guardar o patch:\n" + e.getMessage());
            return;
        }
        new AlertDialog.Builder(this)
            .setTitle("Aplicar " + nome)
            .setMessage("O patch fica guardado na lista de Times das Configurações, para voltar a ele "
                + "(ou aos times originais) quando quiser.\n\nAplicar agora? Equipes editadas e árbitros "
                + "voltam ao original; os jogos salvos ficam.")
            .setPositiveButton("Aplicar", (d, w) -> aplicarPatch(nome))
            .setNegativeButton("Agora não", null)
            .show();
    }

    private void aplicarPatch(String nome) {
        habilitar(false);
        progresso.setVisibility(View.VISIBLE);
        status.setText("Aplicando " + nome + "...");
        new Thread(() -> {
            String erro = null;
            int n = 0;
            try {
                PatchApplier.Resultado r = Patches.trocar(this, jogo, nome);
                if (r != null) n = r.substituidos;
            } catch (Exception e) {
                erro = e.getMessage();
            }
            String fim = erro;
            int arquivos = n;
            runOnUiThread(() -> {
                progresso.setVisibility(View.GONE);
                status.setText("");
                habilitar(true);
                if (fim == null) Ui.mensagem(this, "Patch aplicado", nome + ": " + arquivos + " arquivos.");
                else Ui.mensagem(this, "Erro", "Erro ao aplicar o patch:\n" + fim);
            });
        }).start();
    }

    private String nomeDoArquivo(Uri uri) {
        try (android.database.Cursor c = getContentResolver().query(uri,
                new String[] {android.provider.OpenableColumns.DISPLAY_NAME}, null, null, null)) {
            if (c != null && c.moveToFirst()) return c.getString(0);
        } catch (Exception ignorado) {
        }
        return uri.getLastPathSegment();
    }

    private Button botao(String texto, View.OnClickListener acao) {
        Button b = Ui.botao(this, texto, acao);
        botoes.add(b);
        return b;
    }

    private void habilitar(boolean sim) {
        for (Button b : botoes) b.setEnabled(sim);
    }
}
