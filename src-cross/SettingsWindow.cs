using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ElifootLauncher
{
    public class SettingsWindow : Window
    {
        // No Linux o jogo roda num desktop virtual do Wine desse tamanho.
        // Abaixo de 800x600 as telas do Elifoot nao cabem.
        private static readonly (int W, int H)[] Resolutions =
        {
            (800, 600),
            (1024, 768),
            (1280, 960),
            (1600, 1200),
        };

        public SettingsWindow(LauncherConfig cfg, LinuxGame game)
        {
            Title = "Configurações";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();

            var resolution = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var (w, h) in Resolutions)
                resolution.Items.Add($"{w} × {h}");
            resolution.SelectedIndex = Math.Max(0, Array.IndexOf(Resolutions, (cfg.ResolutionWidth, cfg.ResolutionHeight)));

            var fullscreen = new CheckBox { Content = "Abrir em tela cheia", IsChecked = cfg.Fullscreen };
            var nota = "O jogo desenha suas telas em 640×480; janelas maiores dão mais espaço em volta, sem cortar nada.";
            if (OperatingSystem.IsMacOS())
            {
                // O Wine do macOS nao tem desktop virtual: o jogo sempre abre maximizado
                resolution.IsEnabled = false;
                fullscreen.IsEnabled = false;
                nota = "No macOS o jogo sempre abre maximizado.";
            }

            var btnAtivar = new Button
            {
                Content = "Ativar todos os recursos",
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            btnAtivar.Click += async (_, _) =>
            {
                btnAtivar.IsEnabled = false;
                Cursor = new Cursor(StandardCursorType.Wait);
                try
                {
                    await Task.Run(game.Ativar);
                    await Dialogos.Mensagem(this,
                        "Recursos ativados! Abra o jogo para usar.\n\nSe o jogo estiver aberto, feche e abra de novo.",
                        "Pronto");
                }
                catch (Exception ex)
                {
                    await Dialogos.Mensagem(this, ex.Message, "Não foi possível ativar");
                }
                finally
                {
                    Cursor = Cursor.Default;
                    btnAtivar.IsEnabled = true;
                }
            };

            // Estrangeiros: Original (BOSMAN.TXE do jogo) ou Liberado (os 217 paises
            // do COUNTRY.TXE no BOSMAN.TXE, como fez o Turbo Score)
            var estrangeiros = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemsSource = new[]
                {
                    "Original — até 5 por equipe (Lei Bosman e língua portuguesa não contam)",
                    "Liberado — sem limite de estrangeiros",
                },
            };
            bool liberadoAntes = false;
            try { liberadoAntes = TeamCodec.BosmanLiberado(game.GameDir); } catch { estrangeiros.IsEnabled = false; }
            estrangeiros.SelectedIndex = liberadoAntes ? 1 : 0;

            var btnOk = new Button { Content = "Salvar", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
            var btnCancel = new Button { Content = "Cancelar", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
            btnOk.Click += async (_, _) =>
            {
                bool liberado = estrangeiros.SelectedIndex == 1;
                if (estrangeiros.IsEnabled && liberado != liberadoAntes)
                {
                    try { TeamCodec.DefinirBosman(game.GameDir, liberado); }
                    catch (Exception ex)
                    {
                        await Dialogos.Mensagem(this, $"Não consegui gravar o BOSMAN.TXE:\n{ex.Message}", "Erro");
                        return;
                    }
                }
                var (w, h) = Resolutions[resolution.SelectedIndex];
                cfg.ResolutionWidth = w;
                cfg.ResolutionHeight = h;
                cfg.Fullscreen = fullscreen.IsChecked == true;
                cfg.Save();
                Close(true);
            };
            btnCancel.Click += (_, _) => Close(false);

            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = "Tamanho da janela do jogo:" },
                    resolution,
                    fullscreen,
                    new TextBlock
                    {
                        Text = nota,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    },
                    new TextBlock { Text = "Jogadores estrangeiros (vale no jogo e no Editor de Equipes):", Margin = new Thickness(0, 6, 0, 0) },
                    estrangeiros,
                    btnAtivar,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { btnOk, btnCancel },
                    },
                },
            };
        }
    }
}
