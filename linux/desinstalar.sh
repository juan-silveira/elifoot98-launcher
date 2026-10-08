#!/usr/bin/env bash
# Remove o Elifoot 98 instalado pelo instalar.sh. Os saves vao para ~/Elifoot98-saves.
set -euo pipefail

DEST="${XDG_DATA_HOME:-$HOME/.local/share}/elifoot98"
APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"

if [[ -d "$DEST/game/JOGOS" ]] && ls "$DEST/game/JOGOS"/*.e98 >/dev/null 2>&1; then
  mkdir -p "$HOME/Elifoot98-saves"
  cp -n "$DEST/game/JOGOS"/*.e98 "$HOME/Elifoot98-saves/"
  echo "==> Saves copiados para ~/Elifoot98-saves"
fi
rm -f "$APPS/elifoot98.desktop" "$APPS/elifoot98-editor.desktop" "$HOME/.local/bin/elifoot98"
rm -rf "$DEST"
echo "==> Elifoot 98 removido."
