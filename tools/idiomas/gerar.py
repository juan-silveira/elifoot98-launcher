"""Gera os EXEs traduzidos de todos os idiomas de game/IDIOMAS: para cada
game/ELIFnn.EXE e cada game/IDIOMAS/xx-YY.txt, o game/ELIFnnYY.EXE (pt-BR.txt ->
ELIF08BR.EXE a ELIF20BR.EXE). O launcher abre o do idioma escolhido nas
Configuracoes (LauncherConfig.Idioma).

uso: python3 tools/idiomas/gerar.py   (depois de tools/divisoes/patch_divisoes.py)
"""
import glob
import os
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from traduzir import ler_idioma, traduzir, gravar     # noqa: E402

JOGO = os.path.join(AQUI, '..', '..', 'game')

for idioma in sorted(glob.glob(os.path.join(JOGO, 'IDIOMAS', '*.txt'))):
    sufixo = os.path.basename(idioma)[:-4].split('-')[-1].upper()
    mapa = ler_idioma(idioma)
    for n in range(8, 21, 2):
        entrada = os.path.join(JOGO, 'ELIF%02d.EXE' % n)
        b = bytearray(open(entrada, 'rb').read())
        falhas, faltam = traduzir(b, mapa)
        gravar(b, os.path.join(JOGO, 'ELIF%02d%s.EXE' % (n, sufixo)))
        print('ELIF%02d%s.EXE: %d nao couberam, %d nao achados' % (n, sufixo, len(falhas), len(faltam)))
        for f in falhas:
            print('  NAO COUBE:', f)
