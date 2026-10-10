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
        col.addView(botao("Editor de Equipes", v -> startActivity(new Intent(this, TeamEditorActivity.class))));
        col.addView(botao("Editor de Árbitros", v -> startActivity(new Intent(this, RefereeEditorActivity.class))));
        col.addView(botao("Editor de Save", v -> startActivity(new Intent(this, SaveEditorActivity.class))));
        col.addView(botao("Scout", v -> startActivity(new Intent(this, ScoutActivity.class))));
        col.addView(botao("Aplicar Patch", v -> escolherPatch()));
        View espaco = new View(this);
        col.addView(espaco, new LinearLayout.LayoutParams(1, Ui.dp(this, 16)));
        col.addView(botao("Configurações", v -> configuracoes()));
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
        boolean liberado = TeamCodec.bosmanLiberado(jogo.jogo);
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 20);
        l.setPadding(pad, Ui.dp(this, 8), pad, 0);
        TextView t = new TextView(this);
        t.setText("No Android o jogo abre em tela cheia.\n\n"
            + "\"Ativar todos os recursos\" registra o jogo (Registro para autor).\n\n"
            + "Estrangeiros: " + (liberado ? "Liberado (sem limite)" : "Original (até 5 por equipe; Lei Bosman e língua portuguesa não contam)") + ".\n\n"
            + "\"Restaurar times originais\" desfaz equipes editadas e patches aplicados.");
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
        l.addView(t);
        AlertDialog[] d = new AlertDialog[1];
        l.addView(botaoDialogo("Ativar todos os recursos", () -> { d[0].dismiss(); ativar(); }));
        l.addView(botaoDialogo("Estrangeiros…", () -> { d[0].dismiss(); estrangeiros(liberado); }));
        l.addView(botaoDialogo("Restaurar times originais…", () -> { d[0].dismiss(); restaurar(); }));
        ScrollView sv = new ScrollView(this);
        sv.addView(l);
        d[0] = new AlertDialog.Builder(this)
            .setTitle("Configurações")
            .setView(sv)
            .setNegativeButton("Fechar", null)
            .show();
    }

    private Button botaoDialogo(String texto, Runnable acao) {
        Button b = Ui.botao(this, texto, v -> acao.run());
        b.setLayoutParams(new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 48)));
        ((LinearLayout.LayoutParams) b.getLayoutParams()).topMargin = Ui.dp(this, 10);
        return b;
    }

    // Volta EQUIPAS, arbitros, estrangeiros e o que os patches trocaram ao original do
    // app; os saves (JOGOS) ficam
    private void restaurar() {
        new AlertDialog.Builder(this)
            .setTitle("Restaurar times originais")
            .setMessage("Todas as equipes voltam a ser as originais do jogo: equipes editadas ou criadas, "
                + "patches aplicados, árbitros e a regra de estrangeiros são desfeitos.\n\n"
                + "Os jogos salvos (saves) não são apagados.\n\nRestaurar?")
            .setPositiveButton("Restaurar", (dd, w) -> {
                habilitar(false);
                progresso.setVisibility(View.VISIBLE);
                status.setText("Restaurando os arquivos originais...");
                new Thread(() -> {
                    String erro = null;
                    try {
                        jogo.restaurarOriginal(this);
                    } catch (Exception e) {
                        erro = e.getMessage();
                    }
                    String fim = erro;
                    runOnUiThread(() -> {
                        progresso.setVisibility(View.GONE);
                        status.setText("");
                        habilitar(true);
                        if (fim == null) Ui.mensagem(this, "Pronto", "Times originais restaurados.");
                        else Ui.mensagem(this, "Erro", "Não consegui restaurar:\n" + fim);
                    });
                }).start();
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    // Original: BOSMAN.TXE do jogo (18 paises). Liberado: os 217 paises do
    // COUNTRY.TXE, entao ninguem conta como estrangeiro (como fez o Turbo Score)
    private void estrangeiros(boolean liberado) {
        String[] opcoes = {
            "Original — até 5 estrangeiros por equipe (Lei Bosman e língua portuguesa não contam)",
            "Liberado — sem limite de estrangeiros (todos os 217 países na lista Bosman)",
        };
        new AlertDialog.Builder(this)
            .setTitle("Estrangeiros")
            .setSingleChoiceItems(opcoes, liberado ? 1 : 0, (d, w) -> {
                d.dismiss();
                try {
                    TeamCodec.definirBosman(jogo.jogo, w == 1);
                    Ui.mensagem(this, "Pronto", w == 1 ? "Estrangeiros liberados (vale no jogo e no editor)." : "Regra original de estrangeiros restaurada.");
                } catch (Exception e) {
                    Ui.mensagem(this, "Erro", "Não consegui gravar o BOSMAN.TXE:\n" + e.getMessage());
                }
            })
            .setNegativeButton("Cancelar", null)
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
