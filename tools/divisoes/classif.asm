; Classificacao abre na pagina (1a+2a ou 3a+4a) com mais times de treinadores humanos,
; e nao sempre na 1a (jornada.py). Fica no fim do seg25. -DORG: deslocamento no segmento.
bits 16
org ORG

DIVISOES equ 0x26FA                     ; lista das divisoes (1a = 0 ... Distrital)
PAGINA   equ 0x17D0                     ; divisao da lista da esquerda (0 ou 2; paginas.asm)

%macro longe 1
        push cs
        call %1
%endmacro

; No lugar do "abrir" de paginas.asm (relocacao de seg06:02D0): poe em [PAGINA] a
; divisao da esquerda e segue para ele, que agora abre em [PAGINA]. O quadro (bp) e a
; pilha ficam como o abrir espera.
classif:
        push bp
        mov bp, sp
        push si
        push di
        xor si, si                      ; divisao
        xor cx, cx                      ; humanos na 1a+2a (cl) e na 3a+4a (ch)
.d:     cmp si, 4
        jae .esc
        les di, [DIVISOES]
        les di, [es:di]
        cmp si, [es:di+8]
        jge .esc
        push cx
        push si
        push es
        push di
        longe item                      ; TList.Get (seg23:0DD0)
        mov es, dx
        mov di, ax
        les di, [es:di]                 ; a divisao
        les di, [es:di+0x1B]
        les di, [es:di]                 ; a lista dos times dela
        pop cx
        xor bx, bx
.t:     cmp bx, [es:di+8]
        jge .pd
        push cx
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
        pop cx
        or al, al
        jz .pt
        cmp si, 2
        jae .baixo
        inc cl
        jmp short .pt
.baixo: inc ch
.pt:    inc bx
        jmp .t
.pd:    inc si
        jmp .d
.esc:   xor ax, ax
        cmp ch, cl
        jbe .poe
        mov ax, 2
.poe:   mov [PAGINA], ax
        pop di
        pop si
        pop bp
        db 0xEA
..@abrir: dw 0xFFFF, 0                  ; seg06: abrir (paginas.asm)

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

item:   db 0xEA
..@item: dw 0xFFFF, 0
