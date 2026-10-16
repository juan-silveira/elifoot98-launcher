package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.TextView;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.security.MessageDigest;
import java.net.HttpURLConnection;
import java.net.URL;

/**
 * Aviso de versao nova: le elifoot98.web.app/versao.json em segundo plano e, se a
 * versao de la for maior que a instalada, poe uma faixa no launcher com "Atualizar" e
 * "Agora nao". Sem internet ou com erro, nao mostra nada.
 *
 * "Atualizar" baixa o APK (links.android; confere sha256.android se houver) para
 * cache/atualizacao e abre o instalador do sistema (ApkProvider). Se o Android ainda nao
 * deixa o launcher instalar apps, abre essa permissao; o toque seguinte instala.
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
                JSONObject hashes = j.optJSONObject("sha256");
                String sha = hashes != null ? hashes.optString("android", "").trim().toLowerCase() : "";
                a.runOnUiThread(() -> mostrar(a, col, posicao, nova, novidades, link, sha));
            } catch (Exception ignorado) {
                // offline ou arquivo com problema: segue sem aviso
            }
        }).start();
    }

    static File pasta(Context c) {
        File d = new File(c.getCacheDir(), "atualizacao");
        d.mkdirs();
        return d;
    }

    // Baixa (se ainda nao baixou esta versao) e abre o instalador
    private static void atualizar(Activity a, Button botao, String nova, String link, String sha) {
        File apk = new File(pasta(a), "Elifoot98-" + nova.replaceAll("[^0-9.]", "") + ".apk");
        if (apk.isFile()) {
            instalar(a, botao, apk);
            return;
        }
        botao.setEnabled(false);
        new Thread(() -> {
            String erro = null;
            try {
                for (File velho : pasta(a).listFiles()) velho.delete();
                baixar(link, apk, sha, pct -> a.runOnUiThread(() -> botao.setText(pct >= 0 ? pct + "%" : "Baixando…")));
            } catch (Exception e) {
                apk.delete();
                erro = e.getMessage();
            }
            final String falha = erro;
            a.runOnUiThread(() -> {
                botao.setEnabled(true);
                botao.setText("Atualizar");
                if (falha != null) {
                    new android.app.AlertDialog.Builder(a).setTitle("Atualizar")
                        .setMessage("Não consegui baixar a versão nova (" + falha + ").\n\nAbrir a página de download?")
                        .setPositiveButton("Abrir", (d, w) -> {
                            try { a.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(link))); } catch (Exception ignorado) { }
                        })
                        .setNegativeButton("Agora não", null).show();
                } else {
                    instalar(a, botao, apk);
                }
            });
        }).start();
    }

    private static void instalar(Activity a, Button botao, File apk) {
        if (Build.VERSION.SDK_INT >= 26 && !a.getPackageManager().canRequestPackageInstalls()) {
            botao.setText("Instalar");
            new android.app.AlertDialog.Builder(a).setTitle("Atualizar")
                .setMessage("Para instalar a versão nova, permita que o Elifoot 98 instale apps na próxima tela e volte aqui para tocar em Instalar.")
                .setPositiveButton("Permitir", (d, w) -> {
                    try {
                        a.startActivity(new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                            Uri.parse("package:" + a.getPackageName())));
                    } catch (Exception ignorado) { }
                })
                .setNegativeButton("Agora não", null).show();
            return;
        }
        Intent i = new Intent(Intent.ACTION_VIEW);
        i.setDataAndType(ApkProvider.uri(a, apk), "application/vnd.android.package-archive");
        i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
        try {
            a.startActivity(i);
        } catch (Exception e) {
            botao.setText("Atualizar");
        }
    }

    interface Progresso { void em(int pct); }

    private static void baixar(String link, File destino, String sha, Progresso p) throws Exception {
        File parte = new File(destino.getPath() + ".parte");
        MessageDigest md = MessageDigest.getInstance("SHA-256");
        HttpURLConnection c = (HttpURLConnection) new URL(link).openConnection();
        c.setConnectTimeout(15000);
        c.setReadTimeout(30000);
        c.setInstanceFollowRedirects(true);
        try {
            if (c.getResponseCode() / 100 != 2) throw new IOException("HTTP " + c.getResponseCode());
            long total = c.getContentLengthLong(), lidos = 0;
            int ultimo = -2;
            try (InputStream in = c.getInputStream(); FileOutputStream out = new FileOutputStream(parte)) {
                byte[] buf = new byte[65536];
                for (int n; (n = in.read(buf)) > 0; ) {
                    out.write(buf, 0, n);
                    md.update(buf, 0, n);
                    lidos += n;
                    int pct = total > 0 ? (int) (lidos * 100 / total) : -1;
                    if (pct != ultimo) { ultimo = pct; p.em(pct); }
                }
            }
            if (total > 0 && lidos != total) throw new IOException("download incompleto");
        } finally {
            c.disconnect();
        }
        StringBuilder h = new StringBuilder();
        for (byte x : md.digest()) h.append(String.format("%02x", x));
        if (!sha.isEmpty() && !sha.equals(h.toString())) {
            parte.delete();
            throw new IOException("o arquivo baixado não confere");
        }
        if (!parte.renameTo(destino)) throw new IOException("não consegui gravar o arquivo");
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

    private static void mostrar(Activity a, LinearLayout col, int posicao, String nova, String novidades, String link, String sha) {
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
        Button baixar = Ui.botao(a, link.isEmpty() ? "Baixar" : "Atualizar", null);
        baixar.setOnClickListener(v -> {
            if (link.isEmpty()) {
                try { a.startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse("https://elifoot98.web.app/launcher"))); } catch (Exception ignorado) { }
                return;
            }
            atualizar(a, baixar, nova, link, sha);
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
