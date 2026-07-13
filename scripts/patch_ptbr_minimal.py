#!/usr/bin/env python3
# Versao MINIMAL do patch — apenas 3 substituicoes super seguras pra
# validar toolchain sem risco. Se essa versao abre normal, sabemos que
# o binario aceita patching e o problema esta em UMA string especifica
# da versao completa.

import sys
import shutil
from pathlib import Path

PATCHES = [
    # 3 substituicoes: tamanho exato ou frase sem risco
    ("Avançado",                    "Atacante"),          # 8==8, exato
    ("Actualização de Salário",     "Atualização de Salário"),  # frase com espaco
    ("Todas as jornadas",           "Todas as rodadas"),  # frase com espaco
]


def enc(s: str) -> bytes:
    return s.encode("cp1252", errors="replace")


def apply(exe_bytes: bytearray):
    stats = []
    for old_s, new_s in PATCHES:
        old = enc(old_s)
        new = enc(new_s)
        if len(new) > len(old):
            stats.append((old_s, new_s, 0, "SKIP"))
            continue
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
        stats.append((old_s, new_s, count, "OK" if count > 0 else "(nao achou)"))
    return stats


def main():
    exe = Path(sys.argv[1]).resolve()
    bak = exe.with_suffix(exe.suffix + ".bak")
    if not bak.exists():
        shutil.copy2(exe, bak)
    with open(exe, "rb") as f:
        data = bytearray(f.read())
    stats = apply(data)
    with open(exe, "wb") as f:
        f.write(data)
    for old, new, count, status in stats:
        print(f"{old[:35]:35} -> {new[:30]:30} {count:>3} {status}")


if __name__ == "__main__":
    main()
