; Filtros de Comportamento, Nota e Risco de lesao na Lista de Jogadores (ver filtros.py).
; Fica no fim do seg18, o das rotinas do TComboBox: GetItemIndex (seg18:568B) e
; SetItemIndex (seg18:56B2) sao chamadas como "push cs; call perto", sem relocacao.
;
; -DORG: deslocamento no seg18; -DCOMP, -DNOTA, -DRISCO: campos dos combos no formulario.
bits 16
org ORG

GET_INDEX equ 0x568B
SET_INDEX equ 0x56B2

; Fim da funcao de filtro da busca (seg10:3223, aninhada no findBtnClick), chamada
; pelo trecho no fim do seg10 no lugar de "mov al, [bp-1]": bp e o quadro dela,
; [bp-1] = o jogador passou nos filtros originais, [bp+6] = endereco do ponteiro do
; jogador, [bp+4] = quadro do findBtnClick ([+6] = a janela). Devolve al.
filtro: push si
        cmp byte [bp-1], 0
        je .fim
        mov si, tabela
.laco:  mov di, [bp+4]
        les di, [ss:di+6]               ; a janela
        mov bx, [cs:si]
        les di, [es:di+bx]              ; o combo
        mov ax, es
        or ax, di
        jz .prox
        push si
        push es
        push di
        push cs
        call GET_INDEX                  ; ax = ItemIndex
        pop si
        cmp ax, 0
        jle .prox                       ; -1 ou "Qualquer"
        les di, [bp+6]
        les di, [es:di]                 ; o jogador
        mov bx, [cs:si+2]
        mov dx, [es:di+bx]
        mov cl, [cs:si+4]
        cmp cl, 1
        je .nota
        ja .risco
        dec ax                          ; comportamento: igual ao escolhido
        cmp dx, ax
        jne .fora
        jmp .prox
.nota:  add dx, ax                      ; nota >= 11 - indice
        cmp dx, 11
        jl .fora
        jmp .prox
.risco: cmp dx, ax                      ; risco <= indice - 1
        jge .fora
.prox:  add si, 5
        cmp si, tabela + 15
        jb .laco
        jmp .fim
.fora:  mov byte [bp-1], 0
.fim:   mov al, [bp-1]
        pop si
        retf

; campo do combo no formulario, campo do jogador, modo (0 igual, 1 minimo, 2 maximo)
tabela: dw COMP, 0x2D
        db 0
        dw NOTA, 0x2B
        db 1
        dw RISCO, 0x2F
        db 2

; FormCreate da janela (era seg10:3A65): faz o que fazia e poe os combos novos em
; "Qualquer" (ItemIndex 0; o Delphi 1 nao guarda o ItemIndex no DFM)
criar:  push bp
        mov bp, sp
        les di, [bp+6]
        mov word [es:di+0x1EC], 0
        mov bx, COMP
        call .zero
        mov bx, NOTA
        call .zero
        mov bx, RISCO
        call .zero
        pop bp
        retf 8
.zero:  les di, [bp+6]
        les di, [es:di+bx]
        mov ax, es
        or ax, di
        jz .nada
        push byte 0
        push es
        push di
        push cs
        call SET_INDEX
.nada:  ret
