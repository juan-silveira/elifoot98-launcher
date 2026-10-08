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

        col.addView(botao("Jogar Elifoot 98", v -> abrir("ELIFOOT.EXE")));
        col.addView(botao("Editor de Equipes", v -> abrir("EDITEQ.EXE")));
        col.addView(botao("Editor de Árbitros", v -> startActivity(new Intent(this, RefereeEditorActivity.class))));
        col.addView(botao("Editor de Save", v -> startActivity(new Intent(this, SaveEditorActivity.class))));
        col.addView(botao("Aplicar Patch", v -> escolherPatch()));
        View espaco = new View(this);
        col.addView(espaco, new LinearLayout.LayoutParams(1, Ui.dp(this, 16)));
        col.addView(botao("Configurações", v -> configuracoes()));

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(Color.rgb(0, 114, 0));
        scroll.setFillViewport(true);
        scroll.addView(col);
        setContentView(scroll);

        preparar();
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

    private void abrir(String exe) {
        startActivity(new Intent(this, ElifootActivity.class).putExtra(ElifootActivity.EXTRA_EXE, exe));
    }

    private void configuracoes() {
        new AlertDialog.Builder(this)
            .setTitle("Configurações")
            .setMessage("No Android o jogo abre em tela cheia.\n\n"
                + "\"Ativar todos os recursos\" registra o jogo (Registro para autor).")
            .setPositiveButton("Ativar todos os recursos", (d, w) -> ativar())
            .setNegativeButton("Fechar", null)
            .show();
    }

    private void ativar() {
        try {
            jogo.ativar(this);
            Ui.mensagem(this, "Pronto", "Recursos ativados! Abra o jogo para usar.");
        } catch (Exception e) {
            Ui.mensagem(this, "Não foi possível ativar", e.getMessage());
        }
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
        Uri uri = data.getData();
        new AlertDialog.Builder(this)
            .setTitle("Confirmar patch")
            .setMessage("Todos os arquivos serão substituídos EXCETO a pasta EQUIPAS "
                + "(será renomeada pra EQUIPAS_OLD antes) e JOGOS (nunca tocada).\n\nContinuar?")
            .setPositiveButton("Sim", (d, w) -> aplicarPatch(uri))
            .setNegativeButton("Não", null)
            .show();
    }

    private void aplicarPatch(Uri uri) {
        File tmp = new File(getCacheDir(), "patch.zip");
        try (InputStream in = getContentResolver().openInputStream(uri); OutputStream out = new FileOutputStream(tmp)) {
            byte[] buf = new byte[1 << 16];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
            PatchApplier.Resultado r = PatchApplier.aplicar(tmp, jogo.jogo);
            String msg = "Patch aplicado com sucesso!\n\n" + r.substituidos + " arquivos substituídos.";
            if (r.backupEquipas != null) msg += "\nEQUIPAS antiga preservada em: " + r.backupEquipas;
            if (r.ignorados > 0) msg += "\n\n" + r.ignorados + " entradas em JOGOS/ foram ignoradas (saves preservados).";
            Ui.mensagem(this, "Patch aplicado", msg);
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao aplicar patch:\n" + e.getMessage());
        } finally {
            tmp.delete();
        }
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
