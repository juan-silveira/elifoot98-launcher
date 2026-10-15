"""Tela da jornada abre na pagina dos treinadores humanos (jornada.asm, no fim do seg25).

A pagina (DS:0x2B38) ia sempre para 0 ao abrir a tela e quando a lista muda no minuto
90: com mais de 8 equipas por divisao, quem estava na 3a ou 4a divisao tinha de clicar
na seta. escolher conta os jogos com time humano em cada pagina (a linha do jogo e a
posicao dele na lista DS:0x270A, P linhas por pagina, como em seg03:9F22) e abre na
que tem mais; no campeonato e na Taca.

O seg10 nao tem lugar para relocacoes novas: as duas chamadas que vem logo depois do
"pagina = 0" passam a ir para o seg25, que escolhe a pagina e salta para o destino:
- seg10:02FD call 10:0AFC (as setas), ao abrir;
- seg10:085C call 10:0CEF, no minuto 90 (o "pagina = 0" de 086B vira NOPs).
"""
from montar import montar
from paginas import _tabela, _gravar_tabela
from ingressos import _novos

SEG = 25
DESVIOS = {0x02FE: ('abrir', (10, 0x0AFC)), 0x085D: ('minuto90', (10, 0x0CEF))}


def jornada(ne, b, n):
    p = 20 if n <= 8 else n + 2         # linhas por pagina (patch_divisoes)
    org = ne.seg(SEG)[1]
    cod, rot = montar('jornada.asm', org, ORG='0x%X' % org, P=p)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@setas'], 10, 0x0AFC], [3, 0, rot['..@lista'], 10, 0x0CEF],
                     [3, 0, rot['..@item'], 23, 0x0DD0]])
    ne.patch(10, 0x086B, '31c0a3382b', '9090909090')
    tab, recs = _tabela(ne, 10)
    for loc, (nome, alvo) in DESVIOS.items():
        r = next(r for r in recs if r[2] == loc)
        assert (r[3], r[4]) == alvo, hex(loc)
        r[3], r[4] = SEG, rot[nome]
    _gravar_tabela(ne, tab, recs)
