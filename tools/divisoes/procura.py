"""Colunas Nota e Risco de lesao na Lista de Jogadores (Procura, TcheckPlayerDlg).

A linha de cada jogador e desenhada pela rotina comum seg14:0AD6, que pede ao
jogador (seg03:76A3) um texto com as colunas que a mascara liga (a Procura usa
0xE063: posicao, nome, equipa, forca, nacionalidade, preco e comportamento). O
seg03 nao tem espaco para colunas novas: o fim do desenho da Procura
(ListBoxOfPlayersDrawItem, seg10:321F) salta para procura.asm, acrescentado no
fim do seg10, que escreve os 2 numeros depois do comportamento.

A janela fica 50 pontos mais larga (a lista, o quadro e os botoes), e um texto
"Nota   RL" (Risco de lesao) sobre a lista da nome as colunas.
"""
import struct
from montar import montar
from paginas import _tabela, _gravar_tabela, ler_dfm, escrever_dfm, por, inteiro, texto

FIM_DESENHO = 0x321F                  # leave; retf 0x10
INTTOSTR, TEXTOUT = (24, 0x07D0), (19, 0x1F22)
NOTA_X, RISCO_X = 466, 500            # desde a borda esquerda da lista
MAIS = 50                             # quanto a janela alarga


def _dfm(b):
    classe = 'TcheckPlayerDlg'
    i = b.find(b'TPF0' + bytes([len(classe)]) + classe.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    livre = fim
    while b[livre] == 0 and livre - i < 0x10000 and (livre & 0xFF):
        livre += 1
    c = {f['nome']: f for f in form['filhos']}

    def soma(comp, prop, mais):
        v = dict(comp['props'])[prop]
        atual = struct.unpack('<b', v[1:2])[0] if v[0] == 2 else struct.unpack('<h', v[1:3])[0]
        por(comp, prop, inteiro(atual + mais))

    soma(form, 'ClientWidth', MAIS)
    soma(c['Bevel1'], 'Width', MAIS)
    soma(c['ListBoxOfPlayers'], 'Width', MAIS)
    soma(c['labelListedOnlyFirst'], 'Left', MAIS // 2)
    for nome in ('OKBtn', 'findBtn', 'teamBtn'):
        soma(c[nome], 'Left', MAIS)
    lista_x = dict(c['ListBoxOfPlayers']['props'])['Left'][1] + 2   # + borda
    # espaco no DFM: o texto de exemplo do dinheiro em caixa (o jogo sempre o preenche)
    c['textMoney']['props'] = [pv for pv in c['textMoney']['props'] if pv[0] != 'Caption']
    # sem nome (o enchimento do DFM e curto) e com a largura dada: o
    # AutoSize nao a ajusta ao abrir
    form['filhos'].append(dict(cls='TLabel', nome='', filhos=[], props=[
        ['Left', inteiro(lista_x + NOTA_X - 6)], ['Top', inteiro(168)], ['Width', inteiro(60)],
        ['Caption', texto('Nota   RL')]]))
    novo = b'TPF0' + escrever_dfm(form)
    assert i + len(novo) <= livre, (classe, len(novo), livre - i)
    b[i:livre] = novo + b'\0' * (livre - i - len(novo))


def procura(ne, b):
    base = ne.seg(10)[0]
    assert b[base + FIM_DESENHO: base + FIM_DESENHO + 4] == bytes.fromhex('c9ca1000')
    org = ne.seg(10)[1]
    cod, rot = montar('procura.asm', org, ORG='0x%X' % org, NOTA_X=NOTA_X, RISCO_X=RISCO_X)
    assert ne.append(10, cod) == org
    p, recs = _tabela(ne, 10)
    for nome, (s, alvo) in (('..@in', INTTOSTR), ('..@tn', TEXTOUT),
                            ('..@ir', INTTOSTR), ('..@tr', TEXTOUT)):
        recs.append([3, 0, rot[nome], s, alvo])
    proximo = min(o for o in (ne.seg(t)[0] for t in range(1, ne.nseg + 1)) if o > base)
    assert p + 2 + 8 * len(recs) <= proximo, 'sem espaco no seg10'
    _gravar_tabela(ne, p, recs)
    salto = org - (FIM_DESENHO + 3)
    b[base + FIM_DESENHO: base + FIM_DESENHO + 4] = b'\xe9' + struct.pack('<h', salto) + b'\x90'
    _dfm(b)
