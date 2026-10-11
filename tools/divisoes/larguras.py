"""Listas mais largas para a barra de rolagem vertical (patch do seg18:75C6 em
patch_divisoes.py). Sem ela as listas foram desenhadas a medida do texto; com
ela, a barra (16 pontos) cobria a ultima coluna (a forca nos penaltis, a coluna
S/A na equipa...). Cada lista ganha 16 pontos de largura e, onde nao ha folga,
o quadro em volta, os vizinhos da direita e a propria janela tambem.

Usado depois de historial() e lances(), que mexem nos mesmos DFMs. So troca
inteiros por outros do mesmo tamanho: o DFM fica do mesmo tamanho.
"""
import struct
from paginas import ler_dfm, escrever_dfm, por, inteiro

B = 16          # largura da barra
D = None        # "anda B para a direita" (Left + B)

# classe: {componente: {propriedade: (antes, depois)}}; '' e a propria janela
JANELAS = {
    # Height (+4): no otvdm as listas do Delphi 1 ficam 4 pontos mais baixas que o
    # DFM; as que foram desenhadas para N linhas exatas ganhavam barra de rolagem
    'TcoachManagerDlg': {
        'ListBoxOfCoaches': {'Height': (98, 102)},   # 6 treinadores
    },
    'TpenaltyDlg': {
        'ListBoxOfPlayers': {'Width': (169, 185), 'Height': (178, 182)},   # os 11 em campo
        'Bevel1': {'Width': (185, 201)},
    },
    'TsubstitutionDlg': {
        'ListBoxOfEvents': {'Width': (185, 200)},
        'Arbitro': {'Left': (216, 220), 'Width': (241, 237)},
        # Height: no otvdm as listas ficam 4 pontos mais baixas que o DFM e os 11
        # titulares (e os 5 reservas) nao cabiam sem barra
        'ListBoxOfSelectedPlayers': {'Width': (185, 201), 'Height': (178, 182)},
        'listBoxOfSubstitutePlayers': {'Left': (256, 240), 'Width': (185, 201), 'Height': (82, 86)},
    },
    'TforcedSubstitutionDlg': {
        'ListBoxOfSubstitutePlayers': {'Width': (225, 241)},
        'Bevel1': {'Width': (241, 257)},
    },
    'TprepareGameDlg': {
        '': {'ClientWidth': (632, 648)},
        'ListBoxOfPlayers': {'Width': (353, 369)},
        'ListBoxClassification': {'Height': (34, 38)},   # as 2 equipas do jogo (aba Jogo)
        'Notebook': {'Left': (358, 374)},
        'TabSet': {'Left': (368, 384)},
        'textOpponent': {'Left': (360, 376)},
        'Label2': {'Left': (360, 376)},
        'textSeason': {'Left': (568, 584)},
    },
    'TPlantelDlg': {
        '': {'ClientWidth': (632, 648)},
        'ListBoxOfPlayers': {'Width': (353, 369), 'Height': (386, 390)},   # 24 jogadores
        'Bevel1': {'Width': (369, 385)},
        # tudo o que esta a direita da lista anda B
        'textDesiredSalary': {'Left': D}, 'SpeedButtonObserved': {'Left': D}, 'OKBtn': {'Left': D},
        'gaugeMoral': {'Left': D}, 'balanceBtn': {'Left': D}, 'loanBtn': {'Left': D},
        'PanelPlayer': {'Left': D}, 'GroupBox1': {'Left': D}, 'observedPlayersBtn': {'Left': D},
        'calendarBtn': {'Left': D}, 'buyBtn': {'Left': D}, 'findBtn': {'Left': D},
    },
    'TcheckPlayerDlg': {
        'ListBoxOfPlayers': {'Width': (485, 501)},
        'Bevel1': {'Width': (501, 517)},
    },
    'TObservedPlayersDlg': {
        '': {'ClientWidth': (551, 567)},
        'ListBoxObservedPlayers': {'Width': (521, 537), 'Height': (242, 246)},   # 15 observados
        'Bevel1': {'Width': (537, 553)},
        'Memo1': {'Width': (539, 555)},
        'closeBtn': {'Left': (480, 496)},
    },
    'TcoachManagerDlg': {
        '': {'ClientWidth': (457, 473)},
        'ListBoxOfCoaches': {'Width': (425, 441)},
        'Bevel1': {'Width': (441, 457)},
    },
    'TbalanceDlg': {
        '': {'ClientWidth': (471, 487)},
        'ListBoxBalance': {'Width': (444, 460)},
        'Bevel1': {'Width': (460, 476)},
        'OKBtn': {'Left': (203, 211)},
    },
    'TsorteioDlg': {
        '': {'ClientWidth': (631, 647)},
        'listBoxOfGames': {'Width': (233, 249)},
    },
    'TEndOfSeasonDlg': {
        'ListBoxBestScorers': {'Width': (265, 281)},
    },
    'TPenaltiesDlg': {
        'ListBoxPenalties': {'Left': (58, 50), 'Width': (517, 533)},
    },
}


def _valor(v):
    return struct.unpack({2: '<b', 3: '<h'}[v[0]], v[1:])[0]


def larguras(b):
    for classe, mudancas in JANELAS.items():
        i = b.find(b'TPF0' + bytes([len(classe)]) + classe.encode())
        assert i > 0, classe
        form, fim = ler_dfm(b, i + 4)
        comps = {'': form}
        pilha = list(form['filhos'])
        while pilha:
            c = pilha.pop()
            if c['nome']:                     # (as paginas do TNotebook nao tem nome)
                comps[c['nome']] = c
            pilha += c['filhos']
        for nome, props in mudancas.items():
            c = comps[nome]
            atual = dict(c['props'])
            for prop, troca in props.items():
                v = _valor(atual[prop])
                antes, depois = troca if troca else (v, v + B)
                assert v == antes, (classe, nome, prop, v)
                novo = inteiro(depois)
                assert novo[0] == atual[prop][0], (classe, nome, prop, depois)   # mesmo tamanho
                por(c, prop, novo)
        novo = b'TPF0' + escrever_dfm(form)
        assert len(novo) == fim - i, classe
        b[i:fim] = novo
