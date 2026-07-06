// ScreenHook.dll - injected into otvdmw.exe pra fake screen resolution +
// reposicionamento reativo de janelas via SetWinEventHook.
//
// Hooks de screen size:
//   GetSystemMetrics, GetDeviceCaps, SystemParametersInfo(SPI_GETWORKAREA),
//   GetMonitorInfo — todos retornam a resolucao fake configurada via
//   env vars ELIFOOT_FAKE_WIDTH / ELIFOOT_FAKE_HEIGHT.
//
// Reposicionamento:
//   SetWinEventHook em EVENT_OBJECT_SHOW dispara quando Delphi mostra uma
//   janela top-level. Callback restaura maximizado e centraliza no monitor
//   real. Uma unica vez por hwnd (guardado num set). Sem polling.

#include <windows.h>
#include <stdio.h>
#include <stdlib.h>
#include <unordered_set>
#include "minhook/include/MinHook.h"

typedef int  (WINAPI *GetSystemMetrics_t)(int);
typedef int  (WINAPI *GetDeviceCaps_t)(HDC, int);
typedef BOOL (WINAPI *SystemParametersInfoA_t)(UINT, UINT, PVOID, UINT);
typedef BOOL (WINAPI *SystemParametersInfoW_t)(UINT, UINT, PVOID, UINT);
typedef BOOL (WINAPI *GetMonitorInfoA_t)(HMONITOR, LPMONITORINFO);
typedef BOOL (WINAPI *GetMonitorInfoW_t)(HMONITOR, LPMONITORINFO);

static GetSystemMetrics_t o_GetSystemMetrics = NULL;
static GetDeviceCaps_t o_GetDeviceCaps = NULL;
static SystemParametersInfoA_t o_SystemParametersInfoA = NULL;
static SystemParametersInfoW_t o_SystemParametersInfoW = NULL;
static GetMonitorInfoA_t o_GetMonitorInfoA = NULL;
static GetMonitorInfoW_t o_GetMonitorInfoW = NULL;

static int g_FakeWidth = 640;
static int g_FakeHeight = 480;
static HMODULE g_hSelf = NULL;
static HWINEVENTHOOK g_hEvent = NULL;

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
        r->left = 0;
        r->top = 0;
        r->right = g_FakeWidth;
        r->bottom = g_FakeHeight;
        return TRUE;
    }
    return o_SystemParametersInfoA(uiAction, uiParam, pvParam, fWinIni);
}

static BOOL WINAPI hk_SystemParametersInfoW(UINT uiAction, UINT uiParam, PVOID pvParam, UINT fWinIni)
{
    if (uiAction == SPI_GETWORKAREA && pvParam != NULL)
    {
        RECT* r = (RECT*)pvParam;
        r->left = 0;
        r->top = 0;
        r->right = g_FakeWidth;
        r->bottom = g_FakeHeight;
        return TRUE;
    }
    return o_SystemParametersInfoW(uiAction, uiParam, pvParam, fWinIni);
}

// GetMonitorInfo:
// - rcMonitor mantido REAL (nao mexemos) — evita Windows tentar clampar
//   nossa janela pra dentro dela (que causava flicker no launcher)
// - rcWork vira retangulo com tamanho fake CENTRALIZADO no monitor real.
//   Delphi ShowWindow(SW_MAXIMIZE) usa rcWork como destino → janela ja
//   nasce centralizada e no tamanho certo.
static void CenterFakeWorkArea(LPMONITORINFO lpmi)
{
    LONG realW = lpmi->rcMonitor.right - lpmi->rcMonitor.left;
    LONG realH = lpmi->rcMonitor.bottom - lpmi->rcMonitor.top;
    LONG offX = lpmi->rcMonitor.left + (realW - g_FakeWidth) / 2;
    LONG offY = lpmi->rcMonitor.top + (realH - g_FakeHeight) / 2;
    if (offX < lpmi->rcMonitor.left) offX = lpmi->rcMonitor.left;
    if (offY < lpmi->rcMonitor.top) offY = lpmi->rcMonitor.top;
    lpmi->rcWork.left = offX;
    lpmi->rcWork.top = offY;
    lpmi->rcWork.right = offX + g_FakeWidth;
    lpmi->rcWork.bottom = offY + g_FakeHeight;
}

static BOOL WINAPI hk_GetMonitorInfoA(HMONITOR hMonitor, LPMONITORINFO lpmi)
{
    BOOL r = o_GetMonitorInfoA(hMonitor, lpmi);
    if (r && lpmi != NULL) CenterFakeWorkArea(lpmi);
    return r;
}

static BOOL WINAPI hk_GetMonitorInfoW(HMONITOR hMonitor, LPMONITORINFO lpmi)
{
    BOOL r = o_GetMonitorInfoW(hMonitor, lpmi);
    if (r && lpmi != NULL) CenterFakeWorkArea(lpmi);
    return r;
}

// ---- reposicionamento reativo ----

static std::unordered_set<HWND> g_processed;
static CRITICAL_SECTION g_setLock;
static BOOL g_setLockInit = FALSE;

static void GetRealPrimaryMonitor(int* rw, int* rh)
{
    // Chama o real (nao o hook) direto pra pegar tamanho real do monitor.
    HMONITOR hm = MonitorFromWindow(NULL, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO mi;
    mi.cbSize = sizeof(mi);
    if (hm != NULL && o_GetMonitorInfoA != NULL && o_GetMonitorInfoA(hm, &mi))
    {
        // o_GetMonitorInfoA nao passa pelo hook, entao rcMonitor eh real
        *rw = mi.rcMonitor.right - mi.rcMonitor.left;
        *rh = mi.rcMonitor.bottom - mi.rcMonitor.top;
    }
    else
    {
        *rw = 1920; *rh = 1080;
    }
}

static void CALLBACK WinEventProc(
    HWINEVENTHOOK, DWORD event, HWND hwnd, LONG idObject, LONG idChild,
    DWORD, DWORD)
{
    // So OBJID_WINDOW no proprio hwnd (nao filhos, nao caret, nao cursor)
    if (idObject != OBJID_WINDOW || idChild != 0) return;
    if (hwnd == NULL || !IsWindow(hwnd)) return;
    // So janela top-level (nao child controls)
    if (GetAncestor(hwnd, GA_ROOT) != hwnd) return;

    // Filtra janelas relevantes: pelo menos visiveis com WS_OVERLAPPED
    LONG style = GetWindowLong(hwnd, GWL_STYLE);
    if ((style & WS_VISIBLE) == 0) return;
    // Ignora janelas de sistema (tooltips, popups minusculos, etc)
    RECT r;
    if (!GetWindowRect(hwnd, &r)) return;
    LONG w = r.right - r.left;
    LONG h = r.bottom - r.top;
    if (w < 100 || h < 80) return;

    EnterCriticalSection(&g_setLock);
    if (g_processed.find(hwnd) != g_processed.end()) {
        LeaveCriticalSection(&g_setLock);
        return;
    }
    g_processed.insert(hwnd);
    LeaveCriticalSection(&g_setLock);

    int rw, rh;
    GetRealPrimaryMonitor(&rw, &rh);

    BOOL isMax = (style & WS_MAXIMIZE) != 0;
    if (isMax)
    {
        // Restaura pra tamanho normal, depois centraliza no monitor real
        ShowWindow(hwnd, SW_RESTORE);
        // Depois do restore, GetWindowRect pode ter mudado; releo
        if (GetWindowRect(hwnd, &r))
        {
            w = r.right - r.left;
            h = r.bottom - r.top;
        }
        // Se o tamanho restaurado ainda for maior que fake, encolhe
        if (w > g_FakeWidth) w = g_FakeWidth;
        if (h > g_FakeHeight) h = g_FakeHeight;
        int x = (rw - w) / 2;
        int y = (rh - h) / 2;
        SetWindowPos(hwnd, NULL, x, y, w, h,
            SWP_NOZORDER | SWP_NOACTIVATE);
    }
    else if (r.left < 50 && r.top < 50)
    {
        // Delphi Form.Left/Top = 0 => janela no canto superior esquerdo.
        // Centraliza sem mexer no tamanho.
        int x = (rw - w) / 2;
        int y = (rh - h) / 2;
        if (x < 0) x = 0;
        if (y < 0) y = 0;
        SetWindowPos(hwnd, NULL, x, y, 0, 0,
            SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSIZE);
    }
}

static void InstallHooks(void)
{
    // Le config de env vars
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

    MH_EnableHook(MH_ALL_HOOKS);

    // SetWinEventHook INCONTEXT filtrado pra este proprio processo.
    // Callback dispara na thread da janela (sincrono, sem marshalling).
    InitializeCriticalSection(&g_setLock);
    g_setLockInit = TRUE;
    g_hEvent = SetWinEventHook(
        EVENT_OBJECT_SHOW, EVENT_OBJECT_SHOW,
        g_hSelf, WinEventProc,
        GetCurrentProcessId(), 0,
        WINEVENT_INCONTEXT);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            g_hSelf = hModule;
            DisableThreadLibraryCalls(hModule);
            InstallHooks();
            break;
        case DLL_PROCESS_DETACH:
            if (g_hEvent) { UnhookWinEvent(g_hEvent); g_hEvent = NULL; }
            if (g_setLockInit) { DeleteCriticalSection(&g_setLock); g_setLockInit = FALSE; }
            MH_Uninitialize();
            break;
    }
    return TRUE;
}
