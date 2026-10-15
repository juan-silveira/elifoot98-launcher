; Classificacao em 2 paginas (1a+2a / 3a+4a), no lugar do ListBoxDivision3Click
; (seg06:0922). So as listas 1 e 2 ficam visiveis e desenham a divisao
; [PAGINA] e [PAGINA]+1. As setas "<" e ">" (TLabel) chamam h3 e h4 e ficam
; nos campos das listas 3 e 4 (0x184, 0x188). A seta que nao leva a lado
; nenhum fica cinzenta (cor da fonte) e o clique nela nao faz nada. (Desabilitar
; nao serve: o clique num controlo desabilitado vai para o formulario, que fecha.)
; Montar: nasm -f bin -o paginas.bin paginas.asm
bits 16
org 0x0922

%define PAGINA  0x17D0   ; DS: divisao da lista da esquerda (0 ou 2)
%define PAGINA1 0x17D2   ; DS: divisao da lista da direita (1 ou 3)

; ---- "<": ListBoxDivision3Click(Self, Sender)
h3:     push bp
        mov bp, sp
        xor ax, ax
        jmp short seta

; ---- ">": ListBoxDivision4Click (click4, em 09B9, salta para aqui)
h4:     push bp
        mov bp, sp
        mov ax, 2
seta:   cmp ax, [PAGINA]
        je .sai                         ; ja esta nessa pagina
        call pagina
.sai:   pop bp
        retf 8

; ---- chamado em vez de seg06:0378 ao abrir a Classificacao (bp = quadro do
;      metodo que abre, Self em [bp+6]), depois do classif (classif.asm, seg25),
;      que poe em [PAGINA] a pagina com mais treinadores humanos
abrir:  mov ax, [PAGINA]
        call pagina
        jmp 0x0378

; ---- pagina: ax = 0 ou 2, Self em [bp+6]
pagina: mov [PAGINA], ax
        inc ax
        mov [PAGINA1], ax
        les di, [bp+6]
        mov word [es:di+0x1A0], 0      ; zera o relogio que fecha a janela
        mov si, tab
.l:     cs lodsw
        or ax, ax
        jz .fim
        xchg bx, ax
        cs lodsb
        mov dl, al                      ; +4: cor da fonte em vez de Visible
        and al, 3
        sub al, 2
        jae .p                          ; 2/3: falso/verdadeiro
        add al, 2                       ; 0/1: verdadeiro so na pagina 1/2
        shl al, 1
        cmp al, [PAGINA]
        mov al, 0
        jne .p
        inc ax
.p:     push si
        les di, [bp+6]
        add di, bx
        les di, [es:di]
        test dl, 4
        jnz .cor
        push ax
        push es
        push di
        db 0x9A                         ; call far TControl.SetVisible (seg21:1C77)
..@sv:  dw 0xFFFF, 0
        jmp short .nx
.cor:   xor cx, cx                      ; activa: clBlack
        xor dx, dx
        or al, al
        jnz .k
        mov cl, 0x80                    ; inactiva: clGray (0x00808080)
        mov dx, 0x8080
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

; ---- ListBoxDivision4Click (o metodo publicado da seta ">") comeca em 09B9
        times 0x09B9 - 0x0922 - ($ - $$) db 0x90
click4: jmp h4

tab:    dw 0x18C
        db 0                            ; Label1
        dw 0x190
        db 0                            ; Label2
        dw 0x194
        db 1                            ; Label3
        dw 0x198
        db 1                            ; Label4
        dw 0x17C
        db 2                            ; ListBoxDivision1: esconde e mostra
        dw 0x17C                        ; (redesenha com a divisao nova)
        db 3
        dw 0x180
        db 2                            ; ListBoxDivision2
        dw 0x180
        db 3
        dw 0x184
        db 4+1                          ; seta "<" (campo da lista 3): preta na pagina 2
        dw 0x188
        db 4+0                          ; seta ">" (campo da lista 4): preta na pagina 1
        dw 0
fim:
