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
            // Aviso de versao nova fica acima do painel (ver Atualizacao.cs; ELIFOOT_VERSAO_URL
            // troca o endereco do versao.json pra testar). SizeToContent cresce a janela.
            var raiz = new DockPanel();
            var aviso = new ContentControl();
            DockPanel.SetDock(aviso, Dock.Top);
            raiz.Children.Add(aviso);
            raiz.Children.Add(painel);
            Content = raiz;

            Opened += (_, _) => _ = Task.Run(async () =>
            {
                var info = await Atualizacao.VerificarAsync(ver, Plataforma()).ConfigureAwait(false);
                if (info != null)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => aviso.Content = FaixaAviso(info, () => aviso.Content = null));
            });
        }

        // Chave em "links" do versao.json pra este sistema
        private static string Plataforma()
        {
            if (OperatingSystem.IsWindows()) return "windows-portatil";
            if (OperatingSystem.IsMacOS()) return "macos";
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE")) ? "linux-deb" : "linux-appimage";
        }

        // Faixa verde-escura com borda amarela: "Nova versao X disponivel", novidades, Baixar / Agora nao
        private Control FaixaAviso(InfoAtualizacao info, Action fechar)
        {
            var conteudo = new StackPanel { Spacing = 4 };
            conteudo.Children.Add(Visual.Texto($"Nova versão {info.Versao} disponível", 15, Visual.Amarelo, true));
            if (info.Novidades.Length > 0)
            {
                var nov = Visual.Texto(info.Novidades, 12, Visual.Cor(0xE6F2E6));
                nov.TextWrapping = TextWrapping.Wrap;
                nov.MaxLines = 2;
                nov.TextTrimming = TextTrimming.WordEllipsis;
                conteudo.Children.Add(nov);
            }
            var btnBaixar = Visual.BotaoAmarelo("Baixar", 100);
            var btnAgoraNao = Visual.BotaoClaro("Agora não");
            btnBaixar.Click += async (_, _) =>
            {
                if (!AbrirNoNavegador(info.Link))
                    await Dialogos.Mensagem(this, $"Não consegui abrir o navegador:\n\n{info.Link}", "Erro");
            };
            btnAgoraNao.Click += (_, _) => fechar();
            var botoes = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 0, 0),
                Children = { btnBaixar, btnAgoraNao },
            };
            conteudo.Children.Add(botoes);
            return new Border
            {
                Background = Visual.VerdeTopo,
                BorderBrush = Visual.Amarelo,
                BorderThickness = new Thickness(0, 0, 0, 2),
                Padding = new Thickness(16, 10, 16, 10),
                Child = conteudo,
            };
        }

        // xdg-open no Linux, open no Mac, o navegador padrao no Windows
        private static bool AbrirNoNavegador(string url)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    using var w = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                    return true;
                }
                var psi = new System.Diagnostics.ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open")
                {
                    UseShellExecute = false,
                };
                psi.ArgumentList.Add(url);
                using var p = System.Diagnostics.Process.Start(psi);
                return p != null;
            }
            catch { return false; }
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
                    "O patch fica guardado na lista de Times das Configurações, para voltar a ele " +
                    "(ou aos times originais) quando quiser. Equipes editadas e árbitros voltam ao " +
                    "original; os jogos salvos (JOGOS) ficam.\n\nContinuar?",
                    "Confirmar patch"))
                return;
            if (!await SafeRun(_game.Preparar)) return;

            Cursor = new Cursor(StandardCursorType.Wait);
            PatchResult res;
            try
            {
                res = await Task.Run(() => Patches.Trocar(_game.GameDir, _game.OriginaisDir, _game.BibliotecaDir,
                    Patches.Adicionar(_game.BibliotecaDir, zip)));
            }
            catch (Exception ex) { res = new PatchResult { Error = ex.Message }; }
            finally { Cursor = Cursor.Default; }

            if (!res.Ok)
            {
                await Dialogos.Mensagem(this, $"Erro ao aplicar patch:\n{res.Error}", "Erro");
                return;
            }
            var msg = $"Patch aplicado com sucesso!\n\n{res.FilesReplaced} arquivos substituídos.";
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
