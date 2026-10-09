#!/usr/bin/env bash
# Baixa as dependencias do app Android (versoes fixas) e monta os assets.
#   app/jni/SDL                 SDL2 (codigo + android-project)
#   third_party/Boxedwine       emulador (x86 + Wine) que roda o jogo
#   app/src/main/assets/        wine11.zip, w16.zip (modulos 16-bit) e elifoot.zip
# uso: android/preparar.sh
set -euo pipefail

AND="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$AND/.." && pwd)"
CACHE="${ELIFOOT_CACHE:-$AND/.cache}"
SDL_VER="2.32.10"
BW_COMMIT="a127f191582123b17220e1c2944321571a4e451b"   # Boxedwine, 06/10/2026
BW_WEB="https://github.com/danoon2/Boxedwine/releases/download/26R1.0/Boxedwine26R1Web.zip"
BW_FULLFS="https://boxedwine.org/v2/10/TinyCore15Wine11.0.zip"   # FS completo do Wine 11
mkdir -p "$CACHE" "$AND/third_party" "$AND/app/src/main/assets"

baixar() {  # $1 = url, $2 = destino
  [[ -s "$2" ]] || { curl -fL --retry 3 -o "$2.tmp" "$1" && mv "$2.tmp" "$2"; }
}

# --- SDL2 ---
if [[ ! -f "$AND/app/jni/SDL/CMakeLists.txt" ]]; then
  baixar "https://github.com/libsdl-org/SDL/releases/download/release-$SDL_VER/SDL2-$SDL_VER.tar.gz" "$CACHE/sdl2.tar.gz"
  rm -rf "$AND/app/jni/SDL" && mkdir -p "$AND/app/jni/SDL"
  tar xzf "$CACHE/sdl2.tar.gz" -C "$AND/app/jni/SDL" --strip-components=1
fi

# --- Boxedwine ---
if [[ "$(git -C "$AND/third_party/Boxedwine" rev-parse HEAD 2>/dev/null)" != "$BW_COMMIT" ]]; then
  rm -rf "$AND/third_party/Boxedwine"
  git init -q "$AND/third_party/Boxedwine"
  git -C "$AND/third_party/Boxedwine" fetch -q --depth 1 https://github.com/danoon2/Boxedwine.git "$BW_COMMIT"
  git -C "$AND/third_party/Boxedwine" checkout -q FETCH_HEAD
  # Ajustes pro NDK (atomic_ref no clang/C++20, SDL_Android* no startupArgs)
  git -C "$AND/third_party/Boxedwine" apply "$AND/patches/boxedwine-android.patch"
fi

# --- Assets ---
A="$AND/app/src/main/assets"
if [[ ! -s "$A/wine11.zip" ]]; then
  baixar "$BW_WEB" "$CACHE/bw-web.zip"
  unzip -p "$CACHE/bw-web.zip" Wine11/boxedwine.zip > "$A/wine11.zip"
fi
if [[ ! -s "$A/w16.zip" ]]; then
  # O FS reduzido do Wine 11 nao traz os modulos 16-bit (toolhelp, krnl386 PE...)
  baixar "$BW_FULLFS" "$CACHE/fullfs.zip"
  T="$(mktemp -d)"
  unzip -Z1 "$A/wine11.zip" | sort > "$T/tem.lst"
  unzip -Z1 "$CACHE/fullfs.zip" | grep -E '(16|16\.so)$' | sort > "$T/16.lst"
  comm -23 "$T/16.lst" "$T/tem.lst" > "$T/falta.lst"
  (cd "$T" && unzip -q "$CACHE/fullfs.zip" $(cat falta.lst) -d ov && cd ov && zip -qr "$A/w16.zip" .)
  rm -rf "$T"
fi
# Jogo + iniciar.exe (linux/iniciar.c: manda um movimento de mouse pro jogo
# exibir a janela "Acerca" da abertura, que no Wine nao aparece sozinha)
rm -f "$A/elifoot.zip"
T="$(mktemp -d)"
cp -R "$ROOT/game" "$T/jogo" && rm -f "$T/jogo/CRACK.EXE"
# Nomes que nao sao UTF-8 (EQUIPAS/ARA<0x80>A_BR.EFT, C cedilha do DOS) viram
# ASCII, como no macOS: o jogo lista a pasta, nao depende do nome
python3 - "$T/jogo" <<'PY'
import os, sys, unicodedata
for raiz, pastas, arqs in os.walk(os.fsencode(sys.argv[1])):
    for nome in arqs:
        try:
            nome.decode('utf-8')
        except UnicodeDecodeError:
            novo = unicodedata.normalize('NFKD', nome.decode('cp850')).encode('ascii', 'ignore')
            os.rename(os.path.join(raiz, nome), os.path.join(raiz, novo))
PY
(cd "$T/jogo" && zip -qr "$A/elifoot.zip" .)
i686-w64-mingw32-gcc -O2 -s -mwindows -o "$T/iniciar.exe" "$ROOT/linux/iniciar.c"
(cd "$T" && zip -q "$A/elifoot.zip" iniciar.exe)
rm -rf "$T"
# eli.cod feito pra data fixa das pastas (o mesmo do Linux/macOS)
cp "$ROOT/linux/eli.cod" "$A/eli.cod"
# Fontes com as medidas da Arial/Times New Roman (android/fontes/gerar.py)
rm -rf "$A/fontes" && mkdir -p "$A/fontes" && cp "$AND"/fontes/*.ttf "$A/fontes/"
ls -lh "$A"
