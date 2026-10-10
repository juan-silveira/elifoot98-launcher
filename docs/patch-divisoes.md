# Equipes por divisão (8 a 20) e limite de 30 jogadores

`tools/divisoes/patch_divisoes.py` gera, a partir do `game/ELIFOOT.EXE` (98.003
com o patch do 5-0-5), um EXE para cada número de equipes por divisão:

```
python3 tools/divisoes/patch_divisoes.py 20 game/ELIFOOT.EXE game/ELIF20.EXE
```

O pacote traz os 7 EXEs (`game/ELIF08.EXE` … `game/ELIF20.EXE`). Nas
Configurações do launcher (Linux e Android) o usuário escolhe o número, e o
"Jogar Elifoot 98" abre o EXE correspondente. Precisa do `nasm`. O script gera
sempre os mesmos bytes, e os EXEs têm o mesmo tamanho do original (836.356
bytes). A soma de verificação dos 4 últimos bytes é recalculada.

Um save só continua com o EXE em que foi começado: o calendário e o tamanho das
divisões ficam gravados nele. Com outro EXE o jogo dá "List index out of
bounds.".

Por isso o launcher deixa na pasta `JOGOS` só os saves do número escolhido,
antes de abrir o jogo. Os outros vão para `JOGOS/<N> equipes` e voltam quando
esse número é escolhido de novo (`src/SavesPorEquipes.cs` e
`SavesPorEquipes.java`). O jogo só lista os saves da própria pasta `JOGOS`. O
Editor de Save e o Scout listam os saves de todas essas pastas, com o número
ao lado ("JUAN — 20 equipes").

## Em todos os EXEs: até 30 jogadores por equipe

O jogo tinha dois limites: 24 durante o jogo e 20 no editor de equipes.

| Onde | Antes | Depois | O que é |
|---|---|---|---|
| `seg03:635A` | 24 | 30 | compras e leilões: jogadores (`equipe+0x88`) + compras pendentes (`+0xC7`) abaixo do máximo |
| `seg03:3B51` | 21 | 27 | a partir daqui o computador fica mais disposto a vender (3 abaixo do máximo) |
| `seg01:13B3` | 20 | 30 | editor do jogo: botão Adicionar (`JogadorAdicionar`) |
| `seg12:27FA` | 20 | 30 | editor do jogo: validação "Tem demasiados jogadores" |

Os jogadores da equipe ficam numa `TList` (`seg03:37D0`, `TList.Add`). Os
bytes `+0x83` a `+0x86` contam os jogadores por posição, `+0x87` os
estrangeiros e `+0x88` o total. Não há vetor de tamanho fixo.

Os editores do launcher (Windows, Linux e Android) aceitam de 14 a 30.

`ELIF08.EXE` é o jogo original só com essa mudança.

## Com mais de 8 equipes

R = 2(N − 1) jornadas.

- **Calendário.** É o método de Berger, com o returno espelhado, calculado na
  hora: `calend.asm` e `nucleo.inc` no fim do `seg03`, no lugar das tabelas
  8 × 14 do DS (`DS:0x1838` e `DS:0x17C8`). O campo de 14 bytes da equipe em
  `+0x22` guarda só o número dela na divisão. A ordem dos jogos de cada
  jornada é calculada por um ajudante no fim do `seg11` (`helper11.asm`),
  chamado pelo comparador em `seg11:1A87`.
- **Testes de última jornada e escala do salário pedido:** 14 → R.
- **Taça:** `2^((R − j) div 2)` em `seg05:0EEF`.
- **Escada de força:** `B = D − 14 × posição ÷ (N − 1)` (`seg03:6FED`). Era
  `D − 2 × posição`: com 8 equipes dá o mesmo valor, e com N equipes a última
  de cada divisão continua 14 abaixo da primeira.
- **Inflação** (`seg05:0C2F`): o passo por jornada passa de 0,01 para
  `0,01 × 14 / R`. Assim a temporada anda o mesmo que com 14 jornadas.
- **Mínimo de equipes no jogo novo:** 4N + K (`seg06`).
- **Sobem e descem K por divisão**, com K = 2, ou K = 4 com N ≥ 16. Com
  K = 4 vale `subida.asm` no lugar de `seg05:0987`–`0A83`:
  - a rotina original de troca (`seg05:02A6`) é chamada K vezes por nível;
  - os sorteados do Distrital saem só dos que já estavam lá;
  - das 2 chamadas a `Random`, uma é reaproveitada e a outra sai da tabela
    de relocações.
- **Tela da jornada:** N + 2 linhas por página (eram 20), contando os
  cabeçalhos das divisões. Cada página mostra 2 divisões inteiras. A moldura
  azul passa a ter a altura de uma página (`seg10:0598`).
  Endereços: `seg03:9F22`, `9F43`; `seg10:05AD`, `09C4`, `0B17`.
- **Classificação:**
  - com até 14 equipes, as listas crescem;
  - com mais de 14, fica em 2 páginas, 1ª+2ª e 3ª+4ª (`paginas.asm` e
    `paginas.py`):
    - só as listas 1 e 2 ficam visíveis, e desenham a divisão da página
      (`DS:0x17D0` e `0x17D2`);
    - as setas `<` e `>` são TLabels, nos campos e nos métodos publicados das
      listas 3 e 4;
    - a seta que não leva a lugar nenhum fica cinza (`TFont.SetColor`,
      `seg19:0FDF`) e o clique nela não faz nada. Desabilitá-la não serve: o
      clique num controle desabilitado vai para o formulário, que fecha;
    - a janela sempre abre na página 1.

## O que foi testado (Linux, Wine + otvdm, tela virtual)

- **20 equipes**, jogo novo com todos os países:
  - uma temporada inteira (38 jornadas, Taça até a final, Fim de Época) e a
    1ª jornada da seguinte;
  - em cada divisão desceram 4 e subiram 4. Na 3ª e na 4ª, conferido pela
    classificação final: subiram os 4 primeiros e desceram os 4 últimos;
  - do Distrital subiram 4 por sorteio, e as 4 divisões continuaram com 20.
- **Telas com 20 equipes:**
  - a jornada mostra 1ª+2ª e 3ª+4ª, com os 10 jogos de cada divisão;
  - a Classificação com as setas;
  - clicar numa equipe da página 2 abre a equipe da 3ª ou da 4ª.
- **Limite de jogadores**, com o EXE de 20 e todas as equipes começando com
  28 jogadores:
  - 6 jornadas, com Taça, leilões e lesões;
  - 16 equipes compraram até 29 ou 30, e nenhuma passou de 30.
- **Inflação** com 20 equipes: 2,0 → 2,01 em 6 jornadas (cerca de 0,0017 por
  jornada; o original anda cerca de 0,005).
- **14 equipes:** uma temporada inteira com uma versão anterior do patch,
  sem a inflação nova, sem a moldura e sem o limite de jogadores.
- **10 equipes:** uma temporada e meia com o `tools/patch_10equipas.py`
  (`docs/patch-10-equipes.md`). Ele faz o mesmo de outro jeito: a tabela de
  adversários fica no DS.
- O `elifoot98.sh` abre o EXE de `ELIFOOT_EXE`.
- **Saves:**
  - um save de 20 equipes, aberto de novo no `ELIF20.EXE`, continuou a
    temporada;
  - depois da separação, a lista do jogo mostrou só os saves de 20;
  - o Scout do Android mostrou cada save com o seu número de equipes.

- **8, 10, 12, 16 e 18 equipes:** cada EXE abriu, começou um jogo novo com
  todos os países e jogou a 1ª jornada:
  - com 12, a Classificação mostra as 4 divisões inteiras numa tela;
  - com 16 e 18, a Classificação aparece em 2 páginas.

**Não testado:**
- uma temporada inteira com 8, 10, 12, 16 ou 18 equipes;
- o Android num aparelho real (só no emulador).
