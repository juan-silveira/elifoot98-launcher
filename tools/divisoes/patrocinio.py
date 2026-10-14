"""Patrocinadores: contratos de patrocinio master
(patrocinio.asm, no fim do seg25, que arbitro.py leva para o fim do arquivo).

- Inicio da epoca (seg05:11CE, no lugar de "call far NewSeason"): depois da NewSeason,
  cada marca de SPONSORS\\MARCAS.DAT (na ordem do arquivo) calcula a verba da epoca
  (verba x inflacao / 2, menos os contratos que ja tem e a reserva dos patrocinios de
  jogo) e fecha contratos de 1 a 3 epocas com os times sem patrocinador, da 1a divisao
  ate a divisao maxima dela, respeitando o pais (modo exclusivo). Com o time do
  humano a marca guarda uma proposta: no fim, ate 3 propostas aparecem na janela de
  Sim/Nao do aumento de salario, com a cor normal de janela e o logo da marca (a
  janela volta ao vermelho do aumento de salario depois).
- Fim da epoca (seg05:0AA8, no lugar de "call far premios"): cada contrato paga a epoca
  (linha dos Premios no balanco) e conta uma epoca a menos.
- Opcao PATROC (Configuracoes): desligada, as duas rotinas saltam a parte nova.
O contrato fica no TTeam (+0xCC a +0xDF), que vai e volta do save pelo cabecalho de cada
time (ingressos.py).
"""
import struct
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
    'settop': (21, 0x179D),            # TControl.SetTop
    'altura': (22, 0x2E48),            # TForm.SetClientHeight
}
CLASSE = 'TprepareGameDlg'
DIALOGO = 'TaskMoreSalaryDlg'


def _dfm(b):
    """Pagina "Patrocinio" no Notebook da janela do time, na posicao 5 (antes das
    escondidas Vender/Contrato/Mensagem, que o codigo abre pelo nome): as abas copiam as
    paginas e apagam as 3 ultimas. Os controles graficos ficam na ordem que a rotina aba
    espera: o logo, os 3 rotulos preenchidos e o titulo."""
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
        logo(84),
        rotulo(160, 22, -18, True), rotulo(188, 16, -13), rotulo(206, 16, -13),
        rotulo(48, 20, -15, True, 'Patrocinador principal'),
    ], props=[['Left', inteiro(0)], ['Top', inteiro(0)], ['Caption', texto('Patroc\xednio')]])
    nb['filhos'].insert(5, pagina)
    # 6 abas: a fonte das abas fica Arial -10 (a MS Sans Serif -11 nao deixava caber)
    ts = next(x for x in todos(form) if x['cls'] == 'TTabSet')
    por(ts, 'Font.Name', texto('Arial'))
    por(ts, 'Font.Height', inteiro(-9))
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))


def _dfm_dialogo(b):
    """A janela de Sim/Nao ganha 2 TImage escondidos: o fundo (FControls[0], SPONSORS\\
    FUNDO.BMP esticado por cima do vermelho) e o logo da marca (FControls[3], depois dos 2
    rotulos, que ficam transparentes). A rotina perguntar mostra os dois e desce os botoes."""
    i = b.find(b'TPF0' + bytes([len(DIALOGO)]) + DIALOGO.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    assert [f['cls'] for f in form['filhos']][:2] == ['TLabel', 'TLabel']
    assert all(f['cls'] == 'TBitBtn' for f in form['filhos'][2:])
    largura = dict(form['props'])['ClientWidth']
    largura = int.from_bytes(largura[1:], 'little')
    for rot in form['filhos'][:2]:
        por(rot, 'Transparent', b'\x09')
    form['filhos'].insert(2, dict(cls='TImage', nome='', filhos=[], props=[
        ['Left', inteiro((largura - 120) // 2)], ['Top', inteiro(176)], ['Width', inteiro(120)],
        ['Height', inteiro(60)], ['Center', b'\x09'], ['Visible', FALSO]]))
    form['filhos'].insert(0, dict(cls='TImage', nome='', filhos=[], props=[
        ['Left', inteiro(0)], ['Top', inteiro(0)], ['Width', inteiro(largura)],
        ['Height', inteiro(320)], ['Stretch', b'\x09'], ['Visible', FALSO]]))
    mover_dfm(b, DIALOGO, b'TPF0' + escrever_dfm(form))


def _classes_dialogo(ne, b):
    """O Delphi so cria um componente do DFM se achar a classe na tabela de classes dos
    campos da janela (ou registrada), e a da janela de Sim/Nao (seg07) so tem TBitBtn e
    TLabel: vai uma tabela nova no fim do seg07, com o TImage, e a janela passa a usa-la
    (os campos apontam a classe pelo deslocamento na tabela: 0 TBitBtn, 4 TLabel)."""
    base, ln, fl, mn = ne.seg(7)
    d = bytes(b[base:base + ln])
    i = d.find(b'\x7c\x01\x00\x00\x06YesBtn\x80\x01')
    assert i > 0 and d.find(b'\x7c\x01\x00\x00\x06YesBtn\x80\x01', i + 1) < 0
    tabela = i - 4                      # quantidade de campos, tabela de classes (perto)
    assert d[tabela:tabela + 2] == b'\x05\x00'
    antiga = struct.unpack_from('<H', d, tabela + 2)[0]
    assert d[antiga:antiga + 2] == b'\x02\x00'
    nova = ne.append(7, b'\x03\x00' + b'\xff\xff\x00\x00' * 3)
    _novos(ne, 7, [[3, 0, nova + 2, 16, 0x0E47],     # TBitBtn
                   [3, 0, nova + 6, 18, 0x0617],     # TLabel
                   [3, 0, nova + 10, 17, 0x0365]])   # TImage
    struct.pack_into('<H', b, base + tabela + 2, nova)



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
    _dfm_dialogo(b)
    _classes_dialogo(ne, b)
    for opc, destino in (('..@opc1', 'inicio_fim'), ('..@opc2', 'fim_premios')):
        salto = rot[destino] - (rot[opc] + 2)
        assert 0 < salto < 128
        opcoes.registrar('PATROC', SEG, rot[opc] + 1, [0], [salto])
