"""Patrocinadores: contratos de patrocinio master e naming rights de estadio
(patrocinio.asm, no fim do seg25, que arbitro.py leva para o fim do arquivo).

- Inicio da epoca (seg05:11CE, no lugar de "call far NewSeason"): depois da NewSeason,
  cada marca de PATROCIN\\MARCAS.DAT (na ordem do arquivo) calcula a verba da epoca
  (verba x inflacao / 2, menos os contratos que ja tem e a reserva dos patrocinios de
  jogo) e fecha contratos de 1 a 3 epocas com os times sem patrocinador, da 1a divisao
  ate a divisao maxima dela, respeitando o pais (modo exclusivo). Com o time do
  humano a marca guarda uma proposta: no fim, ate 3 propostas de master e 1 de naming
  rights aparecem na janela de Sim/Nao do aumento de salario.
- Fim da epoca (seg05:0AA8, no lugar de "call far premios"): cada contrato paga a epoca
  (linha dos Premios no balanco) e conta uma epoca a menos.
- Opcao PATROC (Configuracoes): desligada, as duas rotinas saltam a parte nova.
O contrato fica no TTeam (+0xCC a +0xDF), que vai e volta do save pelo cabecalho de cada
time (ingressos.py).
"""
from montar import montar
from ingressos import _redirecionar, _novos
import opcoes

SEG = 25
DESTINOS = {
    'novaepoca': (5, 0x0002),          # NewSeason
    'premios': (5, 0x304C),            # premios e janela de fim de epoca
    'addmoney': (3, 0x58BF),           # TTeam.AddMoney(valor, tipo)
    'random': (25, 0x212D),
    'assign': (25, 0x0E31),
    'reset': (25, 0x0E75),
    'blockread': (25, 0x0F60),
    'close': (25, 0x0EF6),
    'ioresult': (25, 0x0401),
    'extenso': (9, 0x335A),            # valor por extenso ("3 milhoes e 16 mil reais")
    'settext': (21, 0x1D8C),
    'visivel': (21, 0x1C77),           # TControl.SetVisible
    'showmodal': (22, 0x5743),
}


def patrocinio(ne, b):
    org = ne.seg(SEG)[1]
    cod, rot = montar('patrocinio.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@' + nome], s, alvo] for nome, (s, alvo) in DESTINOS.items()])
    _redirecionar(ne, 5, 0x11CE, SEG, rot['inicio'], de=(5, 0x0002))
    _redirecionar(ne, 5, 0x0AA8, SEG, rot['fim'], de=(5, 0x304C))
    for opc, destino in (('..@opc1', 'inicio_fim'), ('..@opc2', 'fim_premios')):
        salto = rot[destino] - (rot[opc] + 2)
        assert 0 < salto < 128
        opcoes.registrar('PATROC', SEG, rot[opc] + 1, [0], [salto])
