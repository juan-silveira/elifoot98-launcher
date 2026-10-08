#!/usr/bin/env bash
# Monta os pacotes Linux em dist/:
#   elifoot98_<versao>_amd64.deb          (instala em /opt/elifoot98 + atalho no menu)
#   Elifoot98-<versao>-x86_64.AppImage    (arquivo unico, so rodar)
# uso: scripts/build-linux.sh [versao]
# requer .NET SDK 8
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VER="${1:-0.0.0}"
NUMVER="${VER%%-*}"   # .NET/Info.plist so aceitam versao numerica
[[ "$NUMVER" =~ ^[0-9]+(\.[0-9]+)*$ ]] || NUMVER=0.0.0
DIST="$ROOT/dist"
TOOLS="$ROOT/vendor/tools"

rm -rf "$DIST" && mkdir -p "$DIST"

# otvdm de desenvolvimento (build 2703 do AppVeyor, 22/09/2026): traz o PR #1617
# (DestroyWindow16 so destroi o menu se a janela real nao tiver um). Com a v0.9.0
# o Wine destruia os submenus do jogo e so o menu "Jogador" abria. O Windows
# continua com a v0.9.0 (vendor/otvdm), onde isso nao acontece.
OTVDM="$DIST/otvdm"
unzip -q "$ROOT/linux/otvdm-master-2703.zip" -d "$DIST/otvdm-zip"
mv "$DIST/otvdm-zip"/otvdm-* "$OTVDM" && rm -rf "$DIST/otvdm-zip"

# Launcher nativo (Avalonia), executavel unico autocontido (nao precisa de .NET)
PUB="$DIST/publish"
dotnet publish "$ROOT/src-cross" -c Release -r linux-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:DebugType=none -p:Version="$NUMVER" -o "$PUB" >/dev/null

# Arquivos do jogo + launcher, comuns aos dois pacotes
copiar_app() {
  local dest="$1"
  mkdir -p "$dest/linux" "$dest/vendor"
  cp "$ROOT/linux/elifoot98.sh" "$PUB/ElifootLauncher" "$dest/"
  chmod +x "$dest/elifoot98.sh" "$dest/ElifootLauncher"
  cp "$ROOT/linux/eli.cod" "$ROOT/linux/elifoot98.png" "$dest/linux/"
  cp -r "$ROOT/game" "$dest/"
  rm -f "$dest/game/CRACK.EXE"
  cp -r "$OTVDM" "$dest/vendor/otvdm"
  echo "$VER" > "$dest/VERSAO"
}

desktop() {  # $1 = Exec do launcher, $2 = Exec do elifoot98.sh, $3 = Icon
  printf '%s\n' \
    "[Desktop Entry]" \
    "Type=Application" \
    "Name=Elifoot 98" \
    "Comment=Elifoot 98 Launcher" \
    "Exec=$1" \
    "Icon=$3" \
    "Categories=Game;SportsGame;" \
    "Terminal=false" \
    "Actions=Jogar;Editor;" \
    "" \
    "[Desktop Action Jogar]" \
    "Name=Jogar direto" \
    "Exec=$2 jogo" \
    "" \
    "[Desktop Action Editor]" \
    "Name=Editor de Equipes" \
    "Exec=$2 editor"
}

# --- .deb ---
DEB="$DIST/deb"
copiar_app "$DEB/opt/elifoot98"
mkdir -p "$DEB/usr/bin" "$DEB/usr/share/applications" "$DEB/usr/share/icons/hicolor/256x256/apps" "$DEB/DEBIAN"
ln -s /opt/elifoot98/ElifootLauncher "$DEB/usr/bin/elifoot98"
desktop elifoot98 /opt/elifoot98/elifoot98.sh elifoot98 > "$DEB/usr/share/applications/elifoot98.desktop"
cp "$ROOT/linux/elifoot98.png" "$DEB/usr/share/icons/hicolor/256x256/apps/"
printf '%s\n' \
  "Package: elifoot98" \
  "Version: $VER" \
  "Architecture: amd64" \
  "Maintainer: Juan Silveira <juansilveira@gmail.com>" \
  "Depends: curl, xz-utils" \
  "Recommends: zenity, libnotify-bin" \
  "Suggests: wine" \
  "Section: games" \
  "Priority: optional" \
  "Homepage: https://github.com/juan-silveira/elifoot98-launcher" \
  "Description: Elifoot 98 (1998) com launcher, rodando no Linux via Wine + otvdm" \
  " Jogo de gestao de futebol de Andre Elias. Usa o Wine do sistema se ele" \
  " rodar programas 32-bit; senao baixa um Wine portatil na primeira vez." \
  > "$DEB/DEBIAN/control"
dpkg-deb --root-owner-group --build "$DEB" "$DIST/elifoot98_${VER}_amd64.deb" >/dev/null
rm -rf "$DEB"

# --- AppImage ---
APPDIR="$DIST/Elifoot98.AppDir"
copiar_app "$APPDIR"
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'D="$(dirname "$(readlink -f "$0")")"' \
  '# AppImage jogo|editor -> direto no jogo; sem argumento -> launcher' \
  'case "${1:-}" in jogo|editor) exec "$D/elifoot98.sh" "$@" ;; esac' \
  'exec "$D/ElifootLauncher" "$@"' \
  > "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun"
desktop ElifootLauncher elifoot98.sh elifoot98 > "$APPDIR/elifoot98.desktop"
cp "$ROOT/linux/elifoot98.png" "$APPDIR/elifoot98.png"
mkdir -p "$TOOLS"
if [[ ! -x "$TOOLS/appimagetool" ]]; then
  curl -fsSL -o "$TOOLS/appimagetool" \
    "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
  chmod +x "$TOOLS/appimagetool"
fi
ARCH=x86_64 "$TOOLS/appimagetool" --appimage-extract-and-run --no-appstream \
  "$APPDIR" "$DIST/Elifoot98-${VER}-x86_64.AppImage" >/dev/null 2>&1
rm -rf "$APPDIR" "$PUB" "$OTVDM"

ls -lh "$DIST"
