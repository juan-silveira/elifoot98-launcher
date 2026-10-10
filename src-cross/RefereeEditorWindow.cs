using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using static ElifootLauncher.Visual;

namespace ElifootLauncher
{
    // Editor de Arbitros (REFEREE.TXE) no visual verde dos outros editores.
    // Esquerda: a lista com bandeira. Direita: o arbitro escolhido, com o pais
    // escolhido entre os 217 do COUNTRY.TXE. "Original" volta a lista que veio
    // com o jogo (so grava ao Salvar).
    public class RefereeEditorWindow : Window
    {
        private const string Colunas = "44,34,52,*";

        private readonly string _refereeTxePath, _gameDir;
        private readonly List<Pais> _paises;
        private readonly Dictionary<string, Pais> _porCodigo = new Dictionary<string, Pais>();
        private readonly Dictionary<string, Bitmap?> _bandeiras = new Dictionary<string, Bitmap?>();
        private RefereeCodec.File _file = new RefereeCodec.File();
        private bool _alterado, _mostrando;
        private string _codigo = "";

        private readonly ListBox _lista = new ListBox { Background = Brushes.Transparent };
        private readonly TextBlock _contagem = Texto("", 12, Cinza);
        private readonly Border _paisEscolhido = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 6), MinHeight = 34 };
        private readonly TextBox _busca = new TextBox { PlaceholderText = "Buscar país ou código" };
        private readonly ListBox _listaPaises = new ListBox { Background = Brushes.White, Height = 210 };
        private readonly TextBox _nome = new TextBox { MaxLength = 251 };
        private readonly TextBlock _status = Texto("", 12, Cinza);
        private readonly Button _salvar, _atualizar, _remover, _subir, _descer;

        private sealed class LinhaArbitro
        {
            public int Indice;
            public RefereeCodec.Record Arbitro = null!;
        }

        public RefereeEditorWindow(string refereeTxePath)
        {
            _refereeTxePath = refereeTxePath;
            _gameDir = Path.GetDirectoryName(refereeTxePath) ?? ".";
            try { _paises = TeamCodec.LerPaises(_gameDir); }
            catch { _paises = new List<Pais>(); }
            _paises = _paises.OrderBy(p => SemAcento(p.Nome), StringComparer.Ordinal).ToList();
            foreach (var p in _paises) _porCodigo[p.Codigo] = p;

            Title = "Editor de Árbitros";
            Width = 960;
            Height = 700;
            MinWidth = 700;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();
            Background = Verde;
            AplicarEstilos(this);
            CaberNaTela(this);

            // Barra do topo: titulo, Original, Salvar e Fechar
            var original = BotaoClaro("Original");
            original.Click += async (_, _) => await VoltarOriginal();
            _salvar = BotaoAmarelo("Salvar", 110);
            _salvar.Click += async (_, _) => await Salvar();
            var fechar = BotaoClaro("Fechar");
            fechar.Click += (_, _) => Close();
            var barra = new DockPanel { Background = VerdeTopo };
            var titulo = Texto("Editor de Árbitros", 22, Amarelo, true);
            titulo.Margin = new Thickness(16, 10);
            titulo.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(titulo, Dock.Left);
            barra.Children.Add(titulo);
            barra.Children.Add(new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                ItemSpacing = 8,
                LineSpacing = 6,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 8),
                Children = { original, _salvar, fechar },
            });

            // Esquerda: a lista de arbitros (zebrada pelo estilo, para a selecao aparecer em todas as linhas)
            CorDasLinhas(_lista, (0x114A11, 0x1B5E20, 0x2E7D32), true);
            CorDasLinhas(_listaPaises, (0xFFFFFF, 0xE8F5E9, 0xC8E6C9), false);
            _lista.ItemTemplate = new FuncDataTemplate<LinhaArbitro>((l, _) => l == null ? new Panel() : LinhaTabela(l));
            _lista.SelectionChanged += (_, _) => MostrarSelecionado();
            _lista.KeyDown += async (_, e) =>
            {
                if (e.Key == Key.Delete) await Remover();
            };
            var cabecalho = Grade(new[] { "#", "", "País", "Nome" }, true);
            cabecalho.Margin = new Thickness(8, 0, 8, 6);
            var topoLista = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var tituloLista = Texto("ÁRBITROS", 12, Amarelo, true);
            tituloLista.LetterSpacing = 1;
            tituloLista.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(tituloLista, Dock.Left);
            topoLista.Children.Add(tituloLista);
            _contagem.HorizontalAlignment = HorizontalAlignment.Right;
            _contagem.VerticalAlignment = VerticalAlignment.Center;
            topoLista.Children.Add(_contagem);
            var grade = new DockPanel();
            DockPanel.SetDock(topoLista, Dock.Top);
            DockPanel.SetDock(cabecalho, Dock.Top);
            grade.Children.Add(topoLista);
            grade.Children.Add(cabecalho);
            grade.Children.Add(_lista);
            var cartaoLista = new Border { Background = Cartao, CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10), Child = grade };

            // Direita: o arbitro escolhido
            _listaPaises.ItemTemplate = new FuncDataTemplate<Pais>((p, _) => p == null ? new Panel() : LinhaComBandeira(p.Codigo, p.Codigo, p.Nome, Brushes.Black));
            _listaPaises.ItemsSource = _paises;
            _listaPaises.SelectionChanged += (_, _) =>
            {
                if (_mostrando || _listaPaises.SelectedItem is not Pais p) return;
                _codigo = p.Codigo;
                MostrarPais();
            };
            _busca.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) FiltrarPaises(); };

            var adicionar = BotaoAmarelo("Adicionar", 110);
            adicionar.Click += async (_, _) => await Adicionar();
            _atualizar = BotaoClaro("Atualizar");
            _atualizar.Click += async (_, _) => await Atualizar();
            _remover = BotaoClaro("Remover");
            _remover.Click += async (_, _) => await Remover();
            _subir = BotaoClaro("↑ Subir");
            _subir.Click += (_, _) => Mover(-1);
            _descer = BotaoClaro("↓ Descer");
            _descer.Click += (_, _) => Mover(+1);
            _status.TextWrapping = TextWrapping.Wrap;

            var cartaoArbitro = NovoCartao("Árbitro");
            var ca = (StackPanel)cartaoArbitro.Child!;
            ca.Children.Add(Rotulo("País", "escolha na lista do jogo"));
            ca.Children.Add(_paisEscolhido);
            ca.Children.Add(_busca);
            ca.Children.Add(_listaPaises);
            ca.Children.Add(Rotulo("Nome", "como aparece no jogo"));
            ca.Children.Add(_nome);
            ca.Children.Add(new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Margin = new Thickness(0, 6, 0, 0), Children = { adicionar, _atualizar, _remover } });
            ca.Children.Add(new WrapPanel { ItemSpacing = 8, LineSpacing = 8, Children = { _subir, _descer } });
            ca.Children.Add(Texto("Adicionar entra logo abaixo do árbitro selecionado.", 11, CinzaFaixa));
            ca.Children.Add(_status);
            var direita = new ScrollViewer { Width = 360, Margin = new Thickness(12, 0, 0, 0), Content = cartaoArbitro };

            var corpo = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(direita, Dock.Right);
            corpo.Children.Add(direita);
            corpo.Children.Add(cartaoLista);
            var raiz = new DockPanel();
            DockPanel.SetDock(barra, Dock.Top);
            raiz.Children.Add(barra);
            raiz.Children.Add(corpo);
            Content = raiz;

            Carregar();
            Closing += async (s, e) =>
            {
                if (!_alterado) return;
                e.Cancel = true;
                if (await Dialogos.Confirmar(this, "Há alterações não salvas nos árbitros. Fechar sem salvar?", "Fechar"))
                {
                    _alterado = false;
                    Close();
                }
            };
        }

        // ---- pecas ----

        private static Control Rotulo(string rotulo, string faixa)
        {
            var r = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
            r.Children.Add(Texto(rotulo, 13, Cinza));
            var f = Texto(faixa, 11, CinzaFaixa);
            f.VerticalAlignment = VerticalAlignment.Bottom;
            r.Children.Add(f);
            return r;
        }

        // Fundo das linhas: (par, mouse em cima, selecionada)
        private static void CorDasLinhas(ListBox lista, (int par, int sobre, int sel) cores, bool zebra)
        {
            Style Fundo(Func<Selector?, Selector> sel, int cor) =>
                new Style(x => sel(x).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
                {
                    Setters = { new Setter(ContentPresenter.BackgroundProperty, Cor(cor)) },
                };
            if (zebra) lista.Styles.Add(Fundo(x => x.OfType<ListBoxItem>().NthChild(2, 1), cores.par));
            lista.Styles.Add(Fundo(x => x.OfType<ListBoxItem>().Class(":pointerover"), cores.sobre));
            lista.Styles.Add(Fundo(x => x.OfType<ListBoxItem>().Class(":selected"), cores.sel));
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
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
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

        private Control Imagem(string? codigo)
        {
            var b = Bandeira(codigo);
            return b != null ? new Image { Source = b, Width = 24, Height = 16, Stretch = Stretch.Fill, VerticalAlignment = VerticalAlignment.Center } : new Border { Width = 24 };
        }

        // Bandeira, codigo e nome do pais
        private Control LinhaComBandeira(string? codigo, string cod, string nome, IBrush cor)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sp.Children.Add(Imagem(codigo));
            sp.Children.Add(new TextBlock { Text = cod, Width = 34, FontWeight = FontWeight.Bold, Foreground = cor, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new TextBlock { Text = nome, Foreground = cor, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            return sp;
        }

        private Control LinhaTabela(LinhaArbitro l)
        {
            var a = l.Arbitro;
            bool conhecido = _porCodigo.ContainsKey(a.CountryCode);
            var g = Grade(new[] { (l.Indice + 1) + ".", "", a.CountryCode, a.Name }, false);
            ((TextBlock)g.Children[0]).Foreground = Cinza;
            ((TextBlock)g.Children[2]).Foreground = conhecido ? Amarelo : Alerta;
            ((TextBlock)g.Children[2]).FontWeight = FontWeight.Bold;
            var img = Imagem(a.CountryCode);
            img.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(img, 1);
            g.Children.Add(img);
            if (conhecido) ToolTip.SetTip(g, _porCodigo[a.CountryCode].Nome);
            return new Border { Padding = new Thickness(8, 5), Child = g };
        }

        private static string SemAcento(string s)
        {
            var n = s.Normalize(System.Text.NormalizationForm.FormD);
            return new string(n.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray())
                .ToLowerInvariant();
        }

        // ---- estado ----

        private void Alterou(bool sim)
        {
            _alterado = sim;
            Title = sim ? "Editor de Árbitros — alterações não salvas" : "Editor de Árbitros";
            _salvar.Content = sim ? "Salvar •" : "Salvar";
            _contagem.Text = _file.Records.Count + " árbitros" + (sim ? " · alterações não salvas" : "");
        }

        private int Selecionado => _lista.SelectedIndex;

        private void MostrarLista(int selecionar)
        {
            var itens = new List<LinhaArbitro>();
            for (int i = 0; i < _file.Records.Count; i++) itens.Add(new LinhaArbitro { Indice = i, Arbitro = _file.Records[i] });
            _lista.ItemsSource = itens;
            _lista.SelectedIndex = selecionar;
            if (selecionar >= 0) _lista.ScrollIntoView(selecionar);
            MostrarSelecionado();
        }

        // Campos da direita com o arbitro selecionado (o pais fica escolhido na lista)
        private void MostrarSelecionado()
        {
            int i = Selecionado;
            bool tem = i >= 0 && i < _file.Records.Count;
            _atualizar.IsEnabled = _remover.IsEnabled = tem;
            _subir.IsEnabled = tem && i > 0;
            _descer.IsEnabled = tem && i < _file.Records.Count - 1;
            if (!tem) { MostrarPais(); return; }
            var a = _file.Records[i];
            _codigo = a.CountryCode;
            _nome.Text = a.Name;
            _mostrando = true;
            if (!string.IsNullOrEmpty(_busca.Text)) _busca.Text = "";
            _mostrando = false;
            MostrarPais();
        }

        private void MostrarPais()
        {
            _porCodigo.TryGetValue(_codigo, out var p);
            _paisEscolhido.Child = _codigo.Length == 0
                ? new TextBlock { Text = "Escolha o país na lista abaixo", Foreground = Brushes.Gray }
                : LinhaComBandeira(_codigo, _codigo, p?.Nome ?? "(fora da lista do jogo)", p != null ? Brushes.Black : Brushes.DarkRed);
            _mostrando = true;
            _listaPaises.SelectedItem = p;
            if (p != null) _listaPaises.ScrollIntoView(p);
            _mostrando = false;
        }

        private void FiltrarPaises()
        {
            var q = SemAcento((_busca.Text ?? "").Trim());
            _mostrando = true;
            _listaPaises.ItemsSource = q.Length == 0 ? _paises
                : _paises.Where(p => SemAcento(p.Nome).Contains(q) || SemAcento(p.Codigo).StartsWith(q)).ToList();
            _mostrando = false;
            MostrarPais();
        }

        // ---- acoes ----

        private void Carregar()
        {
            try
            {
                if (!File.Exists(_refereeTxePath))
                {
                    _status.Text = "REFEREE.TXE não encontrado.";
                    return;
                }
                _file = RefereeCodec.Read(_refereeTxePath);
                MostrarLista(-1);
                Alterou(false);
            }
            catch (Exception ex)
            {
                _status.Text = "Erro ao ler REFEREE.TXE: " + ex.Message;
            }
        }

        // Pais e nome dos campos; null (com aviso) se faltar algo
        private async Task<(string pais, string nome)?> Campos()
        {
            var nome = (_nome.Text ?? "").Trim();
            if (_codigo.Length != 3) { await Dialogos.Mensagem(this, "Escolha o país do árbitro.", "Árbitro"); return null; }
            if (nome.Length == 0) { await Dialogos.Mensagem(this, "Digite o nome do árbitro.", "Árbitro"); return null; }
            if (nome.Any(c => c > 0xFF)) { await Dialogos.Mensagem(this, "O nome tem letras que o jogo não mostra.", "Árbitro"); return null; }
            return (_codigo, nome);
        }

        private async Task Adicionar()
        {
            var c = await Campos();
            if (c == null) return;
            int em = Selecionado >= 0 ? Selecionado + 1 : _file.Records.Count;
            _file.Records.Insert(em, new RefereeCodec.Record { CountryCode = c.Value.pais, Name = c.Value.nome });
            MostrarLista(em);
            Alterou(true);
            _status.Text = $"Adicionado na posição {em + 1}.";
        }

        private async Task Atualizar()
        {
            int i = Selecionado;
            if (i < 0 || i >= _file.Records.Count) return;
            var c = await Campos();
            if (c == null) return;
            _file.Records[i].CountryCode = c.Value.pais;
            _file.Records[i].Name = c.Value.nome;
            MostrarLista(i);
            Alterou(true);
            _status.Text = $"Árbitro {i + 1} atualizado.";
        }

        private async Task Remover()
        {
            int i = Selecionado;
            if (i < 0 || i >= _file.Records.Count) return;
            var a = _file.Records[i];
            if (!await Dialogos.Confirmar(this, $"Remover {a.Name} ({a.CountryCode}) da lista?", "Remover árbitro")) return;
            _file.Records.RemoveAt(i);
            MostrarLista(Math.Min(i, _file.Records.Count - 1));
            Alterou(true);
            _status.Text = $"{a.Name} removido.";
        }

        private void Mover(int direcao)
        {
            int i = Selecionado, j = i + direcao;
            if (i < 0 || j < 0 || j >= _file.Records.Count) return;
            (_file.Records[i], _file.Records[j]) = (_file.Records[j], _file.Records[i]);
            MostrarLista(j);
            Alterou(true);
        }

        // A lista que veio com o jogo; so vai pro disco ao Salvar
        private async Task VoltarOriginal()
        {
            if (!await Dialogos.Confirmar(this,
                    "Voltar à lista de árbitros original do jogo? As mudanças só são gravadas quando você clicar em Salvar.",
                    "Árbitros originais"))
                return;
            try
            {
                _file = RefereeCodec.Original();
                MostrarLista(-1);
                Alterou(true);
                _status.Text = $"Lista original ({_file.Records.Count} árbitros). Clique em Salvar para gravar.";
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, "Não consegui ler o original: " + ex.Message, "Erro");
            }
        }

        private async Task Salvar()
        {
            try
            {
                var backup = _refereeTxePath + ".bak";
                if (!File.Exists(backup) && File.Exists(_refereeTxePath))
                    File.Copy(_refereeTxePath, backup);
                RefereeCodec.Write(_file, _refereeTxePath);
                Alterou(false);
                _status.Text = "Salvo. Backup do anterior em REFEREE.TXE.bak";
                await Dialogos.Mensagem(this,
                    $"REFEREE.TXE salvo com {_file.Records.Count} árbitros.\n" +
                    $"Backup do anterior em: {backup}\n\n" +
                    "Da próxima vez que abrir o Elifoot, os árbitros novos aparecem.",
                    "Salvo");
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, "Erro ao salvar: " + ex.Message, "Erro");
            }
        }
    }
}
