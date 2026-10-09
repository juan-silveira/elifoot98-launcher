package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.text.Editable;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.widget.EditText;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.TextView;

import java.util.Locale;

/** Seletor de cores dos editores: as 16 cores do jogo + R/G/B e hexadecimal. */
final class SeletorCor {
    private SeletorCor() {}

    // As 16 cores que o jogo usa nas equipes
    static final int[] PALETA = {
        0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xC0C0C0,
        0x808080, 0xFF0000, 0x00FF00, 0xFFFF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFFFF,
    };

    interface AoEscolher { void cor(int rgb); }

    static GradientDrawable fundo(int cor, int borda, int raio) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(cor);
        if (borda != 0) g.setStroke(2, borda);
        g.setCornerRadius(raio * 3f);
        return g;
    }

    // 16 cores rapidas do jogo + seletor livre (R/G/B e hexadecimal); o save guarda RGB completo
    static void escolher(Activity a, String titulo, int corAtual, AoEscolher ao) {
        final int[] cor = {corAtual};
        boolean deitado = a.getResources().getDisplayMetrics().widthPixels > a.getResources().getDisplayMetrics().heightPixels;
        LinearLayout caixa = new LinearLayout(a);
        caixa.setOrientation(LinearLayout.VERTICAL);
        LinearLayout direita = caixa;
        LinearLayout dialogo = caixa;
        if (deitado) {
            // Deitado: cores rapidas a esquerda, seletor livre a direita
            dialogo = new LinearLayout(a);
            direita = new LinearLayout(a);
            direita.setOrientation(LinearLayout.VERTICAL);
            direita.setPadding(Ui.dp(a, 20), 0, 0, 0);
            dialogo.addView(caixa);
            dialogo.addView(direita, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        }
        dialogo.setPadding(Ui.dp(a, 16), Ui.dp(a, 8), Ui.dp(a, 16), 0);

        View previaCor = new View(a);
        caixa.addView(previaCor, new LinearLayout.LayoutParams(deitado ? Ui.dp(a, 8 * 36) : LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(a, 40)));
        EditText hex = new EditText(a);
        hex.setSingleLine(true);
        hex.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        hex.setHint("#RRGGBB");
        SeekBar[] barras = new SeekBar[3];
        boolean[] mudando = {false};
        Runnable mostrar = () -> {
            mudando[0] = true;
            previaCor.setBackground(fundo(0xFF000000 | cor[0], Color.GRAY, 4));
            for (int k = 0; k < 3; k++) barras[k].setProgress((cor[0] >> (16 - 8 * k)) & 0xFF);
            String h = String.format(Locale.ROOT, "#%06X", cor[0]);
            if (!hex.getText().toString().equalsIgnoreCase(h)) hex.setText(h);
            mudando[0] = false;
        };

        TextView rapidas = new TextView(a);
        rapidas.setText("Cores do jogo");
        rapidas.setPadding(0, Ui.dp(a, 10), 0, Ui.dp(a, 4));
        caixa.addView(rapidas);
        GridLayout g = new GridLayout(a);
        g.setColumnCount(8);
        for (int c : PALETA) {
            View q = new View(a);
            q.setBackground(fundo(0xFF000000 | c, Color.GRAY, 4));
            q.setOnClickListener(v -> { cor[0] = c; mostrar.run(); });
            GridLayout.LayoutParams p = new GridLayout.LayoutParams();
            p.width = Ui.dp(a, 32);
            p.height = Ui.dp(a, 32);
            p.setMargins(Ui.dp(a, 2), Ui.dp(a, 2), Ui.dp(a, 2), Ui.dp(a, 2));
            g.addView(q, p);
        }
        caixa.addView(g);

        TextView livre = new TextView(a);
        livre.setText("Outra cor");
        livre.setPadding(0, deitado ? 0 : Ui.dp(a, 12), 0, 0);
        direita.addView(livre);
        String[] nomes = {"R", "G", "B"};
        int[] tons = {0xFFE53935, 0xFF43A047, 0xFF1E88E5};
        for (int k = 0; k < 3; k++) {
            final int canal = k;
            LinearLayout l = new LinearLayout(a);
            l.setGravity(Gravity.CENTER_VERTICAL);
            TextView n = new TextView(a);
            n.setText(nomes[k]);
            l.addView(n, new LinearLayout.LayoutParams(Ui.dp(a, 20), LinearLayout.LayoutParams.WRAP_CONTENT));
            SeekBar b = new SeekBar(a);
            b.setMax(255);
            b.setProgressTintList(ColorStateList.valueOf(tons[k]));
            b.setThumbTintList(ColorStateList.valueOf(tons[k]));
            b.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
                @Override public void onProgressChanged(SeekBar s, int v, boolean usuario) {
                    if (mudando[0] || !usuario) return;
                    int desloc = 16 - 8 * canal;
                    cor[0] = (cor[0] & ~(0xFF << desloc)) | (v << desloc);
                    mostrar.run();
                }
                @Override public void onStartTrackingTouch(SeekBar s) {}
                @Override public void onStopTrackingTouch(SeekBar s) {}
            });
            barras[k] = b;
            l.addView(b, new LinearLayout.LayoutParams(0, Ui.dp(a, 36), 1));
            direita.addView(l);
        }
        hex.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence t, int a, int b, int c) {}
            @Override public void onTextChanged(CharSequence t, int a, int b, int c) {}
            @Override public void afterTextChanged(Editable e) {
                if (mudando[0]) return;
                String h = e.toString().trim().replace("#", "");
                if (h.matches("[0-9a-fA-F]{6}")) { cor[0] = Integer.parseInt(h, 16); mostrar.run(); }
            }
        });
        direita.addView(hex);
        mostrar.run();

        ScrollView rolagem = new ScrollView(a);
        rolagem.addView(dialogo);
        new AlertDialog.Builder(a)
            .setTitle(titulo)
            .setView(rolagem)
            .setPositiveButton("OK", (d, w) -> ao.cor(cor[0]))
            .setNegativeButton("Cancelar", null)
            .show();
    }
}
