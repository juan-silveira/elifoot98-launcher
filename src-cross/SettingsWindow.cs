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
        public SettingsWindow(LauncherConfig cfg, LinuxGame game)
        {
            Title = "Configurações";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();

            var fullscreen = new CheckBox { Content = "Abrir em tela cheia", IsChecked = cfg.Fullscreen };
            // Tamanho do jogo: o jogo desenha as telas na escala (DPI alta no Wine), com o
            // texto nitido; a janela do jogo nao pode ser redimensionada depois de aberta
            var escala = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var e in LauncherConfig.OpcoesEscala)
                escala.Items.Add(e == 0 ? "Automático — o maior que cabe no monitor"
                    : (e == 100 ? "Normal" : $"{e / 100.0:0.##}×".Replace('.', ',')) + $" — {800 * e / 100} × {600 * e / 100}");
            escala.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesEscala, cfg.Escala));
            var janelaLivre = new CheckBox { Content = "Janela redimensionável", IsChecked = cfg.JanelaLivre };
            ToolTip.SetTip(janelaLivre, "Puxe a borda ou maximize: a imagem do jogo acompanha a janela. Usa o gamescope (~41 MB, baixado na primeira vez) e precisa de placa de vídeo com Vulkan.");
            // Em tela cheia nao ha janela pra redimensionar
            janelaLivre.IsEnabled = fullscreen.IsChecked != true;
            fullscreen.IsCheckedChanged += (_, _) => janelaLivre.IsEnabled = fullscreen.IsChecked != true;
            var nota = "Maior que o normal, o jogo desenha as telas maiores, com o texto nítido. Com a janela redimensionável, a imagem acompanha a borda da janela.";
            // No Windows (versao portatil) o Tamanho do jogo e o mesmo; a janela redimensionavel
            // (gamescope) e so do Linux
            if (OperatingSystem.IsWindows())
            {
                janelaLivre.IsVisible = false;
                nota = "Maior que o normal, o jogo desenha as telas maiores, com o texto nítido. A janela não muda de tamanho depois de aberta: troque aqui e abra o jogo de novo.";
            }
            if (OperatingSystem.IsMacOS())
            {
                // O Wine do macOS nao tem desktop virtual: o jogo sempre abre maximizado
                fullscreen.IsEnabled = false;
                janelaLivre.IsVisible = false;   // o gamescope e so do Linux
                nota = "No macOS o jogo sempre abre maximizado; o tamanho escolhido é o das telas.";
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

            // Estrangeiros: Original (BOSMAN.TXE que vem com o launcher: a UE atual e o
            // Reino Unido) ou Liberado (todos os paises do COUNTRY.TXE no BOSMAN.TXE,
            // como fez o Turbo Score)
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

            // Equipes por divisao: cada opcao abre o seu EXE (ELIF08.EXE a ELIF20.EXE)
            var equipes = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var n in LauncherConfig.OpcoesEquipes)
                equipes.Items.Add(n == 8 ? "8 (original)" : $"{n} — {2 * (n - 1)} jornadas, mínimo de {LauncherConfig.MinimoDeEquipes(n)} equipes");
            equipes.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesEquipes, cfg.EquipesPorDivisao));

            // Idioma do jogo: o original (Portugal) ou o portugues do Brasil (ELIFnnBR.EXE)
            var idioma = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var nome in LauncherConfig.NomesIdioma)
                idioma.Items.Add(nome);
            idioma.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesIdioma, cfg.Idioma));

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
                cfg.ResolutionWidth = 800;   // a janela do jogo fica no tamanho das telas
                cfg.ResolutionHeight = 600;
                cfg.Fullscreen = fullscreen.IsChecked == true;
                cfg.Escala = LauncherConfig.OpcoesEscala[escala.SelectedIndex];
                cfg.JanelaLivre = janelaLivre.IsChecked == true;
                cfg.EquipesPorDivisao = LauncherConfig.OpcoesEquipes[equipes.SelectedIndex];
                cfg.Idioma = LauncherConfig.OpcoesIdioma[idioma.SelectedIndex];
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
                    new TextBlock { Text = "Tamanho do jogo:" },
                    escala,
                    fullscreen,
                    janelaLivre,
                    new TextBlock
                    {
                        Text = nota,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    },
                    new TextBlock { Text = "Equipes por divisão:", Margin = new Thickness(0, 6, 0, 0) },
                    equipes,
                    new TextBlock
                    {
                        Text = "Um jogo gravado só continua com o mesmo número de equipes por divisão em que foi começado.",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    },
                    new TextBlock { Text = "Idioma do jogo:", Margin = new Thickness(0, 6, 0, 0) },
                    idioma,
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
