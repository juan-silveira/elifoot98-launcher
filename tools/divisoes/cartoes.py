"""Cartoes amarelos (cartoes.asm). Usado por patch_divisoes.py em todos os EXEs.

- seg03:aa40  sorteio de cada minuto: um para cada lado; < VERM vermelho, < LIMITE amarelo
- seg03:48be  cartao(equipa, var jogador), no lugar da expulsao
- seg03:c1f5  o lance sai com tipo 3 quando o jogador continua em campo
- seg03:ab18  amarelo: sem substituicao e sem recalcular a forca
- seg03:572d  fim de cada jogo: 3 amarelos acumulados dao 1 jogo de suspensao
- seg03:c3fa  desenho na jornada: amarelo (tipo 3) ou vermelho (tipo 4); no vermelho
  do 2o amarelo (lance +0Dh, byte novo), o amarelo atras dele
- seg03:c252  tabela de desenhos: o tipo 3 usa o desenho da expulsao
- seg03:7b5a  coluna S da lista: "S", "A" (amarelos acumulados) ou vazio
- seg04:2614  aba Jogador: "Suspenso por N jogos" ou "N amarelos acumulados"
- telas com as expulsoes e as lesoes: so os 2 bytes de baixo (os de cima sao
  os amarelos: +0x25/+0x26 acumulados e marca do jogo, +0x21 na carreira)
"""
import struct
from montar import montar
import opcoes
from paginas import _tabela, _gravar_tabela
from ingressos import _novos

# Random(1000) por minuto e por lado: < VERM vermelho direto, VERM..LIMITE-1 amarelo
# (o original: < 10 vermelho)
VERM, LIMITE = 5, 50

# (segmento, deslocamento): push word [es:di+alto] dos numeros mostrados nas telas
EXPULSOES = [(2, 0x331E), (4, 0x24EE), (10, 0x20AB)]
# (seg07:1E56 nao entra: ali o push [es:di+0x21]/[+0x1F] e o endereco da lista dos
# jogadores observados, que vai para o OrdenarTList (14:0e0d), nao as lesoes; com
# o segmento trocado por 0 a janela dos observados dava "General protection fault")
LESOES = [(2, 0x3347), (4, 0x2523), (10, 0x20D4)]

# Textos dos amarelos da aba Jogador (parte 8): no fim da regiao do Historial
# no seg04 (historial.py os grava la). O seg04 nao pode crescer: no Android o Wine
# derruba o jogo ao comecar a jornada se o seg04 tiver bytes a mais.
TEXTOS4 = 0x251B

# seg03: tamanho da instancia na VMT do lance (seg03:057F), 0x0D no original
TAMANHO_LANCE = 0x0569


def textos_amarelos():
    return montar('cartoes.asm', TEXTOS4, PARTE=8, ORG='0x%X' % TEXTOS4)


RANDOM, P196E, P19ED, P1988 = (25, 0x212D), (25, 0x196E), (25, 0x19ED), (25, 0x1988)
INTTOSTR, SETTEXT, SETVISIBLE = (24, 0x07D0), (21, 0x1D8C), (21, 0x1C77)


def _regiao(b, base, a, fim, cod, original_ini):
    assert b[base + a: base + a + len(original_ini)] == original_ini, hex(a)
    assert len(cod) <= fim - a, (hex(a), len(cod), fim - a)
    b[base + a: base + fim] = cod + b'\x90' * (fim - a - len(cod))


def cartoes(ne, b):
    base = ne.seg(3)[0]

    # --- relocacoes do seg03: cada regiao reescrita fica so com as que usa
    p, recs = _tabela(ne, 3)
    novas = {}      # roff antigo -> roff novo
    apagar = set()

    def regiao(a, fim, quer):
        """quer: lista de (alvo, roff novo), na ordem das relocacoes da regiao."""
        velhas = sorted((r for r in recs if a <= r[2] < fim), key=lambda r: r[2])
        usadas = set()
        for alvo, roff in quer:
            r = next(r for r in velhas if (r[3] & 0xFF, r[4]) == alvo and r[2] not in usadas)
            usadas.add(r[2])
            novas[r[2]] = roff
        apagar.update(r[2] for r in velhas if r[2] not in usadas)

    c1, r1 = montar('cartoes.asm', 0xAA40, PARTE=1, LIMITE=LIMITE, VERM=VERM)
    regiao(0xAA40, 0xAAAD, [(RANDOM, r1['..@rnd'])])
    c4, r4 = montar('cartoes.asm', 0x7B5A, PARTE=4)
    regiao(0x7B5A, 0x7BC8, [(P196E, r4['..@c1']), (P19ED, r4['..@c2']), (P19ED, r4['..@c3']), (P1988, r4['..@c4'])])
    # parte 2 precisa do endereco da parte 5 (fim do seg03 depois das relocacoes);
    # o tamanho do seg03 nao muda com elas
    org5 = ne.seg(3)[1]
    c2, r2 = montar('cartoes.asm', 0x48BE, PARTE=2, AMARELO='0x%X' % org5, VERM=VERM)
    regiao(0x48BE, 0x4940, [((3, 0x100B), r2['..@sj'])])
    regiao(0x572D, 0x5763, [])                # as 2 chamadas a Max (parte 9)
    c5, r5 = montar('cartoes.asm', org5, PARTE=5, ORG='0x%X' % org5,
                    VERMELHO='0x%X' % r2['vermelho'], SIM='0x%X' % r2['sim'],
                    NADA='0x%X' % r2['nada'], VERMELHO2='0x%X' % r2['vermelho2'])
    novos = []
    for r in recs:
        if r[2] in apagar:
            continue
        novos.append(r[:2] + [novas.get(r[2], r[2])] + r[3:])
    _gravar_tabela(ne, p, novos)
    b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * (8 * (len(recs) - len(novos)))

    _regiao(b, base, 0xAA40, 0xAAAD, c1, bytes.fromhex('68e803'))
    _regiao(b, base, 0x48BE, 0x4940, c2, bytes.fromhex('5589e5b80600'))
    c9, _ = montar('cartoes.asm', 0x572D, PARTE=9)
    _regiao(b, base, 0x572D, 0x5763, c9, bytes.fromhex('897ef48c46f66a006a00'))
    global _ATRAS
    c3, r3 = montar('cartoes.asm', 0xC3FA, PARTE=3)
    _ATRAS = r3['..@atras']
    _regiao(b, base, 0xC3FA, 0xC41A, c3, bytes.fromhex('8b46f0050300'))
    _regiao(b, base, 0x7B5A, 0x7BC8, c4, bytes.fromhex('c47e06'))
    assert ne.append(3, c5) == org5
    # o lance (VMT em seg03:057F) ganha o byte +0Dh: vermelho do 2o amarelo
    assert b[base + TAMANHO_LANCE: base + TAMANHO_LANCE + 2] == b'\x0d\x00'
    b[base + TAMANHO_LANCE] = 0x0E
    # opcao das Configuracoes: sem amarelos, os dois limites em 10 (o "Random(1000) < 10"
    # do original: so vermelho)
    opcoes.registrar('CARTOES', 3, r1['..@lim'] + 2, [LIMITE], [10])
    opcoes.registrar('CARTOES', 3, r2['..@verm'] + 4, [VERM], [10])

    # --- seg04: aba Jogador (parte 6), textos no lugar do antigo (parte 7) e no fim (parte 8)
    base4 = ne.seg(4)[0]
    c8, r8 = textos_amarelos()
    c6, r6 = montar('cartoes.asm', 0x2614, PARTE=6, JOGO='0x2266', AMS='0x%X' % r8['AMS'], AM1='0x%X' % r8['AM1'])
    p4, recs4 = _tabela(ne, 4)
    velhas = sorted((r for r in recs4 if 0x2614 <= r[2] < 0x26B8), key=lambda r: r[2])
    quer = [(P196E, '..@t1'), (INTTOSTR, '..@t2'), (P19ED, '..@t3'), (P19ED, '..@t4'), (SETTEXT, '..@t5'), (SETVISIBLE, '..@t6')]
    mapa, usadas = {}, set()
    for alvo, rot in quer:
        r = next(r for r in velhas if (r[3] & 0xFF, r[4]) == alvo and r[2] not in usadas)
        usadas.add(r[2])
        mapa[r[2]] = r6[rot]
    novos4 = [r[:2] + [mapa.get(r[2], r[2])] + r[3:] for r in recs4 if not (0x2614 <= r[2] < 0x26B8 and r[2] not in usadas)]
    _gravar_tabela(ne, p4, novos4)
    b[p4 + 2 + 8 * len(novos4): p4 + 2 + 8 * len(recs4)] = b'\0' * (8 * (len(recs4) - len(novos4)))
    _regiao(b, base4, 0x2614, 0x26B8, c6, bytes.fromhex('c4bef8fe'))
    c7, _ = montar('cartoes.asm', 0x2266, PARTE=7)
    assert b[base4 + 0x2266: base4 + 0x227A] == b'\x13Suspenso por 1 jogo'
    b[base4 + 0x2266: base4 + 0x2266 + len(c7)] = c7

    def chamada(onde, original, alvo):
        ne.patch(3, onde, original, b'\xe8' + struct.pack('<h', alvo - (onde + 3)))
    ne.patch(3, 0xC1F5, '8b4610268945' + '08', '9affff00009090')   # construtor do lance: TIPO (cartoes_seg25)
    # depois do lance, no aa11: jmp (e o pula volta por jmp), nada de call sem ret
    ne.patch(3, 0xAB18, 'c47e06', b'\xe9' + struct.pack('<h', r4['pula'] - (0xAB18 + 3)))
    ne.patch(3, 0xC252 + 2 * 2, struct.pack('<H', 0xC6C6), struct.pack('<H', 0xC3DA))   # desenho do tipo 3

    for s, off in EXPULSOES:
        ne.patch(s, off, '26ff7525', '6a009090')
    for s, off in LESOES:
        ne.patch(s, off, '26ff7521', '6a009090')


_ATRAS = None


def cartoes_seg25(ne, b):
    """Parte 10, no fim do seg25 (depois de o arbitro.py o levar para o fim do arquivo):
    TIPO (o construtor do lance, seg03:c1f5) e ATRAS (o amarelo atras do vermelho do 2o
    amarelo, chamado pela parte 3). As 2 relocacoes do seg03 sao os 16 bytes que ele tem."""
    org = ne.seg(25)[1]
    cod, rot = montar('cartoes.asm', org, PARTE=10, ORG='0x%X' % org)
    assert ne.append(25, cod) == org
    _novos(ne, 25, [[3, 0, rot['..@cor'], 19, 0x1656], [3, 0, rot['..@ret'], 19, 0x1CB7]])
    _novos(ne, 3, [[3, 0, 0xC1F6, 25, rot['TIPO']], [3, 0, _ATRAS, 25, rot['ATRAS']]])
