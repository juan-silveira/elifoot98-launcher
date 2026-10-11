using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;

namespace ElifootLauncher
{
    // Aviso de "nova versao disponivel": le https://elifoot98.web.app/versao.json
    // em segundo plano, ao abrir o launcher. Formato:
    //   {"versao":"0.9.2","novidades":"...","pagina":"https://...",
    //    "links":{"windows":"...","linux-deb":"...","linux-appimage":"...","macos":"...","android":"..."}}
    // Qualquer falha (sem internet, timeout, JSON ruim, campo faltando) e silenciosa: sem aviso.
    //
    // Pra testar sem publicar uma versao nova: ELIFOOT_VERSAO_URL=<url> troca o endereco
    // (ex.: ELIFOOT_VERSAO_URL=file:///tmp/versao.json ou um http://localhost:8000/versao.json).
    //
    // Compartilhado entre o launcher do Windows (net48) e o do Linux/Mac (net8): JSON lido
    // por um parser minimo daqui mesmo, pra nao adicionar dependencia no net48.
    public sealed class InfoAtualizacao
    {
        public string Versao = "";
        public string Novidades = "";
        public string Link = "";
    }

    public static class Atualizacao
    {
        public const string UrlPadrao = "https://elifoot98.web.app/versao.json";
        private const int TimeoutMs = 5000;

        /// <summary>
        /// Busca o versao.json e devolve a info so se a versao remota for mais nova que
        /// <paramref name="atual"/>. Nunca lanca excecao. <paramref name="plataforma"/> e a chave
        /// em "links" (windows, linux-deb, linux-appimage, macos); sem link cai em "pagina".
        /// </summary>
        public static async Task<InfoAtualizacao?> VerificarAsync(Version? atual, string plataforma)
        {
            try
            {
                if (atual == null) return null;
                var url = Environment.GetEnvironmentVariable("ELIFOOT_VERSAO_URL");
                if (string.IsNullOrWhiteSpace(url)) url = UrlPadrao;
                var json = await Baixar(url!.Trim()).ConfigureAwait(false);
                return json == null ? null : Interpretar(json, atual, plataforma);
            }
            catch { return null; }
        }

        public static InfoAtualizacao? Interpretar(string json, Version atual, string plataforma)
        {
            if (!(JsonMinimo.Ler(json) is Dictionary<string, object?> raiz)) return null;
            if (!(raiz.TryGetValue("versao", out var v) && v is string versao)) return null;
            var remota = Componentes(versao);
            if (remota == null) return null;
            var local = new[] { atual.Major, atual.Minor, Math.Max(atual.Build, 0) };
            if (Comparar(remota, local) <= 0) return null;

            string? link = null;
            if (raiz.TryGetValue("links", out var l) && l is Dictionary<string, object?> links
                && links.TryGetValue(plataforma, out var p) && p is string s && UrlValida(s))
                link = s;
            if (link == null && raiz.TryGetValue("pagina", out var pg) && pg is string pagina && UrlValida(pagina))
                link = pagina;
            if (link == null) return null;   // sem pra onde mandar o "Baixar": nao mostra

            var novidades = raiz.TryGetValue("novidades", out var n) && n is string ns ? ns : "";
            return new InfoAtualizacao { Versao = versao.Trim(), Novidades = Resumir(novidades, 120), Link = link.Trim() };
        }

        // "0.9.10" -> [0,9,10]; aceita "v" na frente e ignora sufixo ("-beta"). null se invalida.
        public static int[]? Componentes(string versao)
        {
            var t = versao.Trim();
            if (t.StartsWith("v", StringComparison.OrdinalIgnoreCase)) t = t.Substring(1);
            var corte = t.IndexOfAny(new[] { '-', '+', ' ' });
            if (corte >= 0) t = t.Substring(0, corte);
            var partes = t.Split('.');
            if (partes.Length == 0 || partes.Length > 4) return null;
            var r = new int[partes.Length];
            for (int i = 0; i < partes.Length; i++)
                if (!int.TryParse(partes[i], NumberStyles.None, CultureInfo.InvariantCulture, out r[i])) return null;
            return r;
        }

        // Compara componente a componente (numerico: 0.9.10 > 0.9.9); faltando conta como 0
        public static int Comparar(int[] a, int[] b)
        {
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        // Uma linha so, cortada numa palavra com "…" se passar de max caracteres
        public static string Resumir(string texto, int max)
        {
            var t = string.Join(" ", texto.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            if (t.Length <= max) return t;
            var corte = t.LastIndexOf(' ', max - 1);
            if (corte < max / 2) corte = max - 1;
            return t.Substring(0, corte).TrimEnd(' ', ',', ';', '.', ':', '-') + "…";
        }

        private static bool UrlValida(string s) =>
            Uri.TryCreate(s.Trim(), UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http");

        private static async Task<string?> Baixar(string url)
        {
            if (url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return await Task.Run(() => System.IO.File.ReadAllText(new Uri(url).LocalPath)).ConfigureAwait(false);
#if NETFRAMEWORK
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            return await Task.Run(() =>
            {
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Timeout = TimeoutMs;
                req.ReadWriteTimeout = TimeoutMs;
                req.UserAgent = "ElifootLauncher";
                using var resp = (System.Net.HttpWebResponse)req.GetResponse();
                using var leitor = new System.IO.StreamReader(resp.GetResponseStream(), Encoding.UTF8);
                return leitor.ReadToEnd();
            }).ConfigureAwait(false);
#else
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMilliseconds(TimeoutMs) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("ElifootLauncher");
            return await http.GetStringAsync(url).ConfigureAwait(false);
#endif
        }
    }

    // Parser JSON minimo: objeto -> Dictionary<string, object?>, array -> List<object?>,
    // string, double, bool, null. Lanca FormatException se o texto for invalido.
    internal sealed class JsonMinimo
    {
        private readonly string _s;
        private int _i;
        private JsonMinimo(string s) { _s = s; }

        public static object? Ler(string json)
        {
            var p = new JsonMinimo(json);
            var v = p.Valor();
            p.Espacos();
            if (p._i != p._s.Length) throw new FormatException("lixo depois do JSON");
            return v;
        }

        private void Espacos() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

        private char Proximo()
        {
            Espacos();
            if (_i >= _s.Length) throw new FormatException("fim inesperado");
            return _s[_i];
        }

        private void Esperar(char c)
        {
            if (Proximo() != c) throw new FormatException($"esperava '{c}'");
            _i++;
        }

        private object? Valor()
        {
            switch (Proximo())
            {
                case '{':
                {
                    _i++;
                    var d = new Dictionary<string, object?>();
                    if (Proximo() == '}') { _i++; return d; }
                    while (true)
                    {
                        if (Proximo() != '"') throw new FormatException("chave");
                        var k = Texto();
                        Esperar(':');
                        d[k] = Valor();
                        if (Proximo() == ',') { _i++; continue; }
                        Esperar('}');
                        return d;
                    }
                }
                case '[':
                {
                    _i++;
                    var l = new List<object?>();
                    if (Proximo() == ']') { _i++; return l; }
                    while (true)
                    {
                        l.Add(Valor());
                        if (Proximo() == ',') { _i++; continue; }
                        Esperar(']');
                        return l;
                    }
                }
                case '"': return Texto();
                default:
                    if (Palavra("true")) return true;
                    if (Palavra("false")) return false;
                    if (Palavra("null")) return null;
                    return Numero();
            }
        }

        private bool Palavra(string w)
        {
            if (string.CompareOrdinal(_s, _i, w, 0, w.Length) != 0) return false;
            _i += w.Length;
            return true;
        }

        private double Numero()
        {
            int ini = _i;
            while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
            return double.Parse(_s.Substring(ini, _i - ini), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private string Texto()
        {
            _i++; // aspas
            var sb = new StringBuilder();
            while (true)
            {
                if (_i >= _s.Length) throw new FormatException("string sem fim");
                char c = _s[_i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (_i >= _s.Length) throw new FormatException("escape sem fim");
                char e = _s[_i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_i + 4 > _s.Length) throw new FormatException("\\u curto");
                        sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _i += 4;
                        break;
                    default: throw new FormatException("escape invalido");
                }
            }
        }
    }
}
