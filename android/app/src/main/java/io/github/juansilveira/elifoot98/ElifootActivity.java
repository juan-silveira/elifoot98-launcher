package io.github.juansilveira.elifoot98;

import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.RelativeLayout;

import org.libsdl.app.SDLActivity;
import org.libsdl.app.TecladoSDL;

import java.io.File;
import java.io.IOException;

/**
 * O jogo (ou o Editor de Equipes) rodando no Boxedwine, em tela cheia. Roda num
 * processo proprio (":jogo"), encerrado ao sair, pra poder reabrir depois.
 * Faixa esquerda: botoes (como no elifoot98web) teclado, Esc, Enter, taticas,
 * T (5-0-5), M, * e -. Faixa direita: abas com as janelas abertas do jogo (no Boxedwine
 * elas podem ficar escondidas atras da principal; tocar na aba traz pra frente).
 */
public class ElifootActivity extends SDLActivity {
    public static final String EXTRA_EXE = "exe";
    private static final String TAG = "Elifoot98";
    // Resolucao do Windows emulado: a janela do time tem 636x484 (nao cabe em
    // 640x480). 800x600 e 4:3, sem distorcao (720x540 o Boxedwine desenha errado).
    private static final int LARGURA = 800, ALTURA = 600;

    private final Handler handler = new Handler(Looper.getMainLooper());
    private Jogo jogo;
    private boolean tecladoAberto;
    private String ultimoFoco = "";

    @Override
    protected String[] getLibraries() {
        // Saida do Wine num arquivo (lido pelo constructor em jni/src/semgl.cpp)
        try {
            android.system.Os.setenv("ELIFOOT_SAIDA", new File(getFilesDir(), "saida.txt").getAbsolutePath(), true);
        } catch (Exception ignored) {
        }
        return new String[] {"SDL2", "main"};
    }

    @Override
    protected String[] getArguments() {
        jogo = new Jogo(this);
        try {
            jogo.preparar(this);
        } catch (IOException e) {
            Log.e(TAG, "Falha ao preparar os arquivos do jogo", e);
        }
        jogo.arquivoTeclado().delete();
        new File(jogo.jogo, "janelas.txt").delete();
        new File(jogo.jogo, "comando.txt").delete();
        new File(jogo.jogo, "pronto.txt").delete();
        String exe = getIntent().getStringExtra(EXTRA_EXE);
        if (exe == null) exe = "ELIFOOT.EXE";
        String files = "/home/username/.wine/dosdevices/c:/files";
        return new String[] {
            "-root", jogo.root.getAbsolutePath(),
            "-zip", new File(jogo.dir, "wine11.zip").getAbsolutePath(),
            "-zip", new File(jogo.dir, "w16.zip").getAbsolutePath(),
            "-mount", jogo.jogo.getAbsolutePath(), files,
            "-resolution", LARGURA + "x" + ALTURA, "-fullscreenAspect",
            "-w", files,
            // iniciar.exe (linux/iniciar.c): faz a "Acerca" aparecer e, na pasta
            // do jogo, mantem teclado.txt (campo com foco), janelas.txt (abas) e
            // atende comando.txt (aba tocada, botoes ✱/—)
            "/bin/wine", "iniciar.exe", "/android", "C:\\files", exe,
        };
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        if (mLayout == null) return;  // SDL recusou iniciar (ex.: biblioteca faltando)
        imersivo();

        // Faixa esquerda (o jogo e 4:3): mesmos controles do elifoot98web
        esquerda = grade(RelativeLayout.ALIGN_PARENT_LEFT);
        esquerda.setId(View.generateViewId());
        esquerda.addView(botao("⌨", v -> alternarTeclado()));
        esquerda.addView(botao("Esc", v -> tecla(KeyEvent.KEYCODE_ESCAPE)));
        Button enter = botao("", v -> tecla(KeyEvent.KEYCODE_ENTER));
        enter.setBackground(new android.graphics.drawable.LayerDrawable(new android.graphics.drawable.Drawable[] {
            enter.getBackground(), new IconeEnter(dp(3))}));
        enter.setContentDescription("Enter");
        esquerda.addView(enter);
        Button tat = botao("Tát", v -> taticas());
        // Easter egg: segurar o "Tát" por 3 s pergunta se quer usar o macete Alt+Shift+F4: no
        // computador o atalho fecha a janela do time e comeca a rodada sem conferir a escalacao
        Runnable macete = () -> {
            tat.setPressed(false);
            maceteDisparado = true;
            new android.app.AlertDialog.Builder(this)
                .setTitle("Macete Alt+Shift+F4")
                .setMessage("Fecha a janela do time e começa a rodada sem o jogo conferir a escalação, como o atalho Alt+Shift+F4 do teclado no computador. "
                    + "Assim o time joga com os titulares que você marcou (✱), de nenhum a todos do elenco.\n\nMarque os titulares antes. Usar o macete agora?")
                .setPositiveButton("Usar", (d, w) -> altShiftF4())
                .setNegativeButton("Cancelar", null)
                .setOnDismissListener(d -> imersivo())
                .show();
        };
        tat.setOnTouchListener((v, e) -> {
            if (e.getActionMasked() == android.view.MotionEvent.ACTION_DOWN) {
                maceteDisparado = false;
                handler.postDelayed(macete, 3000);
            } else if (e.getActionMasked() == android.view.MotionEvent.ACTION_UP || e.getActionMasked() == android.view.MotionEvent.ACTION_CANCEL) {
                handler.removeCallbacks(macete);
                if (maceteDisparado) return true;  // o toque longo nao abre as taticas
            }
            return false;
        });
        esquerda.addView(tat);
        esquerda.addView(botao("T", v -> tecla(KeyEvent.KEYCODE_T)));          // 5-0-5 (o Automatico fica na lista do Tat)
        esquerda.addView(botao("M", v -> tecla(KeyEvent.KEYCODE_M)));          // Melhores
        esquerda.addView(botao("✱", v -> comando("*")));   // jogador selecionado: titular
        esquerda.addView(botao("—", v -> comando("-")));   // jogador selecionado: reserva

        // Abas das janelas do jogo: faixa direita deitado, faixa que rola de lado em pe
        abas = new LinearLayout(this);
        rolagem = new android.widget.ScrollView(this);
        rolagemLado = new android.widget.HorizontalScrollView(this);
        rolagemLado.setHorizontalScrollBarEnabled(false);
        mLayout.addView(rolagem);
        mLayout.addView(rolagemLado);

        // Botao "100%": aparece com o jogo ampliado (pinca) e volta ao tamanho original
        botao100 = new Button(this);
        botao100.setText("100%");
        botao100.setAllCaps(false);
        botao100.setTextColor(Color.BLACK);
        botao100.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
        GradientDrawable fundo100 = new GradientDrawable();
        fundo100.setCornerRadius(dp(20));
        fundo100.setColor(Color.rgb(252, 254, 4));
        fundo100.setStroke(dp(2), Color.rgb(0, 80, 0));  // aparece sobre o amarelo do jogo
        botao100.setBackground(fundo100);
        botao100.setFocusable(false);
        botao100.setVisibility(View.GONE);
        botao100.setOnClickListener(v -> zoom(1, 0, 0));
        RelativeLayout.LayoutParams p100 = new RelativeLayout.LayoutParams(dp(72), dp(40));
        p100.addRule(RelativeLayout.ALIGN_PARENT_RIGHT);
        p100.addRule(RelativeLayout.ALIGN_PARENT_TOP);
        p100.setMargins(0, dp(12), dp(12), 0);
        mLayout.addView(botao100, p100);
        botaoControles = new Button(this);
        botaoControles.setText("Botões");
        botaoControles.setAllCaps(false);
        botaoControles.setTextColor(Color.BLACK);
        botaoControles.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
        botaoControles.setBackground(fundo100.getConstantState().newDrawable().mutate());
        botaoControles.setFocusable(false);
        botaoControles.setVisibility(View.GONE);
        botaoControles.setOnClickListener(v -> mostrarControles(esquerda.getVisibility() != View.VISIBLE));
        RelativeLayout.LayoutParams pc = new RelativeLayout.LayoutParams(dp(84), dp(40));
        pc.addRule(RelativeLayout.ALIGN_PARENT_TOP);
        pc.addRule(RelativeLayout.ALIGN_PARENT_RIGHT);
        pc.setMargins(0, dp(12), dp(96), 0);
        mLayout.addView(botaoControles, pc);
        pinca = new android.view.ScaleGestureDetector(this, aoPincar);

        new File(new Jogo(this).jogo, "pronto.txt").delete();  // de uma execucao anterior
        carregando = telaCarregando();
        mLayout.addView(carregando, new RelativeLayout.LayoutParams(
            RelativeLayout.LayoutParams.MATCH_PARENT, RelativeLayout.LayoutParams.MATCH_PARENT));

        organizar();
        handler.post(vigiarFoco);
        mLayout.getViewTreeObserver().addOnGlobalLayoutListener(aoMudarLayout);
    }

    @Override
    public void onConfigurationChanged(android.content.res.Configuration nova) {
        super.onConfigurationChanged(nova);
        if (mLayout != null && esquerda != null) organizar();
    }

    // Em pe: fundo verde embaixo do jogo, atras das abas e dos botoes
    private View fundoControles;

    private void ajustarFundo() {
        if (fundoControles == null || mSurface == null || mLayout.getHeight() == 0) return;
        boolean pe = emPe();
        int alvo = alturaJogo();
        ViewGroup.LayoutParams ls = mSurface.getLayoutParams();
        if (ls.height != alvo) {
            ls.height = alvo;
            if (ls instanceof RelativeLayout.LayoutParams) ((RelativeLayout.LayoutParams) ls).addRule(RelativeLayout.ALIGN_PARENT_TOP);
            mSurface.setLayoutParams(ls);
        }
        fundoControles.setVisibility(pe ? View.VISIBLE : View.GONE);
        if (!pe) return;
        // cobre o que o jogo ampliado passaria da area dele
        int altura = Math.max(0, mLayout.getHeight() - alvo);
        ViewGroup.LayoutParams lp = fundoControles.getLayoutParams();
        if (lp.height != altura) { lp.height = altura; fundoControles.setLayoutParams(lp); }
    }

    // O SDL trava a orientacao pelo formato da janela (800x600 = deitado). Aqui vale a do
    // manifest: gira com o aparelho, e organizar() arruma o layout de cada orientacao.
    @Override
    public void setOrientationBis(int w, int h, boolean resizable, String hint) {
    }

    private boolean emPe() {
        android.util.DisplayMetrics m = getResources().getDisplayMetrics();
        return m.heightPixels > m.widthPixels;
    }

    // Deitado: botoes a esquerda e abas a direita do jogo (4:3 no meio).
    // Em pe: o jogo encosta no topo (aplicarZoom) e abas e botoes ficam embaixo.
    private void organizar() {
        boolean pe = emPe();
        if (fundoControles == null) {
            fundoControles = new View(this);
            fundoControles.setBackgroundColor(Color.rgb(11, 61, 11));
            RelativeLayout.LayoutParams pf = new RelativeLayout.LayoutParams(RelativeLayout.LayoutParams.MATCH_PARENT, 0);
            pf.addRule(RelativeLayout.ALIGN_PARENT_BOTTOM);
            mLayout.addView(fundoControles, Math.max(0, mLayout.indexOfChild(mSurface) + 1), pf);
        }
        // o GridLayout guarda a coluna de cada botao: tira todos, muda as colunas e recoloca
        java.util.List<View> botoes = new java.util.ArrayList<>();
        for (int i = 0; i < esquerda.getChildCount(); i++) botoes.add(esquerda.getChildAt(i));
        esquerda.removeAllViews();
        esquerda.setColumnCount(pe ? 4 : 2);
        for (View b : botoes) {
            android.widget.GridLayout.LayoutParams p = new android.widget.GridLayout.LayoutParams(
                android.widget.GridLayout.spec(android.widget.GridLayout.UNDEFINED, android.widget.GridLayout.CENTER),
                android.widget.GridLayout.spec(android.widget.GridLayout.UNDEFINED, android.widget.GridLayout.CENTER));
            p.width = dp(56);
            p.height = dp(56);
            p.setMargins(dp(5), dp(5), dp(5), dp(5));
            esquerda.addView(b, p);
        }
        RelativeLayout.LayoutParams pg = new RelativeLayout.LayoutParams(
            RelativeLayout.LayoutParams.WRAP_CONTENT, RelativeLayout.LayoutParams.WRAP_CONTENT);
        if (pe) {
            pg.addRule(RelativeLayout.ALIGN_PARENT_BOTTOM);
            pg.addRule(RelativeLayout.CENTER_HORIZONTAL);
            pg.setMargins(0, 0, 0, dp(24));
        } else {
            pg.addRule(RelativeLayout.ALIGN_PARENT_LEFT);
            pg.addRule(RelativeLayout.CENTER_VERTICAL);
            pg.setMargins(dp(8), 0, dp(8), 0);
        }
        esquerda.setLayoutParams(pg);

        if (abas.getParent() != null) ((ViewGroup) abas.getParent()).removeView(abas);
        abas.setOrientation(pe ? LinearLayout.HORIZONTAL : LinearLayout.VERTICAL);
        (pe ? rolagemLado : rolagem).addView(abas);
        faixaAbas = pe ? rolagemLado : rolagem;
        (pe ? rolagem : rolagemLado).setVisibility(View.GONE);
        faixaAbas.setVisibility(esquerda.getVisibility());
        RelativeLayout.LayoutParams pa;
        if (pe) {
            pa = new RelativeLayout.LayoutParams(RelativeLayout.LayoutParams.MATCH_PARENT, RelativeLayout.LayoutParams.WRAP_CONTENT);
            pa.addRule(RelativeLayout.ABOVE, esquerda.getId());
            pa.setMargins(dp(8), 0, dp(8), dp(12));
        } else {
            pa = new RelativeLayout.LayoutParams(dp(150), RelativeLayout.LayoutParams.WRAP_CONTENT);
            pa.addRule(RelativeLayout.ALIGN_PARENT_RIGHT);
            pa.addRule(RelativeLayout.CENTER_VERTICAL);
            pa.setMargins(dp(8), dp(8), dp(8), dp(8));
        }
        faixaAbas.setLayoutParams(pa);
        if (mSurface != null && escala > 1) zoom(1, 0, 0);  // a area do jogo muda ao girar
        ultimasJanelas = "";  // refaz as abas no formato novo
        try { if (jogo != null) atualizarAbas(); } catch (IOException ignored) { }
        if (mSurface != null) mSurface.post(this::aplicarZoom);
    }

    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) imersivo();
    }

    @Override
    protected void onDestroy() {
        handler.removeCallbacks(vigiarFoco);
        super.onDestroy();
        // O Boxedwine nao reinicia no mesmo processo: encerra e o launcher segue
        android.os.Process.killProcess(android.os.Process.myPid());
    }

    // Abre/fecha o teclado sozinho quando um campo de texto do jogo ganha ou perde
    // o foco. O iniciar.exe grava "1 esq topo dir base" (coordenadas do jogo,
    // LARGURAxALTURA) ou "0" no teclado.txt.
    private float campoBase = -1;  // base do campo com foco, em pixels da tela (-1: nenhum)

    private final Runnable vigiarFoco = new Runnable() {
        @Override
        public void run() {
            try {
                File f = jogo != null ? jogo.arquivoTeclado() : null;
                String agora = f != null && f.exists() ? Jogo.ler(f).trim() : "0";
                if (!agora.equals(ultimoFoco)) {
                    ultimoFoco = agora;
                    String[] p = agora.split(" ");
                    if (p[0].equals("1")) {
                        campoBase = p.length >= 5 ? paraTela(Float.parseFloat(p[4])) : -1;
                        if (!tecladoAberto) alternarTeclado();
                        else ajustarAoTeclado();
                    } else if (tecladoAberto) {
                        campoBase = -1;
                        alternarTeclado();
                    }
                }
                if (jogo != null && abas != null) atualizarAbas();
                if (carregando != null && jogo != null && new File(jogo.jogo, "pronto.txt").exists()) {
                    View c = carregando;
                    carregando = null;
                    c.animate().alpha(0).setDuration(300).withEndAction(() -> mLayout.removeView(c));
                }
            } catch (IOException | NumberFormatException ignored) {
            }
            handler.postDelayed(this, 300);
        }
    };

    // "Carregando..." com um spinner por cima de tudo ate a janela do jogo
    // aparecer (o iniciar.exe cria o pronto.txt)
    private View carregando;

    private View telaCarregando() {
        LinearLayout l = new LinearLayout(this);
        l.setOrientation(LinearLayout.VERTICAL);
        l.setGravity(Gravity.CENTER);
        l.setBackgroundColor(Color.rgb(0, 114, 0));  // o mesmo verde do launcher
        l.setClickable(true);  // segura os toques enquanto carrega
        l.addView(Ui.logo(this, 300));
        android.widget.TextView t = new android.widget.TextView(this);
        t.setText("Carregando...");
        t.setTextColor(Color.rgb(252, 254, 4));
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, 24);
        l.addView(t, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT));
        View spinner = new Girando(this, Color.rgb(252, 254, 4), dp(4));
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(dp(48), dp(48));
        p.topMargin = dp(20);
        l.addView(spinner, p);
        return l;
    }

    // Abas: janelas.txt traz "ativa<TAB>hwnd<TAB>titulo" por linha
    private LinearLayout abas;
    private String ultimasJanelas = "";

    private void atualizarAbas() throws IOException {
        File f = new File(jogo.jogo, "janelas.txt");
        String agora = f.exists() ? Jogo.ler(f) : "";
        if (agora.equals(ultimasJanelas)) return;
        ultimasJanelas = agora;
        abas.removeAllViews();
        for (String linha : agora.split("\r?\n")) {
            String[] c = linha.split("\t", 3);
            if (c.length < 3) continue;
            abas.addView(aba(c[2], c[0].equals("1"), c[1]));
        }
    }

    private Button aba(String titulo, boolean ativa, String hwnd) {
        Button b = new Button(this);
        b.setText(titulo);
        b.setAllCaps(false);
        b.setSingleLine(true);
        b.setEllipsize(android.text.TextUtils.TruncateAt.END);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        b.setTextColor(ativa ? Color.BLACK : Color.WHITE);
        GradientDrawable fundo = new GradientDrawable();
        fundo.setCornerRadius(dp(8));
        fundo.setColor(ativa ? Color.rgb(252, 254, 4) : Color.argb(160, 0, 114, 0));
        fundo.setStroke(dp(2), Color.argb(200, 252, 254, 4));
        b.setBackground(fundo);
        b.setFocusable(false);
        b.setPadding(dp(8), 0, dp(8), 0);
        b.setOnClickListener(v -> comando(hwnd));
        boolean pe = emPe();
        if (pe) b.setMaxWidth(dp(180));
        LinearLayout.LayoutParams p = new LinearLayout.LayoutParams(
            pe ? LinearLayout.LayoutParams.WRAP_CONTENT : LinearLayout.LayoutParams.MATCH_PARENT, dp(44));
        if (pe) p.setMargins(dp(4), 0, dp(4), 0); else p.setMargins(0, dp(4), 0, dp(4));
        b.setLayoutParams(p);
        return b;
    }

    // y do jogo (LARGURAxALTURA, -fullscreenAspect) -> y na tela
    private float paraTela(float yJogo) {
        if (mSurface == null) return -1;
        float w = mSurface.getWidth(), h = mSurface.getHeight();
        float escala = Math.min(w / LARGURA, h / ALTURA);
        return (h - ALTURA * escala) / 2f + yJogo * escala;
    }

    private void alternarTeclado() {
        if (tecladoAberto) TecladoSDL.esconder();
        else TecladoSDL.mostrar();
        tecladoAberto = !tecladoAberto;
        if (!tecladoAberto) campoBase = -1;
        ajustarAoTeclado();
    }

    // Com o teclado aberto, sobe a imagem do jogo o suficiente pro campo com
    // foco ficar logo acima dele (o toque continua certo: o SDL usa coordenadas
    // relativas a propria view).
    private final android.view.ViewTreeObserver.OnGlobalLayoutListener aoMudarLayout = this::ajustarAoTeclado;

    private void ajustarAoTeclado() {
        if (mSurface == null) return;
        float deslocamento = 0;
        if (tecladoAberto && campoBase > 0) {
            android.graphics.Rect visivel = new android.graphics.Rect();
            getWindow().getDecorView().getWindowVisibleDisplayFrame(visivel);
            float limite = visivel.bottom - dp(12);
            if (campoBase > limite) deslocamento = limite - campoBase;
        }
        tecladoY = deslocamento;
        aplicarZoom();
    }

    // Zoom com pinca: amplia a imagem do jogo (o SDL recebe os toques ja nas
    // coordenadas da view, entao o clique continua no lugar certo). Dois dedos
    // ampliam e arrastam; um dedo continua sendo o mouse. O 1o toque espera um
    // pouco antes de ir pro jogo: se vier o 2o dedo, era pinca e nao clique.
    private static final int ESPERA_PINCA = 120;  // ms
    private static final float ZOOM_MAX = 4f;
    private android.widget.GridLayout esquerda;
    private android.widget.ScrollView rolagem;
    private android.widget.HorizontalScrollView rolagemLado;
    private View faixaAbas;
    private Button botao100, botaoControles;
    private android.view.ScaleGestureDetector pinca;
    private float escala = 1, zoomX, zoomY, tecladoY;
    private float focoX, focoY;
    private boolean pincando, deTela, tocandoJogo;
    private final java.util.ArrayList<android.view.MotionEvent> retidos = new java.util.ArrayList<>();
    private final Runnable soltarRetidos = this::soltarRetidos;

    private final android.view.ScaleGestureDetector.SimpleOnScaleGestureListener aoPincar =
        new android.view.ScaleGestureDetector.SimpleOnScaleGestureListener() {
            @Override
            public boolean onScale(android.view.ScaleGestureDetector d) {
                // amplia em volta do ponto entre os dedos (o arraste e a parte em arrastar())
                float nova = Math.max(1f, Math.min(ZOOM_MAX, escala * d.getScaleFactor()));
                float fx = d.getFocusX(), fy = d.getFocusY();
                zoom(nova, fx - (fx - zoomX) * nova / escala, fy - (fy - zoomY) * nova / escala);
                return true;
            }
        };

    private void zoom(float nova, float x, float y) {
        escala = nova;
        float w = mSurface.getWidth(), h = mSurface.getHeight();
        // Limita pelo retangulo do jogo (4:3 centrado), nao pela superficie: ampliado, o jogo
        // preenche a area toda sem mostrar as faixas pretas; se ainda couber, fica centrado
        float e = Math.min(w / LARGURA, h / ALTURA), iw = LARGURA * e, ih = ALTURA * e;
        float ox = (w - iw) / 2, oy = (h - ih) / 2;
        zoomX = limitar(x, w, ox, iw);
        zoomY = limitar(y, h, oy, ih);
        if (escala <= 1.01f) { escala = 1; zoomX = 0; zoomY = 0; }
        boolean ampliado = escala > 1;
        if (ampliado != (botao100.getVisibility() == View.VISIBLE)) {
            // ampliado deitado: botoes e abas saem da frente (o botao "Botoes" traz de volta).
            // Em pe eles ficam na area propria, embaixo do jogo, e continuam a vista.
            boolean pe = emPe();
            botao100.setVisibility(ampliado ? View.VISIBLE : View.GONE);
            botaoControles.setVisibility(ampliado && !pe ? View.VISIBLE : View.GONE);
            mostrarControles(!ampliado || pe);
        }
        aplicarZoom();
    }

    // Deslocamento t de um eixo: a imagem (de o a o+tam, na superficie de tamanho total) ampliada
    // por escala ocupa t+escala*o .. t+escala*(o+tam) na tela
    private float limitar(float t, float total, float o, float tam) {
        float ini = escala * o, fim = escala * (o + tam);
        if (fim - ini <= total) return (total - (fim - ini)) / 2 - ini;
        return Math.max(total - fim, Math.min(-ini, t));
    }

    private void mostrarControles(boolean sim) {
        esquerda.setVisibility(sim ? View.VISIBLE : View.GONE);
        faixaAbas.setVisibility(sim ? View.VISIBLE : View.GONE);
    }

    // Em pe a superficie do jogo ocupa so a area acima das abas e dos botoes: o jogo (4:3,
    // -fullscreenAspect) fica centrado nela e o zoom/arraste vale nela toda
    private static final int ALTURA_CONTROLES = 232;  // dp: abas (44+12) + 2 fileiras de botoes (2x66) + margens

    private int alturaJogo() {
        return emPe() ? Math.max(dp(200), mLayout.getHeight() - dp(ALTURA_CONTROLES)) : RelativeLayout.LayoutParams.MATCH_PARENT;
    }

    private void aplicarZoom() {
        if (mSurface == null) return;
        mSurface.setPivotX(0);
        mSurface.setPivotY(0);
        mSurface.setScaleX(escala);
        mSurface.setScaleY(escala);
        mSurface.setTranslationX(zoomX);
        mSurface.setTranslationY(zoomY + tecladoY);
        ajustarFundo();
    }

    private boolean sobre(View v, android.view.MotionEvent e) {
        if (v == null || v.getVisibility() != View.VISIBLE) return false;
        int[] o = new int[2];
        v.getLocationOnScreen(o);
        float x = e.getRawX(), y = e.getRawY();
        return x >= o[0] && x < o[0] + v.getWidth() && y >= o[1] && y < o[1] + v.getHeight();
    }

    @Override
    public boolean dispatchTouchEvent(android.view.MotionEvent e) {
        int acao = e.getActionMasked();
        if (acao == android.view.MotionEvent.ACTION_DOWN) {
            // botoes, abas, "Carregando...": toque normal
            deTela = mSurface != null && carregando == null
                && !sobre(esquerda, e) && !sobre(faixaAbas, e) && !sobre(botao100, e) && !sobre(botaoControles, e);
            pincando = false;
            tocandoJogo = false;
        }
        if (!deTela) return super.dispatchTouchEvent(e);
        pinca.onTouchEvent(e);
        if (acao == android.view.MotionEvent.ACTION_POINTER_DOWN && !pincando) {
            pincando = true;
            handler.removeCallbacks(soltarRetidos);
            descartarRetidos();
            if (tocandoJogo) {  // o 1o dedo ja tinha ido pro jogo: cancela
                android.view.MotionEvent c = android.view.MotionEvent.obtain(e);
                c.setAction(android.view.MotionEvent.ACTION_CANCEL);
                super.dispatchTouchEvent(c);
                c.recycle();
            }
        }
        if (pincando) {
            arrastar(e);
            return true;
        }
        if (!tocandoJogo) {
            retidos.add(android.view.MotionEvent.obtain(e));
            if (acao == android.view.MotionEvent.ACTION_DOWN) handler.postDelayed(soltarRetidos, ESPERA_PINCA);
            else if (acao == android.view.MotionEvent.ACTION_UP || acao == android.view.MotionEvent.ACTION_CANCEL) {
                handler.removeCallbacks(soltarRetidos);
                soltarRetidos();  // toque rapido: clique normal
            }
            return true;
        }
        return super.dispatchTouchEvent(e);
    }

    // Dois dedos arrastando movem a imagem ampliada (o ponto medio dos dedos)
    private void arrastar(android.view.MotionEvent e) {
        int acao = e.getActionMasked(), n = e.getPointerCount();
        if (acao != android.view.MotionEvent.ACTION_MOVE) {
            focoX = -1;  // dedo entrou ou saiu: recomeca do ponto medio novo
            return;
        }
        float fx = 0, fy = 0;
        for (int i = 0; i < n; i++) { fx += e.getX(i); fy += e.getY(i); }
        fx /= n;
        fy /= n;
        if (focoX >= 0 && escala > 1) zoom(escala, zoomX + fx - focoX, zoomY + fy - focoY);
        focoX = fx;
        focoY = fy;
    }

    private void soltarRetidos() {
        tocandoJogo = true;
        for (android.view.MotionEvent r : retidos) {
            super.dispatchTouchEvent(r);
            r.recycle();
        }
        retidos.clear();
    }

    private void descartarRetidos() {
        for (android.view.MotionEvent r : retidos) r.recycle();
        retidos.clear();
    }

    // Pedido pro iniciar.exe: hwnd de uma aba, "*"/"-" pra escalar o jogador ou
    // "K<vk>" pra mandar uma tecla direto pro jogo
    private void comando(String texto) {
        try {
            Jogo.escrever(new File(jogo.jogo, "comando.txt"), texto);
        } catch (IOException e) {
            Log.e(TAG, "Falha ao mandar comando ao jogo", e);
        }
    }

    private boolean maceteDisparado;

    // Mesma sequencia do teclado fisico: segura Alt e Shift, aperta F4, solta tudo
    private static void altShiftF4() {
        SDLActivity.onNativeKeyDown(KeyEvent.KEYCODE_ALT_LEFT);
        SDLActivity.onNativeKeyDown(KeyEvent.KEYCODE_SHIFT_LEFT);
        SDLActivity.onNativeKeyDown(KeyEvent.KEYCODE_F4);
        SDLActivity.onNativeKeyUp(KeyEvent.KEYCODE_F4);
        SDLActivity.onNativeKeyUp(KeyEvent.KEYCODE_SHIFT_LEFT);
        SDLActivity.onNativeKeyUp(KeyEvent.KEYCODE_ALT_LEFT);
    }

    private static void tecla(int codigo) {
        SDLActivity.onNativeKeyDown(codigo);
        SDLActivity.onNativeKeyUp(codigo);
    }

    private void imersivo() {
        getWindow().getDecorView().setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
    }

    // Grade de 2 colunas centralizada numa faixa lateral
    private android.widget.GridLayout grade(int lado) {
        android.widget.GridLayout g = new android.widget.GridLayout(this);
        g.setColumnCount(2);
        RelativeLayout.LayoutParams p = new RelativeLayout.LayoutParams(
            RelativeLayout.LayoutParams.WRAP_CONTENT, RelativeLayout.LayoutParams.WRAP_CONTENT);
        p.addRule(lado);
        p.addRule(RelativeLayout.CENTER_VERTICAL);
        int m = dp(8);
        p.setMargins(m, 0, m, 0);
        mLayout.addView(g, p);
        return g;
    }

    // Formacoes do menu Seleccionar (F1..F12), Automatico (A) e Melhores (M)
    private static final String[] TATICAS = {
        "3-3-4", "3-4-3", "4-2-4", "4-3-3", "4-4-2", "4-5-1",
        "5-2-3", "5-3-2", "5-4-1", "5-5-0", "6-3-1", "6-4-0", "5-0-5",
    };

    // Formacao = tecla do menu Seleccionar. A F10 vai pelo iniciar.exe direto pro
    // jogo: pelo teclado o Windows usa ela pra ativar a barra de menus. Antes de
    // abrir a lista, o iniciar.exe conta os jogadores disponiveis (taticas.txt);
    // as formacoes que o elenco nao permite aparecem cinza, sem toque.
    private void taticas() {
        File arq = new File(jogo.jogo, "taticas.txt");
        arq.delete();
        comando("T");
        long ate = android.os.SystemClock.uptimeMillis() + 1500;
        handler.post(new Runnable() {
            @Override
            public void run() {
                if (!arq.exists() && android.os.SystemClock.uptimeMillis() < ate) { handler.postDelayed(this, 100); return; }
                String menu = "";
                try { if (arq.exists()) menu = Jogo.ler(arq); } catch (IOException ignored) { }
                mostrarTaticas(menu);
            }
        });
    }

    // taticas.txt = "G D M A": jogadores sem S/L de cada posicao (iniciar.exe le a lista
    // da janela do time). Mesma regra do menu do jogo (seg03:0e4c): 1 G e os D, M, A da
    // formacao. Sem leitura (vazio), tudo liberado.
    private static boolean[] permitidas(String disponiveis) {
        boolean[] pode = new boolean[TATICAS.length];
        java.util.Arrays.fill(pode, true);
        String[] c = disponiveis.trim().split("\\s+");
        if (c.length != 4) return pode;
        try {
            int g = Integer.parseInt(c[0]), d = Integer.parseInt(c[1]), m = Integer.parseInt(c[2]), a = Integer.parseInt(c[3]);
            for (int i = 0; i < TATICAS.length; i++) {
                String[] f = TATICAS[i].split("-");
                // o jogo confere o 6-4-0 como 6-4-1 (seg04:2B6D): sem atacante ele fica cinza
                int precisaA = TATICAS[i].equals("6-4-0") ? 1 : Integer.parseInt(f[2]);
                pode[i] = g >= 1 && d >= Integer.parseInt(f[0]) && m >= Integer.parseInt(f[1]) && a >= precisaA;
            }
        } catch (NumberFormatException ignorado) {
            java.util.Arrays.fill(pode, true);
        }
        return pode;
    }

    private void mostrarTaticas(String menu) {
        int n = TATICAS.length + 2;
        String[] nomes = new String[n], teclas = new String[n];
        boolean[] pode = new boolean[n], formacoes = permitidas(menu);
        for (int i = 0; i < TATICAS.length; i++) {
            nomes[i] = TATICAS[i];
            teclas[i] = i == 12 ? "T" : "F" + (i + 1);
            pode[i] = formacoes[i];
        }
        nomes[TATICAS.length] = "Automático";
        teclas[TATICAS.length] = "A";
        nomes[TATICAS.length + 1] = "Melhores";
        teclas[TATICAS.length + 1] = "M";
        pode[TATICAS.length] = true;
        pode[TATICAS.length + 1] = true;

        android.widget.ListView lista = new android.widget.ListView(this);
        lista.setBackgroundColor(Color.rgb(11, 61, 11));
        lista.setDivider(new android.graphics.drawable.ColorDrawable(Color.rgb(20, 82, 20)));
        lista.setDividerHeight(1);
        android.app.AlertDialog[] dialogo = new android.app.AlertDialog[1];
        lista.setAdapter(new android.widget.BaseAdapter() {
            @Override public int getCount() { return n; }
            @Override public Object getItem(int i) { return nomes[i]; }
            @Override public long getItemId(int i) { return i; }
            @Override public boolean areAllItemsEnabled() { return false; }
            @Override public boolean isEnabled(int i) { return pode[i]; }

            @Override
            public View getView(int i, View v, ViewGroup pai) {
                LinearLayout l = new LinearLayout(ElifootActivity.this);
                l.setGravity(Gravity.CENTER_VERTICAL);
                l.setPadding(dp(18), 0, dp(18), 0);
                l.setMinimumHeight(dp(40));
                android.widget.TextView t = new android.widget.TextView(ElifootActivity.this);
                t.setText(nomes[i]);
                t.setTextSize(TypedValue.COMPLEX_UNIT_SP, 16);
                t.setTypeface(android.graphics.Typeface.DEFAULT_BOLD);
                t.setTextColor(pode[i] ? Color.WHITE : Color.rgb(110, 140, 110));
                l.addView(t, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
                android.widget.TextView k = new android.widget.TextView(ElifootActivity.this);
                k.setText(pode[i] ? teclas[i] : "sem jogadores");
                k.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
                k.setTextColor(pode[i] ? Color.rgb(252, 254, 4) : Color.rgb(110, 140, 110));
                l.addView(k);
                return l;
            }
        });
        lista.setOnItemClickListener((p, v, i, id) -> {
            if (i == 9) comando("K" + 0x79);  // VK_F10
            else if (i == 12) tecla(KeyEvent.KEYCODE_T);  // 5-0-5: tecla T (tools/patch_505.py)
            else if (i < TATICAS.length) tecla(KeyEvent.KEYCODE_F1 + i);
            else tecla(i == TATICAS.length ? KeyEvent.KEYCODE_A : KeyEvent.KEYCODE_M);
            if (dialogo[0] != null) dialogo[0].dismiss();
        });
        dialogo[0] = new android.app.AlertDialog.Builder(this)
            .setTitle("Táticas")
            .setView(lista)
            .setOnDismissListener(d -> imersivo())
            .create();
        dialogo[0].show();
    }

    private Button botao(String texto, View.OnClickListener acao) {
        Button b = new Button(this);
        b.setText(texto);
        b.setAllCaps(false);
        b.setTextColor(Color.WHITE);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, texto.length() > 2 ? 15 : 20);
        b.setPadding(0, 0, 0, 0);
        GradientDrawable fundo = new GradientDrawable();
        fundo.setShape(GradientDrawable.OVAL);
        fundo.setColor(Color.argb(140, 0, 114, 0));
        fundo.setStroke(dp(2), Color.argb(200, 252, 254, 4));
        b.setBackground(fundo);
        b.setFocusable(false);  // nao rouba o foco do SDL (teclado fisico continua indo pro jogo)
        b.setOnClickListener(acao);
        // centro da celula (o padrao alinha pela linha de base do texto: grade torta)
        android.widget.GridLayout.LayoutParams p = new android.widget.GridLayout.LayoutParams(
            android.widget.GridLayout.spec(android.widget.GridLayout.UNDEFINED, android.widget.GridLayout.CENTER),
            android.widget.GridLayout.spec(android.widget.GridLayout.UNDEFINED, android.widget.GridLayout.CENTER));
        p.width = dp(56);
        p.height = dp(56);
        p.setMargins(dp(5), dp(5), dp(5), dp(5));
        b.setLayoutParams(p);
        return b;
    }

    private int dp(int v) {
        return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    // Arco de 3/4 de volta girando (o ProgressBar do tema e um anel em degrade:
    // pintado de uma cor so, parece parado)
    private static final class Girando extends View {
        private final android.graphics.Paint tinta = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        private final android.graphics.RectF area = new android.graphics.RectF();

        Girando(android.content.Context ctx, int cor, float espessura) {
            super(ctx);
            tinta.setColor(cor);
            tinta.setStyle(android.graphics.Paint.Style.STROKE);
            tinta.setStrokeWidth(espessura);
            tinta.setStrokeCap(android.graphics.Paint.Cap.ROUND);
        }

        @Override
        protected void onDraw(android.graphics.Canvas c) {
            float m = tinta.getStrokeWidth();
            area.set(m, m, getWidth() - m, getHeight() - m);
            float angulo = (android.os.SystemClock.uptimeMillis() % 900) * 360f / 900f;
            c.drawArc(area, angulo, 270, false, tinta);
            if (isShown()) postInvalidateOnAnimation();
        }
    }

    // Icone de Enter (seta que desce e vira pra esquerda), centrado no botao
    private static final class IconeEnter extends android.graphics.drawable.Drawable {
        private final android.graphics.Paint tinta = new android.graphics.Paint(android.graphics.Paint.ANTI_ALIAS_FLAG);
        private final android.graphics.Path seta = new android.graphics.Path();

        IconeEnter(float espessura) {
            tinta.setColor(Color.WHITE);
            tinta.setStyle(android.graphics.Paint.Style.STROKE);
            tinta.setStrokeWidth(espessura);
            tinta.setStrokeCap(android.graphics.Paint.Cap.ROUND);
            tinta.setStrokeJoin(android.graphics.Paint.Join.ROUND);
        }

        @Override
        public void draw(android.graphics.Canvas c) {
            android.graphics.Rect b = getBounds();
            float l = Math.min(b.width(), b.height()) * 0.40f;  // lado do icone
            float x0 = b.centerX() - l / 2, y0 = b.centerY() - l / 2;
            seta.reset();
            seta.moveTo(x0 + l, y0);                // desce
            seta.lineTo(x0 + l, y0 + l * 0.7f);
            seta.lineTo(x0, y0 + l * 0.7f);         // vai pra esquerda
            seta.moveTo(x0 + l * 0.3f, y0 + l * 0.4f);  // ponta
            seta.lineTo(x0, y0 + l * 0.7f);
            seta.lineTo(x0 + l * 0.3f, y0 + l);
            c.drawPath(seta, tinta);
        }

        @Override public void setAlpha(int a) { tinta.setAlpha(a); }
        @Override public void setColorFilter(android.graphics.ColorFilter f) { tinta.setColorFilter(f); }
        @Override public int getOpacity() { return android.graphics.PixelFormat.TRANSLUCENT; }
    }
}
