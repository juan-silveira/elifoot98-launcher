"""Preco dos ingressos escolhido pelo tecnico (aba Financas da janela do time).

O jogo calcula um preco para cada jogo (seg09:35E1, guardado no jogo em +0x36) e a
renda e publico x preco (seg03:93C4 no campeonato; 98A6/98DB na Taca, dividida). O
publico nao dependia do preco. Agora:
- o TTeam ganha um byte em +0xCB (o tamanho da instancia, na VMT em seg03:0215,
  passa de 0xCB para 0xCC; o Delphi zera o objeto ao criar): 0 = nao escolhido (x1),
  2 a 12 = a escolha em quartos (50% a 300%, de 25 em 25);
- o byte vai para o save no cabecalho "EFa" do time (byte 4, um dos reservados):
  TTeam.Store poe o valor no cabecalho (DS:0x2AFE) antes da gravacao (seg12:1694) e
  a leitura (seg12:15B8) o guarda em DS:0x2B02, que TTeam.Load passa ao time;
- nos jogos em casa a renda usa preco x escolha e a procura cai com a escolha
  elevada a 1,3, antes do limite do estadio (o Round em seg03:B1EC);
- um campo de % (TSpinEdit) na aba Financas, ao lado do "Preco dos bilhetes" (que
  continua o preco de tabela do proximo jogo). O OnChange dele e o TabSetClick, que
  passa a ser o de abas.asm: grava a escolha no time do tecnico, e ao trocar de aba
  o campo mostra o que o time tem.

Os ajudantes ficam no fim do seg22 (preco e publico) e do seg16 (o byte no save),
ingressos.asm, e do seg02 (abas.asm): o seg03 nao tem espaco. Os pontos de chamada que ja tinham relocacao (Round, leitura e
gravacao do .EFT) so trocam o destino dela.
"""
import struct
from montar import montar
from paginas import (_tabela, _gravar_tabela, ler_dfm, escrever_dfm, por, inteiro,
                     ident, texto)

CAB = 0x2AFE                       # cabecalho "EFa" do time no DS (seg11:395E)
TAMANHO_TTEAM = 0x0215             # seg03: tamanho da instancia na VMT do TTeam
PRECO = (0x93C4, 0x98A6, 0x98DB)   # seg03: fld tword [es:di+0x36] da renda
ROUND_PUBLICO = 0xB1EC             # seg03: Round do publico
LER = (0x284D, 0x2C33)             # seg03: TTeam.Load chamando a leitura do .EFT
GRAVAR = 0x3093                    # seg03: TTeam.Store chamando a gravacao do .EFT
LEITURA_IO = 0x15DB                # seg12: verificacao de E/S depois de ler o cabecalho
TABSETCLICK = 0x3215               # seg04
WMPAINT = 0x4A8F                   # seg21: TWinControl.WMPaint, depois do prologo
SELETOR = 0x2CC                    # campo do Label12 (o "Golos" fixo do Historial)
DICA = '% do pre\xe7o de tabela nos seus jogos em casa'


def _registro(ne, s, local):
    """Indice do registro de relocacao que aponta o local (sem cadeia)."""
    p, recs = _tabela(ne, s)
    achados = [i for i, r in enumerate(recs) if r[2] == local and (r[1] & 3) == 0]
    assert len(achados) == 1, (s, hex(local), achados)
    base = ne.seg(s)[0]
    assert ne.b[base + local: base + local + 2] == b'\xff\xff', (s, hex(local))
    return p, recs, achados[0]


def _redirecionar(ne, s, local, alvo_seg, alvo, de=None):
    p, recs, i = _registro(ne, s, local)
    if de:
        assert (recs[i][3], recs[i][4]) == de, (s, hex(local), recs[i])
    recs[i][3], recs[i][4] = alvo_seg, alvo
    _gravar_tabela(ne, p, recs)


def _novos(ne, s, registros):
    """Acrescenta registros [tipo, flags, local, seg, alvo] na tabela do segmento."""
    p, recs = _tabela(ne, s)
    base = ne.seg(s)[0]
    proximo = min(o for o in (ne.seg(x)[0] for x in range(1, ne.nseg + 1)) if o > base)
    recs = recs + registros
    assert p + 2 + 8 * len(recs) <= proximo, 'sem espaco para relocacoes no seg%02d' % s
    _gravar_tabela(ne, p, recs)


def _anexar(ne, s, asm, **defs):
    org = ne.seg(s)[1]
    cod, rot = montar(asm, org, ORG='0x%X' % org, **defs)
    assert ne.append(s, cod) == org
    return rot


def _dfm(b):
    classe = 'TprepareGameDlg'
    i = b.find(b'TPF0' + bytes([len(classe)]) + classe.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    livre = fim
    while b[livre] == 0 and livre - i < 0x10000 and (livre & 0xFF):
        livre += 1

    def todos(c):
        yield c
        for f in c['filhos']:
            yield from todos(f)
    c = {x['nome']: x for x in todos(form)}
    assert 'Golos12' not in c
    c['Label12']['nome'] = 'Golos12'           # o texto fixo cede o campo ao seletor
    financas = [x for x in todos(form) if x['cls'] == 'TPage'
                and dict(x['props']).get('Caption') == texto('Finan\xe7as')]
    assert len(financas) == 1
    # espaco no DFM: textos de exemplo de rotulos que o codigo sempre preenche
    for nome in ('textTicketPrice', 'textLoanInterest', 'textTotalSalaries', 'textMoney', 'textMoney2',
                 'textReferee', 'textLastResult', 'LabelSuspended', 'LabelInjured'):
        c[nome]['props'] = [pv for pv in c[nome]['props'] if pv[0] != 'Caption']
    # o preco em %, de 25 em 25 (o TSpinEdit e a classe que a janela ja usa no salario;
    # um TComboBox nao: o Delphi so acha as classes dos campos do formulario)
    financas[0]['filhos'].append(dict(cls='TSpinEdit', nome='Label12', filhos=[], props=[
        ['Left', inteiro(204)], ['Top', inteiro(305)], ['Width', inteiro(52)], ['Height', inteiro(24)],
        ['Increment', inteiro(25)], ['MaxValue', inteiro(300)],
        ['MinValue', inteiro(50)], ['Value', inteiro(100)], ['ShowHint', b'\x09'],
        ['Hint', texto(DICA)], ['OnChange', ident('TabSetClick')]]))
    financas[0]['filhos'].append(dict(cls='TLabel', nome='', filhos=[], props=[
        ['Left', inteiro(258)], ['Top', inteiro(310)], ['Caption', texto('%')]]))
    novo = b'TPF0' + escrever_dfm(form)
    assert i + len(novo) <= livre, (classe, len(novo), livre - i)
    b[i:livre] = novo + b'\0' * (livre - i - len(novo))


def ingressos(ne, b):
    s3 = ne.seg(3)[0]
    assert b[s3 + TAMANHO_TTEAM: s3 + TAMANHO_TTEAM + 2] == b'\xcb\x00'
    for o in PRECO:
        assert b[s3 + o: s3 + o + 5] == bytes.fromhex('9b26db6d36'), hex(o)

    # seg22: preco e publico; seg16: leitura e gravacao do byte
    PRECOS, BYTE = 22, 16
    rot = _anexar(ne, PRECOS, 'ingressos.asm', CAB='0x%X' % CAB, PARTE=1)
    _novos(ne, PRECOS, [[3, 0, rot['..@round'], 25, 0x1241]])
    rotb = _anexar(ne, BYTE, 'ingressos.asm', CAB='0x%X' % CAB, PARTE=2)
    _novos(ne, BYTE, [[3, 0, rotb['..@ler'], 12, 0x15B8],
                      [3, 0, rotb['..@grava'], 12, 0x1694]])

    # seg03
    b[s3 + TAMANHO_TTEAM] = 0xCC
    _redirecionar(ne, 3, ROUND_PUBLICO + 1, PRECOS, rot['publico'], de=(25, 0x1241))
    for o in LER:
        _redirecionar(ne, 3, o + 1, BYTE, rotb['carregar'], de=(12, 0x15B8))
    _redirecionar(ne, 3, GRAVAR + 1, BYTE, rotb['gravar'], de=(12, 0x1694))
    # renda: call far preco no lugar do fld, os 3 numa cadeia de um registro so
    for k, o in enumerate(PRECO):
        prox = PRECO[k + 1] + 1 if k + 1 < len(PRECO) else 0xFFFF
        b[s3 + o: s3 + o + 5] = b'\x9a' + struct.pack('<HH', prox, 0)
    _novos(ne, 3, [[3, 0, PRECO[0] + 1, PRECOS, rot['preco']]])

    # seg12: depois de ler o cabecalho, o byte 4 dele vai para CAB+4
    s12 = ne.seg(12)[0]
    assert b[s12 + LEITURA_IO: s12 + LEITURA_IO + 5] == bytes.fromhex('9affff0000')
    org12 = ne.seg(12)[1]
    ajudante = (bytes.fromhex('9affff0000')                       # o que estava no lugar
                + bytes.fromhex('8a46d2')                         # mov al, [bp-0x2E]
                + b'\xa2' + struct.pack('<H', CAB + 4)            # mov [CAB+4], al
                + b'\xc3')
    assert ne.append(12, ajudante) == org12
    p, recs, i = _registro(ne, 12, LEITURA_IO + 1)
    assert (recs[i][3], recs[i][4]) == (25, 0x0408), recs[i]
    recs[i][2] = org12 + 1
    _gravar_tabela(ne, p, recs)
    b[s12 + LEITURA_IO: s12 + LEITURA_IO + 5] = (
        b'\xe8' + struct.pack('<h', org12 - (LEITURA_IO + 3)) + b'\x90\x90')

    # seg02 + seg04: o TabSetClick novo
    ABAS = 2
    rot22 = _anexar(ne, ABAS, 'abas.asm', SELETOR='0x%X' % SELETOR)
    _novos(ne, ABAS, [[3, 0, rot22['..@pagina'], 17, 0x3460],
                    [3, 0, rot22['..@ler'], 7, 0x1836],
                    [3, 0, rot22['..@escolher'], 7, 0x1899]])
    s4 = ne.seg(4)[0]
    assert b[s4 + TABSETCLICK: s4 + TABSETCLICK + 5] == bytes.fromhex('5589e531c0')
    b[s4 + TABSETCLICK: s4 + TABSETCLICK + 5] = bytes.fromhex('eaffff0000')
    _novos(ne, 4, [[3, 0, TABSETCLICK + 1, ABAS, rot22['abas']]])

    # TWinControl.WMPaint (seg21) usa o PaintHandler, que pinta passando o DC, se o
    # controle tem qualquer filho (ControlCount); o TSpinEdit tem o botao de setas, e o
    # Wine 16 bits do Android nao desenha o texto do edit nesse DC (o campo do salario
    # em Renovar contrato tambem ficava vazio). O PaintHandler so serve para pintar os
    # filhos graficos, entao o teste passa a ser FControls <> nil (o seletor do ponteiro)
    s21 = ne.seg(21)[0]
    assert b[s21 + WMPAINT: s21 + WMPAINT + 14] == bytes.fromhex('c47e0606579affff000009c07514')
    p, recs, i = _registro(ne, 21, WMPAINT + 6)
    assert (recs[i][3], recs[i][4]) == (21, 0x39FD), recs[i]    # GetControlCount
    del recs[i]
    _gravar_tabela(ne, p, recs)
    b[s21 + WMPAINT: s21 + WMPAINT + 12] = bytes.fromhex(
        'c47e06'               # les di, [bp+6]
        '2683bd940000'         # cmp word [es:di+0x94], 0
        '909090')

    _dfm(b)
