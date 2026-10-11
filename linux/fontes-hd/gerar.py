"""Gera a "MS Sans Serif" TrueType do modo alta definicao (DPI alta).

Em DPI alta a "MS Sans Serif" do jogo (fonte bitmap, sserife.fon) para no maior
tamanho que o arquivo tem e o texto sai pequeno e serrilhado; uma TrueType no
lugar (a Tahoma) e mais larga que o dobro e os rotulos e as abas nao cabem.
Esta fonte tem o desenho da Tahoma do Wine (LGPL) com a largura de cada letra
igual a da MS Sans Serif de 8 pontos (13 pontos de altura, letra de 11) na mesma
proporcao: em qualquer tamanho o texto ocupa o espaco que ocupava no original.
O negrito, que na MS Sans Serif e sintetico, tem 1 ponto a mais por letra.

A familia se chama "MS Sans Serif": em DPI alta o Wine nao acha o tamanho no
bitmap e usa esta. So pode estar instalada (Fonts do Windows) no modo alta
definicao: em 96 DPI o Wine tambem a prefere ao bitmap, e o jogo mudaria.
Tahoma: (c) Wine (LGPL 2.1); o nome e outro arquivo, o desenho e o mesmo.

Uso: python3 gerar.py PASTA_DAS_FONTES_DO_WINE   (share/wine/fonts: sserife.fon,
tahoma.ttf, tahomabd.ttf)
"""
import os, struct, sys
from fontTools.ttLib import TTFont
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.ttGlyphPen import TTGlyphPen


def larguras_fon(path, pontos=8):
    b = open(path, 'rb').read()
    ne = struct.unpack_from('<H', b, 0x3c)[0]
    rt = ne + struct.unpack_from('<H', b, ne + 0x24)[0]
    shift = struct.unpack_from('<H', b, rt)[0]
    p = rt + 2
    while True:
        tid, cnt = struct.unpack_from('<HH', b, p); p += 8
        if tid == 0:
            break
        for _ in range(cnt):
            off, ln = struct.unpack_from('<HH', b, p); p += 12
            if tid != 0x8008:
                continue
            f = b[off << shift:(off << shift) + (ln << shift)]
            if struct.unpack_from('<H', f, 0x44)[0] != pontos:
                continue
            ver = struct.unpack_from('<H', f, 0)[0]
            asc, il, h = (struct.unpack_from('<H', f, o)[0] for o in (0x4A, 0x4C, 0x58))
            first, last = f[0x5F], f[0x60]
            tab, es = (0x76, 4) if ver == 0x200 else (0x94, 6)
            w = {c: struct.unpack_from('<H', f, tab + (c - first) * es)[0] for c in range(first, last + 1)}
            return w, h, asc, il
    raise SystemExit('tamanho %d nao achado em %s' % (pontos, path))


def gerar(base, larg, h, asc, il, extra, saida, estilo):
    f = TTFont(base)
    upm = f['head'].unitsPerEm
    em_px = h - il                       # a "altura da letra" (Font.Height negativo)
    escala = upm / em_px                 # unidades por ponto do bitmap
    cmap = f.getBestCmap()
    gs = f.getGlyphSet()
    glyf, hmtx = f['glyf'], f['hmtx']
    feitos = set()
    for byte in range(32, 256):
        u = ord(bytes([byte]).decode('cp1252', errors='replace'))
        nome = cmap.get(u)
        if not nome or byte not in larg or nome in feitos:
            continue
        feitos.add(nome)
        novo = round((larg[byte] + extra) * escala)
        velho, lsb = hmtx[nome]
        g = glyf[nome]
        if velho > 0 and g.numberOfContours != 0:
            k = min(1.0, novo / velho)    # estreita quando nao cabe, nunca alarga
            pen = TTGlyphPen(gs)
            dx = (novo - velho * k) / 2
            gs[nome].draw(TransformPen(pen, (k, 0, 0, 1, dx, 0)))
            glyf[nome] = pen.glyph()
            glyf[nome].recalcBounds(glyf)
            hmtx[nome] = (novo, glyf[nome].xMin)
        else:
            hmtx[nome] = (novo, lsb)
    # alturas: celula de h pontos, subida asc, para letra de em_px pontos
    a, d = round(asc * escala), round((h - asc) * escala)
    os2, hhea = f['OS/2'], f['hhea']
    os2.usWinAscent, os2.usWinDescent = a, d
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = a, -d, 0
    hhea.ascent, hhea.descent, hhea.lineGap = a, -d, 0
    if 'VDMX' in f:
        del f['VDMX']
    for n in f['name'].names:
        if n.nameID in (1, 3, 4, 6, 16, 17):
            fam = 'MS Sans Serif'
            n.string = {1: fam, 16: fam, 3: fam + ' ' + estilo, 4: fam + ' ' + estilo,
                        6: 'MSSansSerif-' + estilo, 17: estilo}[n.nameID]
    f.save(saida)
    print(saida)


if __name__ == '__main__':
    pasta = sys.argv[1]
    aqui = os.path.dirname(os.path.abspath(__file__))
    larg, h, asc, il = larguras_fon(os.path.join(pasta, 'sserife.fon'))
    gerar(os.path.join(pasta, 'tahoma.ttf'), larg, h, asc, il, 0, os.path.join(aqui, 'MSSansSerifHD-Regular.ttf'), 'Regular')
    gerar(os.path.join(pasta, 'tahomabd.ttf'), larg, h, asc, il, 1, os.path.join(aqui, 'MSSansSerifHD-Bold.ttf'), 'Bold')
