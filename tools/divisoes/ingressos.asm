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
BLOCO   equ 21                          ; bytes do cabecalho 4..24 <-> TTeam +0xCB..+0xDF

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
%elif PARTE == 2

; O bloco do time no cabecalho "EFa" do save: bytes 4 a 4+BLOCO-1 do cabecalho <->
; TTeam +0xCB...: o preco dos ingressos (+0xCB) e, desde os patrocinios, os contratos
; (+0xCC a +0xDF, ver patrocinio.asm).

; TTeam.Load: no lugar da leitura do .EFT (seg12:15B8, que deixa o bloco do cabecalho
; em CAB+4), copia o bloco para o time
carregar:
        push bp
        mov bp, sp
        push word [bp+0xC]
        push word [bp+0xA]
        push word [bp+8]
        push word [bp+6]
        db 0x9A                         ; leitura do .EFT (seg12:15B8)
..@ler: dw 0xFFFF, 0
        push si
        push di
        mov bx, [bp]                    ; quadro do metodo do TTeam
        les di, [ss:bx+6]               ; Self
        add di, TIME_K
        mov si, CAB+4
        mov cx, BLOCO
        cld
        rep movsb
        call zerar
        pop di
        pop si
        pop bp
        retf 8

; TTeam.Store: poe o bloco do time no cabecalho antes da gravacao (seg12:1694)
gravar: push bp
        mov bp, sp
        push si
        push di
        push ds
        pop es
        mov di, CAB+4
        mov bx, [bp]
        push ds
        lds si, [ss:bx+6]
        add si, TIME_K
        mov cx, BLOCO
        cld
        rep movsb
        pop ds
        pop di
        pop si
        push word [bp+0xC]
        push word [bp+0xA]
        push word [bp+8]
        push word [bp+6]
        db 0x9A                         ; gravacao do .EFT (seg12:1694)
..@grava: dw 0xFFFF, 0
        call zerar
        pop bp
        retf 8

; zera o bloco em CAB+4 (o cabecalho fica como o jogo o monta)
zerar:  push di
        push ds
        pop es
        mov di, CAB+4
        xor ax, ax
        mov cx, BLOCO
        cld
        rep stosb
        pop di
        ret

%else

; seg12, chamado (perto) no lugar da verificacao de E/S depois de ler o cabecalho
; ([bp-0x32], 50 bytes): faz a verificacao e copia o bloco (bytes 4...) para CAB+4
ajudante:
        db 0x9A                         ; verificacao de E/S (seg25:0408)
..@io:  dw 0xFFFF, 0
        push si
        push di
        push ds
        pop es
        mov di, CAB+4
        lea si, [bp-0x2E]
        push ds
        push ss
        pop ds
        mov cx, BLOCO
        cld
        rep movsb
        pop ds
        pop di
        pop si
        ret
%endif
