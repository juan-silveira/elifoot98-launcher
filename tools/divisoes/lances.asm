; Janela de detalhes da partida (TsubstitutionDlg, seg02): a lista de lances
; (seg02:24a1) passa a mostrar os amarelos (lance tipo 3) quando a caixinha
; "Cartoes amarelos" esta marcada. Os ramos da expulsao (tipo 4) e da lesao
; (tipo 5) faziam o mesmo texto ("nome" + frase + minuto + "'") e viram um so,
; o que abre o espaco para o do amarelo. Ver lances.py.
bits 16
org 0x2643

%define FIM 0x2740
%define CAMPO 0x194                     ; o campo do antigo GroupBox1 (o quadro do Arbitro)

t4:     cmp byte [0x2B30], 0            ; caixinha dos vermelhos
        mov ax, 0x2471                  ; " expulso "
        jmp short teste
t5:     cmp byte [0x2B31], 0            ; caixinha das lesoes
        mov ax, 0x247B                  ; " lesionou-se "
teste:  jnz .sim
        jmp FIM
.sim:
        push ax
        lea di, [bp-0x22A]
        push ss
        push di
        lea di, [bp-0x120]              ; nome do jogador
        push ss
        push di
        db 0x9A                         ; copia (seg25:196E)
..@c1:  dw 0xFFFF, 0
        mov bx, sp
        push cs
        push word [ss:bx+4]             ; a frase
        db 0x9A                         ; concatena (seg25:19ED)
..@c2:  dw 0xFFFF, 0
        lea di, [bp-0x32A]
        push ss
        push di
        les di, [bp-0x12A]
        mov ax, [es:di+6]               ; minuto
        cwd
        push dx
        push ax
        db 0x9A                         ; IntToStr (seg24:07D0)
..@c3:  dw 0xFFFF, 0
        db 0x9A                         ; concatena
..@c4:  dw 0xFFFF, 0
        mov di, 0x2467                  ; "'"
        push cs
        push di
        db 0x9A                         ; concatena
..@c5:  dw 0xFFFF, 0
        lea di, [bp-0x106]
        push ss
        push di
        push word 0xFF
        db 0x9A                         ; guarda (seg25:1988)
..@c6:  dw 0xFFFF, 0
        pop ax
        jmp FIM

t3:     les di, [bp+6]
        les di, [es:di+CAMPO]           ; caixinha dos amarelos
        push es
        push di
        db 0x9A                         ; TCheckBox.GetChecked (seg18:6856)
..@c7:  dw 0xFFFF, 0
        or al, al
        mov ax, amarelo
        jmp short teste
amarelo: db 9, ' amarelo '
fim:
