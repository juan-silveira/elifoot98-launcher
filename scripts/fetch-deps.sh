#!/usr/bin/env bash
# Baixa as dependências de terceiros que NÃO ficam no repo.
# Rodar antes de gerar o instalador.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VENDOR="$ROOT/vendor"
mkdir -p "$VENDOR"

# --- otvdm (winevdm) ---
OTVDM_VER="v0.9.0"
if [[ ! -f "$VENDOR/otvdm/otvdm.exe" ]]; then
  echo "==> baixando otvdm $OTVDM_VER"
  rm -rf "$VENDOR/otvdm"
  mkdir -p "$VENDOR/otvdm"
  curl -sSL -o /tmp/otvdm.zip \
    "https://github.com/otya128/winevdm/releases/download/$OTVDM_VER/otvdm-$OTVDM_VER.zip"
  # o zip vem com pasta de topo otvdm-vX.Y.Z/ — achatar
  TMP="$(mktemp -d)"
  unzip -qo /tmp/otvdm.zip -d "$TMP"
  # descobrir o subdir de topo
  SUB="$(find "$TMP" -maxdepth 1 -mindepth 1 -type d | head -1)"
  if [[ -n "$SUB" ]]; then
    mv "$SUB"/* "$VENDOR/otvdm/"
  else
    mv "$TMP"/* "$VENDOR/otvdm/"
  fi
  rm -rf "$TMP" /tmp/otvdm.zip
fi

# --- DOSBox-Staging (fork ativo do DOSBox, releases portable no GitHub) ---
DOSBOX_VER="v0.82.2"
if [[ ! -f "$VENDOR/dosbox/dosbox.exe" ]]; then
  echo "==> baixando DOSBox-Staging $DOSBOX_VER (Windows portable)"
  rm -rf "$VENDOR/dosbox"
  mkdir -p "$VENDOR/dosbox"
  curl -sSL -o /tmp/dosbox.zip \
    "https://github.com/dosbox-staging/dosbox-staging/releases/download/$DOSBOX_VER/dosbox-staging-windows-x64-$DOSBOX_VER.zip"
  TMP="$(mktemp -d)"
  unzip -qo /tmp/dosbox.zip -d "$TMP"
  SUB="$(find "$TMP" -maxdepth 1 -mindepth 1 -type d | head -1)"
  if [[ -n "$SUB" ]]; then
    mv "$SUB"/* "$VENDOR/dosbox/"
  else
    mv "$TMP"/* "$VENDOR/dosbox/"
  fi
  rm -rf "$TMP" /tmp/dosbox.zip
fi

# --- Visual C++ 2015-2022 Redistributable (x86) ---
# otvdm eh MSVC-x86, precisa de vcruntime140.dll. Windows 10/11 padrao ja tem,
# mas builds tipo Tiny10 removem essa dep. Bundlar o installer eh redistribuicao
# autorizada pela Microsoft.
if [[ ! -f "$VENDOR/vcredist/vc_redist.x86.exe" ]]; then
  echo "==> baixando vc_redist.x86 (VC++ 2015-2022 Redistributable)"
  mkdir -p "$VENDOR/vcredist"
  curl -sSL -o "$VENDOR/vcredist/vc_redist.x86.exe" \
    "https://aka.ms/vs/17/release/vc_redist.x86.exe"
fi

echo "==> pronto. vendor/ populado:"
ls "$VENDOR/otvdm/" | head -5
echo "..."
ls "$VENDOR/dosbox/" | head -5
echo "..."
ls -lh "$VENDOR/vcredist/"
