"""Nota e Risco de lesao na ficha do jogador, ao lado do Comportamento, nas 3
janelas que mostram o Historial: venda por leilao (TsellPlayerDlg), aba Jogador
da equipa (TprepareGameDlg) e plantel de outra equipa (TPlantelDlg). Usado
depois de historial() e larguras().

Os numeros sao postos pelo laco do historial.asm (linhas F6 e F7). Eles usam os
campos de 2 textos fixos que o codigo nunca le ("Golos esta epoca", "Golos",
"Lesoes"...): o texto fixo muda de nome e o numero novo fica com o nome antigo,
que o Delphi liga ao campo ao ler o DFM.

O DFM tem de caber no enchimento que o recurso ja tinha. Para abrir espaco, os
textos de exemplo ("textPlayerBehaviour"...) saem dos rotulos que o codigo
sempre preenche.
"""
from paginas import ler_dfm, escrever_dfm, por, inteiro, texto

# formulario, painel, y da Nota (a do Risco de lesao vem 15 abaixo), campos (Nota,
# Risco de lesao): o texto fixo que cede o campo e o nome novo dele, o que muda de y
# (as linhas ficam a 15 pontos: Comportamento, Nota, Risco de lesao, Golos esta
# epoca e o Historial; na aba Jogador nao cabe mais, ela acaba no "Renovar
# contrato"), alturas novas, rotulos sem o texto de exemplo
JANELAS = [
    ('TsellPlayerDlg', 'Panel1', 23,
     (('Label10', 'Comport'), ('Label15', 'GolosEp')),
     {'GolosEp': 53, 'textSeasonGoals': 53, 'GroupBox2': 68},
     {'Panel1': 165},
     ['textPlayerBehaviour', 'textSeasonGoals', 'textPlayerName', 'textBasePrice']),
    ('TprepareGameDlg', 'PanelPlayer', 15,
     (('Label11', 'GolosEp'), ('Label14', 'Lesoes')),
     {'Label7': 0, 'textPlayerBehaviour': 0, 'GolosEp': 45, 'textSeasonGoals': 45,
      'GroupBox1': 60, 'LabelSuspended': 252, 'LabelInjured': 266},
     {'PanelPlayer': 157},
     ['textPlayerBehaviour', 'textSeasonGoals', 'textSelectedPlayerName',
      'textSelectedPlayerCountry']),
    ('TPlantelDlg', 'PanelPlayer', 15,
     (('Label3', 'Golos'), ('Label5', 'Lesoes')),
     {'Label2': 45, 'textSeasonGoals': 45, 'GroupBox1': 164, 'LabelSuspended': 158,
      'LabelInjured': 172, 'textDesiredSalary': 291},
     {'GroupBox1': 97, 'PanelPlayer': 186},
     ['textPlayerBehaviour', 'textSeasonGoals']),
]

# Colunas: o texto em x 0 e todos os valores em x 104 (no original, o do
# Comportamento ficava em 88 e o dos Golos esta epoca em 96; com 104 cabe o "Gols na
# Temporada" da traducao, game/IDIOMAS/pt-BR.txt)
TEXTO_X, VALOR_X = 0, 104


def _rotulo(nome, x, y, largura, legenda=None):
    # largura dada: o AutoSize nao a ajusta ao abrir, e o texto que nao cabe na
    # largura padrao (65) quebra para a linha de baixo
    props = [['Left', inteiro(x)], ['Top', inteiro(y)], ['Width', inteiro(largura)]]
    if legenda:
        props.append(['Caption', texto(legenda)])
    return dict(cls='TLabel', nome=nome, props=props, filhos=[])


def _todos(c):
    yield c
    for f in c['filhos']:
        yield from _todos(f)


def _dfm(b, classe, painel, y, campos, mover, alturas, sem_exemplo):
    i = b.find(b'TPF0' + bytes([len(classe)]) + classe.encode())
    assert i > 0, classe
    form, fim = ler_dfm(b, i + 4)
    livre = fim
    while b[livre] == 0 and livre - i < 0x10000 and (livre & 0xFF):
        livre += 1                                    # enchimento ate o fim do bloco de 256
    c = {x['nome']: x for x in _todos(form)}
    assert 'Nota' not in c and 'Risco' not in c, classe
    for (velho, novo) in campos:
        assert novo not in c, (classe, novo)
        c[velho]['nome'] = novo
    c = {x['nome']: x for x in _todos(form)}
    for nome, top in mover.items():
        por(c[nome], 'Top', inteiro(top))
    for nome, altura in alturas.items():
        por(c[nome], 'Height', inteiro(altura))
    for nome in sem_exemplo:
        c[nome]['props'] = [pv for pv in c[nome]['props'] if pv[0] != 'Caption']
    # o "Golos esta epoca" do original (85) quebra "epoca" para baixo no leilao
    por(c['GolosEp'] if 'GolosEp' in c else c['Label2'], 'Width', inteiro(VALOR_X))
    for nome in ('textPlayerBehaviour', 'textSeasonGoals'):
        por(c[nome], 'Left', inteiro(VALOR_X))
        por(c[nome], 'Width', inteiro(185 - VALOR_X))
    (nota, _), (risco, _) = campos
    c[painel]['filhos'] += [
        _rotulo('Nota', TEXTO_X, y, VALOR_X, 'Nota'),
        _rotulo(nota, VALOR_X, y, 185 - VALOR_X),
        _rotulo('Risco', TEXTO_X, y + 15, VALOR_X, 'Risco de les\xe3o'),
        _rotulo(risco, VALOR_X, y + 15, 185 - VALOR_X)]
    novo = b'TPF0' + escrever_dfm(form)
    assert i + len(novo) <= livre, (classe, len(novo), livre - i)
    b[i:livre] = novo + b'\0' * (livre - i - len(novo))


def ficha(b):
    for args in JANELAS:
        _dfm(b, *args)
