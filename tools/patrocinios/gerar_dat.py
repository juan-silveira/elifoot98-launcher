"""Converte PATROCIN/MARCAS.TXT (editavel a mao) em PATROCIN/MARCAS.DAT, a tabela
binaria que o jogo le (com as rotinas de arquivo do Delphi, sem interpretar texto).

uso: gerar_dat.py [MARCAS.TXT] [MARCAS.DAT]

MARCAS.TXT (Latin-1, uma marca por linha, # = comentario):
  nome;logo;tier;verba_anual;cota_minima;divisao_maxima;reserva_pontual_pct;paises;modo
  - nome: ate 24 letras; logo: arquivo 8.3 na pasta PATROCIN (ex.: PETROBRA.BMP)
  - tier: 1 (gigantes) a 5 (comercio local)
  - verba_anual, cota_minima: em reais do comeco do jogo (o jogo multiplica pela
    inflacao, como faz com salarios e premios)
  - divisao_maxima: 1 a 4, ou 5 = aceita o Distrital
  - reserva_pontual_pct: parte da verba guardada para patrocinios de um jogo (Copa)
  - paises: codigos de 3 letras separados por virgula (BRA,POR), ou * = qualquer
  - modo: exclusivo (so esses paises), preferido (aceita qualquer um; so informativo)
    ou exceto (todos menos esses: casas de apostas, proibidas no Brasil); vazio com *

MARCAS.DAT: cabecalho de 8 bytes ("PAT1", versao word = 1, quantidade word) e um
registro de 80 bytes por marca, na ordem do arquivo (o indice da marca, 1..N, e o
que vai no save):
   0  string[24]  nome (1 byte de tamanho + 24)
  25  string[12]  logo (1 + 12)
  38  byte        tier
  39  byte        divisao maxima (1..5)
  40  longint     verba anual (reais)
  44  longint     cota minima (reais)
  48  byte        reserva pontual (%)
  49  byte        modo: 0 qualquer pais, 1 exclusivo, 2 preferido, 3 exceto
  50  byte        quantidade de paises (0..8)
  51  8 x 3 bytes codigos dos paises
  75  5 bytes     zero (reserva)
"""
import struct
import sys

REG = 80
MODOS = {'': 0, 'exclusivo': 1, 'preferido': 2, 'exceto': 3}


def _pstr(s, n):
    b = s.encode('latin1')[:n]
    return bytes([len(b)]) + b + b'\0' * (n - len(b))


def ler(caminho):
    marcas = []
    for n, linha in enumerate(open(caminho, encoding='latin1'), 1):
        linha = linha.strip()
        if not linha or linha.startswith('#'):
            continue
        c = [x.strip() for x in linha.split(';')]
        c += [''] * (9 - len(c))
        nome, logo, tier, verba, cota, divmax, reserva, paises, modo = c[:9]
        paises = [] if paises in ('', '*') else [p.strip().upper() for p in paises.split(',') if p.strip()]
        modo = modo.lower() if paises else ''
        if modo not in MODOS:
            sys.exit('%s:%d: modo "%s" (use exclusivo, preferido ou exceto)' % (caminho, n, modo))
        if paises and not modo:
            modo = 'exclusivo'
        if len(paises) > 8 or any(len(p) != 3 for p in paises):
            sys.exit('%s:%d: paises: ate 8 codigos de 3 letras' % (caminho, n))
        marcas.append(dict(nome=nome.upper(), logo=logo.upper(), tier=int(tier), verba=int(verba),
                           cota=int(cota), divmax=int(divmax), reserva=int(reserva or 0),
                           paises=paises, modo=MODOS[modo]))
    return marcas


def gerar(marcas):
    out = b'PAT1' + struct.pack('<HH', 1, len(marcas))
    for m in marcas:
        r = (_pstr(m['nome'], 24) + _pstr(m['logo'], 12)
             + struct.pack('<BBiiBBB', m['tier'], m['divmax'], m['verba'], m['cota'],
                           m['reserva'], m['modo'], len(m['paises']))
             + b''.join(p.encode('latin1') for p in m['paises']).ljust(24, b'\0'))
        out += r.ljust(REG, b'\0')
    return out


if __name__ == '__main__':
    txt = sys.argv[1] if len(sys.argv) > 1 else 'PATROCIN/MARCAS.TXT'
    dat = sys.argv[2] if len(sys.argv) > 2 else txt[:-4] + '.DAT'
    m = ler(txt)
    open(dat, 'wb').write(gerar(m))
    print('%d marcas -> %s' % (len(m), dat))
