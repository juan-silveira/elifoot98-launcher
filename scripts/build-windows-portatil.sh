#!/usr/bin/env bash
# Monta o .exe portatil do Windows em dist/:
#   Elifoot98-<versao>-Windows-portatil.exe
# O launcher multiplataforma (src-cross, Avalonia) em win-x86, com o .NET dentro do
# .exe: alternativa ao instalador para quem nao consegue abrir o launcher classico
# (.NET Framework 4.8). Um arquivo so, sem instalar: o pacote (jogo, otvdm, fontes,
# ScreenHook) vai embutido e e extraido em %LOCALAPPDATA%\Elifoot98 na 1a abertura
# (src-cross/WindowsJogo.cs).
# Tem o mesmo do Linux: o otvdm build 2703 (linux/otvdm-master-2703.zip, com o
# AdjustDPI do Tamanho do jogo) e a MS Sans Serif TrueType (linux/fontes-hd).
# uso: scripts/build-windows-portatil.sh [versao] [ScreenHook.dll]
#   ScreenHook.dll: a DLL x86 de native/ScreenHook (o CI a compila com o MSVC).
#   Sem ela, e compilada aqui com o i686-w64-mingw32-gcc (o codigo e C).
# requer .NET SDK 8 e o vc_redist do scripts/fetch-deps.sh (de onde sai a
# vcruntime140.dll que o otvdm usa) e o cabextract; no Windows (CI) a DLL vem do
# SysWOW64.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VER="${1:-0.0.0}"
NUMVER="${VER%%-*}"   # o .NET so aceita versao numerica
[[ "$NUMVER" =~ ^[0-9]+(\.[0-9]+)*$ ]] || NUMVER=0.0.0
HOOK="${2:-}"
DIST="$ROOT/dist"
mkdir -p "$DIST"

# No Git Bash do Windows (CI): caminhos no formato do Windows para o dotnet, e o
# 7z quando faltar zip/unzip
win() { command -v cygpath >/dev/null && cygpath -w "$1" || echo "$1"; }
extrair() { if command -v unzip >/dev/null; then unzip -q "$1" -d "$2"; else 7z x -y -o"$2" "$1" >/dev/null; fi; }
compactar() { if command -v zip >/dev/null; then zip -qr "$1" .; else 7z a -tzip "$(win "$1")" . >/dev/null; fi; }

T="$(mktemp -d)"
trap 'rm -rf "$T"' EXIT
P="$T/pacote"
mkdir -p "$P/vendor" "$P/fontes-hd"

cp -r "$ROOT/game" "$P/"
rm -f "$P/game/CRACK.EXE"
extrair "$ROOT/linux/otvdm-master-2703.zip" "$T/otvdm-zip"
mv "$T/otvdm-zip"/otvdm-* "$P/vendor/otvdm"
cp "$ROOT/linux/fontes-hd/"*.ttf "$P/fontes-hd/"

# O otvdm usa a vcruntime140.dll (x86): ao lado do otvdmw.exe, sem precisar
# instalar o Visual C++ (o resto do runtime, a UCRT, ja vem no Windows 10)
VCRT=/c/Windows/SysWOW64/vcruntime140.dll
if [[ -f "$VCRT" ]]; then
  cp "$VCRT" "$P/vendor/otvdm/"
else
  [[ -f "$ROOT/vendor/vcredist/vc_redist.x86.exe" ]] || bash "$ROOT/scripts/fetch-deps.sh"
  mkdir "$T/vc" && (cd "$T/vc" && cabextract -q "$ROOT/vendor/vcredist/vc_redist.x86.exe" \
    && for c in a*; do cabextract -q -F vcruntime140.dll_x86 "$c" 2>/dev/null || true; done)
  cp "$T/vc/vcruntime140.dll_x86" "$P/vendor/otvdm/vcruntime140.dll"
fi

if [[ -n "$HOOK" && -f "$HOOK" ]]; then
  cp "$HOOK" "$P/ScreenHook.dll"
elif command -v i686-w64-mingw32-gcc >/dev/null; then
  SH="$ROOT/native/ScreenHook"
  i686-w64-mingw32-gcc -O2 -s -shared -static-libgcc -I"$SH" -I"$SH/minhook/include" \
    -x c "$SH/dllmain.cpp" -x none "$SH"/minhook/src/{buffer,hook,trampoline}.c "$SH/minhook/src/hde/hde32.c" \
    -o "$P/ScreenHook.dll" -luser32 -lgdi32
else
  echo "AVISO: sem ScreenHook.dll; o jogo abre sem o Tamanho do jogo" >&2
fi
(cd "$P" && compactar "$T/pacote.zip")

# Launcher em 32 bits: o ScreenHook.dll e injetado no otvdmw.exe (32 bits)
dotnet publish "$(win "$ROOT/src-cross")" -c Release -r win-x86 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:DebugType=none -p:Version="$NUMVER" -p:PacotePortatil="$(win "$T/pacote.zip")" -o "$(win "$T/pub")" >/dev/null

EXE="$DIST/Elifoot98-$VER-Windows-portatil.exe"
cp "$T/pub/ElifootLauncher.exe" "$EXE"
ls -la "$EXE"
