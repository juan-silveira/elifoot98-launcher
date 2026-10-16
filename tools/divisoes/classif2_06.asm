; Classificacao moderna (classif2.py): no lugar do ListBoxDivision3Click (seg06:0922).
; Os 4 botoes de divisao (1a a 4a, TLabel com Tag = TAGDIV + divisao) tem todos
; OnClick = ListBoxDivision3Click, que salta para o "botao" de classif2.asm (seg25).
; O ListBoxDivision4Click (09B9) fica sem uso e salta para o mesmo lugar.
bits 16
org 0x0922

h3:     db 0xEA                         ; jmp far botao (seg25)
..@botao: dw 0xFFFF, 0

; ---- ListBoxDivision4Click (o metodo publicado) comeca em 09B9
        times 0x09B9 - 0x0922 - ($ - $$) db 0x90
click4: jmp h3
fim:
