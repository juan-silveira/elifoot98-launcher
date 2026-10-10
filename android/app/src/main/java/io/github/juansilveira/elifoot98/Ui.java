package io.github.juansilveira.elifoot98;

import android.app.AlertDialog;
import android.content.Context;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.text.InputType;
import android.util.TypedValue;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;

/** Pecas de tela comuns (botoes, mensagens, pedir numero). */
final class Ui {
    private Ui() {}

    /**
     * Android 15+ desenha o app por baixo da barra de status e da de navegacao: o
     * conteudo ganha margem do tamanho delas (e do teclado, que o adjustResize nao
     * empurra mais), e a faixa da barra fica com a cor dada.
     */
    static void areaSegura(android.app.Activity a, int cor) {
        View raiz = a.findViewById(android.R.id.content);
        raiz.setBackgroundColor(cor);
        raiz.setOnApplyWindowInsetsListener((v, in) -> {
            int esq, topo, dir, base;
            if (android.os.Build.VERSION.SDK_INT >= 30) {
                android.graphics.Insets b = in.getInsets(android.view.WindowInsets.Type.systemBars()
                    | android.view.WindowInsets.Type.displayCutout() | android.view.WindowInsets.Type.ime());
                esq = b.left; topo = b.top; dir = b.right; base = b.bottom;
            } else {
                esq = in.getSystemWindowInsetLeft(); topo = in.getSystemWindowInsetTop();
                dir = in.getSystemWindowInsetRight(); base = in.getSystemWindowInsetBottom();
            }
            v.setPadding(esq, topo, dir, base);
            return in;
        });
        raiz.requestApplyInsets();
    }

    static int dp(Context c, int v) {
        return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v, c.getResources().getDisplayMetrics());
    }

    static Button botao(Context c, String texto, View.OnClickListener acao) {
        Button b = new Button(c);
        b.setText(texto);
        b.setAllCaps(false);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
        b.setTextColor(Color.BLACK);
        GradientDrawable fundo = new GradientDrawable();
        fundo.setCornerRadius(dp(c, 6));
        fundo.setColor(Color.rgb(252, 254, 4));
        b.setBackground(fundo);
        b.setOnClickListener(acao);
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(dp(c, 300), dp(c, 48));
        p.setMargins(0, dp(c, 6), 0, dp(c, 6));
        b.setLayoutParams(p);
        return b;
    }

    static void mensagem(Context c, String titulo, String texto) {
        new AlertDialog.Builder(c).setTitle(titulo).setMessage(texto).setPositiveButton("OK", null).show();
    }

    interface AoConfirmar {
        void ok(int valor);
    }

    /** Pede um inteiro entre min e max. */
    static void pedirInteiro(Context c, String titulo, String texto, int atual, int min, int max, AoConfirmar ao) {
        EditText campo = new EditText(c);
        campo.setInputType(InputType.TYPE_CLASS_NUMBER);
        campo.setText(Integer.toString(atual));
        campo.setSelectAllOnFocus(true);
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.VERTICAL);
        int pad = dp(c, 20);
        l.setPadding(pad, dp(c, 8), pad, 0);
        TextView t = new TextView(c);
        t.setText(texto);
        l.addView(t);
        l.addView(campo);
        new AlertDialog.Builder(c).setTitle(titulo).setView(l)
            .setPositiveButton("OK", (d, w) -> {
                int v;
                try {
                    v = Integer.parseInt(campo.getText().toString().trim());
                } catch (NumberFormatException e) {
                    mensagem(c, "Erro", "Valor inválido.");
                    return;
                }
                if (v < min || v > max) {
                    mensagem(c, "Fora dos limites", "Valor deve estar entre " + min + " e " + max + ".");
                    return;
                }
                ao.ok(v);
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    /** Logo "Elifoot 98" (recortado da janela Acerca do jogo), com a largura dada em dp */
    static android.widget.ImageView logo(android.content.Context ctx, int larguraDp) {
        android.widget.ImageView v = new android.widget.ImageView(ctx);
        v.setImageResource(R.drawable.logo);
        v.setAdjustViewBounds(true);
        v.setContentDescription("Elifoot 98");
        android.widget.LinearLayout.LayoutParams p = new android.widget.LinearLayout.LayoutParams(
            dp(ctx, larguraDp), android.widget.LinearLayout.LayoutParams.WRAP_CONTENT);
        p.gravity = android.view.Gravity.CENTER_HORIZONTAL;
        p.bottomMargin = dp(ctx, 16);
        v.setLayoutParams(p);
        return v;
    }
}
