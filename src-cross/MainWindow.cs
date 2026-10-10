using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace ElifootLauncher
{
    public class MainWindow : Window
    {
        private readonly LinuxGame _game = new LinuxGame();
        private LauncherConfig _config = LauncherConfig.Load();

        public MainWindow()
        {
            // Versao do release (scripts/build-linux.sh e build-mac.sh passam -p:Version)
            var ver = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            Title = ver != null ? $"Elifoot 98 Launcher v{ver.Major}.{ver.Minor}.{ver.Build}" : "Elifoot 98 Launcher";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = Dialogos.Icone();
            // Verde e amarelo do jogo, como o launcher do Android
            Background = Visual.Cor(0x007200);
            Visual.AplicarEstilos(this);

            var btnJogo = Botao("Jogar Elifoot 98");
            var btnEditor = Botao("Editor de Equipes");
            var btnRefEditor = Botao("Editor de Árbitros");
            var btnSaveEditor = Botao("Editor de Save");
            var btnScout = Botao("Scout");
            var btnPatch = Botao("Aplicar Patch");
            var btnConfig = Botao("Configurações", secundario: true);

            btnJogo.Click += async (_, _) => await SafeRun(() => _game.LaunchElifoot(_config));
            btnEditor.Click += async (_, _) =>
            {
                if (!await SafeRun(_game.Preparar)) return;
                TeamEditorWindow janela;
                try { janela = new TeamEditorWindow(_game.GameDir, () => _ = SafeRun(() => _game.LaunchEditor(_config))); }
                catch (Exception ex)
                {
                    await Dialogos.Mensagem(this, $"Não consegui abrir o Editor de Equipes:\n{ex.Message}", "Erro");
                    return;
                }
                await janela.ShowDialog(this);
            };
            btnRefEditor.Click += async (_, _) =>
            {
                if (await SafeRun(_game.Preparar))
                    await new RefereeEditorWindow(_game.RefereeTxePath).ShowDialog(this);
            };
            btnSaveEditor.Click += async (_, _) =>
            {
                if (await SafeRun(_game.Preparar))
                    await new SaveEditorWindow(_game.JogosDir).ShowDialog(this);
            };
            btnScout.Click += async (_, _) =>
            {
                if (await SafeRun(_game.Preparar))
                    await new ScoutWindow(_game.JogosDir, _game.GameDir).ShowDialog(this);
            };
            btnPatch.Click += async (_, _) => await AplicarPatch();
            btnConfig.Click += async (_, _) =>
            {
                if (await new SettingsWindow(_config, _game).ShowDialog<bool>(this))
                    _config = LauncherConfig.Load();
            };

            var painel = new StackPanel
            {
                Margin = new Thickness(50, 20, 50, 16),
                Spacing = 8,
            };
            // Logo "Elifoot 98" (o mesmo do app Android, recortado da Acerca)
            var logo = typeof(MainWindow).Assembly.GetManifestResourceStream("logo.png");
            if (logo != null)
                painel.Children.Add(new Image
                {
                    Source = new Avalonia.Media.Imaging.Bitmap(logo),
                    Width = 260,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 12),
                });
            else
                painel.Children.Add(Visual.Texto("Elifoot 98", 26, Visual.Amarelo, true));
            foreach (var b in new[] { btnJogo, btnEditor, btnRefEditor, btnSaveEditor, btnScout, btnPatch })
                painel.Children.Add(b);
            painel.Children.Add(new Border { Height = 14 });
            painel.Children.Add(btnConfig);
            if (ver != null)
                painel.Children.Add(new TextBlock
                {
                    Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}",
                    FontSize = 12,
                    Foreground = Visual.Cor(0xC8E6C8),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 0),
                });
            Content = painel;
        }

        private async Task AplicarPatch()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Selecione o patch (.zip) para aplicar",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Patches Elifoot 98 (*.zip)") { Patterns = new[] { "*.zip", "*.ZIP" } },
                    new FilePickerFileType("Todos os arquivos") { Patterns = new[] { "*" } },
                },
            });
            var zip = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (zip == null) return;

            if (!await Dialogos.Confirmar(this,
                    $"Aplicar patch:\n\n{zip}\n\n" +
                    "Todos os arquivos serão substituídos EXCETO a pasta EQUIPAS " +
                    "(será renomeada pra EQUIPAS_OLD antes) e JOGOS (nunca tocada).\n\nContinuar?",
                    "Confirmar patch"))
                return;
            if (!await SafeRun(_game.Preparar)) return;

            Cursor = new Cursor(StandardCursorType.Wait);
            PatchResult res;
            try { res = await Task.Run(() => PatchApplier.Apply(zip, _game.GameDir)); }
            finally { Cursor = Cursor.Default; }

            if (!res.Ok)
            {
                await Dialogos.Mensagem(this, $"Erro ao aplicar patch:\n{res.Error}", "Erro");
                return;
            }
            var msg = $"Patch aplicado com sucesso!\n\n{res.FilesReplaced} arquivos substituídos.";
            if (res.EquipasBackupName != null)
                msg += $"\nEQUIPAS antiga preservada em: {res.EquipasBackupName}";
            if (res.IgnoredEntries.Count > 0)
                msg += $"\n\n{res.IgnoredEntries.Count} entradas em JOGOS/ foram ignoradas (saves preservados).";
            await Dialogos.Mensagem(this, msg, "Patch aplicado");
        }

        private async Task<bool> SafeRun(Action a)
        {
            try
            {
                a();
                return true;
            }
            catch (System.IO.FileNotFoundException ex)
            {
                await Dialogos.Mensagem(this, ex.Message, "Arquivo faltando");
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, ex.Message, "Erro ao iniciar");
            }
            return false;
        }

        // Botao amarelo do launcher (classe "amarelo": continua amarelo com o mouse em cima)
        private static Button Botao(string texto, bool secundario = false) => new Button
        {
            Classes = { "amarelo" },
            Content = texto,
            Height = 42,
            FontSize = 15,
            Foreground = Brushes.Black,
            Background = Visual.Amarelo,
            CornerRadius = new CornerRadius(6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
    }
}
