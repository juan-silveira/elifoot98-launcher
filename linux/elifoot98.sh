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
#   ELIFOOT_EXE=ELIF20.EXE       EXE do jogo (equipes por divisao; padrao ELIFOOT.EXE)
#   ELIFOOT_ESCALA=150           tamanho do jogo em % (100 normal; 125 a 200: DPI alta
#                                no Wine, o jogo desenha as telas maiores e o texto
#                                nitido; auto = a maior que cabe no monitor principal)
#   ELIFOOT_JANELA_LIVRE=1       janela redimensionavel (gamescope, baixado na 1a vez;
#                                so Linux)
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
EXE="${ELIFOOT_EXE:-ELIFOOT.EXE}"
ESCALA="${ELIFOOT_ESCALA:-100}"
[[ "$MODO" == "editor" ]] && EXE="EDITEQ.EXE"

export WINEPREFIX="$APP/prefix" WINEDEBUG=-all WINEDLLOVERRIDES="mscoree,mshtml="

# Fontes instaladas na pasta do usuario (milhares de Google Fonts, por exemplo)
# estouram a lista de fontes do jogo: "EListError ... List index out of bounds"
# ao abrir. O Wine so ve as fontes do sistema
if [[ $MAC == 0 ]]; then
  mkdir -p "$APP"
  cat > "$APP/fontes.conf" <<FIM
<?xml version="1.0"?><!DOCTYPE fontconfig SYSTEM "urn:fontconfig:fonts.dtd">
<fontconfig>
  <include ignore_missing="yes">/etc/fonts/fonts.conf</include>
  <selectfont><rejectfont><glob>$HOME/*</glob><glob>/home/*</glob></rejectfont></selectfont>
</fontconfig>
FIM
  export FONTCONFIG_FILE="$APP/fontes.conf"
fi

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

sha256_ok() {  # $1 = arquivo, $2 = hash (padrao: o do Wine)
  local h="${2:-$RUNTIME_SHA256}"
  if command -v sha256sum >/dev/null; then echo "$h  $1" | sha256sum -c --quiet - >/dev/null 2>&1
  else echo "$h  $1" | shasum -a 256 -c - >/dev/null 2>&1; fi
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

# Janela redimensionavel (so Linux): o gamescope mostra o desktop do Wine numa
# janela que amplia ou reduz a imagem ao puxar a borda. Nao vem nas distros
# (Mint 22, Ubuntu 24.04): um AppImage portatil e baixado na 1a vez
GAMESCOPE_URL="https://github.com/pkgforge-dev/gamescope-AppImage/releases/download/3.16.31-1%402026-10-01_1790876541/gamescope-3.16.31-1-anylinux-x86_64.AppImage"
GAMESCOPE_SHA256="8da6aa8948bcbc86faad16c486dcbb7c5e3a0d563dd5b9a85bb6df8ffcdb7cd8"
GAMESCOPE_MB=41

preparar_gamescope() {
  local dir="$APP/gamescope" tmp
  [[ -x "$dir/AppRun" ]] && return 0
  tmp="$(mktemp -d)"
  if command -v zenity >/dev/null; then
    curl -fsSL -o "$tmp/gamescope.AppImage" "$GAMESCOPE_URL" | zenity --progress --pulsate --auto-close --no-cancel \
      --title="Elifoot 98" --text="Baixando o gamescope (janela redimensionável, ~$GAMESCOPE_MB MB, só desta vez)..." 2>/dev/null || true
  else
    aviso "Baixando o gamescope (~$GAMESCOPE_MB MB, só desta vez), aguarde..."
    curl -fsSL -o "$tmp/gamescope.AppImage" "$GAMESCOPE_URL" || true
  fi
  # Extraido: sem depender do FUSE pra rodar o AppImage
  if sha256_ok "$tmp/gamescope.AppImage" "$GAMESCOPE_SHA256" && chmod +x "$tmp/gamescope.AppImage" \
     && (cd "$tmp" && ./gamescope.AppImage --appimage-extract >/dev/null 2>&1) && [[ -x "$tmp/squashfs-root/AppRun" ]]; then
    # squashfs-root pode ser so um link pra pasta de verdade (AppDir)
    rm -rf "$dir"; mv "$(cd "$tmp/squashfs-root" && pwd -P)" "$dir"; rm -rf "$tmp"
    return 0
  fi
  rm -rf "$tmp"
  aviso "Não deu para baixar o gamescope; o jogo abre na janela normal."
  return 1
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
  cp -R "$SRC/linux/"* "$APP/linux/"
  chmod -R u+w "$APP"
  cp "$SRC/VERSAO" "$APP/.versao"
}

# O jogo deriva a senha do eli.cod das datas das pastas WINDOWS e
# WINDOWS/SYSTEM do otvdm. No Linux/macOS essas datas mudam quando o jogo
# grava arquivos, entao elas sao fixadas antes de cada execucao e o eli.cod
# e um feito para essa data (o registro, secondCode no elif98.ini, e gravado
# pelo "Ativar todos os recursos" do launcher).
# Dois arquivos de equipe com o mesmo nome a menos de maiusculas (ex.:
# PALMEIRA.EFT e PALMEIRA.eft) fazem o jogo parar de ler a pasta EQUIPAS ali:
# as equipes seguintes somem da selecao de paises. Fica so o mais recente.
equipas_sem_repetidas() {
  local d="$APP/game/EQUIPAS" f
  [[ -d "$d" ]] || return 0
  ls -t "$d" | awk '{ u = toupper($0); if (visto[u]++) print }' | while IFS= read -r f; do
    rm -f "$d/$f"
  done
}

preparar_arquivos() {
  equipas_sem_repetidas
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

# Alta definicao: o Delphi 1 desenha cada janela na escala da DPI (texto TrueType
# nitido, no lugar de ampliar a imagem de 640x480). A escala e a maior entre 2x,
# 1,75x, 1,5x e 1,25x em que o desktop virtual (RES x escala) cabe no monitor
# principal; maior que a tela, o Wine o poe em tela cheia. A MS Sans Serif do
# jogo e bitmap e para em 20 pontos: em DPI alta vai a TrueType com as mesmas
# larguras (linux/fontes-hd); em 96 DPI ela nao pode estar instalada (o Wine a
# preferiria ao bitmap e o jogo mudaria).
altura_tela() {
  local a=""
  if [[ $MAC == 1 ]]; then
    a="$(system_profiler SPDisplaysDataType 2>/dev/null | awk '/Resolution:/ {print $4; exit}')"
  elif command -v xrandr >/dev/null; then
    # o monitor principal (com 2 monitores a "tela" do X junta os dois)
    a="$(xrandr --current 2>/dev/null | awk '/ connected primary [0-9]/ {split($4, d, "[x+]"); print d[2]; exit}')"
    [[ -n "$a" ]] || a="$(xrandr --current 2>/dev/null | awk '/ connected [0-9]/ {split($3, d, "[x+]"); if (m == "" || d[2] < m) m = d[2]} END {print m}')"
  fi
  if [[ -z "$a" ]] && command -v xdpyinfo >/dev/null; then
    a="$(xdpyinfo 2>/dev/null | awk '/dimensions:/ {split($2, d, "x"); print d[2]; exit}')"
  fi
  echo "${a:-1080}"
}
dpi=96
if [[ "$ESCALA" == "auto" ]]; then
  tela="$(altura_tela)"
  h="${RES#*x}"
  margem=80; (( FULLSCREEN == 1 )) && margem=0   # janela: barra de titulo e painel
  for e in 200 175 150 125; do
    if (( h * e / 100 + margem <= tela )); then
      dpi=$(( 96 * e / 100 ))
      break
    fi
  done
elif [[ "$ESCALA" =~ ^(125|150|175|200)$ ]]; then
  dpi=$(( 96 * ESCALA / 100 ))
fi
# o desktop virtual do Wine na mesma escala (em pontos da tela)
RES="$(( ${RES%x*} * dpi / 96 ))x$(( ${RES#*x} * dpi / 96 ))"
FONTS="$WINEPREFIX/drive_c/windows/Fonts"
mkdir -p "$FONTS"
if (( dpi > 96 )); then
  cp -f "$APP/linux/fontes-hd/"*.ttf "$FONTS/" 2>/dev/null || true
else
  rm -f "$FONTS/MSSansSerifHD-"*.ttf
fi
# Letras suavizadas (tons de cinza; a ClearType deixa franjas coloridas no verde)
# na alta definicao: o otvdm forca a qualidade da fonte (ForceFontQuality) e o
# Wine liga a suavizacao
OTVDM_INI="$APP/vendor/otvdm/otvdm.ini"
if [[ -f "$OTVDM_INI" ]]; then
  grep -v '^ForceFontQuality=' "$OTVDM_INI" > "$OTVDM_INI.tmp" || true
  if (( dpi > 96 )); then
    awk '{print} /^\[otvdm\]/ {print "ForceFontQuality=4"}' "$OTVDM_INI.tmp" > "$OTVDM_INI"
  else
    cat "$OTVDM_INI.tmp" > "$OTVDM_INI"
  fi
  rm -f "$OTVDM_INI.tmp"
fi
# ...e esquece as que ja tinha registrado (o Wine as carrega do registro mesmo
# sem o fontconfig as ver); so com o wineserver parado
if [[ $MAC == 0 ]] && grep -qsF '="Z:\\home\\' "$WINEPREFIX/system.reg" "$WINEPREFIX/user.reg"; then
  "$(dirname "$WINE")/wineserver" -w >/dev/null 2>&1 || true
  for f in "$WINEPREFIX/system.reg" "$WINEPREFIX/user.reg"; do
    awk 'index($0, "=\"Z:\\\\home\\\\") && tolower($0) ~ /\.(ttf|ttc|otf|otc|pfb|pfa|fon)"$/ {next} {print}' "$f" > "$f.tmp" && mv "$f.tmp" "$f"
  done
fi
# O Wine so aceita no desktop virtual os tamanhos da lista dele (1024x768,
# 1280x960...) e o Desktops\Default; outro tamanho vira tela cheia
if [[ "$(cat "$APP/.dpi" 2>/dev/null)" != "$dpi $RES" ]]; then
  if (( dpi > 96 )); then suave=2; else suave=0; fi
  "$WINE" reg add 'HKCU\Control Panel\Desktop' /v FontSmoothing /d "$suave" /f >/dev/null 2>&1 || true
  "$WINE" reg add 'HKCU\Control Panel\Desktop' /v FontSmoothingType /t REG_DWORD /d 1 /f >/dev/null 2>&1 || true
  "$WINE" reg add 'HKCU\Control Panel\Desktop' /v LogPixels /t REG_DWORD /d "$dpi" /f >/dev/null 2>&1 || true
  "$WINE" reg add 'HKLM\System\CurrentControlSet\Hardware Profiles\Current\Software\Fonts' /v LogPixels /t REG_DWORD /d "$dpi" /f >/dev/null 2>&1 || true
  "$WINE" reg add 'HKCU\Software\Wine\Explorer\Desktops' /v Default /d "$RES" /f >/dev/null 2>&1 || true
  "$(dirname "$WINE")/wineserver" -w >/dev/null 2>&1 || true   # grava o registro antes do jogo
  echo "$dpi $RES" > "$APP/.dpi"
fi

# O otvdm entrega a data em UTC: o instante fixo e 2000-01-01 14:00 UTC (o
# linux/eli.cod foi gerado pra ele), seja qual for o fuso do computador
TZ=UTC touch -t 200001011400.00 "$WIN" "$WIN/SYSTEM"

# iniciar.exe (linux/iniciar.c) abre o otvdm e manda um movimento de mouse
# pro jogo: sem isso, no Wine, a janela "Acerca" da abertura nao aparece
OTVDM='E:\vendor\otvdm\otvdmw.exe'
INICIAR=""
[[ -f "$APP/linux/iniciar.exe" ]] && INICIAR='E:\linux\iniciar.exe'

cd "$APP/game"
[[ -f "$EXE" ]] || EXE="ELIFOOT.EXE"
if [[ "$FULLSCREEN" == "1" ]]; then
  [[ -n "$INICIAR" ]] && exec "$WINE" "$INICIAR" "$OTVDM" "E:\\game\\$EXE"
  exec "$WINE" "$OTVDM" "E:\\game\\$EXE"
fi
JOGO=("$WINE" explorer "/desktop=Elifoot98,$RES")
[[ -n "$INICIAR" ]] && JOGO+=("$INICIAR")
JOGO+=("$OTVDM" "E:\\game\\$EXE")
if [[ $MAC == 0 && "${ELIFOOT_JANELA_LIVRE:-0}" == "1" ]] && preparar_gamescope; then
  # O jogo desenha no tamanho escolhido (nitido) e o gamescope escala a imagem
  # pro tamanho da janela, suavizada
  inicio=$SECONDS
  "$APP/gamescope/AppRun" -w "${RES%x*}" -h "${RES#*x}" -W "${RES%x*}" -H "${RES#*x}" -S fit -F linear -- "${JOGO[@]}" && exit 0
  (( SECONDS - inicio > 15 )) && exit 0
  # Sem Vulkan com apresentacao (placa de video sem suporte, maquina virtual...)
  # o gamescope nao abre: vai sem ele
  "$(dirname "$WINE")/wineserver" -k >/dev/null 2>&1 || true
  aviso "A janela redimensionável não funcionou neste computador; o jogo abre na janela normal."
fi
exec "${JOGO[@]}"
