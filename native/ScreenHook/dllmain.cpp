// ScreenHook.dll - injected into otvdmw.exe pra fake screen resolution.
//
// Hooka GetSystemMetrics, GetDeviceCaps e SystemParametersInfo(SPI_GETWORKAREA)
// pra retornar valores da resolucao configurada via env vars
// ELIFOOT_FAKE_WIDTH e ELIFOOT_FAKE_HEIGHT. Assim Delphi 1 forms e dialogs
// que consultam Screen.Width/Screen.Height computam layout no tamanho fake,
// nao no tamanho real do desktop.

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

static GetSystemMetrics_t o_GetSystemMetrics = NULL;
static GetDeviceCaps_t o_GetDeviceCaps = NULL;
static SystemParametersInfoA_t o_SystemParametersInfoA = NULL;
static SystemParametersInfoW_t o_SystemParametersInfoW = NULL;
static GetMonitorInfoA_t o_GetMonitorInfoA = NULL;
static GetMonitorInfoW_t o_GetMonitorInfoW = NULL;
static SetWindowPos_t o_SetWindowPos = NULL;
static MoveWindow_t o_MoveWindow = NULL;

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

// Delphi Form.Show chama SetWindowPos/MoveWindow com Left=Top=0 do DFM.
// Interceptamos: se a chamada tem coordenada de origem (0,0) e o tamanho
// eh de dialog (>200x150), substituimos por centralizado na tela real.
static void GetRealPrimaryMonitor(int* rw, int* rh)
{
    HMONITOR hm = MonitorFromWindow(NULL, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO mi = { sizeof(mi) };
    if (hm != NULL && o_GetMonitorInfoA != NULL && o_GetMonitorInfoA(hm, &mi))
    {
        *rw = mi.rcMonitor.right - mi.rcMonitor.left;
        *rh = mi.rcMonitor.bottom - mi.rcMonitor.top;
    }
    else
    {
        *rw = 1920; *rh = 1080;
    }
}

static void MaybeCenterCoords(int* X, int* Y, int cx, int cy)
{
    if (*X == 0 && *Y == 0 && cx >= 200 && cy >= 150)
    {
        int rw, rh;
        GetRealPrimaryMonitor(&rw, &rh);
        int nx = (rw - cx) / 2;
        int ny = (rh - cy) / 2;
        if (nx < 0) nx = 0;
        if (ny < 0) ny = 0;
        *X = nx;
        *Y = ny;
    }
}

static BOOL WINAPI hk_SetWindowPos(HWND hWnd, HWND hAfter, int X, int Y, int cx, int cy, UINT uFlags)
{
    // So mexe se o caller esta setando POSICAO explicita (nao SWP_NOMOVE)
    if (!(uFlags & SWP_NOMOVE))
    {
        MaybeCenterCoords(&X, &Y, cx, cy);
    }
    return o_SetWindowPos(hWnd, hAfter, X, Y, cx, cy, uFlags);
}

static BOOL WINAPI hk_MoveWindow(HWND hWnd, int X, int Y, int nWidth, int nHeight, BOOL bRepaint)
{
    MaybeCenterCoords(&X, &Y, nWidth, nHeight);
    return o_MoveWindow(hWnd, X, Y, nWidth, nHeight, bRepaint);
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
    // SetWindowPos/MoveWindow hooks removidos — briga com centralizacao
    // do lado C# gerava flicker. Centralizacao fica no ResizeWhenReady.

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
