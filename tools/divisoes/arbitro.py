"""Explicacao na janela "Ajuda do Arbitro" (TRefereeAidDlg).

A janela so mostra a balanca e uma barra por equipa (gaugeHomeAid/gaugeAwayAid, de 0
a 15), sem dizer o que elas sao. A barra e a influencia do arbitro em cada equipa
(docs/elifoot98-interno.md, "Arbitro"): mais penaltis a favor e menos expulsoes, e
com 9 ou mais (60% da barra) nenhuma expulsao. A janela fica mais alta e ganha 2
paragrafos debaixo do quadro das barras (cada legenda do Delphi 1 tem ate 255
caracteres). O DFM vai para o fim do arquivo (mover_dfm).
"""
import struct
from paginas import ler_dfm, escrever_dfm, por, inteiro, texto, ident, conjunto, mover_dfm

CLASSE = 'TRefereeAidDlg'
MAIS = 96                              # quanto a janela cresce
EFEITO = ('Quanto maior a barra, mais o \xe1rbitro ajuda a equipa: mais pen\xe1ltis a favor '
          'e menos expuls\xf5es. A partir de 60% da barra, a equipa n\xe3o tem expuls\xf5es.')
CALCULO = ('A barra vem da compara\xe7\xe3o das letras do nome do \xe1rbitro com as do nome da '
           'equipa, +3 se ele for do mesmo pa\xeds e +2 para quem joga em casa.')


def _paragrafo(top, altura, caption):
    assert len(caption) <= 255, len(caption)
    return dict(cls='TLabel', nome='', filhos=[], props=[
        ['Left', inteiro(22)], ['Top', inteiro(top)], ['Width', inteiro(370)],
        ['Height', inteiro(altura)], ['AutoSize', b'\x08'], ['WordWrap', b'\x09'],
        ['Caption', texto(caption)], ['Font.Color', ident('clBlack')],
        ['Font.Height', inteiro(-11)], ['Font.Name', texto('MS Sans Serif')],
        ['Font.Style', conjunto()], ['ParentFont', b'\x08']])


def arbitro(b):
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    altura = struct.unpack('<h', dict(form['props'])['ClientHeight'][1:3])[0]
    assert altura == 397, altura
    por(form, 'ClientHeight', inteiro(altura + MAIS))
    form['filhos'].append(_paragrafo(altura + 2, 42, EFEITO))
    form['filhos'].append(_paragrafo(altura + 48, 42, CALCULO))
    mover_dfm(b, CLASSE, b'TPF0' + escrever_dfm(form))
