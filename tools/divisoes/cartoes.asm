; Cartoes amarelos (seg03). Cada parte e montada sozinha com -DPARTE=n, no seu
; endereco. Ver cartoes.py e docs/elifoot98-interno.md ("Cartoes amarelos").
;
; No jogador (campos de memoria; no save, t42-t47):
;   +0x21  word  amarelos na carreira (os 2 bytes de cima das lesoes)
;   +0x25  byte  amarelos acumulados (os 2 bytes de cima das expulsoes)
;   +0x26  byte  marca do jogo do ultimo amarelo (1..255)
bits 16

%ifndef LIMITE
%define LIMITE 50          ; Random(1000) por minuto e lado: < VERM vermelho; VERM..LIMITE-1 amarelo
%endif
%ifndef VERM
%define VERM 5
%endif
%define JORNADA 0x2B40
%define ANO     0x2B42


%if PARTE == 1
; ---- seg03:aa40, dentro do aa11 (eventos de expulsao de cada minuto): um
; sorteio para o mandante e outro para o visitante (o original usava o mesmo
; numero para os dois, e o visitante quase nunca era punido). Cai no aaad com
; [bp-1] = houve cartao, [bp-8] = lado, [bp-6] = jogador escolhido.
org 0xAA40
        mov word [bp-8], 0
.laco:  push word 1000
        db 0x9A                         ; call far Random (seg25:212D)
..@rnd: dw 0xFFFF, 0
        mov [bp-0xE], ax
        les di, [bp+6]
        mov bx, [bp-8]
        shl bx, 1
..@lim: cmp ax, byte LIMITE             ; opcao CARTOES: desligada, 10 (so vermelho)
        jge .prox
        cmp ax, [es:di+bx+0x4C]         ; influencia do arbitro sobre o lado
        jle .prox
        lea ax, [bp-6]
        push ss
        push ax
        shl bx, 1
        add di, bx
        les di, [es:di+0x2E]            ; equipa do lado
        les di, [es:di]
        push es
        push di
        push cs                         ; enchimento: a moldura fica igual a de antes
        call 0x48BE                     ; cartao (parte 2), rotina near (ret 10)
        mov [bp-1], al
        or al, al
        jz .prox
        jmp 0xAAAD
.prox:  inc word [bp-8]
        cmp word [bp-8], 2
        jb .laco
        jmp 0xAAAD

; Lance novo (seg03:c1f5, no lugar de "mov ax,[bp+10h]"): o tipo 4 (expulsao)
; vira 3 (amarelo) se o jogador continua em campo. Saida: ax = tipo, es:di = lance.
tipo:   mov ax, [bp+0x10]
        cmp ax, 4
        jne .r
        les di, [bp+0xC]
        les di, [es:di]
        cmp word [es:di+0x10], 0        ; escalacao: 0 = saiu (vermelho)
        les di, [bp+6]
        je .r
        mov al, 3
.r:     ret
fim1:
%endif

%if PARTE == 2
; ---- seg03:48be, no lugar da expulsao: cartao(equipa, var jogador). Escolhe
; o jogador como a expulsao (pelo comportamento; Fair Play nunca). Com o sorteio
; do aa11 ([bp-0Eh] dele) < VERM e vermelho, senao amarelo; o 2o amarelo no
; mesmo jogo vira vermelho 1 vez em 4. O 3o acumulado so da suspensao no fim
; do jogo (parte 9).
org 0x48BE
        push bp
        mov bp, sp
        les di, [bp+6]
        les di, [es:di+0xA]
        les di, [es:di]
        push byte 3
        push byte 1
        push es
        push di
        db 0x9A                         ; sorteio do jogador (seg03:100B), far com
..@sj:  dw 0xFFFF, 0                    ; relocacao, como no original
        les di, [bp+0xA]
        mov [es:di], ax
        mov [es:di+2], dx
        or ax, dx
        jz nada
        les di, [es:di]
        les di, [es:di]
        cmp word [es:di+0x2D], 0        ; comportamento
        jle nada
        mov bx, [bp]                    ; quadro do aa11
..@verm: cmp word [ss:bx-0xE], byte VERM ; opcao CARTOES: desligada, 10
        jl vermelho
        jmp AMARELO                     ; parte 5
vermelho:
        mov word [es:di+0x10], 0        ; sai de campo
        mov ax, [0x247E]                ; suspensao de 1 a 4 jogos pela semente do
        and ax, 3                       ; Random (RandSeed, DS:247C), ja mexida pelo
        inc ax                          ; sorteio do minuto
        mov [es:di+0x27], ax            ; 1 a 4 jogos de suspensao
        add word [es:di+0x23], 1        ; expulsoes (so os 2 bytes de baixo)
sim:    mov al, 1
        jmp short fim
nada:   mov al, 0
fim:    leave
        ret 10
fim2:
%endif

%if PARTE == 3
; ---- seg03:c3fa, no desenho da expulsao na tela da jornada: as contas do
; retangulo ficam mais curtas e a cor sai do tipo do lance: 4 vermelho, 3 amarelo
org 0xC3FA
        add word [bp-0x10], 3
        add word [bp-0xE], 3
        sub word [bp-0xA], 3
        les di, [bp+6]
        mov ax, [es:di+8]
        sub ax, 4                       ; 3 -> FFFFh, 4 -> 0
        or al, 0xFF                     ; 3 -> clYellow (00FFFFh), 4 -> clRed (0000FFh)
        push byte 0
        push ax
        times 0xC41A - 0xC3FA - ($ - $$) nop
%endif

%if PARTE == 4
; ---- seg03:7b5a, coluna S da lista de jogadores: "S" suspenso, "A" com
; amarelos acumulados, senao vazio (o original tinha 2 copias, uma por texto)
org 0x7B5A
        lea di, [bp-0x114]
        push ss
        push di
        les di, [bp+0x16]
        push es
        push di
        db 0x9A                         ; call far (seg25:196E)
..@c1:  dw 0xFFFF, 0
        mov di, 0x768A                  ; tabulacao
        push cs
        push di
        db 0x9A                         ; call far (seg25:19ED)
..@c2:  dw 0xFFFF, 0
        les di, [bp+6]
        mov ax, 0x768C                  ; "S"
        cmp word [es:di+0x27], 0
        jg .t
        mov ax, 0x7690                  ; vazio
        cmp byte [es:di+0x25], 0
        je .t
        mov ax, letraA
.t:     push cs
        push ax
        db 0x9A                         ; call far (seg25:19ED)
..@c3:  dw 0xFFFF, 0
        les di, [bp+0x16]
        push es
        push di
        push word 0xFF
        db 0x9A                         ; call far (seg25:1988)
..@c4:  dw 0xFFFF, 0
        jmp 0x7BC8
letraA: db 3, ' A', 9                 ; como os do jogo: " S" + tab, " " + tab

; Depois do lance (seg03:ab18, jmp no lugar de "les di,[bp+6]"): num amarelo o
; jogador fica em campo e nao ha substituicao nem forca a recalcular.
; Entra e sai por jmp.
pula:   les di, [bp-6]
        les di, [es:di]
        cmp word [es:di+0x10], 0
        les di, [bp+6]
        je .volta                       ; vermelho: segue no original
        jmp 0xAC6B                      ; amarelo: sem substituicao
.volta: jmp 0xAB1B
fim4:
%endif

%if PARTE == 5
; ---- fim do seg03 (acrescentado): o amarelo do cartao e os dois ajudantes.
; Montar com -DORG=<fim do seg03> -DVERMELHO=, -DSIM= e -DNADA= (da parte 2)
org ORG
AMARELO:
        mov cl, [JORNADA]               ; marca do jogo, em cl: 2*jornada + 1 (+1 na Taca,
        shl cl, 1                       ; +80 no ano impar); ch = sorteio do minuto. O ax e
        inc cx                          ; o dx voltam como o sorteio do jogador os deixou
        test byte [ANO], 1              ; +80 nos anos impares: a mesma jornada do ano
        jz .par                         ; seguinte nao passa por este jogo
        add cl, 80
.par:   push bp                         ; o jogo e o sorteio estao no quadro do aa11
        mov bp, [bp]
        les di, [bp+6]                  ; o jogo
        mov ch, [bp-0xE]
        pop bp
        cmp byte [es:di+0x2D], 1        ; jogo da Taca
        jne .liga
        inc cx
.liga:  les di, [bp+0xA]                ; o jogador outra vez, pela var
        les di, [es:di]
        les di, [es:di]
        cmp [es:di+0x26], cl
        jne .novo
        test ch, 3                      ; 2o amarelo neste jogo: o arbitro so expulsa
        jz .vermelho                    ; 1 vez em 4 (pelo mesmo sorteio do minuto)
        jmp NADA
.vermelho:
        inc word [es:di+0x21]           ; amarelos na carreira
        jmp VERMELHO
.novo:  inc word [es:di+0x21]
        mov [es:di+0x26], cl
        inc byte [es:di+0x25]           ; o 3o acumulado: suspensao no fim do jogo (parte 9)
        jmp SIM

fim5:
%endif

%if PARTE == 6
; ---- seg04:2614, aba Jogador da tela da equipa: o texto debaixo do Historial
; mostra "Suspenso por N jogos" ou, sem suspensao, "N amarelos acumulados".
; (Mesmas chamadas de texto do original; o texto "Suspenso por 1 jogo" deixa de
; ser usado: o lugar dele, cs:2266, guarda " jogo"; os outros vao no fim do seg04.)
org 0x2614
        call escolhe
        jz .mostra                      ; nada a mostrar
        lea di, [bp-0x308]
        push ss
        push di
        push cs
        push si                         ; prefixo
        db 0x9A                         ; call far (seg25:196E)
..@t1:  dw 0xFFFF, 0
        lea di, [bp-0x208]
        push ss
        push di
        call escolhe                    ; ax = numero
        cwd
        push dx
        push ax
        db 0x9A                         ; IntToStr (seg24:07D0)
..@t2:  dw 0xFFFF, 0
        db 0x9A                         ; concatena (seg25:19ED)
..@t3:  dw 0xFFFF, 0
        call escolhe                    ; bx = sufixo
        push cs
        push bx
        db 0x9A                         ; concatena (seg25:19ED)
..@t4:  dw 0xFFFF, 0
        mov di, [bp+4]
        les di, [ss:di+6]
        les di, [es:di+0x2F4]           ; o texto debaixo do Historial
        push es
        push di
        db 0x9A                         ; SetText (seg21:1D8C)
..@t5:  dw 0xFFFF, 0
.mostra:
        call escolhe
        mov al, 0
        jz .v
        cmp word [0x29DE], 0
        jng .v
        inc ax
.v:     push ax
        mov di, [bp+4]
        les di, [ss:di+6]
        les di, [es:di+0x2F4]
        push es
        push di
        db 0x9A                         ; SetVisible (seg21:1C77)
..@t6:  dw 0xFFFF, 0
        jmp 0x26B8

; escolhe: ZF = nada a mostrar; senao ax = numero, si = prefixo, bx = sufixo
escolhe:
        les di, [bp-0x108]              ; o jogador
        mov si, 0x2251                  ; "Suspenso por "
        mov bx, 0x225F                  ; " jogos"
        mov cx, JOGO
        mov ax, [es:di+0x27]
        or ax, ax
        jg .n
        mov si, VAZIO
        mov bx, AMS
        mov cx, AM1
        mov al, [es:di+0x25]
        xor ah, ah
        or ax, ax
        jz .r
.n:     cmp ax, 1
        jne .r1
        mov bx, cx                      ; singular
.r1:    or ax, ax                       ; ZF = 0
.r:     ret
VAZIO:  db 0
fim6:
%endif

%if PARTE == 7
; ---- seg04:2266 (era "Suspenso por 1 jogo", 20 bytes); a parte 6 usa -DJOGO=0x2266
org 0x2266
JOGO:   db 5, ' jogo'
%endif

%if PARTE == 8
; ---- seg04:251B, no fim da regiao do Historial (gravados por historial.py; o seg04
; nao pode crescer, ver cartoes.py): os textos dos amarelos; a parte 6 usa -DAMS e -DAM1
org ORG
AMS:    db 20, ' amarelos acumulados'
AM1:    db 18, ' amarelo acumulado'
%endif

%if PARTE == 9
; ---- seg03:572d, no fim de cada jogo (seg03:56e4, chamado para as 2 equipas): o
; laco que desconta 1 dos jogos de suspensao (+0x27) e de lesao (+0x29) de cada
; jogador. As 2 chamadas a Max(x - 1, 0) viram contas diretas (e o jogador deixa
; de ser guardado em [bp-0Ch], que so servia para elas), o que abre espaco para
; os 3 amarelos acumulados: o contador volta a 0 e, sem outra suspensao, o
; jogador fica suspenso 1 jogo e sai da escalacao (+0x10 = 0), como no vermelho.
; O jogo original nunca tem um suspenso na escalacao: na 0.9.0-0.9.2 o 3o amarelo
; o deixava la (em campo, com a suspensao em 2) e o Android caia mais adiante.
; Entra com es:di = jogador.
org 0x572D
        cmp word [es:di+0x27], 0        ; suspensao
        jle .l
        dec word [es:di+0x27]
.l:     cmp word [es:di+0x29], 0        ; lesao
        jle .a
        dec word [es:di+0x29]
.a:     cmp byte [es:di+0x25], 3        ; 3 amarelos acumulados
        jb .fim
        xor ax, ax
        mov [es:di+0x25], al
        cmp [es:di+0x27], ax
        jg .fim                         ; ja suspenso (vermelho)
        mov [es:di+0x10], ax            ; fora da escalacao
        inc ax
        mov [es:di+0x27], ax            ; 1 jogo
.fim:
        times 0x5763 - 0x572D - ($ - $$) nop
%endif
