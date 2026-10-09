#!/usr/bin/env python3
"""Adiciona a tatica 5-0-5 (tecla T) ao menu Seleccionar do ELIFOOT.EXE.

As 12 taticas originais continuam iguais. O que muda:
  - o formulario TprepareGameDlg (recurso DFM) ganha o item "5-0-5" com atalho T,
    ligado ao mesmo evento do 6-4-0 (MenuSelect640Click);
  - o MenuSelect640Click (seg04:30FF) salta para um codigo novo no fim do seg04
    que olha quem chamou (Sender): se foi o item 6-4-0 (campo Self+0x1A8), escala
    6-4-0 como antes; senao, escala 5-0-5. A escalacao e a rotina do proprio jogo
    (seg04:2E12 com 1 G e os D, M, A, depois seg04:38B0), a mesma das teclas F1-F12.

O codigo novo usa so chamadas "push cs / call near" dentro do seg04, sem
relocacoes novas; a tabela de relocacoes do seg04 e empurrada para depois dele
(ha 153 bytes livres antes do seg05). O DFM cresce dentro da folga do recurso.

O jogo confere a si mesmo ao abrir (seg12:39D9 -> seg12:3825): os 4 ultimos
bytes do EXE guardam uma soma do resto do arquivo (blocos de 6000 bytes,
byte x (posicao no bloco mod 5 + 1)); com qualquer byte mudado ele trava antes da
tela "Acerca". O patch recalcula essa soma.

Funciona no 98.002 e no 98.003 (os enderecos usados sao os mesmos nas duas
versoes). O game/ELIFOOT.EXE do projeto e o 98.003 com este patch.

uso: patch_505.py ELIFOOT.EXE   (altera o arquivo; recusa se nao for o original)
"""
import struct
import sys

SEG = 4                  # segmento com as rotinas de tatica
HANDLER_640 = 0x3109     # MenuSelect640Click, logo depois do prologo (push bp/mov bp,sp/stack check)
ESCALAR = 0x2E12         # escala com (G, D, M, A, 1, 1) e Self
REDESENHAR = 0x38B0      # atualiza a lista de jogadores
CAMPO_640 = 0x1A8        # Self.MenuSelect640


def soma_do_jogo(b):
    """Soma que o jogo confere em seg12:3825 (sobre tudo menos os 4 ultimos bytes)."""
    s = 0
    n = len(b) - 4
    for i in range(n):
        s += b[i] * ((i % 6000) % 5 + 1)
    return s & 0xFFFFFFFF


def main(caminho):
    b = bytearray(open(caminho, 'rb').read())
    ne = struct.unpack_from('<H', b, 0x3C)[0]
    assert b[ne:ne + 2] == b'NE'
    segtab = ne + struct.unpack_from('<H', b, ne + 0x22)[0]
    shift = struct.unpack_from('<H', b, ne + 0x32)[0]
    ent = segtab + 8 * (SEG - 1)
    off, ln, fl, mn = struct.unpack_from('<HHHH', b, ent)
    base = off << shift

    original = bytes.fromhex('6a016a066a046a006a016a01')
    if b[base + HANDLER_640:base + HANDLER_640 + 12] != original:
        if b[base + HANDLER_640] == 0xE9:
            sys.exit('ja aplicado')
        sys.exit('ELIFOOT.EXE diferente do esperado (seg04:3109)')

    # --- codigo novo no fim do seg04 ---
    cave = ln
    c = bytearray()
    def rel16(origem_fim, destino):
        return struct.pack('<h', destino - origem_fim)
    c += bytes.fromhex('c47e06')                                  # les di,[bp+6]   (Self)
    c += bytes.fromhex('268b85') + struct.pack('<H', CAMPO_640)   # mov ax,[es:di+1A8]
    c += bytes.fromhex('3b460a')                                  # cmp ax,[bp+0A]  (Sender)
    j1 = len(c); c += b'\x75\x00'                                 # jne 505
    c += bytes.fromhex('268b85') + struct.pack('<H', CAMPO_640 + 2)
    c += bytes.fromhex('3b460c')
    j2 = len(c); c += b'\x75\x00'
    c += original                                                  # 6-4-0
    j3 = len(c); c += b'\xeb\x00'
    t505 = len(c)
    c += bytes.fromhex('6a016a056a006a056a016a01')                # 5-0-5
    comum = len(c)
    c[j1 + 1] = t505 - (j1 + 2)
    c[j2 + 1] = t505 - (j2 + 2)
    c[j3 + 1] = comum - (j3 + 2)
    c += bytes.fromhex('c47e06' '06' '57' '0e' 'e8')               # les di,[bp+6]; push es; push di; push cs; call
    c += rel16(cave + len(c) + 2, ESCALAR)
    c += bytes.fromhex('c47e06' '06' '57' '0e' 'e8')
    c += rel16(cave + len(c) + 2, REDESENHAR)
    c += bytes.fromhex('c9' 'ca0800')                               # leave; retf 8

    # empurra a tabela de relocacoes (logo depois dos dados do segmento)
    nrel = struct.unpack_from('<H', b, base + ln)[0]
    rel = bytes(b[base + ln: base + ln + 2 + nrel * 8])
    fim = base + ln + len(c) + len(rel)
    proximo = min((struct.unpack_from('<H', b, segtab + 8 * i)[0] << shift)
                  for i in range(struct.unpack_from('<H', b, ne + 0x1C)[0])
                  if (struct.unpack_from('<H', b, segtab + 8 * i)[0] << shift) > base)
    if fim > proximo:
        sys.exit('sem espaco no seg04')
    b[base + ln: fim] = bytes(c) + rel
    struct.pack_into('<HH', b, ent + 2, ln + len(c), fl)
    struct.pack_into('<H', b, ent + 6, mn + len(c) if mn else 0)

    # salto do MenuSelect640Click para o codigo novo
    j = base + HANDLER_640
    b[j] = 0xE9
    b[j + 1:j + 3] = rel16(HANDLER_640 + 3, cave)

    # --- item novo no DFM, logo depois do 6-4-0 ---
    i = b.find(b'TPF0\x0fTprepareGameDlg')
    assert i > 0
    fim640 = b.index(b'\x0cShortCutText\x06\x03F12\x00\x00', i) + len(b'\x0cShortCutText\x06\x03F12\x00\x00')
    def s(t):
        return bytes([len(t)]) + t
    item = (s(b'TMenuItem') + s(b'MenuSelect505') +
            s(b'Caption') + b'\x06' + s(b'5-0-5') +
            s(b'OnClick') + b'\x07' + s(b'MenuSelect640Click') +
            s(b'ShortCutText') + b'\x06' + s(b'T') + b'\x00\x00')
    dfm_fim = i + len(bytes(b[i:i + 0x5F00]).rstrip(b'\0'))
    if dfm_fim + len(item) > i + 0x5F00:
        sys.exit('sem espaco no DFM')
    b[fim640:dfm_fim + len(item)] = item + bytes(b[fim640:dfm_fim])

    struct.pack_into('<I', b, len(b) - 4, soma_do_jogo(b))
    open(caminho, 'wb').write(b)
    print('5-0-5 (T) adicionado: %d bytes de codigo, %d no DFM' % (len(c), len(item)))


if __name__ == '__main__':
    main(sys.argv[1])
