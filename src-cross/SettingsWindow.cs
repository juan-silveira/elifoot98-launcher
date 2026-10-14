using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace ElifootLauncher
{
    // Configuracoes: cada opcao num seletor, e nada muda ate o Salvar. Tamanho do jogo,
    // versao do jogo, equipes por divisao, idioma, registro, estrangeiros e os times
    // (originais ou de um patch da biblioteca, Patches.cs).
    public class SettingsWindow : Window
    {
        private readonly LinuxGame _game;
        private readonly ComboBox _times = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly Button _removerPatch;
        private List<string> _patches = new List<string>();

        public SettingsWindow(LauncherConfig cfg, LinuxGame game)
        {
            _game = game;
            Title = "Configurações";
            Width = 460;
            Height = 680;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();

            var fullscreen = new CheckBox { Content = "Abrir em tela cheia", IsChecked = cfg.Fullscreen };
            // Tamanho do jogo: o jogo desenha as telas na escala (DPI alta no Wine), com o
            // texto nitido; a janela do jogo nao pode ser redimensionada depois de aberta
            var escala = Seletor(LauncherConfig.OpcoesEscala.Select(e => e == 0 ? "Automático — o maior que cabe no monitor"
                : (e == 100 ? "Normal" : $"{e / 100.0:0.##}×".Replace('.', ',')) + $" — {800 * e / 100} × {600 * e / 100}"));
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

            var versao = Seletor(new[]
            {
                "Modificada — Nota e Risco de lesão, equipes por divisão e as adições abaixo",
                "Original — o Elifoot 98 como era (98.003)",
            });
            versao.SelectedIndex = cfg.VersaoOriginal ? 1 : 0;

            // Equipes por divisao: cada opcao abre o seu EXE (ELIF08.EXE a ELIF20.EXE)
            var equipes = Seletor(LauncherConfig.OpcoesEquipes.Select(n =>
                n == 8 ? "8 (original)" : $"{n} — {2 * (n - 1)} rodadas, mínimo de {LauncherConfig.MinimoDeEquipes(n)} equipes"));
            equipes.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesEquipes, cfg.EquipesPorDivisao));
            equipes.IsEnabled = versao.SelectedIndex == 0;
            versao.SelectionChanged += (_, _) => equipes.IsEnabled = versao.SelectedIndex == 0;

            // Idioma do jogo: o original (Portugal) ou o portugues do Brasil (ELIFnnBR.EXE)
            // Adicoes que podem ser desligadas (OpcoesDoJogo acerta o EXE antes de abrir)
            var cartoes = Seletor(new[]
            {
                "Ligados — amarelos, 3 acumulados suspendem",
                "Desligados — só vermelhos, como no original",
            });
            cartoes.SelectedIndex = cfg.Cartoes ? 0 : 1;
            var patrocinios = Seletor(new[]
            {
                "Ligados — propostas e contratos de patrocínio master",
                "Desligados",
            });
            patrocinios.SelectedIndex = cfg.Patrocinios ? 0 : 1;
            // Na versao original as adicoes nao existem: aparecem desligadas e travadas, e a
            // escolha da modificada volta quando ela e selecionada de novo
            int cartoesMod = cartoes.SelectedIndex, patrociniosMod = patrocinios.SelectedIndex;
            void Adicoes()
            {
                bool modificada = versao.SelectedIndex == 0;
                if (!modificada && cartoes.IsEnabled)
                {
                    cartoesMod = cartoes.SelectedIndex;
                    patrociniosMod = patrocinios.SelectedIndex;
                }
                cartoes.IsEnabled = patrocinios.IsEnabled = modificada;
                cartoes.SelectedIndex = modificada ? cartoesMod : 1;
                patrocinios.SelectedIndex = modificada ? patrociniosMod : 1;
            }
            Adicoes();
            versao.SelectionChanged += (_, _) => Adicoes();

            var idioma = Seletor(LauncherConfig.NomesIdioma);
            idioma.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesIdioma, cfg.Idioma));

            var registro = Seletor(new[]
            {
                "Registrado — todos os recursos (Registro para autor)",
                "Não registrado — como a cópia de avaliação",
            });
            registro.SelectedIndex = cfg.Registrado ? 0 : 1;

            // Estrangeiros: Original (BOSMAN.TXE que vem com o launcher: a UE atual e o
            // Reino Unido) ou Liberado (todos os paises do COUNTRY.TXE no BOSMAN.TXE,
            // como fez o Turbo Score)
            var estrangeiros = Seletor(new[]
            {
                "Original — até 5 por equipe (Lei Bosman e língua portuguesa não contam)",
                "Liberado — sem limite de estrangeiros",
            });
            bool liberadoAntes = false;
            try { liberadoAntes = TeamCodec.BosmanLiberado(game.GameDir); } catch { estrangeiros.IsEnabled = false; }
            estrangeiros.SelectedIndex = (cfg.Estrangeiros ?? liberadoAntes) ? 1 : 0;

            var btnAdicionar = BotaoLargo("Adicionar patch…");
            _removerPatch = BotaoLargo("Remover da lista");
            btnAdicionar.Click += async (_, _) => await AdicionarPatch();
            _removerPatch.Click += async (_, _) => await RemoverPatch();
            _times.SelectionChanged += (_, _) => _removerPatch.IsEnabled = _times.SelectedIndex > 0;
            ListarPatches(Patches.EmUso(game.GameDir));

            var btnRestaurar = BotaoLargo("Restaurar os arquivos originais…");
            btnRestaurar.Click += async (_, _) =>
            {
                if (!await Dialogos.Confirmar(this,
                        "Todas as equipes voltam a ser as originais do jogo: equipes editadas ou criadas, patches aplicados, " +
                        "árbitros e a regra de estrangeiros são desfeitos.\n\nOs jogos salvos (saves) não são apagados.\n\nRestaurar?",
                        "Restaurar os arquivos originais"))
                    return;
                if (!await TrocarTimes("")) return;
                try { TeamCodec.DefinirBosman(game.GameDir, false); } catch { }
                cfg.Estrangeiros = false;
                cfg.Save();
                ListarPatches("");
                estrangeiros.SelectedIndex = 0;
                liberadoAntes = false;
                await Dialogos.Mensagem(this, "Arquivos originais restaurados.", "Pronto");
            };

            var btnOk = new Button { Content = "Salvar", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
            var btnCancel = new Button { Content = "Cancelar", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
            btnOk.Click += async (_, _) =>
            {
                var patch = _times.SelectedIndex <= 0 ? "" : _patches[_times.SelectedIndex - 1];
                if (patch != Patches.EmUso(game.GameDir))
                {
                    if (!await TrocarTimes(patch)) return;
                    liberadoAntes = TeamCodec.BosmanLiberado(game.GameDir);
                }
                bool liberado = estrangeiros.SelectedIndex == 1;
                if (estrangeiros.IsEnabled) cfg.Estrangeiros = liberado;
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
                cfg.VersaoOriginal = versao.SelectedIndex == 1;
                cfg.EquipesPorDivisao = LauncherConfig.OpcoesEquipes[equipes.SelectedIndex];
                cfg.Idioma = LauncherConfig.OpcoesIdioma[idioma.SelectedIndex];
                cfg.Registrado = registro.SelectedIndex == 0;
                cfg.Cartoes = (cartoes.IsEnabled ? cartoes.SelectedIndex : cartoesMod) == 0;
                cfg.Patrocinios = (patrocinios.IsEnabled ? patrocinios.SelectedIndex : patrociniosMod) == 0;
                cfg.Save();
                Close(true);
            };
            btnCancel.Click += (_, _) => Close(false);

            var coluna = new StackPanel
            {
                Margin = new Thickness(20, 16, 20, 16),
                Spacing = 6,
                Children =
                {
                    Rotulo("Tamanho do jogo"), escala, fullscreen, janelaLivre, Nota(nota),
                    Rotulo("Versão do jogo"), versao,
                    Nota("Na original só há 8 equipes por divisão. Um jogo gravado na modificada mostra números estranhos de lesões e cartões na original."),
                    Rotulo("Equipes por divisão"), equipes,
                    Nota("Vale para o próximo jogo novo: um jogo gravado só continua com o mesmo número de equipes em que foi começado."),
                    Rotulo("Cartões amarelos"), cartoes,
                    Rotulo("Patrocinadores"), patrocinios, Nota("Valem na versão modificada."),
                    Rotulo("Idioma do jogo"), idioma,
                    Rotulo("Registro"), registro,
                    Nota("Vale a partir da próxima vez que o jogo abrir."),
                    Rotulo("Jogadores estrangeiros"), estrangeiros, Nota("Vale no jogo e no Editor de Equipes."),
                    Rotulo("Times"), _times,
                    Nota("Trocar os times volta o jogo aos arquivos originais e aplica o patch escolhido: equipes editadas e árbitros são desfeitos. Os jogos salvos ficam."),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,8,*"),
                        Children = { btnAdicionar, _removerPatch },
                    },
                    Rotulo("Arquivos do jogo"), btnRestaurar,
                    Nota("Desfaz equipes editadas ou criadas, patches, árbitros e a regra de estrangeiros. Os jogos salvos ficam."),
                },
            };
            Grid.SetColumn(_removerPatch, 2);

            var rodape = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(20, 8, 20, 16),
                Children = { btnOk, btnCancel },
            };
            var raiz = new DockPanel();
            DockPanel.SetDock(rodape, Dock.Bottom);
            raiz.Children.Add(rodape);
            raiz.Children.Add(new ScrollViewer { Content = coluna });
            Content = raiz;
        }

        // Troca os times numa thread (copia os arquivos originais de novo)
        private async Task<bool> TrocarTimes(string patch)
        {
            Cursor = new Cursor(StandardCursorType.Wait);
            try
            {
                var r = await Task.Run(() =>
                {
                    _game.Preparar();
                    return Patches.Trocar(_game.GameDir, _game.OriginaisDir, _game.BibliotecaDir, patch);
                });
                if (!r.Ok)
                {
                    await Dialogos.Mensagem(this, "Não consegui trocar os times:\n" + r.Error, "Erro");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, "Não consegui trocar os times:\n" + ex.Message, "Erro");
                return false;
            }
            finally { Cursor = Cursor.Default; }
        }

        private async Task AdicionarPatch()
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Escolha o patch (.zip)",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Patches Elifoot 98 (*.zip)") { Patterns = new[] { "*.zip", "*.ZIP" } },
                    new FilePickerFileType("Todos os arquivos") { Patterns = new[] { "*" } },
                },
            });
            var zip = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (zip == null) return;
            try
            {
                var nome = Patches.Adicionar(_game.BibliotecaDir, zip);
                ListarPatches(nome);
                await Dialogos.Mensagem(this, $"\"{nome}\" está selecionado em Times. Clique em Salvar para aplicar.", "Patch na lista");
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, "Não consegui guardar o patch:\n" + ex.Message, "Erro");
            }
        }

        private async Task RemoverPatch()
        {
            if (_times.SelectedIndex <= 0) return;
            var nome = _patches[_times.SelectedIndex - 1];
            var emUso = nome == Patches.EmUso(_game.GameDir);
            if (!await Dialogos.Confirmar(this, $"Tirar \"{nome}\" da lista de patches?" +
                    (emUso ? "\n\nEle está em uso: os times continuam os dele até você escolher outros." : ""),
                    "Remover da lista"))
                return;
            Patches.Remover(_game.BibliotecaDir, nome);
            ListarPatches("");
        }

        private void ListarPatches(string selecionado)
        {
            _patches = Patches.Listar(_game.BibliotecaDir);
            var emUso = Patches.EmUso(_game.GameDir);
            var itens = new List<string> { "Originais do jogo" };
            itens.AddRange(_patches.Select(p => p == emUso ? p + " (em uso)" : p));
            _times.ItemsSource = itens;
            _times.SelectedIndex = Math.Max(0, _patches.IndexOf(selecionado) + 1);
            _removerPatch.IsEnabled = _times.SelectedIndex > 0;
        }

        private static ComboBox Seletor(IEnumerable<string> opcoes) =>
            new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = opcoes.ToList() };

        private static Button BotaoLargo(string texto) => new Button
        {
            Content = texto,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        private static TextBlock Rotulo(string texto) =>
            new TextBlock { Text = texto, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 10, 0, 0) };

        private static TextBlock Nota(string texto) => new TextBlock
        {
            Text = texto,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
        };
    }
}
