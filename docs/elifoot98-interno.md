# Elifoot 98 por dentro

O que se sabe, com certeza, sobre como o Elifoot 98 funciona por dentro. Tudo aqui
foi comprovado de uma de duas formas:

- **validado**: conferido contra dados reais (saves, telas do jogo, senhas geradas
  pelo próprio jogo);
- **código**: lido de forma inequívoca no `ELIFOOT.EXE`.

O que ainda é hipótese não entra neste documento (veja o fim).

Endereços como `seg03:7516` são *segmento:deslocamento* no executável (formato NE,
Windows 16 bits, escrito em Delphi 1). Os segmentos são numerados a partir de 1,
na ordem da tabela de segmentos do NE.

### Versões 98.002 e 98.003

A análise foi feita no **98.002** (`ELIFOOT.EXE` de 21/03/1998). O projeto passou
a usar o **98.003** (23/05/1998), a última versão, tirada do instalador original
`elif98.exe` que o autor publicava em `www.ip.pt/~ip213368` (cópia do Internet
Archive de 24/01/2001). **Validado** comparando os dois executáveis:

- mesmo tamanho (836.356 bytes); `EDITEQ.EXE`, `BIVBX11.DLL`, `GAUGE.VBX`,
  `COUNTRY.TXE` e `REFEREE.TXE` do instalador são idênticos aos que já usávamos;
- **seg03** (motor da partida, atributos do nome, escalação, leitura do save) e
  **seg14 a seg26** (VCL, runtime e dados) são idênticos byte a byte: tudo deste
  documento sobre esses segmentos vale igual no 98.003;
- **seg12** (registro, senha e autoverificação) tem o mesmo código; só mudam
  endereços de outros segmentos que ele usa;
- **seg04** é idêntico até `0x3200` (táticas e a janela do time);
- seg01, seg02, seg05 a seg11 e seg13 foram recompilados: endereços citados
  nesses segmentos são do 98.002 e podem estar em outro lugar no 98.003.

No 98.003 a "Acerca" mostra `98.003`, o registro do launcher continua valendo e
o save tem o mesmo formato (**validado**: um save do 98.002 abre no 98.003, e o
save gravado pelo 98.003 depois de uma rodada é lido e regravado byte a byte
pelos editores do launcher).

---

## 1. Registro (senha e contra-senha)

Implementado em `src/Registro.cs` (Windows/Linux/macOS) e
`android/.../Registro.java` (Android). **Validado** com vários pares de
senha/contra-senha gerados pelo jogo.

### eli.cod

- Fica na pasta do Windows (`GetWindowsDirectory`).
- Contém **2 strings Pascal** (1 byte de tamanho + dados).
- Cada string está **cifrada 2 vezes** pela mesma regra (`seg14:0734`): cada byte,
  a partir do 1º byte de dados, recebe a soma do byte anterior (incluindo o de
  tamanho). Para decifrar, desfaz-se de trás para frente, duas vezes.
- **String A**: derivada da data/hora das pastas `WINDOWS` e `WINDOWS\SYSTEM`.
- **String B**: um número sorteado pelo jogo com `Random(20000)`.

### Senha

`senha = "014-" + Formatar(A + B)` (`seg12:2f8b`). `Formatar`:

1. completa com `'0'` até 19 dígitos;
2. enquanto houver mais de 17 dígitos: cada dígito vira
   `(3 × próximo + atual + 3) mod 10` e o tamanho diminui 1;
3. volta o tamanho para 19 (os 2 últimos são sobras) e põe `-` nas posições
   4, 8, 12 e 16.

### Contra-senha

A contra-senha do **tipo N** é a senha passada N vezes pela transformação
`seg12:3645`. Ela escolhe a regra pelo 1º dígito e no fim soma 1 a esse dígito:

| 1º dígito | Regra |
|---|---|
| 0 | `Formatar("***" + s + s)` |
| 1 | mantém |
| 2 | soma 1 ao 5º caractere (9 vira 0) |
| 3, 4, 5 | `Formatar("+++" + s + "***" + s)` |
| 6 a 9 | `Formatar("1213" + s + s + "XXX" + s)` |

- **Tipo 9 = "Registo para autor 2"** (a contra-senha começa com 9).
- O registro fica em texto puro no `elif98.ini`, seção `[System]`:
  `secondCode=<contra-senha>`. O `eli.cod` não muda ao registrar.

### Fora do Windows (Wine, Boxedwine)

A data das pastas muda quando o jogo cria arquivos, e com ela a senha. A solução
**validada** no Linux, macOS e Android é fixar a data de `WINDOWS` e
`WINDOWS\SYSTEM` no instante **01/01/2000 14:00 UTC** e usar o `eli.cod` feito para
esse instante (`linux/eli.cod`). Com ele, a contra-senha tipo 9 é sempre
`959-240-641-242-102`. No Android é preciso fixar **as duas** pastas.

---

## 2. Atributos do jogador que vêm do nome

**Validado** em 1.428 registros de jogadores de dois saves (100% de acerto) e no
exemplo do TurboScore (Ryan Giggs). Rotina: `seg03:7516`–`seg03:766d`.

**S** = soma dos códigos (Latin-1) de todos os caracteres do nome, com a 1ª letra
maiúscula, como aparece no jogo (espaços e acentos contam).

| Atributo | Fórmula | Valores |
|---|---|---|
| Nota | `S mod 10 + 1` | 1 a 10 |
| Lesão | `S mod 11` | 0 a 10 (0 = não se lesiona; 10 = lesiona muito) |
| Comportamento | `5 − ⌊√(S mod 36)⌋`; se for **defesa**: `(c + 2) mod 6` | 0 a 5 |
| Estrela (✱) | Nota ≥ 8 **e** posição Meio ou Avançado | sim/não |

Comportamento: 0 Fair Play, 1 Cordeirinho, 2 Cavalheiro, 3 Caneleiro,
4 Caceteiro, 5 Sarrafeiro.

- Não dependem do time nem da força: o mesmo nome tem os mesmos valores em
  qualquer equipe. Só o comportamento (regra da defesa) e a estrela dependem
  também da posição.
- Exemplo: **Ryan Giggs** → S = 939 → Nota 10, Lesão 4, Caceteiro (Avançado),
  com estrela.

---

## 3. Arquivos de equipe (.EFT) e saves (.e98)

Implementado em `src/SaveCodec.cs`. **Validado** em centenas de registros.

- Cada equipe começa com `EFa\0`. O corpo é cifrado com uma soma rolante:
  `plano[i] = (cifrado[i] − delta) mod 256` e depois
  `delta = (delta + plano[i] − 0x20) mod 256`.
- O save `.e98` contém as equipes no mesmo formato, com um registro maior por
  jogador.
- O nome é guardado com a 1ª letra minúscula.

### Registro do jogador no save

Tamanho = `NL + 55` (NL = tamanho do nome). Os 50 bytes finais (`t0` = byte no
deslocamento `tamanho − 50` do registro, bytes crus):

| Byte | Campo | Como foi comprovado |
|---|---|---|
| t0 | posição (0 G, 1 D, 2 M, 3 A) | validado |
| t1 | estrela (0/1) | validado |
| t2–t3 | força (16 bits) | validado |
| t4–t5 | contador v da evolução de força | validado (0 em todos os jogadores: é zerado antes de cada rodada) |
| t8 | escalação: 1 titular, 2 reserva, 0 fora | validado (tela do time) |
| t12 | golos (carreira) | validado (Historial) |
| t14 | 1 = salário já revisto nesta temporada | validado (os 11 com t14 = 1 tiveram o salário mudado entre dois saves; nenhum dos 544 com t14 = 0) |
| t15 | jogos de suspensão restantes (o "S" ao lado do jogador) | validado ("Suspenso por 3 jogos" = 3; "S" aparece só com t15 > 0) |
| t17 | comportamento | validado |
| t19 | jogos de lesão restantes (o "L" ao lado do jogador) | validado ("L" aparece só com t19 > 0) |
| t21 | Lesão (atributo 0–10) | validado |
| t23 | Nota (1–10) | validado |
| t25–t26 | salário (16 bits) | validado |
| t29 | 2 nos jogadores estrangeiros, 0 nos demais | validado (1.428 registros, sem exceção) |
| t32 | jogos | validado (Historial) |
| t36 | golos esta época | validado (Historial) |
| t40 | lesões | validado (Historial) |
| t44 | cartões vermelhos | validado (Historial) |

Mapa completo (`seg03:7183`–`7346`, leitura do jogador): os 48 bytes de t0 a
t47 são os campos em memória `+0x04` (2), `+0x0A` força (2), `+0x0C`..`+0x13`
(v, `+0x0E`, escalação, `+0x12`), `+0x14` (2) e `+0x16` (1), `+0x27` suspensão,
`+0x2D` comportamento, `+0x29` jogos de lesão, `+0x2F` Lesão, `+0x2B` Nota,
`+0x31` salário (4), `+0x35` (1), `+0x36` (2), `+0x17` jogos (4), `+0x1B` golos
(4), `+0x1F` lesões (4), `+0x23` vermelhos (4), nessa ordem. As rotinas de
expulsão e de lesão somam exatamente em `+0x23` e `+0x1F`.

### Bloco de cada equipe no save

**Validado** nos dois saves analisados (45 equipes).

- Começa com `EFa\0`, seguido de zeros até o byte `0x32`. Os 2 bytes antes de
  `EFa` são o **identificador** da equipe.
- Em `0x32`: **nome completo** e **nome curto**, cada um texto Pascal (1 byte de
  tamanho + letras) cifrado com a soma rolante, começando do zero em cada
  texto.
- Logo depois: **cor da letra** e **cor do fundo**, 4 bytes cada (vermelho,
  verde, azul, 0), em claro. Conferido em 7 equipes (ex.: São Paulo letra
  branca, fundo vermelho).
- Depois: **país** (texto Pascal cifrado), seguido de 1 byte e das estatísticas
  da temporada em palavras de 2 bytes: vitórias, empates, derrotas, gols pró,
  gols contra, pontos (conferido com a tela de Classificação).
- **Moral**: real de 6 bytes, 74 bytes depois do início do país.
- **Dinheiro em caixa**: inteiro de 4 bytes, 142 bytes depois do início do país
  (conferido: 19.267.484 = "19 milhões e 267 mil").
- **Estádio**: 1 byte **N** (capacidade = 5000 × N) seguido de um real de 6
  bytes, imediatamente antes dos 2 bytes do identificador da equipe seguinte
  (ou seja, 9 bytes antes do `EFa` seguinte). Depois da última equipe vem a
  quantidade de equipes (2 bytes) e a lista dos identificadores. Conferido:
  Atlético PR N = 2 = "10000 lugares" na tela do estádio.

**Validado — compra de bancada**: ao clicar em "Construir" (Atlético PR, 10.000
lugares, preço 350 mil), o jogo grava na hora N = 3 (15.000 lugares), volta o
real do estádio para 0,6, desconta 350.000 do dinheiro e registra −350.000 numa
linha de despesas da temporada. O aviso "só estarão disponíveis na próxima
jornada" significa que a capacidade nova vale a partir do próximo jogo.

**Limite do estádio** (lido no código): o botão "Construir" só fica ativo se o
dinheiro cobre o preço **e** N < 24 (`cmp byte [N],0x18` em `seg07:29dd`). O
desenho do estádio tem três anéis de 6, 8 e 10 blocos (`seg07:2d2b`–`2d76`:
N, N−6, N−14), que somam exatamente 24. Logo o máximo pelo jogo é **N = 24 =
120.000 lugares**. A capacidade é calculada em 32 bits (`N × 5000`,
`seg03:bf35`), sem estouro.

Jogadores com **S** (suspenso) ou **L** (lesionado) ao lado do nome mostram
quantos jogos faltam e não podem ser relacionados para uma partida.

**Validado — editar S/L no save**: zerar t15 de Jorginho (Atlético PR, S = 3) e
t19 de João Santos (Santos, L = 10) faz o jogo mostrá-los sem S/L na tela de
plantel ao carregar; Jorginho aparece escalado. Grêmio, sem edição, continuou
mostrando os seus S/L (controle). A força perdida na lesão não volta.

---

## 4. Regras da partida

### Números aleatórios

O jogo usa o `Random` do Delphi: semente em `DS:0x247c`, gerador congruente
`semente = semente × 0x08088405 + 1`. `Random(N)` = `seg25:212d`.
**Código.**

### Sorteio do jogador envolvido num evento

`seg03:100b`: soma um peso de cada jogador elegível, sorteia `Random(soma)` e
escolhe o jogador em cuja faixa o número cai. A chance de cada jogador é
**peso dele ÷ soma dos pesos**. Os pesos (`seg03:0f2e`) são calculados em
16 bits. **Código.**

| Evento | Peso de cada jogador |
|---|---|
| Expulsão | Comportamento (0–5) |
| Lesão | Lesão (0–10) |
| Autor do gol | posição² × força × Nota |

Consequências diretas:

- **Fair Play (0) nunca é expulso.** Um Sarrafeiro (5) tem 5 vezes a chance de um
  Cordeirinho (1) de ser o escolhido.
- **Lesão 0 nunca se lesiona.** Lesão 10 tem 10 vezes a chance de Lesão 1.
- **Goleiro nunca marca gol** (posição 0 → peso 0). Entre os outros, o peso da
  posição é Defesa 1, Meio 4, Avançado 9.
- O peso do autor do gol é calculado só com os 16 bits de baixo de cada
  multiplicação (máximo 65.535): com força muito alta (ex.: 9999 posta por um
  editor) o peso "dá a volta" e vira um valor sem relação com a força, que pode
  ficar enorme ou pequeno conforme a Nota e a posição.

### Força

A força usada pelo motor da partida é o campo `+0x0A` do jogador em memória.
**Validado**: a rotina de lesão tira 10 desse campo (mínimo 1) e, no jogo, o
jogador lesionado perde 10 de força.

### Evolução de força

**Código.**

- **A cada minuto de cada jogo** (`seg03:afff` → `seg03:49e2`), para cada um dos
  dois times, o jogo sorteia **um jogador do elenco** e mexe num contador dele
  (v, campo `+0x0C`): **−1 se ele está em campo, +1 se não está**.
- **Depois de cada rodada** (`seg05:0d22` → `seg03:0d9c` → `seg03:6fb8` →
  `seg03:4a41`), para cada divisão e para o time na posição i da
  **classificação** (i = 0 para o líder), o jogo calcula um alvo
  `B = D − 2 × i` e, para cada jogador desse time:
  - se **v > 0**: força +1 (goleiro só com 50% de chance), desde que não passe de
    **50** nem de **B + 5**;
  - se **v < 0**: com 50% de chance, força −1, desde que não fique abaixo de
    **1** nem de **B − 5**;
  - se v = 0: nada.
- **Antes de cada rodada** (preparação `seg05:0ca1` → `seg03:13e1` → `a67f` →
  `3e9c`), v é **zerado** junto com a escalação dos jogadores. **Validado**: v
  é 0 para todos os jogadores nos saves.
- A classificação usada é a **já atualizada** com os resultados da rodada: a
  ordenação (`seg05:38b8`, pontos e depois saldo de gols) roda logo antes da
  evolução.

**Validado**: o D de cada divisão está no save, logo antes do nome dela
(`seg03:6b37` lê 4 bytes, 2 bytes do D, o nome com 21 bytes, a quantidade e a
lista de times). No save analisado: **1ª divisão D = 52, 2ª = 38, 3ª = 24,
4ª = 10**, com 8 times cada. A lista de times de cada divisão no save está
exatamente na ordem da tela "Classificação" (conferido nas 4 divisões) e muda de
uma rodada para a outra. Cada time é identificado pelos 2 bytes logo antes do
seu bloco `EFa`.

Na prática, os alvos formam uma escada contínua: o líder da 1ª divisão mira 52
(o teto é 50) e o lanterna 38; o líder da 2ª mira 38 e o lanterna 24; e assim
até o lanterna da 4ª, que mira −4 (o mínimo é 1). Nos dados reais, a força
média dos times segue essa escada.

### Escalação (`seg03:4b73`)

**Código.** Todas as escalações automáticas usam a mesma rotina:

1. embaralha o elenco (`seg14:0f68`) e o ordena por **força + gols marcados**
   (um dos contadores de gols do jogador) — empates ficam na sorte;
2. jogadores com **S** (suspensos) ou **L** (lesionados) ficam de fora;
3. percorre a lista na ordem e escala como titular quem couber nas vagas de cada
   setor, até **1 goleiro e 10 jogadores de linha**; quem já estava escalado é
   mantido e conta nas vagas;
4. monta o banco: primeiro **um reserva por setor** que tenha jogadores sobrando,
   depois os melhores restantes de linha, até **5 reservas**.

Quem chama e com que vagas (goleiro / defesa / meio / avançado):

| Origem | Vagas |
|---|---|
| Teclas F1–F12 (formações) | 1 / os números da formação (ex.: F1 = 1/3/3/4) |
| "Melhores" (M) | 1 / 10 / 10 / 10 — os 10 melhores de linha, sem formação |
| "Automático" (A) | mesmo resultado do "Melhores" (a rotina refaz a escalação do zero na 2ª chamada; **validado**: no jogo, A e M deram a mesma escalação) |
| Times do computador (`seg03:4388`) | refeita do zero a cada rodada; 1ª etapa 1 / 3 / 3 / 2; 2ª etapa completa com os melhores de qualquer posição (1 / 10 / 10 / 10) |

### Formações permitidas (`seg04:29ab` → `seg03:4b3e` → `seg03:0e4c`)

No menu "Seleccionar" da janela do time (`TprepareGameDlg`), cada formação
F1–F12 fica habilitada só se o elenco tiver, **sem contar quem tem jogos de
suspensão (S) ou de lesão (L)**, pelo menos **1 goleiro** e os defesas, médios
e avançados da formação. "Automático" e "Melhores" não passam por essa conta.
**Validado**: com 2 G, 5 D, 5 M e 2 A disponíveis (um médio com S e um avançado
com L), o jogo acinzentou 3-3-4, 3-4-3, 4-2-4, 4-3-3, 5-2-3, 6-3-1 e 6-4-0,
exatamente o que a regra prevê.

Na lista de jogadores dessa janela, os grupos G, D, M e A vêm nessa ordem e a
última linha de cada grupo é mais alta (16 → 20 pixels, por causa do traço
separador); S e L aparecem numa coluna entre a força e o salário. **Validado**
lendo a lista no Boxedwine (o app Android usa isso no botão "Tát").

### Tática 5-0-5 (tecla T) — patch do launcher

O menu "Seleccionar" original tem só as 12 formações F1–F12. O
`tools/patch_505.py` acrescenta o item **5-0-5** com a tecla **T**, sem mexer nas
12: o item usa o mesmo evento do 6-4-0, que passa a olhar quem o chamou e escala
5-0-5 com a rotina do próprio jogo (`seg04:2E12`). **Validado** no jogo (Linux):
o menu mostra "5-0-5 T" e a tecla T escalou 1 G, 5 D e 5 A; F5 continuou dando
4-4-2. O item novo fica sempre ativo (o jogo não o confere como faz com F1–F12).

Curiosidade do código original: a conferência do 6-4-0 (`seg04:2B6D`) usa
6-4-**1**, então o 6-4-0 fica cinza quando o elenco não tem nenhum avançado.

Dentro de cada posição, a lista mostra os jogadores em **ordem alfabética**, e
não na ordem em que estão no save (a do save muda de uma rodada para a outra).
**Validado** na tela do SC Palmeiras: Cláber, Edimilson, Júnior, Neném,
Roque Jr; Amaral, Euller, Galeano, Leandro, Marquinhos, Rogerio; Alex, Oséas,
Viola — enquanto no save a ordem era outra (Oséas, Viola, Júnior…). Os editores
de save do launcher usam a mesma ordem.

### Substituições automáticas (`seg03:67f7`)

**Código.** Chamada logo depois de cada lesão e de cada expulsão. Só acontece se
o time ainda tem substituições: o número de substituições é o de reservas,
limitado a **3** (`seg03:4e9c`).

- **Lesão**: entra o primeiro reserva da lista que seja do mesmo tipo do
  lesionado (goleiro por goleiro, linha por linha).
- **Expulsão**: só se o expulso for o **goleiro**. Entra o goleiro reserva e sai
  o último titular de linha da lista; o time fica com 10. Expulso de linha não é
  substituído.
- Depois da troca, a força de cada setor é recalculada.

### Força de cada setor (`seg03:397d`)

Para cada jogador **em campo**, o setor dele (G, D, M ou A) recebe
`força + 2 × estrela + 3 × (nacionalidade do jogador = país do clube)`.
**Código.**

### Relógio da partida (`seg10:069b`)

**Código.** O relógio começa em −5 (minutos sem eventos) e vai até **90**. No
minuto 45 há o intervalo. Em dias com prorrogação, no minuto 90 os jogos que
precisarem continuam até **120**.

### Ordem dos eventos a cada minuto (`seg10:069b`)

**Código.** A cada minuto do relógio, para cada jogo, o jogo tenta nesta ordem e
para no primeiro que acontecer (no máximo um evento por jogo por minuto):

1. pênalti / gol (`seg03:a6eb`);
2. expulsão (`seg03:aa11`);
3. lesão (`seg03:ac72`): `Random(500)` = 0 → mandante, = 1 → visitante
   (1 em 500 por minuto para cada time);
4. outras rotinas (`seg03:aea7`, `seg03:afff`, `seg03:143e`).

### Gol (`seg03:46df`, chamado a cada minuto por `seg03:a6eb`)

**Código.** A cada minuto `m` da partida (relógio `seg10:069b`), o jogo testa o
gol do mandante; o do visitante só é testado se naquele minuto não houve pênalti
nem gol do mandante.

Com `G, D, M, A` = força de cada setor do próprio time e `G', D', M', A'` = do
adversário (ver "Força de cada setor"):

```
R = t1 + t2 + t3          (confronto setor a setor, valores acumulados)
  a1 = 1 + D         b1 = 1 + A'               t1 =     a1 / (a1 + b1 + 1)
  a2 = a1 + M        b2 = b1 + M'              t2 = 2 × a2 / (a2 + b2 + 1)
  a3 = a2 + A        b3 = b2 + 3×G' + D'       t3 =     a3 / (a3 + b3 + 1)

Q = ln(1 + 4×(A + M/2) / (3×G' + D' + M'/2 + 1)) × R × (moral / 2)^(1/4)
```

O teste do minuto, com `S` = gols marcados + gols anulados do time nesta partida:

| | Mandante | Visitante |
|---|---|---|
| sai gol se | `Q − S > Random(90)` | `Q − S > Random(120)` |
| senão, gol "de sorte" se | `Random(180) = 0` | `Random(270) = 0` |

E o gol só vale se `Random(45) < m`: no 1º tempo, a chance cresce de 1/45 no
minuto 1 até sempre a partir do minuto 45.

Consequências diretas:

- **Q funciona como o número de gols "merecidos"**: a chance por minuto é
  proporcional a `Q − S`, então quando o time chega perto de Q gols a chance cai
  para quase só o gol de sorte.
- **Jogar em casa ajuda**: o visitante sorteia em 120 (e 270) em vez de 90 (e 180).
- O goleiro adversário pesa 3 vezes na defesa (`3×G'`).
- O moral multiplica Q pela raiz quarta: moral 2 (máximo) → ×1; moral 0,5 →
  ×0,71.
- Força alta tem retorno decrescente (logaritmo).

### Empate na Taça (`seg03:9a15` → `seg11:0d46`)

**Código.** Jogo da Taça empatado no fim dos 90 minutos vai para a prorrogação
(até 120). Se continuar empatado, é decidido nos pênaltis
(`TPenaltiesDlg`, "Penaltis"):

- cada time bate na ordem do **melhor para o pior batedor** (o mesmo valor P do
  pênalti), só entre os jogadores em campo, recomeçando a fila quando acaba;
- cada cobrança usa a **mesma conta do pênalti** (`seg03:81ed`) contra o
  goleiro adversário;
- **5 cobranças para cada lado**, terminando antes se um time não puder mais
  alcançar o outro; persistindo o empate, cobranças alternadas até desempatar.

Exceção: quando a opção `viewComputerPenalties` do `elif98.ini` está desligada e
nenhum dos times é humano, o desempate entre times do computador não é jogado:
cada time soma **3 sorteios** de `Random(força média do elenco)` e passa o
mandante se a soma dele for maior ou igual.

### Lances da partida

**Código.** Cada lance é guardado com o time, o minuto, o jogador, a posição dele
e o tipo (`seg03:c1bf`): **1** gol, **2** gol de pênalti, **4** expulsão,
**5** lesão, **6** gol anulado.

### Pênalti depois de expulsão (`seg03:a76e`)

**Código.** Se um time teve um jogador **expulso no minuto anterior** (e o minuto
atual não é o 1º de um tempo, ou seja, `minuto mod 45 > 1`), o adversário tem um
pênalti:

- **com certeza**, se o expulso era o **goleiro**;
- com **1 chance em 3**, se era um **defesa**;
- nunca, se era meio ou avançado.

### Gol anulado (`seg03:aea7`)

**Código.** A cada minuto (exceto 46 e 91): se `Random(200)` sair menor que 10
**e** maior que a influência do árbitro sobre o **mandante** (V do mandante), e
o lance do minuto anterior foi um **gol normal** (tipo 1, de qualquer time), esse
gol é anulado: o placar e os gols do autor voltam 1 e o lance vira tipo 6.

### Expulsão (`seg03:48be`)

- O expulso sai de campo.
- **Suspensão = `Random(4) + 1`: de 1 a 4 jogos.**
- Soma 1 aos cartões vermelhos do jogador.
- Depois de cada jogo do time, a suspensão de cada jogador cai 1 (mínimo 0),
  assim como os jogos de lesão (`seg03:56e4`).

### Lesão (`seg03:4940`)

- O lesionado sai de campo.
- **Jogos parado = `Random(Random(20)) + 1`: de 1 a 20 jogos**, com lesões curtas
  bem mais frequentes que longas (é um sorteio dentro de outro).
- Soma 1 às lesões do jogador.
- Perde 10 de força (mínimo 1).

### Pênalti automático (`seg08:39b6` → `seg03:81ed`)

**Código.** A cobrança é resolvida sempre pela mesma conta. Se o time que cobra
é o do jogador humano, o jogo abre a tela "escolha o jogador para marcar o
penalti!" (`seg08:328b`) e **o humano escolhe o batedor**; senão, o batedor é o
escolhido automaticamente.

- **Batedor automático**: o jogador **em campo** com o maior
  `P = arredondar((posição + 1) × força × (Nota/10 + 1) / 2)` (G=0, D=1, M=2, A=3).
  Não é sorteado.
- **Goleiro**: o jogador em campo com a menor posição (o goleiro, ou um defesa se
  não houver goleiro) e, entre esses, o de maior força.
- **Chute** = `Random(P do batedor)`; **defesa** = `Random(força do goleiro)`.
- Em 1 de cada 4 cobranças (`Random(4) = 0`), um chute baixo tem um desfecho
  especial (códigos internos 3 a 6, sem gol): chute < 4, < 10, < 12 ou < 13.
- Nos demais casos: **gol se chute ≥ defesa**; senão, defesa.

### Público (`seg03:b0b0`–`b224`)

**Código.** F(time) = média arredondada da **força** de **todos** os jogadores do
elenco (titulares e reservas). W(time) é o moral da equipe.

```
base   = 3 × F(casa) + F(visitante) + 3 × RandomReal     (RandomReal entre 0 e 1)
público = (base × √W(casa) × √W(visitante) × 200) ^ 1,04
```

- Nos **jogos da Taça** o público é multiplicado por **1,3**. (Tipo do jogo,
  `seg03:8db2`: 0 = "Nª Jornada" do campeonato, 1 = "TAÇA - …", 2 = entrada do
  calendário sem times.)
- **Limite do estádio** (`seg03:c0c0`, `seg03:bf35`): o público final é o menor
  entre o valor calculado e a capacidade do estádio do mandante, que é sempre um
  múltiplo de 5000 (`5000 × N`, com N guardado no estádio).
- Entradas do calendário sem times (tipo 2) não têm público.
- O time da casa pesa 3 vezes mais que o visitante.
- Por isso um jogador com força absurda (ex.: 9999) lota o estádio: ele sozinho
  multiplica a média do elenco.

### Moral

O moral de cada equipe é um número real guardado na equipe (campo `+0x5B` em
memória; no save, byte 74 do cabeçalho da equipe, formato `Real` de 6 bytes do
Delphi 1). **Validado**: as barras `gaugeMoral` / `gaugeOpponentMoral` da tela do
time recebem `arredondar(moral × 10)`, numa escala de 0 a 20 (moral de 0 a 2).
Ex.: moral 0,8425 → 8/20; moral 1,1882 → 12/20 (medido na tela).

O moral entra no público (`√moral` de cada time) e na chance de gol.

**Atualização depois de cada jogo** (`seg03:94dd`–`9c44`, `seg14:10d1`). **Código.**
O moral **não é calculado a partir da força**: só o resultado conta.

```
moral novo = 0,85 × moral + 0,15 × alvo
```

| Jogo | Resultado | Alvo do mandante | Alvo do visitante |
|---|---|---|---|
| Campeonato | vitória do mandante | 2,0 | 0,0 |
| Campeonato | empate | 0,8 | 1,3 |
| Campeonato | vitória do visitante | 0,0 | 2,5 |
| Taça | passa o mandante | 1,5 | 0,0 |
| Taça | passa o visitante | 0,0 | 2,0 |

- O moral de uma equipe nova é 1,0, e volta a 1,0 quando a equipe troca de
  treinador (`seg03:5148`–`51b3`).
- Vencer fora vale mais que vencer em casa; empatar em casa baixa o moral e
  empatar fora sobe.
- A força influi só de forma indireta: quem é mais forte vence mais.

### Árbitro (`seg03:8e52`–`8eff`, `seg03:c95c`)

Cada jogo sorteia um árbitro da lista (`REFEREE.TXE`). Para cada time, o jogo
calcula a **influência** do árbitro. **Validado** (diálogo "Tendência do Árbitro":
Mário Santos × Bahia/Atlético PR mostrou 11/15 e 10/15, igual ao calculado):

1. soma, posição a posição, `letra do nome do árbitro − letra do nome do time`,
   do fim para o começo, até o tamanho do nome mais longo; letras que faltam no
   nome mais curto valem 0. Os nomes são os mostrados no jogo (o do time em
   maiúsculas, ex.: `ATLETICO PR`);
2. influência = `|soma mod 11|` (0 a 10);
3. **+3 se o árbitro for do mesmo país do time.**

O mandante recebe ainda **+2** (fator casa). Esse valor final (V, de 0 a 15) é o
que a **balança** do diálogo "Tendência do Árbitro" mostra para cada time
(`gaugeHomeAid` / `gaugeAwayAid`), e controla, a cada minuto:

- **Pênalti a favor**: `Random(6000) < V` (mandante) ou `Random(7000) < V`
  (visitante). Quanto maior V, mais pênaltis a favor.
- **Expulsão**: `Random(1000)` sai menor que 10 **e** maior que V. Quanto maior V,
  menos expulsões; com V ≥ 9 o time **nunca** tem jogador expulso.


---

## 5. Economia

### Inflação

- É um número real global (`DS:0x2B46`), gravado no save logo depois do **ano
  da temporada** (inteiro de 4 bytes). O save começa com um texto cifrado de
  tamanho variável (1 byte de tamanho); o ano fica 7 bytes depois dele
  (`1 + tamanho + 7`) e a inflação (real de 10 bytes) logo em seguida.
  Conferido em dois saves: texto de 30 caracteres → ano no byte 38; de 14 →
  byte 22. **Começa em 2,0** (`seg11:3680`).
- A tela de Opções mostra `arredondar(inflação × 10)` (`seg06:2f8f`): por isso
  começa em **20**. Limites: 0,5 a 10 (5 a 100 na tela).
- **Muda a cada rodada**, não a cada temporada (`seg05:0d3c`), logo depois da
  evolução de força, com r₁, r₂, r₃ sorteados entre 0 e 1:
  - ano da temporada terminado em **00 a 49** (ex.: 2026–2049):
    `inflação += 0,01 × (r₁ + r₂ − r₃)` → **sobe** em média 0,005 por rodada;
  - ano terminado em **50 a 99**: `inflação += 0,01 × (r₁ − r₂ − r₃)` → **cai**
    em média 0,005 por rodada.
  - O ano da temporada (`DS:0x2B42`) começa no ano da data do computador e soma
    1 a cada temporada.
- Com 14 rodadas por temporada, sobe em média 0,07 (0,7 na tela) por temporada
  na fase de alta, com variação aleatória: por isso não sobe "1 por ano".
- **Validado**: num save a inflação estava em 2,0019 e, uma rodada depois (2026),
  em 2,0096 (+0,0077, dentro do esperado).

### O que a inflação multiplica

**Código.** Todos os valores em dinheiro abaixo são diretamente proporcionais à
inflação. "Moeda" é um fator global da moeda escolhida (`DS:0x2D87`; com reais
os salários do save são todos múltiplos de 250, compatível com moeda = 5).

- **Salário** (`seg03:7ff4`):
  `≈ 35 × moeda × inflação × força^1,04 × 1,318 × √(valor do clube)`,
  arredondado para múltiplo de `50 × moeda`, entre `50 × moeda` e
  `50000 × moeda`. O salário fica em 4 bytes (t25–t28 do registro do jogador).
- **Valor do jogador** (`seg09:371a`):
  `2000 × moeda × inflação × força^1,04 × (1 − 0,04×comportamento − 0,02×Lesão + 0,02×Nota)`.
  Comportamento e Lesão desvalorizam (até −20% cada); Nota valoriza (até +20%).
- **Preço pedido pelos times do computador** (`seg03:3c90`): valor × 1,6.
- **Valor ligado ao clube** (`seg09:3899`):
  `força média do elenco × moral × inflação × moeda × 20000`, arredondado.

---

## 6. Treinadores

### No save

- **Lista de treinadores** logo no início do save (antes das equipes). Cada
  registro: id (2 bytes), nome (string Pascal cifrada: cada byte = letra + byte
  anterior, começando pelo byte do tamanho), 1 byte **humano** (1 = humano,
  0 = computador), contadores e o histórico (entradas com tipo, ano e id da
  equipe; ex.: "05, 2026, 659" = entrou no Atlético PR em 2026).
  Validado: ao adicionar o treinador "Teste" em *Treinador → Gestão de
  treinadores*, o jogo grava um registro novo com id 988, humano = 1 e sem equipe.
- **Treinador da equipe**: 2 bytes com o id do treinador, 76 bytes depois do fim
  do país no bloco da equipe. Confere em 43 das 45 equipes de um save
  (ex.: Bahia → Jair Pereira, Atlético PR → Juan Silveira). O registro do
  treinador não guarda a equipe; o jogo procura a equipe pelo id (`seg03:d55c`).
- **Cabeçalho**: depois da string do cabeçalho vêm 2 bytes, o **id da equipe do
  humano da vez** (em `1 + tamanho + 2`) e o **próximo id de treinador** (2 bytes,
  passou de 988 para 989 ao adicionar "Teste").

**Validado — trocar de equipe pelo save**: trocar os ids de treinador entre
Atlético PR e Santos e pôr o id do Santos no campo "equipe do humano da vez" faz
o jogo abrir com Juan Silveira no Santos (1ª Divisão), parado na tela da 3ª
jornada, como num jogo normal. Sem mudar esse campo, o jogo carrega e já disputa
a rodada (a equipe "da vez" virou do computador).

**Validado — humano no Distrital quebra o jogo**: pôr o treinador humano numa
equipe do Distrital (Araçatuba, no Android) faz o jogo carregar, disputar a
rodada e parar com "List index out of bounds.". Numa equipe da 1ª Divisão
(Botafogo, no Android; Santos, no Linux) o jogo abre normalmente.

**Divisões no save**: depois da lista "quantidade + ids em ordem crescente" vêm a
quantidade de divisões (2 bytes) e, para cada uma, 4 bytes, o D (2 bytes), o
nome (string de 20: "1ª Divisão" … "4ª Divisão", "Distrital"), a quantidade de
equipes e os ids. Conferido em três saves: 8 equipes em cada divisão e 13 no
Distrital (D = −4).

A lista de antes nem sempre é "todas as equipes em ordem crescente". Em 26
saves conferidos:
- em alguns ela tem os ids fora de ordem;
- em outros ela só tem as 32 equipes das 4 divisões;
- alguns saves têm um registro a mais que começa como equipe (`EFa\0`), mas não
  tem treinador e não está em nenhuma divisão.

Por isso os editores acham a tabela de divisões pela própria estrutura:
quantidade de divisões, nomes de até 20 letras e ids de equipes do save sem
repetir. Juntas, as divisões cobrem todas as equipes.

### Chicotada psicológica (`seg03:4f40`)

- A equipe que troca de treinador recebe o escolhido por `seg03:5343`; se ele
  já treinava outra equipe, as duas **trocam de treinador**. As duas ficam com
  moral 1,0 e o histórico dos treinadores ganha uma entrada.
- `seg03:5343` percorre todos os treinadores e monta a lista de candidatos:
  se a média de força do elenco é **menor que 15**, entram os **desempregados**;
  senão, treinadores de outras equipes que passem por `seg03:5661` (compara
  moral e média de força). Em alguns casos (flag 0x400) os **humanos ficam de
  fora**. A lista é ordenada e vale o primeiro que passa em `seg03:8bcc`.
- A janela "Chicotadas Psicológicas" só mostra a lista de trocas da rodada
  (`DS:0x2716`, `seg05:025b`).

## 7. Editor de Equipas (EDITEQ.EXE) e arquivos .EFT

### Formato do .EFT

**Validado nas 282 equipes de `EQUIPAS`** (todas lidas até o último byte) e numa
equipe gravada pelo próprio editor:

| Campo | Formato |
|---|---|
| Marca | `EFa` + byte 0 |
| Reservado | 46 bytes zerados |
| Nome completo, nome abreviado | strings Pascal cifradas |
| Cor da letra, cor do fundo | 4 bytes cada (R, G, B, 0) |
| País da equipe | string Pascal cifrada (código de 3 letras) |
| Nível inicial | 1 byte (1 a 20) |
| Quantidade de jogadores | 2 bytes |
| Cada jogador | país (string), nome (string), posição (2 bytes: 0 G, 1 D, 2 M, 3 A) |
| Treinador | string Pascal cifrada |

Cada string é cifrada sozinha: o byte do tamanho fica em claro e cada letra é
gravada somada ao byte cifrado anterior (o primeiro soma o tamanho). Os números
ficam em claro.

### O que o editor faz

- **Ficheiro**: Nova equipa (assistente: nome, cores, país, nível, treinador e
  depois os jogadores), Abrir (lista as equipes pelo nome; formatos "Elifoot 98"
  e "Elifoot Windows 1.x"), Próxima equipa, Sair. Não há "Gravar": ao trocar de
  equipe ou sair, ele pergunta se grava e o nome do arquivo (até 8 letras).
- **Jogador**: Adicionar (a janela reabre para o próximo até chegar a 20),
  Editar (nome, posição, país), Remover, Transferir (para outra equipe; só vale
  quando essa equipe for aberta).
- **Equipa**: Nome, Cores (grade de 16 cores; botão esquerdo = texto, direito =
  fundo), País, Nível inicial (1 a 20, "servirá para a ordenação das equipas
  pelas várias divisões"), Treinador.
- A lista mostra só nome, posição e (para estrangeiros) o país.
- **O número no canto** é o campo `MemoryAvailable` do formulário: memória livre
  (`MemAvail` do Delphi, que chama `GetFreeSpace`). Não tem relação com a
  equipe; muda com o ambiente (102.407.344 no Linux, 2.080.248.852 no Android) e
  cai 192 bytes a cada jogador adicionado (medido).

### Nível inicial (`seg06:0067`–`023b`)

Ao começar um jogo novo, o jogo percorre os níveis **de 20 até 1**; em cada
nível, lê os arquivos de `EQUIPAS` e acrescenta as equipes daquele nível cujo
país foi escolhido e ainda tem vaga (a vaga do país cai 1 a cada equipe). Assim,
**as equipes de nível maior entram primeiro e ficam nas divisões mais altas**;
as últimas vão para o Distrital ou ficam de fora se acabarem as vagas do país.
Empate de nível: vale a ordem em que os arquivos são lidos.

**Validado** em dois saves (Brasil, 45 equipes): níveis por divisão
1ª = 16, 14, 14, 13, 13, 12, 12, 12; 2ª = 11, 10, 9, 9, 8, 7, 7, 7;
3ª = 6, 6, 6, 5, 5, 5 (…); 4ª = 5, 4, 4, 3, 3, 2, 2 (…); Distrital = 2 e 1.
O nível não é gravado no save; depois disso a divisão muda por subida e descida.

### Regras para gravar uma equipe (`seg12:275e` no ELIFOOT.EXE)

Validado também no editor (Xvfb): recusou com 13 jogadores, gravou com 14;
"Adicionar" fica desativado com 20; recusou com 6 estrangeiros.

| Regra | Mensagem |
|---|---|
| nome completo e abreviado preenchidos | Não está definido o nome da equipa |
| país existe em `COUNTRY.TXE` | Não está definido o país da equipa |
| nível válido | Não está definido o nível da equipa |
| 14 a 20 jogadores | Não tem jogadores suficientes / Tem demasiados jogadores |
| pelo menos 1 guarda-redes | Não tem guarda-redes |
| pelo menos 10 jogadores de campo | Não tem jogadores de campo suficientes |
| **no máximo 5 estrangeiros** | Tem demasiados jogadores estrangeiros |
| treinador com nome | Não está definido o treinador |

### Estrangeiro, Lei Bosman e PLOP (`seg12:0a2f`)

Dados o país do jogador e o da equipe: **mesmo país = nacional**; se **os dois**
estão em `CTRGROUP/BOSMAN.TXE` ou **os dois** em `CTRGROUP/PLOP.TXE`, o jogador
**não conta como estrangeiro** ("ao abrigo da Lei Bosman"); senão é estrangeiro.
Os arquivos são listas de strings na cifra dos `.TXE` (a mesma do REFEREE.TXE):

- BOSMAN (original do jogo): POR ESP FRA ITA ALE AUT GRE ING ESC WAL ILN IRL DIN BEL HOL LUX FIN SUE
- PLOP (língua portuguesa): POR BRA ANG MOC CAV STP GBI GNE TIM

O `COUNTRY.TXE` original tem 217 países ("AFG Afeganistão" …), e a bandeira de
cada um está em `FLAGS/<código>.BMP`, em BMP de 16 cores, 39 × 29.

O launcher usa os arquivos do patch do Turbo Elifoot (`turbo_patch_934_27`):
- **COUNTRY.TXE**: 258 países, com 41 novos (Kosovo, Gibraltar, Hong Kong…) e
  sem SMO (Samoa Ocidental). Nenhuma equipe, jogador ou árbitro original usa
  SMO.
- **BOSMAN.TXE**: a UE atual mais GBR, ING, ESC, WAL e ILN (32 códigos).
- **Bandeiras**: BMP de 24 bits, 39 × 29.

**Validado** no jogo (Android): a seleção de países mostra os nomes novos e
as bandeiras de 24 bits.

## Autoverificação do executável (`seg12:39D9` → `seg12:3825`)

**Validado.** Ao abrir, o jogo lê o próprio `ELIFOOT.EXE` e compara os **4
últimos bytes** do arquivo com uma soma do resto dele:

```
soma = Σ byte[i] × ((i mod 6000) mod 5 + 1)   para i de 0 até tamanho − 5   (32 bits)
```

(o arquivo é lido em blocos de 6000 bytes; o peso recomeça a cada bloco). A soma
calculada assim bate com a guardada no EXE original (`0x0CD8400F`). Com qualquer
byte alterado (até um zero de preenchimento sem uso) e a soma antiga, o jogo fica
na tela verde e não mostra a "Acerca"; com a soma recalculada, abre normalmente.

## Ainda não comprovado (fora deste documento)

- O "valor do clube" que entra no salário (real de 10 bytes passado a
  `seg03:7ff4`) e o que exatamente a conta de `seg09:3899` representa na tela.
- t48–t49 do registro do jogador (provável identificador) e os campos `+0x04`,
  `+0x0E`, `+0x12`, `+0x36` em memória.
- A rotina que ordena a lista da janela do time (a ordem alfabética foi
  observada na tela, mas o código não foi lido) e o critério de desempate de
  nomes acentuados.
- O Score e o Impacto do Scout não são do jogo: saem de simulações com as regras
  deste documento (`docs/score.md`, `tools/sim/sim.c`), não de partidas reais.
