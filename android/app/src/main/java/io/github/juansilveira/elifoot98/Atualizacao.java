package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * Aviso de versao nova: le elifoot98.web.app/versao.json em segundo plano e, se a
 * versao de la for maior que a instalada, poe uma faixa no launcher com "Baixar"
 * (abre o APK no navegador) e "Agora nao". Sem internet ou com erro, nao mostra nada.
 */
final class Atualizacao {
    static final String URL_VERSAO = "https://elifoot98.web.app/versao.json";

    private Atualizacao() {}

    static void verificar(Activity a, LinearLayout col, int posicao) {
        String instalada;
        try {
            instalada = a.getPackageManager().getPackageInfo(a.getPackageName(), 0).versionName;
        } catch (Exception e) {
            return;
        }
        new Thread(() -> {
            try {
                HttpURLConnection c = (HttpURLConnection) new URL(URL_VERSAO).openConnection();
                c.setConnectTimeout(5000);
                c.setReadTimeout(5000);
                c.setUseCaches(false);
                ByteArrayOutputStream b = new ByteArrayOutputStream();
                try (InputStream in = c.getInputStream()) {
                    byte[] buf = new byte[8192];
                    for (int n; (n = in.read(buf)) > 0; ) b.write(buf, 0, n);
                } finally {
                    c.disconnect();
                }
                JSONObject j = new JSONObject(b.toString("UTF-8"));
                String nova = j.getString("versao");
                if (comparar(nova, instalada) <= 0) return;
                String novidades = j.optString("novidades", "");
                JSONObject links = j.optJSONObject("links");
                String link = links != null ? links.optString("android", "") : "";
                if (link.isEmpty()) link = j.optString("pagina", "https://elifoot98.web.app/launcher");
                final String destino = link;
                a.runOnUiThread(() -> mostrar(a, col, posicao, nova, novidades, destino));
            } catch (Exception ignorado) {
                // offline ou arquivo com problema: segue sem aviso
            }
        }).start();
    }

    // Compara "0.9.10" com "0.9.9" numero a numero
    static int comparar(String x, String y) {
        String[] a = x.trim().split("\\."), b = y.trim().split("\\.");
        for (int i = 0; i < Math.max(a.length, b.length); i++) {
            int p = i < a.length ? numero(a[i]) : 0, q = i < b.length ? numero(b[i]) : 0;
            if (p != q) return Integer.compare(p, q);
        }
        return 0;
    }

    private static int numero(String s) {
        try { return Integer.parseInt(s.replaceAll("[^0-9].*$", "")); } catch (Exception e) { return 0; }
    }

    private static void mostrar(Activity a, LinearLayout col, int posicao, String nova, String novidades, String link) {
        if (a.isFinishing()) return;
        LinearLayout faixa = new LinearLayout(a);
        faixa.setOrientation(LinearLayout.VERTICAL);
        int p = Ui.dp(a, 12);
        faixa.setPadding(p, p, p, p);
        GradientDrawable g = new GradientDrawable();
        g.setColor(Color.rgb(0, 70, 0));
        g.setStroke(Ui.dp(a, 2), Color.rgb(252, 254, 4));
        g.setCornerRadius(Ui.dp(a, 8));
        faixa.setBackground(g);

        TextView t = new TextView(a);
        t.setText("Nova versão " + nova + " disponível");
        t.setTextColor(Color.rgb(252, 254, 4));
        t.setTypeface(Typeface.DEFAULT_BOLD);
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
        t.setGravity(Gravity.CENTER);
        faixa.addView(t);
        if (!novidades.isEmpty()) {
            TextView n = new TextView(a);
            n.setText(novidades.length() > 160 ? novidades.substring(0, 157) + "…" : novidades);
            n.setTextColor(Color.WHITE);
            n.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
            n.setGravity(Gravity.CENTER);
            n.setPadding(0, Ui.dp(a, 4), 0, Ui.dp(a, 4));
            faixa.addView(n);
        }
        LinearLayout botoes = new LinearLayout(a);
        Button baixar = Ui.botao(a, "Baixar", v -> {
            try { a.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(link))); } catch (Exception ignorado) { }
        });
        Button depois = Ui.botao(a, "Agora não", v -> col.removeView(faixa));
        LinearLayout.LayoutParams pb = new LinearLayout.LayoutParams(0, Ui.dp(a, 44), 1);
        botoes.addView(baixar, pb);
        LinearLayout.LayoutParams pd = new LinearLayout.LayoutParams(0, Ui.dp(a, 44), 1);
        pd.leftMargin = Ui.dp(a, 8);
        botoes.addView(depois, pd);
        faixa.addView(botoes);

        LinearLayout.LayoutParams pf = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        pf.topMargin = Ui.dp(a, 8);
        pf.bottomMargin = Ui.dp(a, 8);
        col.addView(faixa, Math.min(posicao, col.getChildCount()), pf);
    }
}
