using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Editor de Save (.e98) com o visual do jogo (verde e amarelo), igual ao do
    // Android e do Linux. Esquerda: cartoes Jogo (inflacao), Treinadores (troca
    // de equipe) e Clube (dinheiro, moral, estadio, cores). Direita: tabela de
    // jogadores; duplo-clique abre a ficha (forca, salario, nota, lesao, S/L,
    // comportamento).
    public class SaveEditorForm : Form
    {
        private static readonly Color Verde = Color.FromArgb(0x0B, 0x3D, 0x0B), VerdeTopo = Color.FromArgb(0x06, 0x26, 0x06),
            Cartao = Color.FromArgb(0x14, 0x52, 0x14), LinhaPar = Color.FromArgb(0x11, 0x4A, 0x11), Selecao = Color.FromArgb(0x2E, 0x7D, 0x32),
            Amarelo = Color.FromArgb(0xFC, 0xFE, 0x04), Cinza = Color.FromArgb(0xB8, 0xC9, 0xB8), CinzaFaixa = Color.FromArgb(0x8F, 0xA8, 0x8F),
            Alerta = Color.FromArgb(0xFF, 0x8A, 0x80), Ouro = Color.FromArgb(0xFF, 0xD5, 0x4F);
        // As 16 cores que o jogo usa nas equipes
        private static readonly int[] Paleta =
        {
            0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xC0C0C0,
            0x808080, 0xFF0000, 0x00FF00, 0xFFFF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFFFF,
        };
        private static readonly Font Fonte = new Font("Segoe UI", 9.5F), FonteNegrito = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            FontePequena = new Font("Segoe UI", 8F), FonteTitulo = new Font("Segoe UI", 16F, FontStyle.Bold),
            FonteCartao = new Font("Segoe UI", 8.5F, FontStyle.Bold);

        private readonly string _jogosDir;
        private ComboBox _saveSel = null!, _teamSel = null!;
        private TextBox _inflacao = null!, _dinheiro = null!, _moral = null!;
        private Label _temporada = null!, _estadio = null!, _previa = null!, _hexLetra = null!, _hexFundo = null!;
        private Panel _amostraLetra = null!, _amostraFundo = null!, _cartaoTreinadores = null!;
        private FlowLayoutPanel _treinadores = null!;
        private Button _estMenos = null!, _estMais = null!, _salvar = null!;
        private ListView _players = null!;

        private SaveFile? _current;
        private SaveTeam? _currentTeam;
        private List<SaveTeam> _teams = new List<SaveTeam>();
        private string _currentPath = "";
        private bool _trocandoTime;
        // Texto mostrado ao carregar: so aplica inflacao/moral se o usuario mudou
        // (a tela arredonda em 1 casa e regravaria um valor diferente do original)
        private string _inflacaoMostrada = "", _moralMostrado = "";

        public SaveEditorForm(string jogosDir)
        {
            _jogosDir = jogosDir;
            Text = "Editor de Save";
            ClientSize = new Size(1180, 700);
            MinimumSize = new Size(820, 560);
            Tela.CaberNaTela(this);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Verde;
            Font = Fonte;

            BuildUi();
            RefreshSaveList(preserveSelection: false);
        }

        // ---- pecas de layout ----

        private static Label Texto(string t, Font f, Color cor) => new Label
        {
            Text = t,
            Font = f,
            ForeColor = cor,
            AutoSize = true,
            BackColor = Color.Transparent,
        };

        private static Button BotaoAmarelo(string texto, int largura, int altura = 30)
        {
            var b = new BotaoJogo
            {
                Text = texto,
                Width = largura,
                Height = altura,
                BackColor = Amarelo,
                ForeColor = Color.Black,
                Font = FonteNegrito,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0xEE, 0xF0, 0x00);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0xD8, 0xDA, 0x00);
            return b;
        }

        private static Button BotaoClaro(string texto) => new Button
        {
            Text = texto,
            Width = 96,
            Height = 30,
            BackColor = Color.FromArgb(0xE6, 0xE6, 0xE6),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.System,
        };

        // Cartao: painel verde com titulo em amarelo; os filhos ficam empilhados
        private static FlowLayoutPanel NovoCartao(string titulo)
        {
            var c = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Cartao,
                Padding = new Padding(12, 10, 12, 12),
                Margin = new Padding(0, 0, 0, 10),
                MinimumSize = new Size(316, 0),
            };
            var t = Texto(titulo.ToUpperInvariant(), FonteCartao, Amarelo);
            t.Margin = new Padding(0, 0, 0, 6);
            c.Controls.Add(t);
            return c;
        }

        // Rotulo com a faixa permitida embaixo, em letra menor
        private static Panel LinhaCampo(string rotulo, string faixa, Control campo, Control? extra = null)
        {
            var p = new Panel { Size = new Size(292, 40), Margin = new Padding(0, 2, 0, 2), BackColor = Color.Transparent };
            var r = Texto(rotulo, Fonte, Cinza);
            r.Location = new Point(0, 2);
            var f = Texto(faixa, FontePequena, CinzaFaixa);
            f.Location = new Point(0, 21);
            p.Controls.Add(r);
            p.Controls.Add(f);
            campo.Location = new Point(110, 8);
            campo.Width = extra == null ? 182 : 80;
            p.Controls.Add(campo);
            if (extra != null)
            {
                extra.Location = new Point(196, 12);
                p.Controls.Add(extra);
            }
            return p;
        }

        private void BuildUi()
        {
            // Barra do topo
            var barra = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = VerdeTopo };
            var titulo = Texto("Editor de Save", FonteTitulo, Amarelo);
            titulo.Location = new Point(14, 10);
            barra.Controls.Add(titulo);
            var direita = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                WrapContents = false,
                Padding = new Padding(0, 10, 10, 0),
                BackColor = Color.Transparent,
            };
            var rotSave = Texto("Save", Fonte, Cinza);
            rotSave.Margin = new Padding(0, 6, 4, 0);
            _saveSel = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
            _saveSel.SelectedIndexChanged += (_, _) => LoadSelected();
            var recarregar = BotaoClaro("Recarregar");
            recarregar.Click += (_, _) => RefreshSaveList(preserveSelection: true);
            _salvar = BotaoAmarelo("Salvar", 110);
            _salvar.Margin = new Padding(8, 0, 8, 0);
            _salvar.Enabled = false;
            _salvar.Click += (_, _) => SaveCurrent();
            var fechar = BotaoClaro("Fechar");
            fechar.Click += (_, _) => Close();
            direita.Controls.AddRange(new Control[] { rotSave, _saveSel, recarregar, _salvar, fechar });
            barra.Controls.Add(direita);

            // Coluna da esquerda
            var esquerda = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 350,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(12, 12, 4, 12),
                BackColor = Verde,
            };

            var jogo = NovoCartao("Jogo");
            _inflacao = new TextBox();
            _temporada = Texto("", FontePequena, Cinza);
            jogo.Controls.Add(LinhaCampo("Inflação", "5 a 100", _inflacao, _temporada));

            var treinadores = NovoCartao("Treinadores");
            _treinadores = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                BackColor = Color.Transparent,
            };
            treinadores.Controls.Add(_treinadores);
            treinadores.Visible = false;
            _cartaoTreinadores = treinadores;

            var clube = NovoCartao("Clube");
            _teamSel = new ComboBox { Width = 292, DropDownStyle = ComboBoxStyle.DropDownList };
            _teamSel.SelectedIndexChanged += (_, _) => TrocouTime();
            _previa = new Label
            {
                Size = new Size(292, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Margin = new Padding(0, 8, 0, 6),
                BorderStyle = BorderStyle.FixedSingle,
            };
            _dinheiro = new TextBox();
            _moral = new TextBox();
            _estMenos = BotaoPasso("−", -1);
            _estMais = BotaoPasso("+", 1);
            _estadio = new Label { Size = new Size(110, 30), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent };
            var estadio = new FlowLayoutPanel { Size = new Size(182, 32), WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            _estMenos.Margin = _estMais.Margin = new Padding(0);
            _estadio.Margin = new Padding(0);
            estadio.Controls.AddRange(new Control[] { _estMenos, _estadio, _estMais });
            clube.Controls.Add(_teamSel);
            clube.Controls.Add(_previa);
            clube.Controls.Add(LinhaCampo("Dinheiro", "0 a 999.999.999", _dinheiro));
            clube.Controls.Add(LinhaCampo("Moral", "0 a 20", _moral));
            clube.Controls.Add(LinhaCampo("Estádio", "5.000 a 120.000", estadio));
            var rc = Texto("Cores", Fonte, Cinza);
            rc.Margin = new Padding(0, 6, 0, 4);
            clube.Controls.Add(rc);
            var cores = new FlowLayoutPanel { Size = new Size(292, 50), WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            _amostraLetra = new Panel();
            _amostraFundo = new Panel();
            _hexLetra = Texto("", FontePequena, Cinza);
            _hexFundo = Texto("", FontePequena, Cinza);
            cores.Controls.Add(BotaoCor("Letra", _amostraLetra, _hexLetra, true));
            cores.Controls.Add(BotaoCor("Fundo", _amostraFundo, _hexFundo, false));
            clube.Controls.Add(cores);

            esquerda.Controls.AddRange(new Control[] { jogo, treinadores, clube });

            // Tabela de jogadores
            _players = new ListView
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
                Font = new Font("Segoe UI", 10F),
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
            };
            foreach (var (nome, largura, alinh) in new[]
            {
                ("Pos", 46, HorizontalAlignment.Center), ("Nome", 230, HorizontalAlignment.Left), ("✱", 30, HorizontalAlignment.Center),
                ("Força", 64, HorizontalAlignment.Right), ("Salário", 100, HorizontalAlignment.Right), ("Nota", 54, HorizontalAlignment.Right),
                ("Lesão", 60, HorizontalAlignment.Right), ("Comport.", 120, HorizontalAlignment.Left), ("Sit.", 70, HorizontalAlignment.Left),
            })
                _players.Columns.Add(nome, largura, alinh);
            // Linhas mais altas (a altura vem da ImageList)
            _players.SmallImageList = new ImageList { ImageSize = new Size(1, 30) };
            _players.DrawColumnHeader += DesenharCabecalho;
            _players.DrawSubItem += DesenharCelula;
            _players.MouseDoubleClick += (_, _) => EditarJogador();
            _players.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) EditarJogador(); };
            var dica = Texto("Duplo-clique (ou Enter) num jogador para editar. S = suspenso, L = lesionado (jogos).", FontePequena, Cinza);
            dica.Dock = DockStyle.Bottom;
            dica.AutoSize = false;
            dica.Height = 22;
            dica.TextAlign = ContentAlignment.MiddleLeft;
            var tabela = new Panel { Dock = DockStyle.Fill, BackColor = Cartao, Padding = new Padding(8) };
            tabela.Controls.Add(_players);
            tabela.Controls.Add(dica);
            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 12, 12, 12), BackColor = Verde };
            corpo.Controls.Add(tabela);

            Controls.Add(corpo);
            Controls.Add(esquerda);
            Controls.Add(barra);
        }

        private Button BotaoPasso(string rotulo, int passo)
        {
            var b = BotaoAmarelo(rotulo, 34, 30);
            b.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            b.Click += (_, _) =>
            {
                if (_currentTeam == null || _currentTeam.EstadioOffset < 0) return;
                _currentTeam.Estadio = Math.Max(1, Math.Min(SaveCodec.ESTADIO_MAX, _currentTeam.Estadio + passo));
                MostrarEstadio();
            };
            return b;
        }

        // Botao "Letra"/"Fundo": amostra da cor + nome + codigo; clique abre o seletor
        private Control BotaoCor(string nome, Panel amostra, Label hex, bool letra)
        {
            var b = new Panel
            {
                Size = new Size(142, 46),
                BackColor = LinhaPar,
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                BorderStyle = BorderStyle.FixedSingle,
            };
            amostra.Size = new Size(26, 26);
            amostra.Location = new Point(8, 9);
            amostra.BorderStyle = BorderStyle.FixedSingle;
            var n = Texto(nome, FonteNegrito, Color.White);
            n.Location = new Point(42, 4);
            hex.Location = new Point(42, 24);
            b.Controls.AddRange(new Control[] { amostra, n, hex });
            foreach (Control c in new Control[] { b, amostra, n, hex }) c.Click += (_, _) => EscolherCor(letra);
            return b;
        }

        private static Color Rgb(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

        private static Color CorPosicao(string pos) => pos switch
        {
            "G" => Color.FromArgb(0xE0, 0xB0, 0x00),
            "D" => Color.FromArgb(0x3D, 0x7B, 0xD9),
            "M" => Color.FromArgb(0x2E, 0x9E, 0x4F),
            "A" => Color.FromArgb(0xD9, 0x44, 0x3D),
            _ => Color.Gray,
        };

        private static string NomePosicao(string pos) => pos switch
        {
            "G" => "Guarda-redes",
            "D" => "Defesa",
            "M" => "Médio",
            "A" => "Avançado",
            _ => pos,
        };

        private static string Milhar(long v) => v.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

        private static string Fmt(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        // ---- tabela desenhada ----

        private void DesenharCabecalho(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            using (var fundo = new SolidBrush(Cartao)) e.Graphics.FillRectangle(fundo, e.Bounds);
            var r = Rectangle.Inflate(e.Bounds, -4, 0);
            TextRenderer.DrawText(e.Graphics, e.Header!.Text, FonteCartao, r, Amarelo, Formato(e.Header.TextAlign));
        }

        private static TextFormatFlags Formato(HorizontalAlignment a) => TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (a == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : a == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);

        private void DesenharCelula(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item?.Tag is not SavePlayer j || e.SubItem == null) return;
            var fundo = e.Item.Selected ? Selecao : e.ItemIndex % 2 == 0 ? LinhaPar : Cartao;
            using (var b = new SolidBrush(fundo)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.ColumnIndex == 0)
            {
                // Selo colorido da posicao
                var selo = new Rectangle(e.Bounds.X + (e.Bounds.Width - 28) / 2, e.Bounds.Y + 5, 28, e.Bounds.Height - 10);
                using (var b = new SolidBrush(CorPosicao(j.Posicao))) e.Graphics.FillRectangle(b, selo);
                TextRenderer.DrawText(e.Graphics, j.Posicao, FonteNegrito, selo, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            var cor = Color.White;
            if (e.ColumnIndex == 2) cor = Ouro;
            else if (e.ColumnIndex == 3 && j.Forca > SaveCodec.FORCA_WARN_ABOVE) cor = Amarelo;
            else if (e.ColumnIndex == 8) cor = Alerta;
            var fonte = e.ColumnIndex == 1 ? FonteNegrito : _players.Font;
            var r = Rectangle.Inflate(e.Bounds, -6, 0);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, fonte, r, cor, Formato(_players.Columns[e.ColumnIndex].TextAlign));
        }

        // ---- dados ----

        private void RefreshSaveList(bool preserveSelection)
        {
            string? prev = preserveSelection ? _saveSel.SelectedItem as string : null;
            if (!Directory.Exists(_jogosDir))
            {
                MessageBox.Show(this, $"Pasta JOGOS não encontrada:\n{_jogosDir}", "Sem saves", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var files = Directory.GetFiles(_jogosDir, "*.e98", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).OrderBy(n => n).ToList();
            _saveSel.Items.Clear();
            foreach (var f in files) _saveSel.Items.Add(f);
            if (files.Count == 0)
            {
                MessageBox.Show(this, "Nenhum jogo gravado em JOGOS ainda.", "Sem saves", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int found = prev != null ? files.IndexOf(prev) : -1;
            _saveSel.SelectedIndex = -1;
            _saveSel.SelectedIndex = found >= 0 ? found : 0;
        }

        private void LoadSelected()
        {
            if (_saveSel.SelectedItem is not string name) return;
            _currentPath = Path.Combine(_jogosDir, name);
            try
            {
                _current = SaveCodec.Read(_currentPath);
                _currentTeam = null;
                _inflacao.Enabled = _current.InflacaoOffset > 0;
                _inflacaoMostrada = _current.InflacaoOffset > 0 ? Fmt(_current.Inflacao * 10) : "";
                _inflacao.Text = _inflacaoMostrada;
                _temporada.Text = _current.InflacaoOffset > 0 ? $"temporada {_current.Ano}" : "";
                _teams = _current.Teams.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).ToList();
                _trocandoTime = true;
                _teamSel.Items.Clear();
                foreach (var t in _teams) _teamSel.Items.Add(t.Nome);
                _trocandoTime = false;
                if (_teams.Count > 0) _teamSel.SelectedIndex = 0;
                MostrarTreinadores();
                _salvar.Enabled = true;
            }
            catch (Exception ex)
            {
                _current = null;
                MostrarTreinadores();
                _salvar.Enabled = false;
                MessageBox.Show(this, $"Falha ao ler save:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TrocouTime()
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
                    MessageBox.Show(this, erro, "Valor fora do limite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _moral.Enabled = t.MoralOffset > 0;
            _moralMostrado = t.MoralOffset > 0 ? Fmt(t.Moral * 10) : "";
            _moral.Text = _moralMostrado;
            MostrarEstadio();
            PintarCores();
        }

        private void MostrarJogadores(int selecionado = -1)
        {
            _players.BeginUpdate();
            _players.Items.Clear();
            if (_currentTeam != null)
            {
                foreach (var j in SaveCodec.OrdemDoJogo(_currentTeam.Players))
                {
                    string comp = j.Comportamento >= 0 && j.Comportamento < SaveCodec.ComportamentoLabels.Length
                        ? SaveCodec.ComportamentoLabels[j.Comportamento] : "?";
                    string sit = ((j.Suspensao > 0 ? "S" + j.Suspensao : "") + (j.JogosLesionado > 0 ? " L" + j.JogosLesionado : "")).Trim();
                    var it = new ListViewItem(new[] { j.Posicao, j.Nome, j.Estrela ? "✱" : "", j.Forca.ToString(), Milhar(j.Salario),
                        j.Nota.ToString(), j.Lesao.ToString(), comp, sit }) { Tag = j };
                    _players.Items.Add(it);
                }
            }
            _players.EndUpdate();
            if (selecionado >= 0 && selecionado < _players.Items.Count)
            {
                _players.Items[selecionado].Selected = true;
                _players.EnsureVisible(selecionado);
            }
        }

        private void MostrarEstadio()
        {
            bool tem = _currentTeam != null && _currentTeam.EstadioOffset > 0;
            _estadio.Text = tem ? Milhar(_currentTeam!.Estadio * 5000) : "—";
            _estMenos.Enabled = tem && _currentTeam!.Estadio > 1;
            _estMais.Enabled = tem && _currentTeam!.Estadio < SaveCodec.ESTADIO_MAX;
        }

        private void PintarCores()
        {
            var t = _currentTeam;
            _previa.Text = t?.Nome ?? "";
            if (t == null || t.CoresOffset < 0)
            {
                _previa.BackColor = Cartao;
                _previa.ForeColor = Color.White;
                _hexLetra.Text = _hexFundo.Text = "—";
                return;
            }
            _amostraLetra.BackColor = Rgb(t.CorLetra);
            _amostraFundo.BackColor = Rgb(t.CorFundo);
            _hexLetra.Text = $"#{t.CorLetra:X6}";
            _hexFundo.Text = $"#{t.CorFundo:X6}";
            _previa.BackColor = Rgb(t.CorFundo);
            _previa.ForeColor = Rgb(t.CorLetra);
        }

        // ---- limites ----

        private static string Faixa(string nome, double min, double max) =>
            $"{nome}: use um valor de {Milhar((long)min)} a {Milhar((long)max)}.";

        // Le um inteiro do campo; fora da faixa (ou nao numerico) devolve o erro em vez de cortar
        private static string? LerInteiro(TextBox tb, string nome, long min, long max, out long v)
        {
            if (long.TryParse(tb.Text.Trim().Replace(".", ""), out v) && v >= min && v <= max) return null;
            return Faixa(nome, min, max);
        }

        private static string? LerReal(TextBox tb, string nome, double min, double max, out double v)
        {
            if (double.TryParse(tb.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                && v >= min && v <= max) return null;
            return Faixa(nome, min, max);
        }

        // Passa os campos do clube e da inflacao pro save em memoria (dentro dos
        // limites do jogo); devolve a mensagem de erro, ou null
        private string? AplicarCampos()
        {
            if (_current != null && _current.InflacaoOffset > 0 && _inflacao.Text.Trim() != _inflacaoMostrada)
            {
                var e = LerReal(_inflacao, "Inflação", SaveCodec.INFLACAO_MIN * 10, SaveCodec.INFLACAO_MAX * 10, out double inf);
                if (e != null) return e;
                _current.Inflacao = inf / 10;
            }
            if (_currentTeam != null)
            {
                var e = LerInteiro(_dinheiro, "Dinheiro", 0, SaveCodec.DINHEIRO_MAX, out long verba);
                if (e != null) return e;
                if (_currentTeam.MoralOffset > 0 && _moral.Text.Trim() != _moralMostrado)
                {
                    e = LerReal(_moral, "Moral", 0, SaveCodec.MORAL_MAX * 10, out double m);
                    if (e != null) return e;
                    _currentTeam.Moral = m / 10;
                }
                _currentTeam.Verba = verba;
            }
            return null;
        }

        private void SaveCurrent()
        {
            if (_current == null || string.IsNullOrEmpty(_currentPath)) return;
            var erro = AplicarCampos();
            if (erro != null)
            {
                MessageBox.Show(this, erro, "Valor fora do limite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                var bak = _currentPath + ".bak";
                if (!File.Exists(bak)) File.Copy(_currentPath, bak);
                SaveCodec.Write(_currentPath, _current);
                MessageBox.Show(this, "Save gravado (backup .bak na primeira vez).", "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao gravar:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---- treinadores ----

        private void MostrarTreinadores()
        {
            _treinadores.Controls.Clear();
            bool algum = false;
            if (_current != null)
            {
                foreach (var tec in _current.Tecnicos.Where(t => t.Humano))
                {
                    algum = true;
                    var equipe = _current.TimeDoTecnico(tec);
                    var linha = new Panel { Size = new Size(292, 42), BackColor = Color.Transparent, Margin = new Padding(0, 2, 0, 2) };
                    var nome = Texto(tec.Nome, FonteNegrito, Color.White);
                    nome.Location = new Point(0, 2);
                    var eq = Texto(equipe?.Nome ?? "sem equipe", FontePequena, Cinza);
                    eq.Location = new Point(0, 22);
                    linha.Controls.Add(nome);
                    linha.Controls.Add(eq);
                    if (equipe != null)
                    {
                        var b = BotaoAmarelo("Trocar", 80);
                        b.Location = new Point(212, 6);
                        b.Click += (_, _) => TrocarEquipe(tec);
                        linha.Controls.Add(b);
                        b.BringToFront();
                    }
                    _treinadores.Controls.Add(linha);
                }
            }
            _cartaoTreinadores.Visible = algum;
        }

        // Escolhe a equipe nova; as duas equipes trocam de treinador como numa
        // "chicotada psicologica" do jogo
        private void TrocarEquipe(SaveTecnico tec)
        {
            if (_current == null) return;
            var erro = AplicarCampos();
            if (erro != null) { MessageBox.Show(this, erro, "Valor fora do limite", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var origem = _current.TimeDoTecnico(tec);
            var destinos = _teams.Where(t => t != origem && t.TecnicoId >= 0 && t.PodeTerHumano).ToList();
            if (origem == null || destinos.Count == 0) return;

            using var f = Dialogo($"Trocar de equipe — {tec.Nome}", 470, 250);
            var atual = new Label { Text = $"Equipe atual: {origem.Nome}", Location = new Point(16, 16), AutoSize = true };
            var rot = new Label { Text = "Nova equipe", Location = new Point(16, 48), AutoSize = true };
            var combo = new ComboBox { Location = new Point(16, 70), Width = 430, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var t in destinos) combo.Items.Add($"{t.Nome}  ({t.Divisao})");
            var efeito = new Label { Location = new Point(16, 104), Size = new Size(430, 60), ForeColor = Color.DimGray };
            combo.SelectedIndexChanged += (_, _) =>
            {
                var outro = _current.Tecnico(destinos[Math.Max(0, combo.SelectedIndex)].TecnicoId);
                efeito.Text = (outro != null ? $"{outro.Nome} vai para {origem.Nome}. " : "")
                    + "As duas equipes ficam com moral 10, como numa chicotada psicológica do jogo. "
                    + "Só aparecem equipes das divisões (no Distrital o jogo quebra).";
            };
            combo.SelectedIndex = 0;
            var ok = new Button { Text = "Trocar", DialogResult = DialogResult.OK, Location = new Point(270, 172), Width = 85 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(361, 172), Width = 85 };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { atual, rot, combo, efeito, ok, cn });
            if (f.ShowDialog(this) != DialogResult.OK) return;
            var destino = destinos[combo.SelectedIndex];
            try
            {
                SaveCodec.TrocarEquipe(_current, tec, destino);
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            MostrarTreinadores();
            if (_currentTeam != null) MostrarCampos();
            _teamSel.SelectedIndex = _teams.IndexOf(destino);
            MessageBox.Show(this, $"{tec.Nome} agora treina {destino.Nome}.\nClique em Salvar para gravar no save.", "Equipe trocada",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---- cores ----

        // Janela de cores do Windows com as 16 cores do jogo como "cores
        // personalizadas" (escolha rapida) e o seletor livre aberto; o save guarda RGB completo
        private void EscolherCor(bool letra)
        {
            var t = _currentTeam;
            if (t == null || t.CoresOffset < 0) return;
            using var d = new ColorDialog
            {
                FullOpen = true,
                AnyColor = true,
                Color = Rgb(letra ? t.CorLetra : t.CorFundo),
                // CustomColors usa 0x00BBGGRR
                CustomColors = Paleta.Select(c => ((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF)).ToArray(),
            };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            int rgb = d.Color.R << 16 | d.Color.G << 8 | d.Color.B;
            if (letra) t.CorLetra = rgb; else t.CorFundo = rgb;
            PintarCores();
        }

        // ---- ficha do jogador ----

        private void EditarJogador()
        {
            if (_currentTeam == null || _players.SelectedItems.Count == 0 || _players.SelectedItems[0].Tag is not SavePlayer j) return;
            int idx = _players.SelectedIndices[0];

            using var f = Dialogo(j.Nome, 520, 400);
            var selo = new Label
            {
                Text = NomePosicao(j.Posicao),
                BackColor = CorPosicao(j.Posicao),
                ForeColor = Color.White,
                Font = FonteNegrito,
                AutoSize = true,
                Padding = new Padding(8, 3, 8, 3),
                Location = new Point(16, 14),
            };
            var estrela = new Label { Font = FonteNegrito, AutoSize = true, Location = new Point(150, 18) };
            var regra = new Label
            {
                Text = "Estrela (✱): Médio ou Avançado com nota 8 ou mais. Muda sozinha com a nota.",
                ForeColor = Color.DimGray,
                Font = FontePequena,
                AutoSize = true,
                Location = new Point(16, 48),
            };
            f.Controls.AddRange(new Control[] { selo, estrela, regra });

            int celula = 0;
            Control Celula(string titulo, string faixa, Control campo, int colunas = 1)
            {
                int x = 16 + (celula % 3) * 162, y = 76 + (celula / 3) * 70;
                f.Controls.Add(new Label { Text = titulo, Location = new Point(x, y), AutoSize = true });
                f.Controls.Add(new Label { Text = faixa, Location = new Point(x, y + 18), AutoSize = true, ForeColor = Color.DimGray, Font = FontePequena });
                campo.Location = new Point(x, y + 36);
                campo.Width = 150 * colunas + 12 * (colunas - 1);
                f.Controls.Add(campo);
                celula += colunas;
                return campo;
            }
            TextBox Campo(string titulo, string faixa, int valor) => (TextBox)Celula(titulo, faixa, new TextBox { Text = valor.ToString() });
            var forca = Campo("Força", $"1 a {SaveCodec.FORCA_MAX} (normal ≤ 50)", j.Forca);
            var salario = Campo("Salário", "50 a 9.999.999", j.Salario);
            var nota = Campo("Nota", "1 a 10", j.Nota);
            var lesao = Campo("Lesão", "0 = nunca, 10 = muito", j.Lesao);
            var suspenso = Campo("Suspenso (S)", "jogos, 0 a 4", j.Suspensao);
            var lesionado = Campo("Lesionado (L)", "jogos, 0 a 20", j.JogosLesionado);
            var comp = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            comp.Items.AddRange(SaveCodec.ComportamentoLabels);
            comp.SelectedIndex = Math.Max(0, Math.Min(5, j.Comportamento));
            Celula("Comportamento", "Fair Play … Sarrafeiro", comp, 2);

            // Estrela ao vivo: se a nota mudar, segue a regra do jogo; senao mantem a do save
            void MostrarEstrela()
            {
                bool tem = j.Estrela;
                if (int.TryParse(nota.Text.Trim(), out int n) && n != j.Nota) tem = SaveCodec.TemEstrela(j.Posicao, n);
                estrela.Text = tem ? "✱ Estrela" : "Sem estrela";
                estrela.ForeColor = tem ? Color.FromArgb(0xC7, 0x9A, 0x00) : Color.Gray;
            }
            nota.TextChanged += (_, _) => MostrarEstrela();
            MostrarEstrela();

            var ok = new Button { Text = "OK", Location = new Point(320, 310), Width = 85 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(411, 310), Width = 85 };
            // OK so fecha se tudo estiver dentro dos limites
            ok.Click += (_, _) =>
            {
                long fo = 0, sal = 0, n = 0, l = 0, su = 0, le = 0;
                var erro = LerInteiro(forca, "Força", SaveCodec.FORCA_MIN, SaveCodec.FORCA_MAX, out fo)
                    ?? LerInteiro(salario, "Salário", SaveCodec.SALARIO_MIN, SaveCodec.SALARIO_MAX, out sal)
                    ?? LerInteiro(nota, "Nota", 1, 10, out n)
                    ?? LerInteiro(lesao, "Lesão", 0, 10, out l)
                    ?? LerInteiro(suspenso, "Suspenso", 0, SaveCodec.SUSPENSAO_MAX, out su)
                    ?? LerInteiro(lesionado, "Lesionado", 0, SaveCodec.JOGOS_LESIONADO_MAX, out le);
                if (erro != null)
                {
                    MessageBox.Show(f, erro, "Valor fora do limite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (fo > SaveCodec.FORCA_WARN_ABOVE && fo != j.Forca &&
                    MessageBox.Show(f, $"Força {fo} é bem acima do normal (1-{SaveCodec.FORCA_WARN_ABOVE}).\nContinuar mesmo assim?",
                        "Aviso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
                if (n != j.Nota) j.Estrela = SaveCodec.TemEstrela(j.Posicao, (int)n);
                j.Forca = (int)fo;
                j.Salario = (int)sal;
                j.Nota = (int)n;
                j.Lesao = (int)l;
                j.Suspensao = (int)su;
                j.JogosLesionado = (int)le;
                j.Comportamento = comp.SelectedIndex;
                f.DialogResult = DialogResult.OK;
            };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { ok, cn });
            if (f.ShowDialog(this) == DialogResult.OK) MostrarJogadores(idx);
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
