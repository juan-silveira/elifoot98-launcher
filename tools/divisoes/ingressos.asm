; Preco dos ingressos escolhido pelo tecnico (ver ingressos.py).
; O TTeam ganha um byte em +0xCB: 0 = nao escolhido (100%), 2 a 12 = a escolha em
; quartos (50% a 300%, de 25 em 25). Vale nos jogos em casa: o preco do jogo (+0x36)
; e multiplicado por ela e a procura cai com ela elevada a 1,3.
;
; -DORG: deslocamento no segmento; -DCAB: o cabecalho EFa no DS (o byte 4 leva o
; valor para o save e de volta); -DPARTE=1: preco e publico (no fim do seg22);
; -DPARTE=2: leitura e gravacao do byte (no fim do seg16).
bits 16
org ORG

TIME_K  equ 0xCB

%if PARTE == 1

; Preco do jogo x escolha do mandante, no lugar de "fld tword [es:di+0x36]" (renda)
preco:  fld tword [es:di+0x36]
        push bx
        call fator
        or bx, bx
        jz .fim
        push bx
        mov bx, sp
        fild word [ss:bx]
        pop bx
        fmul dword [cs:quarto]
        fmulp st1
.fim:   pop bx
        retf

; Publico com o preco: antes do Round (seg25:1241) que o arredonda
publico:
        push bx
        call fator
        or bx, bx
        jz .fim
        shl bx, 2
        fmul dword [cs:bx+procura-8]
.fim:   pop bx
        db 0x9A                         ; Round (seg25:1241)
..@round: dw 0xFFFF, 0
        retf

; es:di = jogo. bx = escolha do mandante (2 a 12; 0 = nenhuma). Preserva es:di
fator:  push es
        push di
        push ax
        xor bx, bx
        les di, [es:di+0x2E]            ; mandante (ponteiro para o TTeam)
        mov ax, es
        or ax, di
        jz .fim
        les di, [es:di]
        mov ax, es
        or ax, di
        jz .fim
        mov bl, [es:di+TIME_K]
        cmp bl, 2
        jb .nada
        cmp bl, 12
        jbe .fim
.nada:  xor bl, bl
.fim:   pop ax
        pop di
        pop es
        ret

quarto:  dd 0.25
; procura com o preco: (escolha/4) ^ -1,3, de 50% a 300%
procura: dd 2.4623, 1.4535, 1.0, 0.7482, 0.5903, 0.4831, 0.4061, 0.3485, 0.3039, 0.2685, 0.2397
%else

; TTeam.Load: no lugar da leitura do .EFT (seg12:15B8, que deixa o byte do cabecalho
; em CAB+4), guarda o byte no time
carregar:
        push bp
        mov bp, sp
        push word [bp+0xC]
        push word [bp+0xA]
        push word [bp+8]
        push word [bp+6]
        db 0x9A                         ; leitura do .EFT (seg12:15B8)
..@ler: dw 0xFFFF, 0
        mov bx, [bp]                    ; quadro do metodo do TTeam
        les di, [ss:bx+6]               ; Self
        mov al, [CAB+4]
        mov [es:di+TIME_K], al
        mov byte [CAB+4], 0
        pop bp
        retf 8

; TTeam.Store: poe o byte do time no cabecalho antes da gravacao (seg12:1694)
gravar: push bp
        mov bp, sp
        mov bx, [bp]
        les di, [ss:bx+6]
        mov al, [es:di+TIME_K]
        mov [CAB+4], al
        push word [bp+0xC]
        push word [bp+0xA]
        push word [bp+8]
        push word [bp+6]
        db 0x9A                         ; gravacao do .EFT (seg12:1694)
..@grava: dw 0xFFFF, 0
        mov byte [CAB+4], 0
        pop bp
        retf 8
%endif
