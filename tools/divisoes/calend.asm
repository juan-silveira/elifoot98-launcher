; Calendario de N equipas por divisao calculado na hora (metodo de Berger), no
; lugar da tabela fixa de adversarios 8x14 do jogo. Vai no fim do seg03.
; (a tabela de lugares continua no DS, com 4 bits por jornada)
; Montar: nasm -f bin -DN=14 -DORG=0xDB86 -o calend.bin calend.asm
bits 16
org ORG

%define R (2*(N-1))      ; jornadas
%define STRIDE ((R+1)/2) ; bytes de "lugares" por equipa (4 bits por jornada)
%define BUF 0x17C8       ; DS: area das tabelas antigas (agora livre)

; ---- adversario: entra ax = jornada (1..R), bx = equipa k (0..N-1)
;      sai al = adversario (0 vira N), negativo se joga fora.
;      (no ponto de chamada, seg03:1D84, cx, dx e si estao livres)
adversario:
        dec ax
        call nucleo
        mov al, cl
        or al, al
        jnz .a1
        mov al, N
.a1:    or dl, dl
        jnz .a2
        neg al
.a2:    ret

; ---- escada de forca: dx = 14 * posicao / (N - 1) (era 2 * posicao), posicao em [bp-2]
escada:
        mov ax, [bp-2]
        imul ax, ax, 14
        cwd
        mov cx, N-1
        idiv cx
        mov dx, ax
        ret

%include "nucleo.inc"
