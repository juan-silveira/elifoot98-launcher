#!/usr/bin/env bash
# Baixa o instalador mais recente do GitHub Releases e copia pra
# /home/juan/share (pasta compartilhada com VM).
set -euo pipefail
REPO="juan-silveira/elifoot98-launcher"
DEST="${1:-/home/juan/share}"
mkdir -p "$DEST"

echo "==> checando último release..."
LATEST=$(gh release list --repo "$REPO" --limit 1 --json tagName,name --jq '.[0].tagName')
if [[ -z "$LATEST" ]]; then
  echo "!!! nenhum release encontrado"; exit 1
fi
echo "==> latest = $LATEST"

# Baixa .exe do launcher (padrão: elifoot98-launcher-setup-*.exe)
cd "$DEST"
gh release download "$LATEST" --repo "$REPO" --pattern "elifoot98-launcher-setup-*.exe" --clobber

# Se o Registrador local existir, atualiza também
LOCAL_REG="/home/juan/Desktop/Projects/elifoot98-launcher/src-registrador/bin/Release/net48/ElifootRegistrador.exe"
if [[ -f "$LOCAL_REG" ]]; then
  cp "$LOCAL_REG" "$DEST/elifoot98-registrador-portable.exe"
  echo "==> Registrador portable atualizado (build local)"
fi

echo "==> arquivos em $DEST:"
ls -lh "$DEST"/elifoot98*
