"""Caixinha "Cartoes amarelos" na janela de detalhes da partida (lances.asm).
Usado por patch_divisoes.py, depois de cartoes() e historial().

No DFM do TsubstitutionDlg: o GroupBox1 (quadro do Arbitro, cujo campo o codigo
nao usa) muda de nome e o campo dele fica para a caixinha nova, que entra antes
das dos vermelhos e das lesoes. O clique usa o checkViewInjuriesMouseUp, que so
le a caixinha das lesoes e refaz a lista. Para caber no enchimento do recurso,
as caixinhas perdem as propriedades que repetem o padrao (cor do formulario,
letra preta).
"""
import struct
from montar import montar
from paginas import (_tabela, _gravar_tabela, ler_dfm, escrever_dfm, por,
                     inteiro, ident, texto, conjunto, FALSO)

INI, FIM = 0x2643, 0x26F2               # ramos dos tipos 4 e 5 da lista de lances
TABELA = 0x2495                         # saltos por tipo (1 a 6)
P196E, P19ED, INTTOSTR, P1988 = (25, 0x196E), (25, 0x19ED), (24, 0x07D0), (25, 0x1988)
GETCHECKED = (18, 0x6856)


def _dfm(b):
    i = b.find(b'TPF0\x10TsubstitutionDlg')
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    livre = fim
    while b[livre] == 0 and (livre & 0xFF):
        livre += 1
    f = {c['nome']: c for c in form['filhos']}
    f['GroupBox1']['nome'] = 'Arbitro'
    for nome in ('checkViewRedCards', 'checkViewInjuries'):
        f[nome]['props'] = [pv for pv in f[nome]['props'] if pv[0] not in ('Color', 'ParentColor', 'Font.Color')]
    por(f['checkViewRedCards'], 'Left', inteiro(136))
    por(f['checkViewInjuries'], 'Left', inteiro(264))
    nova = dict(cls='TCheckBox', nome='GroupBox1', filhos=[], props=[
        ['Left', inteiro(16)], ['Top', inteiro(144)], ['Width', inteiro(113)], ['Height', inteiro(17)],
        ['Caption', texto('Cart\xf5es amarelos')],
        ['Font.Height', inteiro(-11)], ['Font.Name', texto('MS Sans Serif')], ['Font.Style', conjunto()],
        ['ParentFont', FALSO], ['OnMouseUp', ident('checkViewInjuriesMouseUp')]])
    k = form['filhos'].index(f['checkViewRedCards'])
    form['filhos'].insert(k, nova)
    novo = b'TPF0' + escrever_dfm(form)
    assert i + len(novo) <= livre, (len(novo), livre - i)
    b[i:livre] = novo + b'\0' * (livre - i - len(novo))


def lances(ne, b):
    cod, rot = montar('lances.asm', INI)
    assert len(cod) <= FIM - INI, len(cod)
    base = ne.seg(2)[0]
    assert struct.unpack_from('<6H', b, base + TABELA) == (0x2576, 0x2576, 0x2740, 0x2643, 0x269B, 0x26F2)

    p, recs = _tabela(ne, 2)
    velhas = sorted((r for r in recs if INI <= r[2] < FIM), key=lambda r: r[2])
    assert [(r[3] & 0xFF, r[4]) for r in velhas] == [P196E, P19ED, INTTOSTR, P19ED, P19ED, P1988] * 2
    quer = [P196E, P19ED, INTTOSTR, P19ED, P19ED, P1988, GETCHECKED]
    novos_alvos = {}
    for k, alvo in enumerate(quer):
        r = velhas[k]
        novos_alvos[r[2]] = (rot['..@c%d' % (k + 1)], alvo)
    novos = []
    for r in recs:
        if r[2] in novos_alvos:
            roff, (sg, off) = novos_alvos[r[2]]
            novos.append([r[0], r[1], roff, (r[3] & 0xFF00) | sg, off])
        elif INI <= r[2] < FIM:
            continue
        else:
            novos.append(r)
    _gravar_tabela(ne, p, novos)
    b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * (8 * (len(recs) - len(novos)))
    b[base + INI: base + FIM] = cod + b'\x90' * (FIM - INI - len(cod))
    struct.pack_into('<3H', b, base + TABELA + 4, rot['t3'], rot['t4'], rot['t5'])

    _dfm(b)
