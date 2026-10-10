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
    // Editor de Equipes nativo (substitui o EDITEQ.EXE). Esquerda: busca e filtro
    // por pais das equipes de EQUIPAS. Direita: dados da equipe, validacao com as
    // regras do jogo (inclusive estrangeiros pelo BOSMAN.TXE/PLOP.TXE) e a tabela
    // de jogadores com os atributos que o jogo tira do nome.
    public class TeamEditorWindow : Window
    {
        private const string Colunas = "40,*,96,48,52,110,28,110";

        private readonly string _gameDir, _equipasDir;
        private readonly Action? _abrirOriginal;
        private readonly List<Pais> _paises;
        private readonly Dictionary<string, Pais> _porCodigo;
        private readonly Dictionary<string, Bitmap?> _bandeiras = new Dictionary<string, Bitmap?>();
        private RegrasEquipe _regras;
        private bool _liberado;

        private readonly List<EftTeam> _equipes = new List<EftTeam>();
        private readonly List<string> _ilegiveis = new List<string>();
        private EftTeam? _atual;
        private bool _alterado, _carregando;

        private readonly TextBox _busca = new TextBox();
        private readonly ComboBox _filtro = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly ListBox _listaEquipes = new ListBox { Background = Brushes.Transparent };
        private readonly TextBlock _contaEquipes = Texto("", 11, CinzaFaixa);
        private readonly TextBox _nomeCompleto = new TextBox { MaxLength = TeamCodec.MAX_NOME_COMPLETO };
        private readonly TextBox _nomeAbreviado = new TextBox { MaxLength = TeamCodec.MAX_NOME };
        private readonly TextBox _treinador = new TextBox { MaxLength = TeamCodec.MAX_NOME };
        private readonly ComboBox _pais = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly TextBlock _nivel = Texto("", 15, Branco, true);
        private readonly Button _nivelMenos, _nivelMais;
        private readonly Border _previa = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 6) };
        private readonly TextBlock _previaTxt = Texto("", 16, Branco, true);
        private readonly Border _amostraLetra = Amostra(), _amostraFundo = Amostra();
        private readonly TextBlock _hexLetra = Texto("", 12, Cinza), _hexFundo = Texto("", 12, Cinza);
        private readonly TextBlock _arquivo = Texto("", 11, CinzaFaixa);
        private readonly TextBlock _contagem = Texto("", 13, Branco);
        private readonly StackPanel _problemas = new StackPanel { Spacing = 2 };
        private readonly ListBox _jogadores = new ListBox { Background = Brushes.Transparent };
        private readonly Button _salvar, _adicionar, _editar, _remover, _transferir;
        private readonly Control _painelEquipe;

        public TeamEditorWindow(string gameDir, Action? abrirOriginal)
        {
            _gameDir = gameDir;
            _equipasDir = TeamCodec.Caminho(gameDir, "EQUIPAS");
            _abrirOriginal = abrirOriginal;
            _paises = TeamCodec.LerPaises(gameDir).OrderBy(p => p.Nome, StringComparer.CurrentCulture).ToList();
            _porCodigo = _paises.ToDictionary(p => p.Codigo);
            _regras = new RegrasEquipe(gameDir, _paises);
            _liberado = TeamCodec.BosmanLiberado(gameDir);

            Title = "Editor de Equipes";
            Width = 1500;
            Height = 900;
            MinWidth = 1000;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();
            Background = Verde;
            AplicarEstilos(this);
            CaberNaTela(this);

            // Barra do topo
            var nova = BotaoClaro("Nova equipe");
            nova.Click += async (_, _) => await NovaEquipe();
            _salvar = BotaoAmarelo("Salvar", 110);
            _salvar.Click += async (_, _) => await Salvar();
            var original = BotaoClaro("Editor original");
            original.Click += (_, _) => _abrirOriginal?.Invoke();
            original.IsVisible = abrirOriginal != null;
            var fechar = BotaoClaro("Fechar");
            fechar.Click += (_, _) => Close();
            var barra = new DockPanel { Background = VerdeTopo };
            var titulo = Texto("Editor de Equipes", 22, Amarelo, true);
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
                Children = { original, nova, _salvar, fechar },
            });

            // Esquerda: busca, filtro por pais e lista de equipes
            _busca.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Filtrar(); };
            _filtro.ItemTemplate = new FuncDataTemplate<FiltroPais>((f, _) => f == null ? new Panel() : LinhaComBandeira(f.Pais?.Codigo, f.Texto, Brushes.Black));
            _filtro.SelectionChanged += (_, _) => Filtrar();
            _listaEquipes.ItemTemplate = new FuncDataTemplate<EftTeam>((t, _) => t == null ? new Panel() : ItemEquipe(t));
            _listaEquipes.SelectionChanged += async (_, _) => await TrocouEquipe();
            var cartaoLista = NovoCartao("Equipes");
            var cl = (StackPanel)cartaoLista.Child!;
            cl.Children.Add(Rotulado("Buscar", _busca));
            cl.Children.Add(Rotulado("País", _filtro));
            cl.Children.Add(_contaEquipes);
            var esquerda = new DockPanel { Width = 330 };
            DockPanel.SetDock(cartaoLista, Dock.Top);
            esquerda.Children.Add(cartaoLista);
            esquerda.Children.Add(new Border
            {
                Background = Cartao,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(6),
                Margin = new Thickness(0, 10, 0, 0),
                Child = _listaEquipes,
            });

            // Dados da equipe
            foreach (var tb in new[] { _nomeCompleto, _nomeAbreviado, _treinador })
                tb.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) CamposMudaram(); };
            _pais.ItemTemplate = new FuncDataTemplate<Pais>((p, _) => p == null ? new Panel() : LinhaComBandeira(p.Codigo, p.Nome, Brushes.Black));
            _pais.ItemsSource = _paises;
            _pais.SelectionChanged += (_, _) => CamposMudaram();
            _nivelMenos = BotaoAmarelo("−", 36);
            _nivelMais = BotaoAmarelo("+", 36);
            _nivelMenos.Click += (_, _) => MudarNivel(-1);
            _nivelMais.Click += (_, _) => MudarNivel(1);
            var nivel = new DockPanel { Width = 150 };
            DockPanel.SetDock(_nivelMenos, Dock.Left);
            DockPanel.SetDock(_nivelMais, Dock.Right);
            nivel.Children.Add(_nivelMenos);
            nivel.Children.Add(_nivelMais);
            _nivel.HorizontalAlignment = HorizontalAlignment.Center;
            _nivel.VerticalAlignment = VerticalAlignment.Center;
            nivel.Children.Add(_nivel);
            _previa.Child = _previaTxt;
            _previaTxt.HorizontalAlignment = HorizontalAlignment.Center;
            var cores = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { BotaoCor("Letra", _amostraLetra, _hexLetra, true), BotaoCor("Fundo", _amostraFundo, _hexFundo, false) },
            };

            // Coluna do meio: rotulo em cima de cada campo, um por linha
            _pais.HorizontalAlignment = HorizontalAlignment.Stretch;
            nivel.HorizontalAlignment = HorizontalAlignment.Left;
            cores.HorizontalAlignment = HorizontalAlignment.Left;
            var dados = new StackPanel { Spacing = 8 };
            foreach (var (rotulo, faixa, campo) in new (string, string, Control)[]
            {
                ("Nome completo", "até 40 letras", _nomeCompleto),
                ("Nome abreviado", "até 20 letras", _nomeAbreviado),
                ("Treinador", "obrigatório", _treinador),
                ("País", "da equipe", _pais),
                ("Nível inicial", "20 = 1ª divisão no jogo novo", nivel),
                ("Cores", "letra e fundo", cores),
            })
            {
                var r = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                r.Children.Add(Texto(rotulo, 13, Cinza));
                var f = Texto(faixa, 11, CinzaFaixa);
                f.VerticalAlignment = VerticalAlignment.Bottom;
                r.Children.Add(f);
                dados.Children.Add(new StackPanel { Spacing = 3, Children = { r, campo } });
            }
            var cartaoEquipe = NovoCartao("Equipe");
            var ce = (StackPanel)cartaoEquipe.Child!;
            _arquivo.HorizontalAlignment = HorizontalAlignment.Right;
            ce.Children.Add(_previa);
            ce.Children.Add(_arquivo);
            ce.Children.Add(dados);

            // Situacao para o jogo
            var cartaoRegras = NovoCartao(_liberado ? "Regras do jogo (estrangeiros liberados)" : "Regras do jogo");
            var cr = (StackPanel)cartaoRegras.Child!;
            cr.Children.Add(_contagem);
            cr.Children.Add(_problemas);

            // Jogadores
            _jogadores.ItemTemplate = new FuncDataTemplate<LinhaJogador>((l, _) => l == null ? new Panel() : LinhaTabela(l));
            _jogadores.DoubleTapped += async (_, _) => await EditarJogador();
            _jogadores.KeyDown += async (_, e) =>
            {
                if (e.Key == Key.Enter) await EditarJogador();
                else if (e.Key == Key.Delete) await RemoverJogador();
                else if (e.Key == Key.Insert) await AdicionarJogador();
            };
            _jogadores.SelectionChanged += (_, _) => AtualizarBotoes();
            _adicionar = BotaoAmarelo("Adicionar", 110);
            _editar = BotaoClaro("Editar");
            _remover = BotaoClaro("Remover");
            _transferir = BotaoClaro("Transferir…");
            _adicionar.Click += async (_, _) => await AdicionarJogador();
            _editar.Click += async (_, _) => await EditarJogador();
            _remover.Click += async (_, _) => await RemoverJogador();
            _transferir.Click += async (_, _) => await TransferirJogador();
            var botoes = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0),
                Children = { _adicionar, _editar, _remover, _transferir, CentroV(Texto("Duplo-clique edita · Ins adiciona · Del remove", 11, CinzaFaixa)) },
            };
            var cabecalho = Grade(new[] { "Pos", "Nome", "País", "Nota", "Lesão", "Comport.", "✱", "Estrangeiro" }, true);
            cabecalho.Margin = new Thickness(8, 0, 8, 6);
            var grade = new DockPanel();
            DockPanel.SetDock(cabecalho, Dock.Top);
            grade.Children.Add(cabecalho);
            grade.Children.Add(_jogadores);
            var tabela = new DockPanel();
            DockPanel.SetDock(botoes, Dock.Bottom);
            tabela.Children.Add(botoes);
            tabela.Children.Add(RolagemLateral(grade, LarguraMinima(Colunas, 170, 6 * 8 + 40)));

            // Coluna do meio (dados e regras) e a tabela com a altura toda, para
            // caberem os 20 jogadores sem rolar
            _contagem.TextWrapping = TextWrapping.Wrap;
            var meio = new ScrollViewer
            {
                Width = 380,
                Margin = new Thickness(12, 0, 0, 0),
                Content = new StackPanel { Spacing = 10, Children = { cartaoEquipe, cartaoRegras } },
            };
            var tabelaCartao = new Border { Background = Cartao, CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = tabela, Margin = new Thickness(12, 0, 0, 0) };
            var direita = new DockPanel();
            DockPanel.SetDock(meio, Dock.Left);
            direita.Children.Add(meio);
            direita.Children.Add(tabelaCartao);
            _painelEquipe = direita;

            var corpo = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(esquerda, Dock.Left);
            corpo.Children.Add(esquerda);
            corpo.Children.Add(direita);
            var raiz = new DockPanel();
            DockPanel.SetDock(barra, Dock.Top);
            raiz.Children.Add(barra);
            raiz.Children.Add(corpo);
            Content = raiz;

            CarregarEquipes();
            MostrarEquipe();
            Closing += async (s, e) =>
            {
                if (!_alterado) return;
                e.Cancel = true;
                if (await Dialogos.Confirmar(this, "Há alterações não salvas nesta equipe. Fechar mesmo assim?", "Fechar"))
                {
                    _alterado = false;
                    Close();
                }
            };
        }

        // ---- pecas ----

        private static Grid Rotulado(string rotulo, Control campo)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("60,*") };
            var r = Texto(rotulo, 13, Cinza);
            r.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(r);
            Grid.SetColumn(campo, 1);
            g.Children.Add(campo);
            return g;
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

        private Control LinhaComBandeira(string? codigo, string texto, IBrush cor)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var b = Bandeira(codigo);
            sp.Children.Add(b != null ? new Image { Source = b, Width = 24, Height = 16, Stretch = Stretch.Fill } : new Border { Width = 24 });
            sp.Children.Add(new TextBlock { Text = texto, Foreground = cor, VerticalAlignment = VerticalAlignment.Center });
            return sp;
        }

        private Control ItemEquipe(EftTeam t)
        {
            var b = Bandeira(t.Pais);
            var problemas = _regras.Validar(t).Count;
            var nome = Texto(t.NomeAbreviado.Length > 0 ? t.NomeAbreviado : "(sem nome)", 14, Branco, true);
            var info = Texto($"{t.NomeCompleto} · {t.Jogadores.Count} jog.", 11, Cinza);
            info.TextTrimming = TextTrimming.CharacterEllipsis;
            var textos = new StackPanel { Children = { nome, info } };
            var linha = new DockPanel { Margin = new Thickness(6, 4) };
            var img = b != null ? (Control)new Image { Source = b, Width = 30, Height = 20, Stretch = Stretch.Fill } : new Border { Width = 30 };
            img.Margin = new Thickness(0, 0, 8, 0);
            img.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(img, Dock.Left);
            linha.Children.Add(img);
            if (problemas > 0)
            {
                var aviso = Texto("⚠", 14, Alerta, true);
                aviso.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(aviso, Dock.Right);
                linha.Children.Add(aviso);
            }
            linha.Children.Add(textos);
            return linha;
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
                Padding = new Thickness(8, 4),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { amostra, new StackPanel { Children = { Texto(nome, 13, Branco, true), hex } } },
                },
            };
            b.PointerPressed += async (_, _) =>
            {
                if (_atual == null) return;
                var cor = await EscolherCor(this, letra ? "Cor da letra" : "Cor do fundo", letra ? _atual.CorLetra : _atual.CorFundo);
                if (cor == null) return;
                if (letra) _atual.CorLetra = cor.Value; else _atual.CorFundo = cor.Value;
                Alterou();
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
                tb.HorizontalAlignment = i == 1 || i == 2 || i == 5 || i == 7 ? HorizontalAlignment.Left
                    : i == 0 || i == 6 ? HorizontalAlignment.Center : HorizontalAlignment.Right;
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
            return g;
        }

        // Coluna "Estrangeiro": conta no limite de 5? (nacional fica em branco)
        private static string TextoEstrangeiro(string situacao) => situacao switch
        {
            "Estrangeiro" => "Sim",
            "Bosman" => "Não (Bosman)",
            "PLOP" => "Não (PLOP)",
            _ => "",
        };

        private sealed class LinhaJogador
        {
            public int Indice;
            public EftPlayer Jogador = null!;
        }

        private sealed class FiltroPais
        {
            public Pais? Pais;
            public string Texto = "";
        }

        private Control LinhaTabela(LinhaJogador l)
        {
            var j = l.Jogador;
            string pos = TeamCodec.PosicoesCurtas[Math.Max(0, Math.Min(3, j.Posicao))];
            string sit = _atual == null ? "" : _regras.Situacao(j.Pais, _atual.Pais);
            var g = Grade(new[] { "", j.Nome, "", j.Nota.ToString(), j.Lesao.ToString(),
                SaveCodec.ComportamentoLabels[j.Comportamento], j.Estrela ? "✱" : "", sit }, false);
            g.Children.Add(new Border
            {
                Background = CorPosicao(pos),
                CornerRadius = new CornerRadius(4),
                Width = 28,
                Padding = new Thickness(0, 1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock { Text = pos, Foreground = Branco, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
            });
            var pais = LinhaComBandeira(j.Pais, j.Pais, _porCodigo.ContainsKey(j.Pais) ? Branco : Alerta);
            pais.Margin = new Thickness(6, 0, 0, 0);
            pais.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(pais, 2);
            g.Children.Add(pais);
            ((TextBlock)g.Children[6]).Foreground = Ouro;
            ((TextBlock)g.Children[7]).Text = TextoEstrangeiro(sit);
            ((TextBlock)g.Children[7]).Foreground = sit == "Estrangeiro" ? Alerta : Ok;
            return new Border
            {
                Background = l.Indice % 2 == 0 ? LinhaPar : Brushes.Transparent,
                Padding = new Thickness(8, 4),
                Child = g,
            };
        }

        // ---- lista de equipes ----

        private void CarregarEquipes()
        {
            _equipes.Clear();
            _ilegiveis.Clear();
            if (Directory.Exists(_equipasDir))
                foreach (var f in Directory.GetFiles(_equipasDir).Where(f => f.EndsWith(".eft", StringComparison.OrdinalIgnoreCase)))
                {
                    try { _equipes.Add(TeamCodec.Read(f)); }
                    catch { _ilegiveis.Add(Path.GetFileName(f)); }
                }
            _equipes.Sort((a, b) => string.Compare(a.NomeAbreviado, b.NomeAbreviado, StringComparison.CurrentCultureIgnoreCase));
            MontarFiltro();
            Filtrar();
        }

        private void MontarFiltro()
        {
            var atual = (_filtro.SelectedItem as FiltroPais)?.Pais?.Codigo;
            var itens = new List<FiltroPais> { new FiltroPais { Texto = $"Todos ({_equipes.Count})" } };
            foreach (var g in _equipes.GroupBy(t => t.Pais)
                         .Select(g => new { Pais = _porCodigo.TryGetValue(g.Key, out var p) ? p : new Pais { Codigo = g.Key, Nome = g.Key }, N = g.Count() })
                         .OrderBy(x => x.Pais.Nome, StringComparer.CurrentCulture))
                itens.Add(new FiltroPais { Pais = g.Pais, Texto = $"{g.Pais.Nome} ({g.N})" });
            _filtro.ItemsSource = itens;
            int i = itens.FindIndex(f => f.Pais?.Codigo == atual);
            _filtro.SelectedIndex = Math.Max(0, i);
        }

        private void Filtrar()
        {
            var pais = (_filtro.SelectedItem as FiltroPais)?.Pais?.Codigo;
            var termo = (_busca.Text ?? "").Trim();
            var lista = _equipes.Where(t =>
                (pais == null || t.Pais == pais) &&
                (termo.Length == 0
                 || t.NomeAbreviado.IndexOf(termo, StringComparison.CurrentCultureIgnoreCase) >= 0
                 || t.NomeCompleto.IndexOf(termo, StringComparison.CurrentCultureIgnoreCase) >= 0
                 || Path.GetFileName(t.Arquivo).IndexOf(termo, StringComparison.CurrentCultureIgnoreCase) >= 0
                 || t.Jogadores.Any(j => j.Nome.IndexOf(termo, StringComparison.CurrentCultureIgnoreCase) >= 0))).ToList();
            _carregando = true;
            _listaEquipes.ItemsSource = lista;
            _listaEquipes.SelectedItem = _atual != null && lista.Contains(_atual) ? _atual : null;
            _carregando = false;
            _contaEquipes.Text = $"{lista.Count} de {_equipes.Count} equipes" +
                (termo.Length > 0 ? " (busca também por jogador)" : "") +
                (_ilegiveis.Count > 0 ? $" · {_ilegiveis.Count} arquivo(s) ilegível(is)" : "");
        }

        private async Task TrocouEquipe()
        {
            if (_carregando) return;
            if (_listaEquipes.SelectedItem is not EftTeam t || t == _atual) return;
            if (_alterado && !await Dialogos.Confirmar(this, "Há alterações não salvas nesta equipe. Descartar?", "Trocar de equipe"))
            {
                _carregando = true;
                _listaEquipes.SelectedItem = _atual;
                _carregando = false;
                return;
            }
            if (_alterado) Recarregar(_atual);
            _alterado = false;
            _atual = t;
            MostrarEquipe();
        }

        // Descarta alteracoes: relê a equipe do disco (ou tira a nova da lista)
        private void Recarregar(EftTeam? t)
        {
            if (t == null) return;
            int i = _equipes.IndexOf(t);
            if (i < 0) return;
            if (string.IsNullOrEmpty(t.Arquivo) || !File.Exists(t.Arquivo)) { _equipes.RemoveAt(i); return; }
            try { _equipes[i] = TeamCodec.Read(t.Arquivo); } catch { _equipes.RemoveAt(i); }
        }

        // ---- equipe ----

        private void MostrarEquipe()
        {
            _painelEquipe.IsEnabled = _atual != null;
            _salvar.IsEnabled = _atual != null;
            _carregando = true;
            var t = _atual;
            _nomeCompleto.Text = t?.NomeCompleto ?? "";
            _nomeAbreviado.Text = t?.NomeAbreviado ?? "";
            _treinador.Text = t?.Treinador ?? "";
            _pais.SelectedItem = t != null && _porCodigo.TryGetValue(t.Pais, out var p) ? p : null;
            _arquivo.Text = t == null ? "" : string.IsNullOrEmpty(t.Arquivo) ? "nova (ainda sem arquivo)" : "EQUIPAS/" + Path.GetFileName(t.Arquivo);
            _carregando = false;
            MostrarNivel();
            PintarCores();
            MostrarJogadores();
        }

        private void CamposMudaram()
        {
            if (_carregando || _atual == null) return;
            _atual.NomeCompleto = (_nomeCompleto.Text ?? "").ToUpperInvariant();
            _atual.NomeAbreviado = (_nomeAbreviado.Text ?? "").ToUpperInvariant();
            _atual.Treinador = _treinador.Text ?? "";
            if (_pais.SelectedItem is Pais p) _atual.Pais = p.Codigo;
            Alterou();
            PintarCores();
            MostrarJogadores();
        }

        private void Alterou()
        {
            _alterado = true;
            Title = "Editor de Equipes — alterações não salvas";
        }

        private void MudarNivel(int passo)
        {
            if (_atual == null) return;
            _atual.Nivel = Math.Max(TeamCodec.NIVEL_MIN, Math.Min(TeamCodec.NIVEL_MAX, _atual.Nivel + passo));
            Alterou();
            MostrarNivel();
            MostrarRegras();
        }

        private void MostrarNivel()
        {
            _nivel.Text = _atual?.Nivel.ToString() ?? "—";
            _nivelMenos.IsEnabled = _atual != null && _atual.Nivel > TeamCodec.NIVEL_MIN;
            _nivelMais.IsEnabled = _atual != null && _atual.Nivel < TeamCodec.NIVEL_MAX;
        }

        private void PintarCores()
        {
            var t = _atual;
            _previaTxt.Text = t == null ? "" : t.NomeAbreviado.Length > 0 ? t.NomeAbreviado : "(sem nome)";
            if (t == null)
            {
                _previa.Background = Brushes.Transparent;
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

        private void MostrarJogadores(int selecionado = -1)
        {
            if (selecionado < 0) selecionado = _jogadores.SelectedIndex;
            _jogadores.ItemsSource = _atual?.Jogadores.Select((j, i) => new LinhaJogador { Indice = i, Jogador = j }).ToList();
            if (_atual != null && selecionado >= 0 && selecionado < _atual.Jogadores.Count) _jogadores.SelectedIndex = selecionado;
            MostrarRegras();
            AtualizarBotoes();
        }

        private void AtualizarBotoes()
        {
            bool sel = _atual != null && _jogadores.SelectedIndex >= 0;
            _adicionar.IsEnabled = _atual != null && _atual.Jogadores.Count < TeamCodec.MAX_JOGADORES;
            _editar.IsEnabled = _remover.IsEnabled = _transferir.IsEnabled = sel;
        }

        private void MostrarRegras()
        {
            _problemas.Children.Clear();
            var t = _atual;
            if (t == null) { _contagem.Text = ""; return; }
            int gr = t.Jogadores.Count(j => j.Posicao == 0), campo = t.Jogadores.Count - gr, est = _regras.Estrangeiros(t);
            _contagem.Text = $"Jogadores {t.Jogadores.Count} (14 a 20)\nGuarda-redes {gr} (mín. 1) · Campo {campo} (mín. 10)\n" +
                (_liberado ? "Estrangeiros: sem limite (Liberado)" : $"Estrangeiros {est} de {TeamCodec.MAX_ESTRANGEIROS}");
            var erros = _regras.Validar(t);
            if (erros.Count == 0)
                _problemas.Children.Add(Texto("✓ Pronta para o jogo", 13, Ok, true));
            else
                foreach (var e in erros)
                {
                    var tb = Texto("✗ " + e, 13, Alerta);
                    tb.TextWrapping = TextWrapping.Wrap;
                    _problemas.Children.Add(tb);
                }
        }

        // ---- acoes ----

        private async Task NovaEquipe()
        {
            if (_alterado && !await Dialogos.Confirmar(this, "Há alterações não salvas nesta equipe. Descartar?", "Nova equipe")) return;
            if (_alterado) Recarregar(_atual);
            var paisFiltro = (_filtro.SelectedItem as FiltroPais)?.Pais?.Codigo;
            var t = new EftTeam { Pais = paisFiltro ?? "BRA", NomeCompleto = "NOVA EQUIPA", NomeAbreviado = "NOVA" };
            _equipes.Insert(0, t);
            _atual = t;
            _alterado = true;
            MontarFiltro();
            Filtrar();
            _carregando = true;
            _listaEquipes.SelectedItem = t;
            _carregando = false;
            MostrarEquipe();
            Alterou();
            _nomeCompleto.Focus();
            _nomeCompleto.SelectAll();
        }

        private async Task<bool> Salvar()
        {
            if (_atual == null) return false;
            var erros = _regras.Validar(_atual);
            if (erros.Count > 0)
            {
                await Dialogos.Mensagem(this, "O jogo não aceita a equipe assim:\n\n• " + string.Join("\n• ", erros), "Não dá para salvar");
                return false;
            }
            if (string.IsNullOrEmpty(_atual.Arquivo))
            {
                var nome = await PedirArquivo(SugerirArquivo(_atual.NomeAbreviado));
                if (nome == null) return false;
                _atual.Arquivo = Path.Combine(_equipasDir, nome + ".EFT");
            }
            try
            {
                TeamCodec.Write(_atual, _atual.Arquivo);
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, $"Erro ao gravar:\n{ex.Message}", "Erro");
                return false;
            }
            _alterado = false;
            Title = "Editor de Equipes";
            _equipes.Sort((a, b) => string.Compare(a.NomeAbreviado, b.NomeAbreviado, StringComparison.CurrentCultureIgnoreCase));
            MontarFiltro();
            Filtrar();
            MostrarEquipe();
            return true;
        }

        // Nome de arquivo de ate 8 letras (como no Editor de Equipas), sem repetir
        private string SugerirArquivo(string nome)
        {
            var b = new string(RemoverAcentos(nome).ToUpperInvariant().Where(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')).ToArray());
            if (b.Length == 0) b = "EQUIPA";
            if (b.Length > 8) b = b.Substring(0, 8);
            var s = b;
            for (int n = 2; ExisteArquivo(s); n++)
                s = b.Substring(0, Math.Min(b.Length, 8 - n.ToString().Length)) + n;
            return s;
        }

        private bool ExisteArquivo(string nome) =>
            Directory.Exists(_equipasDir) && Directory.GetFiles(_equipasDir).Any(f =>
                string.Equals(Path.GetFileNameWithoutExtension(f), nome, StringComparison.OrdinalIgnoreCase));

        private static string RemoverAcentos(string s)
        {
            var n = s.Normalize(System.Text.NormalizationForm.FormD);
            return new string(n.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        }

        private async Task<string?> PedirArquivo(string sugestao)
        {
            var w = Dialogo("Gravar equipe");
            var tb = new TextBox { Text = sugestao, MaxLength = 8 };
            var ok = new Button { Content = "Gravar", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelar = new Button { Content = "Cancelar", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancelar.Click += (_, _) => w.Close(null);
            ok.Click += async (_, _) =>
            {
                var n = (tb.Text ?? "").Trim().ToUpperInvariant();
                if (n.Length == 0 || n.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')))
                {
                    await Dialogos.Mensagem(w, "Use até 8 letras sem acento, números ou _.", "Nome inválido");
                    return;
                }
                if (ExisteArquivo(n))
                {
                    await Dialogos.Mensagem(w, $"Já existe EQUIPAS/{n}.EFT. Escolha outro nome.", "Nome em uso");
                    return;
                }
                w.Close(n);
            };
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Width = 320,
                Children =
                {
                    new TextBlock { Text = "Nome do arquivo (até 8 letras), na pasta EQUIPAS:", TextWrapping = TextWrapping.Wrap },
                    tb,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } },
                },
            };
            w.Opened += (_, _) => { tb.Focus(); tb.SelectAll(); };
            return await w.ShowDialog<string?>(this);
        }

        // ---- jogadores ----

        private async Task AdicionarJogador()
        {
            if (_atual == null || _atual.Jogadores.Count >= TeamCodec.MAX_JOGADORES) return;
            var j = new EftPlayer { Pais = _atual.Pais, Posicao = 1 };
            if (!await FichaJogador(j, "Novo jogador")) return;
            int i = TeamCodec.InserirNaPosicao(_atual.Jogadores, j);
            Alterou();
            MostrarJogadores(i);
        }

        private async Task EditarJogador()
        {
            if (_atual == null || _jogadores.SelectedItem is not LinhaJogador l) return;
            var copia = new EftPlayer { Nome = l.Jogador.Nome, Pais = l.Jogador.Pais, Posicao = l.Jogador.Posicao };
            if (!await FichaJogador(copia, l.Jogador.Nome)) return;
            bool mudouPosicao = l.Jogador.Posicao != copia.Posicao;
            l.Jogador.Nome = copia.Nome;
            l.Jogador.Pais = copia.Pais;
            l.Jogador.Posicao = copia.Posicao;
            int indice = l.Indice;
            if (mudouPosicao)  // vai pro grupo da posicao nova
            {
                _atual.Jogadores.RemoveAt(l.Indice);
                indice = TeamCodec.InserirNaPosicao(_atual.Jogadores, l.Jogador);
            }
            Alterou();
            MostrarJogadores(indice);
        }

        private async Task RemoverJogador()
        {
            if (_atual == null || _jogadores.SelectedItem is not LinhaJogador l) return;
            if (!await Dialogos.Confirmar(this, $"Remover {l.Jogador.Nome} da equipe?", "Remover jogador")) return;
            _atual.Jogadores.RemoveAt(l.Indice);
            Alterou();
            MostrarJogadores(Math.Min(l.Indice, _atual.Jogadores.Count - 1));
        }

        // Passa o jogador para outra equipe: grava as duas na hora (a atual precisa
        // estar salva e as duas continuarem aceitas pelo jogo)
        private async Task TransferirJogador()
        {
            if (_atual == null || _jogadores.SelectedItem is not LinhaJogador l) return;
            if (_alterado || string.IsNullOrEmpty(_atual.Arquivo))
            {
                await Dialogos.Mensagem(this, "Salve a equipe atual antes de transferir.", "Transferir");
                return;
            }
            var destinos = _equipes.Where(t => t != _atual && !string.IsNullOrEmpty(t.Arquivo)).ToList();
            var w = Dialogo($"Transferir {l.Jogador.Nome}");
            var combo = new ComboBox
            {
                ItemsSource = destinos,
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemTemplate = new FuncDataTemplate<EftTeam>((t, _) => t == null ? new Panel() :
                    LinhaComBandeira(t.Pais, $"{t.NomeAbreviado}  ({t.Jogadores.Count} jog.)", Brushes.Black)),
            };
            var ok = new Button { Content = "Transferir", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelar = new Button { Content = "Cancelar", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (_, _) => w.Close(true);
            cancelar.Click += (_, _) => w.Close(false);
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 10,
                Width = 420,
                Children =
                {
                    new TextBlock { Text = "Equipe de destino:" },
                    combo,
                    new TextBlock { Text = "As duas equipes são gravadas na hora.", Foreground = Brushes.Gray, FontSize = 12 },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } },
                },
            };
            if (!await w.ShowDialog<bool>(this) || combo.SelectedItem is not EftTeam destino) return;

            var origemNova = TeamCodec.Read(_atual.Arquivo);
            origemNova.Jogadores.RemoveAt(l.Indice);
            var destinoNovo = TeamCodec.Read(destino.Arquivo);
            TeamCodec.InserirNaPosicao(destinoNovo.Jogadores, new EftPlayer { Nome = l.Jogador.Nome, Pais = l.Jogador.Pais, Posicao = l.Jogador.Posicao });
            var erros = _regras.Validar(origemNova).Select(e => $"{_atual.NomeAbreviado}: {e}")
                .Concat(_regras.Validar(destinoNovo).Select(e => $"{destino.NomeAbreviado}: {e}")).ToList();
            if (erros.Count > 0)
            {
                await Dialogos.Mensagem(this, "Depois da transferência o jogo não aceitaria:\n\n• " + string.Join("\n• ", erros), "Não dá para transferir");
                return;
            }
            TeamCodec.Write(origemNova, origemNova.Arquivo);
            TeamCodec.Write(destinoNovo, destinoNovo.Arquivo);
            _equipes[_equipes.IndexOf(_atual)] = origemNova;
            _equipes[_equipes.IndexOf(destino)] = destinoNovo;
            _atual = origemNova;
            Filtrar();
            MostrarEquipe();
            await Dialogos.Mensagem(this, $"{l.Jogador.Nome} agora joga no {destinoNovo.NomeAbreviado}.", "Transferido");
        }

        // Ficha do jogador: nome, posicao, pais e o que o jogo tira do nome, ao vivo
        private async Task<bool> FichaJogador(EftPlayer j, string titulo)
        {
            var w = Dialogo(titulo);
            var nome = new TextBox { Text = j.Nome, MaxLength = TeamCodec.MAX_NOME };
            var pos = new ComboBox { ItemsSource = TeamCodec.Posicoes, SelectedIndex = j.Posicao, HorizontalAlignment = HorizontalAlignment.Stretch };
            var pais = new ComboBox
            {
                ItemsSource = _paises,
                SelectedItem = _porCodigo.TryGetValue(j.Pais, out var p) ? p : null,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                ItemTemplate = new FuncDataTemplate<Pais>((x, _) => x == null ? new Panel() : LinhaComBandeira(x.Codigo, x.Nome, Brushes.Black)),
            };
            var nota = new TextBlock { FontSize = 22, FontWeight = FontWeight.Bold };
            var lesao = new TextBlock { FontSize = 22, FontWeight = FontWeight.Bold };
            var comp = new TextBlock { FontSize = 15, FontWeight = FontWeight.Bold };
            var estrela = new TextBlock { FontSize = 15, FontWeight = FontWeight.Bold };
            var situacao = new TextBlock { TextWrapping = TextWrapping.Wrap };
            Control Caixa(string rotulo, TextBlock valor) => new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6),
                Child = new StackPanel { Children = { new TextBlock { Text = rotulo, FontSize = 11, Foreground = Brushes.Gray }, valor } },
            };
            void Atualizar()
            {
                var t = new EftPlayer { Nome = nome.Text ?? "", Posicao = Math.Max(0, pos.SelectedIndex), Pais = (pais.SelectedItem as Pais)?.Codigo ?? "" };
                bool vazio = t.Nome.Trim().Length == 0;
                nota.Text = vazio ? "—" : t.Nota.ToString();
                lesao.Text = vazio ? "—" : t.Lesao.ToString();
                comp.Text = vazio ? "—" : SaveCodec.ComportamentoLabels[t.Comportamento];
                estrela.Text = vazio ? "—" : t.Estrela ? "✱ Estrela" : "Sem estrela";
                estrela.Foreground = t.Estrela && !vazio ? Cor(0xC79A00) : Brushes.Gray;
                var s = _atual == null ? "" : _regras.Situacao(t.Pais, _atual.Pais);
                situacao.Text = s switch
                {
                    "" => "Nacional.",
                    "Bosman" => "Não conta como estrangeiro: os dois países estão no BOSMAN.TXE (Lei Bosman).",
                    "PLOP" => "Não conta como estrangeiro: os dois países estão no PLOP.TXE (língua portuguesa).",
                    _ => $"Estrangeiro: conta no limite de {TeamCodec.MAX_ESTRANGEIROS} por equipe.",
                };
                situacao.Foreground = s == "Estrangeiro" ? Cor(0xC62828) : Cor(0x2E7D32);
            }
            nome.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Atualizar(); };
            pos.SelectionChanged += (_, _) => Atualizar();
            pais.SelectionChanged += (_, _) => Atualizar();
            Atualizar();

            var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelar = new Button { Content = "Cancelar", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            cancelar.Click += (_, _) => w.Close(false);
            ok.Click += async (_, _) =>
            {
                var n = (nome.Text ?? "").Trim();
                if (n.Length == 0) { await Dialogos.Mensagem(w, "Escreva o nome do jogador.", "Nome"); return; }
                // O Editor de Equipas nao aceita numeros nos nomes
                if (n.Any(char.IsDigit) || n.Any(c => c > 0xFF))
                {
                    await Dialogos.Mensagem(w, "Use só letras (com ou sem acento), espaço, ponto, hífen ou apóstrofo.", "Nome");
                    return;
                }
                if (pais.SelectedItem is not Pais pp) { await Dialogos.Mensagem(w, "Escolha o país do jogador.", "País"); return; }
                j.Nome = n;
                j.Pais = pp.Codigo;
                j.Posicao = pos.SelectedIndex;
                w.Close(true);
            };

            var grade = new Grid { ColumnDefinitions = new ColumnDefinitions("90,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto") };
            void Linha(string r, Control c, int i)
            {
                var t = new TextBlock { Text = r, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(t, i);
                Grid.SetRow(c, i);
                Grid.SetColumn(c, 1);
                c.Margin = new Thickness(0, 4);
                grade.Children.Add(t);
                grade.Children.Add(c);
            }
            Linha("Nome", nome, 0);
            Linha("Posição", pos, 1);
            Linha("País", pais, 2);
            var atributos = new UniformGrid { Columns = 4, Children = { Caixa("Nota", nota), Caixa("Lesão", lesao), Caixa("Comportamento", comp), Caixa("Estrela", estrela) } };
            foreach (var c in atributos.Children) ((Control)c).Margin = new Thickness(0, 0, 6, 0);
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Width = 520,
                Children =
                {
                    grade,
                    new TextBlock { Text = "O jogo tira estes valores do nome (e da posição):", FontSize = 12, Foreground = Brushes.Gray },
                    atributos,
                    situacao,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } },
                },
            };
            w.Opened += (_, _) => { nome.Focus(); nome.SelectAll(); };
            return await w.ShowDialog<bool>(this);
        }
    }
}
