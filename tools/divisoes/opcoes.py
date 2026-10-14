"""Opcoes do jogo escolhidas no launcher (Configuracoes): cartoes amarelos,
patrocinadores... Cada patch registra os bytes que ligam ou desligam a sua adicao;
no fim, a tabela vai para dentro do EXE e o launcher, antes de abrir o jogo, poe nesses
bytes os valores da opcao escolhida e recalcula a soma da autoverificacao (o mesmo
EXE serve para todas as combinacoes).

Tabela (no fim do seg25, que o jogo nao executa la): "ELIFOPC1", quantidade (byte) e,
para cada entrada: nome (8 letras, completado com espacos), posicao no arquivo
(uint32), tamanho n (byte), n bytes com a opcao ligada, n bytes desligada.
"""
import struct

_entradas = []


def limpar():
    _entradas.clear()


def registrar(nome, seg, off, ligado, desligado):
    assert len(nome) <= 8 and len(ligado) == len(desligado)
    _entradas.append((nome, seg, off, bytes(ligado), bytes(desligado)))


def gravar_tabela(ne, b, seg=25):
    if not _entradas:
        return
    t = b'ELIFOPC1' + bytes([len(_entradas)])
    for nome, s, off, lig, des in _entradas:
        pos = ne.seg(s)[0] + off
        assert bytes(b[pos:pos + len(lig)]) == lig, (nome, s, hex(off))
        t += nome.ljust(8).encode() + struct.pack('<IB', pos, len(lig)) + lig + des
    ne.append(seg, t)
