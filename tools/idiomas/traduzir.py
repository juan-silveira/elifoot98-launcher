"""Traduz os textos de um ELIFnn.EXE por um arquivo de idioma (game/IDIOMAS/*.txt).

uso: traduzir.py IDIOMA.txt ENTRADA.EXE SAIDA.EXE

O arquivo de idioma tem uma linha por texto: "original = traducao", com o texto
inteiro, exatamente como esta no jogo. Espacos no comeco ou no fim contam: nesse
caso o texto vai entre aspas ("A equipa " = "O time "). Entre aspas valem \\t (TAB),
\\r, \\n e \\xNN, para os textos que os tem ("Bilhetes\\t" = "Ingressos\\t"). Linhas
com # sao comentarios. So os textos listados mudam; nomes de arquivo, chaves do
elif98.ini e nomes de componentes nunca entram.

Onde os textos ficam no EXE:
- nos formularios (DFM): titulos, rotulos, botoes, menus, abas e listas. A
  traducao pode ter outro tamanho, desde que o formulario caiba no enchimento
  que o recurso ja tinha;
- no codigo: cada texto constante e passado por "mov di, endereco; push cs;
  push di" (no segmento de dados, "push ds"). Cada um tem uma so referencia.
  Uma traducao do mesmo tamanho ou menor fica no lugar; uma maior vai para o
  fim do segmento (se houver espaco no arquivo) e a referencia passa a apontar
  para ela. Sem espaco, os textos colados (um bloco de textos seguidos, cada um
  com a sua referencia, como as linhas do Balanco) sao reescritos juntos: as
  traducoes podem crescer umas a custa das outras, se o bloco todo couber;
- em tabelas do segmento de dados (posicoes, comportamentos, mensagens), com
  tamanho fixo: a traducao tem de caber no espaco ate o texto seguinte.

No fim, lista as traducoes que nao couberam e as que nao foram achadas.
"""
import os
import re
import struct
import sys

AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(AQUI, '..', 'divisoes'))
from patch_divisoes import NE, gravar                          # noqa: E402
from paginas import ler_dfm, escrever_dfm                      # noqa: E402

DADOS = 26                    # segmento de dados (DS)


def _texto(t):
    """Tira as aspas e, entre elas, troca \\t, \\r, \\n e \\xNN pelos caracteres."""
    if len(t) >= 2 and t[0] == t[-1] == '"':
        return re.sub(r'\\(t|r|n|x[0-9A-Fa-f]{2})', lambda m: {'t': '\t', 'r': '\r', 'n': '\n'}.get(
            m.group(1), chr(int(m.group(1)[1:], 16)) if m.group(1)[0] == 'x' else ''), t[1:-1])
    return t


def ler_idioma(caminho):
    mapa = {}
    for n, linha in enumerate(open(caminho, encoding='utf-8'), 1):
        linha = linha.rstrip('\n')
        if not linha.strip() or linha.lstrip().startswith('#'):
            continue
        if ' = ' not in linha:
            sys.exit('%s:%d: falta " = "' % (caminho, n))
        a, b = linha.split(' = ', 1)
        a, b = a.strip(), b.strip()
        a, b = _texto(a), _texto(b)
        mapa[a.encode('latin1')] = b.encode('latin1')
    return mapa


def _formularios(b, mapa, usados, falhas):
    for m in list(re.finditer(rb'TPF0', b)):
        i = m.start()
        try:
            form, fim = ler_dfm(b, i + 4)
        except Exception:
            continue
        mudou = False

        def walk(c):
            nonlocal mudou
            for pv in c['props']:
                v = pv[1]
                if v[:1] == b'\x06' and bytes(v[2:]) in mapa:
                    usados.add(v[2:])
                    pv[1] = b'\x06' + bytes([len(mapa[v[2:]])]) + mapa[v[2:]]
                    mudou = True
                elif v[:1] == b'\x01':                     # lista de textos
                    p, novo = 1, b'\x01'
                    while v[p] == 6:
                        t = v[p + 2:p + 2 + v[p + 1]]
                        if t in mapa:
                            usados.add(t)
                            t = mapa[t]
                            mudou = True
                        novo += b'\x06' + bytes([len(t)]) + t
                        p += 2 + v[p + 1]
                    if v[p] == 0 and p == len(v) - 1:
                        pv[1] = novo + b'\0'
            for f in c['filhos']:
                walk(f)

        walk(form)
        if not mudou:
            continue
        livre = fim
        while b[livre] == 0 and livre - i < 0x10000 and (livre & 0xFF):
            livre += 1
        novo = b'TPF0' + escrever_dfm(form)
        if i + len(novo) > livre:
            falhas.append('formulario %s: %d bytes a mais' % (form['cls'], i + len(novo) - livre))
            continue
        b[i:livre] = novo + b'\0' * (livre - i - len(novo))


def _capacidade_dados(seg, off):
    """Espaco de um texto numa tabela do segmento de dados: ate o texto seguinte."""
    n = seg[off]
    p = off + 1 + n
    while p < len(seg) and seg[p] == 0 and p - off < 256:
        p += 1
    return p - off - 1


def _codigo(ne, b, mapa, usados, falhas):
    dbase, dln = ne.seg(DADOS)[:2]
    dados_feitos = set()
    for s in range(1, ne.nseg + 1):
        base, ln = ne.seg(s)[:2]
        seg = bytes(b[base:base + ln])
        novos = []                                # (referencia, texto) para o fim do segmento
        for m in re.finditer(rb'\xbf(..)([\x0e\x1e])\x57', seg, re.S):
            off = int.from_bytes(m.group(1), 'little')
            if m.group(2) == b'\x1e':             # texto no segmento de dados
                if s == DADOS or off in dados_feitos:
                    continue
                t = bytes(b[dbase + off + 1: dbase + off + 1 + b[dbase + off]]) if off < dln else b''
                if t in mapa:
                    usados.add(t)
                    dados_feitos.add(off)
                    novo = mapa[t]
                    cap = _capacidade_dados(b[dbase:dbase + dln], off)
                    if len(novo) > cap:
                        falhas.append('%r: cabem %d letras (dados:%04X)' % (t.decode('latin1'), cap, off))
                        continue
                    b[dbase + off: dbase + off + 1 + len(novo)] = bytes([len(novo)]) + novo
                continue
            if off >= ln:
                continue
            t = seg[off + 1: off + 1 + seg[off]]
            if t not in mapa:
                continue
            usados.add(t)
            novo = mapa[t]
            if len(novo) <= len(t):
                b[base + off: base + off + 1 + len(novo)] = bytes([len(novo)]) + novo
            else:
                novos.append((m.start() + 1, novo, t))
        if novos:
            novos = _blocos(b, base, seg, refs_do_segmento(seg, ln), novos, mapa)
        if novos:
            dados = b''.join(bytes([len(n)]) + n for _, n, _ in novos)
            proximo = min(o for o in (ne.seg(x)[0] for x in range(1, ne.nseg + 1)) if o > base)
            nrel = struct.unpack_from('<H', b, base + ln)[0]
            if base + ln + len(dados) + 2 + 8 * nrel > proximo:
                for _, n, t in novos:
                    falhas.append('%r -> %r: sem espaco no seg%02d' % (t.decode('latin1'), n.decode('latin1'), s))
                continue
            org = ne.append(s, dados)
            for ref, n, _ in novos:
                struct.pack_into('<H', b, base + ref, org)
                org += 1 + len(n)
    # tabelas do segmento de dados (sem referencia direta no codigo)
    seg = bytes(b[dbase:dbase + dln])
    i = 0
    while i < len(seg) - 2:
        n = seg[i]
        t = seg[i + 1:i + 1 + n]
        if n >= 2 and t in mapa and i not in dados_feitos and re.fullmatch(rb"[\x20-\x7e\xa0-\xff]+", t):
            usados.add(t)
            novo = mapa[t]
            cap = _capacidade_dados(seg, i)
            if len(novo) > cap:
                falhas.append('%r: cabem %d letras (dados:%04X)' % (t.decode('latin1'), cap, i))
            else:
                b[dbase + i: dbase + i + 1 + len(novo)] = bytes([len(novo)]) + novo
            i += 1 + n
            continue
        i += 1


def refs_do_segmento(seg, ln):
    """Endereco de cada texto do codigo -> posicoes das referencias (mov di, endereco)."""
    refs = {}
    for m in re.finditer(rb'\xbf(..)\x0e\x57', seg, re.S):
        off = int.from_bytes(m.group(1), 'little')
        if off < ln:
            refs.setdefault(off, []).append(m.start() + 1)
    return refs


def _blocos(b, base, seg, refs, novos, mapa):
    """Tenta acomodar as traducoes maiores reescrevendo o bloco de textos colados em
    que cada uma esta. Devolve as que continuam sem lugar."""
    sobram = []
    feitos = set()
    for ref, novo, original in novos:
        off = int.from_bytes(seg[ref:ref + 2], 'little')
        if off in feitos:
            continue
        # o bloco: textos seguidos, cada um com uma so referencia
        ini = off
        while True:
            ant = [o for o in refs if o < ini and o + 1 + seg[o] == ini and len(refs[o]) == 1]
            if not ant:
                break
            ini = ant[0]
        textos, p = [], ini
        while p in refs and len(refs[p]) == 1:
            textos.append(p)
            p += 1 + seg[p]
        fim = p
        if off not in textos:
            sobram.append((ref, novo, original))
            continue
        trad = []
        for o in textos:
            t = bytes(seg[o + 1:o + 1 + seg[o]])
            trad.append(mapa.get(t, t))
        if sum(1 + len(t) for t in trad) > fim - ini:
            sobram.append((ref, novo, original))
            continue
        p = ini
        for o, t in zip(textos, trad):
            b[base + p: base + p + 1 + len(t)] = bytes([len(t)]) + t
            struct.pack_into('<H', b, base + refs[o][0], p)
            p += 1 + len(t)
        feitos.update(textos)
    return sobram


def traduzir(b, mapa):
    """Traduz o EXE em b (bytearray). Devolve (falhas, nao achados)."""
    ne = NE(b)
    usados, falhas = set(), []
    _codigo(ne, b, mapa, usados, falhas)
    _formularios(b, mapa, usados, falhas)
    return falhas, sorted(t.decode('latin1') for t in set(mapa) - usados)


if __name__ == '__main__':
    idioma, entrada, saida = sys.argv[1:4]
    b = bytearray(open(entrada, 'rb').read())
    falhas, faltam = traduzir(b, ler_idioma(idioma))
    gravar(b, saida)          # com a soma da autoverificacao do jogo recalculada
    for f in falhas:
        print('NAO COUBE:', f)
    for t in faltam:
        print('NAO ACHADO:', repr(t))
