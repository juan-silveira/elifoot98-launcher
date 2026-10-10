"""Monta um .asm desta pasta com o nasm e devolve (bytes, rotulos).

Os rotulos vem da listagem do nasm: {nome: endereco}, ja somado o org (o org
vem do proprio .asm ou de -DORG). Rotulos locais (.x) entram sem o prefixo.
"""
import os, subprocess, tempfile

AQUI = os.path.dirname(os.path.abspath(__file__))


def montar(asm, org=0, **defs):
    with tempfile.TemporaryDirectory() as t:
        bin_, lst = os.path.join(t, 'a.bin'), os.path.join(t, 'a.lst')
        cmd = ['nasm', '-f', 'bin', '-I', AQUI + os.sep, '-o', bin_, '-l', lst]
        cmd += ['-D%s=%s' % (k, v) for k, v in defs.items()]
        subprocess.run(cmd + [os.path.join(AQUI, asm)], check=True)
        cod = open(bin_, 'rb').read()
        rot, pendente = {}, None
        for l in open(lst, encoding='latin1'):
            c = l.split()
            # "  12 00000000 55   h3: push bp"  ou  "  12   h3:" sozinho (endereco na linha seguinte)
            if len(c) >= 4 and len(c[1]) == 8 and c[3].endswith(':'):
                rot[c[3][:-1]] = org + int(c[1], 16)
            elif len(c) == 2 and c[1].endswith(':'):
                pendente = c[1][:-1]
            elif pendente and len(c) >= 3 and len(c[1]) == 8:
                rot[pendente] = org + int(c[1], 16)
                pendente = None
    return cod, rot
