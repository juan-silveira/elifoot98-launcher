#!/usr/bin/env python3
"""Gera o ELIFOOT.EXE com N equipes por divisao (N par, 8 a 20) e 2(N-1) jornadas.

Entrada: o game/ELIFOOT.EXE do projeto (98.003 com o patch do 5-0-5). Todos os
EXEs gerados tem limite de 30 jogadores por equipe (o jogo tinha 24 nas
compras e 20 no editor). Com N = 8 e so isso: o resto do jogo fica original.

Com N > 8 (detalhes e enderecos em docs/patch-divisoes.md):
  - calendario de Berger calculado na hora (calend.asm, nucleo.inc, no fim do
    seg03) em vez das tabelas 8 x 14 do DS; ordem dos jogos da jornada pelo
    ajudante do fim do seg11 (helper11.asm)
  - testes de ultima jornada, Taca, escala do salario pedido: 14 -> R
  - escada de forca B = D - 14 x posicao / (N - 1) (era D - 2 x posicao)
  - inflacao por jornada x 14 / R (a temporada anda o mesmo que com 14)
  - minimo de equipes no jogo novo: 4N + K
  - sobem e descem K por divisao: 2, ou 4 com N >= 16 (subida.asm)
  - tela da jornada com N + 2 linhas por pagina (2 divisoes por pagina)
  - Classificacao: listas de N linhas; com N > 14, em 2 paginas com setas
    (paginas.asm, paginas.py)
  - soma de verificacao recalculada

Precisa do nasm. uso: patch_divisoes.py N ENTRADA SAIDA
"""
from fractions import Fraction
import struct, sys

N = 20            # equipas por divisao (main troca)
R = 2 * (N - 1)   # jornadas
MAXJOG = 30       # jogadores por equipa (era 24 no jogo, 20 no editor)
LUG_DS = 0x17C8   # tabela de lugares no DS (4 bits por jornada)
from montar import montar
from cartoes import cartoes


def real10(x):
    """real de 10 bytes (x87 extended) de uma fracao positiva, arredondado."""
    e = 0
    while x >= 2: x /= 2; e += 1
    while x < 1: x *= 2; e -= 1
    m = round(x * 2 ** 63)
    if m == 2 ** 64: m //= 2; e += 1
    return m.to_bytes(8, 'little') + struct.pack('<H', e + 16383)


def soma_do_jogo(b):
    s = 0
    for i in range(len(b) - 4):
        s += b[i] * ((i % 6000) % 5 + 1)
    return s & 0xFFFFFFFF


def calendario():
    """Berger: devolve opp[k][r] (com sinal: + casa, - fora; adversario 0 vira N)
    e slot[k][r] (1..N; impar = mandante, par seguinte = visitante)."""
    pares = []
    m = N - 1
    for r in range(m):
        jogos = []
        a, b = r, N - 1
        jogos.append((a, b) if r % 2 == 0 else (b, a))
        for i in range(1, N // 2):
            x, y = (r + i) % m, (r - i) % m
            jogos.append((x, y) if i % 2 == 1 else (y, x))
        pares.append(jogos)
    pares += [[(y, x) for (x, y) in j] for j in pares]   # 2a volta espelhada
    opp = [[0] * R for _ in range(N)]
    slot = [[0] * R for _ in range(N)]
    for r, jogos in enumerate(pares):
        assert sorted(t for g in jogos for t in g) == list(range(N))
        for g, (h, a) in enumerate(jogos):
            opp[h][r] = a if a else N
            opp[a][r] = -(h if h else N)
            slot[h][r] = 2 * g + 1
            slot[a][r] = 2 * g + 2
    for k in range(N):
        assert sum(1 for v in opp[k] if v > 0) == N - 1
        assert sorted(abs(v) % N for v in opp[k]) == sorted([j for j in range(N) if j != k] * 2)
    return opp, slot


class NE:
    def __init__(self, b):
        self.b = b
        ne = struct.unpack_from('<H', b, 0x3C)[0]
        self.segtab = ne + struct.unpack_from('<H', b, ne + 0x22)[0]
        self.shift = struct.unpack_from('<H', b, ne + 0x32)[0]
        self.nseg = struct.unpack_from('<H', b, ne + 0x1C)[0]

    def ent(self, s):
        return self.segtab + 8 * (s - 1)

    def seg(self, s):
        off, ln, fl, mn = struct.unpack_from('<HHHH', self.b, self.ent(s))
        return off << self.shift, ln, fl, mn

    def relocs(self, s):
        base, ln, fl, mn = self.seg(s)
        n = struct.unpack_from('<H', self.b, base + ln)[0]
        locs = set()
        for r in range(n):
            st, rf, roff = struct.unpack_from('<BBH', self.b, base + ln + 2 + 8 * r)
            if rf & 4:
                locs.add(roff); continue
            pos = roff
            while pos != 0xFFFF:
                locs.add(pos)
                pos = struct.unpack_from('<H', self.b, base + pos)[0]
        return locs

    def append(self, s, data):
        """acrescenta bytes no fim do segmento (empurra a tabela de relocacoes)."""
        base, ln, fl, mn = self.seg(s)
        n = struct.unpack_from('<H', self.b, base + ln)[0]
        rel = bytes(self.b[base + ln: base + ln + 2 + 8 * n])
        nxt = min((struct.unpack_from('<H', self.b, self.segtab + 8 * i)[0] << self.shift)
                  for i in range(self.nseg)
                  if (struct.unpack_from('<H', self.b, self.segtab + 8 * i)[0] << self.shift) > base)
        fim = base + ln + len(data) + len(rel)
        if fim > nxt:
            sys.exit('sem espaco no seg%02d' % s)
        self.b[base + ln: fim] = bytes(data) + rel
        struct.pack_into('<HH', self.b, self.ent(s) + 2, ln + len(data), fl)
        struct.pack_into('<H', self.b, self.ent(s) + 6, mn + len(data) if mn else 0)
        return ln   # deslocamento do que foi acrescentado

    def patch(self, s, off, old, new):
        base = self.seg(s)[0]
        old = bytes.fromhex(old) if isinstance(old, str) else old
        new = bytes.fromhex(new) if isinstance(new, str) else new
        assert len(old) == len(new), (s, hex(off))
        cur = bytes(self.b[base + off: base + off + len(old)])
        if cur != old:
            sys.exit('seg%02d:%04X esperado %s, achado %s' % (s, off, old.hex(), cur.hex()))
        rl = self.relocs(s)
        for i in range(len(old)):
            if old[i] != new[i] and off + i in rl:
                sys.exit('seg%02d:%04X tem relocacao' % (s, off + i))
        self.b[base + off: base + off + len(new)] = new


def limite_jogadores(ne):
    """jogadores por equipa: 24 nas compras e leiloes, 20 no editor do jogo; o
    computador vende mais a partir de 21 (3 abaixo do maximo)."""
    ne.patch(3, 0x635A, '3d1800', '3d%02x00' % MAXJOG)                  # jogadores + compras < max
    ne.patch(3, 0x3B51, '2680bd880015', '2680bd8800%02x' % (MAXJOG - 3))  # computador vende mais
    ne.patch(1, 0x13B3, '26837d0814', '26837d08%02x' % MAXJOG)          # editor: Adicionar
    ne.patch(12, 0x27FA, '837ef814', '837ef8%02x' % MAXJOG)             # editor: validacao


def gravar(b, saida):
    struct.pack_into('<I', b, len(b) - 4, soma_do_jogo(b))
    open(saida, 'wb').write(b)


def main(n, entrada, saida):
    global N, R, K
    N, R = n, 2 * (n - 1)
    K = 4 if N >= 16 else 2   # quantas sobem e descem em cada divisao
    if N % 2 or not 8 <= N <= 20:
        sys.exit('N tem de ser par, de 8 a 20')
    b = bytearray(open(entrada, 'rb').read())
    ne = NE(b)
    limite_jogadores(ne)
    if N == 8:                # o jogo original, so com o limite de jogadores e os amarelos
        cartoes(ne, b)
        gravar(b, saida)
        print('ok: 8 equipas/divisao (original), ate %d jogadores' % MAXJOG)
        return
    opp, slot = calendario()

    # --- lugares: a equipa guarda em +0x22 so o numero dela na divisao (o seg11
    #     calcula o lugar na hora); o buffer de 1 byte fica no DS antigo das tabelas
    dsb = ne.seg(26)[0]
    assert b[dsb + 0x18A8: dsb + 0x18B5] == b'\x0cGuarda-redes'

    # --- adversario calculado na hora (calend.asm) no fim do seg03 ---
    org = ne.seg(3)[1]
    cod, rot = montar('calend.asm', org, N=N, ORG='0x%X' % org)
    assert ne.append(3, cod) == org
    # seg03:1D84: al = adversario(jornada [bp-8], equipa [bp+A])
    ne.patch(3, 0x1D84, '8b46f86b7e0a0e03f88a8537188846fb',
             bytes.fromhex('8b46f8' '8b5e0a') + b'\xe8' + struct.pack('<H', (rot['adversario'] - (0x1D84 + 9)) & 0xFFFF)
             + bytes.fromhex('8846fb') + b'\x90' * 4)
    ne.patch(3, 0x1D9E, 'b90800', 'b9%02x00' % N)                        # mod N
    ne.patch(3, 0x1DE7, '837ef80e', '837ef8%02x' % R)                   # R entradas
    # seg03:391A: [DS:17C8] = numero da equipa na divisao; copia 1 byte para +0x22
    ne.patch(3, 0x391A, '6bf80e81c7c8171e', 'a2c817bfc817901e')
    ne.patch(3, 0x392C, '6a0e', '6a01')
    # seg03:6FED: escada de forca dx = 14*posicao/(N-1)
    ne.patch(3, 0x6FED, '8b46fed1e08bd0', b'\xe8' + struct.pack('<H', (rot['escada'] - (0x6FED + 3)) & 0xFFFF) + b'\x90' * 4)
    # seg03: troca de treinador na ultima jornada
    ne.patch(3, 0x53F3, '833e402b0e', '833e402b%02x' % R)
    # seg03: salario pedido, fator jornada/14
    ne.patch(3, 0x66C6, struct.pack('<f', 14.0), struct.pack('<f', float(R)))

    # tela da jornada: P linhas por pagina (cabecalhos das divisoes contam como
    # linha). Era 20 = 4 x (1 + 4); com N + 2 cabem 2 divisoes inteiras por pagina
    if N > 8:
        P = N + 2
        ne.patch(3, 0x9F22, 'b91400', 'b9%02x00' % P)                    # pagina do jogo
        ne.patch(3, 0x9F43, '6b06382b14', '6b06382b%02x' % P)            # linha na pagina
        ne.patch(10, 0x05AD, '6a146b06382b14', '6a%02x6b06382b%02x' % (P, P))   # moldura
        ne.patch(10, 0x09C4, '6b06382b14', '6b06382b%02x' % P)           # clique do rato
        ne.patch(10, 0x0B17, 'b91400', 'b9%02x00' % P)                   # numero de paginas
        # moldura azul: altura = (linha) x P - 6 em vez de (linha) x total de linhas
        # da jornada (que passava do fundo da tela e escondia a borda de baixo)
        ne.patch(10, 0x0598, 'c43e0a2726c43d26f76508', 'b9%02x00f7e12d0600909090' % P)

    # seg05: inflacao muda a cada jornada por 0.01 x (sorteios); com R jornadas o
    # passo vira 0.01 x 14 / R, para a temporada andar o mesmo que com 14
    ne.patch(5, 0x0C2F, real10(Fraction(1, 100)), real10(Fraction(14, 100 * R)))

    # seg05
    ne.patch(5, 0x0CF1, '833e402b0e', '833e402b%02x' % R)
    ne.patch(5, 0x0EEF, 'b80e00', 'b8%02x00' % R)                        # taca: 2^((R-j)/2)
    ne.patch(5, 0x11DC, '3d0e00', '3d%02x00' % R)
    ne.patch(5, 0x1224, '833e402b0e', '833e402b%02x' % R)
    if K == 2:
        for off in (0x098D, 0x09A3):                                      # 1a div: indice N-2
            ne.patch(5, off, '6a06', '6a%02x' % (N - 2))
        for off in (0x09B9, 0x09CF, 0x09E5, 0x09FB, 0x0A4B, 0x0A62):      # outras: N-4
            ne.patch(5, off, '6a04', '6a%02x' % (N - 4))
    else:                                                                 # K sobem e descem (subida.asm)
        import subida
        subida.subir(ne, b, N, K)
    ne.patch(5, 0x3BDB, 'b90800', 'b9%02x00' % N)                        # distribuicao inicial

    # seg06: minimo de equipas (4*N + 2) e linhas da classificacao
    minimo = 4 * N + K
    for off in (0x1818, 0x185C):
        ne.patch(6, off, '833e582822', '833e5828%02x' % minimo)
    for off in (0x18D8, 0x1B11):
        ne.patch(6, off, '833e5a2822', '833e5a28%02x' % minimo)
    for off in (0x1836, 0x187A):
        ne.patch(6, off, '006a22', '006a%02x' % minimo)
    ne.patch(6, 0x07D8, '837efe08', '837efe%02x' % N)

    # seg11: outro teste de jornada 14
    ne.patch(11, 0x0DA2, '833e402b0e', '833e402b%02x' % R)
    # seg11: ajudante (lugar de 4 bits da jornada actual) + comparador
    org11 = ne.seg(11)[1]
    aj, _ = montar('helper11.asm', org11, N=N, ORG='0x%X' % org11)
    aj_off = ne.append(11, aj)
    cmpcode = bytearray()
    for bpoff, depois in ((0xF6, '88c2'), (0xFA, '3ac2')):
        cmpcode += bytes([0xC4, 0x7E, bpoff]) + bytes.fromhex('26c43d') + b'\xe8'
        cmpcode += struct.pack('<h', aj_off - (0x1A87 + len(cmpcode) + 2))
        cmpcode += bytes.fromhex(depois)
    cmpcode += b'\x90' * (32 - len(cmpcode))
    ne.patch(11, 0x1A87, 'a1402bc47ef626c43d03f8268a5521a1402bc47efa26c43d03f8268a45213ac2', bytes(cmpcode))

    if N > 14:
        # Classificacao em 2 paginas (1a+2a / 3a+4a) com setas: paginas.py
        import paginas
        cartoes(ne, b)
        print('paginas:', {k: '%04X' % v for k, v in paginas.paginar(ne, b, N).items()})
        gravar(b, saida)
        return
    # DFM da Classificacao: 10 linhas (ItemHeight 16) por lista
    i = b.find(b'TPF0\x12TclassificationDlg')
    assert i > 0
    j = b.index(b'\x00\x00\x00\x00', i + 4) if False else None
    extra = 16 * (min(N, 14) - 8)   # com mais de 14 equipas a lista rola
    def dfm(nome, prop, velho, novo):
        k = b.index(nome, i)
        p = b.index(bytes([len(prop)]) + prop + b'\x03', k)
        assert struct.unpack_from('<h', b, p + len(prop) + 2)[0] == velho, (nome, prop)
        struct.pack_into('<h', b, p + len(prop) + 2, novo)
    dfm(b'TclassificationDlg', b'ClientHeight', 371, 371 + 2 * extra)
    for lb in (b'\x10ListBoxDivision1', b'\x10ListBoxDivision2', b'\x10ListBoxDivision3', b'\x10ListBoxDivision4'):
        dfm(lb, b'Height', 130, 130 + extra)
    dfm(b'\x06Label2', b'Top', 193, 193 + extra)
    dfm(b'\x06Label4', b'Top', 193, 193 + extra)
    dfm(b'\x10ListBoxDivision2', b'Top', 217, 217 + extra)
    dfm(b'\x10ListBoxDivision4', b'Top', 217, 217 + extra)

    cartoes(ne, b)
    gravar(b, saida)
    print('ok: %d equipas/divisao, %d jornadas; rotinas seg03 %s, ajudante seg11:%04X'
          % (N, R, {k: '%04X' % v for k, v in rot.items()}, aj_off))


if __name__ == '__main__':
    main(int(sys.argv[1]), sys.argv[2], sys.argv[3])
