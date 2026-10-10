#!/usr/bin/env python3
"""ELIFOOT.EXE (98.003 + 5-0-5): 10 equipas por divisao, 18 jornadas.

Experimental (scratch). Mudancas:
  - distribuicao inicial: i div 8 -> i div 10                       seg05:3BDB
  - calendario (TCalendar de cada equipa): 18 entradas, tabela 10x18 de
    adversarios (DS:17C8, no lugar das duas tabelas 8x14), mod 10    seg03:1D84..1DEA
  - ordem dos jogos da jornada (TTeam+22, 14 bytes): passa a guardar 18
    "lugares" de 4 bits (tabela 10x9 no fim do seg03, copiada com push cs)
    seg03:391A..392D; comparador seg11:1A87 chama um ajudante no fim do seg11
  - fim da epoca / taca / outros testes de jornada 14 -> 18
  - subidas/descidas: indices 6/4 -> 8/6 (continuam 2 sobem, 2 descem)
  - minimo de equipas na seleccao de paises 34 -> 42
  - Classificacao: 10 linhas por divisao (codigo + DFM)
  - salario pedido: escala por jornada/14 -> /18 (seg03:66C6)
uso: patch_10equipas.py ENTRADA SAIDA
"""
import struct, sys

N = 10            # equipas por divisao
R = 2 * (N - 1)   # jornadas
OPP_DS = 0x17C8   # tabela de adversarios no DS (era a tabela de lugares)


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


def main(entrada, saida):
    b = bytearray(open(entrada, 'rb').read())
    ne = NE(b)
    opp, slot = calendario()

    # --- tabela de adversarios no DS (180 bytes no lugar das 2 tabelas de 112) ---
    dsb = ne.seg(26)[0]
    assert b[dsb + 0x18A8: dsb + 0x18B5] == b'\x0cGuarda-redes'
    tab = bytes((v & 0xFF) for k in range(N) for v in opp[k])
    assert len(tab) <= 0x18A8 - OPP_DS
    b[dsb + OPP_DS: dsb + OPP_DS + len(tab)] = tab

    # --- tabela de lugares (4 bits por jornada) no fim do seg03 ---
    nib = bytearray()
    for k in range(N):
        row = bytearray((R + 1) // 2)
        for r in range(R):
            row[r // 2] |= slot[k][r] << (4 * (r % 2))
        nib += row
    stride = (R + 1) // 2
    slot_off = ne.append(3, nib)

    # seg03: calendario de cada equipa (seg03:1D20)
    ne.patch(3, 0x1D87, '6b7e0a0e', '6b7e0a%02x' % R)                 # imul di,[bp+A],R
    ne.patch(3, 0x1D8D, '8a8537188846fb8a46fb',
             bytes([0x8A, 0x85]) + struct.pack('<H', OPP_DS - 1) + bytes.fromhex('8846fb909090'))
    ne.patch(3, 0x1D9E, 'b90800', 'b9%02x00' % N)                        # mod N
    ne.patch(3, 0x1DE7, '837ef80e', '837ef8%02x' % R)                   # R entradas
    # seg03: copia dos lugares para TTeam+22 (seg03:38A9)
    ne.patch(3, 0x391A, '6bf80e81c7c8171e', '6bf8%02x81c7' % stride + struct.pack('<H', slot_off).hex() + '0e')
    ne.patch(3, 0x392C, '6a0e', '6a%02x' % stride)
    # seg03: troca de treinador na ultima jornada
    ne.patch(3, 0x53F3, '833e402b0e', '833e402b%02x' % R)
    # seg03: salario pedido, fator jornada/14
    ne.patch(3, 0x66C6, struct.pack('<f', 14.0), struct.pack('<f', float(R)))

    # seg05
    ne.patch(5, 0x0CF1, '833e402b0e', '833e402b%02x' % R)
    ne.patch(5, 0x0EEF, 'b80e00', 'b8%02x00' % R)                        # taca: 2^((R-j)/2)
    ne.patch(5, 0x11DC, '3d0e00', '3d%02x00' % R)
    ne.patch(5, 0x1224, '833e402b0e', '833e402b%02x' % R)
    for off in (0x098D, 0x09A3):                                          # 1a div: indice N-2
        ne.patch(5, off, '6a06', '6a%02x' % (N - 2))
    for off in (0x09B9, 0x09CF, 0x09E5, 0x09FB, 0x0A4B, 0x0A62):          # outras: N-4
        ne.patch(5, off, '6a04', '6a%02x' % (N - 4))
    ne.patch(5, 0x3BDB, 'b90800', 'b9%02x00' % N)                        # distribuicao inicial

    # seg06: minimo de equipas (4*N + 2) e linhas da classificacao
    minimo = 4 * N + 2
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
    aj = bytes.fromhex('8b1e402b' '4b' '88d9' 'd1eb' '268a4122' 'f6c101' '7403' 'c0e804' '240f' 'c3')
    aj_off = ne.append(11, aj)
    cmpcode = bytearray()
    for bpoff, depois in ((0xF6, '88c2'), (0xFA, '3ac2')):
        cmpcode += bytes([0xC4, 0x7E, bpoff]) + bytes.fromhex('26c43d') + b'\xe8'
        cmpcode += struct.pack('<h', aj_off - (0x1A87 + len(cmpcode) + 2))
        cmpcode += bytes.fromhex(depois)
    cmpcode += b'\x90' * (32 - len(cmpcode))
    ne.patch(11, 0x1A87, 'a1402bc47ef626c43d03f8268a5521a1402bc47efa26c43d03f8268a45213ac2', bytes(cmpcode))

    # DFM da Classificacao: 10 linhas (ItemHeight 16) por lista
    i = b.find(b'TPF0\x12TclassificationDlg')
    assert i > 0
    j = b.index(b'\x00\x00\x00\x00', i + 4) if False else None
    extra = 16 * (N - 8)
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

    struct.pack_into('<I', b, len(b) - 4, soma_do_jogo(b))
    open(saida, 'wb').write(b)
    print('ok: %d equipas/divisao, %d jornadas; lugares em seg03:%04X, ajudante seg11:%04X'
          % (N, R, slot_off, aj_off))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
