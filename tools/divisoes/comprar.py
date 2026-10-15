"""Botao Comprar na Lista de Jogadores (comprar.asm, no fim do seg25).

- DFM do TcheckPlayerDlg: um TBitBtn "Comprar" entre o "Equipa..." e o "Fechar" (os dois
  sobem/descem para dar lugar), com OnClick = teamBtnClick (o DFM so aceita metodos da
  tabela da janela) e Tag = TAGCOMPRA.
- seg10:37AE (o teamBtnClick da Lista): o comeco vira um salto para desvio, que manda o
  clique com esse Tag para comprar e o resto (Equipa..., duplo clique) para o original.
- comprar: pega o jogador como o Equipa... (e desfaz o fechamento da Lista), prepara a
  janela da equipa escondida (TPlantelDlg: equipa, comprador, jogador), calcula o preco e
  confere as condicoes com as rotinas do jogo, pede confirmacao na janela de Sim/Nao e
  chama o buyBtnClick dela; depois atualiza o dinheiro em caixa e refaz a busca da Lista.
- seg04:4249: quando a Lista fecha, a tela principal redesenha tambem as financas.
"""
from montar import montar
from paginas import ler_dfm, escrever_dfm, mover_dfm, inteiro, texto, ident, por, _tabela, _gravar_tabela
from ingressos import _novos

SEG = 25
CLASSE = 'TcheckPlayerDlg'
TAGCOMPRA = 0x434D
DESTINOS = {
    'volta': (10, 0x37B4),              # o resto do teamBtnClick original
    'equipaclick': (10, 0x37AE),        # teamBtnClick da Lista (passa pelo desvio)
    'acharequipa': (3, 0xD55C),         # equipa pelo id
    'preco': (10, 0x1939),              # TPlantelDlg: preco do jogador escolhido
    'podecomprar': (3, 0x633D),         # o comprador pode levar o jogador?
    'compraclick': (10, 0x1D66),        # TPlantelDlg.buyBtnClick
    'procurarclick': (10, 0x34F5),      # findBtnClick da Lista
    'extenso': (9, 0x335A),
    'settext': (21, 0x1D8C),
    'visivel': (21, 0x1C77),
    'showmodal': (22, 0x5743),
    'atualizar': (4, 0x2BD8),           # TMainForm: redesenha uma parte da tela
}


def _dfm(b):
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    bt = {c['nome']: c for c in form['filhos'] if c['cls'] == 'TBitBtn'}
    team, ok, find = bt['teamBtn'], bt['OKBtn'], bt['findBtn']
    assert dict(find['props'])['Top'] == inteiro(184)
    por(team, 'Top', inteiro(252))
    por(ok, 'Top', inteiro(388))
    props = [[k, v] for k, v in team['props'] if not k.startswith('Glyph') and k not in ('Caption', 'Top', 'OnClick', 'TabOrder')]
    props += [['Top', inteiro(320)], ['Caption', texto('Comprar')], ['Tag', inteiro(TAGCOMPRA)],
              ['OnClick', ident('teamBtnClick')]]
    novo = dict(cls='TBitBtn', nome='', filhos=[], props=props)
    form['filhos'].insert(form['filhos'].index(team) + 1, novo)
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))


def comprar(ne, b):
    org = ne.seg(SEG)[1]
    cod, rot = montar('comprar.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@' + nome], s, alvo] for nome, (s, alvo) in DESTINOS.items()])
    # seg10:37AE: push bp; mov bp,sp; mov ax,0108 -> jmp far desvio (o 01 que sobra fica para tras)
    ne.patch(10, 0x37AE, '5589e5b808', 'eaffff0000')
    _novos(ne, 10, [[3, 0, 0x37AF, SEG, rot['desvio']]])
    # seg04:4249 (menu Procurar, depois da Lista): call far atualizar(Self, 3) -> fechalista
    p, recs = _tabela(ne, 4)
    r = next(r for r in recs if r[2] == 0x424A)
    assert (r[3], r[4]) == (4, 0x2BD8)
    r[3], r[4] = SEG, rot['fechalista']
    _gravar_tabela(ne, p, recs)
    _dfm(b)
