package io.github.juansilveira.elifoot98;

import android.content.Context;
import android.content.res.AssetManager;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/**
 * Arquivos do jogo no armazenamento interno do app:
 *   files/wine11.zip                 Wine do Boxedwine
 *   files/jogo/                      o Elifoot (montado em C:\files), gravavel:
 *                                    editores, patches e saves trabalham aqui
 *   files/jogo/otvdm/                o otvdm, que roda o jogo (16 bits), como no
 *                                    Windows e no Linux; WINDOWS e o Windows dele
 *                                    (elif98.ini, eli.cod)
 *   files/root/                      raiz do Linux emulado (C:\windows etc.)
 */
public final class Jogo {
    /** O jogo deriva a senha do eli.cod das datas de WINDOWS e WINDOWS\SYSTEM do otvdm:
     *  elas sao fixadas nesse instante (o mesmo do Linux/macOS), pro eli.cod do
     *  pacote valer. */
    private static final long DATA_FIXA = 946735200000L;  // 2000-01-01 14:00 UTC

    /** windows: o Windows do jogo (o do otvdm); wine: o C:\windows do Wine */
    public final File dir, jogo, root, windows, wine;

    public Jogo(Context ctx) {
        dir = ctx.getFilesDir();
        jogo = new File(dir, "jogo");
        root = new File(dir, "root");
        wine = new File(root, "home/username/.wine/drive_c/windows");
        windows = new File(jogo, "otvdm/WINDOWS");
    }

    public File refereeTxe() { return new File(jogo, "REFEREE.TXE"); }
    public File pastaJogos() { return new File(jogo, "JOGOS"); }
    public File arquivoTeclado() { return new File(jogo, "teclado.txt"); }

    /**
     * Na 1a execucao e quando o app e atualizado: copia o Wine e extrai o jogo.
     * Os saves (JOGOS) nunca sao sobrescritos.
     */
    public void preparar(Context ctx) throws IOException {
        String versao = Long.toString(versaoDoApp(ctx));
        File marca = new File(dir, "versao.txt");
        if (!marca.exists() || !versao.equals(ler(marca).trim())) {
            AssetManager am = ctx.getAssets();
            File zip = new File(dir, "wine11.zip");
            long antes = zip.length();
            try (InputStream in = am.open("wine11.zip")) { copiar(in, zip); }
            if (zip.length() != antes) limparCacheDoWine(zip);
            // modulos de 16 bits do Wine: o modo rapido roda o jogo direto no Win16 do Wine
            try (InputStream in = am.open("w16.zip")) { copiar(in, new File(dir, "w16.zip")); }
            try (InputStream in = am.open("elifoot.zip")) { extrairJogo(in); }
            // a 0.9.3 e a 0.9.4 rodavam o jogo no otvdm: dele fica so o WINDOWS (o
            // elif98.ini com as opcoes e o registro, e o eli.cod)
            File[] velhos = new File(jogo, "otvdm").listFiles();
            if (velhos != null)
                for (File f : velhos) if (!f.getName().equals("WINDOWS")) apagar(f);
            escrever(marca, versao);
            // a extracao pos os arquivos do app por cima: refaz o patch em uso
            String patch = Config.patch(ctx);
            if (!patch.isEmpty() && Patches.existe(ctx, patch))
                try { Patches.trocar(ctx, this, patch); } catch (IOException ignored) { }
        }
        // a regra de estrangeiros escolhida nas Configuracoes
        Boolean liberado = Config.estrangeiros(ctx);
        if (liberado != null)
            try {
                if (TeamCodec.bosmanLiberado(jogo) != liberado) TeamCodec.definirBosman(jogo, liberado);
            } catch (Exception ignored) { }
        equipasSemRepetidas();
        prepararWindows(ctx);
    }

    /**
     * O Boxedwine (-cacheReads) guarda em files/root uma copia de cada arquivo do
     * wine11.zip que o Wine abre, para nao descomprimir de novo a cada vez. Um
     * wine11.zip diferente invalida essas copias; o prefixo (home/, com o registro
     * e as fontes do jogo) fica.
     */
    private void limparCacheDoWine(File zip) throws IOException {
        try (java.util.zip.ZipFile z = new java.util.zip.ZipFile(zip)) {
            java.util.Enumeration<? extends ZipEntry> es = z.entries();
            while (es.hasMoreElements()) {
                ZipEntry e = es.nextElement();
                if (e.isDirectory() || e.getName().startsWith("home/")) continue;
                new File(root, e.getName()).delete();
            }
        }
    }

    /**
     * Dois arquivos de equipe com o mesmo nome a menos de maiusculas (ex.: PALMEIRA.EFT e
     * PALMEIRA.eft) fazem o jogo parar de ler a pasta EQUIPAS ali: as equipes seguintes
     * somem da selecao de paises. Fica so o mais recente (igual ao linux/elifoot98.sh).
     */
    private void equipasSemRepetidas() {
        File[] fs = new File(jogo, "EQUIPAS").listFiles();
        if (fs == null) return;
        java.util.Arrays.sort(fs, (a, b) -> Long.compare(b.lastModified(), a.lastModified()));
        java.util.Set<String> vistos = new java.util.HashSet<>();
        for (File f : fs)
            if (!vistos.add(f.getName().toUpperCase(java.util.Locale.ROOT))) f.delete();
    }

    /** Antes de cada execucao: elif98.ini e eli.cod no lugar e datas das pastas fixas. */
    public void prepararWindows(Context ctx) throws IOException {
        File system = new File(windows, "SYSTEM");
        system.mkdirs();
        File ini = new File(windows, "elif98.ini");
        // Ate a 0.9.2 o jogo usava o C:\windows do Wine: leva as opcoes e o registro
        File antigo = new File(wine, "elif98.ini");
        if (!ini.exists() && antigo.exists())
            Files.copy(antigo.toPath(), ini.toPath());
        if (!ini.exists()) escrever(ini, "[System]\r\n");
        File cod = new File(windows, "eli.cod");
        if (!cod.exists())
            try (InputStream in = ctx.getAssets().open("eli.cod")) { copiar(in, cod); }
        instalarFontes(ctx);
        system.setLastModified(DATA_FIXA);
        windows.setLastModified(DATA_FIXA);
    }

    /**
     * Modo rapido (o jogo no Win16 do Wine, sem o otvdm): o Windows do jogo passa a
     * ser o C:\windows do Wine. O elif98.ini mais recente dos dois (o do otvdm guarda
     * o registro do "Ativar todos os recursos") vai para o outro, o eli.cod vai junto
     * e as datas das pastas ficam fixas, como no otvdm.
     */
    public void prepararWine(Context ctx) throws IOException {
        prepararWindows(ctx);
        File system = new File(wine, "system");
        system.mkdirs();
        File iniOtvdm = new File(windows, "elif98.ini"), iniWine = new File(wine, "elif98.ini");
        if (!iniWine.exists() || iniOtvdm.lastModified() > iniWine.lastModified())
            Files.copy(iniOtvdm.toPath(), iniWine.toPath(), java.nio.file.StandardCopyOption.REPLACE_EXISTING);
        else if (iniWine.lastModified() > iniOtvdm.lastModified())
            Files.copy(iniWine.toPath(), iniOtvdm.toPath(), java.nio.file.StandardCopyOption.REPLACE_EXISTING);
        Files.copy(new File(windows, "eli.cod").toPath(), new File(wine, "eli.cod").toPath(),
            java.nio.file.StandardCopyOption.REPLACE_EXISTING);
        // as copias acima mudam as datas das pastas: fixa por ultimo
        system.setLastModified(DATA_FIXA);
        wine.setLastModified(DATA_FIXA);
        new File(windows, "SYSTEM").setLastModified(DATA_FIXA);
        windows.setLastModified(DATA_FIXA);
    }

    /** Elifoot Sans/Serif (medidas da Arial/Times New Roman, que o jogo usa) em
     *  C:\windows\Fonts; o iniciar.exe diz ao Wine pra usa-las no lugar delas. */
    private void instalarFontes(Context ctx) throws IOException {
        File fonts = new File(wine, "Fonts");
        fonts.mkdirs();
        String[] nomes = ctx.getAssets().list("fontes");
        if (nomes == null) return;
        for (String nome : nomes) {
            File f = new File(fonts, nome);
            if (!f.exists())
                try (InputStream in = ctx.getAssets().open("fontes/" + nome)) { copiar(in, f); }
        }
    }

    /** A alta definicao saiu (0.9.5): apaga a MS Sans Serif TrueType dela de quem a
     *  tinha ligado (em 96 DPI o Wine a preferiria a da fonte bitmap). */
    public void removerFontesAltaDefinicao() {
        File[] fs = new File(wine, "Fonts").listFiles((d, n) -> n.startsWith("MSSansSerifHD"));
        if (fs != null) for (File f : fs) f.delete();
    }

    /**
     * DPI do Wine, gravada no registro antes do Boxedwine abrir: o Wine le a
     * LogPixels uma vez, ao iniciar, e o iniciar.exe /dpi chegava tarde (o jogo
     * desenhava as telas em 96 DPI no desktop de 1600x1200). O registro gravado
     * pelo Wine fica em files/root; antes da 1a gravacao, so no wine11.zip.
     */
    public void definirDpi(int dpi) throws IOException {
        String valor = String.format("\"LogPixels\"=dword:%08x", dpi);
        for (String nome : new String[] { "user.reg", "system.reg" }) {
            String caminho = "home/username/.wine/" + nome;
            File f = new File(root, caminho);
            String texto;
            if (f.exists()) texto = new String(Files.readAllBytes(f.toPath()), StandardCharsets.UTF_8);
            else {
                texto = null;
                try (java.util.zip.ZipFile z = new java.util.zip.ZipFile(new File(dir, "wine11.zip"))) {
                    ZipEntry e = z.getEntry(caminho);
                    if (e != null)
                        try (InputStream in = z.getInputStream(e)) { texto = new String(lerTudo(in), StandardCharsets.UTF_8); }
                }
                if (texto == null) continue;
            }
            String novo = texto.replaceAll("\"LogPixels\"=dword:[0-9a-fA-F]{8}", valor);
            if (novo.equals(texto) && f.exists()) continue;
            f.getParentFile().mkdirs();
            Files.write(f.toPath(), novo.getBytes(StandardCharsets.UTF_8));
        }
        // Letras suavizadas acima de 96 DPI: o otvdm forca a qualidade da fonte
        File ini = new File(jogo, "otvdm/otvdm.ini");
        if (ini.exists()) {
            StringBuilder sb = new StringBuilder();
            for (String l : new String(Files.readAllBytes(ini.toPath()), StandardCharsets.UTF_8).split("\r?\n", -1)) {
                if (l.startsWith("ForceFontQuality=")) continue;
                sb.append(l).append('\n');
                if (dpi > 96 && l.trim().equalsIgnoreCase("[otvdm]")) sb.append("ForceFontQuality=4\n");
            }
            String s = sb.toString().replaceAll("\n+$", "\n");
            Files.write(ini.toPath(), s.getBytes(StandardCharsets.UTF_8));
        }
    }

    private static byte[] lerTudo(InputStream in) throws IOException {
        java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream();
        byte[] b = new byte[65536];
        int n;
        while ((n = in.read(b)) > 0) out.write(b, 0, n);
        return out.toByteArray();
    }

    /** "Ativar todos os recursos": grava secondCode (Registro para autor 2) no elif98.ini. */
    public String ativar(Context ctx) throws IOException {
        prepararWindows(ctx);
        String senha = Registro.senhaDoEliCod(Files.readAllBytes(new File(windows, "eli.cod").toPath()));
        String contra = Registro.contraSenha(senha, Registro.TIPO_AUTOR_2);
        gravarIni(new File(windows, "elif98.ini"), "System", "secondCode", contra);
        windows.setLastModified(DATA_FIXA);
        return contra;
    }

    /**
     * Registro escolhido nas Configuracoes, antes de abrir o jogo: a contra-senha
     * (Registro para autor) no elif98.ini, ou nenhuma (copia nao registada). Os dois
     * elif98.ini (o do otvdm e o do Wine) sao igualados antes, para nao perder opcoes
     * que o jogo gravou num deles.
     */
    public void definirRegistro(Context ctx, boolean registrado) throws IOException {
        prepararWine(ctx);
        String contra = null;
        if (registrado) {
            String senha = Registro.senhaDoEliCod(Files.readAllBytes(new File(windows, "eli.cod").toPath()));
            contra = Registro.contraSenha(senha, Registro.TIPO_AUTOR_2);
        }
        for (File ini : new File[] {new File(windows, "elif98.ini"), new File(wine, "elif98.ini")}) {
            if (registrado) gravarIni(ini, "System", "secondCode", contra);
            else removerIni(ini, "System", "secondCode");
        }
        new File(wine, "system").setLastModified(DATA_FIXA);
        wine.setLastModified(DATA_FIXA);
        new File(windows, "SYSTEM").setLastModified(DATA_FIXA);
        windows.setLastModified(DATA_FIXA);
    }

    /**
     * O jogo aberto por ultimo ("lastGame" do elif98.ini, o nome do save sem o .e98), ou
     * null. Com o jogo rodando, o ini do Wine e o que muda: vale o mais novo dos dois.
     */
    public String ultimoJogo() {
        File iniWine = new File(wine, "elif98.ini"), iniOtvdm = new File(windows, "elif98.ini");
        File ini = iniWine.lastModified() >= iniOtvdm.lastModified() ? iniWine : iniOtvdm;
        try {
            for (String linha : ler(ini).split("\r?\n"))
                if (linha.trim().toLowerCase().startsWith("lastgame="))
                    return linha.trim().substring(9).trim();
        } catch (IOException ignorado) {
        }
        return null;
    }

    // Tira a chave da secao do ini (o jogo usa CRLF)
    static void removerIni(File f, String secao, String chave) throws IOException {
        if (!f.exists()) return;
        List<String> linhas = new ArrayList<>(Arrays.asList(ler(f).replace("\r\n", "\n").split("\n", -1)));
        boolean dentro = false, mudou = false;
        for (int i = 0; i < linhas.size(); i++) {
            String l = linhas.get(i).trim();
            if (l.startsWith("[")) dentro = l.equalsIgnoreCase("[" + secao + "]");
            else if (dentro && l.toLowerCase().startsWith(chave.toLowerCase() + "=")) { linhas.remove(i--); mudou = true; }
        }
        if (mudou) escrever(f, String.join("\r\n", linhas));
    }

    /**
     * Volta o jogo ao original do app: apaga tudo da pasta do jogo menos os saves
     * (JOGOS) e extrai de novo. Desfaz equipes editadas, patches, arbitros e a regra
     * de estrangeiros.
     */
    public void restaurarOriginal(Context ctx) throws IOException {
        File[] itens = jogo.listFiles();
        if (itens != null)
            for (File f : itens)
                // o otvdm fica: o WINDOWS dele tem as opcoes e o registro (elif98.ini)
                if (!f.getName().equalsIgnoreCase("JOGOS") && !f.getName().equals("otvdm")) apagar(f);
        try (InputStream in = ctx.getAssets().open("elifoot.zip")) { extrairJogo(in); }
    }

    /** Um arquivo do jogo como veio no app (do elifoot.zip), ou null. */
    public static byte[] arquivoOriginal(Context ctx, String nome) throws IOException {
        try (ZipInputStream z = new ZipInputStream(ctx.getAssets().open("elifoot.zip"), StandardCharsets.ISO_8859_1)) {
            for (ZipEntry e; (e = z.getNextEntry()) != null; ) {
                if (!e.getName().equalsIgnoreCase(nome)) continue;
                java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream();
                byte[] buf = new byte[1 << 16];
                for (int n; (n = z.read(buf)) > 0; ) out.write(buf, 0, n);
                return out.toByteArray();
            }
        }
        return null;
    }

    private static void apagar(File f) throws IOException {
        File[] filhos = f.isDirectory() ? f.listFiles() : null;
        if (filhos != null) for (File c : filhos) apagar(c);
        if (!f.delete() && f.exists()) throw new IOException("Não consegui apagar " + f.getName());
    }

    // Equivalente ao WritePrivateProfileString (o jogo usa CRLF)
    static void gravarIni(File f, String secao, String chave, String valor) throws IOException {
        List<String> linhas = new ArrayList<>();
        if (f.exists()) {
            String t = ler(f).replace("\r\n", "\n");
            while (t.endsWith("\n")) t = t.substring(0, t.length() - 1);
            if (!t.isEmpty()) linhas.addAll(Arrays.asList(t.split("\n", -1)));
        }
        int inicio = -1;
        for (int i = 0; i < linhas.size(); i++)
            if (linhas.get(i).trim().equalsIgnoreCase("[" + secao + "]")) { inicio = i; break; }
        if (inicio < 0) { linhas.add(0, "[" + secao + "]"); inicio = 0; }
        int fim = linhas.size();
        for (int i = inicio + 1; i < linhas.size(); i++)
            if (linhas.get(i).trim().startsWith("[")) { fim = i; break; }
        boolean feito = false;
        for (int i = inicio + 1; i < fim; i++)
            if (linhas.get(i).toLowerCase().startsWith(chave.toLowerCase() + "=")) {
                linhas.set(i, chave + "=" + valor);
                feito = true;
            }
        if (!feito) linhas.add(inicio + 1, chave + "=" + valor);
        escrever(f, String.join("\r\n", linhas) + "\r\n");
    }

    private void extrairJogo(InputStream zip) throws IOException {
        jogo.mkdirs();
        String base = jogo.getCanonicalPath() + File.separator;
        try (ZipInputStream z = new ZipInputStream(zip, StandardCharsets.ISO_8859_1)) {
            for (ZipEntry e; (e = z.getNextEntry()) != null; ) {
                File destino = new File(jogo, e.getName());
                if (!destino.getCanonicalPath().startsWith(base)) continue;
                if (e.isDirectory()) { destino.mkdirs(); continue; }
                boolean save = e.getName().toUpperCase().startsWith("JOGOS/");
                if (save && destino.exists()) continue;  // preserva os saves
                File pai = destino.getParentFile();
                if (pai != null) pai.mkdirs();
                copiar(z, destino, false);
            }
        }
    }

    private static long versaoDoApp(Context ctx) {
        try {
            return ctx.getPackageManager().getPackageInfo(ctx.getPackageName(), 0).lastUpdateTime;
        } catch (Exception e) {
            return 0;
        }
    }

    private static void copiar(InputStream in, File destino) throws IOException {
        copiar(in, destino, true);
    }

    private static void copiar(InputStream in, File destino, boolean fecharEntrada) throws IOException {
        byte[] buf = new byte[1 << 16];
        try (OutputStream out = new FileOutputStream(destino)) {
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        } finally {
            if (fecharEntrada) in.close();
        }
    }

    static String ler(File f) throws IOException {
        return new String(Files.readAllBytes(f.toPath()), StandardCharsets.ISO_8859_1);
    }

    static void escrever(File f, String texto) throws IOException {
        Files.write(f.toPath(), texto.getBytes(StandardCharsets.ISO_8859_1));
    }
}
