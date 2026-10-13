"""Campinho na aba Escalacao (Seleccao) da janela do time, com os titulares.

A rotina que confere a escalacao (seg04:38B0) e chamada de 17 lugares, sempre que
ela muda (clique na lista, menu Seleccionar, F-teclas, ao abrir a janela), e acaba
com OKBtn.SetEnabled (seg04:3A55 -> seg21:1CB8). Essa chamada passa a ser campo.asm
(no seg25, que arbitro.py ja levou para o fim do arquivo), que liga o botao e desenha
no TImage novo da aba: fundo branco, linhas verdes, e uma bolinha azul por titular,
na coluna do setor (G, D, M, A) e espalhadas na altura.

O TImage usa o campo do Label13 (+0x2D0, texto fixo que o codigo nao le) e vem do DFM
com um TBitmap de 8x8; o codigo poe o bitmap no tamanho da imagem (SetWidth/SetHeight
da VMT do TBitmap) e desenha no canvas dele (TBitmap.GetCanvas, seg19:5945). O DFM vai
para o fim do arquivo (mover_dfm).
"""
import struct
from montar import montar
from paginas import (_tabela, _gravar_tabela, ler_dfm, escrever_dfm, inteiro, texto,
                     mover_dfm)
from ingressos import _novos

SEG = 25
CHAMADA = 0x3A55                       # seg04: call far OKBtn.SetEnabled no fim da 38B0
CLASSE = 'TprepareGameDlg'
DESTINOS = {                           # rotulo do salto -> (segmento, deslocamento)
    'habilitar': (21, 0x1CB8),         # TControl.SetEnabled
    'largura': (19, 0x61A2),           # TBitmap.SetWidth
    'altura': (19, 0x6116),            # TBitmap.SetHeight
    'canvas': (19, 0x5945),            # TBitmap.GetCanvas
    'corpincel': (19, 0x1656),         # TBrush.SetColor
    'corcaneta': (19, 0x13AC),         # TPen.SetColor
    'espessura': (19, 0x14C7),         # TPen.SetWidth
    'preencher': (19, 0x1CB7),         # TCanvas.FillRect
    'retangulo': (19, 0x1E84),         # TCanvas.Rectangle
    'elipse': (19, 0x1C74),            # TCanvas.Ellipse
    'mover': (19, 0x1D8A),             # TCanvas.MoveTo
    'linha': (19, 0x1D4D),             # TCanvas.LineTo
    'item': (3, 0x0EF0),               # jogador i da lista do time
}


def _bmp(lado=8):
    """BMP de 4 bits, lado x lado, branco."""
    paleta = bytes([0, 0, 0, 0, 0, 0, 0x80, 0, 0, 0x80, 0, 0, 0, 0x80, 0x80, 0, 0x80, 0, 0, 0,
                    0x80, 0, 0x80, 0, 0x80, 0x80, 0, 0, 0xC0, 0xC0, 0xC0, 0, 0x80, 0x80, 0x80, 0,
                    0, 0, 0xFF, 0, 0, 0xFF, 0, 0, 0, 0xFF, 0xFF, 0, 0xFF, 0, 0, 0, 0xFF, 0, 0xFF, 0,
                    0xFF, 0xFF, 0, 0, 0xFF, 0xFF, 0xFF, 0])
    linha = ((lado * 4 + 31) // 32) * 4
    bits = bytes([0xFF]) * (linha * lado)
    info = struct.pack('<IiiHHIIiiII', 40, lado, lado, 1, 4, 0, len(bits), 0, 0, 16, 0)
    off = 14 + len(info) + len(paleta)
    return b'BM' + struct.pack('<IHHI', off + len(bits), 0, 0, off) + info + paleta + bits


def _picture():
    bmp = _bmp()
    dados = bytes([7]) + b'TBitmap' + struct.pack('<I', len(bmp)) + bmp
    return b'\x0a' + struct.pack('<I', len(dados)) + dados


def _dfm(b):
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)

    def todos(c):
        yield c
        for f in c['filhos']:
            yield from todos(f)
    c = {x['nome']: x for x in todos(form)}
    assert 'Rotulo13' not in c
    c['Label13']['nome'] = 'Rotulo13'          # o texto fixo cede o campo a imagem
    selecao = [x for x in todos(form) if x['cls'] == 'TPage'
               and dict(x['props']).get('Caption') == texto('Selec\xe7\xe3o')]
    assert len(selecao) == 1
    selecao[0]['filhos'].insert(0, dict(cls='TImage', nome='Label13', filhos=[], props=[
        ['Left', inteiro(8)], ['Top', inteiro(8)], ['Width', inteiro(256)], ['Height', inteiro(166)],
        ['Picture.Data', _picture()]]))
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))


def _desviar(ne, s, local, seg, alvo):
    """A chamada em local (dentro de uma cadeia de relocacoes ou nao) passa a ir para
    seg:alvo, com um registro proprio."""
    p, recs = _tabela(ne, s)
    base = ne.seg(s)[0]
    b = ne.b
    for r in recs:
        if r[0] != 3 or r[1] & 7:
            continue
        ant, pos = None, r[2]
        while pos != 0xFFFF:
            nxt = struct.unpack_from('<H', b, base + pos)[0]
            if pos == local:
                if ant is None and nxt == 0xFFFF:       # sozinho: so muda o destino
                    r[3], r[4] = seg, alvo
                    _gravar_tabela(ne, p, recs)
                    return
                if ant is None:
                    r[2] = nxt
                else:
                    struct.pack_into('<H', b, base + ant, nxt)
                _gravar_tabela(ne, p, recs)
                struct.pack_into('<HH', b, base + local, 0xFFFF, 0)
                _novos(ne, s, [[3, 0, local, seg, alvo]])
                return
            ant, pos = pos, nxt
    raise AssertionError('relocacao nao achada: seg%02d:%04X' % (s, local))


def campo(ne, b):
    s4 = ne.seg(4)[0]
    assert b[s4 + CHAMADA: s4 + CHAMADA + 1] == b'\x9a'
    org = ne.seg(SEG)[1]
    cod, rot = montar('campo.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@' + nome], s, alvo] for nome, (s, alvo) in DESTINOS.items()])
    _desviar(ne, 4, CHAMADA + 1, SEG, rot['campo'])
    _dfm(b)
