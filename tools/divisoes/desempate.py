"""Desempate da classificacao com vitorias (desempate.asm): pontos, vitorias, saldo e golos
marcados, como no Brasil (sem os cartoes). Troca o CmpClassificacao (seg11:1AB6-1BE3)
inteiro; as relocacoes de dentro dele (checagem de pilha, coprocessador, Sinal) saem."""
from montar import montar
from paginas import _tabela, _gravar_tabela

ORG, FIM = 0x1AB6, 0x1BE4


def desempatar(ne, b):
    cod, _ = montar('desempate.asm', ORG)
    assert len(cod) <= FIM - ORG, len(cod)
    base = ne.seg(11)[0]
    assert b[base + ORG: base + ORG + 3] == b'\x55\x89\xe5' and b[base + FIM - 3: base + FIM] == b'\xca\x08\x00'
    p, recs = _tabela(ne, 11)
    novos = [r for r in recs if not ORG <= r[2] < FIM]
    assert len(recs) - len(novos) == 13, len(recs) - len(novos)
    _gravar_tabela(ne, p, novos)
    b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * (8 * (len(recs) - len(novos)))
    b[base + ORG: base + ORG + len(cod)] = cod
    b[base + ORG + len(cod): base + FIM] = b'\x90' * (FIM - ORG - len(cod))
