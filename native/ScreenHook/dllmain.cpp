// ScreenHook.dll - injected into otvdmw.exe
//
// Faz Elifoot rodar em uma janela fake_size centralizada no monitor
// real, SEM polling do lado do launcher. Tudo sincrono via hooks:
//
// 1. Fake screen size (GetSystemMetrics/GetDeviceCaps/SPI_GETWORKAREA/
//    GetMonitorInfo.rcWork) — Delphi calcula layout no tamanho fake.
// 2. GetMonitorInfo.rcMonitor + rcWork = retangulo fake CENTRALIZADO no
//    monitor real. Windows SW_MAXIMIZE resulta em fake_size centrada.
// 3. ShowWindow: se SW_MAXIMIZE em top-level, faz o restore normal e
//    reposiciona explicitamente pra fake_size centralizado (defensivo
//    caso Delphi bypasse rcWork).
// 4. SetWindowPos/MoveWindow: se caller pediu (0,0) em top-level com
//    tamanho de janela real, centraliza no monitor real. Pega dialogs
//    Delphi que usam DFM Left=Top=0.

#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include "minhook/include/MinHook.h"

typedef int  (WINAPI *GetSystemMetrics_t)(int);
typedef int  (WINAPI *GetDeviceCaps_t)(HDC, int);
typedef BOOL (WINAPI *SystemParametersInfoA_t)(UINT, UINT, PVOID, UINT);
typedef BOOL (WINAPI *SystemParametersInfoW_t)(UINT, UINT, PVOID, UINT);
typedef BOOL (WINAPI *GetMonitorInfoA_t)(HMONITOR, LPMONITORINFO);
typedef BOOL (WINAPI *GetMonitorInfoW_t)(HMONITOR, LPMONITORINFO);
typedef BOOL (WINAPI *SetWindowPos_t)(HWND, HWND, int, int, int, int, UINT);
typedef BOOL (WINAPI *MoveWindow_t)(HWND, int, int, int, int, BOOL);
typedef BOOL (WINAPI *ShowWindow_t)(HWND, int);

static GetSystemMetrics_t o_GetSystemMetrics = NULL;
static GetDeviceCaps_t o_GetDeviceCaps = NULL;
static SystemParametersInfoA_t o_SystemParametersInfoA = NULL;
static SystemParametersInfoW_t o_SystemParametersInfoW = NULL;
static GetMonitorInfoA_t o_GetMonitorInfoA = NULL;
static GetMonitorInfoW_t o_GetMonitorInfoW = NULL;
static SetWindowPos_t o_SetWindowPos = NULL;
static MoveWindow_t o_MoveWindow = NULL;
static ShowWindow_t o_ShowWindow = NULL;

static int g_FakeWidth = 640;
static int g_FakeHeight = 480;

static int WINAPI hk_GetSystemMetrics(int nIndex)
{
    switch (nIndex)
    {
        case SM_CXSCREEN:
        case SM_CXFULLSCREEN:
        case SM_CXVIRTUALSCREEN:
        case SM_CXMAXIMIZED:
        case SM_CXMAXTRACK:
            return g_FakeWidth;
        case SM_CYSCREEN:
        case SM_CYFULLSCREEN:
        case SM_CYVIRTUALSCREEN:
        case SM_CYMAXIMIZED:
        case SM_CYMAXTRACK:
            return g_FakeHeight;
    }
    return o_GetSystemMetrics(nIndex);
}

static int WINAPI hk_GetDeviceCaps(HDC hdc, int index)
{
    if (index == HORZRES) return g_FakeWidth;
    if (index == VERTRES) return g_FakeHeight;
    return o_GetDeviceCaps(hdc, index);
}

static BOOL WINAPI hk_SystemParametersInfoA(UINT uiAction, UINT uiParam, PVOID pvParam, UINT fWinIni)
{
    if (uiAction == SPI_GETWORKAREA && pvParam != NULL)
    {
        RECT* r = (RECT*)pvParam;
        r->left = 0; r->top = 0;
        r->right = g_FakeWidth; r->bottom = g_FakeHeight;
        return TRUE;
    }
    return o_SystemParametersInfoA(uiAction, uiParam, pvParam, fWinIni);
}

static BOOL WINAPI hk_SystemParametersInfoW(UINT uiAction, UINT uiParam, PVOID pvParam, UINT fWinIni)
{
    if (uiAction == SPI_GETWORKAREA && pvParam != NULL)
    {
        RECT* r = (RECT*)pvParam;
        r->left = 0; r->top = 0;
        r->right = g_FakeWidth; r->bottom = g_FakeHeight;
        return TRUE;
    }
    return o_SystemParametersInfoW(uiAction, uiParam, pvParam, fWinIni);
}

// GetMonitorInfo:
// rcMonitor + rcWork = tamanho fake CENTRALIZADO no monitor real.
// SW_MAXIMIZE consulta rcWork → maximizacao resulta em janela fake_size
// centrada. Sem polling C# depois.
static void ApplyFakeCentered(LPMONITORINFO lpmi)
{
    LONG realW = lpmi->rcMonitor.right - lpmi->rcMonitor.left;
    LONG realH = lpmi->rcMonitor.bottom - lpmi->rcMonitor.top;
    LONG offX = lpmi->rcMonitor.left + (realW - g_FakeWidth) / 2;
    LONG offY = lpmi->rcMonitor.top + (realH - g_FakeHeight) / 2;
    if (offX < lpmi->rcMonitor.left) offX = lpmi->rcMonitor.left;
    if (offY < lpmi->rcMonitor.top) offY = lpmi->rcMonitor.top;
    lpmi->rcMonitor.left = offX;
    lpmi->rcMonitor.top = offY;
    lpmi->rcMonitor.right = offX + g_FakeWidth;
    lpmi->rcMonitor.bottom = offY + g_FakeHeight;
    lpmi->rcWork = lpmi->rcMonitor;
}

static BOOL WINAPI hk_GetMonitorInfoA(HMONITOR hMonitor, LPMONITORINFO lpmi)
{
    BOOL r = o_GetMonitorInfoA(hMonitor, lpmi);
    if (r && lpmi != NULL) ApplyFakeCentered(lpmi);
    return r;
}

static BOOL WINAPI hk_GetMonitorInfoW(HMONITOR hMonitor, LPMONITORINFO lpmi)
{
    BOOL r = o_GetMonitorInfoW(hMonitor, lpmi);
    if (r && lpmi != NULL) ApplyFakeCentered(lpmi);
    return r;
}

// ---- reposicionamento sincrono via hook direto ----

static BOOL IsTopLevel(HWND h)
{
    return h != NULL && GetAncestor(h, GA_ROOT) == h;
}

// Pega tamanho REAL do monitor primario (chama o unhooked GetMonitorInfoA
// direto — mas nosso hook re-escreve rcMonitor. Precisamos de outra rota).
// Usamos GetDesktopWindow + GetWindowRect_original... mais simples: usar
// EnumDisplayMonitors com o real. Ou GetSystemMetrics_original.
static void GetRealPrimaryMonitor(int* rw, int* rh)
{
    // GetSystemMetrics hookado retorna fake. Precisamos do real.
    // Chamar o_GetSystemMetrics.
    if (o_GetSystemMetrics)
    {
        *rw = o_GetSystemMetrics(SM_CXSCREEN);
        *rh = o_GetSystemMetrics(SM_CYSCREEN);
    }
    else
    {
        *rw = 1920; *rh = 1080;
    }
}

// Forward declaration — TranslateFakeToReal esta definido depois.
static BOOL TranslateFakeToReal(HWND hwnd, int* X, int* Y, int cx, int cy);

// hk_ShowWindow: intercepta antes de mostrar top-level.
// - SW_MAXIMIZE/SW_SHOWMAXIMIZED: converte pra SHOWNORMAL + fake_size
//   centralizado. Pega main + Journey (WindowState=wsMaximized).
// - SW_SHOW/SW_SHOWNORMAL/SW_SHOWNA: se janela ta em (< 50, < 50) com
//   tamanho de janela real, centraliza no monitor real ANTES do show.
//   Pega dialogs Delphi com DFM Left=Top=0 que nao passam pelo
//   SetWindowPos hook (criacao via CreateWindowExA direto).
static BOOL WINAPI hk_ShowWindow(HWND hwnd, int nCmdShow)
{
    if (!IsTopLevel(hwnd)) return o_ShowWindow(hwnd, nCmdShow);

    if (nCmdShow == SW_MAXIMIZE || nCmdShow == SW_SHOWMAXIMIZED)
    {
        BOOL r = o_ShowWindow(hwnd, SW_SHOWNORMAL);
        int rw, rh; GetRealPrimaryMonitor(&rw, &rh);
        int x = (rw - g_FakeWidth) / 2;
        int y = (rh - g_FakeHeight) / 2;
        o_SetWindowPos(hwnd, NULL, x, y, g_FakeWidth, g_FakeHeight,
            SWP_NOZORDER | SWP_NOACTIVATE);
        return r;
    }

    if (nCmdShow == SW_SHOW || nCmdShow == SW_SHOWNORMAL
        || nCmdShow == SW_SHOWNA || nCmdShow == SW_SHOWNOACTIVATE
        || nCmdShow == SW_SHOWDEFAULT)
    {
        RECT r;
        if (GetWindowRect(hwnd, &r))
        {
            int x = r.left, y = r.top;
            int w = r.right - r.left, h = r.bottom - r.top;
            if (TranslateFakeToReal(hwnd, &x, &y, w, h))
            {
                // Move ANTES de tornar visivel — sem flash na posicao antiga
                o_SetWindowPos(hwnd, NULL, x, y, 0, 0,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSIZE);
            }
        }
    }
    return o_ShowWindow(hwnd, nCmdShow);
}

// Delphi calcula coords assumindo tela = fake_size na origem (0,0).
// Em vez de traduzir preservando layout, forcamos CENTRALIZACAO na
// tela real. Se coords estao no retangulo fake (0..fake_w, 0..fake_h),
// centraliza no monitor real. Se estao fora (usuario arrastou), deixa.
// Filtro top-level + tamanho >= 200x150 evita mexer em tooltips/menus.
static BOOL TranslateFakeToReal(HWND hwnd, int* X, int* Y, int cx, int cy)
{
    if (!IsTopLevel(hwnd)) return FALSE;
    if (cx < 200 || cy < 150) return FALSE;
    if (*X < 0 || *Y < 0) return FALSE;
    if (*X > g_FakeWidth || *Y > g_FakeHeight) return FALSE;
    int rw, rh; GetRealPrimaryMonitor(&rw, &rh);
    int nx = (rw - cx) / 2;
    int ny = (rh - cy) / 2;
    if (nx < 0) nx = 0;
    if (ny < 0) ny = 0;
    *X = nx;
    *Y = ny;
    return TRUE;
}

static BOOL WINAPI hk_SetWindowPos(HWND hWnd, HWND hAfter, int X, int Y, int cx, int cy, UINT uFlags)
{
    if (!(uFlags & SWP_NOMOVE))
    {
        int useCx = cx, useCy = cy;
        if (uFlags & SWP_NOSIZE)
        {
            RECT r;
            if (GetWindowRect(hWnd, &r))
            {
                useCx = r.right - r.left;
                useCy = r.bottom - r.top;
            }
        }
        TranslateFakeToReal(hWnd, &X, &Y, useCx, useCy);
    }
    return o_SetWindowPos(hWnd, hAfter, X, Y, cx, cy, uFlags);
}

static BOOL WINAPI hk_MoveWindow(HWND hWnd, int X, int Y, int nWidth, int nHeight, BOOL bRepaint)
{
    TranslateFakeToReal(hWnd, &X, &Y, nWidth, nHeight);
    return o_MoveWindow(hWnd, X, Y, nWidth, nHeight, bRepaint);
}

static void InstallHooks(void)
{
    char buf[64];
    DWORD n = GetEnvironmentVariableA("ELIFOOT_FAKE_WIDTH", buf, sizeof(buf));
    if (n > 0 && n < sizeof(buf)) { int v = atoi(buf); if (v > 0) g_FakeWidth = v; }
    n = GetEnvironmentVariableA("ELIFOOT_FAKE_HEIGHT", buf, sizeof(buf));
    if (n > 0 && n < sizeof(buf)) { int v = atoi(buf); if (v > 0) g_FakeHeight = v; }

    if (MH_Initialize() != MH_OK) return;

    MH_CreateHook((LPVOID)&GetSystemMetrics, (LPVOID)&hk_GetSystemMetrics, (LPVOID*)&o_GetSystemMetrics);
    MH_CreateHook((LPVOID)&GetDeviceCaps, (LPVOID)&hk_GetDeviceCaps, (LPVOID*)&o_GetDeviceCaps);
    MH_CreateHook((LPVOID)&SystemParametersInfoA, (LPVOID)&hk_SystemParametersInfoA, (LPVOID*)&o_SystemParametersInfoA);
    MH_CreateHook((LPVOID)&SystemParametersInfoW, (LPVOID)&hk_SystemParametersInfoW, (LPVOID*)&o_SystemParametersInfoW);
    MH_CreateHook((LPVOID)&GetMonitorInfoA, (LPVOID)&hk_GetMonitorInfoA, (LPVOID*)&o_GetMonitorInfoA);
    MH_CreateHook((LPVOID)&GetMonitorInfoW, (LPVOID)&hk_GetMonitorInfoW, (LPVOID*)&o_GetMonitorInfoW);
    MH_CreateHook((LPVOID)&SetWindowPos, (LPVOID)&hk_SetWindowPos, (LPVOID*)&o_SetWindowPos);
    MH_CreateHook((LPVOID)&MoveWindow, (LPVOID)&hk_MoveWindow, (LPVOID*)&o_MoveWindow);
    MH_CreateHook((LPVOID)&ShowWindow, (LPVOID)&hk_ShowWindow, (LPVOID*)&o_ShowWindow);

    MH_EnableHook(MH_ALL_HOOKS);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            DisableThreadLibraryCalls(hModule);
            InstallHooks();
            break;
        case DLL_PROCESS_DETACH:
            MH_Uninitialize();
            break;
    }
    return TRUE;
}
