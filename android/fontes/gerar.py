# Gera as fontes Elifoot Sans/Serif a partir da Liberation 2.1.5 (SIL OFL 1.1):
# mesmo desenho e larguras da Arial/Times New Roman, mas com a tabela VDMX
# (altura real das letras por tamanho) e a largura media de caractere da
# Arial/Times. Sem isso o Wine mede a Liberation 1-2 px mais baixa, o Delphi
# encolhe as janelas do jogo uns 7% e o texto das listas fica cortado.
# O nome muda por exigencia da OFL (o nome "Liberation" e reservado).
#
# Uso (precisa das fontes da Microsoft, ex.: pacote ttf-mscorefonts-installer):
#   pip install fonttools==4.55.3
#   python3 gerar.py liberation-fonts-ttf-2.1.5/ /usr/share/fonts/truetype/msttcorefonts/
import os, sys
from fontTools.ttLib import TTFont

LIB, MS = sys.argv[1], sys.argv[2]
PARES = [
    ('LiberationSans-Regular', 'Arial', 'ElifootSans-Regular'),
    ('LiberationSans-Bold', 'Arial_Bold', 'ElifootSans-Bold'),
    ('LiberationSans-Italic', 'Arial_Italic', 'ElifootSans-Italic'),
    ('LiberationSans-BoldItalic', 'Arial_Bold_Italic', 'ElifootSans-BoldItalic'),
    ('LiberationSerif-Regular', 'Times_New_Roman', 'ElifootSerif-Regular'),
    ('LiberationSerif-Bold', 'Times_New_Roman_Bold', 'ElifootSerif-Bold'),
    ('LiberationSerif-Italic', 'Times_New_Roman_Italic', 'ElifootSerif-Italic'),
    ('LiberationSerif-BoldItalic', 'Times_New_Roman_Bold_Italic', 'ElifootSerif-BoldItalic'),
]
for lib, ms, novo in PARES:
    f = TTFont(os.path.join(LIB, lib + '.ttf'))
    ref = TTFont(os.path.join(MS, ms + '.ttf'))
    f['VDMX'] = ref['VDMX']
    f['OS/2'].xAvgCharWidth = ref['OS/2'].xAvgCharWidth
    velha = 'Liberation Sans' if 'Sans' in lib else 'Liberation Serif'
    nova = velha.replace('Liberation', 'Elifoot')
    for n in f['name'].names:
        try:
            t = n.toUnicode()
        except Exception:
            continue
        if velha in t or velha.replace(' ', '') in t:
            n.string = t.replace(velha, nova).replace(velha.replace(' ', ''), nova.replace(' ', ''))
    f.save(os.path.join(os.path.dirname(os.path.abspath(__file__)), novo + '.ttf'))
    print(novo)
