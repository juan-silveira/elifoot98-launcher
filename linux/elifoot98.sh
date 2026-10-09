#!/usr/bin/env bash
# Elifoot 98 no Linux e no macOS: roda o jogo (ou o editor de equipes) no Wine
# via otvdm. Chamado pelo launcher (ElifootLauncher).
# uso: elifoot98.sh [jogo|editor|preparar]
#   preparar: so copia os arquivos pra pasta gravavel (sem Wine) e sai
#
# Variaveis opcionais:
#   ELIFOOT_RES=1024x768         tamanho da janela do jogo (padrao 800x600, so Linux)
#   ELIFOOT_FULLSCREEN=1         sem janela (jogo ocupa a tela; padrao no macOS)
#   ELIFOOT_WINE=/caminho/wine   forca um Wine especifico
# Compativel com o bash 3.2 do macOS e com as ferramentas BSD.
set -euo pipefail

# SRC = arquivos do pacote (AppImage, /opt do .deb e o .app sao somente leitura).
# APP = copia gravavel, de onde o jogo roda.
SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ "$(uname)" == "Darwin" ]]; then
  MAC=1
  APP="$HOME/Library/Application Support/Elifoot98"
  # Gcenx (mesma fonte do Homebrew), x86_64 wow64: roda 32-bit e, no Apple Silicon, via Rosetta 2
  RUNTIME_URL="https://github.com/Gcenx/macOS_Wine_builds/releases/download/11.18/wine-devel-11.18-osx64.tar.xz"
  RUNTIME_SHA256="aa0ea4c82e636ae7bca2076387cb0a5affa26509ad13f119ecd0d62bd7ba6f82"
  RUNTIME_MB=180
else
  MAC=0
  APP="${XDG_DATA_HOME:-$HOME/.local/share}/elifoot98"
  RUNTIME_URL="https://github.com/Kron4ek/Wine-Builds/releases/download/11.19/wine-11.19-amd64-wow64.tar.xz"
  RUNTIME_SHA256="a1fe858b45d12c9b16b91c7601d9b934f68430f7b6b95e240212ab6e2a198d8a"
  RUNTIME_MB=95
fi
WIN="$APP/vendor/otvdm/WINDOWS"
RUNTIME="$APP/wine-runtime"
RES="${ELIFOOT_RES:-800x600}"
# O driver do macOS nao tem desktop virtual: la o jogo abre maximizado
FULLSCREEN="${ELIFOOT_FULLSCREEN:-$MAC}"
MODO="${1:-jogo}"
EXE="ELIFOOT.EXE"
[[ "$MODO" == "editor" ]] && EXE="EDITEQ.EXE"

export WINEPREFIX="$APP/prefix" WINEDEBUG=-all WINEDLLOVERRIDES="mscoree,mshtml="

# Texto pra dentro de string do AppleScript
as_texto() { printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'; }

aviso() {
  if [[ $MAC == 1 ]]; then osascript -e "display notification \"$(as_texto "$1")\" with title \"Elifoot 98\"" 2>/dev/null || true
  elif command -v notify-send >/dev/null; then notify-send -i "$APP/linux/elifoot98.png" "Elifoot 98" "$1" 2>/dev/null || true
  else echo "$1"; fi
}

erro() {
  if [[ $MAC == 1 ]]; then osascript -e "display alert \"Elifoot 98\" message \"$(as_texto "$1")\" as critical" >/dev/null 2>&1 || true
  elif command -v zenity >/dev/null; then zenity --error --title="Elifoot 98" --text="$1" 2>/dev/null || true
  else echo "ERRO: $1" >&2; fi
  exit 1
}

confirmar() {
  if [[ $MAC == 1 ]]; then
    osascript -e "button returned of (display dialog \"$(as_texto "$1")\" with title \"Elifoot 98\" buttons {\"Não\", \"Sim\"} default button \"Sim\")" 2>/dev/null | grep -q Sim
  elif command -v zenity >/dev/null; then zenity --question --title="Elifoot 98" --text="$1" 2>/dev/null
  else read -r -p "$1 [s/N] " r; [[ "$r" =~ ^[sS] ]]; fi
}

sha256_ok() {  # $1 = arquivo
  if command -v sha256sum >/dev/null; then echo "$RUNTIME_SHA256  $1" | sha256sum -c --quiet - >/dev/null 2>&1
  else echo "$RUNTIME_SHA256  $1" | shasum -a 256 -c - >/dev/null 2>&1; fi
}

baixar_wine() {
  confirmar "O Elifoot 98 precisa do Wine com suporte a programas 32-bit, que não foi encontrado.

Baixar um Wine portátil agora? (~$RUNTIME_MB MB, só desta vez)" || erro "Instale o Wine (com suporte a 32-bit) e tente de novo."
  local tmp; tmp="$(mktemp -d)"
  if [[ $MAC == 0 ]] && command -v zenity >/dev/null; then
    curl -fsSL -o "$tmp/wine.tar.xz" "$RUNTIME_URL" | zenity --progress --pulsate --auto-close --no-cancel \
      --title="Elifoot 98" --text="Baixando o Wine..." 2>/dev/null || true
  else
    aviso "Baixando o Wine (~$RUNTIME_MB MB), aguarde..."
    curl -fsSL -o "$tmp/wine.tar.xz" "$RUNTIME_URL" || true
  fi
  sha256_ok "$tmp/wine.tar.xz" || { rm -rf "$tmp"; erro "Download do Wine falhou ou veio corrompido."; }
  tar xJf "$tmp/wine.tar.xz" -C "$tmp" 2>/dev/null
  rm -rf "$RUNTIME"
  if [[ $MAC == 1 ]]; then mv "$tmp"/Wine*.app/Contents/Resources/wine "$RUNTIME"
  else mv "$tmp"/wine-*/ "$RUNTIME"; fi
  rm -rf "$tmp"
}

escolher_wine() {
  if [[ -n "${ELIFOOT_WINE:-}" ]]; then WINE="$ELIFOOT_WINE"; return; fi
  if [[ -x "$RUNTIME/bin/wine" ]]; then WINE="$RUNTIME/bin/wine"; return; fi
  # Debian/Ubuntu: wine sem o pacote wine32 nao roda o otvdm (32-bit)
  if command -v wine >/dev/null; then
    local v; v="$(env -u WINEDEBUG wine --version 2>&1 || true)"  # com WINEDEBUG=-all o aviso some
    if [[ "$v" != *"wine32 is missing"* ]]; then WINE=wine; return; fi
  fi
  baixar_wine
  WINE="$RUNTIME/bin/wine"
}

# Copia o jogo pra pasta gravavel na 1a execucao e quando a versao muda.
# Saves (game/JOGOS) e configuracoes (WINDOWS/elif98.ini) sao preservados.
sincronizar() {
  [[ "$(cat "$APP/.versao" 2>/dev/null)" == "$(cat "$SRC/VERSAO")" ]] && return
  mkdir -p "$APP/game/JOGOS" "$APP/vendor" "$APP/linux"
  cp -R "$SRC/vendor/otvdm" "$APP/vendor/"
  local f
  for f in "$SRC/game/"*; do
    [[ "$(basename "$f")" == "JOGOS" ]] || cp -R "$f" "$APP/game/"
  done
  cp -Rn "$SRC/game/JOGOS/." "$APP/game/JOGOS/" 2>/dev/null || true
  # Versoes antigas traziam EQUIPAS/ARA<0x80>A_BR.EFT (C cedilha do DOS, nome
  # que nao e UTF-8); agora e ARACA_BR.EFT. Apaga o antigo pra nao duplicar.
  rm -f "$APP/game/EQUIPAS/ARA"$'\x80'"A_BR.EFT"
  cp "$SRC/linux/"* "$APP/linux/"
  chmod -R u+w "$APP"
  cp "$SRC/VERSAO" "$APP/.versao"
}

# O jogo deriva a senha do eli.cod das datas das pastas WINDOWS e
# WINDOWS/SYSTEM do otvdm. No Linux/macOS essas datas mudam quando o jogo
# grava arquivos, entao elas sao fixadas antes de cada execucao e o eli.cod
# e um feito para essa data (o registro, secondCode no elif98.ini, e gravado
# pelo "Ativar todos os recursos" do launcher).
preparar_arquivos() {
  mkdir -p "$WIN/SYSTEM"
  cmp -s "$APP/linux/eli.cod" "$WIN/eli.cod" 2>/dev/null || cp "$APP/linux/eli.cod" "$WIN/eli.cod"
  [[ -f "$WIN/elif98.ini" ]] || printf '[System]\r\n' > "$WIN/elif98.ini"
}

sincronizar
preparar_arquivos
[[ "$MODO" == "preparar" ]] && exit 0
escolher_wine

# Prefixo criado por outro Wine (ex.: o do sistema sem 32-bit) e recriado
if [[ "$(cat "$APP/.wine" 2>/dev/null)" != "$WINE" ]]; then
  rm -rf "$WINEPREFIX"
  echo "$WINE" > "$APP/.wine"
fi
if [[ ! -f "$WINEPREFIX/system.reg" ]]; then
  aviso "Preparando o Elifoot 98 (só na primeira vez, leva alguns segundos)..."
  "$WINE" wineboot -i >/dev/null 2>&1 || erro "O Wine não conseguiu iniciar."
fi
# E: = pasta do jogo (caminhos curtos pro programa 16-bit)
ln -sfn "$APP" "$WINEPREFIX/dosdevices/e:"

# O otvdm entrega a data em UTC: o instante fixo e 2000-01-01 14:00 UTC (o
# linux/eli.cod foi gerado pra ele), seja qual for o fuso do computador
TZ=UTC touch -t 200001011400.00 "$WIN" "$WIN/SYSTEM"

# iniciar.exe (linux/iniciar.c) abre o otvdm e manda um movimento de mouse
# pro jogo: sem isso, no Wine, a janela "Acerca" da abertura nao aparece
OTVDM='E:\vendor\otvdm\otvdmw.exe'
INICIAR=""
[[ -f "$APP/linux/iniciar.exe" ]] && INICIAR='E:\linux\iniciar.exe'

cd "$APP/game"
if [[ "$FULLSCREEN" == "1" ]]; then
  [[ -n "$INICIAR" ]] && exec "$WINE" "$INICIAR" "$OTVDM" "E:\\game\\$EXE"
  exec "$WINE" "$OTVDM" "E:\\game\\$EXE"
fi
[[ -n "$INICIAR" ]] && exec "$WINE" explorer "/desktop=Elifoot98,$RES" "$INICIAR" "$OTVDM" "E:\\game\\$EXE"
exec "$WINE" explorer "/desktop=Elifoot98,$RES" "$OTVDM" "E:\\game\\$EXE"
