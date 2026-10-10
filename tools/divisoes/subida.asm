; Subida e descida com K equipas (era 2), no lugar de seg05:0987..0A83.
; Listas das divisoes (ponteiros far) em [bp-4] (1a) ... [bp-0x14] (Distrital).
; 02A6 (procedimento aninhado) troca A[ia] com B[ib]: A perde A[ia] e ganha
; B[ib] no fim; B perde B[ib] e ganha A[ia] no fim.
; Montar: nasm -f bin -DN=20 -DK=4 -o subida.bin subida.asm
bits 16
org 0x0987

%define TROCA 0x02A6
%define FIM   0x0A84            ; continua no codigo original (call 025a)
%define NIVEL [bp-0x2C]         ; locais do sorteio original, livres aqui
%define CONTA [bp-0x2A]

        mov word NIVEL, -4
.nivel: mov word CONTA, K
.troca: mov si, NIVEL
        push word [bp+si+2]     ; A = divisao de cima
        push word [bp+si]
        mov ax, N-2*K           ; desce de 2a, 3a, 4a: ja ganhou K e perdeu K
        cmp si, -4
        jne .ia
        mov ax, N-K             ; desce da 1a: os K ultimos
.ia:    push ax
        push word [bp+si-2]     ; B = divisao de baixo, o 1o classificado
        push word [bp+si-4]
        push byte 0
        push byte 0
        push bp
        call TROCA
        dec word CONTA
        jnz .troca
        sub word NIVEL, 4
        cmp word NIVEL, -0x10
        jne .nivel

; Distrital: sobem K por sorteio. Os que descem vao para o fim da lista, por
; isso o sorteio i usa so os primeiros (total - i).
        mov word CONTA, 0
.sorteio:
        les di, [bp-0x14]
        les di, [es:di]
        les di, [es:di+0x1B]
        les di, [es:di]
        mov ax, [es:di+8]
        sub ax, CONTA
        push ax
        db 0x9A                 ; call far Random (seg25:212D)
..@rnd: dw 0xFFFF, 0
        mov NIVEL, ax
        push word [bp-0x0E]     ; A = 4a divisao
        push word [bp-0x10]
        push byte N-2*K
        push word [bp-0x12]     ; B = Distrital
        push word [bp-0x14]
        push word NIVEL
        push byte 1
        push bp
        call TROCA
        inc word CONTA
        cmp word CONTA, K
        jne .sorteio
        jmp FIM
