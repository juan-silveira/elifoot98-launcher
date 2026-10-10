; Calendario de Berger para N equipas (N par): usado no seg03 e no seg11.
%define M (N-1)          ; jornadas por turno
%define H (N/2-1)        ; jogos i = 1..H alem do jogo 0

; ---- nucleo: entra al = jornada (0..R-1), bl = equipa k (0..N-1)
;      sai cl = adversario (0..N-1), ch = jogo g, dl = 1 se mandante
;      usa ah (returno); valores todos < 40, contas em 8 bits
nucleo:
        mov ah, 0
        cmp al, M
        jb .turno
        sub al, M
        mov ah, 1               ; returno: troca o mando
.turno: cmp bl, N-1
        jne .n1
        mov cl, al              ; equipa N-1 joga o jogo 0 contra rr
        mov dl, al
        and dl, 1               ; mandante se rr impar
        jmp .g0
.n1:    cmp bl, al
        jne .n2
        mov cl, N-1             ; equipa rr joga o jogo 0 contra N-1
        mov dl, al
        and dl, 1
        xor dl, 1               ; mandante se rr par
.g0:    mov ch, 0
        jmp .fim
.n2:    mov cl, bl
        sub cl, al              ; d = (k - rr) mod M
        jge .n3
        add cl, M
.n3:    cmp cl, H
        ja .y
        mov ch, cl              ; k = x, i = d: jogo g = i
        mov dl, cl
        and dl, 1               ; mandante se i impar
        mov cl, al              ; adversario (rr - i) mod M
        sub cl, ch
        jge .fim
        add cl, M
        jmp .fim
.y:     mov ch, M
        sub ch, cl              ; k = y, i = M - d: jogo g = i
        mov dl, ch
        and dl, 1
        xor dl, 1               ; mandante se i par
        mov cl, al              ; adversario (rr + i) mod M
        add cl, ch
        cmp cl, M
        jb .fim
        sub cl, M
.fim:   xor dl, ah              ; returno espelhado
        ret
