# ScreenHook.dll

DLL injetada no processo `otvdmw.exe` (via CreateProcess suspended + CreateRemoteThread) que hooka APIs Windows relacionadas a resolução de tela pra retornar valores "fake" configurados pelo launcher.

## APIs hookadas

- `GetSystemMetrics(SM_CXSCREEN / SM_CYSCREEN / SM_CXFULLSCREEN / SM_CYFULLSCREEN / SM_CXVIRTUALSCREEN / SM_CYVIRTUALSCREEN / SM_CXMAXIMIZED / SM_CYMAXIMIZED / SM_CXMAXTRACK / SM_CYMAXTRACK)` → `ELIFOOT_FAKE_WIDTH` / `ELIFOOT_FAKE_HEIGHT`
- `GetDeviceCaps(HORZRES / VERTRES)` → idem
- `SystemParametersInfoA/W(SPI_GETWORKAREA)` → RECT(0,0,W,H)

## Configuração via env vars

Definidas pelo launcher antes de spawnar otvdmw:
- `ELIFOOT_FAKE_WIDTH=640`
- `ELIFOOT_FAKE_HEIGHT=480`

Se não definidas, defaults são 640×480.

## Build

Local (Windows com Visual Studio 2022):
```
cmake -A Win32 -S native/ScreenHook -B build/screenhook
cmake --build build/screenhook --config Release
```

Saída: `build/screenhook/Release/ScreenHook.dll`

CI: `.github/workflows/release.yml` faz build automático no `windows-latest`.

## Dependência

- [MinHook](https://github.com/TsudaKageyu/minhook) (BSD-2, source bundled em `minhook/`)
