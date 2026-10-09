// Simulador de temporadas do Elifoot 98 seguindo docs/elifoot98-interno.md (secao 4).
// Mede o efeito de um jogador "candidato" (posicao, nota, lesao, comportamento) no time 0.
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <math.h>
#include <stdint.h>

static uint64_t rs;
static inline uint32_t rnd32(void) { rs ^= rs << 13; rs ^= rs >> 7; rs ^= rs << 17; return (uint32_t)(rs >> 11); }
static inline int Random(int n) { return n <= 0 ? 0 : (int)(((uint64_t)rnd32() * (uint32_t)n) >> 32); }
static inline double RandomReal(void) { return rnd32() / 4294967296.0; }

#define NT 8
#define NP 18
#define DLIGA 37
static int REF_MESMO_PAIS_PCT = 6; // 11 de 182 arbitros sao brasileiros

typedef struct { int pos, forca, nota, lesao, comp, estrela, gols, susp, lesj, v, campo, banco, cand; } Jog;
typedef struct { Jog j[NP]; double moral; int pts, gp, gc, id; } Time;

static Time liga[NT];
// estatisticas
static long long st_pts, st_jogos, c_tit, c_jogos, c_gols, c_exp, c_les, c_fora, c_pen, c_golsTime;

static void atributos(Jog *p, int S) {
    p->nota = S % 10 + 1; p->lesao = S % 11;
    int c = 5 - (int)floor(sqrt((double)(S % 36)));
    if (p->pos == 1) c = (c + 2) % 6;
    p->comp = c; p->estrela = p->nota >= 8 && p->pos >= 2;
}

static void novoTime(Time *t, int id) {
    static const int dist[NP] = {0,0, 1,1,1,1,1,1, 2,2,2,2,2, 3,3,3,3,3};
    memset(t, 0, sizeof *t); t->id = id; t->moral = 1.0;
    for (int i = 0; i < NP; i++) { t->j[i].pos = dist[i]; t->j[i].forca = 30; atributos(&t->j[i], Random(2520)); }
}

// ---- escalacao (seg03:4b73), times do computador: 1/3/3/2 e depois completa ----
static int ordem[NP];
static void escalar(Time *t) {
    for (int i = 0; i < NP; i++) { t->j[i].campo = 0; t->j[i].banco = 0; ordem[i] = i; }
    for (int i = NP - 1; i > 0; i--) { int k = Random(i + 1), x = ordem[i]; ordem[i] = ordem[k]; ordem[k] = x; }
    // ordena por forca + gols (estavel sobre o embaralhado)
    for (int i = 1; i < NP; i++) { int x = ordem[i], kx = t->j[x].forca + t->j[x].gols, k = i - 1;
        while (k >= 0 && t->j[ordem[k]].forca + t->j[ordem[k]].gols < kx) { ordem[k + 1] = ordem[k]; k--; } ordem[k + 1] = x; }
    int vagas[2][4] = {{1, 3, 3, 2}, {1, 10, 10, 10}};
    for (int e = 0; e < 2; e++) {
        int n[4] = {0}, lin = 0;
        for (int i = 0; i < NP; i++) if (t->j[i].campo) { n[t->j[i].pos]++; if (t->j[i].pos) lin++; }
        for (int q = 0; q < NP; q++) { Jog *p = &t->j[ordem[q]];
            if (p->campo || p->susp || p->lesj) continue;
            if (p->pos == 0) { if (n[0] < 1) { p->campo = 1; n[0]++; } }
            else if (lin < 10 && n[p->pos] < vagas[e][p->pos]) { p->campo = 1; n[p->pos]++; lin++; } }
    }
    int nb = 0, temSetor[4] = {0};
    for (int s = 0; s < 4 && nb < 5; s++) for (int q = 0; q < NP; q++) { Jog *p = &t->j[ordem[q]];
        if (p->pos == s && !p->campo && !p->banco && !p->susp && !p->lesj) { p->banco = 1; nb++; temSetor[s] = 1; break; } }
    for (int q = 0; q < NP && nb < 5; q++) { Jog *p = &t->j[ordem[q]];
        if (p->pos && !p->campo && !p->banco && !p->susp && !p->lesj) { p->banco = 1; nb++; } }
}

static void setores(Time *t, double s[4]) {
    s[0] = s[1] = s[2] = s[3] = 0;
    for (int i = 0; i < NP; i++) if (t->j[i].campo == 1) s[t->j[i].pos] += t->j[i].forca + 2 * t->j[i].estrela + 3;
}

static int sortear(Time *t, int tipo) { // 0 expulsao, 1 lesao, 2 autor do gol
    long soma = 0, w[NP];
    for (int i = 0; i < NP; i++) { Jog *p = &t->j[i]; w[i] = 0;
        if (p->campo != 1) continue;
        w[i] = tipo == 0 ? p->comp : tipo == 1 ? p->lesao : (long)p->pos * p->pos * p->forca * p->nota; soma += w[i]; }
    if (soma <= 0) return -1;
    long r = Random((int)soma);
    for (int i = 0; i < NP; i++) { if (r < w[i]) return i; r -= w[i]; }
    return -1;
}

static int penalti(Time *a, Time *d) {
    int bat = -1, gk = -1; long bp = -1;
    for (int i = 0; i < NP; i++) { Jog *p = &a->j[i]; if (p->campo != 1) continue;
        long P = lround((p->pos + 1) * p->forca * (p->nota / 10.0 + 1) / 2); if (P > bp) { bp = P; bat = i; } }
    for (int i = 0; i < NP; i++) { Jog *p = &d->j[i]; if (p->campo != 1) continue;
        if (gk < 0 || p->pos < d->j[gk].pos || (p->pos == d->j[gk].pos && p->forca > d->j[gk].forca)) gk = i; }
    if (bat < 0) return -1;
    int chute = Random((int)bp), defesa = gk < 0 ? 0 : Random(d->j[gk].forca);
    if (Random(4) == 0 && chute < 13) return -1;
    return chute >= defesa ? bat : -1;
}

static void substituir(Time *t, int saiu, int *subs, int expulsao) {
    if (*subs <= 0) return;
    int gk = t->j[saiu].pos == 0;
    if (expulsao && !gk) return;
    for (int q = 0; q < NP; q++) { Jog *p = &t->j[ordem[0] * 0 + q];
        if (p->banco == 1 && (p->pos == 0) == gk) {
            p->banco = 2; p->campo = 1; (*subs)--;
            if (expulsao) { // sai o ultimo de linha da lista
                for (int k = NP - 1; k >= 0; k--) if (t->j[k].campo == 1 && t->j[k].pos) { t->j[k].campo = 2; break; } }
            return; } }
}

static void jogo(Time *h, Time *a) {
    Time *T[2] = {h, a};
    escalar(h); escalar(a);
    int subs[2];
    for (int k = 0; k < 2; k++) { int nb = 0; for (int i = 0; i < NP; i++) nb += T[k]->j[i].banco; subs[k] = nb < 3 ? nb : 3; }
    int V[2]; int mesmo = Random(100) < REF_MESMO_PAIS_PCT;
    for (int k = 0; k < 2; k++) V[k] = Random(11) + 3 * mesmo + (k == 0 ? 2 : 0);
    int gols[2] = {0, 0}, S[2] = {0, 0};
    int ultTipo = 0, ultMin = -99, ultTime = 0, ultPos = 0, ultJog = -1;
    for (int m = 1; m <= 90; m++) {
        double s[2][4]; setores(h, s[0]); setores(a, s[1]);
        int ev = 0;
        // 1. penalti / gol
        int flag[2] = {0, 0};
        if (ultTipo == 4 && ultMin == m - 1 && m % 45 > 1) {
            int k = 1 - ultTime; // adversario do expulso
            if (ultPos == 0 || (ultPos == 1 && Random(3) == 0)) flag[k] = 1;
        }
        int marcou[2] = {0, 0}, tentou[2] = {0, 0};
        if (Random(6000) < V[0] || flag[0]) { tentou[0] = 1; int b = penalti(h, a); if (b >= 0) { marcou[0] = 1; h->j[b].gols++; if (h->j[b].cand) c_pen++; ultJog = b; } }
        int r7 = Random(7000);
        if ((r7 < V[1] || flag[1]) && !marcou[0]) { tentou[1] = 1; int b = penalti(a, h); if (b >= 0) { marcou[1] = 1; a->j[b].gols++; if (a->j[b].cand) c_pen++; ultJog = b; } }
        int pen = marcou[0] || marcou[1] || flag[0] || flag[1];
        if (marcou[0] || marcou[1]) { int k = marcou[0] ? 0 : 1; gols[k]++; S[k]++; ultTipo = 2; ultMin = m; ultTime = k; ev = 1; }
        (void)tentou;
        if (!pen) for (int k = 0; k < 2 && !ev; k++) {
            double *o = s[k], *d = s[1 - k];
            double a1 = 1 + o[1], b1 = 1 + d[3], t1 = a1 / (a1 + b1 + 1);
            double a2 = a1 + o[2], b2 = b1 + d[2], t2 = 2 * a2 / (a2 + b2 + 1);
            double a3 = a2 + o[3], b3 = b2 + 3 * d[0] + d[1], t3 = a3 / (a3 + b3 + 1);
            double Q = log(1 + 4 * (o[3] + o[2] / 2) / (3 * d[0] + d[1] + d[2] / 2 + 1)) * (t1 + t2 + t3) * pow(T[k]->moral / 2, 0.25);
            int g = Q - S[k] > Random(k == 0 ? 90 : 120) || Random(k == 0 ? 180 : 270) == 0;
            if (g && Random(45) < m) {
                int au = sortear(T[k], 2); if (au < 0) continue;
                T[k]->j[au].gols++; gols[k]++; S[k]++; ev = 1; ultTipo = 1; ultMin = m; ultTime = k; ultJog = au;
            }
        }
        // 2. expulsao
        if (!ev) { int r = Random(1000), k = -1;
            if (r < 10 && r > V[0]) k = 0; else if (r < 10 && r > V[1]) k = 1;
            if (k >= 0) { int e = sortear(T[k], 0);
                if (e >= 0) { Jog *p = &T[k]->j[e]; p->campo = 2; p->susp = Random(4) + 1; if (p->cand) c_exp++;
                    ev = 1; ultTipo = 4; ultMin = m; ultTime = k; ultPos = p->pos; substituir(T[k], e, &subs[k], 1); } } }
        // 3. lesao
        if (!ev) { int r = Random(500);
            if (r < 2) { int k = r; int e = sortear(T[k], 1);
                if (e >= 0) { Jog *p = &T[k]->j[e]; p->campo = 2; p->lesj = Random(Random(20)) + 1; p->forca = p->forca > 11 ? p->forca - 10 : 1;
                    if (p->cand) c_les++; ev = 1; ultTipo = 5; ultMin = m; ultTime = k; substituir(T[k], e, &subs[k], 0); } } }
        // 4. gol anulado
        if (m != 46) { int r = Random(200);
            if (r < 10 && r > V[0] && ultTipo == 1 && ultMin == m - 1) { int k = ultTime; gols[k]--; T[k]->j[ultJog].gols--; ultTipo = 6; } }
        // evolucao: contador v
        for (int k = 0; k < 2; k++) { Jog *p = &T[k]->j[Random(NP)]; p->v += p->campo == 1 ? -1 : 1; }
    }
    // resultado
    int ph = gols[0] > gols[1] ? 3 : gols[0] == gols[1] ? 1 : 0, pa = gols[1] > gols[0] ? 3 : gols[0] == gols[1] ? 1 : 0;
    h->pts += ph; a->pts += pa; h->gp += gols[0]; h->gc += gols[1]; a->gp += gols[1]; a->gc += gols[0];
    double ah = ph == 3 ? 2.0 : ph == 1 ? 0.8 : 0.0, aa = pa == 3 ? 2.5 : pa == 1 ? 1.3 : 0.0;
    h->moral = 0.85 * h->moral + 0.15 * ah; a->moral = 0.85 * a->moral + 0.15 * aa;
    for (int k = 0; k < 2; k++) {
        if (T[k]->id == 0) { st_pts += k == 0 ? ph : pa; st_jogos++; c_golsTime += gols[k];
            for (int i = 0; i < NP; i++) if (T[k]->j[i].cand) { Jog *p = &T[k]->j[i]; c_jogos++;
                if (p->campo || p->banco == 2) c_tit++; if (p->susp || p->lesj) { if (!p->campo && p->banco != 2) c_fora++; } } }
        for (int i = 0; i < NP; i++) { Jog *p = &T[k]->j[i];
            int jogou = p->campo != 0 || p->banco == 2;
            if (!jogou) { if (p->susp) p->susp--; if (p->lesj) p->lesj--; } }
    }
}

static void evolucao(void) {
    int idx[NT]; for (int i = 0; i < NT; i++) idx[i] = i;
    for (int i = 1; i < NT; i++) { int x = idx[i], k = i - 1;
        while (k >= 0 && (liga[idx[k]].pts < liga[x].pts || (liga[idx[k]].pts == liga[x].pts && liga[idx[k]].gp - liga[idx[k]].gc < liga[x].gp - liga[x].gc))) { idx[k + 1] = idx[k]; k--; } idx[k + 1] = x; }
    for (int r = 0; r < NT; r++) { Time *t = &liga[idx[r]]; int B = DLIGA - 2 * r;
        for (int i = 0; i < NP; i++) { Jog *p = &t->j[i];
            if (p->v > 0) { if ((p->pos != 0 || Random(2) == 0) && p->forca < 50 && p->forca + 1 <= B + 5) p->forca++; }
            else if (p->v < 0) { if (Random(2) == 0 && p->forca - 1 >= 1 && p->forca - 1 >= B - 5) p->forca--; }
            p->v = 0; } }
}

static void temporada(int medir) {
    for (int i = 0; i < NT; i++) { liga[i].pts = liga[i].gp = liga[i].gc = 0; for (int k = 0; k < NP; k++) liga[i].j[k].gols = 0; }
    for (int volta = 0; volta < 2; volta++) for (int rd = 0; rd < NT - 1; rd++) {
        long long sp = st_pts, sj = st_jogos, a1 = c_tit, a2 = c_jogos, a3 = c_gols, a4 = c_exp, a5 = c_les, a6 = c_fora, a7 = c_pen, a8 = c_golsTime;
        // round robin (metodo do circulo)
        int arr[NT]; arr[0] = 0; for (int i = 1; i < NT; i++) arr[i] = 1 + (i - 1 + rd) % (NT - 1);
        for (int i = 0; i < NT / 2; i++) { Time *x = &liga[arr[i]], *y = &liga[arr[NT - 1 - i]];
            if ((rd + i + volta) % 2) jogo(x, y); else jogo(y, x); }
        evolucao();
        if (!medir) { st_pts = sp; st_jogos = sj; c_tit = a1; c_jogos = a2; c_gols = a3; c_exp = a4; c_les = a5; c_fora = a6; c_pen = a7; c_golsTime = a8; }
    }
    for (int k = 0; k < NP; k++) if (liga[0].j[k].cand && medir) c_gols += liga[0].j[k].gols;
}

static double rodar(int sem, int pos, int nota, int les, int comp, int ntemp) {
    rs = 0x9E3779B97F4A7C15ULL * sem + 12345;
    for (int t = 0; t < NT; t++) novoTime(&liga[t], t);
    int slot = pos == 0 ? 0 : pos == 1 ? 2 : pos == 2 ? 8 : 13;
    Jog *c = &liga[0].j[slot]; c->cand = 1; c->nota = nota; c->lesao = les; c->comp = comp; c->estrela = nota >= 8 && pos >= 2;
    rs ^= 0xABCDEF; // o resto da temporada com sorteios proprios
    rs = 0x9E3779B97F4A7C15ULL * sem + 777;
    temporada(0); // aquecimento
    long long p0 = st_pts, j0 = st_jogos;
    for (int t = 0; t < ntemp; t++) temporada(1);
    return (double)(st_pts - p0) / (st_jogos - j0);
}

int main(int argc, char **argv) {
    // uso: sim pos nota lesao comp sementes temporadas [refpct]
    // compara o candidato com um jogador de referencia (nota 5, lesao 0, Fair Play) no mesmo elenco
    int pos = atoi(argv[1]), nota = atoi(argv[2]), les = atoi(argv[3]), comp = atoi(argv[4]);
    int nsem = atoi(argv[5]), ntemp = atoi(argv[6]);
    if (argc > 7) REF_MESMO_PAIS_PCT = atoi(argv[7]);
    double sx = 0, sxx = 0;
    long long z[10];
    for (int sem = 1; sem <= nsem; sem++) {
        long long *v[] = {&st_pts, &st_jogos, &c_tit, &c_jogos, &c_gols, &c_exp, &c_les, &c_fora, &c_pen, &c_golsTime};
        for (int k = 0; k < 10; k++) z[k] = *v[k];
        double b = rodar(sem, pos, 5, 0, 0, ntemp);
        for (int k = 0; k < 10; k++) *v[k] = z[k];
        double x = rodar(sem, pos, nota, les, comp, ntemp) - b; sx += x; sxx += x * x;
    }
    double mx = sx / nsem, se = sqrt((sxx / nsem - mx * mx) / nsem);
    double J = (double)st_jogos;
    printf("%d %d %d %d dpts/temp=%+.3f se=%.3f titular=%.3f gols/temp=%.2f exp/temp=%.3f les/temp=%.3f fora/temp=%.2f pen/temp=%.3f\n",
        pos, nota, les, comp, mx * 14, se * 14, c_tit / J, c_gols / (J / 14), c_exp / (J / 14), c_les / (J / 14), c_fora / (J / 14), c_pen / (J / 14));
    return 0;
}
