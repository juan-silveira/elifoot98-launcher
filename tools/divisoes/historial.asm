; Historial com os cartoes amarelos (ver historial.py). Os 4 blocos do original
; que poem golos, golos, vermelhos e lesoes nos textos do Historial (um IntToStr
; e um SetText cada) viram um laco por uma tabela de 5 linhas: a nova e a dos
; amarelos na carreira (+0x21). Os numeros sao os 2 bytes de baixo (os de cima
; das expulsoes e das lesoes guardam os amarelos; os golos nunca passam de 65.535).
;
; -DORG, -DFIM: regiao dos 4 blocos; -DTEMP: texto temporario ([bp-TEMP]);
; -DJOGADOR=0xNNNN: o jogador vem de [JOGADOR] (seg02, seg10), senao do quadro
; do chamador (seg04); -DF1..F5: campos dos textos no formulario.
bits 16
org ORG

%macro jogador 0
%ifdef JOGADOR
        les di, [JOGADOR]
%else
        mov di, [bp+4]
        les di, [ss:di+6]
        les di, [es:di+0x354]
%endif
        les di, [es:di]
%endmacro

%macro formulario 0
%ifdef JOGADOR
        les di, [bp+6]
%else
        mov di, [bp+4]
        les di, [ss:di+6]
%endif
%endmacro

        push si
        xor si, si
.laco:  push si
        lea di, [bp-TEMP]
        push ss
        push di
        jogador
        mov bx, [cs:si+tabela]
        push byte 0
        push word [es:di+bx]
        db 0x9A                         ; IntToStr (seg24:07D0)
..@i:   dw 0xFFFF, 0
        mov bx, sp                      ; o si nao sobrevive as chamadas: volta
        mov si, [ss:bx+4]               ; da pilha (debaixo do texto do IntToStr)
        formulario
        mov bx, [cs:si+tabela+2]
        les di, [es:di+bx]
        push es
        push di
        db 0x9A                         ; SetText (seg21:1D8C)
..@t:   dw 0xFFFF, 0
        pop si
        add si, 4
        cmp si, 5 * 4
        jb .laco
        pop si
        jmp FIM
tabela: dw 0x17, F1                     ; golos
        dw 0x1B, F2                     ; golos (o outro)
        dw 0x21, F3                     ; amarelos na carreira (novo)
        dw 0x23, F4                     ; vermelhos
        dw 0x1F, F5                     ; lesoes
