#!/usr/bin/env bash
# Prepara tudo pra gerar o instalador. No Linux/Mac: só faz o build .NET + fetch deps.
# No Windows: faz o build .NET + fetch deps + chama iscc pra gerar o instalador.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> [1/3] fetch-deps.sh"
bash scripts/fetch-deps.sh

echo ""
echo "==> [2/3] dotnet build (Release, net48)"
# Preferir SDK do usuário em ~/.dotnet (tem WindowsDesktop workload)
# antes do dotnet do sistema (que no Ubuntu/Mint pode não ter).
if [[ -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi
DOTNET_BIN="$(command -v dotnet)"
echo "  usando: $DOTNET_BIN ($("$DOTNET_BIN" --version))"
"$DOTNET_BIN" build src/ElifootLauncher.csproj -c Release

echo ""
echo "==> [3/3] verificando estrutura pro instalador"
for p in src/bin/Release/net48/ElifootLauncher.exe vendor/otvdm/otvdm.exe vendor/dosbox/dosbox.exe game/ELIFOOT.EXE; do
  if [[ -e "$p" ]]; then
    echo "  ✓ $p"
  else
    echo "  ✗ $p (faltando)"
  fi
done

echo ""
echo "==> pronto pro Inno Setup:"
echo "    (no Windows) iscc installer/setup.iss"
echo "    saída: installer/Output/elifoot98-launcher-setup-0.1.0.exe"
