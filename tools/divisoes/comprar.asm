; Botao Comprar na Lista de Jogadores (TcheckPlayerDlg; comprar.py). Compra o jogador
; selecionado sem sair da busca, com as mesmas regras e o mesmo preco da compra pela
; janela da equipa (TPlantelDlg): o preco e as condicoes saem das rotinas do jogo e a
; compra e o buyBtnClick dela, com a janela escondida.
; Fica no fim do seg25. -DORG: deslocamento no segmento.
bits 16
org ORG

DIALOGO equ 0x2880                      ; janela de Sim/Nao (askMoreSalaryDlg)
PLANTEL equ 0x288E                      ; TPlantelDlg
JOGADOR equ 0x2892                      ; jogador escolhido na janela da equipa (celula)
PRECO   equ 0x2896                      ; preco dele (dword)
TAG     equ 0x0C                        ; TComponent.Tag
TAGCOMPRA equ 0x434D                    ; o Tag do botao Comprar no DFM

TXT     equ -0x100                      ; texto da janela
DIN     equ -0x200                      ; valor por extenso / rascunho
EQUIPA  equ -0x204                      ; equipa do jogador (celula, far)
QUADRO  equ 0x210

%macro longe 1
        push cs
        call %1
%endmacro

; No lugar do comeco do teamBtnClick da Lista (seg10:37AE, push bp; mov bp,sp; mov ax,
; 0x108), que tambem e o do botao Comprar no DFM: o Comprar vai para comprar; o resto
; (o Equipa... e o duplo clique na lista) segue o original.
desvio:
        push bp
        mov bp, sp
        les di, [bp+10]                 ; Sender
        mov ax, es
        or ax, di
        jz .original
        cmp word [es:di+TAG], TAGCOMPRA
        jne .original
        cmp word [es:di+TAG+2], 0
        jne .original
        pop bp
        jmp comprar
.original:
        mov ax, 0x108
        db 0xEA
..@volta: dw 0xFFFF, 0                  ; seg10:37B4

; comprar(Self = Lista, Sender), far
comprar:
        push bp
        mov bp, sp
        sub sp, QUADRO
        push si
        push di
        ; 1) o jogador: o teamBtnClick grava em Self+1E4 e fecha a Lista, que fica aberta
        les di, [bp+6]
        push word [es:di+0x1AE]         ; Sender = teamBtn: o caminho original
        push word [es:di+0x1AC]
        push es
        push di
        longe equipaclick
        les di, [bp+6]
        mov word [es:di+0x104], 0       ; ModalResult
        mov ax, [es:di+0x1E4]
        or ax, [es:di+0x1E6]
        jnz .tem
        jmp .fim
.tem:   ; 2) a equipa dele
        push word 0
        push word 3
        les di, [es:di+0x1E4]
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
        jmp .fim
.eq:    ; 3) a janela da equipa (escondida): equipa, comprador e jogador; o preco
        les di, [bp+6]
        mov ax, [es:di+0x1E8]           ; a equipa do humano
        mov dx, [es:di+0x1EA]
        mov bx, [es:di+0x1E4]           ; o jogador
        mov cx, [es:di+0x1E6]
        les di, [PLANTEL]
        mov [es:di+0x1F4], ax
        mov [es:di+0x1F6], dx
        mov [JOGADOR], bx
        mov [JOGADOR+2], cx
        mov ax, [bp+EQUIPA]
        mov [es:di+0x1F8], ax
        mov ax, [bp+EQUIPA+2]
        mov [es:di+0x1FA], ax
        push es
        push di
        longe preco                     ; seg10:1939
        ; 4) da para comprar? (as condicoes do botao Comprar da janela da equipa)
        mov ax, [PRECO]
        mov dx, [PRECO+2]
        or dx, dx
        jl .naovende
        jg .p
        or ax, ax
        jz .naovende
.p:     les di, [PLANTEL]
        les di, [es:di+0x1F4]
        les di, [es:di]                 ; TTeam do comprador
        cmp dx, [es:di+0x73]
        jl .p2
        jg .semdin
        cmp ax, [es:di+0x71]
        jae .semdin
.p2:    push word [JOGADOR+2]
        push word [JOGADOR]
        push word [bp+EQUIPA+2]
        push word [bp+EQUIPA]
        push es
        push di
        longe podecomprar               ; seg03:633D
        or al, al
        jz .naopode
        ; 5) confirmar
        mov byte [bp+TXT], 0
        mov di, T_COMPRAR
        push cs
        push di
        call anexar
        lea di, [bp+DIN]
        push ss
        push di
        push word [PRECO+2]
        push word [PRECO]
        longe extenso
        add sp, 4
        lea di, [bp+DIN]
        push ss
        push di
        call anexar
        mov di, T_INTERROG
        push cs
        push di
        call anexar
        mov al, 1
        call janela
        cmp ax, 6
        jne .fim
        ; 6) a compra (buyBtnClick da janela da equipa) e a Lista de novo
        les di, [PLANTEL]
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
        les di, [bp+6]
        push word [es:di+0x1AA]         ; Sender = findBtn
        push word [es:di+0x1A8]
        push es
        push di
        longe procurarclick             ; seg10:34F5
        jmp .fim
.naovende:
        mov di, T_NAOVENDE
        push cs
        push di
        jmp .aviso
.semdin:
        mov di, T_SEMDIN
        push cs
        push di
        jmp .aviso
.naopode:
        mov di, T_NAOPODE
        push cs
        push di
.aviso: mov byte [bp+TXT], 0
        call anexar
        mov al, 0
        call janela
.fim:   pop di
        pop si
        mov sp, bp
        pop bp
        retf 8

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

; Mostra TXT na janela de Sim/Nao: al = 1 com Sim e Nao, 0 so com OK. ax = ModalResult.
janela:
        xor ah, ah
        push ax
        les di, [DIALOGO]
        lea ax, [bp+DIN]                ; rascunho: celula em DIN, "jogador" em DIN+4
        mov bx, ax
        add ax, 4
        mov [ss:bx], ax
        mov [ss:bx+2], ss
        mov [es:di+0x190], bx
        mov [es:di+0x192], ss
        lea ax, [bp+TXT]
        push ss
        push ax
        les di, [es:di+0x188]           ; textMessage
        push es
        push di
        longe settext
        mov di, T_TITULO
        push cs
        push di
        les di, [DIALOGO]
        push es
        push di
        longe settext
        mov di, T_TITULO
        push cs
        push di
        les di, [DIALOGO]
        les di, [es:di+0x184]
        push es
        push di
        longe settext
        pop ax
        push ax
        xor al, 1
        push ax
        les di, [DIALOGO]
        les di, [es:di+0x18C]           ; okBtn
        push es
        push di
        longe visivel
        pop ax
        push ax
        push ax
        les di, [DIALOGO]
        les di, [es:di+0x17C]           ; YesBtn
        push es
        push di
        longe visivel
        pop ax
        push ax
        push ax
        les di, [DIALOGO]
        les di, [es:di+0x180]           ; NoBtn
        push es
        push di
        longe visivel
        les di, [DIALOGO]
        push es
        push di
        longe showmodal
        push ax
        mov di, T_TITSAL                ; o titulo volta ao do aumento de salario
        push cs
        push di
        les di, [DIALOGO]
        push es
        push di
        longe settext
        pop ax
        pop cx
        ret

; acrescenta a TXT a string (ponteiro longe na pilha)
anexar: push si
        push bp
        mov bx, sp
        push ds
        lds si, [ss:bx+6]
        lea di, [bp+TXT]
        push ss
        pop es
        mov al, [es:di]
        xor ah, ah
        add di, ax
        inc di
        cld
        lodsb
        mov cl, al
        xor ch, ch
        mov dl, [es:bp+TXT]
.c:     jcxz .f
        cmp dl, 250
        jae .f
        movsb
        inc dl
        dec cx
        jmp .c
.f:     mov [es:bp+TXT], dl
        pop ds
        pop bp
        pop si
        ret 4

T_COMPRAR:  db .f - T_COMPRAR - 1, `Comprar o jogador seleccionado por `
.f:
T_INTERROG: db .f - T_INTERROG - 1, `?`
.f:
T_NAOVENDE: db .f - T_NAOVENDE - 1, `Este jogador n\xe3o est\xe1 \xe0 venda.`
.f:
T_SEMDIN:   db .f - T_SEMDIN - 1, `N\xe3o h\xe1 dinheiro em caixa para comprar este jogador.`
.f:
T_NAOPODE:  db .f - T_NAOPODE - 1, `N\xe3o \xe9 poss\xedvel comprar este jogador.`
.f:
T_TITULO:   db .f - T_TITULO - 1, `Comprar jogador`
.f:
T_TITSAL:   db .f - T_TITSAL - 1, `Actualiza\xe7\xe3o de Sal\xe1rio`
.f:

equipaclick:   db 0xEA
..@equipaclick: dw 0xFFFF, 0
acharequipa:   db 0xEA
..@acharequipa: dw 0xFFFF, 0
preco:         db 0xEA
..@preco:      dw 0xFFFF, 0
podecomprar:   db 0xEA
..@podecomprar: dw 0xFFFF, 0
compraclick:   db 0xEA
..@compraclick: dw 0xFFFF, 0
procurarclick: db 0xEA
..@procurarclick: dw 0xFFFF, 0
extenso:       db 0xEA
..@extenso:    dw 0xFFFF, 0
settext:       db 0xEA
..@settext:    dw 0xFFFF, 0
visivel:       db 0xEA
..@visivel:    dw 0xFFFF, 0
atualizar:     db 0xEA
..@atualizar:  dw 0xFFFF, 0
showmodal:     db 0xEA
..@showmodal:  dw 0xFFFF, 0
