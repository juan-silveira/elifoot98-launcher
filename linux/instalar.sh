#!/usr/bin/env bash
# Instala (ou atualiza) o Elifoot 98 em ~/.local/share/elifoot98 e cria os
# atalhos no menu. Saves (game/JOGOS) e configuracoes sao preservados.
set -euo pipefail

SRC="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
DEST="${XDG_DATA_HOME:-$HOME/.local/share}/elifoot98"
APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"

echo "==> Instalando em $DEST"
mkdir -p "$DEST/game/JOGOS" "$DEST/vendor"
cp -r "$SRC/linux" "$DEST/"
cp "$SRC/elifoot98.sh" "$SRC/desinstalar.sh" "$SRC/LEIA-ME.txt" "$DEST/"
cp -r "$SRC/vendor/otvdm" "$DEST/vendor/"
for f in "$SRC/game/"*; do
  [[ "$(basename "$f")" == "JOGOS" ]] && continue
  cp -r "$f" "$DEST/game/"
done
cp -rn "$SRC/game/JOGOS/." "$DEST/game/JOGOS/"   # nao sobrescreve saves
chmod +x "$DEST/elifoot98.sh" "$DEST/desinstalar.sh"

mkdir -p "$APPS" "$HOME/.local/bin"
cat > "$APPS/elifoot98.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Elifoot 98
Comment=Jogar Elifoot 98
Exec="$DEST/elifoot98.sh"
Icon=$DEST/linux/elifoot98.png
Categories=Game;SportsGame;
Terminal=false
EOF
cat > "$APPS/elifoot98-editor.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Elifoot 98 - Editor de Equipes
Exec="$DEST/elifoot98.sh" editor
Icon=$DEST/linux/elifoot98.png
Categories=Game;SportsGame;
Terminal=false
EOF
ln -sfn "$DEST/elifoot98.sh" "$HOME/.local/bin/elifoot98"
command -v update-desktop-database >/dev/null && update-desktop-database "$APPS" 2>/dev/null || true

echo "==> Pronto! Abra 'Elifoot 98' no menu de aplicativos (ou rode: elifoot98)."
