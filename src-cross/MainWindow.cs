using System;
using System.IO;
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
            var btnBaixar = Visual.BotaoAmarelo(info.Arquivo.Length > 0 ? "Atualizar" : "Baixar", 100);
            var btnAgoraNao = Visual.BotaoClaro("Agora não");
            btnBaixar.Click += async (_, _) =>
            {
                if (info.Arquivo.Length == 0)
                {
                    if (!AbrirNoNavegador(info.Link))
                        await Dialogos.Mensagem(this, $"Não consegui abrir o navegador:\n\n{info.Link}", "Erro");
                    return;
                }
                if (Atualizacao.JogoAberto())
                {
                    await Dialogos.Mensagem(this, "Feche o jogo antes de atualizar.", "Atualizar");
                    return;
                }
                btnBaixar.IsEnabled = btnAgoraNao.IsEnabled = false;
                try
                {
                    var progresso = new Progress<int>(p => btnBaixar.Content = p >= 0 ? $"{p}%" : "Baixando…");
                    var erro = await Instalar(info, progresso, t => btnBaixar.Content = t);
                    if (erro == null) return;   // o launcher novo ja abriu e este fechou
                    await Dialogos.Mensagem(this, erro, "Atualizar");
                }
                catch (Exception ex)
                {
                    await Dialogos.Mensagem(this, $"Não consegui atualizar:\n{ex.Message}\n\nO download também está em:\n{info.Link}", "Atualizar");
                }
                btnBaixar.Content = "Atualizar";
                btnBaixar.IsEnabled = btnAgoraNao.IsEnabled = true;
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

        // Baixa o pacote da plataforma e instala. null = instalou e o launcher novo abriu (este
        // fecha); senao, a mensagem para o usuario.
        //   - Windows portatil e AppImage: troca o proprio arquivo e abre o novo;
        //   - .deb: pkexec apt-get install (a janela de senha do sistema) e abre de novo;
        //   - macOS: descompacta em Downloads e mostra o app novo no Finder.
        private async Task<string?> Instalar(InfoAtualizacao info, IProgress<int> progresso, Action<string> estado)
        {
            var plataforma = Plataforma();
            var nome = Atualizacao.NomeDoArquivo(info.Arquivo);
            if (plataforma == "windows-portatil" || plataforma == "linux-appimage")
            {
                var atual = plataforma == "linux-appimage" ? Environment.GetEnvironmentVariable("APPIMAGE")! : Environment.ProcessPath!;
                var novo = atual + ".novo";
                await Atualizacao.BaixarArquivoAsync(info.Arquivo, novo, progresso, info.Sha256);
                if (OperatingSystem.IsWindows())
                {
                    // o .exe em uso pode ser renomeado, nao apagado: o .velho sai na proxima abertura
                    var velho = atual + ".velho";
                    if (File.Exists(velho)) File.Delete(velho);
                    File.Move(atual, velho);
                    File.Move(novo, atual);
                }
                else
                {
                    File.SetUnixFileMode(novo, File.GetUnixFileMode(atual) | UnixFileMode.UserExecute);
                    File.Move(novo, atual, true);
                }
                Reabrir(atual);
                return null;
            }
            var pasta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(pasta);
            var arquivo = Path.Combine(pasta, nome);
            await Atualizacao.BaixarArquivoAsync(info.Arquivo, arquivo, progresso, info.Sha256);
            if (plataforma == "linux-deb")
            {
                estado("Instalando…");
                var psi = new System.Diagnostics.ProcessStartInfo("pkexec") { UseShellExecute = false };
                foreach (var a in new[] { "apt-get", "install", "-y", "--allow-downgrades", arquivo }) psi.ArgumentList.Add(a);
                int codigo;
                try
                {
                    using var p = System.Diagnostics.Process.Start(psi)!;
                    await p.WaitForExitAsync();
                    codigo = p.ExitCode;
                }
                catch { codigo = -1; }
                if (codigo == 0)
                {
                    Reabrir(Environment.ProcessPath!);
                    return null;
                }
                if (codigo == 126) return "A instalação foi cancelada.";
                return $"Não consegui instalar o pacote. Ele foi baixado em:\n{arquivo}\n\nPara instalar pelo terminal:\nsudo apt install {arquivo}";
            }
            // macOS: o zip traz o .app; descompactado ao lado, o Finder mostra para arrastar para Aplicativos
            estado("Abrindo…");
            var dest = Path.Combine(pasta, Path.GetFileNameWithoutExtension(nome));
            var ditto = new System.Diagnostics.ProcessStartInfo("ditto") { UseShellExecute = false };
            foreach (var a in new[] { "-x", "-k", arquivo, dest }) ditto.ArgumentList.Add(a);
            using (var p = System.Diagnostics.Process.Start(ditto)!) await p.WaitForExitAsync();
            var abrir = new System.Diagnostics.ProcessStartInfo("open") { UseShellExecute = false };
            abrir.ArgumentList.Add(dest);
            System.Diagnostics.Process.Start(abrir)?.Dispose();
            return $"A versão {info.Versao} foi baixada em:\n{dest}\n\nFeche o launcher e arraste o Elifoot 98 novo para a pasta Aplicativos, substituindo o antigo.";
        }

        // Abre o launcher novo e fecha este
        private void Reabrir(string caminho)
        {
            var psi = new System.Diagnostics.ProcessStartInfo(caminho) { UseShellExecute = false };
            if (!OperatingSystem.IsWindows()) psi.WorkingDirectory = Path.GetDirectoryName(caminho)!;
            System.Diagnostics.Process.Start(psi)?.Dispose();
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime vida)
                vida.Shutdown();
            else
                Environment.Exit(0);
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
