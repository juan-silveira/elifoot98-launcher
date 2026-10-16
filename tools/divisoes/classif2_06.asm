; Classificacao moderna (classif2.py): uma divisao por pagina (1a a 4a), no lugar do
; ListBoxDivision3Click (seg06:0922), como paginas.asm. So a lista 1 fica visivel; ela
; desenha a divisao [PAGINA] (a linha e desenhada por classif2.asm, no seg25). O titulo
; e o LabelN da divisao (os 4 no mesmo lugar, so o da pagina visivel). As setas "<" e
; ">" (TLabel nos campos das listas 3 e 4) andam uma divisao; a que nao leva a lado
; nenhum fica cinzenta e o clique nela nao faz nada.
bits 16
org 0x0922

%define PAGINA  0x17D0   ; DS: divisao mostrada (0 a 3)

; ---- "<": ListBoxDivision3Click(Self, Sender)
h3:     push bp
        mov bp, sp
        mov ax, [PAGINA]
        dec ax
        jmp short seta

; ---- ">": ListBoxDivision4Click (click4, em 09B9, salta para aqui)
h4:     push bp
        mov bp, sp
        mov ax, [PAGINA]
        inc ax
seta:   cmp ax, 3
        ja .sai                         ; antes da 1a (-1) ou depois da 4a
        call pagina
.sai:   pop bp
        retf 8

; ---- chamado em vez de seg06:0378 ao abrir a Classificacao, depois do classif2
;      (seg25), que poe em [PAGINA] a divisao com mais treinadores humanos
abrir:  mov ax, [PAGINA]
        call pagina
        jmp 0x0378

; ---- ListBoxDivision4Click (o metodo publicado da seta ">") comeca em 09B9
        times 0x09B9 - 0x0922 - ($ - $$) db 0x90
click4: jmp h4

; ---- pagina: ax = 0 a 3, Self em [bp+6]
pagina: mov [PAGINA], ax
        les di, [bp+6]
        mov word [es:di+0x1A0], 0      ; zera o relogio que fecha a janela
        mov si, tab
.l:     cs lodsw
        or ax, ax
        jz .fim
        xchg bx, ax
        cs lodsb
        mov dl, al
        and al, 0x0F                    ; 0..3: visivel so nessa divisao; 4/5: falso/verdadeiro
        cmp al, 4
        jae .fx
        cmp al, [PAGINA]
        mov al, 0
        jne .p
        inc ax
        jmp short .p
.fx:    sub al, 4
.p:     push si
        les di, [bp+6]
        add di, bx
        les di, [es:di]
        test dl, 0x10
        jnz .cor
        push ax
        push es
        push di
        db 0x9A                         ; call far TControl.SetVisible (seg21:1C77)
..@sv:  dw 0xFFFF, 0
        jmp short .nx
.cor:   test dl, 0x20                   ; "<": ativa se PAGINA > 0
        jz .dir
        cmp word [PAGINA], 0
        jne .branco
        jmp short .cinza
.dir:   cmp word [PAGINA], 3            ; ">": ativa se PAGINA < 3
        jae .cinza
.branco: mov cx, 0x00FF                 ; ativa: branco (0x00FFFFFF)
        mov dx, 0xFFFF
        jmp short .k
.cinza: mov cx, 0x5F                    ; inativa: 0x005F6368
        mov dx, 0x6368
.k:     push cx
        push dx
        les di, [es:di+0x34]            ; TControl.Font
        push es
        push di
        db 0x9A                         ; call far TFont.SetColor (seg19:0FDF)
..@se:  dw 0xFFFF, 0
.nx:    pop si
        jmp short .l
.fim:   ret


tab:    dw 0x18C
        db 0                            ; Label1: so na 1a
        dw 0x190
        db 1                            ; Label2
        dw 0x194
        db 2                            ; Label3
        dw 0x198
        db 3                            ; Label4
        dw 0x17C
        db 4                            ; ListBoxDivision1: esconde e mostra
        dw 0x17C                        ; (redesenha com a divisao nova)
        db 5
        dw 0x184
        db 0x10 + 0x20                  ; seta "<" (campo da lista 3): so a cor
        dw 0x188
        db 0x10                         ; seta ">" (campo da lista 4): so a cor
        dw 0
fim:
