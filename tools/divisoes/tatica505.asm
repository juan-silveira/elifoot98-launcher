; Tatica 5-0-5 (tecla T, tools/patch_505.py): o menu Seleccionar desliga as taticas que
; o elenco nao consegue escalar (seg04:29AB), mas o item 5-0-5 nao estava nessa rotina.
; Fica no fim do seg25 (tatica505.py). O fim do bloco do 6-4-0 em seg04:2B7F vira
; "push ax; push [bp+4]; push equipa; call far menu505", e esta rotina faz o SetEnabled
; do 6-4-0 e o do 5-0-5 (o item que no DFM passou a se chamar N4: campo Self+0x304).
; -DORG: deslocamento no segmento.
bits 16
org ORG

%macro longe 1
        push cs
        call %1
%endmacro

; [bp+6] equipa (off), [bp+8] equipa (seg), [bp+10] quadro da rotina de cima (o Self
; esta em [ss:quadro+6]), [bp+12] se o 6-4-0 cabe (al)
menu505:
        push bp
        mov bp, sp
        push si
        push di
        push word [bp+12]
        mov di, [bp+10]
        les di, [ss:di+6]
        les di, [es:di+0x1A8]           ; MenuSelect640
        push es
        push di
        longe setenabled
        push word 1                     ; 1 G, 5 D, 0 M, 5 A (como o MenuSelect640Click do 5-0-5)
        push word 5
        push word 0
        push word 5
        push word [bp+8]
        push word [bp+6]
        longe podeescalar
        mov di, [bp+10]
        les di, [ss:di+6]
        les di, [es:di+0x304]           ; o item 5-0-5 (N4)
        mov cx, es
        or cx, di
        jz .fim
        push ax
        push es
        push di
        longe setenabled
.fim:   pop di
        pop si
        pop bp
        retf 8

setenabled: db 0xEA
..@setenabled: dw 0xFFFF, 0
podeescalar: db 0xEA
..@podeescalar: dw 0xFFFF, 0
