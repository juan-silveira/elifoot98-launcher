#!/usr/bin/env bash
# Monta o pacote macOS: dist/Elifoot98-<versao>-macOS.zip com o Elifoot98.app
# (launcher Avalonia x86_64 — no Apple Silicon roda via Rosetta 2, que o Wine
# tambem precisa). Roda no Linux ou no macOS.
# uso: scripts/build-mac.sh [versao]   (requer .NET SDK 8, python3, zip)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VER="${1:-0.0.0}"
NUMVER="${VER%%-*}"   # .NET/Info.plist so aceitam versao numerica
[[ "$NUMVER" =~ ^[0-9]+(\.[0-9]+)*$ ]] || NUMVER=0.0.0
DIST="$ROOT/dist"
APP="$DIST/Elifoot98.app"
MACOS="$APP/Contents/MacOS"
mkdir -p "$DIST"
rm -rf "$APP" "$DIST/Elifoot98-${VER}-macOS.zip"
mkdir -p "$MACOS/linux" "$MACOS/vendor" "$APP/Contents/Resources"

# Launcher nativo, executavel unico autocontido (nao precisa de .NET)
PUB="$DIST/publish-mac"
dotnet publish "$ROOT/src-cross" -c Release -r osx-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial \
  -p:DebugType=none -p:Version="$NUMVER" -o "$PUB" >/dev/null
cp "$PUB/ElifootLauncher" "$MACOS/"
rm -rf "$PUB"

# Jogo + otvdm (mesmo do Linux: build 2703, com a correcao dos menus no Wine)
cp "$ROOT/linux/elifoot98.sh" "$MACOS/"
chmod +x "$MACOS/elifoot98.sh" "$MACOS/ElifootLauncher"
cp "$ROOT/linux/eli.cod" "$ROOT/linux/elifoot98.png" "$MACOS/linux/"
cp -R "$ROOT/game" "$MACOS/"
rm -f "$MACOS/game/CRACK.EXE"
# O APFS so aceita nomes UTF-8: EQUIPAS/ARA<0x80>A_BR.EFT (C cedilha do DOS,
# CP850) vira ARACA_BR.EFT. O jogo lista a pasta, nao depende do nome.
python3 - "$MACOS/game" <<'PY'
import os, sys, unicodedata
for raiz, pastas, arqs in os.walk(os.fsencode(sys.argv[1])):
    for nome in arqs:
        try:
            nome.decode('utf-8')
        except UnicodeDecodeError:
            novo = unicodedata.normalize('NFKD', nome.decode('cp850')).encode('ascii', 'ignore')
            os.rename(os.path.join(raiz, nome), os.path.join(raiz, novo))
            print('renomeado:', nome, '->', novo.decode())
PY
unzip -q "$ROOT/linux/otvdm-master-2703.zip" -d "$DIST/otvdm-zip"
mv "$DIST/otvdm-zip"/otvdm-* "$MACOS/vendor/otvdm" && rm -rf "$DIST/otvdm-zip"
echo "$VER" > "$MACOS/VERSAO"

# Icone .icns (PNGs embutidos: ic07 = 128px, ic08 = 256px)
python3 - "$ROOT/linux/elifoot98.png" "$APP/Contents/Resources/elifoot98.icns" <<'PY'
import struct, sys, subprocess, tempfile, os
src, dst = sys.argv[1], sys.argv[2]
entries = []
for tipo, px in ((b'ic07', 128), (b'ic08', 256)):
    data = open(src, 'rb').read()
    if px != 256:
        try:
            out = tempfile.mktemp(suffix='.png')
            subprocess.run(['convert', src, '-filter', 'point', '-resize', f'{px}x{px}', out], check=True)
            data = open(out, 'rb').read(); os.remove(out)
        except Exception:
            continue
    entries.append(tipo + struct.pack('>I', 8 + len(data)) + data)
body = b''.join(entries)
open(dst, 'wb').write(b'icns' + struct.pack('>I', 8 + len(body)) + body)
PY

cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Elifoot 98</string>
  <key>CFBundleDisplayName</key><string>Elifoot 98</string>
  <key>CFBundleIdentifier</key><string>io.github.juan-silveira.elifoot98</string>
  <key>CFBundleVersion</key><string>$NUMVER</string>
  <key>CFBundleShortVersionString</key><string>$NUMVER</string>
  <key>CFBundleExecutable</key><string>ElifootLauncher</string>
  <key>CFBundleIconFile</key><string>elifoot98</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>10.15</string>
  <key>LSArchitecturePriority</key><array><string>x86_64</string></array>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
EOF

# zip preserva os bits de execucao; -y mantem symlinks
(cd "$DIST" && zip -qry "Elifoot98-${VER}-macOS.zip" Elifoot98.app)
rm -rf "$APP"
ls -lh "$DIST/Elifoot98-${VER}-macOS.zip"
