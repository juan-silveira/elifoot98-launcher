/*
 * iniciar.exe: abre o comando recebido (otvdmw.exe + jogo, ou so o jogo no
 * Android) e "acorda" a janela principal com um movimento de mouse. O Elifoot
 * so exibe a janela "Acerca" da abertura depois de receber alguma entrada; no
 * Windows o proprio mouse sobre a janela faz isso, mas no Wine (desktop
 * virtual do Linux, driver do macOS, Boxedwine no Android) nada chega e o
 * jogo fica numa tela verde vazia.
 *
 * uso: iniciar.exe [/android PASTA] comando...
 *   /android: modo do app Android. Enquanto o jogo roda, na PASTA:
 *     teclado.txt  "1 esq topo dir base" quando um campo de texto (TEdit/TMemo
 *                  do Delphi) tem o foco, "0" quando nao (o app abre/fecha o
 *                  teclado virtual e sobe a imagem pro campo ficar visivel)
 *     janelas.txt  janelas abertas do jogo, uma por linha: "ativa<TAB>hwnd<TAB>titulo"
 *                  (as abas da lateral do app; a principal fica de fora)
 *     pronto.txt   criado quando a janela do jogo aparece (fim do "Carregando...")
 *     comando.txt  escrito pelo app com o hwnd (hex) de uma aba tocada (essa
 *                  janela vem pra frente) ou "*"/"-" (botoes ✱/—: escala como
 *                  titular/reserva o jogador selecionado na lista) ou "K<vk>"
 *                  (tecla direto pro jogo: F10 da tatica 5-5-0) ou "T" (pede o
 *                  estado das taticas: grava taticas.txt)
 *     taticas.txt  "G D M A": jogadores disponiveis (sem S/L) de cada posicao,
 *                  pro app acinzentar as taticas como o menu do jogo; vazio se
 *                  nao deu pra ler a lista
 *   No Boxedwine as janelas que o jogo abre podem ficar atras da principal;
 *   alem das abas, toda janela nova vem pra frente sozinha.
 *
 * Compilar: i686-w64-mingw32-gcc -O2 -s -mwindows -o iniciar.exe iniciar.c
 */
#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define MAX_JANELAS 64

static char pasta[MAX_PATH];
static DWORD processo_jogo, processo_aberto;
static HWND vistas[MAX_JANELAS], atuais[MAX_JANELAS];
static int n_vistas, n_atuais;

static HWND janela_principal(void)
{
    /* otvdm prefixa as classes 16-bit; o Wine do Boxedwine nao */
    HWND w = FindWindowA("WIN1611B7TmainWindow", NULL);
    if (!w) w = FindWindowA("TmainWindow", NULL);
    return w && IsWindowVisible(w) ? w : NULL;
}

static BOOL acerca_visivel(void)
{
    HWND w = FindWindowA(NULL, "Acerca");
    return w && IsWindowVisible(w);
}

static void pra_frente(HWND w)
{
    SetWindowPos(w, HWND_TOP, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
    SetForegroundWindow(w);
}

/* Janela "de verdade" do jogo: visivel, com tamanho e titulo (fora a
 * TApplication do Delphi, de tamanho zero, e listas de combo/menus) */
static BOOL janela_do_jogo(HWND w)
{
    DWORD pid;
    RECT r;
    char classe[32];
    GetWindowThreadProcessId(w, &pid);
    if (pid != processo_jogo || !IsWindowVisible(w) || GetWindowTextLengthA(w) == 0) return FALSE;
    GetWindowRect(w, &r);
    if (r.right - r.left <= 0 || r.bottom - r.top <= 0) return FALSE;
    GetClassNameA(w, classe, sizeof(classe));
    return lstrcmpiA(classe, "ComboLBox") != 0 && lstrcmpiA(classe, "#32768") != 0;
}

static BOOL CALLBACK listar(HWND w, LPARAM nao_usado)
{
    (void)nao_usado;
    if (n_atuais < MAX_JANELAS && janela_do_jogo(w)) atuais[n_atuais++] = w;
    return TRUE;
}

static BOOL ja_vista(HWND w)
{
    int i;
    for (i = 0; i < n_vistas; i++) if (vistas[i] == w) return TRUE;
    return FALSE;
}

static void caminho(char *destino, const char *nome)
{
    wsprintfA(destino, "%s\\%s", pasta, nome);
}

static void gravar_texto(const char *nome, const char *texto)
{
    char p[MAX_PATH];
    HANDLE h;
    DWORD n;
    caminho(p, nome);
    h = CreateFileA(p, GENERIC_WRITE, FILE_SHARE_READ, NULL, CREATE_ALWAYS, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return;
    WriteFile(h, texto, lstrlenA(texto), &n, NULL);
    CloseHandle(h);
}

/* Campo de texto com foco na janela em primeiro plano? Preenche o retangulo */
static int campo_de_texto(RECT *r)
{
    GUITHREADINFO gti;
    char classe[64];
    HWND frente = GetForegroundWindow();
    if (!frente) return 0;
    memset(&gti, 0, sizeof(gti));
    gti.cbSize = sizeof(gti);
    if (!GetGUIThreadInfo(GetWindowThreadProcessId(frente, NULL), &gti) || !gti.hwndFocus)
        return 0;
    if (!GetClassNameA(gti.hwndFocus, classe, sizeof(classe))) return 0;
    CharLowerA(classe);
    if (strstr(classe, "edit") == NULL && strstr(classe, "memo") == NULL) return 0;
    GetWindowRect(gti.hwndFocus, r);
    return 1;
}

static void atualizar_teclado(void)
{
    static char ultimo[64] = "";
    char agora[64] = "0";
    RECT r;
    if (campo_de_texto(&r))
        wsprintfA(agora, "1 %ld %ld %ld %ld", r.left, r.top, r.right, r.bottom);
    if (lstrcmpA(agora, ultimo) != 0) {
        gravar_texto("teclado.txt", agora);
        lstrcpyA(ultimo, agora);
    }
}

/* Lista as janelas do jogo, traz pra frente as novas (ou a modal escondida que
 * bloqueia a principal) e grava janelas.txt quando algo muda */
static void atualizar_janelas(HWND principal)
{
    static char ultimo[4096] = "";
    char texto[4096] = "", linha[160], titulo[100];
    HWND frente, nova = NULL, habilitada = NULL;
    int i;

    GetWindowThreadProcessId(principal, &processo_jogo);
    n_atuais = 0;
    EnumWindows(listar, 0);  /* do topo pra baixo */
    for (i = 0; i < n_atuais; i++) {
        if (!nova && !ja_vista(atuais[i])) nova = atuais[i];
        if (!habilitada && atuais[i] != principal && IsWindowEnabled(atuais[i])) habilitada = atuais[i];
    }
    if (nova && nova != principal && GetForegroundWindow() != nova) pra_frente(nova);
    else if (!IsWindowEnabled(principal) && habilitada && GetForegroundWindow() != habilitada) pra_frente(habilitada);
    memcpy(vistas, atuais, sizeof(HWND) * n_atuais);
    n_vistas = n_atuais;

    /* A principal (fundo verde) fica fora das abas: as outras pertencem a
     * ela e sempre ficam por cima, entao a aba dela nunca trocaria nada */
    frente = GetForegroundWindow();
    for (i = 0; i < n_atuais; i++) {
        if (atuais[i] == principal) continue;
        GetWindowTextA(atuais[i], titulo, sizeof(titulo));
        wsprintfA(linha, "%d\t%lx\t%s\r\n", atuais[i] == frente, (unsigned long)(ULONG_PTR)atuais[i], titulo);
        if (lstrlenA(texto) + lstrlenA(linha) < (int)sizeof(texto)) lstrcatA(texto, linha);
    }
    if (lstrcmpA(texto, ultimo) != 0) {
        gravar_texto("janelas.txt", texto);
        lstrcpyA(ultimo, texto);
    }
}

/* Marca do jogador numa linha da lista de escalacao (TListBox desenhada pelo
 * jogo): conta as linhas de pixels diferentes do fundo na coluna da marca.
 * 0 = fora do jogo, '-' = reserva (traco, 2 linhas), '*' = titular (bola, 5) */
static int marca(HWND lista, int item)
{
    RECT r;
    HDC dc;
    COLORREF fundo;
    int x, y, linhas = 0;
    if (SendMessageA(lista, LB_GETITEMRECT, item, (LPARAM)&r) == LB_ERR) return -1;
    dc = GetDC(lista);
    fundo = GetPixel(dc, 12, (r.top + r.bottom) / 2);
    for (y = r.top + 2; y < r.bottom - 2; y++)
        for (x = 2; x < 9; x++)
            if (GetPixel(dc, x, y) != fundo) { linhas++; break; }
    ReleaseDC(lista, dc);
    return linhas == 0 ? 0 : linhas <= 3 ? '-' : '*';
}

/* Botoes ✱ e — do app: o jogo so troca a marca com um clique na coluna dela,
 * sempre na ordem titular -> reserva -> fora -> titular. Le a marca atual e
 * clica quantas vezes faltam (a imagem lida logo depois do clique ainda e a
 * antiga no Boxedwine, entao nao da pra conferir clique a clique) */
static void marcar(int pedida)
{
    static const char ciclo[] = { '*', '-', 0 };
    GUITHREADINFO gti;
    char classe[32];
    RECT r;
    HWND lista;
    int item, i, de, para, cliques;
    memset(&gti, 0, sizeof(gti));
    gti.cbSize = sizeof(gti);
    if (!GetGUIThreadInfo(GetWindowThreadProcessId(GetForegroundWindow(), NULL), &gti)) return;
    lista = gti.hwndFocus;
    if (!lista || !GetClassNameA(lista, classe, sizeof(classe)) || lstrcmpiA(classe, "TListBox") != 0) return;
    item = (int)SendMessageA(lista, LB_GETCURSEL, 0, 0);
    if (item < 0 || SendMessageA(lista, LB_GETITEMRECT, item, (LPARAM)&r) == LB_ERR) return;
    de = marca(lista, item);
    for (i = 0; i < 3; i++) if (ciclo[i] == de) break;
    for (para = 0; para < 3; para++) if (ciclo[para] == pedida) break;
    cliques = i < 3 ? (para - i + 3) % 3 : 0;
    for (i = 0; i < cliques; i++) {
        if (i) Sleep(200);
        SendMessageA(lista, WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM(5, (r.top + r.bottom) / 2));
        SendMessageA(lista, WM_LBUTTONUP, 0, MAKELPARAM(5, (r.top + r.bottom) / 2));
    }
}

/* "K<vk>": manda a tecla direto pro controle com foco no jogo. Pra F10: pelo
 * teclado ela vira WM_SYSKEYDOWN e ativa a barra de menus em vez da tatica */
static void tecla(int vk)
{
    GUITHREADINFO gti;
    HWND w;
    memset(&gti, 0, sizeof(gti));
    gti.cbSize = sizeof(gti);
    if (!GetGUIThreadInfo(GetWindowThreadProcessId(GetForegroundWindow(), NULL), &gti)) return;
    w = gti.hwndFocus ? gti.hwndFocus : gti.hwndActive;
    if (!w) return;
    PostMessageA(w, WM_KEYDOWN, vk, 1);
    PostMessageA(w, WM_KEYUP, vk, 0xC0000001);
}

/* "T": quais formacoes o elenco permite. O jogo acinzenta no menu Seleccionar
 * as que nao da pra montar (seg03:0e4c): conta os jogadores sem suspensao (S) e
 * sem lesao (L) de cada posicao e exige 1 G e os D, M, A da formacao. O menu de
 * um programa de 16 bits nao e legivel de fora no Wine, entao a conta e refeita
 * aqui a partir da lista de jogadores da janela do time (TprepareGameDlg):
 *  - ela vem agrupada G, D, M, A; a ultima linha de cada grupo e mais alta (tem
 *    o traco separador embaixo);
 *  - S e L aparecem numa coluna entre a forca e o salario.
 * Grava em taticas.txt "G D M A" (disponiveis) ou nada se a lista nao fechar. */
#define SL_X0 180   /* colunas do S (x 189-195) e do L (213-219) na lista, entre */
#define SL_X1 232   /* a forca (termina em 176) e o salario (comeca em 287)    */

static HWND lista_time;

static BOOL CALLBACK achar_lista(HWND w, LPARAM nao_usado)
{
    char classe[32];
    if (GetClassNameA(w, classe, sizeof(classe)) && lstrcmpiA(classe, "TListBox") == 0
        && IsWindowVisible(w) && SendMessageA(w, LB_GETCOUNT, 0, 0) > 8) {
        lista_time = w;
        return FALSE;
    }
    return TRUE;
}

static void taticas(void)
{
    HWND w;
    int n, i, x, y, grupo = 0, cont[4] = {0, 0, 0, 0}, total = 0;
    char texto[64] = "";
    HDC dc;
    lista_time = NULL;
    for (w = GetTopWindow(NULL); w; w = GetWindow(w, GW_HWNDNEXT)) {
        char classe[32];
        if (!IsWindowVisible(w) || !GetClassNameA(w, classe, sizeof(classe))) continue;
        if (lstrcmpA(classe, "TprepareGameDlg") == 0) { EnumChildWindows(w, achar_lista, 0); break; }
        if (lstrcmpA(classe, "TmainWindow") != 0 && lstrcmpA(classe, "TApplication") != 0 && janela_do_jogo(w))
            break;  /* outra janela do jogo por cima: a lista pode estar coberta */
    }
    if (!lista_time) { gravar_texto("taticas.txt", ""); return; }
    n = (int)SendMessageA(lista_time, LB_GETCOUNT, 0, 0);
    dc = GetDC(lista_time);
    for (i = 0; i < n; i++) {
        RECT r;
        COLORREF fundo;
        int fora = 0, alt;
        if (SendMessageA(lista_time, LB_GETITEMRECT, i, (LPARAM)&r) == LB_ERR) { grupo = 9; break; }
        alt = r.bottom - r.top;
        fundo = GetPixel(dc, 12, r.top + 8);
        for (x = SL_X0; x < SL_X1 && !fora; x++)
            for (y = r.top + 2; y < r.top + 14; y++)
                if (GetPixel(dc, x, y) != fundo) { fora = 1; break; }
        if (grupo > 3) break;
        if (!fora) cont[grupo]++;
        total++;
        if (alt > 17) grupo++;  /* ultima linha do grupo: mais alta, tem o traco separador */
    }
    ReleaseDC(lista_time, dc);
    /* a ultima linha da lista nao tem traco: o grupo dela e o A (o 4o) */
    if (total == n && grupo == 3) sprintf(texto, "%d %d %d %d", cont[0], cont[1], cont[2], cont[3]);
    gravar_texto("taticas.txt", texto);
}

/* comando.txt: hwnd (hex) de uma aba tocada, "*"/"-" dos botoes ✱/—, "K<vk>" ou "T" */
static void ler_comando(void)
{
    char p[MAX_PATH], buf[32] = "";
    HANDLE h;
    DWORD n = 0;
    HWND w;
    caminho(p, "comando.txt");
    h = CreateFileA(p, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, NULL, OPEN_EXISTING, 0, NULL);
    if (h == INVALID_HANDLE_VALUE) return;
    ReadFile(h, buf, sizeof(buf) - 1, &n, NULL);
    CloseHandle(h);
    if (n == 0) return;
    gravar_texto("comando.txt", "");
    buf[n] = 0;
    if (buf[0] == '*' || buf[0] == '-') { marcar(buf[0]); return; }
    if (buf[0] == 'K') { tecla(atoi(buf + 1)); return; }
    if (buf[0] == 'T') { taticas(); return; }
    w = (HWND)(ULONG_PTR)strtoul(buf, NULL, 16);
    if (w && IsWindow(w)) pra_frente(w);
}

/* O jogo desenha quase tudo em Arial (e um pouco em Times New Roman). O Wine
 * do Boxedwine nao tem essas fontes nem o fontconfig do Linux pra achar uma
 * parecida, e cai numa substituta maior: o texto das listas fica cortado. O app
 * poe as Liberation (mesmas medidas, licenca livre) em C:\windows\Fonts e aqui
 * o Wine aprende a troca (Elifoot Sans/Serif: android/fontes). */
static void trocar_fontes(void)
{
    static const char *trocas[][2] = {
        { "Arial", "Elifoot Sans" },
        { "Times New Roman", "Elifoot Serif" },
    };
    HKEY k;
    int i;
    if (RegCreateKeyExA(HKEY_CURRENT_USER, "Software\\Wine\\Fonts\\Replacements", 0, NULL, 0,
                        KEY_SET_VALUE, NULL, &k, NULL) != ERROR_SUCCESS) return;
    for (i = 0; i < 2; i++)
        RegSetValueExA(k, trocas[i][0], 0, REG_SZ, (const BYTE *)trocas[i][1], lstrlenA(trocas[i][1]) + 1);
    RegCloseKey(k);
}

/* Primeira janela visivel (com tamanho) do programa aberto: lParam -> HWND */
static BOOL CALLBACK achar_visivel(HWND w, LPARAM achada)
{
    DWORD pid;
    RECT r;
    GetWindowThreadProcessId(w, &pid);
    GetWindowRect(w, &r);
    if (pid == processo_aberto && IsWindowVisible(w) && r.right - r.left > 0 && r.bottom - r.top > 0) {
        *(HWND *)achada = w;
        return FALSE;
    }
    return TRUE;
}

/* Pula um argumento (com ou sem aspas) da linha de comando */
static char *proximo(char *p, char *destino, int max)
{
    int i = 0;
    while (*p == ' ') p++;
    if (*p == '"') {
        p++;
        while (*p && *p != '"') { if (i < max - 1) destino[i++] = *p; p++; }
        if (*p) p++;
    } else {
        while (*p && *p != ' ') { if (i < max - 1) destino[i++] = *p; p++; }
    }
    destino[i] = 0;
    while (*p == ' ') p++;
    return p;
}

int WINAPI WinMain(HINSTANCE inst, HINSTANCE prev, LPSTR cmd, int show)
{
    STARTUPINFOA si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    HWND principal = NULL;
    char opcao[32];
    int i, tentativas = 0, acerca_ok = 0, pronto = 0;

    (void)inst; (void)prev; (void)show;
    while (*cmd == ' ') cmd++;
    if (_strnicmp(cmd, "/android ", 9) == 0) {
        cmd = proximo(cmd, opcao, sizeof(opcao));
        cmd = proximo(cmd, pasta, sizeof(pasta));
    }
    /* O Boxedwine nao enxerga arquivos criados fora dele com o jogo aberto:
     * o comando.txt nasce aqui e o app so reescreve o conteudo */
    if (pasta[0]) {
        gravar_texto("comando.txt", "");
        trocar_fontes();
    }
    if (!CreateProcessA(NULL, cmd, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi))
        return 1;
    processo_aberto = pi.dwProcessId;

    for (i = 0; WaitForSingleObject(pi.hProcess, 250) == WAIT_TIMEOUT; i++)
    {
        if (!principal) principal = janela_principal();
        /* o app troca o "Carregando..." pela imagem do programa (o jogo ou o
         * Editor de Equipes, que nao tem a janela principal do jogo) */
        if (pasta[0] && !pronto) {
            HWND w = NULL;
            EnumWindows(achar_visivel, (LPARAM)&w);
            if (w) { gravar_texto("pronto.txt", "1"); pronto = 1; }
        }

        /* Ate a "Acerca" aparecer (60 s): WM_MOUSEMOVE 1x por segundo, ate 10 */
        if (principal && !acerca_ok && i < 240 && i % 4 == 3) {
            if (acerca_visivel()) acerca_ok = 1;
            else if (tentativas < 10) {
                PostMessageA(principal, WM_MOUSEMOVE, 0, MAKELPARAM(20, 20));
                tentativas++;
            }
        }

        if (pasta[0]) {
            if (principal) {
                ler_comando();
                atualizar_janelas(principal);
            }
            atualizar_teclado();
        } else if (acerca_ok || tentativas >= 10 || i >= 240) {
            break;  /* Linux/macOS: so espera o jogo terminar */
        }
    }
    if (pasta[0]) {
        gravar_texto("teclado.txt", "0");
        gravar_texto("janelas.txt", "");
    }

    WaitForSingleObject(pi.hProcess, INFINITE);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
