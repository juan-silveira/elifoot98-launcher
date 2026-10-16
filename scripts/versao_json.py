#!/usr/bin/env python3
"""Atualiza o versao.json do site (elifoot98-retro/public) para uma release publicada.

Os launchers leem esse arquivo para oferecer a versao nova. links: o arquivo de cada
plataforma (o "Atualizar" baixa e instala esse); sha256: o de cada arquivo, que o
launcher confere depois de baixar. Baixa os arquivos da release com o gh para calcular.

uso: scripts/versao_json.py VERSAO "novidades" [caminho do versao.json]
"""
import hashlib
import json
import os
import subprocess
import sys
import tempfile

REPO = 'juan-silveira/elifoot98-launcher'
SITE = os.path.expanduser('~/Desktop/Projects/elifoot98-retro/public/versao.json')
ARQUIVOS = {
    'windows': 'elifoot98-launcher-setup-{v}.zip',
    'windows-instalador': 'elifoot98-launcher-setup-{v}.exe',
    'windows-portatil': 'Elifoot98-{v}-Windows-portatil.exe',
    'linux-deb': 'elifoot98_{v}_amd64.deb',
    'linux-appimage': 'Elifoot98-{v}-x86_64.AppImage',
    'macos': 'Elifoot98-{v}-macOS.zip',
    'android': 'Elifoot98-{v}-Android.apk',
}


def main():
    v, novidades = sys.argv[1], sys.argv[2]
    destino = sys.argv[3] if len(sys.argv) > 3 else SITE
    base = 'https://github.com/%s/releases/download/v%s/' % (REPO, v)
    links, hashes = {}, {}
    with tempfile.TemporaryDirectory() as t:
        subprocess.run(['gh', 'release', 'download', 'v' + v, '-R', REPO, '-D', t], check=True)
        for chave, modelo in ARQUIVOS.items():
            nome = modelo.format(v=v)
            caminho = os.path.join(t, nome)
            if not os.path.isfile(caminho):
                sys.exit('falta na release: ' + nome)
            links[chave] = base + nome
            with open(caminho, 'rb') as f:
                hashes[chave] = hashlib.sha256(f.read()).hexdigest()
    d = json.load(open(destino)) if os.path.isfile(destino) else {}
    d.update(versao=v, novidades=novidades, links=links, sha256=hashes)
    d.setdefault('pagina', 'https://elifoot98.web.app/launcher')
    with open(destino, 'w') as f:
        json.dump(d, f, ensure_ascii=False, indent=1)
        f.write('\n')
    print('ok:', destino)


if __name__ == '__main__':
    main()
