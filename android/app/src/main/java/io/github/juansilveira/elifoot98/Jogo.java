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
 *   files/wine11.zip, files/w16.zip  Wine do Boxedwine (+ modulos 16-bit)
 *   files/jogo/                      o Elifoot (montado em C:\files), gravavel:
 *                                    editores, patches e saves trabalham aqui
 *   files/root/                      raiz do Linux emulado (C:\windows etc.)
 */
public final class Jogo {
    /** O jogo deriva a senha do eli.cod das datas de C:\windows e C:\windows\system:
     *  elas sao fixadas nesse instante (o mesmo do Linux/macOS), pro eli.cod do
     *  pacote valer. */
    private static final long DATA_FIXA = 946735200000L;  // 2000-01-01 14:00 UTC

    public final File dir, jogo, root, windows;

    public Jogo(Context ctx) {
        dir = ctx.getFilesDir();
        jogo = new File(dir, "jogo");
        root = new File(dir, "root");
        windows = new File(root, "home/username/.wine/drive_c/windows");
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
            for (String nome : new String[] {"wine11.zip", "w16.zip"})
                try (InputStream in = am.open(nome)) { copiar(in, new File(dir, nome)); }
            try (InputStream in = am.open("elifoot.zip")) { extrairJogo(in); }
            escrever(marca, versao);
        }
        prepararWindows(ctx);
    }

    /** Antes de cada execucao: elif98.ini e eli.cod no lugar e datas das pastas fixas. */
    public void prepararWindows(Context ctx) throws IOException {
        File system = new File(windows, "system");
        system.mkdirs();
        File ini = new File(windows, "elif98.ini");
        if (!ini.exists()) escrever(ini, "[System]\r\n");
        File cod = new File(windows, "eli.cod");
        if (!cod.exists())
            try (InputStream in = ctx.getAssets().open("eli.cod")) { copiar(in, cod); }
        instalarFontes(ctx);
        system.setLastModified(DATA_FIXA);
        windows.setLastModified(DATA_FIXA);
    }

    /** Elifoot Sans/Serif (medidas da Arial/Times New Roman, que o jogo usa) em
     *  C:\windows\Fonts; o iniciar.exe diz ao Wine pra usa-las no lugar delas. */
    private void instalarFontes(Context ctx) throws IOException {
        File fonts = new File(windows, "Fonts");
        fonts.mkdirs();
        String[] nomes = ctx.getAssets().list("fontes");
        if (nomes == null) return;
        for (String nome : nomes) {
            File f = new File(fonts, nome);
            if (!f.exists())
                try (InputStream in = ctx.getAssets().open("fontes/" + nome)) { copiar(in, f); }
        }
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
     * Volta o jogo ao original do app: apaga tudo da pasta do jogo menos os saves
     * (JOGOS) e extrai de novo. Desfaz equipes editadas, patches, arbitros e a regra
     * de estrangeiros.
     */
    public void restaurarOriginal(Context ctx) throws IOException {
        File[] itens = jogo.listFiles();
        if (itens != null)
            for (File f : itens) if (!f.getName().equalsIgnoreCase("JOGOS")) apagar(f);
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
