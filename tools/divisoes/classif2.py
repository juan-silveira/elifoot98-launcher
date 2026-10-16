"""Classificacao moderna: uma divisao por pagina (1a a 4a, setas < >), tema escuro, colunas
Pts PJ VIT E DER GM GC SG, barra da zona (campeao, sobe, desce), quadrinho nas cores da
equipa e as ultimas 5 (bolinhas). Usado por patch_divisoes.py em todos os ELIFnn.EXE, no
lugar da Classificacao original (classificacao(), paginas.py e classif_humano).

- seg06:0922..0A50 (ListBoxDivision3Click..4Click): classif2_06.asm (setas e paginas).
- seg06:03EB (ListBoxDivision1DrawItem): jmp far para a "linha" de classif2.asm (seg25).
- seg06:02D0 (abrir): "humanos" de classif2.asm escolhe a divisao com mais humanos.
- DFM: so a lista 1 (largura toda) e os 4 titulos no mesmo lugar; rotulos das colunas e a
  legenda; cores escuras. As listas 3 e 4 viram as setas (como paginas.py).
"""
import struct
from montar import montar
from paginas import ler_dfm, escrever_dfm, mover_dfm, inteiro, texto, ident, conjunto, por, _tabela, _gravar_tabela, FALSO
from ingressos import _novos

ORG, CLICK4, FIM = 0x0922, 0x09B9, 0x0A50
PAGINA = 0x17D0
SEG = 25
OTVDM = 4
ALT = 18                     # altura de cada linha

# colunas (de classif2.asm), a partir da borda da lista
X = dict(POS=24, NOME=50, PTS=250, PJ=280, V=310, E=340, D=370, GM=400, GC=430, SG=460, ULT=478)

FUNDO, TEXTO, FRACO = 0x00242120, 0x00EDEAE8, 0x00A6A09A
OURO, SOBE, DESCE = 0x0004BCFB, 0x0053A834, 0x003543EA
VITORIA, EMPATE, DERROTA = 0x003E8E1E, 0x0068635F, 0x002530D9

DESTINOS = {
    'item': (23, 0x0DD0),        # TList.Get
    'int': (24, 0x07D0),         # IntToStr
    'ell': (19, 0x1C74),         # TCanvas.Ellipse
    'text': (19, 0x1F22),        # TCanvas.TextOut
    'brush': (19, 0x1656),       # TBrush.SetColor
    'pen': (19, 0x13AC),         # TPen.SetColor
    'font': (19, 0x0FDF),        # TFont.SetColor
    'fill': (19, 0x1CB7),        # TCanvas.FillRect
    'setc': (21, 0x1ED5),        # TControl.SetColor
    'setv': (21, 0x1C77),        # TControl.SetVisible
}
TAGDIV = 0x7700              # Tag dos botoes de divisao (classif2.asm)


def cor(c):
    return b'\x04' + struct.pack('<i', c)


def rotulo(esquerda, topo, largura, altura, legenda, cor_letra=TEXTO, tam=-11, negrito=False,
           alinhar=None, fundo=None):
    props = [['Left', inteiro(esquerda)], ['Top', inteiro(topo)], ['Width', inteiro(largura)],
             ['Height', inteiro(altura)], ['AutoSize', FALSO], ['Caption', texto(legenda)]]
    if alinhar:
        props.append(['Alignment', ident(alinhar)])
    if fundo is not None:
        props += [['Color', cor(fundo)], ['ParentColor', FALSO]]
    props += [['Font.Color', cor(cor_letra)], ['Font.Height', inteiro(tam)], ['Font.Name', texto('Arial')],
              ['Font.Style', conjunto('fsBold') if negrito else conjunto()], ['ParentFont', FALSO]]
    return dict(cls='TLabel', nome='', props=props, filhos=[])


def botao(nome, esquerda, divisao):
    r = rotulo(esquerda, 12, 38, 24, '%d\xaa' % (divisao + 1), TEXTO, -15, True, 'taCenter', fundo=0x00484543)
    r['nome'] = nome
    r['props'] += [['Tag', inteiro(TAGDIV + divisao) if TAGDIV + divisao < 32768 else b'\x04' + struct.pack('<i', TAGDIV + divisao)],
                   ['OnClick', ident('ListBoxDivision3Click')]]
    return r


def novo_dfm(antigo, n):
    form, fim = ler_dfm(antigo)
    lista_top, lista_alt = 62, ALT * n + 2 + OTVDM
    largura = 556
    leg = lista_top + lista_alt + 10
    por(form, 'ClientWidth', inteiro(largura + 20))
    por(form, 'ClientHeight', inteiro(leg + 44))
    por(form, 'Color', cor(FUNDO))
    f = {c['nome']: c for c in form['filhos']}
    for k in range(1, 5):
        lb = f['Label%d' % k]
        por(lb, 'Left', inteiro(12)); por(lb, 'Top', inteiro(12)); por(lb, 'Width', inteiro(300))
        por(lb, 'Height', inteiro(26)); por(lb, 'Font.Color', cor(TEXTO)); por(lb, 'Font.Height', inteiro(-21))
        if k > 1:
            por(lb, 'Visible', FALSO)
    l1, l2 = f['ListBoxDivision1'], f['ListBoxDivision2']
    por(l1, 'Left', inteiro(10)); por(l1, 'Top', inteiro(lista_top)); por(l1, 'Width', inteiro(largura))
    por(l1, 'Height', inteiro(lista_alt)); por(l1, 'ItemHeight', inteiro(ALT))
    por(l1, 'Font.Height', inteiro(-12)); por(l1, 'Font.Color', cor(TEXTO))
    por(l1, 'Color', cor(FUNDO)); por(l1, 'ParentColor', FALSO); por(l1, 'BorderStyle', ident('bsNone'))
    por(l2, 'Visible', FALSO)
    # as listas 3 e 4 saem; os botoes das divisoes 1 e 2 ficam com os nomes delas
    i = form['filhos'].index(f['ListBoxDivision3'])
    x0 = largura + 10 - 4 * 38
    form['filhos'][i:i + 2] = [botao('ListBoxDivision3', x0, 0), botao('ListBoxDivision4', x0 + 38, 1),
                               botao('', x0 + 76, 2), botao('', x0 + 114, 3)]
    # rotulos das colunas
    cab = [rotulo(10 + X['NOME'], 44, 120, 14, 'Clube', FRACO)]
    for chave, nome in (('PTS', 'Pts'), ('PJ', 'PJ'), ('V', 'VIT'), ('E', 'E'), ('D', 'DER'),
                        ('GM', 'GM'), ('GC', 'GC'), ('SG', 'SG')):
        cab.append(rotulo(10 + X[chave] - 15, 44, 30, 14, nome, FRACO, alinhar='taCenter'))
    cab.append(rotulo(10 + X['ULT'], 44, 72, 14, '\xdaltimas 5', FRACO, alinhar='taCenter'))
    # legenda
    for k, (c, nome) in enumerate(((OURO, 'Campe\xe3o'), (SOBE, 'Sobe de divis\xe3o'), (DESCE, 'Desce de divis\xe3o'))):
        cab.append(rotulo(12, leg + 2 + 14 * k, 4, 11, '', fundo=c))
        cab.append(rotulo(22, leg + 14 * k, 140, 14, nome, FRACO))
    for k, (c, nome) in enumerate(((VITORIA, 'Vit\xf3ria'), (EMPATE, 'Empate'), (DERROTA, 'Derrota'))):
        cab.append(rotulo(300, leg + 2 + 14 * k, 10, 10, '', fundo=c))
        cab.append(rotulo(316, leg + 14 * k, 120, 14, nome, FRACO))
    j = form['filhos'].index(f['Timer1'])
    form['filhos'][j:j] = cab
    return b'TPF0' + escrever_dfm(form)


def classificar(ne, b, n):
    # seg06: setas e paginas
    cod, rot6 = montar('classif2_06.asm', ORG)
    assert len(cod) <= FIM - ORG, len(cod)
    assert rot6['click4'] == CLICK4
    base = ne.seg(6)[0]
    p, recs = _tabela(ne, 6)
    velhas = [r for r in recs if ORG <= r[2] < FIM]
    assert len(velhas) == 16 and all(r[1] == 0 and ne.b[base + r[2] - 1] == 0x9A for r in velhas)
    novos = [r for r in recs if r not in velhas]
    _gravar_tabela(ne, p, novos)
    b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * (8 * (len(recs) - len(novos)))
    b[base + ORG: base + ORG + len(cod)] = cod

    # FormCreate: nao preenche as listas 3 e 4 (os campos delas agora sao as setas)
    for off, para, velho in ((0x0706, 0x0732, 'c47e06'), (0x078A, 0x07D8, '8dbefe')):
        ne.patch(6, off, velho, b'\xeb' + bytes([para - (off + 2)]) + bytes.fromhex(velho)[2:])
    # Click 1 (abre a equipa): a divisao e [PAGINA]
    off, campo = 0x0822, 0x17C
    velho = bytes.fromhex('c47e0626c4bd') + struct.pack('<H', campo) + bytes.fromhex('06579affff0000506a00')
    cur = bytes(b[base + off: base + off + len(velho)])
    assert cur[:8] == velho[:8] and cur[13:] == velho[13:], cur.hex()
    b[base + off: base + off + 7] = b'\x50\xff\x36' + struct.pack('<H', PAGINA) + b'\xeb' + bytes([len(velho) - 7])

    # seg25: humanos e linha
    org = ne.seg(SEG)[1]
    cod, rot = montar('classif2.asm', org, ORG='0x%X' % org, N=n)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@' + nome], s, alvo] for nome, (s, alvo) in DESTINOS.items()]
           + [[3, 0, rot['..@abrir'], 6, 0x0378]])
    # seg06:02D0 (call far 6:0378 ao abrir) -> humanos; seg06:03EB (DrawItem 1) -> jmp far linha
    p, recs = _tabela(ne, 6)
    r = next(r for r in recs if r[2] == 0x02D0)
    assert r[3:] == [6, 0x0378]
    r[3], r[4] = SEG, rot['humanos']
    _gravar_tabela(ne, p, recs)
    ne.patch(6, 0x03EB, '5589e5b8', 'eaffff00')
    ne.patch(6, 0x03EF, '10', '00')
    _novos(ne, 6, [[3, 0, 0x03EC, SEG, rot['linha']], [3, 0, rot6['..@botao'], SEG, rot['botao']]])

    # DFM (no fim do arquivo: o novo e maior)
    i = b.find(b'TPF0\x12TclassificationDlg')
    assert i > 0
    _, fim = ler_dfm(b, i + 4)
    mover_dfm(b, 'TclassificationDlg', novo_dfm(bytes(b[i:fim]), n))
    return rot
