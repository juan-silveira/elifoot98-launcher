; Classificacao moderna (classif2.py), no fim do seg25. -DORG: deslocamento no segmento;
; -DN: equipas por divisao (K = 4 sobem e descem com N >= 16, senao 2).
;
; humanos: no lugar do "abrir" (relocacao de seg06:02D0): poe em [PAGINA] a divisao
;   (0 a 3) com mais treinadores humanos (empate: a de cima) e segue para o abrir.
; linha: o ListBoxDivision1DrawItem (seg06:03EB vira jmp far linha). Desenha a equipa
;   da linha Index da divisao [PAGINA]: fundo escuro (zebrado; o humano destacado),
;   barra da zona (campeao, sobe, desce), posicao, quadrinho nas cores da equipa, nome,
;   Pts, PJ, V, E, D, GM, GC, SG e as ultimas 5 (bolinhas: vitoria, empate, derrota).
bits 16
org ORG

DIVISOES equ 0x26FA                     ; lista das divisoes (1a = 0 ... Distrital)
PAGINA   equ 0x17D0                     ; divisao mostrada (0 a 3)
TAGDIV   equ 0x7700                     ; Tag dos botoes de divisao (TAGDIV + divisao)
%if N >= 16
K equ 4
%else
K equ 2
%endif

; colunas (x a partir da borda da lista); os numeros sao alinhados a direita
X_BARRA  equ 0
X_POS    equ 24
X_ESC    equ 30
X_NOME   equ 50
X_PTS    equ 250                        ; os numeros ficam centrados nestes x
X_PJ     equ 280
X_V      equ 310
X_E      equ 340
X_D      equ 370
X_GM     equ 400
X_GC     equ 430
X_SG     equ 460
X_ULT    equ 478                        ; 5 bolinhas de 12, a cada 15
DIGITO   equ 6                          ; largura de um algarismo (Arial, -12)
SINAL    equ 4                          ; largura do "-"
MAIS     equ 6                          ; largura do "+"

; cores (TColor: 0x00BBGGRR)
%define FUNDO      0x0024, 0x2120
%define ZEBRA      0x002A, 0x2625
%define HUMANO     0x0048, 0x3528
%define TEXTO      0x00ED, 0xEAE8
%define FRACO      0x00A6, 0xA09A
%define OURO       0x0004, 0xBCFB
%define SOBE       0x0053, 0xA834
%define DESCE      0x0035, 0x43EA
%define VITORIA    0x003E, 0x8E1E
%define EMPATE     0x0068, 0x635F
%define DERROTA    0x0025, 0x30D9
%define VAZIO      0x0040, 0x3C3C

; quadro de linha
R        equ -8                         ; Left, Top, Right, Bottom (copia do Rect)
TIME     equ -0x0C                      ; ponteiro para o TTeam
CANVAS   equ -0x10
COR      equ -0x14                      ; cor do fundo da linha
NEQ      equ -0x16                      ; equipas na divisao
NRES     equ -0x18                      ; resultados guardados (0..5)
RES      equ -0x1E                      ; os ultimos 5 (1 V, 2 E, 3 D), o mais recente no fim
CAL      equ -0x22                      ; a lista do calendario
RET2     equ -0x2A                      ; retangulo de trabalho
IDX      equ -0x2C                      ; contador dos lacos (o Delphi nao guarda o SI)
BUF      equ -0x12C                     ; texto (string[255])
QUADRO   equ 0x12C

%macro longe 1
        push cs
        call %1
%endmacro

%macro cor 1                            ; empilha a TColor
        push word %1
%endmacro

; chamadas ao Canvas (os argumentos ja empilhados; o Self vai por ultimo)
%macro PINCEL 0                         ; Brush.SetColor(cor)
        les di, [bp+CANVAS]
        les di, [es:di+0x0F]
        push es
        push di
        longe brushcolor
%endmacro
%macro CANETA 0                         ; Pen.SetColor(cor)
        les di, [bp+CANVAS]
        les di, [es:di+0x0B]
        push es
        push di
        longe pencolor
%endmacro
%macro LETRA 0                          ; Font.SetColor(cor)
        les di, [bp+CANVAS]
        les di, [es:di+0x07]
        push es
        push di
        longe fontcolor
%endmacro
%macro PREENCHER 1                      ; FillRect(retangulo em [bp+%1])
        lea ax, [bp+%1]
        push ss
        push ax
        les di, [bp+CANVAS]
        push es
        push di
        longe fillrect
%endmacro
%macro ESCREVER 0                       ; TextOut(x, y, texto)
        les di, [bp+CANVAS]
        push es
        push di
        longe textout
%endmacro

; ===================================================================== humanos
humanos:
        push bp
        mov bp, sp
        sub sp, 8                       ; [bp-8..-2]: humanos nas divisoes 0 a 3
        push si
        push di
        xor ax, ax
        mov [bp-8], ax
        mov [bp-6], ax
        mov [bp-4], ax
        mov [bp-2], ax
        xor si, si                      ; divisao
.d:     cmp si, 4
        jae .esc
        les di, [DIVISOES]
        les di, [es:di]
        cmp si, [es:di+8]
        jge .esc
        push si
        push es
        push di
        longe item                      ; TList.Get (seg23:0DD0)
        mov es, dx
        mov di, ax
        les di, [es:di]                 ; a divisao
        les di, [es:di+0x1B]
        les di, [es:di]                 ; a lista dos times dela
        xor bx, bx
.t:     cmp bx, [es:di+8]
        jge .pd
        push bx
        push es
        push di
        push bx
        push es
        push di
        longe item
        push dx
        pop es
        mov di, ax                      ; ponteiro para o TTeam
        call humano
        pop di
        pop es
        pop bx
        or al, al
        jz .pt
        push di
        mov di, si
        shl di, 1
        inc word [bp+di-8]
        pop di
.pt:    inc bx
        jmp .t
.pd:    inc si
        jmp .d
.esc:   xor ax, ax                      ; a de mais humanos (empate: a de cima)
        mov cx, [bp-8]
        mov si, 1
.m:     cmp si, 4
        jae .poe
        mov di, si
        shl di, 1
        mov dx, [bp+di-8]
        cmp dx, cx
        jbe .mp
        mov cx, dx
        mov ax, si
.mp:    inc si
        jmp .m
.poe:   mov [PAGINA], ax
        mov bx, [bp]                    ; o quadro de quem abre: Self (a janela) em +6
        les di, [ss:bx+6]
        call pagina
        pop di
        pop si
        mov sp, bp
        pop bp
        db 0xEA
..@abrir: dw 0xFFFF, 0                  ; seg06:0378 (o que o abrir chamava)

; ===================================================================== botoes
; botao(Self, Sender): o clique num dos 4 botoes de divisao (ListBoxDivision3Click, via
; classif2_06.asm). A divisao e o Tag do botao - TAGDIV; a da pagina atual nao faz nada.
botao:
        push bp
        mov bp, sp
        push si
        push di
        les di, [bp+0x0A]               ; Sender
        mov ax, [es:di+0x0C]            ; TComponent.Tag
        sub ax, TAGDIV
        cmp ax, 3
        ja .sai
        cmp ax, [PAGINA]
        je .sai
        mov [PAGINA], ax
        les di, [bp+6]
        call pagina
.sai:   pop di
        pop si
        pop bp
        retf 8

; pagina: es:di = a janela; mostra a divisao [PAGINA]: o titulo dela (Label1..4, so um
; visivel), a lista redesenhada (esconde e mostra) e os botoes (o da divisao marcado)
pagina:
        push bp
        mov bp, sp
        push es                         ; [bp-2]:[bp-4] a janela
        push di
        push word 0                     ; [bp-6] contador (o Delphi nao guarda o SI)
        mov word [es:di+0x1A0], 0      ; zera o relogio que fecha a janela
.tit:   mov si, [bp-6]
        cmp si, 4                       ; Label1..4 em +0x18C, +0x190, +0x194, +0x198
        jae .lista
        xor ax, ax
        cmp si, [PAGINA]
        jne .v
        inc ax
.v:     push ax
        les di, [bp-4]
        mov bx, si
        shl bx, 2
        les di, [es:di+bx+0x18C]
        push es
        push di
        longe setvisible
        inc word [bp-6]
        jmp .tit
.lista: push word 0                     ; ListBoxDivision1 (+0x17C): esconde e mostra
        les di, [bp-4]
        les di, [es:di+0x17C]
        push es
        push di
        longe setvisible
        push word 1
        les di, [bp-4]
        les di, [es:di+0x17C]
        push es
        push di
        longe setvisible
        ; os botoes: componentes da janela com Tag = TAGDIV..TAGDIV+3
        mov word [bp-6], 0
.b:     mov si, [bp-6]
        les di, [bp-4]
        les di, [es:di+0x10]            ; TComponent.FComponents (TList)
        cmp si, [es:di+8]
        jge .fim
        les di, [es:di+4]               ; FList
        mov bx, si
        shl bx, 2
        les di, [es:di+bx]
        cmp word [es:di+0x0E], 0
        jne .prox
        mov ax, [es:di+0x0C]
        sub ax, TAGDIV
        cmp ax, 3
        ja .prox
        push es
        push di
        cmp ax, [PAGINA]
        jne .normal
        push word 0x00F8                ; marcado: fundo azul claro, letra escura
        push word 0xB48A
        push es
        push di
        longe setcolor
        pop di
        pop es
        push word 0x0024
        push word 0x2120
        jmp short .letra
.normal:
        push word 0x0048                ; os outros: fundo cinza, letra branca
        push word 0x4543
        push es
        push di
        longe setcolor
        pop di
        pop es
        push word 0x00FF
        push word 0xFFFF
.letra: les di, [es:di+0x34]            ; TControl.Font
        push es
        push di
        longe fontcolor
.prox:  inc word [bp-6]
        jmp .b
.fim:   mov sp, bp
        pop bp
        ret

; es:di = ponteiro para o TTeam (pode ser nil); al = 1 se o treinador dele e humano
; (TTeam+0x0E -> treinador, +7; o mesmo teste da chicotada, seg03:4FFF)
humano:
        mov ax, es
        or ax, di
        jz .nao
        les di, [es:di]
        mov ax, es
        or ax, di
        jz .nao
        les di, [es:di+0x0E]
        mov ax, es
        or ax, di
        jz .nao
        les di, [es:di]
        mov ax, es
        or ax, di
        jz .nao
        cmp byte [es:di+7], 0
        je .nao
        mov al, 1
        ret
.nao:   xor al, al
        ret

; ===================================================================== linha
; ListBoxDivision1DrawItem(Self, Control, Index, Rect, State): [bp+0x12] Control,
; [bp+0x10] Index, [bp+0x0C] Rect (var), [bp+0x0A] State, [bp+6] Self
linha:
        push bp
        mov bp, sp
        sub sp, QUADRO
        push si
        push di
        push ds
        lds si, [bp+0x0C]               ; copia o Rect
        mov ax, [si]
        mov [bp+R], ax
        mov ax, [si+2]
        mov [bp+R+2], ax
        mov ax, [si+4]
        mov [bp+R+4], ax
        mov ax, [si+6]
        mov [bp+R+6], ax
        pop ds
        push word 0x0024                ; a janela no tema escuro (TControl.SetColor
        push word 0x2120                ; volta logo se a cor ja e essa)
        les di, [bp+6]
        push es
        push di
        longe setcolor
        les di, [bp+0x12]               ; Canvas da lista
        les di, [es:di+0xDD]
        mov [bp+CANVAS], di
        mov [bp+CANVAS+2], es
        ; a equipa: divisoes[PAGINA].times[Index]
        push word [PAGINA]
        les di, [DIVISOES]
        les di, [es:di]
        push es
        push di
        longe item
        mov es, dx
        mov di, ax
        les di, [es:di]
        les di, [es:di+0x1B]
        les di, [es:di]                 ; a lista dos times
        mov ax, [es:di+8]
        mov [bp+NEQ], ax
        push word [bp+0x10]
        push es
        push di
        longe item
        mov [bp+TIME], ax
        mov [bp+TIME+2], dx
        ; cor do fundo: humano, ou zebrada
        mov es, dx
        mov di, ax
        call humano
        or al, al
        jz .zeb
        mov word [bp+COR+2], 0x0048
        mov word [bp+COR], 0x3528
        jmp short .fundo
.zeb:   test byte [bp+0x10], 1
        jnz .z1
        mov word [bp+COR+2], 0x0024
        mov word [bp+COR], 0x2120
        jmp short .fundo
.z1:    mov word [bp+COR+2], 0x002A
        mov word [bp+COR], 0x2625
.fundo: push word [bp+COR+2]
        push word [bp+COR]
        PINCEL
        PREENCHER R
        ; barra da zona
        mov ax, [bp+0x10]
        cmp word [PAGINA], 0
        jne .sobe
        or ax, ax
        jnz .desce
        cor 0x0004                      ; campeao (1o da 1a)
        cor 0xBCFB
        jmp short .barra
.sobe:  cmp ax, K
        jae .desce
        cor 0x0053
        cor 0xA834
        jmp short .barra
.desce: mov cx, [bp+NEQ]
        sub cx, K
        cmp ax, cx
        jl .texto
        cor 0x0035
        cor 0x43EA
.barra: PINCEL
        mov ax, [bp+R]
        mov [bp+RET2], ax
        add ax, 4
        mov [bp+RET2+4], ax
        mov ax, [bp+R+2]
        inc ax
        mov [bp+RET2+2], ax
        mov ax, [bp+R+6]
        dec ax
        mov [bp+RET2+6], ax
        PREENCHER RET2
        push word [bp+COR+2]
        push word [bp+COR]
        PINCEL
.texto: ; posicao (texto fraco)
        cor 0x00A6
        cor 0xA09A
        LETRA
        mov ax, [bp+0x10]
        inc ax
        mov bx, X_POS
        call numero
        ; quadrinho nas cores da equipa: o fundo (+0x46) e uma faixa na cor da letra (+0x42)
        les di, [bp+TIME]
        les di, [es:di]
        les di, [es:di+4]
        push word [es:di+0x48]
        push word [es:di+0x46]
        PINCEL
        mov ax, [bp+R]
        add ax, X_ESC
        mov [bp+RET2], ax
        add ax, 14
        mov [bp+RET2+4], ax
        mov ax, [bp+R+2]
        add ax, 3
        mov [bp+RET2+2], ax
        add ax, 14
        mov [bp+RET2+6], ax
        PREENCHER RET2
        les di, [bp+TIME]
        les di, [es:di]
        les di, [es:di+4]
        push word [es:di+0x44]
        push word [es:di+0x42]
        PINCEL
        add word [bp+RET2], 5
        sub word [bp+RET2+4], 5
        PREENCHER RET2
        push word [bp+COR+2]
        push word [bp+COR]
        PINCEL
        ; nome (string[20] em +0x2D dos atributos)
        cor 0x00ED
        cor 0xEAE8
        LETRA
        mov ax, [bp+R]
        add ax, X_NOME
        push ax
        mov ax, [bp+R+2]
        add ax, 2
        push ax
        les di, [bp+TIME]
        les di, [es:di]
        les di, [es:di+4]
        add di, 0x2D
        push es
        push di
        ESCREVER
        ; numeros
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x20]            ; Pts
        mov bx, X_PTS
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x16]
        add ax, [es:di+0x18]
        add ax, [es:di+0x1A]            ; PJ
        mov bx, X_PJ
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x16]
        mov bx, X_V
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x18]
        mov bx, X_E
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x1A]
        mov bx, X_D
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x1C]
        mov bx, X_GM
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x1E]
        mov bx, X_GC
        call numero
        les di, [bp+TIME]
        les di, [es:di]
        mov ax, [es:di+0x1C]
        sub ax, [es:di+0x1E]            ; SG (com "+")
        mov bx, X_SG
        call numero_sinal
        call ultimas
        pop di
        pop si
        mov sp, bp
        pop bp
        retf 0x10

; ---- ultimas: os ultimos 5 jogos da liga ja jogados (calendario +0x69: +0x07 Taca,
;      +0x0A golos dele, +0x0C do adversario), como bolinhas da esquerda (antigo) para a
;      direita (recente); as que faltam ficam vazias
ultimas:
        mov word [bp+NRES], 0
        les di, [bp+TIME]
        les di, [es:di]
        les di, [es:di+0x69]
        les di, [es:di]
        mov [bp+CAL], di
        mov [bp+CAL+2], es
        mov word [bp+IDX], 0
.c:     les di, [bp+CAL]
        mov ax, [bp+IDX]
        cmp ax, [es:di+8]
        jge .desenha
        push ax
        push es
        push di
        longe item
        mov es, dx
        mov di, ax
        les di, [es:di]                 ; a entrada
        cmp byte [es:di+7], 0
        jne .prox                       ; Taca
        mov ax, [es:di+0x0A]
        or ax, ax
        jl .prox                        ; ainda nao jogado
        mov dl, 1
        cmp ax, [es:di+0x0C]
        jg .guarda
        mov dl, 2
        je .guarda
        mov dl, 3
.guarda:
        mov cx, [bp+NRES]
        cmp cx, 5
        jb .poe
        ; cheio: anda uma casa para a esquerda
        mov al, [bp+RES+1]
        mov [bp+RES], al
        mov al, [bp+RES+2]
        mov [bp+RES+1], al
        mov al, [bp+RES+3]
        mov [bp+RES+2], al
        mov al, [bp+RES+4]
        mov [bp+RES+3], al
        mov cx, 4
        mov word [bp+NRES], 4
.poe:   mov di, cx
        mov [bp+di+RES], dl
        inc word [bp+NRES]
.prox:  inc word [bp+IDX]
        jmp .c
.desenha:
        mov word [bp+IDX], 0            ; bolinha 0..4
.b:     mov si, [bp+IDX]
        cmp si, 5
        jae .fim
        ; indice no RES: as vazias primeiro
        mov ax, 5
        sub ax, [bp+NRES]
        cmp si, ax
        jb .vazia
        mov di, si
        sub di, ax
        mov al, [bp+di+RES]
        cmp al, 1
        jne .e
        mov cx, 0x003E
        mov dx, 0x8E1E
        jmp short .pinta
.e:     cmp al, 2
        jne .d
        mov cx, 0x0068
        mov dx, 0x635F
        jmp short .pinta
.d:     mov cx, 0x0025
        mov dx, 0x30D9
        jmp short .pinta
.vazia: mov cx, 0x0040
        mov dx, 0x3C3C
.pinta: push cx
        push dx
        push cx
        push dx
        PINCEL
        pop dx
        pop cx
        push cx
        push dx
        CANETA
        mov ax, [bp+IDX]
        mov bx, 15
        mul bx
        add ax, [bp+R]
        add ax, X_ULT
        push ax                         ; X1
        mov bx, [bp+R+2]
        add bx, 3
        push bx                         ; Y1
        add ax, 12
        push ax                         ; X2
        add bx, 12
        push bx                         ; Y2
        les di, [bp+CANVAS]
        push es
        push di
        longe ellipse                   ; TCanvas.Ellipse (seg19:1C74)
        inc word [bp+IDX]
        jmp .b
.fim:   ret

; ---- numero: ax = valor, bx = x do centro da coluna (a posicao, X_POS: a borda direita)
numero:
        push bx
        lea di, [bp+BUF]
        push ss
        push di
        cwd
        push dx
        push ax
        longe inttostr                  ; IntToStr (seg24:07D0)
        add sp, 4                       ; o resultado fica no BUF
        pop bx
alinhado:
        xor cx, cx
        mov cl, [bp+BUF]                ; largura: algarismos (e o "-"/"+")
        mov ax, DIGITO
        mul cx
        mov cx, ax
        cmp byte [bp+BUF+1], '-'
        jne .m
        sub cx, DIGITO - SINAL
.m:     cmp bx, X_POS
        je .dir
        shr cx, 1                       ; centrado na coluna
.dir:   mov ax, [bp+R]
        add ax, bx
        sub ax, cx
        push ax
        mov ax, [bp+R+2]
        add ax, 2
        push ax
        lea di, [bp+BUF]
        push ss
        push di
        ESCREVER
        ret

; ---- numero_sinal: como numero, com "+" na frente dos positivos
numero_sinal:
        push bx
        push ax
        lea di, [bp+BUF]
        push ss
        push di
        cwd
        push dx
        push ax
        longe inttostr
        add sp, 4
        pop ax
        pop bx
        or ax, ax
        jle alinhado
        ; insere o "+": desloca o texto uma casa
        xor cx, cx
        mov cl, [bp+BUF]
        lea si, [bp+BUF]
        add si, cx
.d:     mov al, [ss:si]
        mov [ss:si+1], al
        dec si
        loop .d
        mov byte [bp+BUF+1], '+'
        inc byte [bp+BUF]
        jmp alinhado

item:     db 0xEA
..@item:  dw 0xFFFF, 0
inttostr: db 0xEA
..@int:   dw 0xFFFF, 0
ellipse:  db 0xEA
..@ell:   dw 0xFFFF, 0
textout:  db 0xEA
..@text:  dw 0xFFFF, 0
brushcolor: db 0xEA
..@brush: dw 0xFFFF, 0
pencolor: db 0xEA
..@pen:   dw 0xFFFF, 0
fontcolor: db 0xEA
..@font:  dw 0xFFFF, 0
setvisible: db 0xEA
..@setv:  dw 0xFFFF, 0
setcolor: db 0xEA
..@setc:  dw 0xFFFF, 0
fillrect: db 0xEA
..@fill:  dw 0xFFFF, 0
