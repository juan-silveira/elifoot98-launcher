# Árbitros originais do Elifoot 98 (versão de lançamento)

Fonte: usuário juan-silveira. 182 árbitros presentes em `REFEREE.TXE`.
Serve como known plaintext pra reverter a cifra do arquivo.

## Ordem no arquivo (por país)

- Alemanha: Bern Heyenemann, Helmut Krug, Hermann Albrecht, Markus Merk
- Áustria: Gerd Grabher, Gunter Benko
- Bélgica: Michel Piraux
- Myanmar: Mika Peitota
- Belarus: Vadim Zhuk
- Brasil: (11 árbitros — inclui Arnaldo Cezar Coelho, Márcio Rezende de Freitas, Wilson de Souza)
- Dinamarca: Kim Milton Nielsen, Peter Mikkelsen (+3)
- Escócia: Leslie Mottram, Mike McCurry
- Espanha: 8 árbitros (Manuel Diaz Vega, Garcia Aranda, etc.)
- França: 5 árbitros (Marc Batta, etc.)
- Holanda: Dick Jol, Marco Van Der Ende
- Hungria: Sandor Pull
- Inglaterra: 3 árbitros
- Irlanda: Richard O'Hanlon
- Itália: 6 árbitros (Paolo Collina, Pierluigi Pairetto, etc.)
- Letônia: Terje Hauge (registrado como Letônia)
- Marrocos: Nafis Abdel-Ali
- Malta: Charles Aglus
- Maurício: Lim Kee Chong
- Noruega: Rune Pedersen
- Portugal: **~121 árbitros** (a maioria)
- Rússia: Nikolai Levnikov
- Suécia: 5 (Anders Frisk, etc.)
- Suíça: 3
- Trinidad e Tobago: Peter Kelly
- Turquia: 2 (Ahmed Cakar, Duncan Servan)
- Ucrânia: Vassily Melnichuk
- Vanuatu: Louis Fred

## Estrutura provável do arquivo

`REFEREE.TXE` = 3.445 bytes, começa com `07 60 1E 42 CB C6 26 EB...`

Hipótese: header de 7 bytes + registros por país, cada país tendo `[length][name][refCount][ref_entries...]`.

## Estado da RE

Cifra ainda não decifrada. Caesar acumulativa (que funciona pra `.EFT`) não decodifica leiturável no `.TXE`. Precisa análise dedicada usando esta lista como known plaintext.
