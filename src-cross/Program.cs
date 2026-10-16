using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace ElifootLauncher
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            // Nenhum erro fecha o launcher em silencio: vai para o erro.log e para uma
            // mensagem (no Windows pela caixa do sistema, que funciona mesmo se a janela
            // do Avalonia nao abrir)
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Relatar(e.ExceptionObject as Exception, "erro não tratado");
            // Sobra da atualizacao do Windows portatil (o .exe antigo, renomeado)
            try
            {
                var velho = Environment.ProcessPath + ".velho";
                if (OperatingSystem.IsWindows() && File.Exists(velho)) File.Delete(velho);
            }
            catch { }
            try
            {
                AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Relatar(ex, "ao iniciar");
            }
        }

        private static void Relatar(Exception? ex, string contexto)
        {
            var log = GravarLog(ex, contexto);
            var texto = "O Elifoot 98 Launcher encontrou um erro e não pôde continuar.\n\n"
                + $"{ex?.GetType().Name}: {ex?.Message}\n\n"
                + (log != null ? $"Detalhes em:\n{log}\n\n" : "")
                + "Se o problema continuar, abra um chamado em\nhttps://github.com/juan-silveira/elifoot98-launcher/issues";
            if (OperatingSystem.IsWindows())
                MessageBoxW(IntPtr.Zero, texto, "Elifoot 98 Launcher", 0x10);
            else
                Console.Error.WriteLine(texto);
        }

        private static string? GravarLog(Exception? ex, string contexto)
        {
            foreach (var pasta in new[] { LinuxGame.PastaDeDadosPublica(), Path.GetTempPath() })
            {
                try
                {
                    Directory.CreateDirectory(pasta);
                    var arq = Path.Combine(pasta, "erro.log");
                    var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
                    File.AppendAllText(arq,
                        $"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({contexto}) ====\n"
                        + $"Launcher {ver}; {RuntimeInformation.OSDescription}; {RuntimeInformation.ProcessArchitecture}; .NET {Environment.Version}\n"
                        + $"Pasta do launcher: {AppContext.BaseDirectory}\n"
                        + $"{ex}\n\n");
                    return arq;
                }
                catch { }
            }
            return null;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr janela, string texto, string titulo, uint tipo);
    }

    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = ThemeVariant.Light;
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }
}
