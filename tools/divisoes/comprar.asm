; Botao Comprar na Lista de Jogadores (TcheckPlayerDlg; comprar.py). Compra o jogador
; selecionado sem sair da busca, com as mesmas regras e o mesmo preco da compra pela
; janela da equipa (TPlantelDlg): o preco e as condicoes saem das rotinas do jogo e a
; compra e o buyBtnClick dela, com a janela escondida.
; Fica no fim do seg25. -DORG: deslocamento no segmento.
bits 16
org ORG

LISTA   equ 0x28AA                      ; TcheckPlayerDlg (a Lista de Jogadores)
PLANTEL equ 0x288E                      ; TPlantelDlg
JOGADOR equ 0x2892                      ; jogador escolhido na janela da equipa (celula)
PRECO   equ 0x2896                      ; preco dele (dword)
TAG     equ 0x0C                        ; TComponent.Tag
TAGCOMPRA equ 0x434D                    ; o Tag do botao Comprar no DFM

DIN     equ -0x200                      ; valor por extenso / rascunho
EQUIPA  equ -0x204                      ; equipa do jogador (celula, far)
QUADRO  equ 0x210

%macro longe 1
        push cs
        call %1
%endmacro

; No lugar da checagem de pilha do teamBtnClick da Lista (seg10:37B4, call far vira jmp
; far; o push bp, mov bp,sp e mov ax,0x108 ja rodaram), que tambem e o do botao Comprar
; no DFM: o Comprar vai para comprar; o resto (o Equipa... e o duplo clique na lista)
; faz a checagem e volta para o original.
desvio:
        les di, [bp+10]                 ; Sender
        mov bx, es
        or bx, di
        jz .original
        cmp word [es:di+TAG], TAGCOMPRA
        jne .original
        cmp word [es:di+TAG+2], 0
        jne .original
        pop bp
        jmp comprar
.original:
        push cs
        call 0x0444                     ; a checagem de pilha (seg25:0444)
        db 0xEA
..@volta: dw 0xFFFF, 0                  ; seg10:37B9

; comprar(Self = Lista, Sender = o botao Comprar), far. Como o Comprar da janela da
; equipa: compra direto, e a linha fica na Lista, redesenhada (sem preco).
comprar:
        push bp
        mov bp, sp
        sub sp, QUADRO
        push si
        push di
        call preparar                   ; jogador, equipa e preco
        or ax, ax
        jz .fim
        call checar
        or ax, ax
        jz .fim
        les di, [PLANTEL]               ; a compra (buyBtnClick da janela da equipa)
        push word [bp+12]
        push word [bp+10]
        push es
        push di
        longe compraclick               ; seg10:1D66
        lea ax, [bp+DIN]                ; o dinheiro em caixa (textMoney), como no Execute
        push ss
        push ax
        les di, [bp+6]
        les di, [es:di+0x1E8]
        les di, [es:di]
        push word [es:di+0x73]
        push word [es:di+0x71]
        longe extenso
        les di, [bp+6]
        les di, [es:di+0x1E0]
        push es
        push di
        longe settext
        les di, [bp+6]                  ; a linha escolhida e redesenhada: sai e volta
        les di, [es:di+0x184]
        push es
        push di
        longe itemindex
        mov si, ax
        push word -1
        les di, [bp+6]
        push word [es:di+0x186]
        push word [es:di+0x184]
        longe poeindex
        push si
        les di, [bp+6]
        push word [es:di+0x186]
        push word [es:di+0x184]
        longe poeindex
        call preparar                   ; o Comprar de novo (desliga: ja sem preco)
        or ax, ax
        jz .poe
        call checar
.poe:   push ax
        push word [bp+12]
        push word [bp+10]
        longe setenabled
.fim:   pop di
        pop si
        mov sp, bp
        pop bp
        retf 8

; Depois de preparar: ax = 1 se o Comprar da janela da equipa estaria ligado (preco > 0,
; menor que o dinheiro do comprador e seg03:633D deixa), 0 se nao.
checar:
        mov ax, [PRECO]
        mov dx, [PRECO+2]
        or dx, dx
        jl .nao
        jg .p
        or ax, ax
        jz .nao
.p:     les di, [PLANTEL]
        les di, [es:di+0x1F4]
        les di, [es:di]                 ; TTeam do comprador
        cmp dx, [es:di+0x73]
        jl .p2
        jg .nao
        cmp ax, [es:di+0x71]
        jae .nao
.p2:    push word [JOGADOR+2]
        push word [JOGADOR]
        push word [bp+EQUIPA+2]
        push word [bp+EQUIPA]
        push es
        push di
        longe podecomprar               ; seg03:633D
        xor ah, ah
        or al, al
        jz .nao
        mov ax, 1
        ret
.nao:   xor ax, ax
        ret

; Com o frame de quem chama (QUADRO): 1) o jogador da linha escolhida na Lista, como o
; Equipa... (mas sem fechar a Lista nem gravar em +1E4); 2) a equipa dele; 3) a janela
; da equipa (escondida) com equipa, comprador e jogador, e o preco em [PRECO].
; ax = 0 sem jogador ou sem equipa.
preparar:
        les di, [LISTA]
        les di, [es:di+0x184]           ; a lista
        push es
        push di
        longe itemindex
        or ax, ax
        jge .sel
        xor ax, ax
        ret
.sel:   lea bx, [bp+DIN]
        push ss
        push bx
        push ax
        les di, [LISTA]
        les di, [es:di+0x184]
        les di, [es:di+0xD8]            ; Items
        push es
        push di
        les di, [es:di]
        call far [es:di+0xC]            ; Items.Get: o texto em DIN, o ponteiro fica na pilha
        longe linha                     ; seg10:3041 (jogador da linha), tira o ponteiro
        mov [JOGADOR], ax
        mov [JOGADOR+2], dx
        or ax, dx
        jnz .eq0
        ret
.eq0:   push word 0
        push word 3
        les di, [JOGADOR]
        les di, [es:di]
        mov ax, [es:di+4]
        cwd
        push dx
        push ax
        longe acharequipa
        mov [bp+EQUIPA], ax
        mov [bp+EQUIPA+2], dx
        or ax, dx
        jnz .eq
        ret
.eq:    les di, [LISTA]
        mov ax, [es:di+0x1E8]           ; a equipa do humano
        mov dx, [es:di+0x1EA]
        les di, [PLANTEL]
        mov [es:di+0x1F4], ax
        mov [es:di+0x1F6], dx
        mov ax, [bp+EQUIPA]
        mov [es:di+0x1F8], ax
        mov ax, [bp+EQUIPA+2]
        mov [es:di+0x1FA], ax
        push es
        push di
        longe preco                     ; seg10:1939
        mov ax, 1
        ret

; No lugar das chamadas a SetEnabled do Equipa... (seg10: no Execute, no comeco da
; busca e no clique na lista): faz o SetEnabled e liga o Comprar so se ha jogador
; escolhido com preco (a venda), desligado nos outros casos.
ativar:
        push bp
        mov bp, sp
        sub sp, QUADRO
        push si
        push di
        push word [bp+10]
        push word [bp+8]
        push word [bp+6]
        longe setenabled
        xor ax, ax
        cmp byte [bp+10], 0
        je .poe
        call preparar
        or ax, ax
        jz .poe
        call checar
.poe:   push ax
        les di, [LISTA]
        les di, [es:di+0x10]            ; TComponent.FComponents (TList)
        mov cx, [es:di+8]               ; Count
        les di, [es:di+4]               ; List
.b:     jcxz .nada
        push es
        push di
        les di, [es:di]
        cmp word [es:di+TAG], TAGCOMPRA
        jne .prox
        cmp word [es:di+TAG+2], 0
        jne .prox
        add sp, 4
        push es
        push di
        longe setenabled
        jmp .fim
.prox:  pop di
        pop es
        add di, 4
        dec cx
        jmp .b
.nada:  pop ax
.fim:   pop di
        pop si
        mov sp, bp
        pop bp
        retf 6

; No lugar da ultima chamada do handler do menu Procurar (seg04:4249, atualizar(Self, 3)
; depois que a Lista fecha): atualiza tambem as financas (2), que mudam com o Comprar.
fechalista:
        push bp
        mov bp, sp
        push word 2
        push word [bp+8]
        push word [bp+6]
        longe atualizar
        push word [bp+10]
        push word [bp+8]
        push word [bp+6]
        longe atualizar
        pop bp
        retf 6

linha:         db 0xEA
..@linha:      dw 0xFFFF, 0
poeindex:      db 0xEA
..@poeindex:   dw 0xFFFF, 0
itemindex:     db 0xEA
..@itemindex:  dw 0xFFFF, 0
setenabled:    db 0xEA
..@setenabled: dw 0xFFFF, 0
acharequipa:   db 0xEA
..@acharequipa: dw 0xFFFF, 0
preco:         db 0xEA
..@preco:      dw 0xFFFF, 0
podecomprar:   db 0xEA
..@podecomprar: dw 0xFFFF, 0
compraclick:   db 0xEA
..@compraclick: dw 0xFFFF, 0
extenso:       db 0xEA
..@extenso:    dw 0xFFFF, 0
settext:       db 0xEA
..@settext:    dw 0xFFFF, 0
atualizar:     db 0xEA
..@atualizar:  dw 0xFFFF, 0
