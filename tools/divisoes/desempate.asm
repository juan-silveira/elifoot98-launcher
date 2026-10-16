; Desempate da classificacao com vitorias (desempate.py), no lugar do CmpClassificacao
; (seg11:1AB6, a comparacao da bolha seg14:0e0d): a vem antes de b com mais pontos
; (+0x20); empatados, mais vitorias (+0x16); empatados, maior saldo (+0x1C - +0x1E);
; empatados, mais golos marcados (+0x1C) ou iguais (fica a ordem anterior).
; O original nao olhava as vitorias, e fazia as contas no coprocessador.
bits 16
org 0x1AB6

; CmpClassificacao(a, b): far, a em [bp+0x0A], b em [bp+6]; al = 1 se a fica antes
cmp_:   push bp
        mov bp, sp
        les di, [bp+0x0A]
        les di, [es:di]                 ; a (TTeam)
        mov ax, [es:di+0x20]            ; pontos
        mov bx, [es:di+0x16]            ; vitorias
        mov cx, [es:di+0x1C]            ; golos marcados
        mov dx, cx
        sub dx, [es:di+0x1E]            ; saldo
        les di, [bp+6]
        les di, [es:di]                 ; b
        cmp ax, [es:di+0x20]
        jg .sim
        jl .nao
        cmp bx, [es:di+0x16]
        jg .sim
        jl .nao
        mov ax, [es:di+0x1C]
        sub ax, [es:di+0x1E]
        cmp dx, ax
        jg .sim
        jl .nao
        cmp cx, [es:di+0x1C]
        jl .nao
.sim:   mov al, 1
        jmp short .fim
.nao:   xor al, al
.fim:   pop bp
        retf 8
