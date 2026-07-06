#!/usr/bin/env bash
# Gera payload.zip com CRACK.EXE + vendor/dosbox/ pra embutir no
# ElifootRegistrador.exe portable. Rodar depois de fetch-deps.sh e antes
# do dotnet build.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

STAGE="$(mktemp -d)"
trap "rm -rf '$STAGE'" EXIT

# Copia payload pra staging
cp game/CRACK.EXE "$STAGE/CRACK.EXE"
mkdir -p "$STAGE/vendor/dosbox"
cp -r vendor/dosbox/. "$STAGE/vendor/dosbox/"

# Zipa preservando estrutura
OUT="src-registrador/payload.zip"
rm -f "$OUT"
(cd "$STAGE" && zip -qr - .) > "$OUT"
echo "==> gerado $OUT ($(du -h "$OUT" | cut -f1))"
