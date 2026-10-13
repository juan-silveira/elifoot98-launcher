; Campinho na aba Escalacao da janela do time (ver campo.py).
; A rotina que confere a escalacao (seg04:38B0, chamada sempre que ela muda) acaba
; ligando ou desligando o "Jogar" (OKBtn.SetEnabled, seg21:1CB8); essa chamada passa a
; ser campo: ela liga o botao como antes e redesenha o campo no TImage da aba (no
; campo +0x2D0 da janela), com uma bolinha para cada titular, por setor.
;
; -DORG: deslocamento no segmento.
bits 16
org ORG

IMAGEM  equ 0x2D0                       ; o TImage (campo do Label13, sem uso)
TIME    equ 0x348                       ; ponteiro para o time do tecnico

LARG    equ -6                          ; largura e altura da imagem
ALT     equ -8
NUM     equ -0x10                       ; titulares por setor (G, D, M, A), words
POSI    equ -0x18                       ; quantos ja foram desenhados por setor
LISTA   equ -0x1C                       ; ponteiro longe (o bitmap, depois a lista)
I       equ -0x1E
ULT     equ -0x20
RETAN     equ -0x28                       ; TRect
X       equ -0x2A
Y       equ -0x2C
RAIO    equ -0x2E
PASSO   equ -0x30

%macro longe 1
        push cs
        call %1
%endmacro

%macro tela 0                           ; o canvas na pilha
        push word [bp-2]
        push word [bp-4]
%endmacro

campo:  push bp
        mov bp, sp
        sub sp, 0x30
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
        les di, [es:di+IMAGEM]
        mov ax, es
        or ax, di
        jz .fim
        mov ax, [es:di+0x22]
        mov [bp+LARG], ax
        mov ax, [es:di+0x24]
        mov [bp+ALT], ax
        les di, [es:di+0x8E]            ; Picture
        les di, [es:di+4]               ; Graphic (o TBitmap do DFM)
        mov ax, es
        or ax, di
        jz .fim
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

        ; fundo branco
        mov dx, 0x00FF
        mov ax, 0xFFFF
        call pincel
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

        ; linhas verdes
        xor dx, dx
        mov ax, 0xFF00                  ; clLime
        call caneta
        push byte 3
        les di, [bp-4]
        les di, [es:di+0xB]
        push es
        push di
        longe espessura
        ; contorno
        push byte 2
        push byte 2
        mov ax, [bp+LARG]
        sub ax, 2
        push ax
        mov ax, [bp+ALT]
        sub ax, 2
        push ax
        tela
        longe retangulo
        ; grandes areas: 15% da largura, 60% da altura
        mov ax, [bp+LARG]
        mov cx, 15
        call porc
        mov [bp+RAIO], ax               ; largura da area (o Delphi nao guarda si)
        mov ax, [bp+ALT]
        mov cx, 20
        call porc
        mov [bp+Y], ax                  ; topo da area
        push byte 2
        push ax
        mov ax, [bp+RAIO]
        add ax, 2
        push ax
        mov ax, [bp+ALT]
        sub ax, [bp+Y]
        push ax
        tela
        longe retangulo
        mov ax, [bp+LARG]
        sub ax, 2
        sub ax, [bp+RAIO]
        push ax
        push word [bp+Y]
        mov ax, [bp+LARG]
        sub ax, 2
        push ax
        mov ax, [bp+ALT]
        sub ax, [bp+Y]
        push ax
        tela
        longe retangulo
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
        push byte 2
        tela
        longe mover
        push word [bp+X]
        mov ax, [bp+ALT]
        sub ax, 2
        push ax
        tela
        longe linha

        ; jogadores: bolinhas azuis
        mov dx, 0x00FF
        xor ax, ax                      ; clBlue
        call pincel
        mov dx, 0x00FF
        xor ax, ax
        call caneta
        push byte 1
        les di, [bp-4]
        les di, [es:di+0xB]
        push es
        push di
        longe espessura
        mov ax, [bp+ALT]
        xor dx, dx
        mov cx, 24
        div cx
        add ax, 2
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
        mov al, [cs:di+colunas]
        xor ah, ah
        mov [bp+X], ax
        mov ax, [bp+LARG]
        mov cx, [bp+X]
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
.prox:  inc word [bp+I]
        jmp .jog
.fimlista:
        cmp byte [bp+PASSO], 0
        jne .fim
        mov byte [bp+PASSO], 1
        jmp .lista
.fim:   ret

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

; dx:ax = cor
pincel: push dx
        push ax
        les di, [bp-4]
        les di, [es:di+0xF]             ; Brush
        push es
        push di
        longe corpincel
        ret
caneta: push dx
        push ax
        les di, [bp-4]
        les di, [es:di+0xB]             ; Pen
        push es
        push di
        longe corcaneta
        ret

; setor (G, D, M, A) -> coluna em % da largura
colunas: db 5, 0, 24, 0, 47, 0, 72, 0

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
