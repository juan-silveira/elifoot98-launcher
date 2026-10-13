; TabSetClick da janela do time (ver ingressos.py), no fim do seg02: faz o que fazia
; (Notebook.PageIndex := TabSet.TabIndex) e cuida do preco dos ingressos (o TSpinEdit
; da aba Financas, em %). Chamado tambem pelo OnChange dele (Sender = o campo): grava a
; escolha no time do tecnico; pelas abas, o campo mostra o que o time tem.
;
; -DORG: deslocamento no seg02; -DSELETOR: campo do TSpinEdit no formulario.
bits 16
org ORG

TIME_K  equ 0xCB
TIME    equ 0x348                       ; ponteiro para o TTeam do tecnico

abas:   push bp
        mov bp, sp
        les di, [bp+6]
        les di, [es:di+0x1C8]           ; TabSet
        push word [es:di+0xE4]          ; TabIndex
        les di, [bp+6]
        les di, [es:di+0x1C4]           ; Notebook
        push es
        push di
        db 0x9A                         ; PageIndex := (seg17:3460)
..@pagina: dw 0xFFFF, 0
        les di, [bp+6]
        mov ax, [es:di+SELETOR]
        or ax, [es:di+SELETOR+2]
        jz .fim
        les di, [es:di+TIME]
        mov ax, es
        or ax, di
        jz .fim
        les di, [es:di]
        mov ax, es
        or ax, di
        jz .fim
        les di, [bp+6]
        mov ax, [es:di+SELETOR]
        cmp ax, [bp+0xA]
        jne .mostrar
        mov ax, [es:di+SELETOR+2]
        cmp ax, [bp+0xC]
        jne .mostrar
        les di, [es:di+SELETOR]         ; o campo mudou: grava no time
        push es
        push di
        db 0x9A                         ; Value (seg07:1836) -> dx:ax
..@ler: dw 0xFFFF, 0
        or dx, dx
        jz .positivo
        mov ax, 0                       ; negativo: 50%; grande: 300%
        jl .positivo
        mov ax, 300
.positivo:
        add ax, 12                      ; de 25 em 25, o mais perto
        xor dx, dx
        mov cx, 25
        div cx
        cmp ax, 2
        jae .min
        mov ax, 2
.min:   cmp ax, 12
        jbe .max
        mov ax, 12
.max:   les di, [bp+6]
        les di, [es:di+TIME]
        les di, [es:di]
        mov [es:di+TIME_K], al
        jmp .fim
.mostrar:
        les di, [es:di+TIME]
        les di, [es:di]
        mov al, [es:di+TIME_K]
        or al, al
        jnz .valor
        mov al, 4                       ; nao escolhido: 100%
.valor: mov cl, 25
        mul cl
        push word 0                     ; Value := (longint)
        push ax
        les di, [bp+6]
        les di, [es:di+SELETOR]
        push es
        push di
        db 0x9A                         ; Value := (seg07:1899)
..@escolher: dw 0xFFFF, 0
.fim:   pop bp
        retf 8
