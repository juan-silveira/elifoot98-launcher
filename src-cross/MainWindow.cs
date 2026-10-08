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
            Title = "Elifoot 98 Launcher";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Icon = Dialogos.Icone();

            var btnJogo = Botao("Jogar Elifoot 98");
            var btnEditor = Botao("Editor de Equipes");
            var btnRefEditor = Botao("Editor de Árbitros");
            var btnSaveEditor = Botao("Editor de Save");
            var btnPatch = Botao("Aplicar Patch");
            var btnConfig = Botao("Configurações", secundario: true);

            btnJogo.Click += async (_, _) => await SafeRun(() => _game.LaunchElifoot(_config));
            btnEditor.Click += async (_, _) => await SafeRun(() => _game.LaunchEditor(_config));
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
            btnPatch.Click += async (_, _) => await AplicarPatch();
            btnConfig.Click += async (_, _) =>
            {
                if (await new SettingsWindow(_config, _game).ShowDialog<bool>(this))
                    _config = LauncherConfig.Load();
            };

            Content = new StackPanel
            {
                Margin = new Thickness(60, 12, 60, 24),
                Spacing = 7,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Elifoot 98",
                        FontSize = 24,
                        FontWeight = FontWeight.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8),
                    },
                    btnJogo, btnEditor, btnRefEditor, btnSaveEditor, btnPatch,
                    new Border { Height = 18 },
                    btnConfig,
                },
            };
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

        private static Button Botao(string texto, bool secundario = false) => new Button
        {
            Content = texto,
            Height = secundario ? 32 : 38,
            FontSize = secundario ? 13 : 14,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
    }
}
