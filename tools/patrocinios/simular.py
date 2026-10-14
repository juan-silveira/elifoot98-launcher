"""Simula a distribuicao de patrocinios do inicio da 1a temporada (a rotina distribuir
de tools/divisoes/patrocinio.asm) num jogo novo com todas as equipes de game/EQUIPAS,
e mostra, por divisao e por pais, a chance de um time do computador ter patrocinador
master e o valor medio do contrato.

uso: simular.py [MARCAS.TXT] [-n EQUIPES_POR_DIVISAO] [-r RODADAS]

Regras reproduzidas (patrocinio.asm):
- as equipes entram por nivel (20 a 1, na ordem dos arquivos): as primeiras N na 1a
  divisao, as N seguintes na 2a, ... e o resto no Distrital;
- cada marca, na ordem do arquivo, tem verba x inflacao/2 menos a reserva (%) e atua
  da divisao (maxima - 1) ate a maxima; a quota na divisao d (0 = 1a) e
  cota x inflacao/2 (arredondada no milhar) x (5 - d) / 2;
- percorre os times da divisao: sem verba para a quota, a marca para; modo exclusivo
  so aceita os paises da lista, exceto aceita todos menos eles; time sem patrocinador
  assina com 60% de chance (o humano sempre recebe a proposta: nao entra aqui).
"""
import argparse
import collections
import os
import random
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
from gerar_dat import ler  # noqa: E402

RAIZ = os.path.dirname(os.path.dirname(AQUI))
INFLACAO = 2.0                       # o jogo comeca com 2,0
NOMES = ['1a', '2a', '3a', '4a', 'Distrital']


def _texto(b, o):
    n = b[o]
    ant = n
    p = bytearray()
    for i in range(n):
        p.append((b[o + 1 + i] - ant) & 255)
        ant = b[o + 1 + i]
    return p.decode('latin1'), o + 1 + n


def equipes(pasta):
    times = []
    for f in sorted(os.listdir(pasta)):
        if not f.upper().endswith('.EFT'):
            continue
        b = open(os.path.join(pasta, f), 'rb').read()
        o = 0x32
        _, o = _texto(b, o)
        abrev, o = _texto(b, o)
        o += 8
        pais, o = _texto(b, o)
        times.append((abrev, pais, b[o]))
    return [t for n in range(20, 0, -1) for t in times if t[2] == n]


def pais_ok(m, pais):
    if m['modo'] == 1:
        return pais in m['paises']
    if m['modo'] == 3:
        return pais not in m['paises']
    return True


def distribuir(marcas, divs):
    tem = [[None] * len(d) for d in divs]
    for m in marcas:
        disp = int(m['verba'] * INFLACAO / 2)
        disp -= disp * m['reserva'] // 100
        cota = round(m['cota'] * INFLACAO / 2 / 1000) * 1000
        acabou = False
        for dv in range(max(0, m['divmax'] - 2), m['divmax']):
            if acabou:
                break
            quota = cota * (5 - dv) // 2
            for k, t in enumerate(divs[dv]):
                if disp < quota:
                    acabou = True
                    break
                if not pais_ok(m, t[1]):
                    continue
                if tem[dv][k] is None and random.randrange(100) < 60:
                    tem[dv][k] = (m['nome'], quota)
                    disp -= quota
    return tem


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('txt', nargs='?', default=os.path.join(RAIZ, 'game', 'PATROCIN', 'MARCAS.TXT'))
    ap.add_argument('-n', type=int, default=20, help='equipes por divisao (8 a 20)')
    ap.add_argument('-r', type=int, default=2000, help='rodadas da simulacao')
    a = ap.parse_args()
    marcas = ler(a.txt)
    ordem = equipes(os.path.join(RAIZ, 'game', 'EQUIPAS'))
    n = a.n
    divs = [ordem[i * n:(i + 1) * n] for i in range(4)] + [ordem[4 * n:]]
    tot, cont, valor = collections.Counter(), collections.Counter(), collections.Counter()
    pago = 0
    for _ in range(a.r):
        tem = distribuir(marcas, divs)
        for dv, d in enumerate(divs):
            for k, t in enumerate(d):
                tot[dv, t[1]] += 1
                if tem[dv][k]:
                    cont[dv, t[1]] += 1
                    valor[dv, t[1]] += tem[dv][k][1]
                    pago += tem[dv][k][1]
    print('%d marcas, %d equipes por divisao, %d rodadas' % (len(marcas), n, a.r))
    for dv, d in enumerate(divs):
        paises = sorted({t[1] for t in d}, key=lambda p: -sum(1 for t in d if t[1] == p))
        geral = sum(cont[dv, p] for p in paises) / max(1, sum(tot[dv, p] for p in paises))
        print('%s (%d times): %.0f%% com patrocinador' % (NOMES[dv], len(d), 100 * geral))
        for p in paises:
            c = (dv, p)
            media = valor[c] // max(1, cont[c]) // 1000
            print('   %s %3d times  %5.1f%%  media %5d mil' % (p, tot[c] // a.r, 100 * cont[c] / tot[c], media))
    print('total pago por temporada (media): %.1f milhoes' % (pago / a.r / 1e6))


if __name__ == '__main__':
    main()
