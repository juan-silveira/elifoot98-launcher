package io.github.juansilveira.elifoot98;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;

/**
 * Entrega ao instalador do Android o APK baixado pela atualizacao (cache/atualizacao/),
 * como um content:// (o Android nao aceita file:// de outro app). So leitura, e so a
 * quem recebeu a permissao no Intent.
 */
public final class ApkProvider extends ContentProvider {
    static String autoridade(android.content.Context c) { return c.getPackageName() + ".apk"; }

    static Uri uri(android.content.Context c, File apk) {
        return new Uri.Builder().scheme("content").authority(autoridade(c)).appendPath(apk.getName()).build();
    }

    private File arquivo(Uri u) throws FileNotFoundException {
        String nome = u.getLastPathSegment();
        if (nome == null || nome.contains("/") || !nome.endsWith(".apk")) throw new FileNotFoundException();
        File f = new File(Atualizacao.pasta(getContext()), nome);
        if (!f.isFile()) throw new FileNotFoundException();
        return f;
    }

    @Override public boolean onCreate() { return true; }

    @Override public String getType(Uri u) { return "application/vnd.android.package-archive"; }

    @Override public ParcelFileDescriptor openFile(Uri u, String modo) throws FileNotFoundException {
        if (!"r".equals(modo)) throw new FileNotFoundException();
        return ParcelFileDescriptor.open(arquivo(u), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override public Cursor query(Uri u, String[] colunas, String sel, String[] args, String ordem) {
        MatrixCursor c = new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE });
        try {
            File f = arquivo(u);
            c.addRow(new Object[] { f.getName(), f.length() });
        } catch (FileNotFoundException ignorado) { }
        return c;
    }

    @Override public Uri insert(Uri u, ContentValues v) { return null; }
    @Override public int delete(Uri u, String s, String[] a) { return 0; }
    @Override public int update(Uri u, ContentValues v, String s, String[] a) { return 0; }
}
