"""Gera o game/JOGO.DIF: as diferencas que o launcher aplica ao game/ELIFOOT.EXE para montar,
na hora de abrir, o EXE das Configuracoes (equipes por divisao e idioma). O pacote nao
precisa trazer os 14 ELIFnn[YY].EXE, so o original e este arquivo.

  MOD     ELIFOOT.EXE -> ELIF08.EXE (a versao modificada)
  Nnn     ELIF08.EXE  -> ELIFnn.EXE (10 a 20 equipes)
  YY      ELIF08.EXE  -> ELIF08YY.EXE (idioma; serve para todos os nn)
  98YY    ELIFOOT.EXE -> ELIF98YY.EXE (idioma na versao original)

Formato: "ELIFDIF1", quantidade (byte) e, para cada parte: nome (8 letras, completado com
espacos), tamanho do EXE resultante (uint32), quantidade de trechos (uint32) e os trechos
(posicao uint32, tamanho uint32, bytes). Um EXE maior que a base ganha zeros antes dos
trechos. Os 4 ultimos bytes (a soma da autoverificacao) ficam fora: o launcher recalcula.

uso: python3 tools/divisoes/diferencas.py   (depois de patch_divisoes.py e idiomas/gerar.py)
Confere que cada combinacao montada da byte a byte o EXE gerado.
"""
import glob
import os
import struct

JOGO = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'game')
FOLGA = 8          # bytes iguais que ainda ficam dentro do mesmo trecho


def ler(nome):
    return open(os.path.join(JOGO, nome), 'rb').read()


def soma(b):
    s = 0
    for i in range(len(b) - 4):
        s += b[i] * ((i % 6000) % 5 + 1)
    return s & 0xFFFFFFFF


def trechos(base, alvo):
    base = base[:len(alvo)] + bytes(max(0, len(alvo) - len(base)))
    fim, r, i = len(alvo) - 4, [], 0
    while i < fim:
        if alvo[i] == base[i]:
            i += 1
            continue
        j = ultimo = i
        while j < fim and j - ultimo <= FOLGA:
            if alvo[j] != base[j]:
                ultimo = j
            j += 1
        r.append((i, alvo[i:ultimo + 1]))
        i = ultimo + 1
    return r


def aplicar(b, parte):
    tam, ts = parte
    b = bytearray(b[:tam]) + bytes(max(0, tam - len(b)))
    for pos, dados in ts:
        b[pos:pos + len(dados)] = dados
    return b


def fechar(b):
    b = bytearray(b)
    struct.pack_into('<I', b, len(b) - 4, soma(b))
    return bytes(b)


def main():
    orig, m08 = ler('ELIFOOT.EXE'), ler('ELIF08.EXE')
    partes = {'MOD': (len(m08), trechos(orig, m08))}
    for n in range(10, 21, 2):
        e = ler('ELIF%02d.EXE' % n)
        partes['N%02d' % n] = (len(e), trechos(m08, e))
    idiomas = sorted(os.path.basename(f)[:-4].split('-')[-1].upper()
                     for f in glob.glob(os.path.join(JOGO, 'IDIOMAS', '*.txt')))
    for yy in idiomas:
        e = ler('ELIF08%s.EXE' % yy)
        partes[yy] = (len(e), trechos(m08, e))
        e = ler('ELIF98%s.EXE' % yy)
        partes['98' + yy] = (len(e), trechos(orig, e))

    # confere todas as combinacoes
    for yy in [''] + idiomas:
        alvo = ler('ELIF98%s.EXE' % yy) if yy else orig
        b = aplicar(orig, partes['98' + yy]) if yy else orig
        assert fechar(b) == alvo, '98' + yy
        for n in range(8, 21, 2):
            b = aplicar(orig, partes['MOD'])
            if n > 8:
                b = aplicar(b, partes['N%02d' % n])
            if yy:
                b = aplicar(b, partes[yy])
            assert fechar(b) == ler('ELIF%02d%s.EXE' % (n, yy)), (n, yy)

    out = bytearray(b'ELIFDIF1' + bytes([len(partes)]))
    for nome, (tam, ts) in partes.items():
        out += nome.ljust(8).encode('ascii') + struct.pack('<II', tam, len(ts))
        for pos, dados in ts:
            out += struct.pack('<II', pos, len(dados)) + dados
        print('%-5s %4d trechos %7d bytes' % (nome, len(ts), sum(len(d) for _, d in ts)))
    open(os.path.join(JOGO, 'JOGO.DIF'), 'wb').write(out)
    print('JOGO.DIF: %d bytes' % len(out))


if __name__ == '__main__':
    main()
