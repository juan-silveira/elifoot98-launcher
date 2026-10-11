"""Linha "Cartoes amarelos" no Historial (historial.asm), nas 3 janelas que o
mostram: venda (seg02, sellPlayerDlg), aba Jogador da equipa (seg04,
prepareGameDlg) e plantel (seg10, PlantelDlg). Usado depois de cartoes().

No DFM: o texto fixo "Cartoes vermelhos" passa a chamar-se "Vermelhos" (sem
campo) e o campo dele no formulario fica para o numero novo; entram o texto
"Cartoes amarelos" e esse numero, e as 5 linhas ficam a 16 pontos uma da outra.
O DFM cresce dentro do espaco que o recurso ja tinha (o tamanho na tabela de
recursos inclui o enchimento).
"""
from montar import montar
from paginas import (_tabela, _gravar_tabela, ler_dfm, escrever_dfm, por,
                     inteiro, ident, texto, FALSO)

INTTOSTR, SETTEXT = (24, 0x07D0), (21, 0x1D8C)

# seg, regiao, [bp-TEMP], jogador, campos (golos, golos, amarelos, vermelhos, lesoes),
# formulario, caixa, texto fixo dos vermelhos, 1a linha
JANELAS = [
    (2, 0x32BF, 0x3363, 0x20C, 0x26DA, (0x20C, 0x1F8, 0x1F0, 0x1FC, 0x200),
     'TsellPlayerDlg', 'GroupBox2', 'Label19', 16),
    (4, 0x246F, 0x2543, 0x208, None, (0x2E8, 0x2D8, 0x2D0, 0x2DC, 0x2E0),
     'TprepareGameDlg', 'GroupBox1', 'Label13', 16),
    (10, 0x204C, 0x20F0, 0x108, 0x2892, (0x1DC, 0x1C4, 0x1B4, 0x1C8, 0x1CC),
     'TPlantelDlg', 'GroupBox1', 'Label4', 24),
]

LINHAS = ['Jogos', 'Golos', 'Cart\xf5es amarelos', 'Cart\xf5es vermelhos', 'Les\xf5es']
VALORES = ['textHistoryGamesPlayed', 'textHistoryGoals', None, 'textHistoryRedCards', 'textHistoryInjuries']


def _rotulo(nome, props):
    return dict(cls='TLabel', nome=nome, props=props, filhos=[])


def _caixa(caixa, fixo, topo):
    f = {c['nome']: c for c in caixa['filhos']}
    legenda = {}
    for c in caixa['filhos']:
        for pn, v in c['props']:
            if pn == 'Caption' and v[0] == 6:
                legenda[v[2:].decode('latin1')] = c
    assert legenda['Cart\xf5es vermelhos'] is f[fixo]
    valor = f['textHistoryRedCards']
    # os valores sao postos pelo codigo: a legenda do DFM ("textHistory...") nao serve
    for nome in VALORES:
        if nome:
            f[nome]['props'] = [pv for pv in f[nome]['props'] if pv[0] != 'Caption']
    f[fixo]['nome'] = 'Vermelhos'
    caixa['filhos'].append(_rotulo('Amarelos', [
        ['Left', inteiro(8)], ['Top', inteiro(0)], ['Caption', texto('Cart\xf5es amarelos')]]))
    caixa['filhos'].append(_rotulo(fixo, [
        ['Left', dict(valor['props'])['Left']], ['Top', inteiro(0)],
        ['Width', dict(valor['props'])['Width']], ['Height', dict(valor['props'])['Height']],
        ['Alignment', ident('taRightJustify')], ['AutoSize', FALSO]]))
    legenda['Cart\xf5es amarelos'] = caixa['filhos'][-2]
    f = {c['nome']: c for c in caixa['filhos']}
    for k, (txt, val) in enumerate(zip(LINHAS, VALORES)):
        y = inteiro(topo + 16 * k)
        por(legenda[txt], 'Top', y)
        por(f[val or fixo], 'Top', y)


def _dfm(b, classe, nome_caixa, fixo, topo):
    i = b.find(b'TPF0' + bytes([len(classe)]) + classe.encode())
    assert i > 0, classe
    form, fim = ler_dfm(b, i + 4)
    livre = fim
    while b[livre] == 0 and livre - i < 0x10000 and (livre & 0xFF):
        livre += 1                                    # enchimento ate o fim do bloco de 256
    pilha = [form]
    while pilha:
        c = pilha.pop()
        if c['nome'] == nome_caixa:
            _caixa(c, fixo, topo)
            break
        pilha += c['filhos']
    else:
        raise AssertionError(nome_caixa)
    novo = b'TPF0' + escrever_dfm(form)
    assert i + len(novo) <= livre, (classe, len(novo), livre - i)
    b[i:livre] = novo + b'\0' * (livre - i - len(novo))


def historial(ne, b):
    for s, ini, fim, temp, jog, campos, classe, nome_caixa, fixo, topo in JANELAS:
        defs = dict(ORG='0x%X' % ini, FIM='0x%X' % fim, TEMP='0x%X' % temp)
        if jog:
            defs['JOGADOR'] = '0x%X' % jog
        for k, c in enumerate(campos):
            defs['F%d' % (k + 1)] = '0x%X' % c
        cod, rot = montar('historial.asm', ini, **defs)
        assert len(cod) <= fim - ini, (s, len(cod), fim - ini)
        base = ne.seg(s)[0]
        assert b[base + ini: base + ini + 4] == bytes.fromhex('8dbe') + b[base + ini + 2: base + ini + 4]

        p, recs = _tabela(ne, s)
        velhas = sorted((r for r in recs if ini <= r[2] < fim), key=lambda r: r[2])
        assert [(r[3] & 0xFF, r[4]) for r in velhas] == [INTTOSTR, SETTEXT] * 4, (s, velhas)
        novas = {velhas[0][2]: rot['..@i'], velhas[1][2]: rot['..@t']}
        novos = [r[:2] + [novas[r[2]]] + r[3:] if r[2] in novas else r
                 for r in recs if not (ini <= r[2] < fim and r[2] not in novas)]
        _gravar_tabela(ne, p, novos)
        b[p + 2 + 8 * len(novos): p + 2 + 8 * len(recs)] = b'\0' * (8 * (len(recs) - len(novos)))
        b[base + ini: base + fim] = cod + b'\x90' * (fim - ini - len(cod))
        if s == 4:      # os textos da aba Jogador (cartoes.py) ficam no fim desta regiao
            from cartoes import textos_amarelos, TEXTOS4
            t, _ = textos_amarelos()
            assert ini + len(cod) <= TEXTOS4 and TEXTOS4 + len(t) <= fim, (len(cod), len(t))
            b[base + TEXTOS4: base + TEXTOS4 + len(t)] = t

        _dfm(b, classe, nome_caixa, fixo, topo)
