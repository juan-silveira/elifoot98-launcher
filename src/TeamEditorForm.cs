using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Editor de Equipes nativo (substitui o EDITEQ.EXE), com o visual do jogo.
    // Esquerda: busca e filtro por pais das equipes de EQUIPAS. Direita: dados da
    // equipe (um por linha), regras do jogo (inclusive estrangeiros pelo
    // BOSMAN.TXE/PLOP.TXE) e a tabela de jogadores com os atributos que o jogo
    // tira do nome.
    public class TeamEditorForm : Form
    {
        private static readonly Color Verde = Color.FromArgb(0x0B, 0x3D, 0x0B), VerdeTopo = Color.FromArgb(0x06, 0x26, 0x06),
            Cartao = Color.FromArgb(0x14, 0x52, 0x14), LinhaPar = Color.FromArgb(0x11, 0x4A, 0x11), Selecao = Color.FromArgb(0x2E, 0x7D, 0x32),
            Amarelo = Color.FromArgb(0xFC, 0xFE, 0x04), Cinza = Color.FromArgb(0xB8, 0xC9, 0xB8), CinzaFaixa = Color.FromArgb(0x8F, 0xA8, 0x8F),
            Alerta = Color.FromArgb(0xFF, 0x8A, 0x80), Ok = Color.FromArgb(0x9C, 0xE2, 0x9C), Ouro = Color.FromArgb(0xFF, 0xD5, 0x4F);
        private static readonly int[] Paleta =
        {
            0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xC0C0C0,
            0x808080, 0xFF0000, 0x00FF00, 0xFFFF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFFFF,
        };
        private static readonly Font Fonte = new Font("Segoe UI", 9.5F), FonteNegrito = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            FontePequena = new Font("Segoe UI", 8F), FonteTitulo = new Font("Segoe UI", 16F, FontStyle.Bold),
            FonteCartao = new Font("Segoe UI", 8.5F, FontStyle.Bold), FonteLista = new Font("Segoe UI", 10F);
        private static readonly CultureInfo Br = new CultureInfo("pt-BR");

        private readonly string _gameDir, _equipasDir;
        private readonly Action? _abrirOriginal;
        private readonly List<Pais> _paises;
        private readonly Dictionary<string, Pais> _porCodigo;
        private readonly Dictionary<string, Image?> _bandeiras = new Dictionary<string, Image?>();
        private readonly RegrasEquipe _regras;
        private readonly bool _liberado;

        private readonly List<EftTeam> _equipes = new List<EftTeam>();
        private int _ilegiveis;
        private EftTeam? _atual;
        private bool _alterado, _carregando;

        private TextBox _busca = null!, _nomeCompleto = null!, _nomeAbreviado = null!, _treinador = null!;
        private ComboBox _filtro = null!, _pais = null!;
        private ListBox _listaEquipes = null!;
        private ListView _jogadores = null!;
        private Label _contaEquipes = null!, _nivel = null!, _previa = null!, _arquivo = null!, _contagem = null!, _problemas = null!;
        private Label _hexLetra = null!, _hexFundo = null!;
        private Panel _amostraLetra = null!, _amostraFundo = null!, _direita = null!;
        private Button _salvar = null!, _nivelMenos = null!, _nivelMais = null!, _adicionar = null!, _editar = null!, _remover = null!, _transferir = null!;

        public TeamEditorForm(string gameDir, Action? abrirOriginal)
        {
            _gameDir = gameDir;
            _equipasDir = TeamCodec.Caminho(gameDir, "EQUIPAS");
            _abrirOriginal = abrirOriginal;
            _paises = TeamCodec.LerPaises(gameDir).OrderBy(p => p.Nome, StringComparer.Create(Br, true)).ToList();
            _porCodigo = _paises.ToDictionary(p => p.Codigo);
            _regras = new RegrasEquipe(gameDir, _paises);
            _liberado = TeamCodec.BosmanLiberado(gameDir);

            Text = "Editor de Equipes";
            ClientSize = new Size(1500, 900);
            MinimumSize = new Size(1000, 560);
            Tela.CaberNaTela(this);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Verde;
            Font = Fonte;
            KeyPreview = true;

            BuildUi();
            CarregarEquipes();
            MostrarEquipe();
            FormClosing += (s, e) =>
            {
                if (_alterado && MessageBox.Show(this, "Há alterações não salvas nesta equipe. Fechar mesmo assim?", "Fechar",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    e.Cancel = true;
            };
        }

        // ---- pecas ----

        private static Label Texto(string t, Font f, Color cor) => new Label
        {
            Text = t,
            Font = f,
            ForeColor = cor,
            AutoSize = true,
            BackColor = Color.Transparent,
        };

        private static Button Botao(string texto, bool amarelo, int largura)
        {
            var b = new BotaoJogo
            {
                Text = texto,
                Width = largura,
                Height = 30,
                BackColor = amarelo ? Amarelo : Color.FromArgb(0xE6, 0xE6, 0xE6),
                ForeColor = Color.Black,
                Font = amarelo ? FonteNegrito : Fonte,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private static Panel NovoCartao(string titulo, DockStyle dock, int altura = 0)
        {
            var c = new Panel { Dock = dock, BackColor = Cartao, Padding = new Padding(12, 28, 12, 10) };
            if (altura > 0) c.Height = altura;
            var t = Texto(titulo.ToUpperInvariant(), FonteCartao, Amarelo);
            t.Location = new Point(12, 8);
            c.Controls.Add(t);
            return c;
        }

        // Campo com o rotulo (e a explicacao) em cima
        private static Panel CampoVertical(string rotulo, string faixa, Control campo, int largura = 0)
        {
            var p = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.Transparent };
            var r = Texto(rotulo, Fonte, Cinza);
            r.Location = new Point(0, 2);
            var f = Texto(faixa, FontePequena, CinzaFaixa);
            f.Location = new Point(r.PreferredWidth + 6, 5);
            campo.Location = new Point(0, 24);
            campo.Width = largura > 0 ? largura : 340;
            p.Controls.AddRange(new Control[] { r, f, campo });
            return p;
        }

        private Image? Bandeira(string? codigo)
        {
            if (string.IsNullOrEmpty(codigo)) return null;
            if (_bandeiras.TryGetValue(codigo!, out var b)) return b;
            try
            {
                var p = TeamCodec.Caminho(_gameDir, "FLAGS", codigo + ".BMP");
                b = File.Exists(p) ? Image.FromFile(p) : null;
            }
            catch { b = null; }
            _bandeiras[codigo!] = b;
            return b;
        }

        // Coluna "Estrangeiro": conta no limite de 5? (nacional fica em branco)
        private static string TextoEstrangeiro(string situacao) => situacao switch
        {
            "Estrangeiro" => "Sim",
            "Bosman" => "Não (Bosman)",
            "PLOP" => "Não (PLOP)",
            _ => "",
        };

        private static Color Rgb(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        private static Color CorPosicao(int pos) => pos switch
        {
            0 => Color.FromArgb(0xE0, 0xB0, 0x00),
            1 => Color.FromArgb(0x3D, 0x7B, 0xD9),
            2 => Color.FromArgb(0x2E, 0x9E, 0x4F),
            3 => Color.FromArgb(0xD9, 0x44, 0x3D),
            _ => Color.Gray,
        };

        // ComboBox de paises (ou do filtro) com bandeira
        private ComboBox ComboBandeiras()
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                MaxDropDownItems = 16,
                Font = Fonte,
            };
            c.DrawItem += (s, e) =>
            {
                e.DrawBackground();
                if (e.Index < 0) return;
                var item = c.Items[e.Index];
                string? codigo = item is Pais p ? p.Codigo : item is ItemFiltro f ? f.Codigo : null;
                string texto = item.ToString() ?? "";
                var img = Bandeira(codigo);
                if (img != null) e.Graphics.DrawImage(img, e.Bounds.X + 4, e.Bounds.Y + 3, 24, 16);
                var cor = (e.State & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : Color.Black;
                TextRenderer.DrawText(e.Graphics, texto, Fonte, new Rectangle(e.Bounds.X + 34, e.Bounds.Y, e.Bounds.Width - 36, e.Bounds.Height),
                    cor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            return c;
        }

        private sealed class ItemFiltro
        {
            public string? Codigo;
            public string Texto = "";
            public override string ToString() => Texto;
        }

        // ---- montagem ----

        private void BuildUi()
        {
            // Barra do topo
            var barra = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = VerdeTopo };
            var titulo = Texto("Editor de Equipes", FonteTitulo, Amarelo);
            titulo.Location = new Point(14, 10);
            barra.Controls.Add(titulo);
            var botoesTopo = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 11, 10, 0), BackColor = Color.Transparent };
            var original = Botao("Editor original", false, 120);
            original.Click += (_, _) => _abrirOriginal?.Invoke();
            original.Visible = _abrirOriginal != null;
            var nova = Botao("Nova equipe", false, 110);
            nova.Click += (_, _) => NovaEquipe();
            _salvar = Botao("Salvar", true, 110);
            _salvar.Click += (_, _) => Salvar();
            var fechar = Botao("Fechar", false, 90);
            fechar.Click += (_, _) => Close();
            foreach (var b in new[] { original, nova, _salvar, fechar }) { b.Margin = new Padding(6, 0, 0, 0); botoesTopo.Controls.Add(b); }
            barra.Controls.Add(botoesTopo);

            // Esquerda: busca, filtro e lista
            var esquerda = new Panel { Dock = DockStyle.Left, Width = 330, Padding = new Padding(12, 12, 6, 12), BackColor = Verde };
            var cartaoBusca = NovoCartao("Equipes", DockStyle.Top, 128);
            _busca = new TextBox { Location = new Point(70, 30), Width = 236 };
            _busca.TextChanged += (_, _) => Filtrar();
            _filtro = ComboBandeiras();
            _filtro.Location = new Point(70, 62);
            _filtro.Width = 236;
            _filtro.SelectedIndexChanged += (_, _) => { if (!_carregando) Filtrar(); };
            var rb = Texto("Buscar", Fonte, Cinza);
            rb.Location = new Point(12, 33);
            var rp = Texto("País", Fonte, Cinza);
            rp.Location = new Point(12, 65);
            _contaEquipes = Texto("", FontePequena, CinzaFaixa);
            _contaEquipes.Location = new Point(12, 98);
            cartaoBusca.Controls.AddRange(new Control[] { rb, _busca, rp, _filtro, _contaEquipes });
            _listaEquipes = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 44,
                BorderStyle = BorderStyle.None,
                BackColor = Cartao,
                IntegralHeight = false,
            };
            _listaEquipes.DrawItem += DesenharEquipe;
            _listaEquipes.SelectedIndexChanged += (_, _) => TrocouEquipe();
            var molduraLista = new Panel { Dock = DockStyle.Fill, BackColor = Cartao, Padding = new Padding(4) };
            molduraLista.Controls.Add(_listaEquipes);
            var espaco = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Verde };
            esquerda.Controls.Add(molduraLista);
            esquerda.Controls.Add(espaco);
            esquerda.Controls.Add(cartaoBusca);

            // Direita
            _direita = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 12, 12, 12), BackColor = Verde };

            var cartaoEquipe = NovoCartao("Equipe", DockStyle.Top, 470);
            _previa = new Label { Dock = DockStyle.Top, Height = 34, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12F, FontStyle.Bold), BorderStyle = BorderStyle.FixedSingle };
            _arquivo = Texto("", FontePequena, CinzaFaixa);
            _arquivo.Dock = DockStyle.Top;
            _arquivo.AutoSize = false;
            _arquivo.Height = 18;
            _arquivo.TextAlign = ContentAlignment.MiddleRight;
            _nomeCompleto = new TextBox { MaxLength = TeamCodec.MAX_NOME_COMPLETO, CharacterCasing = CharacterCasing.Upper };
            _nomeAbreviado = new TextBox { MaxLength = TeamCodec.MAX_NOME, CharacterCasing = CharacterCasing.Upper };
            _treinador = new TextBox { MaxLength = TeamCodec.MAX_NOME };
            foreach (var tb in new[] { _nomeCompleto, _nomeAbreviado, _treinador }) tb.TextChanged += (_, _) => CamposMudaram();
            _pais = ComboBandeiras();
            foreach (var p in _paises) _pais.Items.Add(p);
            _pais.SelectedIndexChanged += (_, _) => CamposMudaram();
            _nivelMenos = Botao("−", true, 34);
            _nivelMais = Botao("+", true, 34);
            _nivelMenos.Click += (_, _) => MudarNivel(-1);
            _nivelMais.Click += (_, _) => MudarNivel(1);
            _nivel = new Label { Size = new Size(50, 30), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            var nivel = new FlowLayoutPanel { Size = new Size(130, 32), WrapContents = false, BackColor = Color.Transparent };
            foreach (Control c in new Control[] { _nivelMenos, _nivel, _nivelMais }) { c.Margin = new Padding(0); nivel.Controls.Add(c); }
            _amostraLetra = new Panel();
            _amostraFundo = new Panel();
            _hexLetra = Texto("", FontePequena, Cinza);
            _hexFundo = Texto("", FontePequena, Cinza);
            var cores = new FlowLayoutPanel { Size = new Size(300, 34), WrapContents = false, BackColor = Color.Transparent };
            cores.Controls.Add(BotaoCor("Letra", _amostraLetra, _hexLetra, true));
            cores.Controls.Add(BotaoCor("Fundo", _amostraFundo, _hexFundo, false));
            // Coluna do meio: rotulo em cima de cada campo, um por linha; Dock=Top
            // empilha de baixo para cima
            var linhas = new Control[]
            {
                _previa, _arquivo,
                CampoVertical("Nome completo", "até 40 letras", _nomeCompleto),
                CampoVertical("Nome abreviado", "até 20 letras", _nomeAbreviado),
                CampoVertical("Treinador", "obrigatório", _treinador),
                CampoVertical("País", "da equipe", _pais),
                CampoVertical("Nível inicial", "20 = 1ª divisão no jogo novo", nivel, 130),
                CampoVertical("Cores", "letra e fundo", cores, 300),
            };
            for (int i = linhas.Length - 1; i >= 0; i--) cartaoEquipe.Controls.Add(linhas[i]);

            var cartaoRegras = NovoCartao(_liberado ? "Regras do jogo (estrangeiros liberados)" : "Regras do jogo", DockStyle.Top, 190);
            _contagem = Texto("", FonteNegrito, Color.White);
            _contagem.Location = new Point(12, 30);
            _problemas = Texto("", Fonte, Ok);
            _problemas.Location = new Point(12, 90);
            _problemas.MaximumSize = new Size(340, 0);
            cartaoRegras.Controls.AddRange(new Control[] { _contagem, _problemas });

            // Jogadores
            _jogadores = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                OwnerDraw = true,
                BorderStyle = BorderStyle.None,
                BackColor = Cartao,
                ForeColor = Color.White,
                Font = FonteLista,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
            };
            foreach (var (nome, largura, alinh) in new[]
            {
                ("Pos", 46, HorizontalAlignment.Center), ("Nome", 220, HorizontalAlignment.Left), ("País", 90, HorizontalAlignment.Left),
                ("Nota", 54, HorizontalAlignment.Right), ("Lesão", 60, HorizontalAlignment.Right), ("Comport.", 120, HorizontalAlignment.Left),
                ("✱", 30, HorizontalAlignment.Center), ("Estrangeiro", 110, HorizontalAlignment.Left),
            })
                _jogadores.Columns.Add(nome, largura, alinh);
            _jogadores.SmallImageList = new ImageList { ImageSize = new Size(1, 28) };
            _jogadores.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(Cartao)) e.Graphics.FillRectangle(b, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Header!.Text, FonteCartao, Rectangle.Inflate(e.Bounds, -4, 0), Amarelo, Formato(e.Header.TextAlign));
            };
            _jogadores.DrawSubItem += DesenharJogador;
            _jogadores.MouseDoubleClick += (_, _) => EditarJogador();
            _jogadores.SelectedIndexChanged += (_, _) => AtualizarBotoes();
            _jogadores.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) EditarJogador();
                else if (e.KeyCode == Keys.Delete) RemoverJogador();
                else if (e.KeyCode == Keys.Insert) AdicionarJogador();
            };
            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, WrapContents = false, Padding = new Padding(0, 6, 0, 0), BackColor = Cartao };
            _adicionar = Botao("Adicionar", true, 100);
            _editar = Botao("Editar", false, 90);
            _remover = Botao("Remover", false, 90);
            _transferir = Botao("Transferir…", false, 100);
            _adicionar.Click += (_, _) => AdicionarJogador();
            _editar.Click += (_, _) => EditarJogador();
            _remover.Click += (_, _) => RemoverJogador();
            _transferir.Click += (_, _) => TransferirJogador();
            foreach (var b in new[] { _adicionar, _editar, _remover, _transferir }) { b.Margin = new Padding(0, 0, 8, 0); botoes.Controls.Add(b); }
            var dica = Texto("Duplo-clique edita · Ins adiciona · Del remove", FontePequena, CinzaFaixa);
            dica.Margin = new Padding(4, 8, 0, 0);
            botoes.Controls.Add(dica);
            var tabela = new Panel { Dock = DockStyle.Fill, BackColor = Cartao, Padding = new Padding(8) };
            tabela.Controls.Add(_jogadores);
            tabela.Controls.Add(botoes);

            // Coluna do meio (dados e regras) e a tabela com a altura toda, para
            // caberem os 20 jogadores sem rolar
            var meio = new Panel { Dock = DockStyle.Left, Width = 384, AutoScroll = true, BackColor = Verde, Padding = new Padding(0, 0, 12, 0) };
            var esp1 = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Verde };
            meio.Controls.Add(cartaoRegras);
            meio.Controls.Add(esp1);
            meio.Controls.Add(cartaoEquipe);
            _direita.Controls.Add(tabela);
            _direita.Controls.Add(meio);

            Controls.Add(_direita);
            Controls.Add(esquerda);
            Controls.Add(barra);
        }

        private Control BotaoCor(string nome, Panel amostra, Label hex, bool letra)
        {
            var b = new Panel { Size = new Size(140, 34), BackColor = LinhaPar, Margin = new Padding(0, 0, 8, 0), Cursor = Cursors.Hand, BorderStyle = BorderStyle.FixedSingle };
            amostra.Size = new Size(22, 22);
            amostra.Location = new Point(6, 5);
            amostra.BorderStyle = BorderStyle.FixedSingle;
            var n = Texto(nome, FonteNegrito, Color.White);
            n.Location = new Point(34, 0);
            hex.Location = new Point(34, 17);
            b.Controls.AddRange(new Control[] { amostra, n, hex });
            foreach (Control c in new Control[] { b, amostra, n, hex }) c.Click += (_, _) => EscolherCor(letra);
            return b;
        }

        private static TextFormatFlags Formato(HorizontalAlignment a) => TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (a == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : a == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);

        private void DesenharEquipe(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _listaEquipes.Items.Count) return;
            var t = (EftTeam)_listaEquipes.Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Selecao : Cartao)) e.Graphics.FillRectangle(b, e.Bounds);
            var img = Bandeira(t.Pais);
            if (img != null) e.Graphics.DrawImage(img, e.Bounds.X + 6, e.Bounds.Y + 12, 30, 20);
            int x = e.Bounds.X + 44, w = e.Bounds.Width - 70;
            TextRenderer.DrawText(e.Graphics, t.NomeAbreviado.Length > 0 ? t.NomeAbreviado : "(sem nome)", FonteNegrito,
                new Rectangle(x, e.Bounds.Y + 3, w, 20), Color.White, TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, $"{t.NomeCompleto} · {t.Jogadores.Count} jog.", FontePequena,
                new Rectangle(x, e.Bounds.Y + 23, w, 18), Cinza, TextFormatFlags.EndEllipsis);
            if (_regras.Validar(t).Count > 0)
                TextRenderer.DrawText(e.Graphics, "⚠", FonteNegrito, new Rectangle(e.Bounds.Right - 24, e.Bounds.Y, 20, e.Bounds.Height), Alerta,
                    TextFormatFlags.VerticalCenter);
        }

        private void DesenharJogador(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item?.Tag is not EftPlayer j || e.SubItem == null || _atual == null) return;
            var fundo = e.Item.Selected ? Selecao : e.ItemIndex % 2 == 0 ? LinhaPar : Cartao;
            using (var b = new SolidBrush(fundo)) e.Graphics.FillRectangle(b, e.Bounds);
            int pos = Math.Max(0, Math.Min(3, j.Posicao));
            if (e.ColumnIndex == 0)
            {
                var selo = new Rectangle(e.Bounds.X + (e.Bounds.Width - 28) / 2, e.Bounds.Y + 4, 28, e.Bounds.Height - 8);
                using (var b = new SolidBrush(CorPosicao(pos))) e.Graphics.FillRectangle(b, selo);
                TextRenderer.DrawText(e.Graphics, TeamCodec.PosicoesCurtas[pos], FonteNegrito, selo, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            if (e.ColumnIndex == 2)
            {
                var img = Bandeira(j.Pais);
                if (img != null) e.Graphics.DrawImage(img, e.Bounds.X + 6, e.Bounds.Y + 7, 24, 16);
                TextRenderer.DrawText(e.Graphics, j.Pais, _jogadores.Font, new Rectangle(e.Bounds.X + 36, e.Bounds.Y, e.Bounds.Width - 36, e.Bounds.Height),
                    _porCodigo.ContainsKey(j.Pais) ? Color.White : Alerta, TextFormatFlags.VerticalCenter);
                return;
            }
            var cor = Color.White;
            if (e.ColumnIndex == 6) cor = Ouro;
            else if (e.ColumnIndex == 7) cor = e.SubItem.Text == "Sim" ? Alerta : Ok;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.ColumnIndex == 1 ? FonteNegrito : _jogadores.Font,
                Rectangle.Inflate(e.Bounds, -6, 0), cor, Formato(_jogadores.Columns[e.ColumnIndex].TextAlign));
        }

        // ---- lista de equipes ----

        private void CarregarEquipes()
        {
            _equipes.Clear();
            _ilegiveis = 0;
            if (Directory.Exists(_equipasDir))
                foreach (var f in Directory.GetFiles(_equipasDir).Where(f => f.EndsWith(".eft", StringComparison.OrdinalIgnoreCase)))
                {
                    try { _equipes.Add(TeamCodec.Read(f)); }
                    catch { _ilegiveis++; }
                }
            Ordenar();
            MontarFiltro();
            Filtrar();
        }

        private void Ordenar() => _equipes.Sort((a, b) => string.Compare(a.NomeAbreviado, b.NomeAbreviado, true, Br));

        private void MontarFiltro()
        {
            var antes = (_filtro.SelectedItem as ItemFiltro)?.Codigo;
            _carregando = true;
            _filtro.Items.Clear();
            _filtro.Items.Add(new ItemFiltro { Texto = $"Todos ({_equipes.Count})" });
            int sel = 0;
            foreach (var g in _equipes.GroupBy(t => t.Pais)
                         .Select(g => new { Codigo = g.Key, Nome = _porCodigo.TryGetValue(g.Key, out var p) ? p.Nome : g.Key, N = g.Count() })
                         .OrderBy(x => x.Nome, StringComparer.Create(Br, true)))
            {
                if (g.Codigo == antes) sel = _filtro.Items.Count;
                _filtro.Items.Add(new ItemFiltro { Codigo = g.Codigo, Texto = $"{g.Nome} ({g.N})" });
            }
            _filtro.SelectedIndex = sel;
            _carregando = false;
        }

        private static string SemAcento(string s)
        {
            var n = s.Normalize(System.Text.NormalizationForm.FormD);
            return new string(n.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        }

        private void Filtrar()
        {
            var pais = (_filtro.SelectedItem as ItemFiltro)?.Codigo;
            var termo = SemAcento(_busca.Text.Trim());
            bool Contem(string s) => SemAcento(s).IndexOf(termo, StringComparison.Ordinal) >= 0;
            var lista = _equipes.Where(t => (pais == null || t.Pais == pais) &&
                (termo.Length == 0 || Contem(t.NomeAbreviado) || Contem(t.NomeCompleto) || Contem(Path.GetFileName(t.Arquivo))
                 || t.Jogadores.Any(j => Contem(j.Nome)))).ToList();
            _carregando = true;
            _listaEquipes.BeginUpdate();
            _listaEquipes.Items.Clear();
            foreach (var t in lista) _listaEquipes.Items.Add(t);
            if (_atual != null && lista.Contains(_atual)) _listaEquipes.SelectedItem = _atual;
            _listaEquipes.EndUpdate();
            _carregando = false;
            _contaEquipes.Text = $"{lista.Count} de {_equipes.Count} equipes" + (termo.Length > 0 ? " (busca também por jogador)" : "") +
                (_ilegiveis > 0 ? $" · {_ilegiveis} ilegível(is)" : "");
        }

        private void TrocouEquipe()
        {
            if (_carregando || _listaEquipes.SelectedItem is not EftTeam t || t == _atual) return;
            if (_alterado && MessageBox.Show(this, "Há alterações não salvas nesta equipe. Descartar?", "Trocar de equipe",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                _carregando = true;
                _listaEquipes.SelectedItem = _atual;
                _carregando = false;
                return;
            }
            if (_alterado) Recarregar(_atual);
            _alterado = false;
            _atual = t;
            Text = "Editor de Equipes";
            MostrarEquipe();
        }

        // Descarta alteracoes: rele a equipe do disco (ou tira a nova da lista)
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
            var t = _atual;
            _direita.Enabled = t != null;
            _salvar.Enabled = t != null;
            _carregando = true;
            _nomeCompleto.Text = t?.NomeCompleto ?? "";
            _nomeAbreviado.Text = t?.NomeAbreviado ?? "";
            _treinador.Text = t?.Treinador ?? "";
            _pais.SelectedItem = t != null && _porCodigo.TryGetValue(t.Pais, out var p) ? p : null;
            _pais.Visible = t != null;
            _arquivo.Text = t == null ? "" : string.IsNullOrEmpty(t.Arquivo) ? "nova (ainda sem arquivo)" : "EQUIPAS/" + Path.GetFileName(t.Arquivo);
            _carregando = false;
            MostrarNivel();
            PintarCores();
            MostrarJogadores();
        }

        private void CamposMudaram()
        {
            if (_carregando || _atual == null) return;
            var pais = (_pais.SelectedItem as Pais)?.Codigo ?? _atual.Pais;
            if (_nomeCompleto.Text == _atual.NomeCompleto && _nomeAbreviado.Text == _atual.NomeAbreviado &&
                _treinador.Text == _atual.Treinador && pais == _atual.Pais) return;
            _atual.NomeCompleto = _nomeCompleto.Text;
            _atual.NomeAbreviado = _nomeAbreviado.Text;
            _atual.Treinador = _treinador.Text;
            _atual.Pais = pais;
            Alterou();
            PintarCores();
            MostrarJogadores();
            _listaEquipes.Invalidate();
        }

        private void Alterou()
        {
            _alterado = true;
            Text = "Editor de Equipes — alterações não salvas";
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
            _nivelMenos.Enabled = _atual != null && _atual.Nivel > TeamCodec.NIVEL_MIN;
            _nivelMais.Enabled = _atual != null && _atual.Nivel < TeamCodec.NIVEL_MAX;
        }

        private void PintarCores()
        {
            var t = _atual;
            if (t == null)
            {
                _previa.Text = "Escolha uma equipe à esquerda ou crie uma nova";
                _previa.BackColor = Cartao;
                _previa.ForeColor = Cinza;
                _hexLetra.Text = _hexFundo.Text = "—";
                return;
            }
            _previa.Text = t.NomeAbreviado.Length > 0 ? t.NomeAbreviado : "(sem nome)";
            _previa.BackColor = Rgb(t.CorFundo);
            _previa.ForeColor = Rgb(t.CorLetra);
            _amostraLetra.BackColor = Rgb(t.CorLetra);
            _amostraFundo.BackColor = Rgb(t.CorFundo);
            _hexLetra.Text = $"#{t.CorLetra:X6}";
            _hexFundo.Text = $"#{t.CorFundo:X6}";
        }

        // Janela de cores do Windows com as 16 cores do jogo como "cores
        // personalizadas" (escolha rapida) e o seletor livre aberto
        private void EscolherCor(bool letra)
        {
            if (_atual == null) return;
            using var d = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                Color = Rgb(letra ? _atual.CorLetra : _atual.CorFundo),
                CustomColors = Paleta.Select(c => ((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF)).ToArray(),
            };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            int rgb = d.Color.R << 16 | d.Color.G << 8 | d.Color.B;
            if (letra) _atual.CorLetra = rgb; else _atual.CorFundo = rgb;
            Alterou();
            PintarCores();
        }

        private void MostrarJogadores(int selecionado = -1)
        {
            if (selecionado < 0 && _jogadores.SelectedIndices.Count > 0) selecionado = _jogadores.SelectedIndices[0];
            _jogadores.BeginUpdate();
            _jogadores.Items.Clear();
            if (_atual != null)
                foreach (var j in _atual.Jogadores)
                {
                    int pos = Math.Max(0, Math.Min(3, j.Posicao));
                    _jogadores.Items.Add(new ListViewItem(new[]
                    {
                        TeamCodec.PosicoesCurtas[pos], j.Nome, j.Pais, j.Nota.ToString(), j.Lesao.ToString(),
                        SaveCodec.ComportamentoLabels[j.Comportamento], j.Estrela ? "✱" : "", TextoEstrangeiro(_regras.Situacao(j.Pais, _atual.Pais)),
                    }) { Tag = j });
                }
            _jogadores.EndUpdate();
            if (selecionado >= 0 && selecionado < _jogadores.Items.Count)
            {
                _jogadores.Items[selecionado].Selected = true;
                _jogadores.EnsureVisible(selecionado);
            }
            MostrarRegras();
            AtualizarBotoes();
        }

        private void AtualizarBotoes()
        {
            bool sel = _atual != null && _jogadores.SelectedIndices.Count > 0;
            _adicionar.Enabled = _atual != null && _atual.Jogadores.Count < TeamCodec.MAX_JOGADORES;
            _editar.Enabled = _remover.Enabled = _transferir.Enabled = sel;
        }

        private void MostrarRegras()
        {
            var t = _atual;
            if (t == null) { _contagem.Text = _problemas.Text = ""; return; }
            int gr = t.Jogadores.Count(j => j.Posicao == 0), campo = t.Jogadores.Count - gr, est = _regras.Estrangeiros(t);
            _contagem.Text = $"Jogadores {t.Jogadores.Count} (14 a 20)\nGuarda-redes {gr} (mín. 1) · Campo {campo} (mín. 10)\n" +
                (_liberado ? "Estrangeiros: sem limite (Liberado)" : $"Estrangeiros {est} de {TeamCodec.MAX_ESTRANGEIROS}");
            var erros = _regras.Validar(t);
            _problemas.Text = erros.Count == 0 ? "✓ Pronta para o jogo" : "✗ " + string.Join("\n✗ ", erros);
            _problemas.ForeColor = erros.Count == 0 ? Ok : Alerta;
        }

        // ---- acoes ----

        private void NovaEquipe()
        {
            if (_alterado && MessageBox.Show(this, "Há alterações não salvas nesta equipe. Descartar?", "Nova equipe",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (_alterado) Recarregar(_atual);
            var t = new EftTeam { Pais = (_filtro.SelectedItem as ItemFiltro)?.Codigo ?? "BRA", NomeCompleto = "NOVA EQUIPA", NomeAbreviado = "NOVA" };
            _equipes.Insert(0, t);
            _atual = t;
            MontarFiltro();
            Filtrar();
            MostrarEquipe();
            Alterou();
            _nomeCompleto.Focus();
            _nomeCompleto.SelectAll();
        }

        private void Salvar()
        {
            if (_atual == null) return;
            var erros = _regras.Validar(_atual);
            if (erros.Count > 0)
            {
                MessageBox.Show(this, "O jogo não aceita a equipe assim:\n\n• " + string.Join("\n• ", erros), "Não dá para salvar",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrEmpty(_atual.Arquivo))
            {
                var nome = PedirArquivo(SugerirArquivo(_atual.NomeAbreviado));
                if (nome == null) return;
                _atual.Arquivo = Path.Combine(_equipasDir, nome + ".EFT");
            }
            try { TeamCodec.Write(_atual, _atual.Arquivo); }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao gravar:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            _alterado = false;
            Text = "Editor de Equipes";
            Ordenar();
            MontarFiltro();
            Filtrar();
            MostrarEquipe();
        }

        // Nome de arquivo de ate 8 letras (como no Editor de Equipas), sem repetir
        private string SugerirArquivo(string nome)
        {
            var b = new string(SemAcento(nome).ToUpperInvariant().Where(c => (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')).ToArray());
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

        private string? PedirArquivo(string sugestao)
        {
            using var f = Dialogo("Gravar equipe", 360, 140);
            var lbl = new Label { Text = "Nome do arquivo (até 8 letras), na pasta EQUIPAS:", Location = new Point(14, 14), AutoSize = true };
            var tb = new TextBox { Text = sugestao, MaxLength = 8, CharacterCasing = CharacterCasing.Upper, Location = new Point(14, 40), Width = 330 };
            var ok = new Button { Text = "Gravar", Location = new Point(168, 96), Width = 85 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(259, 96), Width = 85 };
            ok.Click += (_, _) =>
            {
                var n = tb.Text.Trim().ToUpperInvariant();
                if (n.Length == 0 || n.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_')))
                {
                    MessageBox.Show(f, "Use até 8 letras sem acento, números ou _.", "Nome inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (ExisteArquivo(n))
                {
                    MessageBox.Show(f, $"Já existe EQUIPAS/{n}.EFT. Escolha outro nome.", "Nome em uso", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                f.DialogResult = DialogResult.OK;
            };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { lbl, tb, ok, cn });
            return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim().ToUpperInvariant() : null;
        }

        // ---- jogadores ----

        private EftPlayer? Selecionado() => _jogadores.SelectedItems.Count > 0 ? _jogadores.SelectedItems[0].Tag as EftPlayer : null;

        private void AdicionarJogador()
        {
            if (_atual == null || _atual.Jogadores.Count >= TeamCodec.MAX_JOGADORES) return;
            var j = new EftPlayer { Pais = _atual.Pais, Posicao = 1 };
            if (!FichaJogador(j, "Novo jogador")) return;
            _atual.Jogadores.Add(j);
            Alterou();
            MostrarJogadores(_atual.Jogadores.Count - 1);
            _listaEquipes.Invalidate();
        }

        private void EditarJogador()
        {
            var j = Selecionado();
            if (_atual == null || j == null) return;
            var c = new EftPlayer { Nome = j.Nome, Pais = j.Pais, Posicao = j.Posicao };
            if (!FichaJogador(c, j.Nome)) return;
            j.Nome = c.Nome;
            j.Pais = c.Pais;
            j.Posicao = c.Posicao;
            Alterou();
            MostrarJogadores();
            _listaEquipes.Invalidate();
        }

        private void RemoverJogador()
        {
            var j = Selecionado();
            if (_atual == null || j == null) return;
            if (MessageBox.Show(this, $"Remover {j.Nome} da equipe?", "Remover jogador", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int i = _atual.Jogadores.IndexOf(j);
            _atual.Jogadores.Remove(j);
            Alterou();
            MostrarJogadores(Math.Min(i, _atual.Jogadores.Count - 1));
            _listaEquipes.Invalidate();
        }

        // Passa o jogador para outra equipe: grava as duas na hora (a atual precisa
        // estar salva e as duas continuarem aceitas pelo jogo)
        private void TransferirJogador()
        {
            var j = Selecionado();
            if (_atual == null || j == null) return;
            if (_alterado || string.IsNullOrEmpty(_atual.Arquivo))
            {
                MessageBox.Show(this, "Salve a equipe atual antes de transferir.", "Transferir", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var destinos = _equipes.Where(t => t != _atual && !string.IsNullOrEmpty(t.Arquivo)).ToList();
            using var f = Dialogo($"Transferir {j.Nome}", 440, 140);
            var lbl = new Label { Text = "Equipe de destino (as duas são gravadas na hora):", Location = new Point(14, 14), AutoSize = true };
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(14, 40), Width = 410 };
            foreach (var t in destinos) combo.Items.Add($"{t.NomeAbreviado}  ({t.Pais}, {t.Jogadores.Count} jog.)");
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            var ok = new Button { Text = "Transferir", DialogResult = DialogResult.OK, Location = new Point(248, 96), Width = 85 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(339, 96), Width = 85 };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { lbl, combo, ok, cn });
            if (f.ShowDialog(this) != DialogResult.OK || combo.SelectedIndex < 0) return;
            var destino = destinos[combo.SelectedIndex];
            int indice = _atual.Jogadores.IndexOf(j);
            var origemNova = TeamCodec.Read(_atual.Arquivo);
            origemNova.Jogadores.RemoveAt(indice);
            var destinoNovo = TeamCodec.Read(destino.Arquivo);
            destinoNovo.Jogadores.Add(new EftPlayer { Nome = j.Nome, Pais = j.Pais, Posicao = j.Posicao });
            var erros = _regras.Validar(origemNova).Select(e => $"{_atual.NomeAbreviado}: {e}")
                .Concat(_regras.Validar(destinoNovo).Select(e => $"{destino.NomeAbreviado}: {e}")).ToList();
            if (erros.Count > 0)
            {
                MessageBox.Show(this, "Depois da transferência o jogo não aceitaria:\n\n• " + string.Join("\n• ", erros), "Não dá para transferir",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            TeamCodec.Write(origemNova, origemNova.Arquivo);
            TeamCodec.Write(destinoNovo, destinoNovo.Arquivo);
            _equipes[_equipes.IndexOf(_atual)] = origemNova;
            _equipes[_equipes.IndexOf(destino)] = destinoNovo;
            _atual = origemNova;
            Filtrar();
            MostrarEquipe();
            MessageBox.Show(this, $"{j.Nome} agora joga no {destinoNovo.NomeAbreviado}.", "Transferido", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Ficha do jogador: nome, posicao, pais e o que o jogo tira do nome, ao vivo
        private bool FichaJogador(EftPlayer j, string titulo)
        {
            using var f = Dialogo(titulo, 540, 290);
            var nome = new TextBox { Text = j.Nome, MaxLength = TeamCodec.MAX_NOME, Location = new Point(100, 14), Width = 420 };
            var pos = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(100, 46), Width = 200 };
            pos.Items.AddRange(TeamCodec.Posicoes);
            pos.SelectedIndex = Math.Max(0, Math.Min(3, j.Posicao));
            var pais = ComboBandeiras();
            foreach (var p in _paises) pais.Items.Add(p);
            pais.Location = new Point(100, 78);
            pais.Width = 320;
            pais.SelectedItem = _porCodigo.TryGetValue(j.Pais, out var pp) ? pp : null;
            f.Controls.Add(new Label { Text = "Nome", Location = new Point(14, 17), AutoSize = true });
            f.Controls.Add(new Label { Text = "Posição", Location = new Point(14, 49), AutoSize = true });
            f.Controls.Add(new Label { Text = "País", Location = new Point(14, 81), AutoSize = true });
            f.Controls.Add(new Label { Text = "O jogo tira estes valores do nome (e da posição):", Location = new Point(14, 116), AutoSize = true, ForeColor = Color.DimGray, Font = FontePequena });
            var caixas = new Label[4];
            string[] rotulos = { "Nota", "Lesão", "Comportamento", "Estrela" };
            int[] largs = { 90, 90, 150, 150 };
            int x = 14;
            for (int k = 0; k < 4; k++)
            {
                var cx = new Panel { Location = new Point(x, 136), Size = new Size(largs[k], 50), BorderStyle = BorderStyle.FixedSingle };
                cx.Controls.Add(new Label { Text = rotulos[k], Location = new Point(6, 3), AutoSize = true, ForeColor = Color.DimGray, Font = FontePequena });
                caixas[k] = new Label { Location = new Point(6, 20), AutoSize = true, Font = new Font("Segoe UI", k < 2 ? 13F : 10.5F, FontStyle.Bold) };
                cx.Controls.Add(caixas[k]);
                f.Controls.Add(cx);
                x += largs[k] + 8;
            }
            var situacao = new Label { Location = new Point(14, 196), Size = new Size(510, 36) };
            f.Controls.Add(situacao);
            void Atualizar()
            {
                var t = new EftPlayer { Nome = nome.Text, Posicao = Math.Max(0, pos.SelectedIndex), Pais = (pais.SelectedItem as Pais)?.Codigo ?? "" };
                bool vazio = t.Nome.Trim().Length == 0;
                caixas[0].Text = vazio ? "—" : t.Nota.ToString();
                caixas[1].Text = vazio ? "—" : t.Lesao.ToString();
                caixas[2].Text = vazio ? "—" : SaveCodec.ComportamentoLabels[t.Comportamento];
                caixas[3].Text = vazio ? "—" : t.Estrela ? "✱ Estrela" : "Sem estrela";
                caixas[3].ForeColor = t.Estrela && !vazio ? Color.FromArgb(0xC7, 0x9A, 0x00) : Color.Gray;
                var s = _atual == null ? "" : _regras.Situacao(t.Pais, _atual.Pais);
                situacao.Text = s switch
                {
                    "" => "Nacional.",
                    "Bosman" => "Não conta como estrangeiro: os dois países estão no BOSMAN.TXE (Lei Bosman).",
                    "PLOP" => "Não conta como estrangeiro: os dois países estão no PLOP.TXE (língua portuguesa).",
                    _ => $"Estrangeiro: conta no limite de {TeamCodec.MAX_ESTRANGEIROS} por equipe.",
                };
                situacao.ForeColor = s == "Estrangeiro" ? Color.FromArgb(0xC6, 0x28, 0x28) : Color.FromArgb(0x2E, 0x7D, 0x32);
            }
            nome.TextChanged += (_, _) => Atualizar();
            pos.SelectedIndexChanged += (_, _) => Atualizar();
            pais.SelectedIndexChanged += (_, _) => Atualizar();
            Atualizar();
            var ok = new Button { Text = "OK", Location = new Point(344, 246), Width = 85 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(435, 246), Width = 85 };
            ok.Click += (_, _) =>
            {
                var n = nome.Text.Trim();
                if (n.Length == 0) { MessageBox.Show(f, "Escreva o nome do jogador.", "Nome"); return; }
                // O Editor de Equipas nao aceita numeros nos nomes
                if (n.Any(char.IsDigit) || n.Any(c => c > 0xFF))
                {
                    MessageBox.Show(f, "Use só letras (com ou sem acento), espaço, ponto, hífen ou apóstrofo.", "Nome");
                    return;
                }
                if (pais.SelectedItem is not Pais p) { MessageBox.Show(f, "Escolha o país do jogador.", "País"); return; }
                j.Nome = n;
                j.Pais = p.Codigo;
                j.Posicao = pos.SelectedIndex;
                f.DialogResult = DialogResult.OK;
            };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { nome, pos, pais, ok, cn });
            return f.ShowDialog(this) == DialogResult.OK;
        }

        private static Form Dialogo(string titulo, int largura, int altura) => new Form
        {
            Text = titulo,
            ClientSize = new Size(largura, altura),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            Font = Fonte,
        };
    }
}
