"""Subida e descida com K equipas por divisao (subida.asm em seg05:0987)."""
from montar import montar
from paginas import _tabela, _gravar_tabela

ORG, FIM = 0x0987, 0x0A84


def subir(ne, b, n, k):
    cod, rot = montar('subida.asm', ORG, N=n, K=k)
    rnd = rot.get('..@rnd')
    assert rnd and len(cod) <= FIM - ORG
    base = ne.seg(5)[0]
    assert b[base + ORG: base + ORG + 3] == bytes.fromhex('ff76fe')
    # relocacoes: as 2 chamadas a Random do sorteio original viram 1, no laco novo
    p, recs = _tabela(ne, 5)
    velhas = [r for r in recs if ORG <= r[2] < FIM]
    assert [r[2:] for r in velhas] == [[0x0A1D, 25, 0x212D], [0x0A36, 25, 0x212D]], velhas
    novos = []
    for r in recs:
        if r is velhas[0]:
            novos.append(r[:2] + [rnd] + r[3:])
        elif r is not velhas[1]:
            novos.append(r)
    _gravar_tabela(ne, p, novos)
    b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * 8
    b[base + ORG: base + FIM] = cod + b'\x90' * (FIM - ORG - len(cod))
