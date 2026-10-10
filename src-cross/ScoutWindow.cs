using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using static ElifootLauncher.Visual;

namespace ElifootLauncher
{
    // Scout: todos os jogadores de um save (como o Turbo Save), com filtros e
    // ordenacao para achar os melhores. So leitura.
    public class ScoutWindow : Window
    {
        // A estrela logo depois do nome; equipe depois dos numeros
        // Jogos, Gols, Lesoes e Expuls. sao o historial do jogador no save
        // Score e Impacto: src/Score.cs (o botao "?" explica cada coluna)
        private const string Colunas = "40,*,26,70,74,66,60,64,100,78,66,56,70,74,150,60,92,70";
        private static readonly string[] Titulos = { "Pos", "Nome", "✱", "Score", "País", "Força", "Nota", "Lesão", "Comport.", "Impacto", "Jogos", "Gols", "Lesões", "Expuls.", "Equipe", "Div.", "Salário", "Situação" };

        private sealed class Linha
        {
            public SavePlayer J = null!;
            public SaveTeam Equipe = null!;
            public int Pos;            // 0 G, 1 D, 2 M, 3 A
            public string Divisao = "";
            public int OrdemDivisao;   // 1..4, 5 Distrital, 9 sem divisao
            public string Situacao = "";
            public bool Humana;
            public int Score;
            public double Impacto;
            public int Indice;
        }

        private readonly string _jogosDir, _gameDir;
        private readonly Dictionary<string, Pais> _porCodigo;
        private readonly Dictionary<string, Bitmap?> _bandeiras = new Dictionary<string, Bitmap?>();
        private readonly RegrasEquipe _regras;
        private List<Linha> _todos = new List<Linha>();
        private readonly List<Criterio> _criterios = new List<Criterio> { new Criterio { Coluna = 5, Desc = true } };
        private bool _montando;

        private readonly ComboBox _saveSel = new ComboBox { Width = 200 };
        private readonly Button _botaoFiltros = BotaoAmarelo("Filtros ◂", 110);
        private readonly TextBox _nome = new TextBox();
        private readonly ToggleButton[] _posicoes = new ToggleButton[4];
        private readonly ComboBox _pais = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly ComboBox _divisao = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly ComboBox _equipe = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBox _forcaMin = new TextBox { Width = 70 }, _forcaMax = new TextBox { Width = 70 };
        private readonly ComboBox _notaMin = new ComboBox { Width = 120 }, _lesaoMax = new ComboBox { Width = 120 }, _compMax = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly CheckBox _soEstrelas = Marcar("Só com estrela (✱)"), _soEstrangeiros = Marcar("Só estrangeiros (contam no limite)"),
            _semIndisponiveis = Marcar("Esconder suspensos e lesionados"), _semHumanas = Marcar("Esconder as equipes humanas");
        private readonly TextBlock _conta = Texto("", 13, Cinza);
        private readonly Grid _cabecalho = new Grid { ColumnDefinitions = new ColumnDefinitions(Colunas) };
        private readonly ListBox _lista = new ListBox { Background = Brushes.Transparent };

        public ScoutWindow(string jogosDir, string gameDir)
        {
            _jogosDir = jogosDir;
            _gameDir = gameDir;
            var paises = new List<Pais>();
            try { paises = TeamCodec.LerPaises(gameDir); } catch { }
            _porCodigo = paises.ToDictionary(p => p.Codigo);
            _regras = new RegrasEquipe(gameDir, paises);

            Title = "Scout";
            Width = 1720;
            Height = 900;
            MinWidth = 820;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();
            Background = Verde;
            AplicarEstilos(this);
            CaberNaTela(this);

            // Barra do topo
            _saveSel.SelectionChanged += (_, _) => CarregarSave();
            var recarregar = BotaoClaro("Recarregar");
            recarregar.Click += (_, _) => ListarSaves(true);
            var fechar = BotaoClaro("Fechar");
            var ajuda = BotaoClaro("?");
            ajuda.Click += async (_, _) =>
            {
                var w = Dialogo("O que significa cada coluna");
                var ok = BotaoAmarelo("OK", 90);
                ok.HorizontalAlignment = HorizontalAlignment.Right;
                ok.Click += (_, _) => w.Close();
                w.Background = Verde;
                w.Content = new StackPanel
                {
                    Margin = new Thickness(18),
                    Spacing = 12,
                    Width = 620,
                    Children = { new TextBlock { Text = Score.Ajuda, TextWrapping = TextWrapping.Wrap, Foreground = Branco, FontSize = 14 }, ok },
                };
                await w.ShowDialog(this);
            };
            fechar.Click += (_, _) => Close();
            var barra = new DockPanel { Background = VerdeTopo };
            var titulo = Texto("Scout", 22, Amarelo, true);
            titulo.Margin = new Thickness(16, 10);
            titulo.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(titulo, Dock.Left);
            barra.Children.Add(titulo);
            var sub = Texto("os melhores jogadores do seu jogo", 13, CinzaFaixa);
            sub.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(sub, Dock.Left);
            barra.Children.Add(sub);
            SizeChanged += (_, e) => sub.IsVisible = e.NewSize.Width > 1180;  // estreita: sem o subtitulo
            // Botoes do topo quebram de linha com a janela estreita, em vez de sair da tela
            barra.Children.Add(new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                ItemSpacing = 8,
                LineSpacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 8),
                Children = { new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { CentroV(Texto("Save", 14, Cinza)), _saveSel } }, _botaoFiltros, ajuda, recarregar, fechar },
            });

            // Filtros
            _nome.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Filtrar(); };
            var posicoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            for (int i = 0; i < 4; i++)
            {
                var b = new ToggleButton
                {
                    Content = TeamCodec.PosicoesCurtas[i],
                    Width = 44,
                    Height = 32,
                    FontWeight = FontWeight.Bold,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    IsChecked = true,
                };
                ToolTip.SetTip(b, TeamCodec.Posicoes[i]);
                b.IsCheckedChanged += (_, _) => { PintarPosicoes(); Filtrar(); };
                _posicoes[i] = b;
                posicoes.Children.Add(b);
            }
            PintarPosicoes();
            _pais.ItemTemplate = new FuncDataTemplate<Tuple<string?, string>>((t, _) => t == null ? new Panel() : LinhaBandeira(t.Item1, t.Item2));
            _equipe.ItemTemplate = new FuncDataTemplate<Tuple<string?, string>>((t, _) => t == null ? new Panel() : LinhaBandeira(t.Item1, t.Item2));
            foreach (var c in new[] { _pais, _divisao, _equipe, _notaMin, _lesaoMax, _compMax })
                c.SelectionChanged += (_, _) => Filtrar();
            foreach (var tb in new[] { _forcaMin, _forcaMax })
                tb.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Filtrar(); };
            foreach (var cb in new[] { _soEstrelas, _soEstrangeiros, _semIndisponiveis, _semHumanas })
                cb.IsCheckedChanged += (_, _) => Filtrar();
            _notaMin.ItemsSource = new[] { "Qualquer" }.Concat(Enumerable.Range(2, 9).Select(n => $"{n} ou mais")).ToList();
            _lesaoMax.ItemsSource = new[] { "Qualquer" }.Concat(Enumerable.Range(0, 10).Select(n => $"até {n}")).ToList();
            _compMax.ItemsSource = new[] { "Qualquer" }.Concat(SaveCodec.ComportamentoLabels.Select((c, i) => i == 0 ? "Só Fair Play" : $"Até {c}")).ToList();
            var limpar = BotaoClaro("Limpar filtros");
            limpar.Click += (_, _) => LimparFiltros();

            var cartao = NovoCartao("Filtros");
            var cf = (StackPanel)cartao.Child!;
            cf.Spacing = 8;
            cf.Children.Add(Rotulo("Nome", _nome));
            cf.Children.Add(Rotulo("Posição", posicoes));
            cf.Children.Add(Rotulo("País do jogador", _pais));
            cf.Children.Add(Rotulo("Divisão", _divisao));
            cf.Children.Add(Rotulo("Equipe", _equipe));
            cf.Children.Add(Rotulo("Força", new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { CentroV(Texto("de", 13, Cinza)), _forcaMin, CentroV(Texto("até", 13, Cinza)), _forcaMax },
            }));
            cf.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children = { Rotulo("Nota", _notaMin), Rotulo("Lesão", _lesaoMax) },
            });
            cf.Children.Add(Rotulo("Comportamento", _compMax));
            cf.Children.Add(_soEstrelas);
            cf.Children.Add(_soEstrangeiros);
            cf.Children.Add(_semIndisponiveis);
            cf.Children.Add(_semHumanas);
            cf.Children.Add(limpar);
            var esquerda = new ScrollViewer { Width = 330, Content = cartao };

            // Tabela
            for (int i = 0; i < Titulos.Length; i++)
            {
                int col = i;
                var tb = Texto(Titulos[i], 12, Amarelo, true);
                tb.Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0);
                tb.HorizontalAlignment = Alinhamento(i);
                tb.Cursor = new Cursor(StandardCursorType.Hand);
                tb.Background = Brushes.Transparent;
                tb.PointerPressed += (_, _) => Ordenar(col);
                Grid.SetColumn(tb, i);
                _cabecalho.Children.Add(tb);
            }
            _cabecalho.Margin = new Thickness(8, 0, 8, 6);
            _lista.ItemTemplate = new FuncDataTemplate<Linha>((l, _) => l == null ? new Panel() : LinhaTabela(l));
            var rodape = new DockPanel { Margin = new Thickness(4, 6, 0, 0) };
            rodape.Children.Add(_conta);
            var grade = new DockPanel();
            DockPanel.SetDock(_cabecalho, Dock.Top);
            grade.Children.Add(_cabecalho);
            grade.Children.Add(_lista);
            var tabela = new DockPanel();
            DockPanel.SetDock(rodape, Dock.Bottom);
            tabela.Children.Add(rodape);
            tabela.Children.Add(RolagemLateral(grade, LarguraMinima(Colunas, 170, 6 * Titulos.Length + 40)));

            var corpo = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(esquerda, Dock.Left);
            corpo.Children.Add(esquerda);
            var cartaoTabela = new Border
            {
                Background = Cartao,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                Margin = new Thickness(12, 0, 0, 0),
                Child = tabela,
            };
            corpo.Children.Add(cartaoTabela);
            // Recolher os filtros da a largura toda para a tabela
            _botaoFiltros.Click += (_, _) =>
            {
                esquerda.IsVisible = !esquerda.IsVisible;
                cartaoTabela.Margin = new Thickness(esquerda.IsVisible ? 12 : 0, 0, 0, 0);
                _botaoFiltros.Content = esquerda.IsVisible ? "Filtros ◂" : "Filtros ▸";
            };
            var raiz = new DockPanel();
            DockPanel.SetDock(barra, Dock.Top);
            raiz.Children.Add(barra);
            raiz.Children.Add(corpo);
            Content = raiz;

            LimparFiltros();
            Opened += (_, _) => ListarSaves(false);
        }

        // ---- pecas ----

        private static CheckBox Marcar(string t) => new CheckBox { Content = t, Foreground = Branco };

        private static StackPanel Rotulo(string rotulo, Control campo) => new StackPanel
        {
            Spacing = 3,
            Children = { Texto(rotulo, 13, Cinza), campo },
        };

        private static HorizontalAlignment Alinhamento(int i) =>
            i == 1 || i == 4 || i == 8 || i == 14 || i == 17 ? HorizontalAlignment.Left
            : i == 0 || i == 2 || i == 15 ? HorizontalAlignment.Center : HorizontalAlignment.Right;

        // Ligado: cor da posicao; desligado: verde escuro (o tema pintaria de azul)
        private void PintarPosicoes()
        {
            for (int i = 0; i < 4; i++)
            {
                var b = _posicoes[i];
                var on = Cor(new[] { 0xE0B000, 0x3D7BD9, 0x2E9E4F, 0xD9443D }[i]);
                var off = Cor(0x2A5C2A);
                foreach (var sufixo in new[] { "", "PointerOver", "Pressed" })
                {
                    b.Resources["ToggleButtonBackground" + sufixo] = off;
                    b.Resources["ToggleButtonForeground" + sufixo] = Cor(0xA9C4A9);
                    b.Resources["ToggleButtonBackgroundChecked" + sufixo] = on;
                    b.Resources["ToggleButtonForegroundChecked" + sufixo] = Branco;
                }
            }
        }

        private Bitmap? Bandeira(string? codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return null;
            if (_bandeiras.TryGetValue(codigo!, out var b)) return b;
            try
            {
                var p = TeamCodec.Caminho(_gameDir, "FLAGS", codigo + ".BMP");
                b = File.Exists(p) ? new Bitmap(p) : null;
            }
            catch { b = null; }
            _bandeiras[codigo!] = b;
            return b;
        }

        private Control LinhaBandeira(string? codigo, string texto)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var b = Bandeira(codigo);
            sp.Children.Add(b != null ? new Image { Source = b, Width = 24, Height = 16, Stretch = Stretch.Fill } : new Border { Width = 24 });
            sp.Children.Add(new TextBlock { Text = texto, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        private string NomePais(string codigo) => _porCodigo.TryGetValue(codigo, out var p) ? p.Nome : codigo;

        private Control LinhaTabela(Linha l)
        {
            var j = l.J;
            string sl = ((j.Suspensao > 0 ? "S" + j.Suspensao : "") + (j.JogosLesionado > 0 ? " L" + j.JogosLesionado : "")).Trim();
            string sit = sl.Length > 0 ? sl : l.Situacao == "Estrangeiro" ? "Estrang." : l.Situacao;
            var textos = new[] { "", j.Nome, j.Estrela ? "✱" : "", l.Score.ToString(), j.Pais, j.Forca.ToString(), j.Nota.ToString(), j.Lesao.ToString(),
                SaveCodec.ComportamentoLabels[Math.Max(0, Math.Min(5, j.Comportamento))], Score.TextoImpacto(l.Impacto),
                j.Jogos.ToString(), j.Gols.ToString(), j.Lesoes.ToString(), j.Expulsoes.ToString(), l.Equipe.NomeExibido,
                l.OrdemDivisao <= 4 ? l.OrdemDivisao + "ª" : l.OrdemDivisao == 5 ? "Dist." : "—", Milhar(j.Salario), sit };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions(Colunas) };
            for (int i = 0; i < textos.Length; i++)
            {
                Control c;
                if (i == 0)
                    c = new Border
                    {
                        Background = CorPosicao(j.Posicao),
                        CornerRadius = new CornerRadius(4),
                        Width = 28,
                        Child = new TextBlock { Text = j.Posicao, Foreground = Branco, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
                    };
                else if (i == 4)
                {
                    var b = Bandeira(j.Pais);
                    var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
                    sp.Children.Add(b != null ? new Image { Source = b, Width = 21, Height = 14, Stretch = Stretch.Fill } : new Border { Width = 21 });
                    sp.Children.Add(Texto(j.Pais, 13, Branco));
                    ToolTip.SetTip(sp, NomePais(j.Pais));
                    c = sp;
                }
                else
                {
                    var tb = Texto(textos[i], 14, Branco, i == 1 || i == 3 || i == 5);
                    tb.TextTrimming = TextTrimming.CharacterEllipsis;
                    if (i == 14 && l.Humana) tb.Foreground = Amarelo;
                    if (i == 3) tb.Foreground = Ouro;
                    if (i == 9) tb.Foreground = l.Impacto > 0.004 ? Ok : l.Impacto < -0.004 ? Alerta : Branco;
                    if (i == 5 && j.Forca > SaveCodec.FORCA_WARN_ABOVE) tb.Foreground = Amarelo;
                    if (i == 2) tb.Foreground = Ouro;
                    if (i == 17) tb.Foreground = sl.Length > 0 || l.Situacao == "Estrangeiro" ? Alerta : Ok;
                    c = tb;
                }
                c.VerticalAlignment = VerticalAlignment.Center;
                c.HorizontalAlignment = Alinhamento(i);
                c.Margin = new Thickness(i == 0 ? 0 : 6, 0, 0, 0);
                Grid.SetColumn(c, i);
                g.Children.Add(c);
            }
            return new Border { Background = l.Indice % 2 == 0 ? LinhaPar : Brushes.Transparent, Padding = new Thickness(8, 4), Child = g };
        }

        // ---- dados ----

        private void ListarSaves(bool manter)
        {
            var antes = manter ? _saveSel.SelectedItem as string : null;
            var arquivos = Directory.Exists(_jogosDir)
                ? Directory.GetFiles(_jogosDir).Where(f => f.EndsWith(".e98", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).OrderBy(n => n).ToList()
                : new List<string?>();
            _saveSel.ItemsSource = arquivos;
            if (arquivos.Count == 0) { _conta.Text = "Nenhum jogo gravado em JOGOS ainda."; return; }
            int i = antes != null ? arquivos.IndexOf(antes) : -1;
            _saveSel.SelectedIndex = -1;
            _saveSel.SelectedIndex = i >= 0 ? i : 0;
        }

        private static int OrdemDivisao(string d) =>
            d.StartsWith("1") ? 1 : d.StartsWith("2") ? 2 : d.StartsWith("3") ? 3 : d.StartsWith("4") ? 4 : d.StartsWith("Distrital") ? 5 : 9;

        private void CarregarSave()
        {
            if (_saveSel.SelectedItem is not string nome) return;
            SaveFile sf;
            try { sf = SaveCodec.Read(Path.Combine(_jogosDir, nome)); }
            catch (Exception ex)
            {
                _todos = new List<Linha>();
                _conta.Text = "Não consegui ler o save: " + ex.Message;
                Filtrar();
                return;
            }
            var humanas = new HashSet<SaveTeam>(sf.Tecnicos.Where(t => t.Humano).Select(sf.TimeDoTecnico).Where(t => t != null)!);
            _todos = sf.Teams.SelectMany(t => t.Players.Select(j => new Linha
            {
                J = j,
                Equipe = t,
                Pos = Array.IndexOf(TeamCodec.PosicoesCurtas, j.Posicao),
                Divisao = t.Divisao,
                OrdemDivisao = OrdemDivisao(t.Divisao),
                Situacao = _regras.Situacao(j.Pais, t.Pais),
                Humana = humanas.Contains(t),
                Score = Score.Rendimento(j.Posicao, j.Nota, j.Lesao, j.Comportamento),
                Impacto = Score.Impacto(j.Posicao, j.Nota, j.Lesao, j.Comportamento),
            })).ToList();

            _montando = true;
            var paises = _todos.GroupBy(l => l.J.Pais).OrderBy(g => NomePais(g.Key))
                .Select(g => Tuple.Create<string?, string>(g.Key, $"{NomePais(g.Key)} ({g.Count()})")).ToList();
            paises.Insert(0, Tuple.Create<string?, string>(null, $"Todos ({_todos.Count})"));
            _pais.ItemsSource = paises;
            _pais.SelectedIndex = 0;
            var divisoes = new List<string> { "Todas" };
            divisoes.AddRange(sf.Teams.Select(t => t.Divisao).Where(d => d.Length > 0).Distinct().OrderBy(OrdemDivisao));
            _divisao.ItemsSource = divisoes;
            _divisao.SelectedIndex = 0;
            var equipes = sf.Teams.OrderBy(t => t.NomeExibido).Select(t => Tuple.Create<string?, string>(t.Pais, (humanas.Contains(t) ? "★ " : "") + t.NomeExibido)).ToList();
            equipes.Insert(0, Tuple.Create<string?, string>(null, "Todas"));
            _equipe.ItemsSource = equipes;
            _equipe.SelectedIndex = 0;
            _equipesOrdenadas = sf.Teams.OrderBy(t => t.NomeExibido).ToList();
            _montando = false;
            Filtrar();
        }

        private List<SaveTeam> _equipesOrdenadas = new List<SaveTeam>();

        private void LimparFiltros()
        {
            _montando = true;
            _nome.Text = "";
            foreach (var b in _posicoes) b.IsChecked = true;
            _pais.SelectedIndex = _divisao.SelectedIndex = _equipe.SelectedIndex = 0;
            _forcaMin.Text = _forcaMax.Text = "";
            _notaMin.SelectedIndex = _lesaoMax.SelectedIndex = _compMax.SelectedIndex = 0;
            foreach (var cb in new[] { _soEstrelas, _soEstrangeiros, _semIndisponiveis, _semHumanas }) cb.IsChecked = false;
            _criterios.Clear();
            _criterios.Add(new Criterio { Coluna = 5, Desc = true });
            _montando = false;
            PintarPosicoes();
            Filtrar();
        }

        private static int? Numero(TextBox tb) => int.TryParse((tb.Text ?? "").Trim(), out var v) ? v : (int?)null;

        private void Filtrar()
        {
            if (_montando) return;
            var termo = (_nome.Text ?? "").Trim();
            var pais = (_pais.SelectedItem as Tuple<string?, string>)?.Item1;
            var divisao = _divisao.SelectedIndex > 0 ? _divisao.SelectedItem as string : null;
            var equipe = _equipe.SelectedIndex > 0 && _equipe.SelectedIndex - 1 < _equipesOrdenadas.Count ? _equipesOrdenadas[_equipe.SelectedIndex - 1] : null;
            int? fMin = Numero(_forcaMin), fMax = Numero(_forcaMax);
            int notaMin = _notaMin.SelectedIndex > 0 ? _notaMin.SelectedIndex + 1 : 0;
            int lesaoMax = _lesaoMax.SelectedIndex > 0 ? _lesaoMax.SelectedIndex - 1 : 10;
            int compMax = _compMax.SelectedIndex > 0 ? _compMax.SelectedIndex - 1 : 5;
            var lista = _todos.Where(l =>
                (termo.Length == 0 || l.J.Nome.IndexOf(termo, StringComparison.CurrentCultureIgnoreCase) >= 0) &&
                l.Pos >= 0 && _posicoes[l.Pos].IsChecked == true &&
                (pais == null || l.J.Pais == pais) &&
                (divisao == null || l.Divisao == divisao) &&
                (equipe == null || l.Equipe == equipe) &&
                (fMin == null || l.J.Forca >= fMin) && (fMax == null || l.J.Forca <= fMax) &&
                l.J.Nota >= notaMin && l.J.Lesao <= lesaoMax && l.J.Comportamento <= compMax &&
                (_soEstrelas.IsChecked != true || l.J.Estrela) &&
                (_soEstrangeiros.IsChecked != true || l.Situacao == "Estrangeiro") &&
                (_semIndisponiveis.IsChecked != true || (l.J.Suspensao == 0 && l.J.JogosLesionado == 0)) &&
                (_semHumanas.IsChecked != true || !l.Humana)).ToList();
            lista = OrdenarLista(lista);
            for (int i = 0; i < lista.Count; i++) lista[i].Indice = i;
            _lista.ItemsSource = lista;
            _conta.Text = _todos.Count == 0 ? _conta.Text : $"{lista.Count} de {_todos.Count} jogadores · clique nos títulos para ordenar por várias colunas (o 3º clique tira a coluna)";
            for (int i = 0; i < Titulos.Length; i++)
                ((TextBlock)_cabecalho.Children[i]).Text = Titulos[i] + MarcaOrdem(i);
        }

        // 1o clique: entra como proximo criterio; 2o: inverte; 3o: sai
        private void Ordenar(int coluna)
        {
            var achado = _criterios.FirstOrDefault(c => c.Coluna == coluna);
            if (achado == null) _criterios.Add(new Criterio { Coluna = coluna, Desc = SentidoPadrao(coluna) });
            else if (achado.Desc == SentidoPadrao(coluna)) achado.Desc = !achado.Desc;
            else _criterios.Remove(achado);
            if (_criterios.Count == 0) _criterios.Add(new Criterio { Coluna = 5, Desc = true });
            Filtrar();
        }

        // Numeros comecam do maior; textos e lesao/comportamento, do menor
        private static bool SentidoPadrao(int coluna) => coluna == 2 || coluna == 3 || coluna == 5 || coluna == 6 || coluna == 9 || coluna == 10 || coluna == 11 || coluna == 16;

        private sealed class Criterio
        {
            public int Coluna;
            public bool Desc;
        }

        private string MarcaOrdem(int coluna)
        {
            int n = _criterios.FindIndex(c => c.Coluna == coluna);
            if (n < 0) return "";
            return (_criterios.Count > 1 ? " " + "¹²³⁴⁵⁶⁷⁸⁹"[Math.Min(n, 8)] : " ") + (_criterios[n].Desc ? "▼" : "▲");
        }

        private Func<Linha, IComparable> Chave(int coluna) => coluna switch
        {
            0 => x => x.Pos,
            1 => x => x.J.Nome,
            2 => x => x.J.Estrela ? 1 : 0,
            3 => x => x.Score,
            4 => x => NomePais(x.J.Pais),
            6 => x => x.J.Nota,
            7 => x => x.J.Lesao,
            8 => x => x.J.Comportamento,
            9 => x => x.Impacto,
            10 => x => x.J.Jogos,
            11 => x => x.J.Gols,
            12 => x => x.J.Lesoes,
            13 => x => x.J.Expulsoes,
            14 => x => x.Equipe.NomeExibido,
            15 => x => x.OrdemDivisao,
            16 => x => x.J.Salario,
            17 => x => x.Situacao,
            _ => x => x.J.Forca,
        };

        // Varios niveis: o 1o criterio manda, os outros desempatam; por fim forca e nota
        private List<Linha> OrdenarLista(List<Linha> l)
        {
            IOrderedEnumerable<Linha>? o = null;
            foreach (var c in _criterios)
            {
                var k = Chave(c.Coluna);
                o = o == null ? (c.Desc ? l.OrderByDescending(k) : l.OrderBy(k)) : (c.Desc ? o.ThenByDescending(k) : o.ThenBy(k));
            }
            return o!.ThenByDescending(x => x.J.Forca).ThenByDescending(x => x.J.Nota).ToList();
        }
    }
}
