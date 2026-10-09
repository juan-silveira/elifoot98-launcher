package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.text.Editable;
import android.text.InputType;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.EditorInfo;
import android.widget.AdapterView;
import android.widget.ArrayAdapter;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.GridLayout;
import android.widget.LinearLayout;
import android.widget.FrameLayout;
import android.widget.ListView;
import android.widget.ScrollView;
import android.widget.Spinner;
import android.widget.TextView;

import java.io.File;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;

/**
 * Editor de Save (.e98), com o visual do jogo (verde e amarelo).
 * Barra do topo: save + Salvar. Esquerda: cartoes "Jogo" (inflacao) e "Clube"
 * (time, dinheiro, moral, estadio, cores). Direita (ou abaixo, em pe): tabela
 * dos jogadores; tocar abre a ficha (forca, salario, Nota, Lesao, comportamento).
 */
public class SaveEditorActivity extends Activity {
    private static final int VERDE = 0xFF0B3D0B, VERDE_CARTAO = 0xFF145214, VERDE_LINHA = 0xFF114A11,
        AMARELO = 0xFFFCFE04, BRANCO = 0xFFFFFFFF, CINZA = 0xFFB8C9B8;
    private static final Locale BR = new Locale("pt", "BR");

    private File pasta;
    private final List<String> saves = new ArrayList<>();
    private final List<SaveCodec.Time> times = new ArrayList<>();
    private ArrayAdapter<String> adapterSaves, adapterTimes;
    private final Jogadores adapterJogadores = new Jogadores();
    private Spinner spSave, spTime;
    private EditText inflacao, dinheiro, moral;
    private TextView estadio;
    private Button estadioMenos, estadioMais;
    private TextView previa, infoInflacao, hexLetra, hexFundo;
    private View corLetra, corFundo;
    private LinearLayout cartaoTreinadores, listaTreinadores;
    private SaveCodec.Save atual;
    private SaveCodec.Time timeAtual;
    private File arquivoAtual;
    // Texto mostrado ao carregar: so aplica inflacao/moral se o usuario mudou
    // (a tela arredonda em 1 casa e regravaria um valor diferente do original)
    private String inflacaoMostrada = "", moralMostrado = "";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        pasta = new Jogo(this).pastaJogos();
        adapterSaves = adaptador(saves);
        adapterTimes = adaptador(new ArrayList<>());
        montarTela();
        listarSaves();
    }

    // Girar o aparelho refaz a tela sem perder o save carregado nem o que foi digitado
    @Override
    public void onConfigurationChanged(android.content.res.Configuration nova) {
        super.onConfigurationChanged(nova);
        String inf = inflacao.getText().toString(), din = dinheiro.getText().toString(), mor = moral.getText().toString();
        montarTela();
        if (atual == null) return;
        inflacao.setEnabled(atual.temInflacao());
        inflacao.setText(inf);
        infoInflacao.setText(atual.temInflacao() ? "temporada " + atual.ano : "");
        int is = saves.indexOf(arquivoAtual.getName());
        if (is >= 0) spSave.setSelection(is, false);
        if (timeAtual != null) {
            int it = times.indexOf(timeAtual);
            if (it >= 0) spTime.setSelection(it, false);
            mostrarCampos();
            dinheiro.setText(din);
            moral.setText(mor);
        }
        mostrarTreinadores();
        adapterJogadores.notifyDataSetChanged();
    }

    private void montarTela() {
        boolean deitado = getResources().getDisplayMetrics().widthPixels > getResources().getDisplayMetrics().heightPixels;
        visivelPx = 0;

        LinearLayout raiz = new LinearLayout(this);
        raiz.setOrientation(LinearLayout.VERTICAL);
        raiz.setBackgroundColor(VERDE);

        // Barra do topo: titulo e Salvar; deitado, o save fica na mesma linha
        LinearLayout barra = new LinearLayout(this);
        barra.setGravity(Gravity.CENTER_VERTICAL);
        barra.setBackgroundColor(0xFF062606);
        barra.setPadding(dp(16), dp(8), dp(12), dp(8));
        barra.addView(texto("Editor de Save", 20, AMARELO, true));
        barra.addView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
        spSave = spinner();
        spSave.setAdapter(adapterSaves);
        spTime = spinner();
        spTime.setAdapter(adapterTimes);
        if (deitado) {
            TextView rs = texto("Save", 14, CINZA, false);
            rs.setPadding(0, 0, dp(8), 0);
            barra.addView(rs);
            barra.addView(spSave, new LinearLayout.LayoutParams(dp(170), dp(44)));
        }
        Button salvar = new Button(this);
        salvar.setText("Salvar");
        salvar.setAllCaps(false);
        salvar.setTextColor(Color.BLACK);
        salvar.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
        salvar.setTypeface(Typeface.DEFAULT_BOLD);
        salvar.setBackground(fundo(AMARELO, 0, 8));
        salvar.setOnClickListener(v -> salvar());
        LinearLayout.LayoutParams ps = new LinearLayout.LayoutParams(dp(100), dp(42));
        ps.leftMargin = dp(12);
        barra.addView(salvar, ps);
        raiz.addView(barra);

        // Em pe: save e clube numa faixa propria, logo abaixo da barra
        if (!deitado) {
            LinearLayout faixa = new LinearLayout(this);
            faixa.setGravity(Gravity.CENTER_VERTICAL);
            faixa.setPadding(dp(10), dp(8), dp(10), 0);
            faixa.addView(spSave, new LinearLayout.LayoutParams(0, dp(44), 2));
            LinearLayout.LayoutParams pt = new LinearLayout.LayoutParams(0, dp(44), 3);
            pt.leftMargin = dp(8);
            faixa.addView(spTime, pt);
            raiz.addView(faixa);
        }

        // Cartao "Jogo"
        LinearLayout cartaoJogo = cartao("Jogo");
        inflacao = campoNumero(true);
        infoInflacao = texto("", 12, CINZA, false);
        cartaoJogo.addView(linhaCampo("Inflação", "5 a 100", inflacao, infoInflacao));

        // Cartao "Treinadores": humanos e as suas equipes, com a troca de equipe
        cartaoTreinadores = cartao("Treinadores");
        listaTreinadores = new LinearLayout(this);
        listaTreinadores.setOrientation(LinearLayout.VERTICAL);
        cartaoTreinadores.addView(listaTreinadores);
        cartaoTreinadores.setVisibility(View.GONE);

        // Cartao "Clube"
        LinearLayout cartaoClube = cartao("Clube");
        if (deitado) cartaoClube.addView(spTime, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, dp(44)));
        previa = texto("", 15, BRANCO, true);
        previa.setGravity(Gravity.CENTER);
        previa.setPadding(dp(8), dp(6), dp(8), dp(6));
        LinearLayout.LayoutParams pp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        pp.topMargin = deitado ? dp(6) : 0;
        cartaoClube.addView(previa, pp);
        dinheiro = campoNumero(false);
        moral = campoNumero(true);
        cartaoClube.addView(linhaCampo("Dinheiro", "0 a 999.999.999", dinheiro, null));
        cartaoClube.addView(linhaCampo("Moral", "0 a 20", moral, null));
        cartaoClube.addView(linhaCampo("Estádio", "5.000 a 120.000", seletorEstadio(), null));
        corLetra = amostra(true);
        corFundo = amostra(false);
        hexLetra = texto("", 12, CINZA, false);
        hexFundo = texto("", 12, CINZA, false);
        TextView rc = texto("Cores", 14, CINZA, false);
        rc.setPadding(0, dp(8), 0, dp(4));
        cartaoClube.addView(rc);
        LinearLayout cores = new LinearLayout(this);
        cores.addView(botaoCor("Letra", corLetra, hexLetra, true), new LinearLayout.LayoutParams(0, dp(48), 1));
        cores.addView(new View(this), new LinearLayout.LayoutParams(dp(8), 1));
        cores.addView(botaoCor("Fundo", corFundo, hexFundo, false), new LinearLayout.LayoutParams(0, dp(48), 1));
        cartaoClube.addView(cores);

        LinearLayout esquerda = new LinearLayout(this);
        esquerda.setOrientation(LinearLayout.VERTICAL);
        if (deitado) {
            esquerda.addView(cartaoJogo);
            esquerda.addView(cartaoTreinadores);
            esquerda.addView(cartaoClube);
        } else {
            // Em pe o clube vem primeiro: e o que mais se edita
            esquerda.addView(cartaoClube);
            esquerda.addView(cartaoTreinadores);
            esquerda.addView(cartaoJogo);
        }
        ScrollView rolagem = new ScrollView(this);
        rolagem.addView(esquerda);

        // Tabela de jogadores: cabecalho e lista numa rolagem lateral; o nome fica com o que sobra
        LinearLayout tabela = new LinearLayout(this);
        tabela.setOrientation(LinearLayout.VERTICAL);
        tabela.setBackground(fundo(VERDE_CARTAO, 0, 10));
        tabela.setPadding(dp(8), dp(8), dp(8), dp(8));
        cabecalhoTabela = linhaTabela(TITULOS, true);
        ListView lista = new ListView(this);
        lista.setAdapter(adapterJogadores);
        lista.setDivider(null);
        lista.setOnItemClickListener((p, v, pos, id) -> editarJogador(pos));
        larga = new LinearLayout(this);
        larga.setOrientation(LinearLayout.VERTICAL);
        larga.addView(cabecalhoTabela);
        larga.addView(lista, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        android.widget.HorizontalScrollView lado = new android.widget.HorizontalScrollView(this);
        lado.setFillViewport(true);
        lado.addView(larga, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        lado.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> {
            if (r - l != visivelPx) { visivelPx = r - l; v.post(this::ajustarLarguras); }
        });
        tabela.addView(lado, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        TextView dica = texto("Toque num jogador para editar. S = suspenso, L = lesionado (jogos).", 11, CINZA, false);
        dica.setPadding(dp(4), dp(4), 0, 0);
        tabela.addView(dica);

        LinearLayout corpo = new LinearLayout(this);
        corpo.setPadding(dp(10), dp(10), dp(10), dp(10));
        if (deitado) {
            corpo.setOrientation(LinearLayout.HORIZONTAL);
            corpo.addView(rolagem, new LinearLayout.LayoutParams(dp(280), LinearLayout.LayoutParams.MATCH_PARENT));
            LinearLayout.LayoutParams pt = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1);
            pt.leftMargin = dp(10);
            corpo.addView(tabela, pt);
        } else {
            // Em pe: abas "Clube" e "Jogadores"; cada uma usa a tela toda
            corpo.setOrientation(LinearLayout.VERTICAL);
            LinearLayout abas = new LinearLayout(this);
            abaClube = botaoAba("Clube");
            abaJogadores = botaoAba("Jogadores");
            abas.addView(abaClube, new LinearLayout.LayoutParams(0, dp(40), 1));
            LinearLayout.LayoutParams pa = new LinearLayout.LayoutParams(0, dp(40), 1);
            pa.leftMargin = dp(8);
            abas.addView(abaJogadores, pa);
            corpo.addView(abas);
            FrameLayout conteudo = new FrameLayout(this);
            conteudo.addView(rolagem);
            conteudo.addView(tabela);
            LinearLayout.LayoutParams pc = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1);
            pc.topMargin = dp(10);
            corpo.addView(conteudo, pc);
            abaClube.setOnClickListener(v -> mostrarAba(rolagem, tabela, false));
            abaJogadores.setOnClickListener(v -> mostrarAba(rolagem, tabela, true));
            mostrarAba(rolagem, tabela, abaJogadoresAtiva);
        }
        raiz.addView(corpo, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(raiz);

        spSave.setOnItemSelectedListener(new Selecao(this::carregarSave));
        spTime.setOnItemSelectedListener(new Selecao(this::carregarTime));
    }

    private boolean abaJogadoresAtiva;
    private Button abaClube, abaJogadores;

    private Button botaoAba(String t) {
        Button b = new Button(this);
        b.setText(t);
        b.setAllCaps(false);
        b.setTextColor(Color.BLACK);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
        b.setStateListAnimator(null);
        return b;
    }

    private void mostrarAba(View clube, View jogadores, boolean jog) {
        abaJogadoresAtiva = jog;
        clube.setVisibility(jog ? View.GONE : View.VISIBLE);
        jogadores.setVisibility(jog ? View.VISIBLE : View.GONE);
        abaClube.setBackground(fundo(jog ? 0xFFE6E6E6 : AMARELO, 0, 6));
        abaJogadores.setBackground(fundo(jog ? AMARELO : 0xFFE6E6E6, 0, 6));
        abaClube.setTypeface(jog ? Typeface.DEFAULT : Typeface.DEFAULT_BOLD);
        abaJogadores.setTypeface(jog ? Typeface.DEFAULT_BOLD : Typeface.DEFAULT);
        atualizarAbaJogadores();
    }

    private void atualizarAbaJogadores() {
        if (abaJogadores == null) return;
        abaJogadores.setText(timeAtual == null ? "Jogadores" : "Jogadores (" + timeAtual.jogadores.size() + ")");
    }

    // ---- pecas de layout ----

    private int dp(int v) { return Ui.dp(this, v); }

    private TextView texto(String t, int sp, int cor, boolean negrito) {
        TextView v = new TextView(this);
        v.setText(t);
        v.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        v.setTextColor(cor);
        if (negrito) v.setTypeface(Typeface.DEFAULT_BOLD);
        return v;
    }

    private static GradientDrawable fundo(int cor, int borda, int raio) {
        GradientDrawable g = new GradientDrawable();
        g.setColor(cor);
        if (borda != 0) g.setStroke(2, borda);
        g.setCornerRadius(raio * 3f);
        return g;
    }

    private LinearLayout cartao(String titulo) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setBackground(fundo(VERDE_CARTAO, 0, 10));
        c.setPadding(dp(14), dp(10), dp(14), dp(12));
        TextView t = texto(titulo.toUpperCase(BR), 12, AMARELO, true);
        t.setLetterSpacing(0.1f);
        t.setPadding(0, 0, 0, dp(6));
        c.addView(t);
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        p.bottomMargin = dp(10);
        c.setLayoutParams(p);
        return c;
    }

    private EditText campoNumero(boolean decimal) {
        EditText e = new EditText(this);
        e.setInputType(InputType.TYPE_CLASS_NUMBER | (decimal ? InputType.TYPE_NUMBER_FLAG_DECIMAL : 0));
        e.setSingleLine(true);
        e.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);  // deitado: teclado sem tela cheia
        e.setTextColor(BRANCO);
        e.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
        e.setBackgroundTintList(ColorStateList.valueOf(AMARELO));
        return e;
    }

    // Rotulo com a faixa permitida embaixo, em letra menor
    private LinearLayout linhaCampo(String rotulo, String faixa, View campo, TextView extra) {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout r = new LinearLayout(this);
        r.setOrientation(LinearLayout.VERTICAL);
        r.addView(texto(rotulo, 14, CINZA, false));
        r.addView(texto(faixa, 11, 0xFF8FA88F, false));
        l.addView(r, new LinearLayout.LayoutParams(dp(112), LinearLayout.LayoutParams.WRAP_CONTENT));
        l.addView(campo, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        if (extra != null) {
            extra.setPadding(dp(8), 0, 0, 0);
            l.addView(extra);
        }
        return l;
    }

    // Estadio: − / + de 5.000 em 5.000 lugares (N de 1 a 24, como no jogo)
    private View seletorEstadio() {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.CENTER_VERTICAL);
        estadioMenos = botaoPasso("−", -1);
        estadioMais = botaoPasso("+", 1);
        estadio = texto("", 15, BRANCO, true);
        estadio.setGravity(Gravity.CENTER);
        // "120.000" tem que caber numa linha: encolhe a letra se precisar
        estadio.setMaxLines(1);
        estadio.setAutoSizeTextTypeUniformWithConfiguration(10, 15, 1, TypedValue.COMPLEX_UNIT_SP);
        l.addView(estadioMenos, new LinearLayout.LayoutParams(dp(34), dp(34)));
        LinearLayout.LayoutParams pe = new LinearLayout.LayoutParams(0, dp(34), 1);
        pe.leftMargin = dp(4);
        pe.rightMargin = dp(4);
        l.addView(estadio, pe);
        l.addView(estadioMais, new LinearLayout.LayoutParams(dp(34), dp(34)));
        return l;
    }

    private Button botaoPasso(String rotulo, int passo) {
        Button b = new Button(this);
        b.setText(rotulo);
        b.setTextColor(Color.BLACK);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 20);
        b.setPadding(0, 0, 0, 0);
        b.setMinWidth(0);
        b.setMinimumWidth(0);
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        b.setStateListAnimator(null);
        b.setBackground(fundo(AMARELO, 0, 6));
        b.setOnClickListener(v -> {
            if (timeAtual == null || !timeAtual.temEstadio()) return;
            timeAtual.estadio = Math.max(1, Math.min(SaveCodec.ESTADIO_MAX, timeAtual.estadio + passo));
            mostrarEstadio();
        });
        return b;
    }

    private void mostrarEstadio() {
        boolean tem = timeAtual != null && timeAtual.temEstadio();
        estadio.setText(tem ? String.format(BR, "%,d", timeAtual.estadio * 5000) : "—");
        estadioMenos.setEnabled(tem && timeAtual.estadio > 1);
        estadioMais.setEnabled(tem && timeAtual.estadio < SaveCodec.ESTADIO_MAX);
        estadioMenos.setAlpha(estadioMenos.isEnabled() ? 1f : 0.35f);
        estadioMais.setAlpha(estadioMais.isEnabled() ? 1f : 0.35f);
    }

    private Spinner spinner() {
        Spinner s = new Spinner(this);
        s.setBackground(fundo(0xFFFFFFFF, 0, 6));
        s.setPadding(dp(8), 0, dp(8), 0);
        return s;
    }

    private ArrayAdapter<String> adaptador(List<String> itens) {
        return new ArrayAdapter<String>(this, android.R.layout.simple_spinner_item, itens) {
            {
                setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
            }

            @Override
            public View getView(int pos, View v, ViewGroup pai) {
                TextView t = (TextView) super.getView(pos, v, pai);
                t.setTextColor(Color.BLACK);
                t.setSingleLine(true);
                t.setEllipsize(TextUtils.TruncateAt.END);
                return t;
            }
        };
    }

    private View amostra(boolean letra) {
        return new View(this);
    }

    // Botao "Letra"/"Fundo": amostra da cor + nome + codigo; toque abre o seletor
    private LinearLayout botaoCor(String nome, View amostra, TextView hex, boolean letra) {
        LinearLayout b = new LinearLayout(this);
        b.setGravity(Gravity.CENTER_VERTICAL);
        b.setPadding(dp(8), 0, dp(8), 0);
        b.setBackground(fundo(VERDE_LINHA, 0xFF2E6B2E, 6));
        b.addView(amostra, new LinearLayout.LayoutParams(dp(28), dp(28)));
        LinearLayout t = new LinearLayout(this);
        t.setOrientation(LinearLayout.VERTICAL);
        t.setPadding(dp(8), 0, 0, 0);
        t.addView(texto(nome, 14, BRANCO, true));
        t.addView(hex, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        b.addView(t, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        b.setOnClickListener(x -> escolherCor(letra));
        return b;
    }

    // Colunas da tabela: posicao, nome, forca, salario, nota, lesao, comportamento, situacao
    private static final int[] LARGURAS = {28, 0, 14, 40, 64, 34, 38, 84, 36};
    private static final String[] TITULOS = {"Pos", "Nome", "✱", "Força", "Salário", "Nota", "Lesão", "Comport.", "Sit."};
    private static final int COL_ESTRELA = 2, COL_FORCA = 3, COL_COMP = 7, COL_SIT = 8;

    private static final int NOME_MIN = 150;
    private int nomePx, visivelPx;
    private LinearLayout larga, cabecalhoTabela;

    private LinearLayout.LayoutParams params(int i) {
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(LARGURAS[i] == 0 ? nomePx : dp(LARGURAS[i]), LinearLayout.LayoutParams.WRAP_CONTENT);
        p.leftMargin = i == 0 ? 0 : dp(5);
        return p;
    }

    // Nome (largura 0) fica com o espaco que sobra, no minimo NOME_MIN; se nao couber, a tabela rola de lado
    private void ajustarLarguras() {
        if (larga == null || visivelPx <= 0) return;
        int fixo = dp(12);
        for (int i = 0; i < LARGURAS.length; i++) fixo += (i == 0 ? 0 : dp(5)) + dp(LARGURAS[i]);
        nomePx = Math.max(dp(NOME_MIN), visivelPx - fixo);
        ViewGroup.LayoutParams lp = larga.getLayoutParams();
        lp.width = fixo + nomePx;
        larga.setLayoutParams(lp);
        for (int i = 0; i < cabecalhoTabela.getChildCount(); i++) cabecalhoTabela.getChildAt(i).setLayoutParams(params(i));
        adapterJogadores.notifyDataSetChanged();
    }

    private LinearLayout linhaTabela(String[] textos, boolean cabecalho) {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.CENTER_VERTICAL);
        l.setPadding(dp(6), dp(cabecalho ? 4 : 9), dp(6), dp(cabecalho ? 6 : 9));
        for (int i = 0; i < textos.length; i++) {
            TextView t = texto(textos[i], cabecalho ? 12 : 14, cabecalho ? AMARELO : BRANCO, cabecalho);
            t.setSingleLine(true);
            t.setEllipsize(TextUtils.TruncateAt.END);
            t.setGravity(i == 1 || i == COL_COMP ? Gravity.START : Gravity.END);
            if (i == 0 || i == COL_ESTRELA) t.setGravity(Gravity.CENTER);
            l.addView(t, params(i));
        }
        return l;
    }

    private View cabecalho() {
        return linhaTabela(TITULOS, true);
    }

    private static String nomePosicao(String pos) {
        switch (pos) {
            case "G": return "Guarda-redes";
            case "D": return "Defesa";
            case "M": return "Médio";
            case "A": return "Avançado";
            default: return pos;
        }
    }

    private static int corPosicao(String pos) {
        switch (pos) {
            case "G": return 0xFFE0B000;
            case "D": return 0xFF3D7BD9;
            case "M": return 0xFF2E9E4F;
            case "A": return 0xFFD9443D;
            default: return Color.GRAY;
        }
    }

    private final class Jogadores extends BaseAdapter {
        @Override public int getCount() { return timeAtual == null ? 0 : timeAtual.jogadores.size(); }
        @Override public Object getItem(int i) { return timeAtual.jogadores.get(i); }
        @Override public long getItemId(int i) { return i; }

        @Override
        public View getView(int i, View v, ViewGroup pai) {
            SaveCodec.Jogador j = timeAtual.jogadores.get(i);
            String comp = j.comportamento >= 0 && j.comportamento < SaveCodec.COMPORTAMENTOS.length
                ? SaveCodec.COMPORTAMENTOS[j.comportamento] : "?";
            String sit = (j.suspensao > 0 ? "S" + j.suspensao : "") + (j.jogosLesionado > 0 ? " L" + j.jogosLesionado : "");
            LinearLayout l = linhaTabela(new String[] {
                j.posicao, j.nome, j.estrela ? "✱" : "", Integer.toString(j.forca),
                String.format(BR, "%,d", j.salario), Integer.toString(j.nota), Integer.toString(j.lesao), comp, sit.trim(),
            }, false);
            l.setBackgroundColor(i % 2 == 0 ? VERDE_LINHA : VERDE_CARTAO);
            TextView pos = (TextView) l.getChildAt(0);
            pos.setTextColor(BRANCO);
            pos.setTypeface(Typeface.DEFAULT_BOLD);
            pos.setBackground(fundo(corPosicao(j.posicao), 0, 4));
            ((TextView) l.getChildAt(COL_ESTRELA)).setTextColor(0xFFFFD54F);
            if (j.forca > SaveCodec.FORCA_AVISO_ACIMA) ((TextView) l.getChildAt(COL_FORCA)).setTextColor(AMARELO);
            if (!sit.isEmpty()) ((TextView) l.getChildAt(COL_SIT)).setTextColor(0xFFFF8A80);
            return l;
        }
    }

    // ---- dados ----

    private void listarSaves() {
        saves.clear();
        String[] nomes = pasta.list((d, n) -> n.toLowerCase(Locale.ROOT).endsWith(".e98"));
        if (nomes != null) {
            Arrays.sort(nomes);
            saves.addAll(Arrays.asList(nomes));
        }
        adapterSaves.notifyDataSetChanged();
        if (saves.isEmpty()) Ui.mensagem(this, "Sem saves", "Nenhum jogo gravado em JOGOS ainda.");
    }

    private void carregarSave(int pos) {
        if (pos < 0 || pos >= saves.size()) return;
        if (atual != null && arquivoAtual != null && arquivoAtual.getName().equals(saves.get(pos))) return;
        arquivoAtual = new File(pasta, saves.get(pos));
        try {
            atual = SaveCodec.ler(arquivoAtual);
            timeAtual = null;
            inflacao.setEnabled(atual.temInflacao());
            inflacaoMostrada = atual.temInflacao() ? fmt(atual.inflacao * 10) : "";
            inflacao.setText(inflacaoMostrada);
            infoInflacao.setText(atual.temInflacao() ? "temporada " + atual.ano : "");
            times.clear();
            times.addAll(atual.times);
            times.sort((a, b) -> a.nome.compareToIgnoreCase(b.nome));
            adapterTimes.clear();
            for (SaveCodec.Time t : times) adapterTimes.add(t.nome);
            adapterTimes.notifyDataSetChanged();
            if (!times.isEmpty()) { spTime.setSelection(0); carregarTime(0); }
            mostrarTreinadores();
        } catch (Exception e) {
            atual = null;
            mostrarTreinadores();
            Ui.mensagem(this, "Erro", "Falha ao ler save:\n" + e.getMessage());
        }
    }

    private void carregarTime(int pos) {
        if (pos < 0 || pos >= times.size()) return;
        SaveCodec.Time t = times.get(pos);
        if (t != timeAtual) {
            if (timeAtual != null && !aplicarCampos()) { spTime.setSelection(times.indexOf(timeAtual)); return; }
            timeAtual = t;
            mostrarCampos();
        }
        atualizarAbaJogadores();
        adapterJogadores.notifyDataSetChanged();
    }

    private void mostrarCampos() {
        SaveCodec.Time t = timeAtual;
        dinheiro.setText(Long.toString(t.verba));
        moral.setEnabled(t.temMoral());
        moralMostrado = t.temMoral() ? fmt(t.moral * 10) : "";
        moral.setText(moralMostrado);
        mostrarEstadio();
        pintarCores();
    }

    // ---- treinadores ----

    private void mostrarTreinadores() {
        listaTreinadores.removeAllViews();
        boolean algum = false;
        if (atual != null) {
            for (SaveCodec.Tecnico tec : atual.tecnicos) {
                if (!tec.humano) continue;
                algum = true;
                SaveCodec.Time equipe = atual.timeDoTecnico(tec);
                LinearLayout l = new LinearLayout(this);
                l.setGravity(Gravity.CENTER_VERTICAL);
                l.setPadding(0, dp(4), 0, dp(4));
                LinearLayout txt = new LinearLayout(this);
                txt.setOrientation(LinearLayout.VERTICAL);
                txt.addView(texto(tec.nome, 15, BRANCO, true));
                txt.addView(texto(equipe != null ? equipe.nome : "sem equipe", 12, CINZA, false));
                l.addView(txt, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
                if (equipe != null) {
                    Button b = new Button(this);
                    b.setText("Trocar");
                    b.setAllCaps(false);
                    b.setTextColor(Color.BLACK);
                    b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
                    b.setMinWidth(0);
                    b.setMinimumWidth(0);
                    b.setMinHeight(0);
                    b.setMinimumHeight(0);
                    b.setPadding(dp(12), 0, dp(12), 0);
                    b.setStateListAnimator(null);
                    b.setBackground(fundo(AMARELO, 0, 6));
                    b.setOnClickListener(v -> trocarEquipe(tec));
                    l.addView(b, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, dp(34)));
                }
                listaTreinadores.addView(l);
            }
        }
        cartaoTreinadores.setVisibility(algum ? View.VISIBLE : View.GONE);
    }

    // Escolhe a equipe nova; as duas equipes trocam de treinador como numa
    // "chicotada psicologica" do jogo
    private void trocarEquipe(SaveCodec.Tecnico tec) {
        if (atual == null || !aplicarCampos()) return;
        SaveCodec.Time origem = atual.timeDoTecnico(tec);
        List<SaveCodec.Time> destinos = new ArrayList<>();
        for (SaveCodec.Time t : times) if (t != origem && t.tecnicoId >= 0 && t.podeTerHumano()) destinos.add(t);
        if (origem == null || destinos.isEmpty()) return;
        List<String> nomes = new ArrayList<>();
        for (SaveCodec.Time t : destinos) nomes.add(t.nome + "  (" + t.divisao + ")");

        LinearLayout caixa = new LinearLayout(this);
        caixa.setOrientation(LinearLayout.VERTICAL);
        caixa.setPadding(dp(20), dp(8), dp(20), 0);
        TextView atualTxt = new TextView(this);
        atualTxt.setText("Equipe atual: " + origem.nome);
        caixa.addView(atualTxt);
        TextView rotulo = new TextView(this);
        rotulo.setText("Nova equipe");
        rotulo.setPadding(0, dp(12), 0, 0);
        caixa.addView(rotulo);
        Spinner sp = new Spinner(this);
        sp.setAdapter(new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, nomes));
        caixa.addView(sp);
        TextView efeito = texto("", 12, Color.GRAY, false);
        efeito.setPadding(0, dp(10), 0, 0);
        caixa.addView(efeito);
        sp.setOnItemSelectedListener(new Selecao(pos -> {
            SaveCodec.Tecnico outro = atual.tecnico(destinos.get(pos).tecnicoId);
            efeito.setText((outro != null ? outro.nome + " vai para " + origem.nome + ". " : "")
                + "As duas equipes ficam com moral 10, como numa chicotada psicológica do jogo.");
        }));
        ScrollView rolagem = new ScrollView(this);
        rolagem.addView(caixa);
        new AlertDialog.Builder(this)
            .setTitle("Trocar de equipe — " + tec.nome)
            .setView(rolagem)
            .setPositiveButton("Trocar", (d, w) -> {
                SaveCodec.Time destino = destinos.get(sp.getSelectedItemPosition());
                try {
                    SaveCodec.trocarEquipe(atual, tec, destino);
                } catch (IllegalArgumentException e) {
                    Ui.mensagem(this, "Erro", e.getMessage());
                    return;
                }
                mostrarTreinadores();
                if (timeAtual != null) mostrarCampos();
                int i = times.indexOf(destino);
                if (i >= 0) spTime.setSelection(i);
                Ui.mensagem(this, "Equipe trocada", tec.nome + " agora treina " + destino.nome + ".\nToque em Salvar para gravar no save.");
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    private void pintarCores() {
        boolean tem = timeAtual != null && timeAtual.temCores();
        corLetra.setEnabled(tem);
        corFundo.setEnabled(tem);
        if (!tem) { previa.setText(timeAtual != null ? timeAtual.nome : ""); return; }
        corLetra.setBackground(fundo(0xFF000000 | timeAtual.corLetra, CINZA, 4));
        corFundo.setBackground(fundo(0xFF000000 | timeAtual.corFundo, CINZA, 4));
        hexLetra.setText(String.format(Locale.ROOT, "#%06X", timeAtual.corLetra));
        hexFundo.setText(String.format(Locale.ROOT, "#%06X", timeAtual.corFundo));
        previa.setText(timeAtual.nome);
        previa.setTextColor(0xFF000000 | timeAtual.corLetra);
        previa.setBackground(fundo(0xFF000000 | timeAtual.corFundo, CINZA, 4));
    }

    private void escolherCor(boolean letra) {
        if (timeAtual == null || !timeAtual.temCores()) return;
        SeletorCor.escolher(this, letra ? "Cor da letra" : "Cor do fundo", letra ? timeAtual.corLetra : timeAtual.corFundo, rgb -> {
            if (letra) timeAtual.corLetra = rgb; else timeAtual.corFundo = rgb;
            pintarCores();
        });
    }

    private void editarJogador(int pos) {
        if (timeAtual == null || pos >= timeAtual.jogadores.size()) return;
        SaveCodec.Jogador j = timeAtual.jogadores.get(pos);
        boolean deitado = getResources().getDisplayMetrics().widthPixels > getResources().getDisplayMetrics().heightPixels;
        LinearLayout ficha = new LinearLayout(this);
        ficha.setOrientation(LinearLayout.VERTICAL);
        ficha.setPadding(dp(20), dp(4), dp(20), 0);
        // Posicao por extenso num selo com a cor da posicao (+ estrela)
        LinearLayout topo = new LinearLayout(this);
        topo.setGravity(Gravity.CENTER_VERTICAL);
        TextView selo = texto(nomePosicao(j.posicao), 13, BRANCO, true);
        selo.setPadding(dp(10), dp(3), dp(10), dp(3));
        selo.setBackground(fundo(corPosicao(j.posicao), 0, 4));
        topo.addView(selo);
        TextView est = texto("", 13, 0xFFE0B000, true);
        est.setPadding(dp(12), 0, 0, 0);
        topo.addView(est);
        ficha.addView(topo);
        TextView regra = texto("Estrela (✱): Médio ou Avançado com nota 8 ou mais. Muda sozinha com a nota.", 12, Color.GRAY, false);
        regra.setPadding(0, dp(6), 0, dp(8));
        ficha.addView(regra);
        GridLayout g = new GridLayout(this);
        g.setColumnCount(deitado ? 3 : 2);
        ficha.addView(g);
        EditText forca = campoFicha(g, "Força", "1 a " + SaveCodec.FORCA_MAX + " (normal ≤ 50)", j.forca);
        EditText salario = campoFicha(g, "Salário", "50 a 9.999.999", j.salario);
        EditText nota = campoFicha(g, "Nota", "1 a 10", j.nota);
        EditText lesao = campoFicha(g, "Lesão", "0 = nunca, 10 = muito", j.lesao);
        EditText suspenso = campoFicha(g, "Suspenso (S)", "jogos, 0 a 4", j.suspensao);
        EditText lesionado = campoFicha(g, "Lesionado (L)", "jogos, 0 a 20", j.jogosLesionado);
        // Estrela ao vivo: se a nota mudar, segue a regra do jogo; senao mantem a do save
        Runnable mostrarEstrela = () -> {
            boolean tem = j.estrela;
            try {
                int n = Integer.parseInt(nota.getText().toString().trim());
                if (n != j.nota) tem = SaveCodec.temEstrela(j.posicao, n);
            } catch (NumberFormatException ignorado) { }
            est.setText(tem ? "✱ Estrela" : "Sem estrela");
            est.setTextColor(tem ? 0xFFE0B000 : Color.GRAY);
        };
        nota.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence t, int a, int b, int c) {}
            @Override public void onTextChanged(CharSequence t, int a, int b, int c) {}
            @Override public void afterTextChanged(Editable e) { mostrarEstrela.run(); }
        });
        mostrarEstrela.run();
        Spinner comp = new Spinner(this);
        comp.setAdapter(new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, SaveCodec.COMPORTAMENTOS));
        comp.setSelection(Math.max(0, Math.min(5, j.comportamento)));
        celulaFicha(g, "Comportamento", "Fair Play … Sarrafeiro", comp, 2);
        ScrollView rolagem = new ScrollView(this);
        rolagem.addView(ficha);
        AlertDialog dialogo = new AlertDialog.Builder(this)
            .setTitle(j.nome)
            .setView(rolagem)
            .setPositiveButton("OK", null)
            .setNegativeButton("Cancelar", null)
            .create();
        // OK so fecha se tudo estiver dentro dos limites
        dialogo.setOnShowListener(x -> dialogo.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            try {
                int f = ler(forca, "Força", SaveCodec.FORCA_MIN, SaveCodec.FORCA_MAX);
                int s = ler(salario, "Salário", SaveCodec.SALARIO_MIN, SaveCodec.SALARIO_MAX);
                int n = ler(nota, "Nota", 1, 10);
                int l = ler(lesao, "Lesão", 0, 10);
                int su = ler(suspenso, "Suspenso", 0, SaveCodec.SUSPENSAO_MAX);
                int le = ler(lesionado, "Lesionado", 0, SaveCodec.JOGOS_LESIONADO_MAX);
                if (n != j.nota) j.estrela = SaveCodec.temEstrela(j.posicao, n);
                j.forca = f;
                j.salario = s;
                j.nota = n;
                j.lesao = l;
                j.suspensao = su;
                j.jogosLesionado = le;
                j.comportamento = comp.getSelectedItemPosition();
                adapterJogadores.notifyDataSetChanged();
                dialogo.dismiss();
                if (j.forca > SaveCodec.FORCA_AVISO_ACIMA)
                    Ui.mensagem(this, "Aviso", "Força " + j.forca + " é bem acima do normal (1-" + SaveCodec.FORCA_AVISO_ACIMA + ").");
            } catch (ForaDoLimite e) {
                Ui.mensagem(this, "Valor fora do limite", e.getMessage());
            }
        }));
        dialogo.show();
    }

    // Celula da ficha: nome e faixa em cima, campo embaixo
    private void celulaFicha(GridLayout g, String titulo, String faixa, View campo, int colunas) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        TextView nome = new TextView(this);
        nome.setText(titulo);
        c.addView(nome);
        c.addView(texto(faixa, 11, Color.GRAY, false));
        c.addView(campo, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        GridLayout.LayoutParams p = new GridLayout.LayoutParams(GridLayout.spec(GridLayout.UNDEFINED),
            GridLayout.spec(GridLayout.UNDEFINED, Math.min(colunas, g.getColumnCount())));
        p.width = dp(132 * Math.min(colunas, g.getColumnCount()) + 16 * (Math.min(colunas, g.getColumnCount()) - 1));
        p.setMargins(0, 0, dp(16), dp(10));
        g.addView(c, p);
    }

    private EditText campoFicha(GridLayout g, String titulo, String faixa, int valor) {
        EditText e = new EditText(this);
        e.setInputType(InputType.TYPE_CLASS_NUMBER);
        e.setSingleLine(true);
        e.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);  // deitado: teclado sem tela cheia
        e.setText(Integer.toString(valor));
        celulaFicha(g, titulo, faixa, e, 1);
        return e;
    }

    private static final class ForaDoLimite extends Exception {
        ForaDoLimite(String m) { super(m); }
    }

    private static String faixa(String nome, double min, double max) {
        return String.format(BR, "%s: use um valor de %,.0f a %,.0f.", nome, min, max);
    }

    // Le um inteiro do campo; fora da faixa (ou nao numerico) avisa em vez de cortar
    private static long lerLong(EditText e, String nome, long min, long max) throws ForaDoLimite {
        String t = e.getText().toString().trim().replace(".", "");
        try {
            long v = Long.parseLong(t);
            if (v >= min && v <= max) return v;
        } catch (NumberFormatException ignorado) { }
        throw new ForaDoLimite(faixa(nome, min, max));
    }

    private static int ler(EditText e, String nome, int min, int max) throws ForaDoLimite {
        return (int) lerLong(e, nome, min, max);
    }

    private static double lerReal(EditText e, String nome, double min, double max) throws ForaDoLimite {
        try {
            double v = Double.parseDouble(e.getText().toString().trim().replace(',', '.'));
            if (v >= min && v <= max) return v;
        } catch (NumberFormatException ignorado) { }
        throw new ForaDoLimite(faixa(nome, min, max));
    }

    // Passa os campos do clube e da inflacao pro save em memoria (dentro dos limites do jogo)
    private boolean aplicarCampos() {
        try {
            if (atual != null && atual.temInflacao() && !inflacao.getText().toString().trim().equals(inflacaoMostrada))
                atual.inflacao = lerReal(inflacao, "Inflação", SaveCodec.INFLACAO_MIN * 10, SaveCodec.INFLACAO_MAX * 10) / 10;
            if (timeAtual != null) {
                timeAtual.verba = lerLong(dinheiro, "Dinheiro", 0, SaveCodec.DINHEIRO_MAX);
                if (timeAtual.temMoral() && !moral.getText().toString().trim().equals(moralMostrado))
                    timeAtual.moral = lerReal(moral, "Moral", 0, SaveCodec.MORAL_MAX * 10) / 10;
            }
            return true;
        } catch (ForaDoLimite e) {
            Ui.mensagem(this, "Valor fora do limite", e.getMessage());
            return false;
        }
    }

    private void salvar() {
        if (atual == null || arquivoAtual == null || !aplicarCampos()) return;
        try {
            File bak = new File(arquivoAtual.getPath() + ".bak");
            if (!bak.exists()) Files.copy(arquivoAtual.toPath(), bak.toPath());
            SaveCodec.gravar(arquivoAtual, atual);
            Ui.mensagem(this, "Salvo", "Save gravado (backup .bak na primeira vez).");
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao gravar:\n" + e.getMessage());
        }
    }

    private static String fmt(double v) {
        return String.format(Locale.ROOT, "%.1f", v);
    }

    private static final class Selecao implements AdapterView.OnItemSelectedListener {
        interface Acao { void em(int pos); }
        private final Acao acao;
        Selecao(Acao a) { acao = a; }
        @Override public void onItemSelected(AdapterView<?> p, View v, int pos, long id) { acao.em(pos); }
        @Override public void onNothingSelected(AdapterView<?> p) {}
    }
}
