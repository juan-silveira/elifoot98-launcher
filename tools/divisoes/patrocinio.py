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
from paginas import ler_dfm, escrever_dfm, inteiro, texto, ident, conjunto, FALSO, mover_dfm, por
import ingressos
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
    'corfonte': (19, 0x0FDF),          # TFont.SetColor
    'carregaimg': (19, 0x42C9),        # TPicture.LoadFromFile
}
CLASSE = 'TprepareGameDlg'


def _dfm(b):
    """Pagina "Patrocinio" no Notebook da janela do time, na posicao 5 (antes das
    escondidas Vender/Contrato/Mensagem, que o codigo abre pelo nome): as abas copiam as
    paginas e apagam as 3 ultimas. Os controles graficos ficam na ordem que a rotina aba
    espera: os 2 logos, os 6 rotulos preenchidos e os 2 titulos."""
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)

    def todos(c):
        yield c
        for f in c['filhos']:
            yield from todos(f)
    nb = next(x for x in todos(form) if x['cls'] == 'TNotebook')
    assert [dict(x['props'])['Caption'][2:] for x in nb['filhos']][5] == b'Vender'
    L = 265

    def rotulo(top, alt, fonte, negrito=False, caption=''):
        return dict(cls='TLabel', nome='', filhos=[], props=[
            ['Left', inteiro(4)], ['Top', inteiro(top)], ['Width', inteiro(L)], ['Height', inteiro(alt)],
            ['Alignment', ident('taCenter')], ['AutoSize', FALSO], ['Caption', texto(caption)],
            ['Font.Height', inteiro(fonte)], ['Font.Name', texto('Arial')],
            ['Font.Style', conjunto('fsBold') if negrito else conjunto()], ['ParentFont', FALSO]])

    def logo(top):
        return dict(cls='TImage', nome='', filhos=[], props=[
            ['Left', inteiro((L + 8 - 120) // 2)], ['Top', inteiro(top)], ['Width', inteiro(120)],
            ['Height', inteiro(60)], ['Center', b'\x09']])
    pagina = dict(cls='TPage', nome='', filhos=[
        logo(32), logo(196),
        rotulo(96, 20, -16, True), rotulo(118, 16, -12), rotulo(134, 16, -12),
        rotulo(260, 20, -16, True), rotulo(282, 16, -12), rotulo(298, 16, -12),
        rotulo(8, 18, -13, True, 'Patrocinador principal'),
        rotulo(172, 18, -13, True, 'Naming rights do est\xe1dio'),
    ], props=[['Left', inteiro(0)], ['Top', inteiro(0)], ['Caption', texto('Patroc\xednio')]])
    nb['filhos'].insert(5, pagina)
    # 6 abas: a fonte das abas fica Arial -10 (a MS Sans Serif -11 nao deixava caber)
    ts = next(x for x in todos(form) if x['cls'] == 'TTabSet')
    por(ts, 'Font.Name', texto('Arial'))
    por(ts, 'Font.Height', inteiro(-9))
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))



def patrocinio(ne, b):
    org = ne.seg(SEG)[1]
    cod, rot = montar('patrocinio.asm', org, ORG='0x%X' % org)
    assert ne.append(SEG, cod) == org
    _novos(ne, SEG, [[3, 0, rot['..@' + nome], s, alvo] for nome, (s, alvo) in DESTINOS.items()])
    _redirecionar(ne, 5, 0x11CE, SEG, rot['inicio'], de=(5, 0x0002))
    _redirecionar(ne, 5, 0x0AA8, SEG, rot['fim'], de=(5, 0x304C))
    # o clique da aba Patrocinio (abas.asm, seg02)
    seg_abas, local = ingressos.ABA_PATROCINIO
    _novos(ne, seg_abas, [[3, 0, local, SEG, rot['aba']]])
    _dfm(b)
    for opc, destino in (('..@opc1', 'inicio_fim'), ('..@opc2', 'fim_premios')):
        salto = rot[destino] - (rot[opc] + 2)
        assert 0 < salto < 128
        opcoes.registrar('PATROC', SEG, rot[opc] + 1, [0], [salto])
