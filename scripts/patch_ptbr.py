#!/usr/bin/env python3
# Aplica traducoes PT-PT -> PT-BR em ELIFOOT.EXE (Delphi 1 / Windows 3.x NE).
#
# Estrategia SEGURA:
#   Strings visiveis do jogo sao Pascal ShortString: 1 byte de comprimento (L)
#   + L bytes de texto (cp1252). Muitas dessas strings vivem DENTRO de
#   recursos DFM (form definitions) — Caption de forms, itens de menu,
#   Memo.Lines.
#
#   REGRA CRITICA: NUNCA reduzir o length byte. Se o texto novo for menor
#   que o antigo, mantemos o length original e fazemos padding com espacos
#   dentro do proprio texto renderizado.
#
#   Por que? O parser DFM le a estrutura em cadeia: form -> property ->
#   next property -> next control. Se eu reduzo o length byte, o parser
#   pula uns bytes a menos e tenta ler os "sobrantes" como se fossem o
#   proximo campo, batendo em bytes aleatorios (EReadError).
#
#   Mantendo o length byte original + pad com espacos, o buffer preserva
#   tanto o tamanho quanto a semantica de "aqui tem L bytes de texto".
#   Delphi renderiza os espacos no fim mas nao quebra parser.

import sys
import shutil
from pathlib import Path

PATCHES = [
    # =========================================================================
    # FRASES DE UI CURTAS (labels de botao, menu, dialogs)
    # =========================================================================

    # Menu Arquivo e afins
    ("Editor de Equipas",                  "Editor de Times"),
    ("Sortear equipas",                    "Sortear times"),
    ("Nova equipa",                        "Novo time"),
    ("Próxima equipa",                     "Próximo time"),
    ("Gravar Equipa",                      "Salvar Time"),
    ("Gravar a equipa",                    "Salvar o time"),
    ("Abrir uma equipa",                   "Abrir um time"),
    ("Nome do treinador da equipa",        "Nome do treinador do time"),

    # Filenames em dialogs
    ("Erro a abrir o ficheiro ",           "Erro ao abrir o arquivo "),
    ("Erro a abrir o ficheiro",            "Erro ao abrir o arquivo"),
    ("Nome do ficheiro inválido",          "Nome do arquivo inválido"),
    ("Nome do ficheiro (max. 8 letras)",   "Nome do arquivo (max. 8 letras)"),
    ("Nome do ficheiro (8 letras):",       "Nome do arquivo (8 letras):"),
    ("Nome do ficheiro para gravar o novo jogo",
                                           "Nome do arquivo pra salvar o jogo novo"),
    ("Verifique a directoria ",            "Verifique o diretório  "),

    # Campeonato / estatisticas
    ("Golos esta época",                   "Gols nesta época"),
    ("Todas as jornadas",                  "Todas as rodadas"),
    ("De 3 em 3 jornadas",                 "De 3 em 3 rodadas"),
    ("Melhores marcadores de sempre",      "Maiores artilheiros"),
    ("Melhores marcadores desta época",    "Melhores artilheiros"),

    # Mensagens de erro
    ("Não tem guarda-redes",               "Não tem goleiros"),
    ("Não pode vender mais guarda-redes",  "Não pode vender mais goleiros"),
    ("Não pode vender mais jogadores de campo",
                                           "Não pode vender mais jogadores de linha"),
    ("Não está definido o nome da equipa", "Não está definido o nome do time"),
    ("Não está definido o país da equipa", "Não está definido o país do time"),
    ("Não está definido o nível da equipa","Não está definido o nível do time"),
    ("Não se pode gravar a equipa porque:","Não se pode salvar o time porque:"),
    ("Não mostrar os jogadores da minha equipa",
                                           "Não mostrar os jogadores do meu time"),

    # Header de dialog
    ("Actualização de Salário",            "Atualização de Salário"),

    # Listas de itens (sem prefixo "- ", sao short strings de menu)
    ("Equipas com empréstimos...",         "Times com empréstimos..."),
    ("Equipas com empréstimos",            "Times com empréstimos"),
    ("Equipas endividadas...",             "Times endividados..."),
    ("Equipas endividadas",                "Times endividados"),
    ("Equipas existentes",                 "Times existentes"),
    ("Equipas seleccionadas",              "Times selecionados"),
    ("Países seleccionados",               "Países selecionados"),
    ("Selecção de Países",                 "Seleção de Países"),

    # =========================================================================
    # PALAVRAS ISOLADAS
    # =========================================================================
    #
    # REGRA: palavras ASCII simples (Ficheiro, Equipa, Golos, Plantel...)
    # sao usadas por Delphi como Caption E como nome de componente ao mesmo
    # tempo (o TMenuItem chamado 'Ficheiro' tambem tem Caption='Ficheiro').
    # Se substituo "Ficheiro" por "Arquivo " (com espaco pra pad), o nome
    # do componente vira "Arquivo " que eh identifier invalido — o parser
    # dispara EComponentError.
    #
    # So substituo palavras isoladas quando:
    #   a) len(new) == len(old)  → nao precisa pad, seguro
    #   b) old contem '-', espaco ou caractere nao-identifier → nao pode
    #      ser identifier Delphi, seguro adicionar pad
    ("Guarda-redes", "Goleiro"),      # tem '-', nao pode ser identifier
    ("Avançado",     "Atacante"),     # 8 == 8, sem pad

    # NOTA: NAO substituir as frases longas com prefixo "- " ou " " —
    # essas estao em TMemo.Lines (recursos DFM) e quebrar length byte
    # la dentro dispara EReadError no runtime Delphi.
    #
    # NAO substituir frases dentro de TMemo:
    #   "- Efectuar a compra directa de jogadores..."
    #   "- Visualizar o plantel..."
    #   "- Obter informacao..."
    #   "- Seleccionar, um a um, os jogadores..."
    #   " Efectuar substituições..."
    #   "Uma vez que este jogador..." (dialog com Memo)
    #   "Se não aceitar o aumento..." (dialog com Memo)
    #   "Algumas das equipas dos países..." (dialog com Memo)
    #   "Não é considerado jogador estrangeiro..." (dialog com Memo)
    #   "Este jogador só pode ser vendido..." (dialog com Memo)
    #   "As novas bancadas..." (dialog com Memo)
    #   "Só pode tentar vender..." (dialog com Memo)
    #   "O nível da equipa servirá..." (dialog com Memo)
    #   "Seleccione a equipa para onde..." (label longo)
    #   "Escolha um país da lista..." (label longo)
]


def enc(s: str) -> bytes:
    return s.encode("cp1252", errors="replace")


def apply(exe_bytes: bytearray):
    stats = []
    for old_s, new_s in PATCHES:
        old = enc(old_s)
        new = enc(new_s)
        if len(new) > len(old):
            stats.append((old_s, new_s, 0, f"SKIP: novo({len(new)}) > antigo({len(old)})"))
            continue

        # LENGTH BYTE PRESERVADO: mantemos len(old), pad com espaco.
        # Isso evita corromper o parser DFM que le structs em cadeia.
        old_pat = bytes([len(old)]) + old
        pad = b" " * (len(old) - len(new))
        new_pat = bytes([len(old)]) + new + pad

        count = 0
        pos = 0
        while True:
            i = exe_bytes.find(old_pat, pos)
            if i < 0:
                break
            exe_bytes[i:i + len(old_pat)] = new_pat
            count += 1
            pos = i + len(old_pat)

        status = "OK" if count > 0 else "(nao achou)"
        stats.append((old_s, new_s, count, status))
    return stats


def main():
    if len(sys.argv) < 2:
        print("uso: patch_ptbr.py <ELIFOOT.EXE>")
        sys.exit(2)

    exe = Path(sys.argv[1]).resolve()
    if not exe.exists():
        print(f"ERRO: {exe} nao existe")
        sys.exit(1)

    bak = exe.with_suffix(exe.suffix + ".bak")
    if not bak.exists():
        shutil.copy2(exe, bak)
        print(f"backup criado: {bak}")

    with open(exe, "rb") as f:
        data = bytearray(f.read())

    stats = apply(data)

    with open(exe, "wb") as f:
        f.write(data)

    print()
    print(f"{'ORIGEM':40} {'DESTINO':32} {'HITS':>5}  STATUS")
    print("-" * 90)
    ok_total = 0
    for old, new, count, status in stats:
        print(f"{old[:39]:40} {new[:31]:32} {count:>5}  {status}")
        if count > 0:
            ok_total += count
    print()
    print(f"Total: {ok_total} substituicoes em {sum(1 for _,_,c,_ in stats if c>0)}/{len(stats)} patches")


if __name__ == "__main__":
    main()
