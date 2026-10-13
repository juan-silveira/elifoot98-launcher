; Colunas Nota e Risco de lesao na Lista de Jogadores (Procura), ver procura.py.
; O fim do ListBoxOfPlayersDrawItem (seg10:321F, "leave; retf 0x10") salta para
; ca, no fim do seg10, depois de a linha ser desenhada (seg14:0AD6). No quadro dele:
; [bp-8] e [bp-6] = Left e Top do retangulo da linha, [bp-0xC] = endereco do
; ponteiro do jogador, [bp-0x110] = a lista; [bp-0x210] fica livre para o texto.
; Cada coluna: Canvas.TextOut(Left + x, Top, IntToStr(campo)), com o tipo de letra
; e as cores com que a linha acabou de ser desenhada.
;
; -DORG: deslocamento no seg10; -DNOTA_X, -DRISCO_X: colunas, desde a borda da lista.
bits 16
org ORG

%macro coluna 2                         ; campo, x
        mov ax, [bp-0x8]
        add ax, %2
        push ax
        push word [bp-0x6]
        lea di, [bp-0x210]
        push ss
        push di
        les di, [bp-0xC]
        les di, [es:di]
        push byte 0
        push word [es:di+%1]
%endmacro

%macro canvas 0
        les di, [bp-0x110]
        les di, [es:di+0xDD]            ; Canvas da lista
        push es
        push di
%endmacro

        coluna 0x2B, NOTA_X             ; Nota
        db 0x9A                         ; IntToStr (seg24:07D0)
..@in:  dw 0xFFFF, 0
        canvas
        db 0x9A                         ; TCanvas.TextOut (seg19:1F22)
..@tn:  dw 0xFFFF, 0
        coluna 0x2F, RISCO_X            ; Risco de lesao
        db 0x9A
..@ir:  dw 0xFFFF, 0
        canvas
        db 0x9A
..@tr:  dw 0xFFFF, 0
        leave
        retf 0x10
