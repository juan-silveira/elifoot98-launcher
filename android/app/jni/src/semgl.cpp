// O app Android compila o Boxedwine sem OpenGL (o Elifoot e GDI 2D). A CPU
// ainda referencia callOpenGL para a instrucao de chamada GL do Wine; sem
// OpenGL nenhum programa chega nela.
#include "boxedwine.h"

void callOpenGL(CPU* cpu, U32 index) {
    (void)cpu;
    (void)index;
}

// Diagnostico: com ELIFOOT_SAIDA definida (pelo app), a saida do Boxedwine/Wine
// (stdout/stderr, que no Android nao vai pra lugar nenhum) vai pra esse arquivo.
#include <cstdio>
#include <cstdlib>
#include <unistd.h>

__attribute__((constructor)) static void redirecionarSaida() {
    const char* arquivo = getenv("ELIFOOT_SAIDA");
    if (!arquivo || !*arquivo) return;
    if (freopen(arquivo, "w", stdout)) {
        setvbuf(stdout, nullptr, _IOLBF, 0);
        dup2(fileno(stdout), fileno(stderr));
    }
}
