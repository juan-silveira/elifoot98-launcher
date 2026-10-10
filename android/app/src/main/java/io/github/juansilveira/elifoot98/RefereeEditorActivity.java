package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Bundle;
import android.text.Editable;
import android.text.TextWatcher;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;

import java.io.File;
import java.nio.file.Files;
import java.text.Normalizer;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Editor de Arbitros (REFEREE.TXE), no visual verde dos outros editores: toque num
 * arbitro pra editar, mover ou remover. "Original" volta a lista que veio com o
 * jogo (so grava ao Salvar).
 */
public class RefereeEditorActivity extends Activity {
    private static final int VERDE = 0xFF0B3D0B, VERDE_TOPO = 0xFF062606, VERDE_CARTAO = 0xFF145214, VERDE_LINHA = 0xFF114A11,
        AMARELO = 0xFFFCFE04, BRANCO = 0xFFFFFFFF, CINZA = 0xFFB8C9B8;

    private File arquivo;
    private RefereeCodec.Arquivo dados = new RefereeCodec.Arquivo();
    private BaseAdapter adapter;
    private TextView status;
    private Button salvar;
    private boolean alterado;
    // Os paises do jogo (COUNTRY.TXE): o pais do arbitro e escolhido da lista
    private List<TeamCodec.Pais> paises = new ArrayList<>();
    private final Map<String, String> nomePais = new HashMap<>();
    private final Map<String, Bitmap> bandeiras = new HashMap<>();
    private File jogo;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        arquivo = new Jogo(this).refereeTxe();
        jogo = new Jogo(this).jogo;
        try {
            paises = TeamCodec.lerPaises(jogo);
            paises.sort((a, b) -> semAcento(a.nome).compareTo(semAcento(b.nome)));
            for (TeamCodec.Pais p : paises) nomePais.put(p.codigo, p.nome);
        } catch (Exception ignorado) {
        }

        LinearLayout raiz = new LinearLayout(this);
        raiz.setOrientation(LinearLayout.VERTICAL);
        raiz.setBackgroundColor(VERDE);

        // Barra do topo: titulo, Original e Salvar
        LinearLayout barra = new LinearLayout(this);
        barra.setGravity(Gravity.CENTER_VERTICAL);
        barra.setBackgroundColor(VERDE_TOPO);
        barra.setPadding(dp(16), dp(8), dp(12), dp(8));
        barra.addView(texto("Árbitros", 20, AMARELO, true), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        Button original = botao("Original", false);
        original.setOnClickListener(v -> voltarOriginal());
        salvar = botao("Salvar", true);
        salvar.setOnClickListener(v -> salvar());
        for (Button b : new Button[] {original, salvar}) {
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(40));
            p.leftMargin = dp(8);
            barra.addView(b, p);
        }
        raiz.addView(barra);

        // Cartao com a lista
        LinearLayout cartao = new LinearLayout(this);
        cartao.setOrientation(LinearLayout.VERTICAL);
        cartao.setBackground(SeletorCor.fundo(VERDE_CARTAO, 0, 10));
        cartao.setPadding(dp(12), dp(10), dp(12), dp(10));
        LinearLayout topoCartao = new LinearLayout(this);
        topoCartao.setGravity(Gravity.CENTER_VERTICAL);
        status = texto("", 13, CINZA, false);
        topoCartao.addView(status, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        Button adicionar = botao("Adicionar", false);
        adicionar.setOnClickListener(v -> editar(-1));
        topoCartao.addView(adicionar, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(38)));
        cartao.addView(topoCartao);

        ListView lista = new ListView(this);
        lista.setDivider(null);
        lista.setSelector(new android.graphics.drawable.ColorDrawable(0x332E7D32));
        adapter = new BaseAdapter() {
            @Override public int getCount() { return dados.arbitros.size(); }
            @Override public Object getItem(int i) { return dados.arbitros.get(i); }
            @Override public long getItemId(int i) { return i; }

            @Override
            public View getView(int i, View v, ViewGroup pai) {
                RefereeCodec.Arbitro a = dados.arbitros.get(i);
                LinearLayout l = new LinearLayout(RefereeEditorActivity.this);
                l.setGravity(Gravity.CENTER_VERTICAL);
                l.setPadding(dp(10), 0, dp(10), 0);
                l.setMinimumHeight(dp(44));
                l.setBackgroundColor(i % 2 == 0 ? VERDE_LINHA : VERDE_CARTAO);
                TextView n = texto((i + 1) + ".", 13, CINZA, false);
                l.addView(n, new LinearLayout.LayoutParams(dp(40), ViewGroup.LayoutParams.WRAP_CONTENT));
                l.addView(imagemBandeira(a.pais, 24));
                TextView p = texto(a.pais, 14, AMARELO, true);
                p.setPadding(dp(8), 0, 0, 0);
                l.addView(p, new LinearLayout.LayoutParams(dp(56), ViewGroup.LayoutParams.WRAP_CONTENT));
                l.addView(texto(a.nome, 15, BRANCO, false), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
                return l;
            }
        };
        lista.setAdapter(adapter);
        lista.setOnItemClickListener((pai, v, pos, id) -> editar(pos));
        LinearLayout.LayoutParams pl = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1);
        pl.topMargin = dp(8);
        cartao.addView(lista, pl);

        LinearLayout.LayoutParams pc = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1);
        pc.setMargins(dp(10), dp(10), dp(10), dp(10));
        raiz.addView(cartao, pc);
        setContentView(raiz);
        Ui.areaSegura(this, VERDE_TOPO);
        carregar();
    }

    private int dp(int v) { return Ui.dp(this, v); }

    private TextView texto(String t, int sp, int cor, boolean negrito) {
        TextView v = new TextView(this);
        v.setText(t);
        v.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        v.setTextColor(cor);
        if (negrito) v.setTypeface(Typeface.DEFAULT_BOLD);
        return v;
    }

    private Button botao(String t, boolean amarelo) {
        Button b = new Button(this);
        b.setText(t);
        b.setAllCaps(false);
        b.setTextColor(Color.BLACK);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        if (amarelo) b.setTypeface(Typeface.DEFAULT_BOLD);
        b.setMinWidth(0);
        b.setMinimumWidth(0);
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        b.setPadding(dp(14), 0, dp(14), 0);
        b.setStateListAnimator(null);
        b.setBackground(SeletorCor.fundo(amarelo ? AMARELO : 0xFFE6E6E6, 0, 6));
        return b;
    }

    private void alterou(boolean sim) {
        alterado = sim;
        salvar.setText(sim ? "Salvar •" : "Salvar");
        status.setText(dados.arbitros.size() + " árbitros" + (sim ? " · alterações não salvas" : ""));
        adapter.notifyDataSetChanged();
    }

    private void carregar() {
        try {
            if (!arquivo.exists()) {
                status.setText("REFEREE.TXE não encontrado. Abra o launcher uma vez antes.");
                return;
            }
            dados = RefereeCodec.ler(arquivo);
            alterou(false);
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao ler REFEREE.TXE: " + e.getMessage());
        }
    }

    // A lista que veio com o jogo (elifoot.zip do app); so vai pro disco ao Salvar
    private void voltarOriginal() {
        new AlertDialog.Builder(this)
            .setTitle("Árbitros originais")
            .setMessage("Voltar a lista de árbitros original do jogo? As mudanças só são gravadas quando você tocar em Salvar.")
            .setPositiveButton("Voltar ao original", (d, w) -> {
                try {
                    byte[] b = Jogo.arquivoOriginal(this, "REFEREE.TXE");
                    if (b == null) throw new java.io.IOException("REFEREE.TXE não está no pacote do app.");
                    File tmp = new File(getCacheDir(), "REFEREE.TXE");
                    Files.write(tmp.toPath(), b);
                    dados = RefereeCodec.ler(tmp);
                    tmp.delete();
                    alterou(true);
                } catch (Exception e) {
                    Ui.mensagem(this, "Erro", "Não consegui ler o original: " + e.getMessage());
                }
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    // pos = -1: novo arbitro (entra no fim)
    private void editar(int pos) {
        RefereeCodec.Arbitro atual = pos >= 0 ? dados.arbitros.get(pos) : null;
        String[] codigo = {atual != null ? atual.pais : ""};
        LinearLayout pais = new LinearLayout(this);
        pais.setGravity(Gravity.CENTER_VERTICAL);
        pais.setBackground(SeletorCor.fundo(0xFFFFFFFF, 0, 6));
        pais.setPadding(dp(10), 0, dp(10), 0);
        Runnable mostrarPais = () -> {
            pais.removeAllViews();
            if (!codigo[0].isEmpty()) pais.addView(imagemBandeira(codigo[0], 24));
            String n = nomePais.get(codigo[0]);
            TextView t = texto(codigo[0].isEmpty() ? "Escolher o país" : codigo[0] + (n != null ? "  " + n : ""), 16, Color.BLACK, false);
            t.setPadding(dp(10), 0, 0, 0);
            t.setSingleLine(true);
            t.setEllipsize(android.text.TextUtils.TruncateAt.END);
            pais.addView(t, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
            pais.addView(texto("▾", 16, Color.BLACK, false));
        };
        mostrarPais.run();
        pais.setOnClickListener(v -> escolherPais(c -> { codigo[0] = c; mostrarPais.run(); }));
        EditText nome = new EditText(this);
        nome.setHint("Nome");
        if (atual != null) nome.setText(atual.nome);
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        l.setPadding(dp(20), dp(8), dp(20), 0);
        l.addView(pais, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(48)));
        l.addView(nome);
        if (atual != null) {
            LinearLayout mover = new LinearLayout(this);
            mover.setPadding(0, dp(8), 0, 0);
            Button subir = botao("↑ Subir", false), descer = botao("↓ Descer", false);
            subir.setOnClickListener(v -> mover(pos, -1));
            descer.setOnClickListener(v -> mover(pos, +1));
            LinearLayout.LayoutParams p1 = new LinearLayout.LayoutParams(0, dp(42), 1), p2 = new LinearLayout.LayoutParams(0, dp(42), 1);
            p2.leftMargin = dp(8);
            mover.addView(subir, p1);
            mover.addView(descer, p2);
            l.addView(mover);
        }

        AlertDialog.Builder b = new AlertDialog.Builder(this)
            .setTitle(atual == null ? "Adicionar árbitro" : "Árbitro " + (pos + 1))
            .setView(l)
            .setPositiveButton(atual == null ? "Adicionar" : "Atualizar", (d, w) -> {
                String p = codigo[0];
                String n = nome.getText().toString().trim();
                if (p.length() != 3) { Ui.mensagem(this, "Árbitro", "Escolha o país do árbitro."); return; }
                if (n.isEmpty()) { Ui.mensagem(this, "Árbitro", "Digite o nome."); return; }
                RefereeCodec.Arbitro a = atual != null ? atual : new RefereeCodec.Arbitro();
                a.pais = p;
                a.nome = n;
                if (atual == null) dados.arbitros.add(a);
                alterou(true);
            })
            .setNeutralButton("Cancelar", null);
        if (atual != null)
            b.setNegativeButton("Remover", (d, w) -> {
                dados.arbitros.remove(pos);
                alterou(true);
            });
        dialogo = b.show();
    }

    private AlertDialog dialogo;

    interface AoEscolherPais { void codigo(String c); }

    // Lista dos paises do jogo com bandeira, busca por nome ou codigo
    private void escolherPais(AoEscolherPais ao) {
        LinearLayout caixa = new LinearLayout(this);
        caixa.setOrientation(LinearLayout.VERTICAL);
        caixa.setPadding(dp(16), dp(8), dp(16), 0);
        EditText busca = new EditText(this);
        busca.setHint("Buscar país ou código");
        busca.setSingleLine(true);
        caixa.addView(busca);
        List<TeamCodec.Pais> filtrados = new ArrayList<>(paises);
        ListView lista = new ListView(this);
        BaseAdapter ad = new BaseAdapter() {
            @Override public int getCount() { return filtrados.size(); }
            @Override public Object getItem(int i) { return filtrados.get(i); }
            @Override public long getItemId(int i) { return i; }

            @Override
            public View getView(int i, View v, ViewGroup pai) {
                TeamCodec.Pais p = filtrados.get(i);
                LinearLayout l = new LinearLayout(RefereeEditorActivity.this);
                l.setGravity(Gravity.CENTER_VERTICAL);
                l.setPadding(dp(4), dp(8), dp(4), dp(8));
                l.addView(imagemBandeira(p.codigo, 24));
                TextView c = texto(p.codigo, 14, AMARELO, true);
                c.setPadding(dp(10), 0, 0, 0);
                l.addView(c, new LinearLayout.LayoutParams(dp(58), ViewGroup.LayoutParams.WRAP_CONTENT));
                l.addView(texto(p.nome, 15, BRANCO, false), new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
                return l;
            }
        };
        lista.setAdapter(ad);
        caixa.addView(lista, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(360)));
        busca.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence t, int a, int b, int c) {}
            @Override public void onTextChanged(CharSequence t, int a, int b, int c) {}
            @Override
            public void afterTextChanged(Editable e) {
                String q = semAcento(e.toString().trim());
                filtrados.clear();
                for (TeamCodec.Pais p : paises)
                    if (q.isEmpty() || semAcento(p.nome).contains(q) || semAcento(p.codigo).startsWith(q)) filtrados.add(p);
                ad.notifyDataSetChanged();
            }
        });
        AlertDialog d = new AlertDialog.Builder(this)
            .setTitle("País do árbitro")
            .setView(caixa)
            .setNegativeButton("Cancelar", null)
            .create();
        lista.setOnItemClickListener((pai, v, pos, id) -> { ao.codigo(filtrados.get(pos).codigo); d.dismiss(); });
        d.show();
    }

    private static String semAcento(String s) {
        return Normalizer.normalize(s, Normalizer.Form.NFD).replaceAll("\\p{M}", "").toLowerCase(java.util.Locale.ROOT);
    }

    private Bitmap bandeira(String codigo) {
        if (codigo == null || codigo.isEmpty()) return null;
        if (bandeiras.containsKey(codigo)) return bandeiras.get(codigo);
        File f = TeamCodec.caminho(jogo, "FLAGS", codigo + ".BMP");
        Bitmap b = f.exists() ? BitmapFactory.decodeFile(f.getPath()) : null;
        bandeiras.put(codigo, b);
        return b;
    }

    private View imagemBandeira(String codigo, int largura) {
        ImageView i = new ImageView(this);
        Bitmap b = bandeira(codigo);
        if (b != null) i.setImageBitmap(b);
        i.setScaleType(ImageView.ScaleType.FIT_XY);
        i.setLayoutParams(new LinearLayout.LayoutParams(dp(largura), dp(largura * 2 / 3)));
        return i;
    }

    private void mover(int pos, int direcao) {
        int j = pos + direcao;
        if (j < 0 || j >= dados.arbitros.size()) return;
        RefereeCodec.Arbitro t = dados.arbitros.get(pos);
        dados.arbitros.set(pos, dados.arbitros.get(j));
        dados.arbitros.set(j, t);
        if (dialogo != null) dialogo.dismiss();
        alterou(true);
    }

    private void salvar() {
        try {
            File backup = new File(arquivo.getPath() + ".bak");
            if (!backup.exists()) Files.copy(arquivo.toPath(), backup.toPath());
            RefereeCodec.gravar(dados, arquivo);
            alterou(false);
            Ui.mensagem(this, "Salvo", "REFEREE.TXE salvo com " + dados.arbitros.size() + " árbitros.\n"
                + "Backup do anterior em: REFEREE.TXE.bak\n\n"
                + "Da próxima vez que abrir o Elifoot, os árbitros novos aparecem.");
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao salvar: " + e.getMessage());
        }
    }

    @Override
    public void onBackPressed() {
        if (!alterado) { super.onBackPressed(); return; }
        new AlertDialog.Builder(this)
            .setTitle("Alterações não salvas")
            .setMessage("Sair sem salvar os árbitros?")
            .setPositiveButton("Sair", (d, w) -> finish())
            .setNegativeButton("Cancelar", null)
            .show();
    }
}
