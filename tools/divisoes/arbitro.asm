; Ajuda do Arbitro (ver arbitro.py): depois de preencher a janela e antes do ShowModal
; (seg06:2963), o nome de cada equipa ganha a chance de penalti a favor no jogo e
; quantos cartoes o arbitro deixa de dar a ela, pela influencia V (0 a 15) do jogo:
; +0x4C mandante, +0x4E visitante.
; - penalti: 1 - (1 - V/6000)^90 no mandante, V/7000 no visitante (tabelas abaixo);
; - cartoes: o sorteio de cada minuto (0 a 49 da cartao, cartoes.asm) so vale acima
;   de V: o arbitro anula V + 1 de 50, 2 x (V + 1)% (no maximo 32%).
; Os textos sao passados com "mov di; push cs; push di", como os do jogo, para a
; ferramenta de traducao os achar.
;
; -DORG: deslocamento no segmento.
bits 16
org ORG

TEXTO   equ 0x18C                       ; textHomeTeam; textAwayTeam em +4
JOGO    equ 0x194                       ; ponteiro para o jogo

mostrar:
        mov ax, bp
        push bp
        mov bp, sp
        sub sp, 0x100                   ; [bp-0x100] o texto (string Pascal)
        push ax                         ; [bp-0x102] quadro de quem chamou
        push si                         ; [bp-0x104]
        xor ax, ax
        push ax                         ; [bp-0x106] lado: 0 mandante, 4 visitante
        push ax                         ; [bp-0x108] fim do texto
.lado:  lea ax, [bp-0xFF]
        mov [bp-0x108], ax
        call jogo
        les di, [es:di+bx+0x2E]         ; a equipa
        les di, [es:di]
        les di, [es:di+4]
        add di, 0x2D                    ; nome
        push es
        push di
        call anexar
        mov di, PENALTI
        push cs
        push di
        call anexar
        call jogo
        shr bx, 1
        mov ax, [es:di+bx+0x4C]         ; V
        cmp ax, 15
        jbe .v
        mov ax, 15
.v:     push ax
        shl bx, 3                       ; + 16 no visitante
        add bx, ax
        mov al, [cs:bx+tabela]
        call numero
        mov di, CARTOES
        push cs
        push di
        call anexar
        pop ax
        inc ax
        shl ax, 1
        call numero
        mov ax, [bp-0x108]
        lea di, [bp-0xFF]
        sub ax, di
        mov [bp-0x100], al
        dec di
        push ss
        push di
        call janela
        add di, [bp-0x106]
        les di, [es:di+TEXTO]
        push es
        push di
        db 0x9A                         ; TControl.SetText (seg21:1D8C)
..@texto: dw 0xFFFF, 0
        add word [bp-0x106], 4
        cmp word [bp-0x106], 8
        jb .lado
        call janela
        push es
        push di
        db 0x9A                         ; ShowModal (seg22:5743)
..@modal: dw 0xFFFF, 0
        mov si, [bp-0x104]
        mov sp, bp
        pop bp
        retf

; es:di = a janela
janela: mov bx, [bp-0x102]
        les di, [ss:bx+6]
        ret

; es:di = o jogo, bx = o lado
jogo:   call janela
        les di, [es:di+JOGO]
        les di, [es:di]
        mov bx, [bp-0x106]
        ret

; acrescenta ao texto a string (ponteiro longe na pilha)
anexar: mov bx, sp
        push ds
        lds si, [ss:bx+2]
        push ss
        pop es
        mov di, [bp-0x108]
        lodsb
        cbw
        mov cx, ax
        rep movsb
        mov [bp-0x108], di
        pop ds
        ret 4

; al (0 a 99) em decimal, mais "%"
numero: push ss
        pop es
        mov di, [bp-0x108]
        aam                             ; ah = dezenas, al = unidades
        or ah, ah
        jz .um
        xchg al, ah
        add al, '0'
        stosb
        xchg al, ah
.um:    add al, '0'
        stosb
        mov al, '%'
        stosb
        mov [bp-0x108], di
        ret

PENALTI: db .f - PENALTI - 1, '   Pen', 0xE1, 'lti: '
.f:
CARTOES: db .f - CARTOES - 1, '   Cart', 0xF5, 'es: -'
.f:
; chance de penalti a favor no jogo, V de 0 a 15: mandante, visitante
tabela: db 0, 1, 3, 4, 6, 7, 9, 10, 11, 13, 14, 15, 16, 18, 19, 20
        db 0, 1, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13, 14, 15, 16, 18
