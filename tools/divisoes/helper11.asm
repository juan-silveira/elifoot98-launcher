; seg11: "lugar" do jogo da equipa na jornada atual (1..N), calculado na hora.
; A equipa guarda em +0x22 so o numero dela na divisao (0..N-1).
; Entra es:di = equipa; sai al = lugar (2g+1 mandante, 2g+2 visitante).
; Preserva dx (o comparador guarda o 1o lugar em dl); bx e cx ficam livres ali.
; Montar: nasm -f bin -DN=20 -DORG=0x3FAA -o helper11.bin helper11.asm
bits 16
org ORG

lugar:
        push dx
        mov bl, [es:di+0x22]
        mov ax, [0x2b40]        ; jornada atual (1..R)
        dec ax
        call nucleo
        mov al, ch
        shl al, 1
        inc al
        or dl, dl
        jnz .casa
        inc al
.casa:  pop dx
        ret

%include "nucleo.inc"
