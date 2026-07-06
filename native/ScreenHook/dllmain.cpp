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
typedef BOOL (WINAPI *GetWindowRect_t)(HWND, LPRECT);
typedef BOOL (WINAPI *GetClientRect_t)(HWND, LPRECT);

static GetSystemMetrics_t o_GetSystemMetrics = NULL;
static GetDeviceCaps_t o_GetDeviceCaps = NULL;
static SystemParametersInfoA_t o_SystemParametersInfoA = NULL;
static SystemParametersInfoW_t o_SystemParametersInfoW = NULL;
static GetMonitorInfoA_t o_GetMonitorInfoA = NULL;
static GetMonitorInfoW_t o_GetMonitorInfoW = NULL;

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

// GetMonitorInfo eh o que Windows usa pra decidir tamanho de maximizacao.
// Sobreescrever rcMonitor e rcWork faz maximizacao virar do tamanho fake.
static BOOL WINAPI hk_GetMonitorInfoA(HMONITOR hMonitor, LPMONITORINFO lpmi)
{
    BOOL r = o_GetMonitorInfoA(hMonitor, lpmi);
    if (r && lpmi != NULL)
    {
        lpmi->rcMonitor.left = 0;
        lpmi->rcMonitor.top = 0;
        lpmi->rcMonitor.right = g_FakeWidth;
        lpmi->rcMonitor.bottom = g_FakeHeight;
        lpmi->rcWork.left = 0;
        lpmi->rcWork.top = 0;
        lpmi->rcWork.right = g_FakeWidth;
        lpmi->rcWork.bottom = g_FakeHeight;
    }
    return r;
}

static BOOL WINAPI hk_GetMonitorInfoW(HMONITOR hMonitor, LPMONITORINFO lpmi)
{
    BOOL r = o_GetMonitorInfoW(hMonitor, lpmi);
    if (r && lpmi != NULL)
    {
        lpmi->rcMonitor.left = 0;
        lpmi->rcMonitor.top = 0;
        lpmi->rcMonitor.right = g_FakeWidth;
        lpmi->rcMonitor.bottom = g_FakeHeight;
        lpmi->rcWork.left = 0;
        lpmi->rcWork.top = 0;
        lpmi->rcWork.right = g_FakeWidth;
        lpmi->rcWork.bottom = g_FakeHeight;
    }
    return r;
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
