package io.github.juansilveira.elifoot98;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.content.res.ColorStateList;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Bundle;
import android.text.Editable;
import android.text.InputFilter;
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
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.ScrollView;
import android.widget.Spinner;
import android.widget.TextView;

import java.io.File;
import java.text.Collator;
import java.text.Normalizer;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * Editor de Equipes nativo (substitui o EDITEQ.EXE). Esquerda: busca e filtro por
 * pais das equipes de EQUIPAS. Direita, em duas abas: dados da equipe (com as
 * regras do jogo, inclusive estrangeiros pelo BOSMAN.TXE/PLOP.TXE) e a tabela de
 * jogadores com os atributos que o jogo tira do nome.
 */
public class TeamEditorActivity extends Activity {
    private static final int VERDE = 0xFF0B3D0B, VERDE_TOPO = 0xFF062606, VERDE_CARTAO = 0xFF145214, VERDE_LINHA = 0xFF114A11,
        SELECAO = 0xFF2E7D32, AMARELO = 0xFFFCFE04, BRANCO = 0xFFFFFFFF, CINZA = 0xFFB8C9B8, CINZA_FAIXA = 0xFF8FA88F,
        ALERTA = 0xFFFF8A80, OK = 0xFF9CE29C, OURO = 0xFFFFD54F;
    private static final Locale BR = new Locale("pt", "BR");
    private static final String[] COMPORTAMENTOS = SaveCodec.COMPORTAMENTOS;

    private File jogo, equipasDir;
    private List<TeamCodec.Pais> paises = new ArrayList<>();
    private final Map<String, TeamCodec.Pais> porCodigo = new HashMap<>();
    private final Map<String, Bitmap> bandeiras = new HashMap<>();
    private TeamCodec.Regras regras;
    private boolean liberado;

    private final List<TeamCodec.Equipe> equipes = new ArrayList<>();
    private final List<TeamCodec.Equipe> visiveis = new ArrayList<>();
    private int ilegiveis;
    private TeamCodec.Equipe atual;
    private boolean alterado, carregando;

    private EditText busca, nomeCompleto, nomeAbreviado, treinador;
    private Spinner filtro, spPais;
    private final List<Object[]> itensFiltro = new ArrayList<>();   // {codigo ou null, texto}
    private ListView listaEquipes, listaJogadores;
    private final EquipesAdapter adapterEquipes = new EquipesAdapter();
    private final JogadoresAdapter adapterJogadores = new JogadoresAdapter();
    private TextView contaEquipes, nivel, previa, hexLetra, hexFundo, arquivo, resumo, tituloBarra;
    private View amostraLetra, amostraFundo;
    private LinearLayout problemas, painelDireito;
    private Button salvar, abaEquipe, abaJogadores, adicionar, editar, remover, transferir, nivelMenos, nivelMais;
    private View vistaEquipe, vistaJogadores;
    private int jogadorSelecionado = -1;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        jogo = new Jogo(this).jogo;
        equipasDir = TeamCodec.caminho(jogo, "EQUIPAS");
        try {
            paises = TeamCodec.lerPaises(jogo);
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Não consegui ler COUNTRY.TXE:\n" + e.getMessage());
        }
        Collator c = Collator.getInstance(BR);
        paises.sort((a, b) -> c.compare(a.nome, b.nome));
        for (TeamCodec.Pais p : paises) porCodigo.put(p.codigo, p);
        regras = new TeamCodec.Regras(jogo, paises);
        liberado = TeamCodec.bosmanLiberado(jogo);

        montarTela();
        carregarEquipes();
        mostrarAba(true);
        mostrarEquipe();
    }

    // Deitado ou em pe: o usuario escolhe girando o aparelho. A tela e montada de
    // novo, mas a equipe aberta, as alteracoes e os filtros continuam.
    @Override
    public void onConfigurationChanged(android.content.res.Configuration nova) {
        super.onConfigurationChanged(nova);
        String termo = busca.getText().toString();
        int f = filtro.getSelectedItemPosition();
        String codigoFiltro = f >= 0 && f < itensFiltro.size() ? (String) itensFiltro.get(f)[0] : null;
        boolean aba = abaEquipeAtiva;
        montarTela();
        montarFiltro();
        for (int i = 0; i < itensFiltro.size(); i++)
            if (java.util.Objects.equals(itensFiltro.get(i)[0], codigoFiltro)) { carregando = true; filtro.setSelection(i); carregando = false; }
        busca.setText(termo);
        filtrar();
        mostrarAba(aba);
        mostrarEquipe();
        if (alterado) alterou();
    }

    private boolean deitado() {
        return getResources().getDisplayMetrics().widthPixels > getResources().getDisplayMetrics().heightPixels;
    }

    private void montarTela() {
        boolean deitado = deitado();
        larguras = deitado ? LARGURAS : LARGURAS_EM_PE;
        LinearLayout raiz = new LinearLayout(this);
        raiz.setOrientation(LinearLayout.VERTICAL);
        raiz.setBackgroundColor(VERDE);
        raiz.addView(barraTopo(deitado));

        LinearLayout corpo = new LinearLayout(this);
        corpo.setPadding(dp(10), dp(10), dp(10), dp(10));
        painelDireito = colunaDireita();
        if (deitado) {
            corpo.addView(colunaEquipes(), new LinearLayout.LayoutParams(dp(240), ViewGroup.LayoutParams.MATCH_PARENT));
            LinearLayout.LayoutParams pd = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, 1);
            pd.leftMargin = dp(10);
            corpo.addView(painelDireito, pd);
        } else {
            // Em pe: equipes em cima (um terco da tela), abas embaixo
            corpo.setOrientation(LinearLayout.VERTICAL);
            corpo.addView(colunaEquipes(), new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, getResources().getDisplayMetrics().heightPixels * 2 / 5));
            LinearLayout.LayoutParams pd = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1);
            pd.topMargin = dp(10);
            corpo.addView(painelDireito, pd);
        }
        raiz.addView(corpo, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(raiz);
    }

    @Override
    public void onBackPressed() {
        if (!alterado) { super.onBackPressed(); return; }
        confirmar("Há alterações não salvas nesta equipe. Sair mesmo assim?", () -> { alterado = false; finish(); });
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
        b.setTag(amarelo);
        return b;
    }

    // Desativado legivel sobre o verde: fundo verde medio, texto claro, borda
    private void habilitar(Button b, boolean sim) {
        b.setEnabled(sim);
        boolean amarelo = Boolean.TRUE.equals(b.getTag());
        b.setBackground(sim ? SeletorCor.fundo(amarelo ? AMARELO : 0xFFE6E6E6, 0, 6) : SeletorCor.fundo(0xFF2A5C2A, 0xFF4A7C4A, 6));
        b.setTextColor(sim ? Color.BLACK : 0xFFA9C4A9);
    }

    private LinearLayout cartao(String titulo) {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setBackground(SeletorCor.fundo(VERDE_CARTAO, 0, 10));
        c.setPadding(dp(12), dp(8), dp(12), dp(10));
        if (titulo != null) {
            TextView t = texto(titulo.toUpperCase(BR), 12, AMARELO, true);
            t.setLetterSpacing(0.1f);
            t.setPadding(0, 0, 0, dp(4));
            c.addView(t);
        }
        return c;
    }

    private EditText campo(int max, boolean maiusculas) {
        EditText e = new EditText(this);
        e.setSingleLine(true);
        e.setTextColor(BRANCO);
        e.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
        e.setBackgroundTintList(ColorStateList.valueOf(AMARELO));
        e.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        e.setInputType(InputType.TYPE_CLASS_TEXT | (maiusculas ? InputType.TYPE_TEXT_FLAG_CAP_CHARACTERS : InputType.TYPE_TEXT_FLAG_CAP_WORDS));
        e.setFilters(maiusculas ? new InputFilter[] {new InputFilter.LengthFilter(max), new InputFilter.AllCaps()}
            : new InputFilter[] {new InputFilter.LengthFilter(max)});
        return e;
    }

    // Rotulo com a explicacao embaixo, em letra menor
    private LinearLayout linhaCampo(String rotulo, String faixa, View campo) { return linhaCampo(rotulo, faixa, campo, deitado() ? 112 : 92); }

    private LinearLayout linhaCampo(String rotulo, String faixa, View campo, int largura) {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout r = new LinearLayout(this);
        r.setOrientation(LinearLayout.VERTICAL);
        r.addView(texto(rotulo, 14, CINZA, false));
        r.addView(texto(faixa, 11, CINZA_FAIXA, false));
        l.addView(r, new LinearLayout.LayoutParams(dp(largura), ViewGroup.LayoutParams.WRAP_CONTENT));
        l.addView(campo, campo instanceof Spinner ? new LinearLayout.LayoutParams(dp(300), dp(42))
            : campo instanceof EditText ? new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1)
            : new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        l.setPadding(0, dp(2), 0, dp(2));
        return l;
    }

    private Bitmap bandeira(String codigo) {
        if (codigo == null || codigo.isEmpty()) return null;
        if (bandeiras.containsKey(codigo)) return bandeiras.get(codigo);
        Bitmap b = null;
        File f = TeamCodec.caminho(jogo, "FLAGS", codigo + ".BMP");
        if (f.exists()) b = BitmapFactory.decodeFile(f.getPath());
        bandeiras.put(codigo, b);
        return b;
    }

    private ImageView imagemBandeira(String codigo, int largura) {
        ImageView i = new ImageView(this);
        Bitmap b = bandeira(codigo);
        if (b != null) i.setImageBitmap(b);
        i.setScaleType(ImageView.ScaleType.FIT_XY);
        i.setLayoutParams(new LinearLayout.LayoutParams(dp(largura), dp(largura * 2 / 3)));
        return i;
    }

    private LinearLayout linhaBandeira(String codigo, String t, int cor) {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.CENTER_VERTICAL);
        l.setPadding(dp(8), dp(6), dp(8), dp(6));
        l.addView(imagemBandeira(codigo, 24));
        TextView tv = texto(t, 15, cor, false);
        tv.setPadding(dp(10), 0, 0, 0);
        tv.setSingleLine(true);
        tv.setEllipsize(TextUtils.TruncateAt.END);
        l.addView(tv, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        return l;
    }

    /** Spinner de paises (ou do filtro) com bandeira. */
    private ArrayAdapter<Object[]> adaptadorBandeiras(List<Object[]> itens) {
        return new ArrayAdapter<Object[]>(this, android.R.layout.simple_spinner_item, itens) {
            @Override public View getView(int pos, View v, ViewGroup pai) {
                Object[] it = getItem(pos);
                LinearLayout l = linhaBandeira((String) it[0], (String) it[1], Color.BLACK);
                l.setPadding(dp(6), dp(2), dp(6), dp(2));
                return l;
            }
            @Override public View getDropDownView(int pos, View v, ViewGroup pai) {
                // Lista aberta: verde do jogo, texto branco, bandeira maior
                Object[] it = getItem(pos);
                LinearLayout l = linhaBandeira((String) it[0], (String) it[1], BRANCO);
                l.setPadding(dp(12), dp(10), dp(12), dp(10));
                if (it[0] == null) ((TextView) l.getChildAt(1)).setTypeface(Typeface.DEFAULT_BOLD);
                return l;
            }
        };
    }

    private List<Object[]> itensPaises() {
        List<Object[]> l = new ArrayList<>();
        for (TeamCodec.Pais p : paises) l.add(new Object[] {p.codigo, p.nome});
        return l;
    }

    private Spinner spinnerBranco() {
        Spinner s = new Spinner(this, Spinner.MODE_DROPDOWN);
        s.setBackground(SeletorCor.fundo(0xFFFFFFFF, 0, 6));
        s.setPopupBackgroundDrawable(SeletorCor.fundo(VERDE_CARTAO, AMARELO, 6));
        // Lista aberta da mesma largura do botao
        s.addOnLayoutChangeListener((v, l, t, r, b, ol, ot, or, ob) -> { if (r - l > 0) s.setDropDownWidth(r - l); });
        return s;
    }

    private int indicePais(String codigo) {
        for (int i = 0; i < paises.size(); i++) if (paises.get(i).codigo.equals(codigo)) return i;
        return -1;
    }

    // ---- montagem ----

    private View barraTopo(boolean deitado) {
        LinearLayout barra = new LinearLayout(this);
        barra.setGravity(Gravity.CENTER_VERTICAL);
        barra.setBackgroundColor(VERDE_TOPO);
        barra.setPadding(dp(16), dp(8), dp(12), dp(8));
        tituloBarra = texto(deitado ? "Editor de Equipes" : "Equipes", deitado ? 20 : 18, AMARELO, true);
        barra.addView(tituloBarra, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        Button original = botao(deitado ? "Editor original" : "Original", false);
        original.setOnClickListener(v -> startActivity(new Intent(this, ElifootActivity.class).putExtra(ElifootActivity.EXTRA_EXE, "EDITEQ.EXE")));
        Button nova = botao(deitado ? "Nova equipe" : "Nova", false);
        nova.setOnClickListener(v -> novaEquipe());
        salvar = botao("Salvar", true);
        salvar.setOnClickListener(v -> salvar(null));
        for (Button b : new Button[] {original, nova, salvar}) {
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(40));
            p.leftMargin = dp(8);
            barra.addView(b, p);
        }
        return barra;
    }

    // Em pe: busca e filtro ficam atras de uma faixa que abre e fecha (a lista ganha espaco)
    private boolean buscaAberta;
    private Button faixaBusca;
    private View cartaoBusca;

    private void atualizarFaixaBusca() {
        if (faixaBusca == null) return;
        int f = filtro.getSelectedItemPosition();
        boolean ativo = busca.getText().length() > 0 || f > 0;
        faixaBusca.setText((buscaAberta ? "Buscar e filtrar ▴" : "Buscar e filtrar ▾") + (ativo && !buscaAberta ? "  (filtro ativo)" : ""));
        cartaoBusca.setVisibility(buscaAberta ? View.VISIBLE : View.GONE);
    }

    private View colunaEquipes() {
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        LinearLayout c = cartao("Equipes");
        cartaoBusca = c;
        faixaBusca = null;
        if (!deitado()) {
            faixaBusca = botao("", false);
            faixaBusca.setOnClickListener(v -> { buscaAberta = !buscaAberta; atualizarFaixaBusca(); });
            LinearLayout.LayoutParams pfb = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(36));
            pfb.bottomMargin = dp(8);
            col.addView(faixaBusca, pfb);
        }
        busca = campo(30, false);
        busca.setHint("Buscar equipe ou jogador");
        busca.setHintTextColor(CINZA_FAIXA);
        busca.addTextChangedListener(new Mudou(this::filtrar));
        c.addView(busca);
        filtro = spinnerBranco();
        filtro.setAdapter(adaptadorBandeiras(itensFiltro));
        filtro.setOnItemSelectedListener(new Selecao(pos -> filtrar()));
        LinearLayout.LayoutParams pf = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(42));
        pf.topMargin = dp(6);
        c.addView(filtro, pf);
        contaEquipes = texto("", 11, CINZA_FAIXA, false);
        contaEquipes.setPadding(0, dp(6), 0, 0);
        c.addView(contaEquipes);
        col.addView(c);

        listaEquipes = new ListView(this);
        listaEquipes.setAdapter(adapterEquipes);
        listaEquipes.setDivider(null);
        listaEquipes.setBackground(SeletorCor.fundo(VERDE_CARTAO, 0, 10));
        listaEquipes.setOnItemClickListener((p, v, pos, id) -> escolherEquipe(visiveis.get(pos)));
        LinearLayout.LayoutParams pl = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1);
        pl.topMargin = dp(10);
        col.addView(listaEquipes, pl);
        if (faixaBusca != null) {
            pl.topMargin = buscaAberta ? dp(10) : 0;
            atualizarFaixaBusca();
        }
        return col;
    }

    private LinearLayout colunaDireita() {
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);

        // Abas + resumo das regras
        LinearLayout abas = new LinearLayout(this);
        abas.setGravity(Gravity.CENTER_VERTICAL);
        abaEquipe = botao("Equipe", false);
        abaJogadores = botao("Jogadores", false);
        abaEquipe.setOnClickListener(v -> mostrarAba(true));
        abaJogadores.setOnClickListener(v -> mostrarAba(false));
        abas.addView(abaEquipe, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(38)));
        LinearLayout.LayoutParams pa = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(38));
        pa.leftMargin = dp(6);
        abas.addView(abaJogadores, pa);
        resumo = texto("", 13, BRANCO, false);
        resumo.setPadding(dp(12), 0, 0, 0);
        resumo.setMaxLines(2);
        resumo.setEllipsize(TextUtils.TruncateAt.END);
        resumo.setOnClickListener(v -> mostrarProblemas());
        abas.addView(resumo, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        col.addView(abas);

        FrameLayout conteudo = new FrameLayout(this);
        vistaEquipe = vistaEquipe();
        vistaJogadores = vistaJogadores();
        conteudo.addView(vistaEquipe);
        conteudo.addView(vistaJogadores);
        LinearLayout.LayoutParams pc = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1);
        pc.topMargin = dp(8);
        col.addView(conteudo, pc);
        return col;
    }

    private View vistaEquipe() {
        LinearLayout v = new LinearLayout(this);
        v.setOrientation(LinearLayout.VERTICAL);

        LinearLayout dados = cartao(null);
        LinearLayout topo = new LinearLayout(this);
        topo.setGravity(Gravity.CENTER_VERTICAL);
        previa = texto("", 17, BRANCO, true);
        previa.setGravity(Gravity.CENTER);
        previa.setPadding(dp(12), dp(6), dp(12), dp(6));
        topo.addView(previa, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        arquivo = texto("", 11, CINZA_FAIXA, false);
        arquivo.setPadding(dp(10), 0, 0, 0);
        topo.addView(arquivo);
        dados.addView(topo);

        // Duas colunas de campos
        LinearLayout colunas = new LinearLayout(this);
        LinearLayout c1 = new LinearLayout(this);
        c1.setOrientation(LinearLayout.VERTICAL);
        nomeCompleto = campo(TeamCodec.MAX_NOME_COMPLETO, true);
        nomeAbreviado = campo(TeamCodec.MAX_NOME, true);
        treinador = campo(TeamCodec.MAX_NOME, false);
        for (EditText e : new EditText[] {nomeCompleto, nomeAbreviado, treinador}) e.addTextChangedListener(new Mudou(this::camposMudaram));
        c1.addView(linhaCampo("Nome completo", "até 40 letras", nomeCompleto));
        c1.addView(linhaCampo("Abreviado", "até 20 letras", nomeAbreviado));
        c1.addView(linhaCampo("Treinador", "obrigatório", treinador));
        spPais = spinnerBranco();
        spPais.setAdapter(adaptadorBandeiras(itensPaises()));
        spPais.setOnItemSelectedListener(new Selecao(pos -> camposMudaram()));
        c1.addView(linhaCampo("País", "da equipe", spPais));
        nivelMenos = botao("−", true);
        nivelMais = botao("+", true);
        nivelMenos.setOnClickListener(x -> mudarNivel(-1));
        nivelMais.setOnClickListener(x -> mudarNivel(1));
        nivel = texto("", 16, BRANCO, true);
        nivel.setGravity(Gravity.CENTER);
        LinearLayout nv = new LinearLayout(this);
        nv.setGravity(Gravity.CENTER_VERTICAL);
        nv.addView(nivelMenos, new LinearLayout.LayoutParams(dp(38), dp(36)));
        nv.addView(nivel, new LinearLayout.LayoutParams(dp(50), ViewGroup.LayoutParams.WRAP_CONTENT));
        nv.addView(nivelMais, new LinearLayout.LayoutParams(dp(38), dp(36)));
        c1.addView(linhaCampo("Nível inicial", "20 = 1ª divisão no jogo novo", nv));
        amostraLetra = new View(this);
        amostraFundo = new View(this);
        hexLetra = texto("", 11, CINZA, false);
        hexFundo = texto("", 11, CINZA, false);
        hexLetra.setSingleLine(true);
        hexFundo.setSingleLine(true);
        LinearLayout cores = new LinearLayout(this);
        int larguraCor = deitado() ? 130 : 118;
        cores.addView(botaoCor("Letra", amostraLetra, hexLetra, true), new LinearLayout.LayoutParams(dp(larguraCor), dp(44)));
        View sep = new View(this);
        cores.addView(sep, new LinearLayout.LayoutParams(dp(6), 1));
        cores.addView(botaoCor("Fundo", amostraFundo, hexFundo, false), new LinearLayout.LayoutParams(dp(larguraCor), dp(44)));
        // Um campo por linha: o nome completo pode ter 40 letras
        LinearLayout coresLinha = new LinearLayout(this);
        coresLinha.addView(cores, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(44)));
        c1.addView(linhaCampo("Cores", "letra e fundo", coresLinha));
        colunas.addView(c1, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        dados.addView(colunas);
        v.addView(dados);

        LinearLayout regrasCartao = cartao(liberado ? "Regras do jogo (estrangeiros liberados)" : "Regras do jogo");
        problemas = new LinearLayout(this);
        problemas.setOrientation(LinearLayout.VERTICAL);
        regrasCartao.addView(problemas);
        LinearLayout.LayoutParams pr = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        pr.topMargin = dp(10);
        v.addView(regrasCartao, pr);

        ScrollView rolagem = new ScrollView(this);
        rolagem.addView(v);
        return rolagem;
    }

    private View botaoCor(String nome, View amostra, TextView hex, boolean letra) {
        LinearLayout b = new LinearLayout(this);
        b.setGravity(Gravity.CENTER_VERTICAL);
        b.setPadding(dp(6), 0, dp(4), 0);
        b.setBackground(SeletorCor.fundo(VERDE_LINHA, 0xFF2E6B2E, 6));
        b.addView(amostra, new LinearLayout.LayoutParams(dp(22), dp(22)));
        LinearLayout t = new LinearLayout(this);
        t.setOrientation(LinearLayout.VERTICAL);
        t.setPadding(dp(6), 0, 0, 0);
        t.addView(texto(nome, 13, BRANCO, true));
        // O codigo comeca como "—": sem ocupar a largura toda, cortaria "#FFFFFF"
        t.addView(hex, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        b.addView(t, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        b.setOnClickListener(x -> {
            if (atual == null) return;
            SeletorCor.escolher(this, letra ? "Cor da letra" : "Cor do fundo", letra ? atual.corLetra : atual.corFundo, rgb -> {
                if (letra) atual.corLetra = rgb; else atual.corFundo = rgb;
                alterou();
                pintarCores();
            });
        });
        return b;
    }

    // Colunas da tabela: posicao, nome, pais, nota, lesao, comportamento, estrela, situacao
    private static final int[] LARGURAS = {30, 0, 54, 34, 38, 84, 16, 92};
    // Em pe: sem comportamento (-1 = escondida; aparece na ficha) e colunas mais estreitas
    private static final int[] LARGURAS_EM_PE = {26, 0, 60, 30, 34, -1, 14, 60};
    private int[] larguras = LARGURAS;
    private static final String[] TITULOS = {"Pos", "Nome", "País", "Nota", "Lesão", "Comport.", "✱", "Estrangeiro"};
    private static final String[] TITULOS_EM_PE = {"Pos", "Nome", "País", "Nota", "Lesão", "Comport.", "✱", "Estrang."};

    private View vistaJogadores() {
        LinearLayout v = new LinearLayout(this);
        v.setOrientation(LinearLayout.VERTICAL);
        v.setBackground(SeletorCor.fundo(VERDE_CARTAO, 0, 10));
        v.setPadding(dp(8), dp(8), dp(8), dp(8));
        v.addView(linhaTabela(deitado() ? TITULOS : TITULOS_EM_PE, true, null));
        listaJogadores = new ListView(this);
        listaJogadores.setAdapter(adapterJogadores);
        listaJogadores.setDivider(null);
        listaJogadores.setOnItemClickListener((p, x, pos, id) -> {
            jogadorSelecionado = pos;
            adapterJogadores.notifyDataSetChanged();
            atualizarBotoes();
        });
        listaJogadores.setOnItemLongClickListener((p, x, pos, id) -> {
            jogadorSelecionado = pos;
            editarJogador();
            return true;
        });
        v.addView(listaJogadores, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));
        LinearLayout botoes = new LinearLayout(this);
        botoes.setGravity(Gravity.CENTER_VERTICAL);
        botoes.setPadding(0, dp(8), 0, 0);
        adicionar = botao("Adicionar", true);
        editar = botao("Editar", false);
        remover = botao("Remover", false);
        transferir = botao("Transferir…", false);
        adicionar.setOnClickListener(x -> adicionarJogador());
        editar.setOnClickListener(x -> editarJogador());
        remover.setOnClickListener(x -> removerJogador());
        transferir.setOnClickListener(x -> transferirJogador());
        for (Button b : new Button[] {adicionar, editar, remover, transferir}) {
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, dp(38));
            p.rightMargin = dp(8);
            botoes.addView(b, p);
        }
        if (deitado()) botoes.addView(texto("Toque para escolher, toque longo para editar", 11, CINZA_FAIXA, false));
        else for (Button b : new Button[] {adicionar, editar, remover, transferir}) b.setPadding(dp(10), 0, dp(10), 0);
        v.addView(botoes);
        return v;
    }

    private LinearLayout linhaTabela(String[] textos, boolean cabecalho, TeamCodec.Jogador j) {
        LinearLayout l = new LinearLayout(this);
        l.setGravity(Gravity.CENTER_VERTICAL);
        l.setPadding(dp(6), dp(cabecalho ? 2 : 8), dp(6), dp(cabecalho ? 6 : 8));
        for (int i = 0; i < textos.length; i++) {
            View celula;
            if (!cabecalho && i == 2) {
                LinearLayout b = new LinearLayout(this);
                b.setGravity(Gravity.CENTER_VERTICAL);
                b.addView(imagemBandeira(j.pais, 21));
                TextView t = texto(j.pais, 13, porCodigo.containsKey(j.pais) ? BRANCO : ALERTA, false);
                t.setPadding(dp(5), 0, 0, 0);
                t.setSingleLine(true);
                b.addView(t);
                celula = b;
            } else {
                TextView t = texto(textos[i], cabecalho ? 12 : 14, cabecalho ? AMARELO : BRANCO, cabecalho || i == 1);
                t.setSingleLine(true);
                t.setEllipsize(TextUtils.TruncateAt.END);
                t.setGravity(i == 1 || i == 2 || i == 5 || i == 7 ? Gravity.START : i == 0 || i == 6 ? Gravity.CENTER : Gravity.END);
                celula = t;
            }
            LinearLayout.LayoutParams p = larguras[i] < 0 ? new LinearLayout.LayoutParams(0, 0)
                : larguras[i] == 0 ? new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1)
                : new LinearLayout.LayoutParams(dp(larguras[i]), ViewGroup.LayoutParams.WRAP_CONTENT);
            p.leftMargin = i == 0 ? 0 : dp(5);
            l.addView(celula, p);
        }
        return l;
    }

    private static int corPosicao(int pos) {
        switch (pos) {
            case 0: return 0xFFE0B000;
            case 1: return 0xFF3D7BD9;
            case 2: return 0xFF2E9E4F;
            case 3: return 0xFFD9443D;
            default: return Color.GRAY;
        }
    }

    // Coluna "Estrangeiro": conta no limite de 5? (nacional fica em branco)
    private String textoEstrangeiro(String situacao) {
        boolean curto = !deitado();
        switch (situacao) {
            case "Estrangeiro": return "Sim";
            case "Bosman": return curto ? "Bosman" : "Não (Bosman)";
            case "PLOP": return curto ? "PLOP" : "Não (PLOP)";
            default: return "";
        }
    }

    private final class JogadoresAdapter extends BaseAdapter {
        @Override public int getCount() { return atual == null ? 0 : atual.jogadores.size(); }
        @Override public Object getItem(int i) { return atual.jogadores.get(i); }
        @Override public long getItemId(int i) { return i; }

        @Override
        public View getView(int i, View v, ViewGroup pai) {
            TeamCodec.Jogador j = atual.jogadores.get(i);
            int pos = Math.max(0, Math.min(3, j.posicao));
            String sit = regras.situacao(j.pais, atual.pais);
            LinearLayout l = linhaTabela(new String[] {
                TeamCodec.POSICOES_CURTAS[pos], j.nome, "", Integer.toString(j.nota()), Integer.toString(j.lesao()),
                COMPORTAMENTOS[j.comportamento()], j.estrela() ? "✱" : "", textoEstrangeiro(sit),
            }, false, j);
            l.setBackgroundColor(i == jogadorSelecionado ? SELECAO : i % 2 == 0 ? VERDE_LINHA : VERDE_CARTAO);
            TextView p = (TextView) l.getChildAt(0);
            p.setTypeface(Typeface.DEFAULT_BOLD);
            p.setBackground(SeletorCor.fundo(corPosicao(pos), 0, 4));
            ((TextView) l.getChildAt(6)).setTextColor(OURO);
            ((TextView) l.getChildAt(7)).setTextColor(sit.equals("Estrangeiro") ? ALERTA : OK);
            return l;
        }
    }

    private final class EquipesAdapter extends BaseAdapter {
        @Override public int getCount() { return visiveis.size(); }
        @Override public Object getItem(int i) { return visiveis.get(i); }
        @Override public long getItemId(int i) { return i; }

        @Override
        public View getView(int i, View v, ViewGroup pai) {
            TeamCodec.Equipe t = visiveis.get(i);
            LinearLayout l = new LinearLayout(TeamEditorActivity.this);
            l.setGravity(Gravity.CENTER_VERTICAL);
            l.setPadding(dp(8), dp(6), dp(8), dp(6));
            l.setBackgroundColor(t == atual ? SELECAO : Color.TRANSPARENT);
            l.addView(imagemBandeira(t.pais, 30));
            LinearLayout txt = new LinearLayout(TeamEditorActivity.this);
            txt.setOrientation(LinearLayout.VERTICAL);
            txt.setPadding(dp(10), 0, 0, 0);
            TextView n = texto(t.nomeAbreviado.isEmpty() ? "(sem nome)" : t.nomeAbreviado, 15, BRANCO, true);
            n.setSingleLine(true);
            n.setEllipsize(TextUtils.TruncateAt.END);
            TextView info = texto(t.nomeCompleto + " · " + t.jogadores.size() + " jog.", 11, CINZA, false);
            info.setSingleLine(true);
            info.setEllipsize(TextUtils.TruncateAt.END);
            txt.addView(n);
            txt.addView(info);
            l.addView(txt, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
            if (!regras.validar(t).isEmpty()) l.addView(texto("⚠", 15, ALERTA, true));
            return l;
        }
    }

    // ---- lista de equipes ----

    private void carregarEquipes() {
        equipes.clear();
        ilegiveis = 0;
        File[] arqs = equipasDir.listFiles();
        if (arqs != null)
            for (File f : arqs) {
                if (!f.getName().toLowerCase(Locale.ROOT).endsWith(".eft")) continue;
                try { equipes.add(TeamCodec.ler(f)); } catch (Exception e) { ilegiveis++; }
            }
        ordenar();
        montarFiltro();
        filtrar();
    }

    private void ordenar() {
        Collator c = Collator.getInstance(BR);
        equipes.sort((a, b) -> c.compare(a.nomeAbreviado, b.nomeAbreviado));
    }

    private void montarFiltro() {
        String antes = filtro.getSelectedItemPosition() >= 0 && filtro.getSelectedItemPosition() < itensFiltro.size()
            ? (String) itensFiltro.get(filtro.getSelectedItemPosition())[0] : null;
        Map<String, Integer> conta = new LinkedHashMap<>();
        for (TeamCodec.Equipe t : equipes) conta.merge(t.pais, 1, Integer::sum);
        itensFiltro.clear();
        itensFiltro.add(new Object[] {null, "Todos (" + equipes.size() + ")"});
        List<String> codigos = new ArrayList<>(conta.keySet());
        Collator c = Collator.getInstance(BR);
        codigos.sort((a, b) -> c.compare(nomePais(a), nomePais(b)));
        int sel = 0;
        for (String cod : codigos) {
            if (cod.equals(antes)) sel = itensFiltro.size();
            itensFiltro.add(new Object[] {cod, nomePais(cod) + " (" + conta.get(cod) + ")"});
        }
        ((ArrayAdapter<?>) filtro.getAdapter()).notifyDataSetChanged();
        carregando = true;
        filtro.setSelection(sel);
        carregando = false;
    }

    private String nomePais(String codigo) {
        TeamCodec.Pais p = porCodigo.get(codigo);
        return p != null ? p.nome : codigo;
    }

    private static String semAcento(String s) {
        return Normalizer.normalize(s, Normalizer.Form.NFD).replaceAll("\\p{M}", "").toLowerCase(Locale.ROOT);
    }

    private void filtrar() {
        int f = filtro.getSelectedItemPosition();
        String pais = f >= 0 && f < itensFiltro.size() ? (String) itensFiltro.get(f)[0] : null;
        String termo = semAcento(busca.getText().toString().trim());
        visiveis.clear();
        for (TeamCodec.Equipe t : equipes) {
            if (pais != null && !t.pais.equals(pais)) continue;
            if (!termo.isEmpty() && !contem(t, termo)) continue;
            visiveis.add(t);
        }
        adapterEquipes.notifyDataSetChanged();
        contaEquipes.setText(visiveis.size() + " de " + equipes.size() + " equipes"
            + (termo.isEmpty() ? "" : " (busca também por jogador)")
            + (ilegiveis > 0 ? " · " + ilegiveis + " arquivo(s) ilegível(is)" : ""));
        atualizarFaixaBusca();
    }

    private static boolean contem(TeamCodec.Equipe t, String termo) {
        if (semAcento(t.nomeAbreviado).contains(termo) || semAcento(t.nomeCompleto).contains(termo)) return true;
        if (t.arquivo != null && semAcento(t.arquivo.getName()).contains(termo)) return true;
        for (TeamCodec.Jogador j : t.jogadores) if (semAcento(j.nome).contains(termo)) return true;
        return false;
    }

    private void escolherEquipe(TeamCodec.Equipe t) {
        if (t == atual) return;
        Runnable trocar = () -> {
            if (alterado) recarregar(atual);
            alterado = false;
            atual = t;
            jogadorSelecionado = -1;
            tituloBarra.setText("Editor de Equipes");
            filtrar();
            mostrarEquipe();
        };
        if (alterado) confirmar("Há alterações não salvas nesta equipe. Descartar?", trocar);
        else trocar.run();
    }

    // Descarta alteracoes: rele a equipe do disco (ou tira a nova da lista)
    private void recarregar(TeamCodec.Equipe t) {
        int i = equipes.indexOf(t);
        if (i < 0) return;
        if (t.arquivo == null || !t.arquivo.exists()) { equipes.remove(i); return; }
        try { equipes.set(i, TeamCodec.ler(t.arquivo)); } catch (Exception e) { equipes.remove(i); }
    }

    // ---- equipe ----

    private boolean abaEquipeAtiva = true;

    private void mostrarAba(boolean equipe) {
        abaEquipeAtiva = equipe;
        vistaEquipe.setVisibility(equipe ? View.VISIBLE : View.GONE);
        vistaJogadores.setVisibility(equipe ? View.GONE : View.VISIBLE);
        abaEquipe.setBackground(SeletorCor.fundo(equipe ? AMARELO : 0xFFE6E6E6, 0, 6));
        abaJogadores.setBackground(SeletorCor.fundo(equipe ? 0xFFE6E6E6 : AMARELO, 0, 6));
        abaEquipe.setTypeface(equipe ? Typeface.DEFAULT_BOLD : Typeface.DEFAULT);
        abaJogadores.setTypeface(equipe ? Typeface.DEFAULT : Typeface.DEFAULT_BOLD);
    }

    private void mostrarEquipe() {
        boolean tem = atual != null;
        for (View v : new View[] {painelDireito}) v.setAlpha(tem ? 1f : 0.4f);
        habilitar(salvar, tem);
        carregando = true;
        nomeCompleto.setText(tem ? atual.nomeCompleto : "");
        nomeAbreviado.setText(tem ? atual.nomeAbreviado : "");
        treinador.setText(tem ? atual.treinador : "");
        int ip = tem ? indicePais(atual.pais) : -1;
        if (ip >= 0) spPais.setSelection(ip);
        arquivo.setText(!tem ? "" : atual.arquivo == null ? "nova (sem arquivo)" : "EQUIPAS/" + atual.arquivo.getName());
        carregando = false;
        for (View v : new View[] {nomeCompleto, nomeAbreviado, treinador, spPais}) v.setEnabled(tem);
        spPais.setVisibility(tem ? View.VISIBLE : View.INVISIBLE);
        mostrarNivel();
        pintarCores();
        mostrarJogadores();
    }

    private void camposMudaram() {
        if (carregando || atual == null) return;
        String nc = nomeCompleto.getText().toString().toUpperCase(BR), na = nomeAbreviado.getText().toString().toUpperCase(BR);
        String tr = treinador.getText().toString();
        int ip = spPais.getSelectedItemPosition();
        String pa = ip >= 0 && ip < paises.size() ? paises.get(ip).codigo : atual.pais;
        // O Spinner avisa a selecao depois de carregar: so conta se algo mudou
        if (nc.equals(atual.nomeCompleto) && na.equals(atual.nomeAbreviado) && tr.equals(atual.treinador) && pa.equals(atual.pais)) return;
        atual.nomeCompleto = nc;
        atual.nomeAbreviado = na;
        atual.treinador = tr;
        atual.pais = pa;
        alterou();
        pintarCores();
        mostrarJogadores();
        adapterEquipes.notifyDataSetChanged();
    }

    private void alterou() {
        alterado = true;
        tituloBarra.setText("Editor de Equipes •");
    }

    private void mudarNivel(int passo) {
        if (atual == null) return;
        atual.nivel = Math.max(TeamCodec.NIVEL_MIN, Math.min(TeamCodec.NIVEL_MAX, atual.nivel + passo));
        alterou();
        mostrarNivel();
        mostrarRegras();
    }

    private void mostrarNivel() {
        nivel.setText(atual == null ? "—" : Integer.toString(atual.nivel));
        habilitar(nivelMenos, atual != null && atual.nivel > TeamCodec.NIVEL_MIN);
        habilitar(nivelMais, atual != null && atual.nivel < TeamCodec.NIVEL_MAX);
    }

    private void pintarCores() {
        if (atual == null) {
            previa.setText(deitado() ? "Escolha uma equipe à esquerda ou crie uma nova" : "Escolha uma equipe acima ou crie uma nova");
            previa.setBackground(null);
            previa.setTextColor(CINZA);
            hexLetra.setText("—");
            hexFundo.setText("—");
            return;
        }
        previa.setText(atual.nomeAbreviado.isEmpty() ? "(sem nome)" : atual.nomeAbreviado);
        previa.setTextColor(0xFF000000 | atual.corLetra);
        previa.setBackground(SeletorCor.fundo(0xFF000000 | atual.corFundo, CINZA, 4));
        amostraLetra.setBackground(SeletorCor.fundo(0xFF000000 | atual.corLetra, CINZA, 4));
        amostraFundo.setBackground(SeletorCor.fundo(0xFF000000 | atual.corFundo, CINZA, 4));
        hexLetra.setText(String.format(Locale.ROOT, "#%06X", atual.corLetra));
        hexFundo.setText(String.format(Locale.ROOT, "#%06X", atual.corFundo));
    }

    private void mostrarJogadores() {
        if (atual != null && jogadorSelecionado >= atual.jogadores.size()) jogadorSelecionado = atual.jogadores.size() - 1;
        adapterJogadores.notifyDataSetChanged();
        abaJogadores.setText(atual == null ? "Jogadores" : "Jogadores (" + atual.jogadores.size() + ")");
        mostrarRegras();
        atualizarBotoes();
    }

    private void atualizarBotoes() {
        boolean sel = atual != null && jogadorSelecionado >= 0 && jogadorSelecionado < atual.jogadores.size();
        habilitar(adicionar, atual != null && atual.jogadores.size() < TeamCodec.MAX_JOGADORES);
        habilitar(editar, sel);
        habilitar(remover, sel);
        habilitar(transferir, sel);
    }

    private void mostrarRegras() {
        problemas.removeAllViews();
        if (atual == null) { resumo.setText(""); return; }
        int gr = 0;
        for (TeamCodec.Jogador j : atual.jogadores) if (j.posicao == 0) gr++;
        int campo = atual.jogadores.size() - gr, est = regras.estrangeiros(atual);
        String contagem = atual.jogadores.size() + " jogadores (14 a 20) · " + gr + " GR · " + campo + " de campo (mín. 10) · "
            + (liberado ? "estrangeiros sem limite" : est + " de " + TeamCodec.MAX_ESTRANGEIROS + " estrangeiros");
        ultimaContagem = contagem;
        problemas.addView(texto(contagem, 13, BRANCO, false));
        List<String> erros = regras.validar(atual);
        if (erros.isEmpty()) {
            problemas.addView(texto("✓ Pronta para o jogo", 14, OK, true));
            // Em pe o resumo divide a linha com as abas: so o estado; a contagem fica no toque
            resumo.setText(larguras == LARGURAS_EM_PE ? "✓ Pronta para o jogo" : "✓ Pronta para o jogo · " + contagem);
            resumo.setTextColor(OK);
        } else {
            for (String e : erros) problemas.addView(texto("✗ " + e, 14, ALERTA, false));
            resumo.setText("✗ " + erros.size() + (erros.size() == 1 ? " problema" : " problemas") + " (toque para ver)"
                + (larguras == LARGURAS_EM_PE ? "" : " · " + contagem));
            resumo.setTextColor(ALERTA);
        }
    }

    private String ultimaContagem = "";

    private void mostrarProblemas() {
        if (atual == null) return;
        List<String> erros = regras.validar(atual);
        Ui.mensagem(this, erros.isEmpty() ? "Pronta para o jogo" : "O jogo não aceita a equipe assim",
            (erros.isEmpty() ? "A equipe cumpre todas as regras do jogo." : "• " + TextUtils.join("\n• ", erros))
                + "\n\n" + ultimaContagem);
    }

    // ---- acoes ----

    private void novaEquipe() {
        Runnable criar = () -> {
            if (alterado) recarregar(atual);
            int f = filtro.getSelectedItemPosition();
            String pais = f > 0 && f < itensFiltro.size() ? (String) itensFiltro.get(f)[0] : "BRA";
            TeamCodec.Equipe t = new TeamCodec.Equipe();
            t.pais = pais;
            t.nomeCompleto = "NOVA EQUIPA";
            t.nomeAbreviado = "NOVA";
            equipes.add(0, t);
            atual = t;
            jogadorSelecionado = -1;
            montarFiltro();
            filtrar();
            mostrarEquipe();
            mostrarAba(true);
            alterou();
            nomeCompleto.requestFocus();
            nomeCompleto.selectAll();
        };
        if (alterado) confirmar("Há alterações não salvas nesta equipe. Descartar?", criar);
        else criar.run();
    }

    private void salvar(Runnable depois) {
        if (atual == null) return;
        List<String> erros = regras.validar(atual);
        if (!erros.isEmpty()) {
            Ui.mensagem(this, "Não dá para salvar", "O jogo não aceita a equipe assim:\n\n• " + TextUtils.join("\n• ", erros));
            return;
        }
        if (atual.arquivo == null) {
            pedirArquivo(sugerirArquivo(atual.nomeAbreviado), nome -> {
                atual.arquivo = new File(equipasDir, nome + ".EFT");
                gravar(depois);
            });
            return;
        }
        gravar(depois);
    }

    private void gravar(Runnable depois) {
        try {
            TeamCodec.gravar(atual, atual.arquivo);
        } catch (Exception e) {
            Ui.mensagem(this, "Erro", "Erro ao gravar:\n" + e.getMessage());
            return;
        }
        alterado = false;
        tituloBarra.setText("Editor de Equipes");
        ordenar();
        montarFiltro();
        filtrar();
        mostrarEquipe();
        Ui.mensagem(this, "Salvo", "Equipe gravada em EQUIPAS/" + atual.arquivo.getName() + ".");
        if (depois != null) depois.run();
    }

    // Nome de arquivo de ate 8 letras (como no Editor de Equipas), sem repetir
    private String sugerirArquivo(String nome) {
        StringBuilder b = new StringBuilder();
        for (char ch : semAcento(nome).toUpperCase(Locale.ROOT).toCharArray())
            if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')) b.append(ch);
        String base = b.length() == 0 ? "EQUIPA" : b.length() > 8 ? b.substring(0, 8) : b.toString();
        String s = base;
        for (int n = 2; existeArquivo(s); n++) {
            String num = Integer.toString(n);
            s = base.substring(0, Math.min(base.length(), 8 - num.length())) + num;
        }
        return s;
    }

    private boolean existeArquivo(String nome) {
        File[] arqs = equipasDir.listFiles();
        if (arqs == null) return false;
        for (File f : arqs) {
            String n = f.getName();
            int p = n.lastIndexOf('.');
            if ((p >= 0 ? n.substring(0, p) : n).equalsIgnoreCase(nome)) return true;
        }
        return false;
    }

    private interface AoNome { void nome(String n); }

    private void pedirArquivo(String sugestao, AoNome ao) {
        EditText e = new EditText(this);
        e.setSingleLine(true);
        e.setText(sugestao);
        e.setSelectAllOnFocus(true);
        e.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        e.setFilters(new InputFilter[] {new InputFilter.LengthFilter(8), new InputFilter.AllCaps()});
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        l.setPadding(dp(20), dp(8), dp(20), 0);
        TextView t = new TextView(this);
        t.setText("Nome do arquivo (até 8 letras), na pasta EQUIPAS:");
        l.addView(t);
        l.addView(e);
        AlertDialog d = new AlertDialog.Builder(this)
            .setTitle("Gravar equipe")
            .setView(l)
            .setPositiveButton("Gravar", null)
            .setNegativeButton("Cancelar", null)
            .create();
        d.setOnShowListener(x -> d.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            String n = e.getText().toString().trim().toUpperCase(Locale.ROOT);
            if (!n.matches("[A-Z0-9_]{1,8}")) { Ui.mensagem(this, "Nome inválido", "Use até 8 letras sem acento, números ou _."); return; }
            if (existeArquivo(n)) { Ui.mensagem(this, "Nome em uso", "Já existe EQUIPAS/" + n + ".EFT. Escolha outro nome."); return; }
            d.dismiss();
            ao.nome(n);
        }));
        d.show();
    }

    // ---- jogadores ----

    private void adicionarJogador() {
        if (atual == null || atual.jogadores.size() >= TeamCodec.MAX_JOGADORES) return;
        TeamCodec.Jogador j = new TeamCodec.Jogador();
        j.pais = atual.pais;
        j.posicao = 1;
        fichaJogador(j, "Novo jogador", () -> {
            atual.jogadores.add(j);
            jogadorSelecionado = atual.jogadores.size() - 1;
            alterou();
            mostrarJogadores();
            listaJogadores.smoothScrollToPosition(jogadorSelecionado);
            adapterEquipes.notifyDataSetChanged();
        });
    }

    private void editarJogador() {
        if (atual == null || jogadorSelecionado < 0 || jogadorSelecionado >= atual.jogadores.size()) return;
        TeamCodec.Jogador original = atual.jogadores.get(jogadorSelecionado);
        TeamCodec.Jogador c = original.copia();
        fichaJogador(c, original.nome, () -> {
            original.nome = c.nome;
            original.pais = c.pais;
            original.posicao = c.posicao;
            alterou();
            mostrarJogadores();
            adapterEquipes.notifyDataSetChanged();
        });
    }

    private void removerJogador() {
        if (atual == null || jogadorSelecionado < 0 || jogadorSelecionado >= atual.jogadores.size()) return;
        TeamCodec.Jogador j = atual.jogadores.get(jogadorSelecionado);
        confirmar("Remover " + j.nome + " da equipe?", () -> {
            atual.jogadores.remove(j);
            alterou();
            mostrarJogadores();
            adapterEquipes.notifyDataSetChanged();
        });
    }

    // Passa o jogador para outra equipe: grava as duas na hora (a atual precisa
    // estar salva e as duas continuarem aceitas pelo jogo)
    private void transferirJogador() {
        if (atual == null || jogadorSelecionado < 0 || jogadorSelecionado >= atual.jogadores.size()) return;
        if (alterado || atual.arquivo == null) {
            Ui.mensagem(this, "Transferir", "Salve a equipe atual antes de transferir.");
            return;
        }
        TeamCodec.Jogador j = atual.jogadores.get(jogadorSelecionado);
        int indice = jogadorSelecionado;
        List<TeamCodec.Equipe> destinos = new ArrayList<>();
        List<Object[]> itens = new ArrayList<>();
        for (TeamCodec.Equipe t : equipes)
            if (t != atual && t.arquivo != null) {
                destinos.add(t);
                itens.add(new Object[] {t.pais, t.nomeAbreviado + "  (" + t.jogadores.size() + " jog.)"});
            }
        Spinner sp = spinnerBranco();
        sp.setAdapter(adaptadorBandeiras(itens));
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        l.setPadding(dp(20), dp(8), dp(20), 0);
        l.addView(sp, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, dp(44)));
        TextView aviso = new TextView(this);
        aviso.setText("As duas equipes são gravadas na hora.");
        aviso.setPadding(0, dp(8), 0, 0);
        l.addView(aviso);
        new AlertDialog.Builder(this)
            .setTitle("Transferir " + j.nome)
            .setView(l)
            .setPositiveButton("Transferir", (d, w) -> {
                int i = sp.getSelectedItemPosition();
                if (i < 0) return;
                TeamCodec.Equipe destino = destinos.get(i);
                try {
                    TeamCodec.Equipe origemNova = TeamCodec.ler(atual.arquivo);
                    origemNova.jogadores.remove(indice);
                    TeamCodec.Equipe destinoNovo = TeamCodec.ler(destino.arquivo);
                    destinoNovo.jogadores.add(j.copia());
                    List<String> erros = new ArrayList<>();
                    for (String e : regras.validar(origemNova)) erros.add(atual.nomeAbreviado + ": " + e);
                    for (String e : regras.validar(destinoNovo)) erros.add(destino.nomeAbreviado + ": " + e);
                    if (!erros.isEmpty()) {
                        Ui.mensagem(this, "Não dá para transferir", "Depois da transferência o jogo não aceitaria:\n\n• " + TextUtils.join("\n• ", erros));
                        return;
                    }
                    TeamCodec.gravar(origemNova, origemNova.arquivo);
                    TeamCodec.gravar(destinoNovo, destinoNovo.arquivo);
                    equipes.set(equipes.indexOf(atual), origemNova);
                    equipes.set(equipes.indexOf(destino), destinoNovo);
                    atual = origemNova;
                    jogadorSelecionado = -1;
                    filtrar();
                    mostrarEquipe();
                    Ui.mensagem(this, "Transferido", j.nome + " agora joga no " + destinoNovo.nomeAbreviado + ".");
                } catch (Exception e) {
                    Ui.mensagem(this, "Erro", "Erro ao transferir:\n" + e.getMessage());
                }
            })
            .setNegativeButton("Cancelar", null)
            .show();
    }

    // Ficha do jogador: nome, posicao, pais e o que o jogo tira do nome, ao vivo
    private void fichaJogador(TeamCodec.Jogador j, String titulo, Runnable aoConfirmar) {
        LinearLayout ficha = new LinearLayout(this);
        ficha.setOrientation(LinearLayout.VERTICAL);
        ficha.setPadding(dp(20), dp(6), dp(20), 0);

        EditText nome = new EditText(this);
        nome.setSingleLine(true);
        nome.setText(j.nome);
        nome.setImeOptions(EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        nome.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_WORDS | InputType.TYPE_TEXT_VARIATION_PERSON_NAME);
        nome.setFilters(new InputFilter[] {new InputFilter.LengthFilter(TeamCodec.MAX_NOME)});
        Spinner pos = new Spinner(this);
        pos.setAdapter(new ArrayAdapter<>(this, android.R.layout.simple_spinner_dropdown_item, TeamCodec.POSICOES));
        pos.setSelection(Math.max(0, Math.min(3, j.posicao)));
        Spinner pais = spinnerBranco();
        pais.setAdapter(adaptadorBandeiras(itensPaises()));
        int ip = indicePais(j.pais);
        if (ip >= 0) pais.setSelection(ip);

        LinearLayout l1 = new LinearLayout(this);
        l1.setGravity(Gravity.CENTER_VERTICAL);
        TextView rn = new TextView(this);
        rn.setText("Nome");
        l1.addView(rn, new LinearLayout.LayoutParams(dp(60), ViewGroup.LayoutParams.WRAP_CONTENT));
        l1.addView(nome, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        // Em pe: um campo por linha (o nome pode ter 20 letras); deitado: nome e posicao juntos
        boolean emPe = larguras == LARGURAS_EM_PE;
        TextView rp = new TextView(this);
        rp.setText(emPe ? "Posição" : "   Posição");
        ficha.addView(l1);
        if (emPe) {
            LinearLayout lp = new LinearLayout(this);
            lp.setGravity(Gravity.CENTER_VERTICAL);
            lp.addView(rp, new LinearLayout.LayoutParams(dp(60), ViewGroup.LayoutParams.WRAP_CONTENT));
            lp.addView(pos, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
            ficha.addView(lp);
        } else {
            l1.addView(rp);
            l1.addView(pos, new LinearLayout.LayoutParams(dp(150), ViewGroup.LayoutParams.WRAP_CONTENT));
        }
        LinearLayout l2 = new LinearLayout(this);
        l2.setGravity(Gravity.CENTER_VERTICAL);
        TextView rpa = new TextView(this);
        rpa.setText("País");
        l2.addView(rpa, new LinearLayout.LayoutParams(dp(60), ViewGroup.LayoutParams.WRAP_CONTENT));
        l2.addView(pais, new LinearLayout.LayoutParams(0, dp(44), 1));
        ficha.addView(l2);

        TextView explica = new TextView(this);
        explica.setText("O jogo tira estes valores do nome (e da posição):");
        explica.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        explica.setPadding(0, dp(8), 0, dp(4));
        ficha.addView(explica);
        LinearLayout caixas = new LinearLayout(this), caixas2 = caixas;
        if (emPe) { caixas2 = new LinearLayout(this); }
        TextView[] valores = new TextView[4];
        String[] rotulos = {"Nota", "Lesão", "Comportamento", "Estrela"};
        for (int k = 0; k < 4; k++) {
            LinearLayout cx = new LinearLayout(this);
            cx.setOrientation(LinearLayout.VERTICAL);
            cx.setPadding(dp(8), dp(4), dp(8), dp(4));
            cx.setBackground(SeletorCor.fundo(0x00000000, Color.LTGRAY, 4));
            TextView r = new TextView(this);
            r.setText(rotulos[k]);
            r.setTextSize(TypedValue.COMPLEX_UNIT_SP, 11);
            cx.addView(r);
            valores[k] = new TextView(this);
            valores[k].setTextSize(TypedValue.COMPLEX_UNIT_SP, k < 2 ? 20 : 15);
            valores[k].setTypeface(Typeface.DEFAULT_BOLD);
            valores[k].setSingleLine(true);
            cx.addView(valores[k]);
            LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.MATCH_PARENT, emPe ? 1 : k < 2 ? 1 : 1.6f);
            p.rightMargin = dp(6);
            p.bottomMargin = emPe ? dp(6) : 0;
            (emPe && k >= 2 ? caixas2 : caixas).addView(cx, p);
        }
        ficha.addView(caixas);
        if (emPe) ficha.addView(caixas2);
        TextView situacao = new TextView(this);
        situacao.setPadding(0, dp(8), 0, 0);
        ficha.addView(situacao);

        Runnable atualizar = () -> {
            TeamCodec.Jogador t = new TeamCodec.Jogador();
            t.nome = nome.getText().toString();
            t.posicao = Math.max(0, pos.getSelectedItemPosition());
            int i = pais.getSelectedItemPosition();
            t.pais = i >= 0 && i < paises.size() ? paises.get(i).codigo : "";
            boolean vazio = t.nome.trim().isEmpty();
            valores[0].setText(vazio ? "—" : Integer.toString(t.nota()));
            valores[1].setText(vazio ? "—" : Integer.toString(t.lesao()));
            valores[2].setText(vazio ? "—" : COMPORTAMENTOS[t.comportamento()]);
            valores[3].setText(vazio ? "—" : t.estrela() ? "✱ Estrela" : "Sem estrela");
            valores[3].setTextColor(t.estrela() && !vazio ? 0xFFC79A00 : Color.GRAY);
            String s = atual == null ? "" : regras.situacao(t.pais, atual.pais);
            situacao.setText(s.isEmpty() ? "Nacional."
                : s.equals("Bosman") ? "Não conta como estrangeiro: os dois países estão no BOSMAN.TXE (Lei Bosman)."
                : s.equals("PLOP") ? "Não conta como estrangeiro: os dois países estão no PLOP.TXE (língua portuguesa)."
                : "Estrangeiro: conta no limite de " + TeamCodec.MAX_ESTRANGEIROS + " por equipe.");
            situacao.setTextColor(s.equals("Estrangeiro") ? 0xFFC62828 : 0xFF2E7D32);
        };
        nome.addTextChangedListener(new Mudou(atualizar));
        pos.setOnItemSelectedListener(new Selecao(x -> atualizar.run()));
        pais.setOnItemSelectedListener(new Selecao(x -> atualizar.run()));
        atualizar.run();

        ScrollView rolagem = new ScrollView(this);
        rolagem.addView(ficha);
        AlertDialog d = new AlertDialog.Builder(this)
            .setTitle(titulo)
            .setView(rolagem)
            .setPositiveButton("OK", null)
            .setNegativeButton("Cancelar", null)
            .create();
        d.setOnShowListener(x -> d.getButton(AlertDialog.BUTTON_POSITIVE).setOnClickListener(v -> {
            String n = nome.getText().toString().trim();
            if (n.isEmpty()) { Ui.mensagem(this, "Nome", "Escreva o nome do jogador."); return; }
            // O Editor de Equipas nao aceita numeros nos nomes
            for (char ch : n.toCharArray())
                if (Character.isDigit(ch) || ch > 0xFF) {
                    Ui.mensagem(this, "Nome", "Use só letras (com ou sem acento), espaço, ponto, hífen ou apóstrofo.");
                    return;
                }
            int i = pais.getSelectedItemPosition();
            if (i < 0 || i >= paises.size()) { Ui.mensagem(this, "País", "Escolha o país do jogador."); return; }
            j.nome = n;
            j.pais = paises.get(i).codigo;
            j.posicao = pos.getSelectedItemPosition();
            d.dismiss();
            aoConfirmar.run();
        }));
        d.show();
    }

    // ---- utilidades ----

    private void confirmar(String texto, Runnable sim) {
        new AlertDialog.Builder(this)
            .setMessage(texto)
            .setPositiveButton("Sim", (d, w) -> sim.run())
            .setNegativeButton("Não", null)
            .show();
    }

    private static final class Mudou implements TextWatcher {
        private final Runnable acao;
        Mudou(Runnable a) { acao = a; }
        @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) {}
        @Override public void onTextChanged(CharSequence s, int a, int b, int c) {}
        @Override public void afterTextChanged(Editable e) { acao.run(); }
    }

    private final class Selecao implements AdapterView.OnItemSelectedListener {
        private final Acao acao;
        Selecao(Acao a) { acao = a; }
        @Override public void onItemSelected(AdapterView<?> p, View v, int pos, long id) { if (!carregando) acao.em(pos); }
        @Override public void onNothingSelected(AdapterView<?> p) {}
    }

    private interface Acao { void em(int pos); }
}
