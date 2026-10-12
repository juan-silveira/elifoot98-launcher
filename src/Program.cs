using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ElifootLauncher
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Nada de erro sem mensagem: sem esses tratadores, uma excecao antes da janela
            // abrir (ou numa thread de fundo) derruba o processo em silencio (so fica um
            // registro no Visualizador de Eventos do Windows).
            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => RelatarErro(e.Exception, fatal: false);
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    RelatarErro(e.ExceptionObject as Exception, fatal: true);
            }
            catch (Exception ex) { GravarLog(ex, "registrar tratadores"); }

            try
            {
                Iniciar();
            }
            catch (Exception ex)
            {
                RelatarErro(ex, fatal: true);
            }
        }

        // Separado do Main (e sem inline) pra que uma falha ao carregar MainForm ou
        // outra classe aconteca dentro do try acima, e nao ao compilar o Main
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Iniciar()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        // Evita uma cascata de caixas de mensagem se o mesmo erro se repetir (ex.: no Paint)
        private static int _mostrandoErro;

        // Grava o erro no log e avisa o usuario em portugues. fatal = o launcher vai fechar.
        private static void RelatarErro(Exception? ex, bool fatal)
        {
            var log = GravarLog(ex, fatal ? "erro fatal" : "erro");
            if (Interlocked.Exchange(ref _mostrandoErro, 1) == 1) return;
            try
            {
                var texto = new StringBuilder();
                texto.AppendLine(fatal
                    ? "O Elifoot 98 Launcher encontrou um erro e não pôde continuar."
                    : "O Elifoot 98 Launcher encontrou um erro inesperado.");
                texto.AppendLine();
                if (ex != null)
                {
                    var raiz = ex is TypeInitializationException && ex.InnerException != null ? ex.InnerException : ex;
                    texto.AppendLine($"{raiz.GetType().Name}: {raiz.Message}");
                    var dica = Dica(raiz);
                    if (dica != null) { texto.AppendLine(); texto.AppendLine(dica); }
                    texto.AppendLine();
                }
                texto.AppendLine(log != null
                    ? $"Os detalhes foram gravados em:\n{log}"
                    : "Não consegui gravar o arquivo de log com os detalhes.");
                texto.AppendLine();
                texto.Append("Se o problema continuar, abra um chamado em github.com/juan-silveira/elifoot98-launcher/issues " +
                             "e anexe esse arquivo.");
                MessageBox.Show(texto.ToString(), "Elifoot 98 Launcher - Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
            finally { Interlocked.Exchange(ref _mostrandoErro, 0); }
        }

        // Causas comuns na instalacao do usuario, com o que fazer
        private static string? Dica(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
                return "Sem permissão para acessar um arquivo. Verifique se o antivírus não bloqueou a pasta " +
                       "do launcher, ou reinstale o Elifoot 98 Launcher.";
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException || ex is FileLoadException)
                return "Falta um arquivo do launcher. O antivírus pode ter colocado algum arquivo em quarentena; " +
                       "restaure-o ou reinstale o Elifoot 98 Launcher.";
            if (ex is BadImageFormatException)
                return "Um arquivo do launcher está corrompido. Reinstale o Elifoot 98 Launcher.";
            if (ex is MissingMethodException || ex is TypeLoadException)
                return "O .NET Framework instalado é antigo ou está danificado. Instale o .NET Framework 4.8 " +
                       "(https://dotnet.microsoft.com/download/dotnet-framework/net48).";
            return null;
        }

        // %LOCALAPPDATA%\Elifoot98Launcher\erro.log (ou %TEMP%, se nao der). Devolve o caminho
        // gravado ou null.
        private static string? GravarLog(Exception? ex, string contexto)
        {
            string texto;
            try { texto = MontarRelatorio(ex, contexto); }
            catch { texto = $"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {contexto} ===\r\n{ex}\r\n"; }

            foreach (var dir in PastasDeLog())
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var caminho = Path.Combine(dir, "erro.log");
                    // Guarda o historico, mas sem crescer sem limite
                    var fi = new FileInfo(caminho);
                    if (fi.Exists && fi.Length > 512 * 1024) fi.Delete();
                    File.AppendAllText(caminho, texto, new UTF8Encoding(false));
                    return caminho;
                }
                catch { }
            }
            return null;
        }

        private static string[] PastasDeLog()
        {
            var r = new System.Collections.Generic.List<string>();
            try
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(local)) r.Add(Path.Combine(local, "Elifoot98Launcher"));
            }
            catch { }
            try { r.Add(Path.Combine(Path.GetTempPath(), "Elifoot98Launcher")); } catch { }
            return r.ToArray();
        }

        private static string MontarRelatorio(Exception? ex, string contexto)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {contexto} ===");
            sb.AppendLine($"Launcher: {Tentar(VersaoLauncher)}");
            sb.AppendLine($"Windows: {Tentar(VersaoWindows)}");
            sb.AppendLine($".NET: CLR {Environment.Version}, Framework {Tentar(VersaoNetFramework)}");
            sb.AppendLine($"Processo {(Environment.Is64BitProcess ? "64" : "32")}-bit, sistema {(Environment.Is64BitOperatingSystem ? "64" : "32")}-bit");
            sb.AppendLine($"Pasta do launcher: {Tentar(() => AppDomain.CurrentDomain.BaseDirectory)}");
            sb.AppendLine($"Pasta atual: {Tentar(() => Environment.CurrentDirectory)}");
            sb.AppendLine($"Idioma: {Tentar(() => System.Globalization.CultureInfo.CurrentCulture.Name)} / " +
                          $"{Tentar(() => System.Globalization.CultureInfo.CurrentUICulture.Name)}");
            sb.AppendLine(ex != null ? ex.ToString() : "(excecao desconhecida)");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string Tentar(Func<string> f)
        {
            try { return f() ?? "?"; }
            catch (Exception ex) { return $"? ({ex.GetType().Name})"; }
        }

        private static string VersaoLauncher()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var info = (System.Reflection.AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(
                asm, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
            return $"{asm.GetName().Version}" + (info != null ? $" ({info.InformationalVersion})" : "");
        }

        // Environment.OSVersion mente sem supportedOS no manifest (diz 6.2): le do registro
        private static string VersaoWindows()
        {
            using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                if (k == null) return Environment.OSVersion.ToString();
                return $"{k.GetValue("ProductName")} {k.GetValue("DisplayVersion") ?? k.GetValue("ReleaseId")} " +
                       $"(build {k.GetValue("CurrentBuild")}.{k.GetValue("UBR")}) [{Environment.OSVersion}]";
            }
        }

        // Release >= 528040 = 4.8; >= 533320 = 4.8.1
        private static string VersaoNetFramework()
        {
            using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
            {
                var release = k?.GetValue("Release");
                var versao = k?.GetValue("Version");
                return $"{versao ?? "?"} (Release {release ?? "?"})";
            }
        }
    }
}
