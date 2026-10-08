/*
 * iniciar.exe: abre o comando recebido (otvdmw.exe + jogo) e "acorda" a
 * janela principal com um movimento de mouse. O Elifoot so exibe a janela
 * "Acerca" da abertura depois de receber alguma entrada; no Windows o
 * proprio mouse sobre a janela faz isso, mas no Wine (desktop virtual do
 * Linux e driver do macOS) nada chega e o jogo fica numa tela verde vazia.
 *
 * Compilar: i686-w64-mingw32-gcc -O2 -s -mwindows -o iniciar.exe iniciar.c
 */
#include <windows.h>

static BOOL acerca_visivel(void)
{
    HWND w = FindWindowA(NULL, "Acerca");
    return w && IsWindowVisible(w);
}

int WINAPI WinMain(HINSTANCE inst, HINSTANCE prev, LPSTR cmd, int show)
{
    STARTUPINFOA si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    HWND principal = NULL;
    int i;

    (void)inst; (void)prev; (void)show;
    if (!CreateProcessA(NULL, cmd, NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi))
        return 1;

    /* espera a janela principal do jogo (ate 60 s) */
    for (i = 0; i < 600 && !principal; i++)
    {
        if (WaitForSingleObject(pi.hProcess, 100) == WAIT_OBJECT_0)
            break;
        principal = FindWindowA("WIN1611B7TmainWindow", NULL);
        if (principal && !IsWindowVisible(principal))
            principal = NULL;
    }

    /* um WM_MOUSEMOVE basta; repete por garantia ate a "Acerca" aparecer */
    for (i = 0; principal && i < 10 && !acerca_visivel(); i++)
    {
        Sleep(1000);
        PostMessageA(principal, WM_MOUSEMOVE, 0, MAKELPARAM(20, 20));
    }

    WaitForSingleObject(pi.hProcess, INFINITE);
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    return 0;
}
