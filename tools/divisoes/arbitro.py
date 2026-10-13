"""Chances na janela "Ajuda do Arbitro" (TRefereeAidDlg).

A janela so mostra a balanca e uma barra por equipa (de 0 a 15), sem dizer o que
elas sao. A barra e a influencia V do arbitro em cada equipa (docs/elifoot98-interno.md,
"Arbitro"). O nome de cada equipa passa a mostrar a chance de penalti a favor no jogo
e quantos cartoes o arbitro deixa de dar a ela (arbitro.asm, no fim do seg25),
antes do ShowModal da rotina que preenche a janela (seg06:2963). O seg25 vai para o
fim do arquivo (NE.mover): nenhum segmento tinha espaco para o trecho. Os nomes ficam com
letra menor (-13) para caber.
"""
import struct
from paginas import _tabela, _gravar_tabela, ler_dfm, escrever_dfm, por, inteiro
from montar import montar
from ingressos import _registro, _novos

CLASSE = 'TRefereeAidDlg'
MODAL = 0x2963                          # seg06: les di,[bp+6]; push es; push di; call ShowModal
SEG = 25


def _dfm(b):
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    for c in form['filhos']:
        if c['nome'] in ('textHomeTeam', 'textAwayTeam'):
            assert dict(c['props'])['Font.Height'] == inteiro(-16)
            por(c, 'Font.Height', inteiro(-13))
    novo = b'TPF0' + escrever_dfm(form)
    assert len(novo) == fim - i
    b[i:fim] = novo


def arbitro(ne, b):
    s6 = ne.seg(6)[0]
    assert b[s6 + MODAL: s6 + MODAL + 10] == bytes.fromhex('c47e0606579affff0000')
    ne.mover(SEG)                       # nenhum segmento tem 330 bytes livres
    org = ne.seg(SEG)[1]
    cod, rot = montar('arbitro.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@texto'], 21, 0x1D8C], [3, 0, rot['..@modal'], 22, 0x5743]])
    # o call far do ShowModal passa a ser o de mostrar, 5 bytes antes
    p, recs, k = _registro(ne, 6, MODAL + 6)
    assert (recs[k][3], recs[k][4]) == (22, 0x5743), recs[k]
    recs[k][2], recs[k][3], recs[k][4] = MODAL + 1, SEG, rot['mostrar']
    _gravar_tabela(ne, p, recs)
    b[s6 + MODAL: s6 + MODAL + 10] = bytes.fromhex('9affff0000') + b'\x90' * 5
    _dfm(b)
