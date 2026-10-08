#!/usr/bin/env bash
# Elifoot 98 no Linux: roda o jogo (ou o editor de equipes) no Wine via otvdm.
# uso: elifoot98.sh [editor]
#
# Variaveis opcionais:
#   ELIFOOT_RES=1024x768   tamanho da janela do jogo (padrao 800x600)
#   ELIFOOT_WINE=/caminho/wine   forca um Wine especifico
set -euo pipefail

APP="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
WIN="$APP/vendor/otvdm/WINDOWS"
RUNTIME="$APP/wine-runtime"
RUNTIME_URL="https://github.com/Kron4ek/Wine-Builds/releases/download/11.19/wine-11.19-amd64-wow64.tar.xz"
RUNTIME_SHA256="a1fe858b45d12c9b16b91c7601d9b934f68430f7b6b95e240212ab6e2a198d8a"
# Contra-senha de "Registro para autor 2" para o linux/eli.cod do pacote
SECOND_CODE="959-240-641-242-102"
RES="${ELIFOOT_RES:-800x600}"
EXE="ELIFOOT.EXE"
[[ "${1:-}" == "editor" ]] && EXE="EDITEQ.EXE"

export WINEPREFIX="$APP/prefix" WINEDEBUG=-all WINEDLLOVERRIDES="mscoree,mshtml="

aviso() {
  if command -v notify-send >/dev/null; then notify-send -i "$APP/linux/elifoot98.png" "Elifoot 98" "$1" 2>/dev/null || true
  else echo "$1"; fi
}

erro() {
  if command -v zenity >/dev/null; then zenity --error --title="Elifoot 98" --text="$1" 2>/dev/null || true
  else echo "ERRO: $1" >&2; fi
  exit 1
}

confirmar() {
  if command -v zenity >/dev/null; then zenity --question --title="Elifoot 98" --text="$1" 2>/dev/null
  else read -r -p "$1 [s/N] " r; [[ "$r" =~ ^[sS] ]]; fi
}

baixar_wine() {
  confirmar "O Elifoot 98 precisa do Wine com suporte a programas 32-bit, que não foi encontrado.

Baixar um Wine portátil agora? (~95 MB, só desta vez)" || erro "Instale o Wine (com suporte a 32-bit) e tente de novo."
  local tmp; tmp="$(mktemp -d)"
  if command -v zenity >/dev/null; then
    curl -fsSL -o "$tmp/wine.tar.xz" "$RUNTIME_URL" | zenity --progress --pulsate --auto-close --no-cancel \
      --title="Elifoot 98" --text="Baixando o Wine..." 2>/dev/null || true
  else
    echo "Baixando o Wine..."; curl -fL -o "$tmp/wine.tar.xz" "$RUNTIME_URL"
  fi
  echo "$RUNTIME_SHA256  $tmp/wine.tar.xz" | sha256sum -c --quiet - || { rm -rf "$tmp"; erro "Download do Wine falhou ou veio corrompido."; }
  tar xJf "$tmp/wine.tar.xz" -C "$tmp"
  rm -rf "$RUNTIME"; mv "$tmp"/wine-*/ "$RUNTIME"; rm -rf "$tmp"
}

escolher_wine() {
  if [[ -n "${ELIFOOT_WINE:-}" ]]; then WINE="$ELIFOOT_WINE"; return; fi
  if [[ -x "$RUNTIME/bin/wine" ]]; then WINE="$RUNTIME/bin/wine"; return; fi
  # Debian/Ubuntu: wine sem o pacote wine32 nao roda o otvdm (32-bit)
  if command -v wine >/dev/null && ! wine --version 2>&1 | grep -q 'wine32 is missing'; then WINE=wine; return; fi
  baixar_wine
  WINE="$RUNTIME/bin/wine"
}

escolher_wine

if [[ ! -f "$WINEPREFIX/system.reg" ]]; then
  aviso "Preparando o Elifoot 98 (só na primeira vez, leva alguns segundos)..."
  "$WINE" wineboot -i >/dev/null 2>&1 || erro "O Wine não conseguiu iniciar."
fi
# E: = pasta do jogo (caminhos curtos pro programa 16-bit)
ln -sfn "$APP" "$WINEPREFIX/dosdevices/e:"

# Registro: o jogo deriva a senha do eli.cod das datas das pastas WINDOWS e
# WINDOWS/SYSTEM do otvdm. No Linux essas datas mudam quando o jogo grava
# arquivos, entao fixamos as duas e usamos um eli.cod feito para essa data.
mkdir -p "$WIN/SYSTEM"
cmp -s "$APP/linux/eli.cod" "$WIN/eli.cod" 2>/dev/null || cp "$APP/linux/eli.cod" "$WIN/eli.cod"
INI="$WIN/elif98.ini"
[[ -f "$INI" ]] || printf '[System]\r\n' > "$INI"
if grep -q '^secondCode=' "$INI"; then
  sed -i "s/^secondCode=.*/secondCode=$SECOND_CODE\r/" "$INI"
else
  sed -i "s/^\[System\]\r\?$/[System]\r\nsecondCode=$SECOND_CODE\r/" "$INI"
fi
touch -d '2000-01-01 12:00:00' "$WIN" "$WIN/SYSTEM"

cd "$APP/game"
exec "$WINE" explorer "/desktop=Elifoot98,$RES" 'E:\vendor\otvdm\otvdmw.exe' "E:\\game\\$EXE"
