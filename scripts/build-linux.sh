#!/usr/bin/env bash
# Monta o pacote Linux: dist/elifoot98-linux-<versao>.tar.gz
# uso: scripts/build-linux.sh [versao]   (requer vendor/otvdm: scripts/fetch-deps.sh)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VER="${1:-dev}"
NAME="elifoot98-linux-$VER"
OUT="$ROOT/dist/$NAME"

[[ -f "$ROOT/vendor/otvdm/otvdmw.exe" ]] || { echo "falta vendor/otvdm (rode scripts/fetch-deps.sh)"; exit 1; }

rm -rf "$OUT" && mkdir -p "$OUT/linux" "$OUT/vendor"
cp "$ROOT/linux/elifoot98.sh" "$ROOT/linux/instalar.sh" "$ROOT/linux/desinstalar.sh" "$ROOT/linux/LEIA-ME.txt" "$OUT/"
cp "$ROOT/linux/eli.cod" "$ROOT/linux/elifoot98.png" "$OUT/linux/"
chmod +x "$OUT"/*.sh
cp -r "$ROOT/game" "$OUT/"
rm -f "$OUT/game/CRACK.EXE"
cp -r "$ROOT/vendor/otvdm" "$OUT/vendor/"

tar czf "$ROOT/dist/$NAME.tar.gz" -C "$ROOT/dist" "$NAME"
echo "==> dist/$NAME.tar.gz ($(du -h "$ROOT/dist/$NAME.tar.gz" | cut -f1))"
