# Score e Impacto do Scout

Os dois números usam só os atributos que vêm do nome do jogador (Nota, Lesão,
Comportamento e estrela; ver `docs/elifoot98-interno.md`, seção 2). A força fica
de fora: ela muda ao longo do jogo e já aparece na sua própria coluna.

As contas estão em `src/Score.cs` (Windows, Linux e macOS) e em
`android/.../Score.java` (Android).

## Como os números foram medidos

Um simulador de temporadas segue as regras da seção 4 do documento interno. Ele
cobre gols, pênaltis, a influência do árbitro, gols anulados, expulsões, lesões,
substituições, suspensões, evolução de força e moral. O cenário é uma liga de
8 times com 18 jogadores, todos com força 30, e os atributos dos outros
jogadores sorteados como os de nomes reais. Os árbitros são de outro país em 94%
dos jogos (11 dos 182 árbitros do `REFEREE.TXE` são brasileiros).

Em cada teste, um único jogador do time muda. O mesmo elenco joga 10 temporadas
com o jogador de referência (nota sem estrela, Lesão 0, Fair Play) e depois com
o candidato, em 30.000 elencos diferentes. O erro padrão fica em cerca de
0,02 ponto por temporada.

O código está em `tools/sim/sim.c`
(`gcc -O2 -o sim sim.c -lm`; `./sim posição nota lesão comportamento elencos temporadas`).

## Score (0 a 1000)

```
Score = Nota × 100 × (1 − jogos perdidos ÷ 14)
jogos perdidos por temporada de 14 jogos = a × Lesão + b × Comportamento
```

| Posição | a (por ponto de Lesão) | b (por ponto de Comportamento) |
|---|---|---|
| Goleiro | 0,105 | 0,17 |
| Defesa, Meio, Avançado | 0,13 | 0,21 |

Exemplos:
- Nota 10, Lesão 0, Fair Play → 1000.
- Nota 10, Lesão 3, Fair Play → 972.
- Nota 10, Lesão 10, Sarrafeiro → 832.
- Nota 6, Lesão 0, Fair Play → 600.

## Impacto (pontos por temporada)

Mede quantos pontos o time faz a mais ou a menos com o jogador, em 14 jogos,
comparado ao jogador de referência da mesma posição.

| Posição | Estrela | por ponto de Lesão | por ponto de Comportamento |
|---|---|---|---|
| Goleiro | – | −0,016 | −0,065 |
| Defesa | – | +0,02 | 0 |
| Meio | +0,26 | 0 | +0,019 |
| Avançado | +0,27 | −0,013 | +0,021 |

**Por que alguns sinais são positivos:** o jogo sorteia a cada minuto **se** o
time terá uma lesão (`Random(500)`) ou uma expulsão (`Random(1000)` contra a
influência do árbitro). Lesão e Comportamento só escolhem **quem** sofre.
- Um atacante violento atrai expulsões que iriam para o goleiro ou para os
  defesas, e essas dão pênalti contra.
- Um defesa frágil atrai lesões que iriam para outros jogadores.

**A nota quase não muda o resultado do time.** O número de gols vem da força dos
setores; a nota só escolhe o autor do gol e o batedor de pênalti. Por isso, no
Impacto, só aparece a estrela (Nota 8 ou mais em meio-campistas e avançados:
+2 na força do setor).

Gols por temporada medidos, por ponto de nota: Defesa 0,06; Meio 0,23;
Avançado 0,53.
