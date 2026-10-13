; Campinho na aba Escalacao da janela do time (ver campo.py).
; A rotina que confere a escalacao (seg04:38B0, chamada sempre que ela muda) acaba
; ligando ou desligando o "Jogar" (OKBtn.SetEnabled, seg21:1CB8); essa chamada passa a
; ser campo: ela liga o botao como antes e redesenha o campo no TImage da aba (o 1o
; controle grafico da aba do botao, sem campo no formulario): gramado verde com linhas brancas, sempre igual, e uma
; bolinha para cada titular na cor da letra do time, com a letra do setor (G, D, M,
; A) em negrito na cor do fundo do time.
;
; -DORG: deslocamento no segmento.
bits 16
org ORG

JOGAR   equ 0x1D0                       ; OKBtn: a imagem e o 1o controle grafico da aba dele
TIME    equ 0x348                       ; ponteiro para o time do tecnico

LARG    equ -6                          ; largura e altura da imagem
ALT     equ -8
NUM     equ -0x10                       ; titulares por setor (G, D, M, A), words
POSI    equ -0x18                       ; quantos ja foram desenhados por setor
LISTA   equ -0x1C                       ; ponteiro longe (o bitmap, depois a lista)
I       equ -0x1E
ULT     equ -0x20
RETAN   equ -0x28                       ; TRect
X       equ -0x2A
Y       equ -0x2C
RAIO    equ -0x2E
PASSO   equ -0x30
FUNDO   equ -0x34                       ; cores do time (TColor)
LETRA   equ -0x38

GRAMA_H equ 0x003C                      ; verde da grama, RGB(76, 160, 60)
GRAMA_L equ 0xA04C

%macro longe 1
        push cs
        call %1
%endmacro

%macro tela 0                           ; o canvas na pilha
        push word [bp-2]
        push word [bp-4]
%endmacro

%macro objeto 1                         ; Font (+7), Pen (+0xB) ou Brush (+0xF) na pilha
        les di, [bp-4]
        les di, [es:di+%1]
        push es
        push di
%endmacro

%macro ret4 5                           ; rotina do canvas com 4 coordenadas
        push %1
        push %2
        push %3
        push %4
        tela
        longe %5
%endmacro

campo:  cld                             ; o Delphi nao garante a direcao
        push bp
        mov bp, sp
        sub sp, 0x38
        push si
        push word [bp+0xA]
        push word [bp+8]
        push word [bp+6]
        longe habilitar
        call desenhar
        pop si
        mov sp, bp
        pop bp
        retf 6

desenhar:
        mov bx, [bp]                    ; quadro da 38B0: [+6] = a janela
        les di, [ss:bx+6]
        les di, [es:di+JOGAR]           ; o botao "Jogar", na aba Escalacao
        call naonulo
        les di, [es:di+0x1A]            ; Parent: a aba
        call naonulo
        les di, [es:di+0x92]            ; FControls: os controles graficos da aba
        call naonulo
        cmp word [es:di+8], 0
        jne .tem
        ret
.tem:   les di, [es:di+4]               ; FList
        les di, [es:di]                 ; o 1o: a imagem (o DFM a poe antes dos outros)
        call naonulo
        mov ax, [es:di+0x22]
        mov [bp+LARG], ax
        mov ax, [es:di+0x24]
        mov [bp+ALT], ax
        les di, [es:di+0x8E]            ; Picture
        les di, [es:di+4]               ; Graphic (o TBitmap do DFM)
        mov ax, es
        or ax, di
        jnz .bitmap
        ret
.bitmap:
        mov [bp+LISTA], di
        mov [bp+LISTA+2], es
        push word [bp+LARG]
        push es
        push di
        longe largura
        push word [bp+ALT]
        push word [bp+LISTA+2]
        push word [bp+LISTA]
        longe altura
        push word [bp+LISTA+2]
        push word [bp+LISTA]
        longe canvas
        mov [bp-4], ax
        mov [bp-2], dx

        ; cores do time: fundo (+0x46) e letra (+0x42), como na Ajuda do Arbitro
        mov bx, [bp]
        les di, [ss:bx+6]
        les di, [es:di+TIME]
        les di, [es:di]
        les di, [es:di+4]
        mov ax, [es:di+0x46]
        mov [bp+FUNDO], ax
        mov ax, [es:di+0x48]
        mov [bp+FUNDO+2], ax
        mov ax, [es:di+0x42]
        mov [bp+LETRA], ax
        mov ax, [es:di+0x44]
        mov [bp+LETRA+2], ax

        ; gramado
        push word GRAMA_H
        push word GRAMA_L
        objeto 0xF
        longe corpincel
        xor ax, ax
        mov [bp+RETAN], ax
        mov [bp+RETAN+2], ax
        mov ax, [bp+LARG]
        mov [bp+RETAN+4], ax
        mov ax, [bp+ALT]
        mov [bp+RETAN+6], ax
        lea di, [bp+RETAN]
        push ss
        push di
        tela
        longe preencher

        ; linhas brancas (os retangulos e o circulo enchem de grama por dentro)
        push word 0x00FF
        push word 0xFFFF
        objeto 0xB
        longe corcaneta
        push byte 2
        objeto 0xB
        longe espessura
        mov si, [bp+LARG]
        sub si, 3                       ; borda direita
        mov ax, [bp+ALT]
        sub ax, 3
        mov [bp+Y], ax                  ; borda de baixo
        ret4 3, 3, si, word [bp+Y], retangulo
        ; grande area: 16% da largura, 56% da altura; pequena: 6% e 28%
        mov cx, 16
        mov dx, 22
        call areas
        mov cx, 6
        mov dx, 36
        call areas
        ; circulo central: raio de 1/6 da altura
        mov ax, [bp+ALT]
        xor dx, dx
        mov cx, 6
        div cx
        mov si, ax
        mov ax, [bp+LARG]
        shr ax, 1
        mov [bp+X], ax
        mov ax, [bp+ALT]
        shr ax, 1
        mov [bp+Y], ax
        call bola
        ; linha do meio
        push word [bp+X]
        push byte 3
        tela
        longe mover
        push word [bp+X]
        mov ax, [bp+ALT]
        sub ax, 3
        push ax
        tela
        longe linha

        ; jogadores: bolinha na cor da letra do time, letra (negrito) na cor do fundo
        push word [bp+LETRA+2]
        push word [bp+LETRA]
        objeto 0xF
        longe corpincel
        push word [bp+FUNDO+2]
        push word [bp+FUNDO]
        objeto 0xB
        longe corcaneta
        push byte 1
        objeto 0xB
        longe espessura
        mov di, ARIAL
        push cs
        push di
        objeto 7
        longe fontenome
        push byte 8
        objeto 7
        longe fontetam
        push byte 1                     ; [fsBold]
        objeto 7
        longe fonteestilo
        push word [bp+FUNDO+2]
        push word [bp+FUNDO]
        objeto 7
        longe fontecor
        mov ax, [bp+ALT]
        xor dx, dx
        mov cx, 20
        div cx
        inc ax
        mov [bp+RAIO], ax

        lea di, [bp+POSI]               ; zera POSI e NUM
        push ss
        pop es
        xor ax, ax
        mov cx, 8
        rep stosw
        mov byte [bp+PASSO], 0          ; 0 conta, 1 desenha
.lista: mov bx, [bp]
        les di, [ss:bx+6]
        les di, [es:di+TIME]
        les di, [es:di]
        les di, [es:di+0xA]
        les di, [es:di]
        mov [bp+LISTA], di
        mov [bp+LISTA+2], es
        mov ax, [es:di+8]
        dec ax
        mov [bp+ULT], ax
        mov word [bp+I], 0
.jog:   mov ax, [bp+I]
        cmp ax, [bp+ULT]
        jg .fimlista
        push ax
        push word [bp+LISTA+2]
        push word [bp+LISTA]
        longe item
        mov di, ax
        mov es, dx
        les di, [es:di]                 ; o jogador
        cmp word [es:di+0x10], 1        ; titular
        jne .prox
        les di, [es:di+6]
        mov bl, [es:di+0x19]            ; setor
        xor bh, bh
        cmp bx, 3
        jbe .setor
        mov bx, 3
.setor: shl bx, 1
        mov di, bx
        cmp byte [bp+PASSO], 0
        jne .desenha
        inc word [bp+di+NUM]
        jmp .prox
.desenha:
        push di
        mov al, [cs:di+colunas]
        xor ah, ah
        mov cx, ax
        mov ax, [bp+LARG]
        call porc
        mov [bp+X], ax
        inc word [bp+di+POSI]           ; y = altura x k / (n + 1)
        mov ax, [bp+ALT]
        mul word [bp+di+POSI]
        mov cx, [bp+di+NUM]
        inc cx
        div cx
        mov [bp+Y], ax
        mov si, [bp+RAIO]
        call bola
        pop di
        mov al, [cs:di+meialetra]       ; a letra, mais ou menos no centro
        xor ah, ah
        neg ax
        add ax, [bp+X]
        push ax
        mov ax, [bp+Y]
        sub ax, 7
        push ax
        lea ax, [di+letras]
        push cs
        push ax
        tela
        longe texto
.prox:  inc word [bp+I]
        jmp .jog
.fimlista:
        cmp byte [bp+PASSO], 0
        jne .fim
        mov byte [bp+PASSO], 1
        jmp .lista
.fim:   ret

; es:di nulo: sai de desenhar (tira a volta desta e volta de desenhar)
naonulo:
        mov ax, es
        or ax, di
        jz .nulo
        ret
.nulo:  pop ax
        ret

; as 2 areas (esquerda e direita): cx = % da largura, dx = % da altura do topo
areas:  push dx
        mov ax, [bp+LARG]
        call porc
        mov [bp+X], ax                  ; largura da area
        pop cx
        mov ax, [bp+ALT]
        call porc
        mov [bp+Y], ax                  ; topo da area
        mov ax, [bp+X]
        add ax, 3
        mov si, [bp+ALT]
        sub si, [bp+Y]
        ret4 3, word [bp+Y], ax, si, retangulo
        mov si, [bp+LARG]
        sub si, 3
        mov ax, si
        sub ax, [bp+X]
        mov dx, [bp+ALT]
        sub dx, [bp+Y]
        ret4 ax, word [bp+Y], si, dx, retangulo
        ret

; ax = ax x cx / 100
porc:   mul cx
        mov cx, 100
        div cx
        ret

; elipse de raio si em volta de (X, Y)
bola:   mov ax, [bp+X]
        sub ax, si
        push ax
        mov ax, [bp+Y]
        sub ax, si
        push ax
        mov ax, [bp+X]
        add ax, si
        push ax
        mov ax, [bp+Y]
        add ax, si
        push ax
        tela
        longe elipse
        ret

; setor (G, D, M, A) -> coluna em % da largura, meia largura da letra
colunas:   db 6, 0, 25, 0, 55, 0, 82, 0
meialetra: db 4, 0, 4, 0, 5, 0, 4, 0
letras:    db 1, 'G', 1, 'D', 1, 'M', 1, 'A'
ARIAL:     db 5, 'Arial'

; saltos longe (um registro de relocacao para cada destino)
habilitar:  db 0xEA
..@habilitar: dw 0xFFFF, 0
largura:    db 0xEA
..@largura: dw 0xFFFF, 0
altura:     db 0xEA
..@altura:  dw 0xFFFF, 0
canvas:     db 0xEA
..@canvas:  dw 0xFFFF, 0
corpincel:  db 0xEA
..@corpincel: dw 0xFFFF, 0
corcaneta:  db 0xEA
..@corcaneta: dw 0xFFFF, 0
espessura:  db 0xEA
..@espessura: dw 0xFFFF, 0
preencher:  db 0xEA
..@preencher: dw 0xFFFF, 0
retangulo:  db 0xEA
..@retangulo: dw 0xFFFF, 0
elipse:     db 0xEA
..@elipse:  dw 0xFFFF, 0
mover:      db 0xEA
..@mover:   dw 0xFFFF, 0
linha:      db 0xEA
..@linha:   dw 0xFFFF, 0
item:       db 0xEA
..@item:    dw 0xFFFF, 0
fontenome:  db 0xEA
..@fontenome: dw 0xFFFF, 0
fontetam:   db 0xEA
..@fontetam: dw 0xFFFF, 0
fontecor:   db 0xEA
..@fontecor: dw 0xFFFF, 0
fonteestilo: db 0xEA
..@fonteestilo: dw 0xFFFF, 0
texto:      db 0xEA
..@texto:   dw 0xFFFF, 0
