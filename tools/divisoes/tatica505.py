"""Tatica 5-0-5: o item do menu Seleccionar passa a ser desligado quando o elenco nao
tem 5 defesas e 5 avancados (como os outros 12), em vez de ficar sempre ligado.

- DFM do TprepareGameDlg: o item 5-0-5 recebe o nome N4 (o campo Self+0x304, que era
  de um separador do menu Jogador e nao e usado no codigo); o separador fica sem nome.
- seg04:2B7F, no fim da rotina que liga e desliga as taticas (seg04:29AB): o SetEnabled
  do 6-4-0 vira uma chamada a menu505 (tatica505.asm, no fim do seg25), que faz o do
  6-4-0 e o do 5-0-5. O seg04 nao cresce (no Android o jogo cai se crescer): o trecho
  novo cabe nos 20 bytes do antigo, e a relocacao do SetEnabled passa a ser a da chamada.
"""
import struct
from montar import montar
from paginas import ler_dfm, escrever_dfm, mover_dfm, _tabela, _gravar_tabela
from ingressos import _novos

SEG = 25
CLASSE = 'TprepareGameDlg'
SETENABLED = (20, 0x129B)      # TMenuItem.SetEnabled
PODEESCALAR = (3, 0x4B3E)      # a equipa consegue escalar G-D-M-A?


def _dfm(b):
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)

    def todos(c):
        yield c
        for f in c['filhos']:
            yield from todos(f)
    itens = {c['nome']: c for c in todos(form) if c['cls'] == 'TMenuItem'}
    assert 'MenuSelect505' in itens and 'N4' in itens
    itens['N4']['nome'] = ''
    itens['MenuSelect505']['nome'] = 'N4'
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))


def tatica505(ne, b):
    org = ne.seg(SEG)[1]
    cod, rot = montar('tatica505.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@setenabled'], *SETENABLED],
                     [3, 0, rot['..@podeescalar'], *PODEESCALAR]])
    # seg04:2B7F: push ax; push [bp+4]; push [bp-2]; push [bp-4]; call far menu505; 5 x nop
    velho = bytes.fromhex('508b7e0436c47d0626c4bda80106579affff0000')
    novo = bytes.fromhex('50ff7604ff76feff76fc9affff0000') + b'\x90' * 5
    base4 = ne.seg(4)[0]
    assert bytes(b[base4 + 0x2B7F: base4 + 0x2B7F + len(velho)]) == velho, 'seg04:2B7F diferente'
    b[base4 + 0x2B7F: base4 + 0x2B7F + len(novo)] = novo
    p, recs = _tabela(ne, 4)
    r = next(r for r in recs if r[2] == 0x2B8F)
    assert (r[3], r[4]) == SETENABLED
    r[2], r[3], r[4] = 0x2B8A, SEG, rot['menu505']
    _gravar_tabela(ne, p, recs)
    _dfm(b)
