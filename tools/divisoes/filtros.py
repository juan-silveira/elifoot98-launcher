"""Filtros de Comportamento, Nota e Risco de lesao na Lista de Jogadores (Procura).

A busca (findBtnClick, seg10:34F5) passa cada jogador por uma funcao aninhada
(seg10:3223) que testa nome, posicao, nacionalidade, forca, preco e "nao mostrar os
da minha equipa", e devolve [bp-1]. O fim dela ("mov al, [bp-1]", seg10:34EC) salta
para um trecho no fim do seg10 que chama filtros.asm (fim do seg18) e testa os 3
combos novos contra o jogador: comportamento (+0x2D) igual, nota (+0x2B) minima e
risco de lesao (+0x2F) maximo.

Os combos usam os campos dos rotulos fixos Label1, Label2 e Label3 (Nome, Forca,
Posicao), que o codigo nao le: os rotulos mudam de nome e os combos ficam com os
nomes antigos. O FormCreate (no metodo publicado) passa a ser o de filtros.asm, que
poe os 3 em "Qualquer".

Na janela, a coluna da direita fica com os 3 filtros (nas linhas de Nome, Forca e
Posicao), "Nao mostrar os jogadores da minha equipa" (linha da Nacionalidade) e
"Dinheiro em caixa" (linha do Preco). A janela nao muda de tamanho.

O DFM da janela nao tem espaco para crescer no lugar: ele vai para o fim do arquivo
(os recursos ficam no fim, antes dos 4 bytes da autoverificacao) e a entrada dele na
tabela de recursos passa a apontar para la; o antigo e zerado.
"""
import struct
from montar import montar
from paginas import (_tabela, _gravar_tabela, ler_dfm, escrever_dfm, por, inteiro, ident,
                     texto, mover_dfm)
from ingressos import _redirecionar

FIM_FILTRO = 0x34EC                    # seg10: mov al, [bp-1]; leave; ret 6
CRIAR = 0x3A65                         # seg10: FormCreate
METODO_CRIAR = 0x27D7                  # seg10: endereco do FormCreate na tabela de metodos
CAMPOS = dict(COMP=0x188, NOTA=0x18C, RISCO=0x190)   # Label1, Label2, Label3
CLASSE = 'TcheckPlayerDlg'

COMPORTAMENTOS = ['Fair Play', 'Cordeirinho', 'Cavalheiro', 'Caneleiro', 'Caceteiro', 'Sarrafeiro']
NOTAS = ['10'] + ['%d ou mais' % n for n in range(9, 1, -1)]
RISCOS = ['0'] + ['At\xe9 %d' % n for n in range(1, 10)]


def _lista(itens):
    """Items.Strings de um TComboBox: vaList com as strings."""
    return b'\x01' + b''.join(texto(t) for t in itens) + b'\x00'


def _dfm(b):
    i = b.find(b'TPF0' + bytes([len(CLASSE)]) + CLASSE.encode())
    assert i > 0
    form, fim = ler_dfm(b, i + 4)
    c = {f['nome']: f for f in form['filhos']}

    # os rotulos fixos cedem os campos aos combos
    for velho, novo in (('Label1', 'TextoNome'), ('Label2', 'TextoNacionalidade'), ('Label3', 'TextoForca')):
        c[velho]['nome'] = novo

    X_ROTULO, X_COMBO, LARGURA = 385, 505, 170

    def rotulo(top, caption):
        form['filhos'].append(dict(cls='TLabel', nome='', filhos=[], props=[
            ['Left', inteiro(X_ROTULO)], ['Top', inteiro(top)], ['Width', inteiro(X_COMBO - X_ROTULO - 4)],
            ['Caption', texto(caption)]]))

    def combo(nome, top, itens):
        form['filhos'].append(dict(cls='TComboBox', nome=nome, filhos=[], props=[
            ['Left', inteiro(X_COMBO)], ['Top', inteiro(top)], ['Width', inteiro(LARGURA)],
            ['Height', inteiro(24)], ['Style', ident('csDropDownList')], ['ItemHeight', inteiro(16)],
            ['Items.Strings', _lista(['Qualquer'] + itens)], ['OnChange', ident('textNameChange')]]))

    rotulo(12, 'Comportamento')
    combo('Label1', 8, COMPORTAMENTOS)
    rotulo(44, 'Nota')
    combo('Label2', 40, NOTAS)
    rotulo(76, 'Risco de les\xe3o')
    combo('Label3', 72, RISCOS)

    # "Nao mostrar..." numa linha, na altura da Nacionalidade; o dinheiro na do Preco
    por(c['checkBoxNotMyTeam'], 'Left', inteiro(X_ROTULO + 4))
    por(c['checkBoxNotMyTeam'], 'Top', inteiro(102))
    por(c['Label6'], 'Left', inteiro(X_ROTULO + 32))
    por(c['Label6'], 'Top', inteiro(108))
    por(c['Label6'], 'Width', inteiro(270))
    por(c['Label6'], 'Height', inteiro(16))
    por(c['Label7'], 'Left', inteiro(X_ROTULO))
    por(c['Label7'], 'Top', inteiro(146))
    por(c['textMoney'], 'Left', inteiro(X_COMBO))
    por(c['textMoney'], 'Top', inteiro(146))
    por(c['textMoney'], 'Width', inteiro(LARGURA))

    novo = b'TPF0' + escrever_dfm(form)
    mover_dfm(b, CLASSE, novo)


def filtros(ne, b):
    s10, s18 = ne.seg(10)[0], ne.seg(18)[0]
    assert b[s10 + FIM_FILTRO: s10 + FIM_FILTRO + 7] == bytes.fromhex('8a46ffc9c20600')
    defs = {k: '0x%X' % v for k, v in CAMPOS.items()}

    org18 = ne.seg(18)[1]
    cod, rot = montar('filtros.asm', org18, ORG='0x%X' % org18, **defs)
    assert ne.append(18, cod) == org18

    # fim do seg10: call far filtro; leave; ret 6
    org10 = ne.seg(10)[1]
    assert ne.append(10, b'\x9a\xff\xff\x00\x00\xc9\xc2\x06\x00') == org10
    p, recs = _tabela(ne, 10)
    recs.append([3, 0, org10 + 1, 18, rot['filtro']])
    proximo = min(o for o in (ne.seg(t)[0] for t in range(1, ne.nseg + 1)) if o > s10)
    assert p + 2 + 8 * len(recs) <= proximo, 'sem espaco para relocacoes no seg10'
    _gravar_tabela(ne, p, recs)
    b[s10 + FIM_FILTRO: s10 + FIM_FILTRO + 3] = b'\xe9' + struct.pack('<h', org10 - (FIM_FILTRO + 3))

    _redirecionar(ne, 10, METODO_CRIAR, 18, rot['criar'], de=(10, CRIAR))
    _dfm(b)
