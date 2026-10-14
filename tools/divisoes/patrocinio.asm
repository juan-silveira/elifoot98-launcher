; Patrocinadores (ver patrocinio.py). No fim do seg25 (que arbitro.py leva para o fim do
; arquivo). As marcas vem de <pasta do jogo>\PATROCIN\MARCAS.DAT (o launcher gera do
; MARCAS.TXT; formato em tools/patrocinios/gerar_dat.py: cabecalho de 8 bytes e um
; registro de 80 por marca).
;
; No TTeam (o bloco do cabecalho do save, ingressos.asm):
;   +0xCC byte  marca do patrocinador master (1..N; 0 = nenhum)
;   +0xCD byte  epocas que faltam do contrato (contando a atual)
;   +0xCE dword valor por epoca (reais, ja com a inflacao da assinatura)
;   +0xD2 byte  marca do naming rights do estadio, +0xD3 epocas, +0xD4 dword valor
;   +0xD8 dword patrocinio de jogos recebido na epoca, +0xDC byte a ultima marca dele
;
; -DORG: deslocamento no segmento.
bits 16
org ORG

M_MARCA equ 0xCC
M_ANOS  equ 0xCD
M_VALOR equ 0xCE
N_MARCA equ 0xD2
N_ANOS  equ 0xD3
N_VALOR equ 0xD4

DIVISOES equ 0x26FA                     ; TList das 5 TDivision (4 = Distrital)
INFLACAO equ 0x2B46                     ; real de 10 bytes (comeca em 2,0)
PASTA    equ 0x29FE                     ; pasta do jogo (string)
FILEMODE equ 0x248B
DIALOGO  equ 0x2880                     ; janela de Sim/Nao do aumento de salario

; registro da marca (80 bytes)
R_NOME  equ 0
R_TIER  equ 38
R_DIVMAX equ 39
R_VERBA equ 40
R_COTA  equ 44
R_RESERVA equ 48
R_MODO  equ 49
R_NPAIS equ 50
R_PAISES equ 51

; quadro de distribuir (e das rotinas que ele chama perto)
F       equ -0x90                       ; TFileRec (128 bytes); [bp-2..-0x10] rascunho
CAM     equ -0xE0                       ; caminho do arquivo
REG     equ -0x130                      ; registro da marca
TXT     equ -0x230                      ; texto da proposta
DIN     equ -0x330                      ; valor por extenso / rascunho do jogador
N       equ -0x332
B       equ -0x334
DISP    equ -0x338                      ; verba disponivel da marca (dword)
COTA    equ -0x33C                      ; cota minima com a inflacao
QUOTA   equ -0x340
DV      equ -0x342                      ; divisao
K       equ -0x344                      ; time na lista
OFS     equ -0x3A4                      ; 3 propostas de master x 32 bytes
NOF     equ -0x3A6
OFN     equ -0x3C6                      ; 1 proposta de naming (32 bytes)
NNOF    equ -0x3C8
FM      equ -0x3CA
TIME    equ -0x3CE                      ; o time da vez (far)
LISTA   equ -0x3D2
QUADRO  equ 0x3D4
; proposta (32 bytes): +0 nome (string[24]), +25 epocas, +26 valor (dword), +30 tipo
; (0 master, 1 naming), +31 marca

%macro longe 1
        push cs
        call %1
%endmacro

; ---------------------------------------------------------------- inicio da epoca
; No lugar de "call far seg05:0002" (NewSeason) em seg05:11CE
inicio:
        longe novaepoca
..@opc1: jmp short inicio_ligado        ; opcao PATROC: desligada, salta para inicio_fim
inicio_ligado:
        call distribuir
inicio_fim:
        retf

; ---------------------------------------------------------------- fim da epoca
; No lugar de "call far seg05:304C(campeao, taca)" (premios) em seg05:0AA8
fim:    push bp
        mov bp, sp
..@opc2: jmp short fim_ligado           ; opcao PATROC: desligada, salta para fim_premios
fim_ligado:
        call pagar
fim_premios:
        push word [bp+0xC]
        push word [bp+0xA]
        push word [bp+8]
        push word [bp+6]
        longe premios
        pop bp
        retf 8

; paga a epoca dos contratos (como os Premios) e desconta uma epoca deles
pagar:  push bp
        mov bp, sp
        sub sp, 0x10
        mov word [bp-2], 0              ; divisao
.div:   call lista_div                  ; es:di = TList da divisao [bp-2]
        mov [bp-6], di
        mov [bp-4], es
        mov word [bp-8], 0              ; time
.time:  les di, [bp-6]
        mov ax, [bp-8]
        cmp ax, [es:di+8]
        jae .proxdiv
        call time_k                     ; es:di = TTeam
        mov [bp-0xC], di
        mov [bp-0xA], es
        mov bx, M_MARCA
        call paga1
        les di, [bp-0xC]
        mov bx, N_MARCA
        call paga1
        inc word [bp-8]
        jmp .time
.proxdiv:
        inc word [bp-2]
        cmp word [bp-2], 5
        jb .div
        mov sp, bp
        pop bp
        ret

; es:di = time, bx = M_MARCA ou N_MARCA (o campo das epocas vem logo depois e o valor 2
; depois)
paga1:  cmp byte [es:di+bx], 0
        je .nada
        push bx
        push es
        push di
        push word 3                     ; linha dos Premios no balanco
        push word [es:di+bx+4]
        push word [es:di+bx+2]
        push es
        push di
        longe addmoney
        pop di
        pop es
        pop bx
        dec byte [es:di+bx+1]
        jnz .nada
        mov byte [es:di+bx], 0
        mov word [es:di+bx+2], 0
        mov word [es:di+bx+4], 0
.nada:  ret

; ---------------------------------------------------------------- distribuicao
distribuir:
        push bp
        mov bp, sp
        sub sp, QUADRO
        push si
        mov word [bp+NOF], 0
        mov word [bp+NNOF], 0
        call abrir
        jc .sai
        mov cx, 8                       ; cabecalho: "PAT1", versao, quantidade
        call ler
        jc .fecha
        cmp word [bp+REG], 'PA'
        jne .fecha
        mov ax, [bp+REG+6]
        mov [bp+N], ax
        mov word [bp+B], 0
.marca: mov ax, [bp+B]
        cmp ax, [bp+N]
        jae .fecha
        inc word [bp+B]
        mov cx, 80
        call ler
        jc .fecha
        call orcamento                  ; DISP e COTA
        ; a marca atua nas 2 divisoes do nivel dela: da (divisao maxima - 1) ate a maxima
        mov al, [bp+REG+R_DIVMAX]
        xor ah, ah
        sub ax, 2
        jge .comeco
        xor ax, ax
.comeco:
        mov [bp+DV], ax
.divisao:
        mov al, [bp+REG+R_DIVMAX]
        xor ah, ah
        cmp [bp+DV], ax
        jae .marca
        ; QUOTA = COTA x (5 - divisao) / 2
        mov ax, 5
        sub ax, [bp+DV]
        mov cx, ax
        mov ax, [bp+COTA]
        mov dx, [bp+COTA+2]
        call mul32                      ; dx:ax = dx:ax x cx
        shr dx, 1
        rcr ax, 1
        mov [bp+QUOTA], ax
        mov [bp+QUOTA+2], dx
        call lista_div_dv
        mov [bp+LISTA], di
        mov [bp+LISTA+2], es
        mov word [bp+K], 0
.time:  les di, [bp+LISTA]
        mov ax, [bp+K]
        cmp ax, [es:di+8]
        jae .proxdiv
        inc word [bp+K]
        ; disponivel < quota: acabou a verba da marca
        mov ax, [bp+DISP]
        mov dx, [bp+DISP+2]
        sub ax, [bp+QUOTA]
        sbb dx, [bp+QUOTA+2]
        jl .marca
        mov ax, [bp+K]
        dec ax
        les di, [bp+LISTA]
        call time_ax                    ; es:di = TTeam
        mov [bp+TIME], di
        mov [bp+TIME+2], es
        call pais_ok
        jnc .time
        ; master
        les di, [bp+TIME]
        cmp byte [es:di+M_MARCA], 0
        jne .naming
        call humano                     ; o humano sempre recebe a proposta
        jc .master
        push word 100
        longe random
        cmp ax, 60
        jae .naming
.master:
        mov bx, M_MARCA
        xor cl, cl
        call contratar
        jmp .time
.naming:
        cmp byte [bp+REG+R_TIER], 3
        ja .time
        les di, [bp+TIME]
        cmp byte [es:di+N_MARCA], 0
        jne .time
        call humano
        jc .nomear
        push word 100
        longe random
        cmp ax, 20
        jae .time
.nomear:
        mov bx, N_MARCA
        mov cl, 1
        call contratar
        jmp .time
.proxdiv:
        inc word [bp+DV]
        jmp .divisao
.fecha: call fechar
        call propostas
.sai:   pop si
        mov sp, bp
        pop bp
        ret

; um contrato da marca B com o time TIME: bx = campo (M_MARCA/N_MARCA), cl = tipo da
; proposta (0 master, 1 naming). Para o humano so guarda a proposta.
contratar:
        push bx
        push cx
        push word 3
        longe random
        inc ax
        mov [bp-4], ax                  ; epocas: 1 a 3 (bp-4: fora do quadro usado)
        pop cx
        pop bx
        ; valor: a quota (naming: metade)
        mov ax, [bp+QUOTA]
        mov dx, [bp+QUOTA+2]
        or cl, cl
        jz .valor
        shr dx, 1
        rcr ax, 1
.valor: les di, [bp+TIME]
        push ax
        call humano                     ; (usa o ax)
        pop ax
        jc .proposta
        mov cl, [bp+B]
        mov [es:di+bx], cl
        mov cl, [bp-4]
        mov [es:di+bx+1], cl
        mov [es:di+bx+2], ax
        mov [es:di+bx+4], dx
        sub [bp+DISP], ax
        sbb [bp+DISP+2], dx
        ret
.proposta:
        ; ate 3 de master, 1 de naming
        push si
        or cl, cl
        jnz .pn
        mov si, [bp+NOF]
        cmp si, 3
        jae .cheia
        inc word [bp+NOF]
        shl si, 5
        lea si, [bp+si+OFS]
        jmp .guarda
.pn:    cmp word [bp+NNOF], 0
        jne .cheia
        inc word [bp+NNOF]
        lea si, [bp+OFN]
.guarda:
        mov [ss:si+26], ax
        mov [ss:si+28], dx
        mov [ss:si+30], cl
        mov al, [bp+B]
        mov [ss:si+31], al
        mov al, [bp-4]
        mov [ss:si+25], al
        push di
        push es
        push ss
        pop es
        mov di, si
        push si
        lea si, [bp+REG+R_NOME]
        mov cx, 25
        cld
        push ds
        push ss
        pop ds
        rep movsb
        pop ds
        pop si
        pop es
        pop di
        ; a verba fica reservada para a proposta
        mov ax, [ss:si+26]
        mov dx, [ss:si+28]
        sub [bp+DISP], ax
        sbb [bp+DISP+2], dx
.cheia: pop si
        ret

; ---------------------------------------------------------------- propostas ao humano
propostas:
        xor si, si
.m:     cmp si, [bp+NOF]
        jae .n
        push si
        shl si, 5
        lea si, [bp+si+OFS]
        call perguntar
        pop si
        jnc .n                          ; aceitou uma: as outras ficam de fora
        inc si
        jmp .m
.n:     cmp word [bp+NNOF], 0
        je .fim
        lea si, [bp+OFN]
        call perguntar
.fim:   ret

; ss:si = proposta. Monta o texto, abre a janela de Sim/Nao. Aceitou: assina e CF = 0;
; recusou: CF = 1.
perguntar:
        mov byte [bp+TXT], 0
        push si
        lea di, [ss:si]                 ; nome da marca
        push ss
        push di
        call anexar
        mov di, T_OFERECE
        push cs
        push di
        call anexar
        pop si
        push si
        lea di, [bp+DIN]                ; valor por extenso (seg09:335A)
        push ss
        push di
        push word [ss:si+28]
        push word [ss:si+26]
        longe extenso
        add sp, 4
        lea di, [bp+DIN]
        push ss
        push di
        call anexar
        mov di, T_EPOCA
        push cs
        push di
        call anexar
        pop si
        push si
        mov al, [ss:si+25]
        add al, '0'
        call poe
        cmp byte [ss:si+25], 1
        jne .plural
        mov di, T_EPOCA1
        push cs
        push di
        jmp .epocas
.plural:
        mov di, T_EPOCAS
        push cs
        push di
.epocas:
        call anexar
        pop si
        push si
        cmp byte [ss:si+30], 0
        jne .naming
        mov di, T_MASTER
        push cs
        push di
        jmp .tipo
.naming:
        mov di, T_NAMING
        push cs
        push di
.tipo:  call anexar
        mov di, T_ACEITA
        push cs
        push di
        call anexar
        ; a janela: texto, Sim/Nao, e o ponteiro do jogador num rascunho na pilha
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
        mov di, T_TITULO                ; titulo da janela
        push cs
        push di
        les di, [DIALOGO]
        push es
        push di
        longe settext
        mov di, T_PROPOSTA              ; rotulo de cima (era o nome do treinador)
        push cs
        push di
        les di, [DIALOGO]
        les di, [es:di+0x184]
        push es
        push di
        longe settext
        les di, [DIALOGO]
        push word 0
        les di, [es:di+0x18C]           ; okBtn: some
        push es
        push di
        longe visivel
        les di, [DIALOGO]
        push word 1
        les di, [es:di+0x17C]           ; YesBtn
        push es
        push di
        longe visivel
        les di, [DIALOGO]
        push word 1
        les di, [es:di+0x180]           ; NoBtn
        push es
        push di
        longe visivel
        les di, [DIALOGO]
        push es
        push di
        longe showmodal
        pop si
        cmp ax, 6
        jne .nao
        ; assina com o time do humano
        call time_humano                ; es:di = TTeam do humano
        jc .nao
        mov bx, M_MARCA
        cmp byte [ss:si+30], 0
        je .campo
        mov bx, N_MARCA
.campo: mov al, [ss:si+31]
        mov [es:di+bx], al
        mov al, [ss:si+25]
        mov [es:di+bx+1], al
        mov ax, [ss:si+26]
        mov [es:di+bx+2], ax
        mov ax, [ss:si+28]
        mov [es:di+bx+4], ax
        clc
        ret
.nao:   stc
        ret

; ---------------------------------------------------------------- aba Patrocinio
; Chamada pelo TabSetClick (abas.asm) quando a aba 5 abre: preenche a pagina 5 do
; Notebook. Os controles graficos da pagina, na ordem do DFM (patrocinio.py): 0 e 1 os
; logos (master, naming), 2 a 4 nome/valor/prazo do master, 5 a 7 os do naming, 8 e 9
; os titulos.
aba:    push bp
        mov bp, sp
        sub sp, QUADRO
        push si
        les di, [bp+6]
        les di, [es:di+0x348]           ; time do tecnico
        mov ax, es
        or ax, di
        jz .sai
        les di, [es:di]
        mov ax, es
        or ax, di
        jz .sai
        mov [bp+TIME], di
        mov [bp+TIME+2], es
        les di, [bp+6]
        les di, [es:di+0x1C4]           ; Notebook
        les di, [es:di+0xDC]            ; as paginas
        cmp word [es:di+8], 6
        jb .sai
        les di, [es:di+4]
        les di, [es:di+20]              ; pagina 5
        les di, [es:di+0x92]            ; FControls: os controles graficos dela
        mov ax, es
        or ax, di
        jz .sai
        cmp word [es:di+8], 10
        jb .sai
        mov [bp+LISTA], di
        mov [bp+LISTA+2], es
        ; rotulos na cor da letra do time (o fundo da pagina e o do time)
        mov word [bp-4], 2
.cor:   mov ax, [bp-4]
        call controle
        les di, [es:di+0x34]            ; Font
        push es
        push di
        les di, [bp+TIME]
        les di, [es:di+4]
        push word [es:di+0x44]
        push word [es:di+0x42]
        pop ax
        pop dx
        pop di
        pop es
        push dx
        push ax
        push es
        push di
        longe corfonte
        inc word [bp-4]
        cmp word [bp-4], 10
        jb .cor
        mov word [bp-2], 0
        mov word [bp-4], 2
        mov bx, M_MARCA
        call bloco
        mov word [bp-2], 1
        mov word [bp-4], 5
        mov bx, N_MARCA
        call bloco
.sai:   pop si
        mov sp, bp
        pop bp
        retf 4

; es:di = controle grafico ax da pagina
controle:
        les di, [bp+LISTA]
        les di, [es:di+4]
        shl ax, 2
        add di, ax
        les di, [es:di]
        ret

; texto (ponteiro longe na pilha) no rotulo [bp-4] + dx
rotulo: pop cx                          ; volta
        mov ax, [bp-4]
        add ax, dx
        push cx
        push ax
        call controle
        pop ax
        pop cx
        push es
        push di
        push cx
        jmp settext_perto
settext_perto:
        pop cx                          ; volta (perto) de rotulo
        push cs
        push cx
        jmp settext

; um bloco: bx = M_MARCA ou N_MARCA; [bp-2] = logo, [bp-4] = primeiro rotulo
bloco:  les di, [bp+TIME]
        mov al, [es:di+bx]
        or al, al
        jnz .tem
        jmp .vazio
.tem:   push bx
        xor ah, ah
        mov [bp+B], ax
        call ler_marca
        pop bx
        jnc .leu
        jmp .vazio
.leu:   push bx
        lea ax, [bp+REG+R_NOME]         ; nome
        push ss
        push ax
        xor dx, dx
        call rotulo
        pop bx
        ; valor por epoca
        push bx
        mov byte [bp+TXT], 0
        les di, [bp+TIME]
        lea ax, [bp+DIN]
        push ss
        push ax
        push word [es:di+bx+4]
        push word [es:di+bx+2]
        longe extenso
        add sp, 4
        lea di, [bp+DIN]
        push ss
        push di
        call anexar
        mov di, T_PORTEMP
        push cs
        push di
        call anexar
        lea ax, [bp+TXT]
        push ss
        push ax
        mov dx, 1
        call rotulo
        pop bx
        ; epocas que faltam
        push bx
        mov byte [bp+TXT], 0
        les di, [bp+TIME]
        mov al, [es:di+bx+1]
        add al, '0'
        call poe
        les di, [bp+TIME]
        cmp byte [es:di+bx+1], 1
        jne .varias
        mov di, T_RESTA1
        push cs
        push di
        jmp .prazo
.varias:
        mov di, T_RESTAN
        push cs
        push di
.prazo: call anexar
        lea ax, [bp+TXT]
        push ss
        push ax
        mov dx, 2
        call rotulo
        pop bx
        ; logo: <pasta>\PATROCIN\<arquivo>, se existir
        mov si, PASTA_PAT
        call caminho
        jc .semlogo
        lea si, [bp+REG+25]
        call cam_mais
        call abrir_cam
        jc .semlogo
        call fechar
        mov ax, [bp-2]
        call controle
        lea ax, [bp+CAM]
        push ss
        push ax
        les di, [es:di+0x8E]            ; Picture
        push es
        push di
        longe carregaimg
        push word 1
        jmp .mostra
.semlogo:
        push word 0
.mostra:
        mov ax, [bp-2]
        call controle
        push es
        push di
        longe visivel
        ret
.vazio: cmp bx, M_MARCA
        jne .vn
        mov di, T_SEM
        push cs
        push di
        jmp .vs
.vn:    mov di, T_SEMN
        push cs
        push di
.vs:    xor dx, dx
        call rotulo
        mov di, T_NADA
        push cs
        push di
        mov dx, 1
        call rotulo
        mov di, T_NADA
        push cs
        push di
        mov dx, 2
        call rotulo
        push word 0
        jmp .mostra

; le o registro da marca [bp+B] em REG; CF = 1 se nao deu
ler_marca:
        call abrir
        jc .fim
        mov cx, 8
        call ler
        jc .fecha
        mov si, [bp+B]
.l:     mov cx, 80
        call ler
        jc .fecha
        dec si
        jnz .l
        call fechar
        clc
        ret
.fecha: call fechar
        stc
.fim:   ret

; ---------------------------------------------------------------- ajudantes
; DISP = verba x inflacao/2 - contratos da marca B - reserva; COTA = cota x inflacao/2
orcamento:
        fild dword [bp+REG+R_VERBA]
        fld tword [INFLACAO]
        fmulp st1
        fmul dword [cs:meio]
        fistp dword [bp+DISP]
        fild dword [bp+REG+R_COTA]
        fld tword [INFLACAO]
        fmulp st1
        fmul dword [cs:meio]
        fdiv dword [cs:mil]             ; em milhares redondos
        frndint
        fmul dword [cs:mil]
        fistp dword [bp+COTA]
        fwait
        ; reserva dos patrocinios de jogo: DISP x pct / 100
        mov al, [bp+REG+R_RESERVA]
        xor ah, ah
        mov cx, ax
        mov ax, [bp+DISP]
        mov dx, [bp+DISP+2]
        call mul32
        mov cx, 100
        call div32
        sub [bp+DISP], ax
        sbb [bp+DISP+2], dx
        ; o que ja esta contratado com a marca
        mov word [bp-2], 0
.div:   call lista_div
        mov [bp+LISTA], di
        mov [bp+LISTA+2], es
        mov word [bp+K], 0
.time:  les di, [bp+LISTA]
        mov ax, [bp+K]
        cmp ax, [es:di+8]
        jae .prox
        inc word [bp+K]
        call time_ax
        mov al, [bp+B]
        cmp [es:di+M_MARCA], al
        jne .n
        mov ax, [es:di+M_VALOR]
        mov dx, [es:di+M_VALOR+2]
        sub [bp+DISP], ax
        sbb [bp+DISP+2], dx
.n:     mov al, [bp+B]
        cmp [es:di+N_MARCA], al
        jne .time
        mov ax, [es:di+N_VALOR]
        mov dx, [es:di+N_VALOR+2]
        sub [bp+DISP], ax
        sbb [bp+DISP+2], dx
        jmp .time
.prox:  inc word [bp-2]
        cmp word [bp-2], 5
        jb .div
        ret

; dx:ax = dx:ax x cx (sem sinal, 32 bits)
mul32:  push bx
        mov bx, dx
        mul cx                          ; dx:ax = baixo x cx
        push ax
        push dx
        mov ax, bx
        mul cx                          ; alto x cx
        pop dx
        add dx, ax
        pop ax
        pop bx
        ret
; dx:ax = dx:ax / cx (sem sinal)
div32:  push bx
        mov bx, ax
        mov ax, dx
        xor dx, dx
        div cx
        push ax
        mov ax, bx
        div cx
        pop dx
        pop bx
        ret

; es:di = TList da divisao [bp-2] (do quadro de quem chama)
lista_div:
        mov ax, [bp-2]
lista_div_ax:
        les di, [DIVISOES]              ; cada objeto vem por uma celula que o aponta
        les di, [es:di]                 ; TList das divisoes
        les di, [es:di+4]               ; FList
        shl ax, 2
        add di, ax
        les di, [es:di]                 ; celula
        les di, [es:di]                 ; TDivision
        les di, [es:di+0x1B]            ; celula
        les di, [es:di]                 ; TList das equipes
        ret
lista_div_dv:
        mov ax, [bp+DV]
        jmp lista_div_ax

; es:di = TList, [bp-8] = indice -> es:di = TTeam
time_k: mov ax, [bp-8]
; es:di = TList, ax = indice -> es:di = TTeam (item: celula que aponta o time)
time_ax:
        les di, [es:di+4]
        shl ax, 2
        add di, ax
        les di, [es:di]
        les di, [es:di]
        ret

; es:di = TTeam: CF = 1 se o treinador e humano (seg03:4351)
humano: push es
        push di
        mov ax, [es:di+0xE]
        or ax, [es:di+0x10]
        jz .nao
        les di, [es:di+0xE]
        les di, [es:di]
        cmp byte [es:di+7], 0
        je .nao
        cmp byte [es:di+6], 0
        jne .nao
        pop di
        pop es
        stc
        ret
.nao:   pop di
        pop es
        clc
        ret

; es:di = o (primeiro) time do humano; CF = 1 se nao achou
time_humano:
        push bp
        mov bp, sp
        sub sp, 4
        mov word [bp-2], 0
.div:   call lista_div
        mov word [bp-4], 0
.t:     mov ax, [bp-4]
        cmp ax, [es:di+8]
        jae .prox
        push es
        push di
        call time_ax
        call humano
        jc .achou
        pop di
        pop es
        inc word [bp-4]
        jmp .t
.achou: add sp, 4
        mov sp, bp
        pop bp
        clc
        ret
.prox:  inc word [bp-2]
        cmp word [bp-2], 5
        jb .div
        mov sp, bp
        pop bp
        stc
        ret

; CF = 1 se o time TIME pode ter a marca do registro (pais)
pais_ok:
        cmp byte [bp+REG+R_MODO], 1
        jne .sim                        ; qualquer pais ou preferido
        les di, [bp+TIME]
        les di, [es:di+4]
        mov ax, [es:di+0x4B]            ; pais do time (string[3] em +0x4A)
        mov dl, [es:di+0x4D]
        mov cl, [bp+REG+R_NPAIS]
        xor ch, ch
        jcxz .nao
        lea si, [bp+REG+R_PAISES]
.laco:  cmp ax, [ss:si]
        jne .prox
        cmp dl, [ss:si+2]
        je .sim
.prox:  add si, 3
        loop .laco
.nao:   clc
        ret
.sim:   stc
        ret

; ---------------------------------------------------------------- arquivo
; CAM = <pasta do jogo> + a string em cs:si (CF = 1 se a pasta e comprida demais)
caminho:
        push si
        push ss
        pop es
        lea di, [bp+CAM]
        mov si, PASTA
        mov cl, [si]
        xor ch, ch
        cmp cx, 40
        ja .erro
        cld
        movsb
        rep movsb
        pop si
        push si
        mov cl, [cs:si]
        xor ch, ch
        add [bp+CAM], cl
        inc si
.c:     mov al, [cs:si]
        stosb
        inc si
        loop .c
        pop si
        clc
        ret
.erro:  pop si
        stc
        ret
; acrescenta a CAM a string em ss:si
cam_mais:
        push si
        push ss
        pop es
        mov al, [bp+CAM]
        xor ah, ah
        lea di, [bp+CAM]
        add di, ax
        inc di
        mov cl, [ss:si]
        xor ch, ch
        add [bp+CAM], cl
        inc si
        cld
        push ds
        push ss
        pop ds
        rep movsb
        pop ds
        pop si
        ret

; abre <pasta>\PATROCIN\MARCAS.DAT; CF = 1 se nao deu
abrir:  push si
        mov si, NOME_ARQ
        call caminho
        pop si
        jc abrir_falhou
; abre o arquivo de CAM (so leitura); CF = 1 se nao deu
abrir_cam:
        lea ax, [bp+F]
        push ss
        push ax
        lea ax, [bp+CAM]
        push ss
        push ax
        longe assign
        mov al, [FILEMODE]
        mov [bp+FM], al
        mov byte [FILEMODE], 0          ; so leitura
        lea ax, [bp+F]
        push ss
        push ax
        push word 1
        longe reset
        mov al, [bp+FM]
        mov [FILEMODE], al
        longe ioresult
        or ax, ax
        jnz abrir_falhou
        clc
        ret
abrir_falhou:
        stc
        ret

; le cx bytes para REG; CF = 1 se nao deu
ler:    lea ax, [bp+F]
        push ss
        push ax
        lea ax, [bp+REG]
        push ss
        push ax
        push cx
        push word 0
        push word 0
        longe blockread
        longe ioresult
        or ax, ax
        jnz .erro
        clc
        ret
.erro:  stc
        ret

fechar: lea ax, [bp+F]
        push ss
        push ax
        longe close
        longe ioresult
        ret

; ---------------------------------------------------------------- texto
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
.c:     jcxz .fim
        cmp dl, 250
        jae .fim
        movsb
        inc dl
        dec cx
        jmp .c
.fim:   mov [es:bp+TXT], dl
        pop ds
        pop bp
        pop si
        ret 4
; acrescenta o caractere al
poe:    push di
        mov di, [bp+TXT]
        and di, 0xFF
        cmp di, 250
        jae .fim
        inc byte [bp+TXT]
        inc di
        add di, bp
        mov [ss:di+TXT], al
.fim:   pop di
        ret

meio:   dd 0.5
mil:    dd 1000.0
NOME_ARQ: db .f - NOME_ARQ - 1, '\PATROCIN\MARCAS.DAT'
.f:
PASTA_PAT: db .f - PASTA_PAT - 1, '\PATROCIN\'
.f:

; textos (passados com "mov di; push cs; push di", como os do jogo: a traducao os acha)
T_OFERECE: db .f - T_OFERECE - 1, ` oferece `
.f:
T_EPOCA:   db .f - T_EPOCA - 1, ` por \xe9poca, durante `
.f:
T_EPOCA1:  db .f - T_EPOCA1 - 1, ` \xe9poca`
.f:
T_EPOCAS:  db .f - T_EPOCAS - 1, ` \xe9pocas`
.f:
T_MASTER:  db .f - T_MASTER - 1, `,`, 13, `para ser o patrocinador principal da sua equipa.`
.f:
T_NAMING:  db .f - T_NAMING - 1, `,`, 13, `para dar o nome ao seu est\xe1dio.`
.f:
T_ACEITA:  db .f - T_ACEITA - 1, 13, 13, `Aceita?`
.f:
T_PORTEMP: db .f - T_PORTEMP - 1, ` por \xe9poca`
.f:
T_RESTA1:  db .f - T_RESTA1 - 1, ` \xe9poca restante`
.f:
T_RESTAN:  db .f - T_RESTAN - 1, ` \xe9pocas restantes`
.f:
T_SEM:     db .f - T_SEM - 1, `Sem patrocinador`
.f:
T_SEMN:    db .f - T_SEMN - 1, `Sem naming rights`
.f:
T_NADA:    db 0
T_TITULO:  db .f - T_TITULO - 1, `Patroc\xednio`
.f:
T_PROPOSTA: db .f - T_PROPOSTA - 1, `Proposta de patroc\xednio`
.f:

; saltos longe (um registro de relocacao para cada destino)
novaepoca: db 0xEA
..@novaepoca: dw 0xFFFF, 0
premios:   db 0xEA
..@premios: dw 0xFFFF, 0
addmoney:  db 0xEA
..@addmoney: dw 0xFFFF, 0
random:    db 0xEA
..@random: dw 0xFFFF, 0
assign:    db 0xEA
..@assign: dw 0xFFFF, 0
reset:     db 0xEA
..@reset:  dw 0xFFFF, 0
blockread: db 0xEA
..@blockread: dw 0xFFFF, 0
close:     db 0xEA
..@close:  dw 0xFFFF, 0
ioresult:  db 0xEA
..@ioresult: dw 0xFFFF, 0
extenso:   db 0xEA
..@extenso: dw 0xFFFF, 0
settext:   db 0xEA
..@settext: dw 0xFFFF, 0
visivel:   db 0xEA
..@visivel: dw 0xFFFF, 0
corfonte:  db 0xEA
..@corfonte: dw 0xFFFF, 0
carregaimg: db 0xEA
..@carregaimg: dw 0xFFFF, 0
showmodal: db 0xEA
..@showmodal: dw 0xFFFF, 0
