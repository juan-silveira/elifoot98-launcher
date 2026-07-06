#!/usr/bin/env bash
# Baixa as dependências de terceiros que NÃO ficam no repo (grandes/licenças mistas).
# Rodar antes de gerar o instalador.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VENDOR="$ROOT/vendor"
mkdir -p "$VENDOR"

OTVDM_VER="v0.9.0"
if [[ ! -d "$VENDOR/otvdm" ]]; then
  echo "==> baixando otvdm $OTVDM_VER"
  mkdir -p "$VENDOR/otvdm"
  curl -sSL -o /tmp/otvdm.zip \
    "https://github.com/otya128/winevdm/releases/download/$OTVDM_VER/otvdm-$OTVDM_VER.zip"
  unzip -qo /tmp/otvdm.zip -d "$VENDOR/otvdm"
  rm /tmp/otvdm.zip
fi

DOSBOX_VER="0.74-3"
if [[ ! -d "$VENDOR/dosbox" ]]; then
  echo "==> baixando DOSBox $DOSBOX_VER (Windows portable)"
  mkdir -p "$VENDOR/dosbox"
  curl -sSL -o /tmp/dosbox.zip \
    "https://sourceforge.net/projects/dosbox/files/dosbox/$DOSBOX_VER/DOSBox$DOSBOX_VER-win32.zip/download"
  unzip -qo /tmp/dosbox.zip -d "$VENDOR/dosbox"
  rm /tmp/dosbox.zip
fi

echo "==> pronto. vendor/ populado."
ls "$VENDOR"
