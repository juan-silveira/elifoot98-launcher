package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.content.res.ColorStateList;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.Typeface;
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
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.ScrollView;
import android.widget.Spinner;
import android.widget.TextView;

import java.io.File;
import java.text.Collator;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

/**
 * Scout: todos os jogadores de um save (como o Turbo Save), com filtros e
 * ordenacao para achar os melhores. So leitura.
 */
public class ScoutActivity extends Activity {
    private static final int VERDE = 0xFF0B3D0B, VERDE_TOPO = 0xFF062606, VERDE_CARTAO = 0xFF145214, VERDE_LINHA = 0xFF114A11,
        AMARELO = 0xFFFCFE04, BRANCO = 0xFFFFFFFF, CINZA = 0xFFB8C9B8, CINZA_FAIXA = 0xFF8FA88F,
        ALERTA = 0xFFFF8A80, OK = 0xFF9CE29C, OURO = 0xFFFFD54F, DESLIGADO = 0xFF2A5C2A;
    private static final int[] CORES_POS = {0xFFE0B000, 0xFF3D7BD9, 0xFF2E9E4F, 0xFFD9443D};
    private static final Locale BR = new Locale("pt", "BR");
    // Equipe depois dos numeros; a estrela logo depois do nome
    // Score = rendimento (0 a 1000) e Impacto = pontos por temporada no time (docs/score.md)
    private static final String[] TITULOS = {"Pos", "Nome", "✱", "Score", "Força", "Nota", "Lesão", "Comport.", "Impacto", "Jogos", "Gols", "Lesões", "Expuls.", "Equipe", "Sit."};
    // Deitado com filtros abertos o historial (jogos, gols, lesoes, expulsoes) fica escondido (-1);
    // com os filtros recolhidos a tabela ganha a largura e mostra tudo
    private static final int[] LARGURAS = {28, 0, 14, 60, 56, 48, 56, 84, -1, -1, -1, -1, -1, 130, 64};
    private static final int[] LARGURAS_SEM_FILTROS = {28, 0, 14, 60, 56, 48, 56, 84, 72, 56, 48, 60, 64, 130, 64};
    // Em pe: sem a coluna de comportamento (-1 = escondida) e equipe mais estreita
    // Em pe: colunas na largura cheia e a tabela rola para os lados (nada truncado)
    private static final int[] LARGURAS_EM_PE = {28, 0, 16, 60, 56, 48, 56, 100, 72, 56, 48, 60, 64, 150, 70};
    private int[] larguras = LARGURAS;
    private static final int NOME_MIN = 150;
    private int nomePx, visivelPx;
    private LinearLayout larga;

    private static final class Linha {
        SaveCodec.Jogador j;
        SaveCodec.Time equipe;
        int pos, ordemDivisao;
        String situacao = "";
        boolean humana;
        int score;
        double impacto;
    }

    private File jogo, pastaJogos;
    private final Map<String, TeamCodec.Pais> porCodigo = new HashMap<>();
    private final Map<String, Bitmap> bandeiras = new HashMap<>();
    private TeamCodec.Regras regras;
    private final List<String> saves = new ArrayList<>();
    private List<Linha> todos = new ArrayList<>();
    private final List<Linha> visiveis = new ArrayList<>();
    private final List<String> paisesFiltro = new ArrayList<>();
    private final List<String> divisoesFiltro = new ArrayList<>();
    private final List<SaveCodec.Time> equipesFiltro = new ArrayList<>();
    // Ordenacao em niveis: {coluna, 1 = do maior}; o 1o criterio manda, os outros desempatam
    private final List<int[]> criterios = new ArrayList<>(Arrays.asList(new int[][] {{3, 1}}));
    private static final String SOBRESCRITOS = "¹²³⁴⁵⁶⁷⁸⁹";
    private boolean montando;

    private Spinner spSave, spPais, spDivisao, spEquipe, spNota, spLesao, spComp;
    private EditText nome, forcaMin, forcaMax;
    private final Button[] posicoes = new Button[4];
    private final boolean[] posLigada = {true, true, true, true};
    private CheckBox soEstrelas, soEstrangeiros, semIndisponiveis, semHumanas;
    private TextView conta;
    private LinearLayout cabecalho;
    private final LinhasAdapter adapter = new LinhasAdapter();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        Jogo j = new Jogo(this);
        jogo = j.jogo;
        pastaJogos = j.pastaJogos();
        List<TeamCodec.Pais> paises = new ArrayList<>();
        try { paises = TeamCodec.lerPaises(jogo); } catch (Exception ignorado) { }
        for (TeamCodec.Pais p : paises) porCodigo.put(p.codigo, p);
        regras = new TeamCodec.Regras(jogo, paises);

        LinearLayout raiz = new LinearLayout(this);
        raiz.setOrientation(LinearLayout.VERTICAL);
        raiz.setBackgroundColor(VERDE);

        LinearLayout barra = new LinearLayout(this);
        barra.setGravity(Gravity.CENTER_VERTICAL);
        barra.setBackgroundColor(VERDE_TOPO);
        barra.setPadding(dp(16), dp(8), dp(12), dp(8));
        barra.addView(texto("Scout", 20, AMARELO, true));
        TextView sub = texto("os melhores jogadores do seu jogo", 13, CINZA_FAIXA, false);
        sub.setPadding(dp(12), 0, 0, 0);
        barra.addView(sub, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        boolean deitado = getResources().getDisplayMetrics().widthPixels > getResources().getDisplayMetrics().heightPixels;
        if (!deitado) sub.setVisibility(View.GONE);
        larguras = deitado ? LARGURAS : LARGURAS_EM_PE;
        TextView rotSave = texto("Save", 14, CINZA, false);
        rotSave.setPadding(dp(12), 0, dp(6), 0);  // em pe o subtitulo some e o "Save" encostava no titulo
        barra.addView(rotSave);
        spSave = spinnerBranco();
        spSave.setAdapter(adaptadorTexto(saves));
        spSave.setOnItemSelectedListener(new Selecao(p -> carregarSave()));
        barra.addView(spSave, new LinearLayout.LayoutParams(dp(deitado ? 170 : 120), dp(40)));
        raiz.addView(barra);

        LinearLayout corpo = new LinearLayout(this);
        corpo.setPadding(dp(10), dp(10), dp(10), dp(10));
        View painelFiltros = filtros();
        // Filtros abrem e fecham por um botao no topo: em pe comecam fechados (abaixo da barra),
        // deitados comecam abertos (ao lado); fechados, a tabela usa a largura toda
        String abertoTxt = deitado ? "Filtros ◂" : "Filtros ▴", fechadoTxt = deitado ? "Filtros ▸" : "Filtros ▾";
        if (deitado) {
            LinearLayout.LayoutParams pf = new LinearLayout.LayoutParams(dp(240), ViewGroup.LayoutParams.MATCH_PARENT);
            pf.rightMargin = dp(10); // a margem vai junto com o painel, fechado a tabela encosta na borda
            corpo.addView(painelFiltros, pf);
        } else {
            corpo.setOrientation(LinearLayout.VERTICAL);
            painelFiltros.setVisibility(View.GONE);
            LinearLayout.LayoutParams pf = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, getResources().getDisplayMetrics().heightPixels / 2);
            pf.bottomMargin = dp(10);
            corpo.addView(painelFiltros, pf);
        }
        Button bf = new Button(this);
        bf.setText(deitado ? abertoTxt : fechadoTxt);
        bf.setAllCaps(false);
        bf.setTextColor(Color.BLACK);
        bf.setTypeface(Typeface.DEFAULT_BOLD);
        bf.setBackground(SeletorCor.fundo(AMARELO, 0, 6));
        bf.setPadding(dp(14), 0, dp(14), 0);
        bf.setOnClickListener(v -> {
            boolean abrir = painelFiltros.getVisibility() != View.VISIBLE;
            painelFiltros.setVisibility(abrir ? View.VISIBLE : View.GONE);
            bf.setText(abrir ? abertoTxt : fechadoTxt);
            if (deitado) {
                larguras = abrir ? LARGURAS : LARGURAS_SEM_FILTROS;
                ajustarLarguras();
            }
        });
        LinearLayout.LayoutParams pb = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(40));
        pb.leftMargin = dp(8);
        barra.addView(bf, pb);
        // "?" explica as colunas e o score
        Button ajuda = new Button(this);
        ajuda.setText("?");
        ajuda.setTextColor(Color.BLACK);
        ajuda.setTypeface(Typeface.DEFAULT_BOLD);
        ajuda.setBackground(SeletorCor.fundo(0xFFE6E6E6, 0, 6));
        ajuda.setPadding(0, 0, 0, 0);
        ajuda.setOnClickListener(v -> new android.app.AlertDialog.Builder(this)
            .setTitle("O que significa cada coluna")
            .setMessage(Score.AJUDA)
            .setPositiveButton("OK", null)
            .show());
        LinearLayout.LayoutParams pa = new LinearLayout.LayoutParams(dp(40), dp(40));
        pa.leftMargin = dp(8);
        barra.addView(ajuda, pa);
        LinearLayout tabela = new LinearLayout(this);
        tabela.setOrientation(LinearLayout.VERTICAL);
        tabela.setBackground(SeletorCor.fundo(VERDE_CARTAO, 0, 10));
        tabela.setPadding(dp(6), dp(6), dp(6), dp(6));
        cabecalho = new LinearLayout(this);
        cabecalho.setPadding(dp(6), dp(2), dp(6), dp(6));
        for (int i = 0; i < TITULOS.length; i++) {
            int col = i;
            TextView t = texto(TITULOS[i], 12, AMARELO, true);
            t.setGravity(gravidade(i));
            t.setSingleLine(true);
            t.setOnClickListener(v -> ordenar(col));
            cabecalho.addView(t, params(i));
        }
        ListView lista = new ListView(this);
        lista.setAdapter(adapter);
        lista.setDivider(null);
        // Cabecalho e lista juntos numa rolagem lateral: o nome ocupa o que sobra, com um minimo;
        // se nao couber tudo, a tabela rola para o lado em vez de cortar os campos
        larga = new LinearLayout(this);
        larga.setOrientation(LinearLayout.VERTICAL);
        larga.addView(cabecalho);
        larga.addView(lista, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        android.widget.HorizontalScrollView lado = new android.widget.HorizontalScrollView(this);
        lado.setFillViewport(true);
        lado.addView(larga, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
        lado.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> {
            if (r - l != visivelPx) { visivelPx = r - l; v.post(this::ajustarLarguras); }
        });
        tabela.addView(lado, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        conta = texto("", 11, CINZA_FAIXA, false);
        conta.setPadding(dp(4), dp(4), 0, 0);
        tabela.addView(conta);
        LinearLayout.LayoutParams pt = deitado ? new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1)
            : new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1);
        corpo.addView(tabela, pt);
        raiz.addView(corpo, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(raiz);
        Ui.areaSegura(this, VERDE_TOPO);

        listarSaves();
    }

    // ---- pecas ----

    private int dp(int v) { return Ui.dp(this, v); }

    private TextView texto(String t, int sp, int cor, boolean negrito) {
        TextView v = new TextView(this);
        v.setText(t);
        v.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        v.setTextColor(cor);
        if (negrito) v.setTypeface(Typeface.DEFAULT_BOLD);
        return v;
    }

    private Spinner spinnerBranco() {
        Spinner s = new Spinner(this, Spinner.MODE_DROPDOWN);
        s.setBackground(SeletorCor.fundo(0xFFFFFFFF, 0, 6));
        s.setPadding(0, 0, dp(4), 0); // sem seta: o padding do tema cortava o texto
        s.setPopupBackgroundDrawable(SeletorCor.fundo(VERDE_CARTAO, AMARELO, 6));
        s.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> { if (r - l > 0) s.setDropDownWidth(r - l); });
        return s;
    }

    // Spinner de textos: fechado preto no branco, aberto branco no verde
    private ArrayAdapter<String> adaptadorTexto(List<String> itens) {
        return new ArrayAdapter<String>(this, android.R.layout.simple_spinner_item, itens) {
            @Override public View getView(int pos, View v, ViewGroup pai) {
                TextView t = texto(getItem(pos), 14, Color.BLACK, false);
                t.setSingleLine(true);
                t.setEllipsize(TextUtils.TruncateAt.END);
                t.setPadding(dp(8), dp(6), dp(8), dp(6));
                return t;
            }
            @Override public View getDropDownView(int pos, View v, ViewGroup pai) {
                TextView t = texto(getItem(pos), 15, BRANCO, false);
                t.setPadding(dp(12), dp(10), dp(12), dp(10));
                return t;
            }
        };
    }

    private Bitmap bandeira(String codigo) {
        if (codigo == null || codigo.isEmpty()) return null;
        if (bandeiras.containsKey(codigo)) return bandeiras.get(codigo);
        File f = TeamCodec.caminho(jogo, "FLAGS", codigo + ".BMP");
        Bitmap b = f.exists() ? BitmapFactory.decodeFile(f.getPath()) : null;
        bandeiras.put(codigo, b);
        return b;
    }

    private String nomePais(String codigo) {
        TeamCodec.Pais p = porCodigo.get(codigo);
        return p != null ? p.nome : codigo;
    }

    private EditText campo(boolean numero) {
        EditText e = new EditText(this);
        e.setSingleLine(true);
        e.setTextColor(BRANCO);
        e.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        e.setBackgroundTintList(ColorStateList.valueOf(AMARELO));
        e.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        e.setInputType(numero ? InputType.TYPE_CLASS_NUMBER : InputType.TYPE_CLASS_TEXT);
        e.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) {}
            @Override public void onTextChanged(CharSequence s, int a, int b, int c) {}
            @Override public void afterTextChanged(Editable s) { filtrar(); }
        });
        return e;
    }

    private TextView rotulo(String t) {
        TextView r = texto(t, 13, CINZA, false);
        r.setPadding(0, dp(8), 0, dp(2));
        return r;
    }

    // Rotulo em cima do campo
    private View bloco(String rotulo, View campo) {
        LinearLayout b = new LinearLayout(this);
        b.setOrientation(LinearLayout.VERTICAL);
        b.addView(rotulo(rotulo));
        b.addView(campo, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
            campo instanceof Spinner ? dp(40) : ViewGroup.LayoutParams.WRAP_CONTENT));
        return b;
    }

    // Dois na mesma linha, meia largura cada
    private View par(View a, View b) {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.TOP);
        l.addView(a, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        View sep = new View(this);
        l.addView(sep, new LinearLayout.LayoutParams(dp(12), 1));
        if (b != null) l.addView(b, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        else l.addView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
        return l;
    }

    private CheckBox marcar(String t) {
        CheckBox c = new CheckBox(this);
        c.setText(t);
        c.setTextColor(BRANCO);
        c.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        c.setButtonTintList(ColorStateList.valueOf(AMARELO));
        c.setOnCheckedChangeListener((b, sim) -> filtrar());
        return c;
    }

    private View filtros() {
        LinearLayout f = new LinearLayout(this);
        f.setOrientation(LinearLayout.VERTICAL);
        f.setBackground(SeletorCor.fundo(VERDE_CARTAO, 0, 10));
        f.setPadding(dp(12), dp(8), dp(12), dp(12));
        TextView t = texto("FILTROS", 12, AMARELO, true);
        t.setLetterSpacing(0.1f);
        f.addView(t);
        f.addView(rotulo("Nome"));
        nome = campo(false);
        f.addView(nome);
        f.addView(rotulo("Posição"));
        LinearLayout pos = new LinearLayout(this);
        for (int i = 0; i < 4; i++) {
            int k = i;
            Button b = new Button(this);
            b.setText(TeamCodec.POSICOES_CURTAS[i]);
            b.setTypeface(Typeface.DEFAULT_BOLD);
            b.setMinWidth(0);
            b.setMinimumWidth(0);
            b.setMinHeight(0);
            b.setMinimumHeight(0);
            b.setPadding(0, 0, 0, 0);
            b.setStateListAnimator(null);
            b.setOnClickListener(v -> { posLigada[k] = !posLigada[k]; pintarPosicao(k); filtrar(); });
            posicoes[i] = b;
            pintarPosicao(i);
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(dp(40), dp(34));
            p.rightMargin = dp(5);
            pos.addView(b, p);
        }
        f.addView(pos);
        spPais = spinnerBranco();
        spDivisao = spinnerBranco();
        spEquipe = spinnerBranco();
        LinearLayout forca = new LinearLayout(this);
        forcaMin = campo(true);
        forcaMax = campo(true);
        forca.addView(forcaMin, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        View sep = new View(this);
        forca.addView(sep, new LinearLayout.LayoutParams(dp(12), 1));
        forca.addView(forcaMax, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        List<String> notas = new ArrayList<>(Arrays.asList("Qualquer"));
        for (int n = 2; n <= 10; n++) notas.add(n + " ou mais");
        List<String> lesoes = new ArrayList<>(Arrays.asList("Qualquer"));
        for (int n = 0; n <= 9; n++) lesoes.add("até " + n);
        List<String> comps = new ArrayList<>(Arrays.asList("Qualquer"));
        for (int c = 0; c < 6; c++) comps.add(c == 0 ? "Só Fair Play" : "Até " + SaveCodec.COMPORTAMENTOS[c]);
        spNota = spinnerBranco();
        spNota.setAdapter(adaptadorTexto(notas));
        spLesao = spinnerBranco();
        spLesao.setAdapter(adaptadorTexto(lesoes));
        spComp = spinnerBranco();
        spComp.setAdapter(adaptadorTexto(comps));
        for (Spinner s : new Spinner[] {spPais, spDivisao, spEquipe, spNota, spLesao, spComp})
            s.setOnItemSelectedListener(new Selecao(p -> filtrar()));
        soEstrelas = marcar("Só com estrela (✱)");
        soEstrangeiros = marcar("Só estrangeiros");
        semIndisponiveis = marcar("Esconder suspensos/lesionados");
        semHumanas = marcar("Esconder equipes humanas");

        View[] blocos = {
            bloco("País do jogador", spPais), bloco("Divisão", spDivisao),
            bloco("Equipe", spEquipe), bloco("Força (de / até)", forca),
            bloco("Nota", spNota), bloco("Lesão", spLesao),
            bloco("Comportamento", spComp),
        };
        CheckBox[] caixas = {soEstrelas, soEstrangeiros, semIndisponiveis, semHumanas};
        // Dois filtros por linha (deitado e em pe)
        for (int i = 0; i < blocos.length; i += 2) f.addView(par(blocos[i], i + 1 < blocos.length ? blocos[i + 1] : null));
        // Deitado o painel e estreito: uma caixa por linha; em pe cabem duas
        boolean painelEstreito = getResources().getDisplayMetrics().widthPixels > getResources().getDisplayMetrics().heightPixels;
        for (int i = 0; i < caixas.length; i += 2) {
            if (painelEstreito) { f.addView(caixas[i]); f.addView(caixas[i + 1]); }
            else f.addView(par(caixas[i], caixas[i + 1]));
        }
        Button limpar = new Button(this);
        limpar.setText("Limpar filtros");
        limpar.setAllCaps(false);
        limpar.setTextColor(Color.BLACK);
        limpar.setBackground(SeletorCor.fundo(0xFFE6E6E6, 0, 6));
        limpar.setOnClickListener(v -> limparFiltros());
        LinearLayout.LayoutParams pl = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(38));
        pl.topMargin = dp(8);
        f.addView(limpar, pl);
        ScrollView rolagem = new ScrollView(this);
        rolagem.addView(f);
        return rolagem;
    }

    private void pintarPosicao(int i) {
        Button b = posicoes[i];
        b.setBackground(SeletorCor.fundo(posLigada[i] ? CORES_POS[i] : DESLIGADO, posLigada[i] ? 0 : 0xFF4A7C4A, 6));
        b.setTextColor(posLigada[i] ? BRANCO : 0xFFA9C4A9);
    }

    private static int gravidade(int i) {
        return i == 1 || i == 7 || i == 13 || i == 14 ? Gravity.START | Gravity.CENTER_VERTICAL
            : i == 0 || i == 2 ? Gravity.CENTER : Gravity.END | Gravity.CENTER_VERTICAL;
    }

    private int margem(int i) { return i == 0 ? 0 : i == 13 ? dp(14) : dp(5); }

    private LinearLayout.LayoutParams params(int i) {
        if (larguras[i] < 0) return new LinearLayout.LayoutParams(0, 0);   // coluna escondida
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(larguras[i] == 0 ? nomePx : dp(larguras[i]), ViewGroup.LayoutParams.WRAP_CONTENT);
        p.leftMargin = margem(i);
        return p;
    }

    // Nome (largura 0 nas tabelas) fica com o espaco que sobra, no minimo NOME_MIN
    private void ajustarLarguras() {
        if (larga == null || visivelPx <= 0) return;
        int fixo = dp(12);
        for (int i = 0; i < larguras.length; i++) if (larguras[i] >= 0) fixo += margem(i) + dp(larguras[i]);
        nomePx = Math.max(dp(NOME_MIN), visivelPx - fixo);
        ViewGroup.LayoutParams lp = larga.getLayoutParams();
        lp.width = fixo + nomePx;
        larga.setLayoutParams(lp);
        for (int i = 0; i < cabecalho.getChildCount(); i++) cabecalho.getChildAt(i).setLayoutParams(params(i));
        adapter.notifyDataSetChanged();
    }

    private final class LinhasAdapter extends BaseAdapter {
        @Override public int getCount() { return visiveis.size(); }
        @Override public Object getItem(int i) { return visiveis.get(i); }
        @Override public long getItemId(int i) { return i; }

        @Override
        public View getView(int i, View v, ViewGroup pai) {
            Linha l = visiveis.get(i);
            SaveCodec.Jogador j = l.j;
            String sl = ((j.suspensao > 0 ? "S" + j.suspensao : "") + (j.jogosLesionado > 0 ? " L" + j.jogosLesionado : "")).trim();
            String sit = !sl.isEmpty() ? sl : l.situacao.equals("Estrangeiro") ? "Estrang." : l.situacao;
            LinearLayout linha = new LinearLayout(ScoutActivity.this);
            linha.setGravity(Gravity.CENTER_VERTICAL);
            linha.setPadding(dp(6), dp(7), dp(6), dp(7));
            linha.setBackgroundColor(i % 2 == 0 ? VERDE_LINHA : VERDE_CARTAO);
            String[] textos = {j.posicao, j.nome, j.estrela ? "✱" : "", Integer.toString(l.score), Integer.toString(j.forca), Integer.toString(j.nota),
                Integer.toString(j.lesao), SaveCodec.COMPORTAMENTOS[Math.max(0, Math.min(5, j.comportamento))], Score.textoImpacto(l.impacto),
                Integer.toString(j.jogos), Integer.toString(j.gols), Integer.toString(j.lesoes), Integer.toString(j.expulsoes), l.equipe.nomeCurto(), sit};
            for (int c = 0; c < textos.length; c++) {
                View celula;
                if (c == 1) {
                    LinearLayout n = new LinearLayout(ScoutActivity.this);
                    n.setGravity(Gravity.CENTER_VERTICAL);
                    ImageView img = new ImageView(ScoutActivity.this);
                    Bitmap b = bandeira(j.pais);
                    if (b != null) img.setImageBitmap(b);
                    img.setScaleType(ImageView.ScaleType.FIT_XY);
                    n.addView(img, new LinearLayout.LayoutParams(dp(21), dp(14)));
                    TextView t = texto(j.nome, 14, BRANCO, true);
                    t.setSingleLine(true);
                    t.setEllipsize(TextUtils.TruncateAt.END);
                    t.setPadding(dp(6), 0, 0, 0);
                    n.addView(t, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
                    celula = n;
                } else {
                    TextView t = texto(textos[c], c == 13 ? 13 : 14, BRANCO, c == 0 || c == 3 || c == 4);
                    t.setSingleLine(true);
                    t.setEllipsize(TextUtils.TruncateAt.END);
                    t.setGravity(gravidade(c));
                    if (c == 0) {
                        int p = Math.max(0, l.pos);
                        t.setBackground(SeletorCor.fundo(CORES_POS[p], 0, 4));
                    }
                    if (c == 13 && l.humana) t.setTextColor(AMARELO);
                    if (c == 3) t.setTextColor(OURO);
                    if (c == 8) t.setTextColor(l.impacto > 0.004 ? OK : l.impacto < -0.004 ? ALERTA : BRANCO);
                    if (c == 4 && j.forca > SaveCodec.FORCA_AVISO_ACIMA) t.setTextColor(AMARELO);
                    if (c == 2) t.setTextColor(OURO);
                    if (c == 14) t.setTextColor(!sl.isEmpty() || l.situacao.equals("Estrangeiro") ? ALERTA : OK);
                    celula = t;
                }
                linha.addView(celula, params(c));
            }
            return linha;
        }
    }

    // ---- dados ----

    private void listarSaves() {
        saves.clear();
        String[] nomes = pastaJogos.list((d, n) -> n.toLowerCase(Locale.ROOT).endsWith(".e98"));
        if (nomes != null) {
            Arrays.sort(nomes);
            saves.addAll(Arrays.asList(nomes));
        }
        ((ArrayAdapter<?>) spSave.getAdapter()).notifyDataSetChanged();
        if (saves.isEmpty()) conta.setText("Nenhum jogo gravado em JOGOS ainda.");
        else carregarSave();
    }

    private static int ordemDivisao(String d) {
        if (d.startsWith("1")) return 1;
        if (d.startsWith("2")) return 2;
        if (d.startsWith("3")) return 3;
        if (d.startsWith("4")) return 4;
        return d.startsWith("Distrital") ? 5 : 9;
    }

    private void carregarSave() {
        int i = spSave.getSelectedItemPosition();
        if (i < 0 || i >= saves.size()) return;
        SaveCodec.Save sf;
        try {
            sf = SaveCodec.ler(new File(pastaJogos, saves.get(i)));
        } catch (Exception e) {
            todos = new ArrayList<>();
            filtrar();
            conta.setText("Não consegui ler o save: " + e.getMessage());
            return;
        }
        Set<SaveCodec.Time> humanas = new HashSet<>();
        for (SaveCodec.Tecnico t : sf.tecnicos) if (t.humano && sf.timeDoTecnico(t) != null) humanas.add(sf.timeDoTecnico(t));
        todos = new ArrayList<>();
        for (SaveCodec.Time t : sf.times)
            for (SaveCodec.Jogador j : t.jogadores) {
                Linha l = new Linha();
                l.j = j;
                l.equipe = t;
                l.pos = Arrays.asList(TeamCodec.POSICOES_CURTAS).indexOf(j.posicao);
                l.ordemDivisao = ordemDivisao(t.divisao);
                l.situacao = regras.situacao(j.pais, t.pais);
                l.humana = humanas.contains(t);
                l.score = Score.rendimento(j.posicao, j.nota, j.lesao, j.comportamento);
                l.impacto = Score.impacto(j.posicao, j.nota, j.lesao, j.comportamento);
                todos.add(l);
            }

        montando = true;
        Collator col = Collator.getInstance(BR);
        Map<String, Integer> conta = new LinkedHashMap<>();
        for (Linha l : todos) conta.merge(l.j.pais, 1, Integer::sum);
        List<String> codigos = new ArrayList<>(conta.keySet());
        codigos.sort((a, b) -> col.compare(nomePais(a), nomePais(b)));
        paisesFiltro.clear();
        List<Object[]> itens = new ArrayList<>();
        itens.add(new Object[] {null, "Todos"});
        paisesFiltro.add(null);
        for (String c : codigos) {
            paisesFiltro.add(c);
            itens.add(new Object[] {c, nomePais(c) + " (" + conta.get(c) + ")"});
        }
        spPais.setAdapter(adaptadorBandeiras(itens));
        divisoesFiltro.clear();
        divisoesFiltro.add("Todas");
        for (SaveCodec.Time t : sf.times) if (!t.divisao.isEmpty() && !divisoesFiltro.contains(t.divisao)) divisoesFiltro.add(t.divisao);
        divisoesFiltro.subList(1, divisoesFiltro.size()).sort(Comparator.comparingInt(ScoutActivity::ordemDivisao));
        spDivisao.setAdapter(adaptadorTexto(new ArrayList<>(divisoesFiltro)));
        equipesFiltro.clear();
        equipesFiltro.addAll(sf.times);
        equipesFiltro.sort((a, b) -> col.compare(a.nomeCurto(), b.nomeCurto()));
        List<Object[]> eqs = new ArrayList<>();
        eqs.add(new Object[] {null, "Todas"});
        for (SaveCodec.Time t : equipesFiltro) eqs.add(new Object[] {t.pais, (humanas.contains(t) ? "★ " : "") + t.nomeCurto()});
        spEquipe.setAdapter(adaptadorBandeiras(eqs));
        montando = false;
        filtrar();
    }

    private ArrayAdapter<Object[]> adaptadorBandeiras(List<Object[]> itens) {
        return new ArrayAdapter<Object[]>(this, android.R.layout.simple_spinner_item, itens) {
            private View linha(int pos, int cor, int padV) {
                Object[] it = getItem(pos);
                LinearLayout l = new LinearLayout(ScoutActivity.this);
                l.setGravity(Gravity.CENTER_VERTICAL);
                l.setPadding(dp(8), dp(padV), dp(8), dp(padV));
                ImageView img = new ImageView(ScoutActivity.this);
                Bitmap b = bandeira((String) it[0]);
                if (b != null) img.setImageBitmap(b);
                else img.setVisibility(View.GONE); // "Todos" sem bandeira nao perde espaco
                img.setScaleType(ImageView.ScaleType.FIT_XY);
                l.addView(img, new LinearLayout.LayoutParams(dp(24), dp(16)));
                TextView t = texto((String) it[1], 14, cor, false);
                t.setSingleLine(true);
                t.setEllipsize(TextUtils.TruncateAt.END);
                t.setPadding(dp(8), 0, 0, 0);
                l.addView(t);
                return l;
            }
            @Override public View getView(int pos, View v, ViewGroup pai) { return linha(pos, Color.BLACK, 4); }
            @Override public View getDropDownView(int pos, View v, ViewGroup pai) { return linha(pos, BRANCO, 10); }
        };
    }

    private void limparFiltros() {
        montando = true;
        nome.setText("");
        forcaMin.setText("");
        forcaMax.setText("");
        for (int i = 0; i < 4; i++) { posLigada[i] = true; pintarPosicao(i); }
        for (Spinner s : new Spinner[] {spPais, spDivisao, spEquipe, spNota, spLesao, spComp}) if (s.getCount() > 0) s.setSelection(0);
        for (CheckBox c : new CheckBox[] {soEstrelas, soEstrangeiros, semIndisponiveis, semHumanas}) c.setChecked(false);
        criterios.clear();
        criterios.add(new int[] {4, 1});
        montando = false;
        filtrar();
    }

    private static Integer numero(EditText e) {
        try { return Integer.parseInt(e.getText().toString().trim()); } catch (NumberFormatException x) { return null; }
    }

    private void filtrar() {
        if (montando || nome == null || conta == null) return;
        String termo = nome.getText().toString().trim().toLowerCase(BR);
        int ip = spPais.getSelectedItemPosition();
        String pais = ip > 0 && ip < paisesFiltro.size() ? paisesFiltro.get(ip) : null;
        int id = spDivisao.getSelectedItemPosition();
        String divisao = id > 0 && id < divisoesFiltro.size() ? divisoesFiltro.get(id) : null;
        int ie = spEquipe.getSelectedItemPosition();
        SaveCodec.Time equipe = ie > 0 && ie - 1 < equipesFiltro.size() ? equipesFiltro.get(ie - 1) : null;
        Integer fMin = numero(forcaMin), fMax = numero(forcaMax);
        int notaMin = spNota.getSelectedItemPosition() > 0 ? spNota.getSelectedItemPosition() + 1 : 0;
        int lesaoMax = spLesao.getSelectedItemPosition() > 0 ? spLesao.getSelectedItemPosition() - 1 : 10;
        int compMax = spComp.getSelectedItemPosition() > 0 ? spComp.getSelectedItemPosition() - 1 : 5;
        visiveis.clear();
        for (Linha l : todos) {
            SaveCodec.Jogador j = l.j;
            if (!termo.isEmpty() && !j.nome.toLowerCase(BR).contains(termo)) continue;
            if (l.pos < 0 || !posLigada[l.pos]) continue;
            if (pais != null && !j.pais.equals(pais)) continue;
            if (divisao != null && !l.equipe.divisao.equals(divisao)) continue;
            if (equipe != null && l.equipe != equipe) continue;
            if (fMin != null && j.forca < fMin) continue;
            if (fMax != null && j.forca > fMax) continue;
            if (j.nota < notaMin || j.lesao > lesaoMax || j.comportamento > compMax) continue;
            if (soEstrelas.isChecked() && !j.estrela) continue;
            if (soEstrangeiros.isChecked() && !l.situacao.equals("Estrangeiro")) continue;
            if (semIndisponiveis.isChecked() && (j.suspensao > 0 || j.jogosLesionado > 0)) continue;
            if (semHumanas.isChecked() && l.humana) continue;
            visiveis.add(l);
        }
        Comparator<Linha> c = null;
        for (int[] cr : criterios) {
            Comparator<Linha> k = comparador(cr[0]);
            if (cr[1] == 1) k = k.reversed();
            c = c == null ? k : c.thenComparing(k);
        }
        // Desempate final: forca, depois nota
        c = c.thenComparing(Comparator.comparingInt((Linha x) -> x.j.forca).reversed()).thenComparing(Comparator.comparingInt((Linha x) -> x.j.nota).reversed());
        visiveis.sort(c);
        adapter.notifyDataSetChanged();
        for (int i = 0; i < TITULOS.length; i++) {
            String marca = "";
            for (int n = 0; n < criterios.size(); n++)
                if (criterios.get(n)[0] == i)
                    marca = (criterios.size() > 1 ? String.valueOf(SOBRESCRITOS.charAt(Math.min(n, 8))) : "") + (criterios.get(n)[1] == 1 ? "▼" : "▲");
            ((TextView) cabecalho.getChildAt(i)).setText(TITULOS[i] + marca);
        }
        if (!todos.isEmpty()) conta.setText(visiveis.size() + " de " + todos.size()
            + " jogadores · toque nos títulos para ordenar por várias colunas (3º toque tira a coluna)");
    }

    private Comparator<Linha> comparador(int coluna) {
        Collator col = Collator.getInstance(BR);
        switch (coluna) {
            case 0: return Comparator.comparingInt(x -> x.pos);
            case 1: return (a, b) -> col.compare(a.j.nome, b.j.nome);
            case 2: return Comparator.comparingInt(x -> x.j.estrela ? 1 : 0);
            case 3: return Comparator.comparingInt(x -> x.score);
            case 5: return Comparator.comparingInt(x -> x.j.nota);
            case 6: return Comparator.comparingInt(x -> x.j.lesao);
            case 7: return Comparator.comparingInt(x -> x.j.comportamento);
            case 8: return Comparator.comparingDouble(x -> x.impacto);
            case 9: return Comparator.comparingInt(x -> x.j.jogos);
            case 10: return Comparator.comparingInt(x -> x.j.gols);
            case 11: return Comparator.comparingInt(x -> x.j.lesoes);
            case 12: return Comparator.comparingInt(x -> x.j.expulsoes);
            case 13: return (a, b) -> col.compare(a.equipe.nomeCurto(), b.equipe.nomeCurto());
            case 14: return (a, b) -> a.situacao.compareTo(b.situacao);
            default: return Comparator.comparingInt(x -> x.j.forca);
        }
    }

    // Numeros comecam do maior; textos e lesao/comportamento, do menor
    private static int sentidoPadrao(int coluna) { return coluna == 2 || coluna == 3 || coluna == 4 || coluna == 5 || coluna == 8 || coluna == 9 || coluna == 10 ? 1 : 0; }

    // 1o toque: entra como proximo criterio; 2o: inverte; 3o: sai
    private void ordenar(int coluna) {
        int[] achado = null;
        for (int[] c : criterios) if (c[0] == coluna) achado = c;
        if (achado == null) criterios.add(new int[] {coluna, sentidoPadrao(coluna)});
        else if (achado[1] == sentidoPadrao(coluna)) achado[1] = 1 - achado[1];
        else criterios.remove(achado);
        if (criterios.isEmpty()) criterios.add(new int[] {4, 1});
        filtrar();
    }

    private final class Selecao implements AdapterView.OnItemSelectedListener {
        private final Acao acao;
        Selecao(Acao a) { acao = a; }
        @Override public void onItemSelected(AdapterView<?> p, View v, int pos, long id) { if (!montando) acao.em(pos); }
        @Override public void onNothingSelected(AdapterView<?> p) {}
    }

    private interface Acao { void em(int pos); }
}
