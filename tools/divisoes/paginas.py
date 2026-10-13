"""Classificacao em 2 paginas (1a+2a, 3a+4a) com setas, para mais de 14 equipas.

Usado por patch_divisoes.py. Codigo em paginas.asm (no lugar do
ListBoxDivision3Click, seg06:0922).
"""
import struct
from montar import montar

PAGINA, PAGINA1 = 0x17D0, 0x17D2
ORG = 0x0922          # ListBoxDivision3Click
CLICK4 = 0x09B9       # ListBoxDivision4Click
FIM = 0x0A50          # Timer1Timer (fim do ListBoxDivision4Click)


# ---------- DFM binario (TPF0): arvore com os valores crus ----------
def _ler_valor(d, p):
    t = d[p]; p += 1
    if t in (0,): return p
    if t == 2: return p + 1
    if t == 3: return p + 2
    if t == 4: return p + 4
    if t == 5: return p + 10
    if t in (6, 7): return p + 1 + d[p]
    if t in (8, 9): return p
    if t == 10: return p + 4 + struct.unpack_from('<I', d, p)[0]
    if t == 11:
        while d[p]: p += 1 + d[p]
        return p + 1
    if t == 12: return p + 4 + struct.unpack_from('<I', d, p)[0]
    if t == 1:
        while d[p]: p = _ler_valor(d, p)
        return p + 1
    raise ValueError('tipo %d' % t)


def _sstr(d, p):
    return d[p + 1:p + 1 + d[p]].decode('latin1'), p + 1 + d[p]


def ler_dfm(d, p=4):
    cls, p = _sstr(d, p)
    nome, p = _sstr(d, p)
    props = []
    while d[p]:
        pn, p = _sstr(d, p)
        q = _ler_valor(d, p)
        props.append([pn, bytes(d[p:q])])
        p = q
    p += 1
    filhos = []
    while d[p]:
        f, p = ler_dfm(d, p)
        filhos.append(f)
    return dict(cls=cls, nome=nome, props=props, filhos=filhos), p + 1


def _s(s):
    s = s.encode('latin1')
    return bytes([len(s)]) + s


def escrever_dfm(c):
    o = _s(c['cls']) + _s(c['nome'])
    for pn, v in c['props']:
        o += _s(pn) + v
    o += b'\0'
    for f in c['filhos']:
        o += escrever_dfm(f)
    return o + b'\0'


def inteiro(v):
    return (b'\x02' + struct.pack('<b', v)) if -128 <= v < 128 else (b'\x03' + struct.pack('<h', v))


def ident(s): return b'\x07' + _s(s)
def texto(s): return b'\x06' + _s(s)
def conjunto(*s): return b'\x0b' + b''.join(_s(x) for x in s) + b'\0'
FALSO = b'\x08'


def por(c, prop, valor):
    for pv in c['props']:
        if pv[0] == prop:
            pv[1] = valor; return
    c['props'].append([prop, valor])


def seta(nome, esquerda, legenda, metodo):
    props = [
        ['Left', inteiro(esquerda)], ['Top', inteiro(12)], ['Width', inteiro(30)], ['Height', inteiro(26)],
        ['Alignment', ident('taCenter')], ['AutoSize', FALSO], ['Caption', texto(legenda)],
        ['Color', ident('clSilver')], ['Font.Color', ident('clBlack')], ['Font.Height', inteiro(-21)],
        ['Font.Name', texto('Arial')], ['Font.Style', conjunto('fsBold')],
        ['ParentColor', FALSO], ['ParentFont', FALSO], ['OnClick', ident(metodo)],
    ]
    return dict(cls='TLabel', nome=nome, props=props, filhos=[])


def novo_dfm(antigo, n):
    form, fim = ler_dfm(antigo)
    alt = 16 * n + 2 + 4                              # n linhas de 16 + borda + 4 do otvdm
    por(form, 'ClientHeight', inteiro(41 + alt + 12))
    f = {c['nome']: c for c in form['filhos']}
    por(f['Label2'], 'Top', inteiro(17)); por(f['Label2'], 'Left', inteiro(307))
    por(f['Label3'], 'Left', inteiro(11)); por(f['Label3'], 'Visible', FALSO)
    por(f['Label4'], 'Top', inteiro(17)); por(f['Label4'], 'Visible', FALSO)
    por(f['ListBoxDivision1'], 'Height', inteiro(alt))
    por(f['ListBoxDivision2'], 'Top', inteiro(41)); por(f['ListBoxDivision2'], 'Left', inteiro(306))
    por(f['ListBoxDivision2'], 'Height', inteiro(alt))
    # as listas 3 e 4 saem; as setas ficam com os nomes (e os campos) delas
    i = form['filhos'].index(f['ListBoxDivision3'])
    form['filhos'][i:i + 2] = [seta('ListBoxDivision3', 517, '<', 'ListBoxDivision3Click'),
                               seta('ListBoxDivision4', 548, '>', 'ListBoxDivision4Click')]
    novo = b'TPF0' + escrever_dfm(form)
    assert len(novo) <= fim, (len(novo), fim)
    return novo + b'\0' * (fim - len(novo))


# ---------- relocacoes do seg06 ----------
def _tabela(ne, s):
    base, ln, fl, mn = ne.seg(s)
    n = struct.unpack_from('<H', ne.b, base + ln)[0]
    return base + ln, [list(struct.unpack_from('<BBHHH', ne.b, base + ln + 2 + 8 * r)) for r in range(n)]


def _gravar_tabela(ne, p, recs):
    struct.pack_into('<H', ne.b, p, len(recs))
    for r, rec in enumerate(recs):
        struct.pack_into('<BBHHH', ne.b, p + 2 + 8 * r, *rec)


def paginar(ne, b, n):
    cod, rot = montar('paginas.asm', ORG)
    assert len(cod) <= FIM - ORG, len(cod)
    assert rot['click4'] == CLICK4
    base = ne.seg(6)[0]

    # relocacoes: as do ListBoxDivision3Click somem, menos uma, que vira o
    # call far SetVisible; a chamada a seg06:0378 ao abrir passa por "abrir"
    p, recs = _tabela(ne, 6)
    velhas = [r for r in recs if ORG <= r[2] < FIM]
    assert len(velhas) == 16 and all(r[1] == 0 and ne.b[base + r[2] - 1] == 0x9A for r in velhas)
    novos = []
    for r in recs:
        if r is velhas[0]:
            novos.append([3, 0, rot['..@sv'], 0x15, 0x1C77])   # seg21:1C77 TControl.SetVisible
        elif r is velhas[1]:
            novos.append([3, 0, rot['..@se'], 0x13, 0x0FDF])   # seg19:0FDF TFont.SetColor
        elif r in velhas:
            continue
        elif r[2] == 0x02D0:
            assert r[3:] == [6, 0x0378]
            novos.append(r[:3] + [6, rot['abrir']])
        else:
            novos.append(r)
    assert len(novos) == len(recs) - 14
    _gravar_tabela(ne, p, novos)
    b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * (8 * 14)

    b[base + ORG: base + ORG + len(cod)] = cod

    # FormCreate: nao preenche as listas 3 e 4 (os campos delas agora sao as setas)
    for off, para, velho in ((0x0706, 0x0732, 'c47e06'), (0x078A, 0x07D8, '8dbefe')):
        ne.patch(6, off, velho, b'\xeb' + bytes([para - (off + 2)]) + bytes.fromhex(velho)[2:])
    # DrawItem 1 e 2: divisao = [PAGINA] / [PAGINA1] (push ss/pop es abre 2 bytes)
    for off, var, k in ((0x03F9, PAGINA, 0), (0x04B1, PAGINA1, 1)):
        ne.patch(6, off, '8cd38ec38cdbfc8d7ef8c5760cb90800f3a48edbff76106a%02x' % k,
                 '16078cdbfc8d7ef8c5760cb90800f3a48edbff7610ff36' + struct.pack('<H', var).hex())
    # Click 1 e 2 (abre a equipa): a 2a leitura do ItemIndex vira o indice da divisao
    for off, campo, var, k in ((0x0822, 0x17C, PAGINA, 0), (0x08B9, 0x180, PAGINA1, 1)):
        velho = bytes.fromhex('c47e0626c4bd') + struct.pack('<H', campo) + bytes.fromhex('06579affff000050') + bytes([0x6A, k])
        cur = bytes(b[base + off: base + off + len(velho)])
        assert cur[:8] == velho[:8] and cur[13:] == velho[13:], (hex(off), cur.hex())
        b[base + off: base + off + 7] = b'\x50\xff\x36' + struct.pack('<H', var) + b'\xeb' + bytes([len(velho) - 7])

    # DFM
    i = b.find(b'TPF0\x12TclassificationDlg')
    assert i > 0
    _, fim = ler_dfm(b, i + 4)
    novo = novo_dfm(bytes(b[i:fim]), n)
    b[i:fim] = novo
    return rot


def _recurso(b, nome):
    """(posicao da entrada na tabela de recursos, deslocamento, tamanho, alinhamento)."""
    ne = struct.unpack_from('<H', b, 0x3C)[0]
    rt = ne + struct.unpack_from('<H', b, ne + 0x24)[0]
    sh = struct.unpack_from('<H', b, rt)[0]
    p = rt + 2
    while True:
        tipo, n = struct.unpack_from('<HH', b, p)
        p += 8
        assert tipo, nome
        for _ in range(n):
            off, ln, fl, id_ = struct.unpack_from('<HHHH', b, p)
            if not id_ & 0x8000 and b[rt + id_ + 1: rt + id_ + 1 + b[rt + id_]].upper() == nome.upper().encode():
                return p, off << sh, ln << sh, sh
            p += 12


def mover_dfm(b, classe, novo, folga=512):
    """Poe o DFM novo (b'TPF0'...) da janela no fim do arquivo e aponta a entrada dela na
    tabela de recursos para la; o antigo e zerado. Os recursos ficam no fim do EXE, antes
    dos 4 bytes da autoverificacao (gravar recalcula a soma). A folga e para as
    traducoes crescerem."""
    p, off, ln, sh = _recurso(b, classe)
    i = b.find(b'TPF0' + bytes([len(classe)]) + classe.encode())
    assert off <= i < off + ln, (classe, hex(off), hex(i))
    b[off: off + ln] = b'\0' * ln
    pos = len(b) - 4
    assert pos % (1 << sh) == 0, pos
    tam = -(-(len(novo) + folga) // (1 << sh)) * (1 << sh)
    b[pos:] = novo + b'\0' * (tam - len(novo)) + b'\0\0\0\0'
    struct.pack_into('<HH', b, p, pos >> sh, tam >> sh)
