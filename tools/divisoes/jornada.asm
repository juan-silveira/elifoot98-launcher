; Tela da jornada (TjourneyDlg) abre na pagina do jogo dos treinadores humanos, e nao
; sempre na 1a (jornada.py). Com mais de um humano, na pagina que tem mais deles.
; Fica no fim do seg25. -DORG: deslocamento no segmento; -DP: linhas por pagina.
bits 16
org ORG

JOGOS   equ 0x270A                      ; lista dos jogos da jornada (com os cabecalhos)
PAGINA  equ 0x2B38                      ; pagina mostrada
MAXPAG  equ 32
CONTA   equ -2 * MAXPAG                 ; humanos por pagina (palavras)

%macro longe 1
        push cs
        call %1
%endmacro

; No lugar da chamada a 10:0AFC (as setas) em seg10:02FD, quando a tela abre, logo
; depois de a pagina ir para 0.
abrir:
        call escolher
        db 0xEA
..@setas: dw 0xFFFF, 0                  ; seg10:0AFC

; No lugar da chamada a 10:0CEF em seg10:085C, quando a lista muda no minuto 90 (o
; "pagina = 0" que vinha depois sai).
minuto90:
        call escolher
        db 0xEA
..@lista: dw 0xFFFF, 0                  ; seg10:0CEF

; [PAGINA] = a pagina com mais jogos de times humanos (a primeira, se empatar); 0 sem
; nenhum. A linha de cada jogo e a posicao dele na lista, P linhas por pagina.
escolher:
        push bp
        mov bp, sp
        sub sp, -CONTA
        push si
        push di
        push ss
        pop es
        lea di, [bp+CONTA]
        mov cx, MAXPAG
        xor ax, ax
        cld
        rep stosw
        xor si, si                      ; linha
.l:     les di, [JOGOS]
        les di, [es:di]
        cmp si, [es:di+8]               ; Count
        jge .melhor
        push si
        push es
        push di
        longe item                      ; TList.Get (seg23:0DD0)
        mov es, dx
        mov di, ax
        les di, [es:di]                 ; o jogo
        mov ax, es
        or ax, di
        jz .prox
        push es
        push di
        les di, [es:di+0x2E]            ; mandante
        call humano
        pop di
        pop es
        mov dl, al
        les di, [es:di+0x32]            ; visitante
        call humano
        add al, dl
        jz .prox
        xor ah, ah
        mov bx, ax
        mov ax, si
        cwd
        mov cx, P
        idiv cx
        cmp ax, MAXPAG
        jae .prox
        mov di, ax
        shl di, 1
        add [bp+di+CONTA], bx
.prox:  inc si
        jmp .l
.melhor:
        xor ax, ax                      ; pagina
        xor dx, dx                      ; humanos nela
        xor si, si
.m:     mov di, si
        shl di, 1
        mov cx, [bp+di+CONTA]
        cmp cx, dx
        jbe .mp
        mov dx, cx
        mov ax, si
.mp:    inc si
        cmp si, MAXPAG
        jb .m
        mov [PAGINA], ax
        pop di
        pop si
        mov sp, bp
        pop bp
        ret

; es:di = ponteiro para o TTeam (pode ser nil); al = 1 se o treinador dele e humano
; (TTeam+0x0E -> treinador, +7; o mesmo teste da chicotada, seg03:4FFF)
humano:
        mov ax, es
        or ax, di
        jz .nao
        les di, [es:di]                 ; TTeam
        mov ax, es
        or ax, di
        jz .nao
        les di, [es:di+0x0E]
        mov ax, es
        or ax, di
        jz .nao
        les di, [es:di]                 ; treinador
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
