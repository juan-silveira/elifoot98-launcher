"""Botao Comprar na Lista de Jogadores (comprar.asm, no fim do seg25).

- DFM do TcheckPlayerDlg: um TBitBtn "Comprar" entre o "Equipa..." e o "Fechar" (os dois
  sobem/descem para dar lugar), com o icone do Comprar da janela da equipa, desligado,
  OnClick = teamBtnClick (o DFM so aceita metodos da tabela da janela) e Tag = TAGCOMPRA.
- seg10:37B4 (checagem de pilha do teamBtnClick da Lista): vira um salto para desvio, que
  manda o clique com esse Tag para comprar e o resto (Equipa..., duplo clique) de volta.
- fim do seg10: linha, ponte de longe para a rotina perto que acha o jogador de uma linha.
- preparar: pega o jogador da linha escolhida, prepara a janela da equipa escondida
  (TPlantelDlg: equipa, comprador, jogador) e calcula o preco com as rotinas do jogo.
- ativar: no lugar dos SetEnabled do Equipa... (Execute, comeco da busca, clique na
  lista); o Comprar so fica ligado com um jogador escolhido que tem preco.
- comprar: preparar, confere as condicoes, pede confirmacao na janela de Sim/Nao e chama
  o buyBtnClick; depois atualiza o dinheiro em caixa e refaz a busca da Lista.
- seg04:4249: quando a Lista fecha, a tela principal redesenha tambem as financas.
"""
import struct
from montar import montar
from paginas import ler_dfm, escrever_dfm, mover_dfm, inteiro, texto, ident, por, _tabela, _gravar_tabela
from ingressos import _novos

SEG = 25
CLASSE = 'TcheckPlayerDlg'
TAGCOMPRA = 0x434D
DESTINOS = {
    'volta': (10, 0x37B9),              # o resto do teamBtnClick original
    'acharequipa': (3, 0xD55C),         # equipa pelo id
    'preco': (10, 0x1939),              # TPlantelDlg: preco do jogador escolhido
    'podecomprar': (3, 0x633D),         # o comprador pode levar o jogador?
    'compraclick': (10, 0x1D66),        # TPlantelDlg.buyBtnClick
    'procurarclick': (10, 0x34F5),      # findBtnClick da Lista
    'extenso': (9, 0x335A),
    'settext': (21, 0x1D8C),
    'visivel': (21, 0x1C77),
    'showmodal': (22, 0x5743),
    'atualizar': (4, 0x2BD8),
    'itemindex': (18, 0x723F),          # TListBox.ItemIndex
    'setenabled': (21, 0x1CB8),           # TMainForm: redesenha uma parte da tela
}


# chamadas a SetEnabled do Equipa... (relocacao): Execute, comeco da busca, clique na lista
ATIVAR = (0x2FBA, 0x351E, 0x3864)
JOGADORDALINHA = 0x3041                 # seg10, perto: jogador do texto de uma linha


def _icone(b):
    i = b.find(b'TPF0\x0bTPlantelDlg')
    assert i > 0
    form, _ = ler_dfm(b, i + 4)
    bt = next(c for c in form['filhos'] if c['nome'] == 'buyBtn')
    return [[k, v] for k, v in bt['props'] if k.startswith('Glyph')]


def _dfm(b):
    glifo = _icone(b)
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    bt = {c['nome']: c for c in form['filhos'] if c['cls'] == 'TBitBtn'}
    team, ok, find = bt['teamBtn'], bt['OKBtn'], bt['findBtn']
    assert dict(find['props'])['Top'] == inteiro(184)
    por(team, 'Top', inteiro(252))
    por(ok, 'Top', inteiro(388))
    props = [[k, v] for k, v in team['props'] if not k.startswith('Glyph') and k not in ('Caption', 'Top', 'OnClick', 'TabOrder')]
    props += [['Top', inteiro(320)], ['Caption', texto('Comprar')], ['Enabled', b'\x08'],
              ['Tag', inteiro(TAGCOMPRA)], ['OnClick', ident('teamBtnClick')]] + glifo
    novo = dict(cls='TBitBtn', nome='', filhos=[], props=props)
    form['filhos'].insert(form['filhos'].index(team) + 1, novo)
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))


def comprar(ne, b):
    # fim do seg10: linha, que chama o jogadordalinha (perto, tira 4 da pilha) de longe:
    # pop ax; pop dx; pop bx; pop cx; push dx; push ax; push cx; push bx; call; retf
    org10 = ne.seg(10)[1]
    rel = JOGADORDALINHA - (org10 + 11)
    assert ne.append(10, bytes.fromhex('585a5b59525051 53'.replace(' ', '')) + b'\xe8' + struct.pack('<h', rel) + b'\xcb') == org10
    DESTINOS['linha'] = (10, org10)
    org = ne.seg(SEG)[1]
    cod, rot = montar('comprar.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@' + nome], s, alvo] for nome, (s, alvo) in DESTINOS.items()])
    # seg10:37B4: call far 25:0444 (checagem de pilha) -> jmp far desvio, na mesma relocacao
    ne.patch(10, 0x37B4, '9a', 'ea')
    p, recs = _tabela(ne, 10)
    r = next(r for r in recs if r[2] == 0x37B5)
    assert (r[3], r[4]) == (25, 0x0444)
    r[4] = rot['desvio']
    for loc in ATIVAR:
        r = next(r for r in recs if r[2] == loc)
        assert (r[3], r[4]) == DESTINOS['setenabled']
        r[3], r[4] = SEG, rot['ativar']
    _gravar_tabela(ne, p, recs)
    # seg04:4249 (menu Procurar, depois da Lista): call far atualizar(Self, 3) -> fechalista
    p, recs = _tabela(ne, 4)
    r = next(r for r in recs if r[2] == 0x424A)
    assert (r[3], r[4]) == (4, 0x2BD8)
    r[3], r[4] = SEG, rot['fechalista']
    _gravar_tabela(ne, p, recs)
    _dfm(b)
