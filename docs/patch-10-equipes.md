# Patch de 10 equipes por divisão (experimental)

> Substituído por `tools/divisoes/patch_divisoes.py` (8 a 20 equipes), ver
> `docs/patch-divisoes.md`. Fica aqui como registro do primeiro teste.

`tools/patch_10equipas.py` muda o `ELIFOOT.EXE` (98.003 com o patch do 5-0-5)
para ter **10 equipes em cada divisão** e **18 jornadas** por temporada (turno e
returno), em vez de 8 equipes e 14 jornadas. O Distrital continua com as equipes
que sobram.

```
python3 tools/patch_10equipas.py game/ELIFOOT.EXE ELIFOOT10.EXE
```

O EXE gerado tem o mesmo tamanho do original (836.356 bytes). A soma de
verificação dos 4 últimos bytes é recalculada (ver "Autoverificação do
executável" em `elifoot98-interno.md`). MD5 do EXE testado: `d72e3f07eea6…`. O
script gera sempre o mesmo arquivo.

**Ainda não vai para o launcher.** Um save criado com este EXE só funciona com
ele: o calendário e o tamanho das divisões ficam gravados no save.

## O que foi testado (Linux, Wine + otvdm, tela virtual)

Um jogo novo só com o Brasil (45 equipes) e um treinador humano, jogado
automaticamente por **uma temporada e meia** (2026 inteira e 2027 até a 10ª
jornada):

- **Divisões**: as 4 com 10 equipes e o Distrital com 5. A classificação mostra
  as 10 linhas de cada divisão.
- **Calendário**: 18 jornadas, 9 jogos em casa e 9 fora para cada equipe. Na
  tela da jornada aparecem os 5 jogos de cada divisão, com as setas de página.
- **Taça**: começou na 8ª jornada ("32 avos de final", 13 jogos), com todas as
  45 equipes, inclusive o Distrital.
- **Durante a temporada**: pênaltis (escolha do batedor), lesões com
  substituição, leilões e salários funcionaram.
- **Fim da temporada** (2026 → 2027): sobem 2 e descem 2 em cada divisão.
  - Da 1ª desceram Internacional e Lusa.
  - Da 2ª subiram Juventude e Coritiba.
  - Da 4ª subiram Criciúma e Santa Cruz, e desceram Ituano e Araçatuba.
  - Do Distrital subiram, por sorteio, Lusa Santista e Piracicaba.
  - Todas as divisões continuaram com 10 equipes.
- **Save**: gravado sozinho pelo jogo a cada jornada. O Editor de Save e o
  Scout do launcher leem as divisões certas (10, 10, 10, 10 e 5) e regravam o
  save idêntico byte a byte.

**Não testado:**
- carregar o save de volta no jogo (fechar e "Quero continuar um jogo");
- jogos com mais de um país e com treinadores humanos em várias divisões;
- o Android (Boxedwine).

## O que o patch muda

Os endereços são do 98.003.

| Onde | Antes | Depois | Para quê |
|---|---|---|---|
| `seg05:3BDB` | `i div 8` | `i div 10` | distribuição inicial das equipes pelas divisões |
| `seg03:1D87` | `imul di,[bp+A],14` | `…,18` | calendário de cada equipe: linha da tabela de adversários |
| `seg03:1D8D` | `mov al,[di+0x1837]` | `mov al,[di+0x17C7]` | tabela de adversários nova (10 × 18, com sinal: + casa, − fora) em `DS:0x17C8`, no lugar das duas tabelas 8 × 14 do jogo |
| `seg03:1D9E` | `mov cx,8` (mod 8) | `mov cx,10` | adversário dentro da divisão |
| `seg03:1DE7` | 14 entradas | 18 | tamanho do calendário (`TCalendar`) da equipe |
| `seg03:391A`, `392C` | tabela 8 × 14 de "lugares" no DS, 14 bytes por equipe | tabela 10 × 9 no fim do `seg03` (lida com `push cs`), 4 bits por jornada | ordem dos jogos de cada jornada. O campo da equipe `+0x22` tem 14 bytes: com 4 bits por jornada cabem até 28 jornadas |
| `seg11:1A87` (comparador) e um ajudante novo no fim do `seg11` | lia 1 byte por jornada | lê o "lugar" de 4 bits da jornada atual | ordenação dos jogos na tela da jornada |
| `seg03:53F3`, `seg05:0CF1`, `seg05:1224`, `seg11:0DA2` | `cmp [jornada],14` | `18` | testes de "última jornada" (fim da época, troca de treinador) |
| `seg05:11DC` | `cmp ax,14` | `18` | outro teste de última jornada |
| `seg05:0EEF` | `14 − jornada` | `18 − jornada` | calendário da Taça: `2^((18 − j) div 2)` |
| `seg05:098D`, `09A3` | posição 6 | 8 | quem desce da 1ª: os dois últimos de 10 |
| `seg05:09B9`…`0A62` (6 lugares) | posição 4 | 6 | quem desce da 2ª, 3ª e 4ª (a lista já ganhou os 2 que desceram e perdeu os 2 que subiram) |
| `seg06:1818`, `185C`, `18D8`, `1B11`, `1836`, `187A` | mínimo de 34 equipes | 42 (4 × 10 + 2) | seleção de países para um jogo novo |
| `seg06:07D8` | 8 linhas | 10 | linhas por divisão na Classificação |
| `seg03:66C6` | `jornada / 14` (real de 4 bytes) | `/ 18` | escala do salário pedido ao longo da temporada |
| DFM `TclassificationDlg` | listas de 130 pixels | 162 (2 linhas de 16 a mais); `ClientHeight` 371 → 435; `Label2`, `Label4` e as listas das divisões 2 e 4 descem 32 pixels | a janela da Classificação mostra as 10 linhas |

O calendário é gerado pelo método de Berger (rodízio), em `calendario()`, com o
returno espelhado. O script confere que cada equipe joga 9 vezes em casa e
enfrenta cada adversária duas vezes.

## Limitações conhecidas

- **A escada de força não acompanha.** O alvo da evolução de força é
  `B = D − 2 × posição`, e o D de cada divisão é fixo: `D = 52 − 14 × divisão`
  (`seg03:0BDD`), ou seja 52, 38, 24 e 10. Com 10 equipes, a última de cada
  divisão mira 4 a menos que a líder da divisão de baixo (1ª: 52 → 34; 2ª: 38 →
  20; 3ª: 24 → 6; 4ª: 10 → −8). O jogo funciona, mas a escada deixa de ser
  contínua. A correção planejada é `B = D − 14 × posição ÷ (N − 1)` (`seg03:6FED`,
  `shl ax,1`), que com 8 equipes dá exatamente o original.
- **A inflação** muda a cada jornada: com 18 jornadas ela anda cerca de 29% mais
  por temporada.
- **Até onde dá para ir**: a tabela de adversários precisa caber nos 224 bytes
  de `DS:0x17C8` (10 × 18 = 180). Os "lugares" precisam de N ≤ 15 (4 bits) e de
  jornadas ≤ 28. Para 12 ou 14 equipes a tabela de adversários (264 e 364 bytes)
  precisa de outro lugar.
  - **Tentativa que não funcionou**: mover o `seg03` inteiro para o fim do
    arquivo para ele poder crescer. Mover e acrescentar bytes de enchimento
    funciona, mas com as tabelas reais o jogo fica na tela verde, antes da
    "Acerca". A causa não foi encontrada.
