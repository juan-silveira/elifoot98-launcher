using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static ElifootLauncher.Visual;

namespace ElifootLauncher
{
    // Editor de Save (.e98) com o visual do jogo (verde e amarelo), igual ao do
    // Android. Esquerda: cartoes Jogo (inflacao), Treinadores (troca de equipe) e
    // Clube (dinheiro, moral, estadio, cores). Direita: tabela de jogadores;
    // duplo-clique abre a ficha (forca, salario, nota, lesao, S/L, comportamento).
    public class SaveEditorWindow : Window
    {
        private const string Colunas = "40,*,24,56,96,48,52,120,64";

        private readonly string _jogosDir;
        private readonly ComboBox _saveSel = new ComboBox { Width = 200 };
        private readonly ComboBox _teamSel = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBox _inflacao = new TextBox(), _dinheiro = new TextBox(), _moral = new TextBox();
        private readonly TextBlock _temporada = Texto("", 12, Cinza);
        private readonly TextBlock _estadio = Texto("", 15, Branco, true);
        private readonly Button _estMenos, _estMais;
        private readonly Border _previa = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 6), Margin = new Thickness(0, 8, 0, 4) };
        private readonly TextBlock _previaTxt = Texto("", 15, Branco, true);
        private readonly Border _amostraLetra = Amostra(), _amostraFundo = Amostra();
        private readonly TextBlock _hexLetra = Texto("", 12, Cinza), _hexFundo = Texto("", 12, Cinza);
        private readonly StackPanel _treinadores = new StackPanel { Spacing = 6 };
        private readonly Border _cartaoTreinadores;
        private readonly ListBox _lista;
        private readonly Button _salvar;

        private SaveFile? _current;
        private SaveTeam? _currentTeam;
        private List<SaveTeam> _teams = new List<SaveTeam>();
        private string _currentPath = "";
        private bool _trocandoTime;
        // Texto mostrado ao carregar: so aplica inflacao/moral se o usuario mudou
        // (a tela arredonda em 1 casa e regravaria um valor diferente do original)
        private string _inflacaoMostrada = "", _moralMostrado = "";

        public SaveEditorWindow(string jogosDir)
        {
            _jogosDir = jogosDir;
            Title = "Editor de Save";
            Width = 1180;
            Height = 720;
            MinWidth = 760;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();
            Background = Verde;
            AplicarEstilos(this);
            CaberNaTela(this);

            // Barra do topo
            _saveSel.SelectionChanged += async (_, _) => await LoadSelected();
            var recarregar = BotaoClaro("Recarregar");
            recarregar.Click += async (_, _) => await RefreshSaveList(preserveSelection: true);
            _salvar = BotaoAmarelo("Salvar", 110);
            _salvar.IsEnabled = false;
            _salvar.Click += async (_, _) => await SaveCurrent();
            var fechar = BotaoClaro("Fechar");
            fechar.Click += (_, _) => Close();
            var barra = new DockPanel { Background = VerdeTopo };
            var titulo = Texto("Editor de Save", 22, Amarelo, true);
            titulo.Margin = new Thickness(16, 10);
            titulo.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(titulo, Dock.Left);
            barra.Children.Add(titulo);
            // Botoes do topo quebram de linha com a janela estreita, em vez de sair da tela
            barra.Children.Add(new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                ItemSpacing = 8,
                LineSpacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 8),
                Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { CentroV(Texto("Save", 14, Cinza)), _saveSel } }, recarregar, _salvar, fechar },
            });

            // Cartao Jogo
            var jogo = NovoCartao("Jogo");
            ((StackPanel)jogo.Child!).Children.Add(LinhaCampo("Inflação", "5 a 100", _inflacao, _temporada));

            // Cartao Treinadores (escondido se o save nao tiver treinador humano)
            _cartaoTreinadores = NovoCartao("Treinadores");
            ((StackPanel)_cartaoTreinadores.Child!).Children.Add(_treinadores);
            _cartaoTreinadores.IsVisible = false;

            // Cartao Clube
            _teamSel.SelectionChanged += (_, _) => TrocouTime();
            _previa.Child = _previaTxt;
            _previaTxt.HorizontalAlignment = HorizontalAlignment.Center;
            _estMenos = BotaoPasso("−", -1);
            _estMais = BotaoPasso("+", 1);
            var estadio = new DockPanel();
            DockPanel.SetDock(_estMenos, Dock.Left);
            DockPanel.SetDock(_estMais, Dock.Right);
            estadio.Children.Add(_estMenos);
            estadio.Children.Add(_estMais);
            _estadio.HorizontalAlignment = HorizontalAlignment.Center;
            _estadio.VerticalAlignment = VerticalAlignment.Center;
            estadio.Children.Add(_estadio);
            var cores = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*"), Margin = new Thickness(0, 4, 0, 0) };
            var bLetra = BotaoCor("Letra", _amostraLetra, _hexLetra, true);
            var bFundo = BotaoCor("Fundo", _amostraFundo, _hexFundo, false);
            Grid.SetColumn(bFundo, 2);
            cores.Children.Add(bLetra);
            cores.Children.Add(bFundo);
            var clube = NovoCartao("Clube");
            var cc = (StackPanel)clube.Child!;
            cc.Children.Add(_teamSel);
            cc.Children.Add(_previa);
            cc.Children.Add(LinhaCampo("Dinheiro", "0 a 999.999.999", _dinheiro));
            cc.Children.Add(LinhaCampo("Moral", "0 a 20", _moral));
            cc.Children.Add(LinhaCampo("Estádio", "5.000 a 120.000", estadio));
            cc.Children.Add(Texto("Cores", 14, Cinza));
            cc.Children.Add(cores);

            var esquerda = new ScrollViewer
            {
                Width = 330,
                Content = new StackPanel { Spacing = 10, Children = { jogo, _cartaoTreinadores, clube } },
            };

            // Tabela de jogadores
            _lista = new ListBox
            {
                Background = Brushes.Transparent,
                ItemTemplate = new FuncDataTemplate<LinhaJogador>((l, _) => l == null ? new Panel() : LinhaTabela(l)),
            };
            _lista.DoubleTapped += async (_, _) => await EditarJogador();
            _lista.KeyDown += async (_, e) => { if (e.Key == Key.Enter) await EditarJogador(); };
            var cabecalho = Grade(new[] { "Pos", "Nome", "✱", "Força", "Salário", "Nota", "Lesão", "Comport.", "Sit." }, true);
            cabecalho.Margin = new Thickness(8, 0, 8, 6);
            var dica = Texto("Duplo-clique (ou Enter) num jogador para editar. S = suspenso, L = lesionado (jogos).", 12, Cinza);
            dica.Margin = new Thickness(4, 6, 0, 0);
            var grade = new DockPanel();
            DockPanel.SetDock(cabecalho, Dock.Top);
            grade.Children.Add(cabecalho);
            grade.Children.Add(_lista);
            var tabela = new DockPanel();
            DockPanel.SetDock(dica, Dock.Bottom);
            tabela.Children.Add(dica);
            tabela.Children.Add(RolagemLateral(grade, LarguraMinima(Colunas, 160, 6 * 9 + 40)));

            var corpo = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(esquerda, Dock.Left);
            corpo.Children.Add(esquerda);
            corpo.Children.Add(new Border
            {
                Background = Cartao,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                Margin = new Thickness(12, 0, 0, 0),
                Child = tabela,
            });

            var raiz = new DockPanel();
            DockPanel.SetDock(barra, Dock.Top);
            raiz.Children.Add(barra);
            raiz.Children.Add(corpo);
            Content = raiz;

            Opened += async (_, _) => await RefreshSaveList(preserveSelection: false);
        }

        // ---- pecas de layout ----

        private Button BotaoPasso(string rotulo, int passo)
        {
            var b = BotaoAmarelo(rotulo, 36);
            b.Click += (_, _) =>
            {
                if (_currentTeam == null || _currentTeam.EstadioOffset < 0) return;
                _currentTeam.Estadio = Math.Max(1, Math.Min(SaveCodec.ESTADIO_MAX, _currentTeam.Estadio + passo));
                MostrarEstadio();
            };
            return b;
        }

        // Botao "Letra"/"Fundo": amostra da cor + nome + codigo; clique abre o seletor
        private Border BotaoCor(string nome, Border amostra, TextBlock hex, bool letra)
        {
            var b = new Border
            {
                Background = LinhaPar,
                BorderBrush = Cor(0x2E6B2E),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { amostra, new StackPanel { Children = { Texto(nome, 14, Branco, true), hex } } },
                },
            };
            b.PointerPressed += async (_, _) =>
            {
                var t = _currentTeam;
                if (t == null || t.CoresOffset < 0) return;
                var cor = await EscolherCor(this, letra ? "Cor da letra" : "Cor do fundo", letra ? t.CorLetra : t.CorFundo);
                if (cor == null) return;
                if (letra) t.CorLetra = cor.Value; else t.CorFundo = cor.Value;
                PintarCores();
            };
            return b;
        }

        private static Grid Grade(string[] textos, bool cabecalho)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions(Colunas) };
            for (int i = 0; i < textos.Length; i++)
            {
                var tb = Texto(textos[i], cabecalho ? 12 : 14, cabecalho ? Amarelo : Branco, cabecalho);
                tb.TextTrimming = TextTrimming.CharacterEllipsis;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0);
                tb.HorizontalAlignment = i == 1 || i == 7 ? HorizontalAlignment.Left
                    : i == 0 || i == 2 ? HorizontalAlignment.Center : HorizontalAlignment.Right;
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
            return g;
        }

        private sealed class LinhaJogador
        {
            public int Indice;
            public SavePlayer Jogador = null!;
        }

        private static Control LinhaTabela(LinhaJogador l)
        {
            var j = l.Jogador;
            string comp = j.Comportamento >= 0 && j.Comportamento < SaveCodec.ComportamentoLabels.Length
                ? SaveCodec.ComportamentoLabels[j.Comportamento] : "?";
            string sit = ((j.Suspensao > 0 ? "S" + j.Suspensao : "") + (j.JogosLesionado > 0 ? " L" + j.JogosLesionado : "")).Trim();
            var g = Grade(new[] { "", j.Nome, j.Estrela ? "✱" : "", j.Forca.ToString(), Milhar(j.Salario),
                j.Nota.ToString(), j.Lesao.ToString(), comp, sit }, false);
            var selo = new Border
            {
                Background = CorPosicao(j.Posicao),
                CornerRadius = new CornerRadius(4),
                Width = 28,
                Padding = new Thickness(0, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = j.Posicao, Foreground = Branco, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
            };
            g.Children.Add(selo);
            ((TextBlock)g.Children[2]).Foreground = Ouro;
            if (j.Forca > SaveCodec.FORCA_WARN_ABOVE) ((TextBlock)g.Children[3]).Foreground = Amarelo;
            if (sit.Length > 0) ((TextBlock)g.Children[8]).Foreground = Alerta;
            return new Border
            {
                Background = l.Indice % 2 == 0 ? LinhaPar : Brushes.Transparent,
                Padding = new Thickness(8, 7),
                Child = g,
            };
        }

        // ---- dados ----

        private async Task RefreshSaveList(bool preserveSelection)
        {
            string? prev = preserveSelection ? _saveSel.SelectedItem as string : null;
            if (!Directory.Exists(_jogosDir))
            {
                await Dialogos.Mensagem(this, $"Pasta JOGOS não encontrada:\n{_jogosDir}", "Sem saves");
                return;
            }
            var files = Directory.GetFiles(_jogosDir, "*.e98", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).OrderBy(n => n).ToList();
            _saveSel.ItemsSource = files;
            if (files.Count > 0)
            {
                int found = prev != null ? files.IndexOf(prev) : -1;
                _saveSel.SelectedIndex = found >= 0 ? found : 0;
                if (found >= 0) await LoadSelected();
            }
            else await Dialogos.Mensagem(this, "Nenhum jogo gravado em JOGOS ainda.", "Sem saves");
        }

        private async Task LoadSelected()
        {
            if (_saveSel.SelectedItem is not string name) return;
            _currentPath = Path.Combine(_jogosDir, name);
            try
            {
                _current = SaveCodec.Read(_currentPath);
                _currentTeam = null;
                _inflacao.IsEnabled = _current.InflacaoOffset > 0;
                _inflacaoMostrada = _current.InflacaoOffset > 0 ? Fmt(_current.Inflacao * 10) : "";
                _inflacao.Text = _inflacaoMostrada;
                _temporada.Text = _current.InflacaoOffset > 0 ? $"temporada {_current.Ano}" : "";
                _teams = _current.Teams.OrderBy(t => t.NomeExibido, StringComparer.OrdinalIgnoreCase).ToList();
                _trocandoTime = true;
                _teamSel.ItemsSource = _teams.Select(t => t.NomeExibido).ToList();
                _teamSel.SelectedIndex = -1;
                _trocandoTime = false;
                _teamSel.SelectedIndex = _teams.Count > 0 ? 0 : -1;
                MostrarTreinadores();
                _salvar.IsEnabled = true;
            }
            catch (Exception ex)
            {
                _current = null;
                MostrarTreinadores();
                _salvar.IsEnabled = false;
                await Dialogos.Mensagem(this, $"Falha ao ler save:\n{ex.Message}", "Erro");
            }
        }

        private async void TrocouTime()
        {
            if (_trocandoTime) return;
            int i = _teamSel.SelectedIndex;
            if (i < 0 || i >= _teams.Count || _teams[i] == _currentTeam) return;
            if (_currentTeam != null)
            {
                var erro = AplicarCampos();
                if (erro != null)
                {
                    _trocandoTime = true;
                    _teamSel.SelectedIndex = _teams.IndexOf(_currentTeam);
                    _trocandoTime = false;
                    await Dialogos.Mensagem(this, erro, "Valor fora do limite");
                    return;
                }
            }
            _currentTeam = _teams[i];
            MostrarCampos();
            MostrarJogadores();
        }

        private void MostrarCampos()
        {
            var t = _currentTeam!;
            _dinheiro.Text = t.Verba.ToString();
            _moral.IsEnabled = t.MoralOffset > 0;
            _moralMostrado = t.MoralOffset > 0 ? Fmt(t.Moral * 10) : "";
            _moral.Text = _moralMostrado;
            MostrarEstadio();
            PintarCores();
        }

        private void MostrarJogadores(int selecionado = -1)
        {
            if (_currentTeam == null) { _lista.ItemsSource = null; return; }
            _lista.ItemsSource = SaveCodec.OrdemDoJogo(_currentTeam.Players).Select((p, i) => new LinhaJogador { Indice = i, Jogador = p }).ToList();
            if (selecionado >= 0) _lista.SelectedIndex = selecionado;
        }

        private void MostrarEstadio()
        {
            bool tem = _currentTeam != null && _currentTeam.EstadioOffset > 0;
            _estadio.Text = tem ? Milhar(_currentTeam!.Estadio * 5000) : "—";
            _estMenos.IsEnabled = tem && _currentTeam!.Estadio > 1;
            _estMais.IsEnabled = tem && _currentTeam!.Estadio < SaveCodec.ESTADIO_MAX;
        }

        private void PintarCores()
        {
            var t = _currentTeam;
            _previaTxt.Text = t?.NomeExibido ?? "";
            if (t == null || t.CoresOffset < 0)
            {
                _previa.Background = Brushes.Transparent;
                _previaTxt.Foreground = Branco;
                _hexLetra.Text = _hexFundo.Text = "—";
                return;
            }
            _amostraLetra.Background = Cor(t.CorLetra);
            _amostraFundo.Background = Cor(t.CorFundo);
            _hexLetra.Text = $"#{t.CorLetra:X6}";
            _hexFundo.Text = $"#{t.CorFundo:X6}";
            _previa.Background = Cor(t.CorFundo);
            _previaTxt.Foreground = Cor(t.CorLetra);
        }

        // ---- limites ----

        private static string Faixa(string nome, double min, double max) =>
            $"{nome}: use um valor de {Milhar((long)min)} a {Milhar((long)max)}.";

        // Le um inteiro do campo; fora da faixa (ou nao numerico) devolve o erro em vez de cortar
        private static string? LerInteiro(TextBox tb, string nome, long min, long max, out long v)
        {
            if (long.TryParse((tb.Text ?? "").Trim().Replace(".", ""), out v) && v >= min && v <= max) return null;
            return Faixa(nome, min, max);
        }

        private static string? LerReal(TextBox tb, string nome, double min, double max, out double v)
        {
            if (double.TryParse((tb.Text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                && v >= min && v <= max) return null;
            return Faixa(nome, min, max);
        }

        // Passa os campos do clube e da inflacao pro save em memoria (dentro dos
        // limites do jogo); devolve a mensagem de erro, ou null
        private string? AplicarCampos()
        {
            if (_current != null && _current.InflacaoOffset > 0 && (_inflacao.Text ?? "").Trim() != _inflacaoMostrada)
            {
                var e = LerReal(_inflacao, "Inflação", SaveCodec.INFLACAO_MIN * 10, SaveCodec.INFLACAO_MAX * 10, out double inf);
                if (e != null) return e;
                _current.Inflacao = inf / 10;
            }
            if (_currentTeam != null)
            {
                var e = LerInteiro(_dinheiro, "Dinheiro", 0, SaveCodec.DINHEIRO_MAX, out long verba);
                if (e != null) return e;
                if (_currentTeam.MoralOffset > 0 && (_moral.Text ?? "").Trim() != _moralMostrado)
                {
                    e = LerReal(_moral, "Moral", 0, SaveCodec.MORAL_MAX * 10, out double m);
                    if (e != null) return e;
                    _currentTeam.Moral = m / 10;
                }
                _currentTeam.Verba = verba;
            }
            return null;
        }

        private async Task SaveCurrent()
        {
            if (_current == null || string.IsNullOrEmpty(_currentPath)) return;
            var erro = AplicarCampos();
            if (erro != null)
            {
                await Dialogos.Mensagem(this, erro, "Valor fora do limite");
                return;
            }
            try
            {
                var bak = _currentPath + ".bak";
                if (!File.Exists(bak)) File.Copy(_currentPath, bak);
                SaveCodec.Write(_currentPath, _current);
                await Dialogos.Mensagem(this, "Save gravado (backup .bak na primeira vez).", "Salvo");
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, $"Erro ao gravar:\n{ex.Message}", "Erro");
            }
        }

        // ---- treinadores ----

        private void MostrarTreinadores()
        {
            _treinadores.Children.Clear();
            bool algum = false;
            if (_current != null)
            {
                foreach (var tec in _current.Tecnicos.Where(t => t.Humano))
                {
                    algum = true;
                    var equipe = _current.TimeDoTecnico(tec);
                    var linha = new DockPanel();
                    if (equipe != null)
                    {
                        var b = BotaoAmarelo("Trocar", 80);
                        b.VerticalAlignment = VerticalAlignment.Center;
                        b.Click += async (_, _) => await TrocarEquipe(tec);
                        DockPanel.SetDock(b, Dock.Right);
                        linha.Children.Add(b);
                    }
                    linha.Children.Add(new StackPanel
                    {
                        Children = { Texto(tec.Nome, 15, Branco, true), Texto(equipe?.NomeExibido ?? "sem equipe", 12, Cinza) },
                    });
                    _treinadores.Children.Add(linha);
                }
            }
            _cartaoTreinadores.IsVisible = algum;
        }

        // Escolhe a equipe nova; as duas equipes trocam de treinador como numa
        // "chicotada psicologica" do jogo
        private async Task TrocarEquipe(SaveTecnico tec)
        {
            if (_current == null) return;
            var erro = AplicarCampos();
            if (erro != null) { await Dialogos.Mensagem(this, erro, "Valor fora do limite"); return; }
            var origem = _current.TimeDoTecnico(tec);
            var destinos = _teams.Where(t => t != origem && t.TecnicoId >= 0 && t.PodeTerHumano).ToList();
            if (origem == null) return;
            if (destinos.Count == 0) { await Dialogos.Mensagem(this, "Não achei as divisões neste save: sem elas não dá pra saber quais equipes podem ter treinador humano.", "Trocar de equipe"); return; }

            var w = Dialogo($"Trocar de equipe — {tec.Nome}");
            var combo = new ComboBox
            {
                ItemsSource = destinos.Select(t => $"{t.NomeExibido}  ({t.Divisao})").ToList(),
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var efeito = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, MaxWidth = 420 };
            void Atualiza()
            {
                var outro = _current.Tecnico(destinos[Math.Max(0, combo.SelectedIndex)].TecnicoId);
                efeito.Text = (outro != null ? $"{outro.Nome} vai para {origem.NomeExibido}. " : "")
                    + "As duas equipes ficam com moral 10, como numa chicotada psicológica do jogo. "
                    + "Só aparecem equipes das divisões (no Distrital o jogo quebra).";
            }
            combo.SelectionChanged += (_, _) => Atualiza();
            Atualiza();
            var ok = new Button { Content = "Trocar", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelar = new Button { Content = "Cancelar", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (_, _) => w.Close(true);
            cancelar.Click += (_, _) => w.Close(false);
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Width = 440,
                Children =
                {
                    new TextBlock { Text = $"Equipe atual: {origem.NomeExibido}" },
                    new TextBlock { Text = "Nova equipe", Margin = new Thickness(0, 6, 0, 0) },
                    combo,
                    efeito,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } },
                },
            };
            if (!await w.ShowDialog<bool>(this)) return;
            var destino = destinos[combo.SelectedIndex];
            try
            {
                SaveCodec.TrocarEquipe(_current, tec, destino);
            }
            catch (InvalidOperationException ex)
            {
                await Dialogos.Mensagem(this, ex.Message, "Erro");
                return;
            }
            MostrarTreinadores();
            if (_currentTeam != null) MostrarCampos();
            _teamSel.SelectedIndex = _teams.IndexOf(destino);
            await Dialogos.Mensagem(this, $"{tec.Nome} agora treina {destino.NomeExibido}.\nClique em Salvar para gravar no save.", "Equipe trocada");
        }

        // ---- ficha do jogador ----

        private async Task EditarJogador()
        {
            if (_currentTeam == null || _lista.SelectedItem is not LinhaJogador { Jogador: var j }) return;
            int idx = _lista.SelectedIndex;
            var w = Dialogo(j.Nome);

            var selo = new Border
            {
                Background = CorPosicao(j.Posicao),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 3),
                Child = new TextBlock { Text = NomePosicao(j.Posicao), Foreground = Branco, FontWeight = FontWeight.Bold },
            };
            var estrela = new TextBlock { FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
            var regra = new TextBlock
            {
                Text = "Estrela (✱): Médio ou Avançado com nota 8 ou mais. Muda sozinha com a nota.",
                Foreground = Brushes.Gray,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };

            var grade = new Grid { ColumnDefinitions = new ColumnDefinitions("150,16,150,16,150") };
            int celula = 0;
            TextBox Campo(string titulo, string faixa, int valor)
            {
                var tb = new TextBox { Text = valor.ToString() };
                Celula(titulo, faixa, tb);
                return tb;
            }
            void Celula(string titulo, string faixa, Control campo, int colunas = 1)
            {
                var c = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 10) };
                c.Children.Add(new TextBlock { Text = titulo });
                c.Children.Add(new TextBlock { Text = faixa, FontSize = 11, Foreground = Brushes.Gray });
                c.Children.Add(campo);
                int linha = celula / 3, col = celula % 3;
                while (grade.RowDefinitions.Count <= linha) grade.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(c, linha);
                Grid.SetColumn(c, col * 2);
                Grid.SetColumnSpan(c, colunas * 2 - 1);
                grade.Children.Add(c);
                celula += colunas;
            }
            var forca = Campo("Força", $"1 a {SaveCodec.FORCA_MAX} (normal ≤ 50)", j.Forca);
            var salario = Campo("Salário", "50 a 9.999.999", j.Salario);
            var nota = Campo("Nota", "1 a 10", j.Nota);
            var lesao = Campo("Lesão", "0 = nunca, 10 = muito", j.Lesao);
            var suspenso = Campo("Suspenso (S)", "jogos, 0 a 4", j.Suspensao);
            var lesionado = Campo("Lesionado (L)", "jogos, 0 a 20", j.JogosLesionado);
            var comp = new ComboBox
            {
                ItemsSource = SaveCodec.ComportamentoLabels,
                SelectedIndex = Math.Max(0, Math.Min(5, j.Comportamento)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            Celula("Comportamento", "Fair Play … Sarrafeiro", comp, 2);

            // Estrela ao vivo: se a nota mudar, segue a regra do jogo; senao mantem a do save
            void MostrarEstrela()
            {
                bool tem = j.Estrela;
                if (int.TryParse((nota.Text ?? "").Trim(), out int n) && n != j.Nota) tem = SaveCodec.TemEstrela(j.Posicao, n);
                estrela.Text = tem ? "✱ Estrela" : "Sem estrela";
                estrela.Foreground = tem ? Cor(0xC79A00) : Brushes.Gray;
            }
            nota.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) MostrarEstrela(); };
            MostrarEstrela();

            var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelar = new Button { Content = "Cancelar", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancelar.Click += (_, _) => w.Close(false);
            // OK so fecha se tudo estiver dentro dos limites
            ok.Click += async (_, _) =>
            {
                long f = 0, sal = 0, n = 0, l = 0, su = 0, le = 0;
                var erro = LerInteiro(forca, "Força", SaveCodec.FORCA_MIN, SaveCodec.FORCA_MAX, out f)
                    ?? LerInteiro(salario, "Salário", SaveCodec.SALARIO_MIN, SaveCodec.SALARIO_MAX, out sal)
                    ?? LerInteiro(nota, "Nota", 1, 10, out n)
                    ?? LerInteiro(lesao, "Lesão", 0, 10, out l)
                    ?? LerInteiro(suspenso, "Suspenso", 0, SaveCodec.SUSPENSAO_MAX, out su)
                    ?? LerInteiro(lesionado, "Lesionado", 0, SaveCodec.JOGOS_LESIONADO_MAX, out le);
                if (erro != null) { await Dialogos.Mensagem(w, erro, "Valor fora do limite"); return; }
                if (f > SaveCodec.FORCA_WARN_ABOVE && f != j.Forca &&
                    !await Dialogos.Confirmar(w, $"Força {f} é bem acima do normal (1-{SaveCodec.FORCA_WARN_ABOVE}).\nContinuar mesmo assim?", "Aviso"))
                    return;
                if (n != j.Nota) j.Estrela = SaveCodec.TemEstrela(j.Posicao, (int)n);
                j.Forca = (int)f;
                j.Salario = (int)sal;
                j.Nota = (int)n;
                j.Lesao = (int)l;
                j.Suspensao = (int)su;
                j.JogosLesionado = (int)le;
                j.Comportamento = comp.SelectedIndex;
                w.Close(true);
            };

            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Children =
                {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { selo, estrela } },
                    regra,
                    grade,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } },
                },
            };
            w.Opened += (_, _) => { forca.Focus(); forca.SelectAll(); };
            if (await w.ShowDialog<bool>(this)) MostrarJogadores(idx);
        }

    }
}
