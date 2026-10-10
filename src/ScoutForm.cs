using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Scout: todos os jogadores de um save (como o Turbo Save), com filtros e
    // ordenacao para achar os melhores. So leitura.
    public class ScoutForm : Form
    {
        private static readonly Color Verde = Color.FromArgb(0x0B, 0x3D, 0x0B), VerdeTopo = Color.FromArgb(0x06, 0x26, 0x06),
            Cartao = Color.FromArgb(0x14, 0x52, 0x14), LinhaPar = Color.FromArgb(0x11, 0x4A, 0x11), Selecao = Color.FromArgb(0x2E, 0x7D, 0x32),
            Amarelo = Color.FromArgb(0xFC, 0xFE, 0x04), Cinza = Color.FromArgb(0xB8, 0xC9, 0xB8), CinzaFaixa = Color.FromArgb(0x8F, 0xA8, 0x8F),
            Alerta = Color.FromArgb(0xFF, 0x8A, 0x80), Ok = Color.FromArgb(0x9C, 0xE2, 0x9C), Ouro = Color.FromArgb(0xFF, 0xD5, 0x4F),
            Desligado = Color.FromArgb(0x2A, 0x5C, 0x2A);
        private static readonly Color[] CoresPosicao =
            { Color.FromArgb(0xE0, 0xB0, 0x00), Color.FromArgb(0x3D, 0x7B, 0xD9), Color.FromArgb(0x2E, 0x9E, 0x4F), Color.FromArgb(0xD9, 0x44, 0x3D) };
        private static readonly Font Fonte = new Font("Segoe UI", 9.5F), FonteNegrito = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            FontePequena = new Font("Segoe UI", 8F), FonteTitulo = new Font("Segoe UI", 16F, FontStyle.Bold),
            FonteCartao = new Font("Segoe UI", 8.5F, FontStyle.Bold), FonteLista = new Font("Segoe UI", 10F);
        // A estrela logo depois do nome; equipe depois dos numeros
        // Jogos, Gols, Lesoes e Expuls. sao o historial do jogador no save
        // Score e Impacto: src/Score.cs (o botao "?" explica cada coluna)
        private static readonly string[] Titulos = { "Pos", "Nome", "✱", "Score", "País", "Força", "Nota", "Lesão", "Comport.", "Impacto", "Jogos", "Gols", "Lesões", "Expuls.", "Equipe", "Div.", "Salário", "Situação" };
        private static readonly int[] Larguras = { 46, 200, 30, 72, 80, 70, 62, 66, 106, 80, 66, 58, 72, 76, 170, 56, 96, 84 };

        private sealed class Linha
        {
            public SavePlayer J = null!;
            public SaveTeam Equipe = null!;
            public int Pos;
            public int OrdemDivisao;
            public string Situacao = "";
            public bool Humana;
            public int Score;
            public double Impacto;
        }

        private readonly string _jogosDir, _gameDir;
        private readonly Dictionary<string, Pais> _porCodigo;
        private readonly Dictionary<string, Image?> _bandeiras = new Dictionary<string, Image?>();
        private readonly RegrasEquipe _regras;
        private List<Linha> _todos = new List<Linha>(), _visiveis = new List<Linha>();
        private List<SaveTeam> _equipesOrdenadas = new List<SaveTeam>();
        private List<string?> _paisesFiltro = new List<string?>();
        private readonly List<Criterio> _criterios = new List<Criterio> { new Criterio { Coluna = 5, Desc = true } };
        private bool _montando;

        private ComboBox _saveSel = null!, _pais = null!, _divisao = null!, _equipe = null!, _notaMin = null!, _lesaoMax = null!, _compMax = null!;
        private TextBox _nome = null!, _forcaMin = null!, _forcaMax = null!;
        private readonly CheckBox[] _posicoes = new CheckBox[4];
        private CheckBox _soEstrelas = null!, _soEstrangeiros = null!, _semIndisponiveis = null!, _semHumanas = null!;
        private ListView _lista = null!;
        private Label _conta = null!;

        public ScoutForm(string jogosDir, string gameDir)
        {
            _jogosDir = jogosDir;
            _gameDir = gameDir;
            var paises = new List<Pais>();
            try { paises = TeamCodec.LerPaises(gameDir); } catch { }
            _porCodigo = paises.ToDictionary(p => p.Codigo);
            _regras = new RegrasEquipe(gameDir, paises);

            Text = "Scout";
            ClientSize = new Size(1720, 900);
            MinimumSize = new Size(960, 560);
            Tela.CaberNaTela(this);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Verde;
            Font = Fonte;
            BuildUi();
            LimparFiltros();
            Shown += (_, _) => ListarSaves(false);
        }

        // ---- pecas ----

        private static Label Texto(string t, Font f, Color cor) => new Label { Text = t, Font = f, ForeColor = cor, AutoSize = true, BackColor = Color.Transparent };

        private static Button BotaoClaro(string texto, int largura) => new BotaoJogo
        {
            Text = texto,
            Width = largura,
            Height = 30,
            BackColor = Color.FromArgb(0xE6, 0xE6, 0xE6),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
        };

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

        private string NomePais(string codigo) => _porCodigo.TryGetValue(codigo, out var p) ? p.Nome : codigo;

        private static string Milhar(long v) => v.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

        private static TextFormatFlags Formato(HorizontalAlignment a) => TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            (a == HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : a == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);

        private static HorizontalAlignment Alinhamento(int i) =>
            i == 1 || i == 4 || i == 8 || i == 14 || i == 17 ? HorizontalAlignment.Left
            : i == 0 || i == 2 || i == 15 ? HorizontalAlignment.Center : HorizontalAlignment.Right;

        // ---- montagem ----

        private void BuildUi()
        {
            var barra = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = VerdeTopo };
            var titulo = Texto("Scout", FonteTitulo, Amarelo);
            titulo.Location = new Point(14, 10);
            var sub = Texto("os melhores jogadores do seu jogo", Fonte, CinzaFaixa);
            sub.Location = new Point(100, 20);
            barra.Controls.AddRange(new Control[] { titulo, sub });
            Resize += (_, _) => sub.Visible = ClientSize.Width > 1200;  // estreita: sem o subtitulo
            var topo = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 11, 10, 0), BackColor = Color.Transparent };
            var rs = Texto("Save", Fonte, Cinza);
            rs.Margin = new Padding(0, 6, 4, 0);
            _saveSel = new ComboBox { Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 2, 8, 0) };
            _saveSel.SelectedIndexChanged += (_, _) => CarregarSave();
            var recarregar = BotaoClaro("Recarregar", 100);
            recarregar.Click += (_, _) => ListarSaves(true);
            var fechar = BotaoClaro("Fechar", 90);
            fechar.Margin = new Padding(6, 0, 0, 0);
            fechar.Click += (_, _) => Close();
            var botaoFiltros = BotaoClaro("Filtros ◂", 100);
            botaoFiltros.Margin = new Padding(0, 0, 6, 0);
            var ajuda = BotaoClaro("?", 34);
            ajuda.Margin = new Padding(0, 0, 6, 0);
            ajuda.Click += (_, _) => MessageBox.Show(this, Score.Ajuda, "O que significa cada coluna", MessageBoxButtons.OK, MessageBoxIcon.Information);
            topo.Controls.AddRange(new Control[] { rs, _saveSel, botaoFiltros, ajuda, recarregar, fechar });
            barra.Controls.Add(topo);

            // Filtros
            var filtros = new Panel { Dock = DockStyle.Left, Width = 340, BackColor = Cartao, AutoScroll = true, Padding = new Padding(12) };
            int y = 8;
            Label Rot(string t) { var l = Texto(t, Fonte, Cinza); l.Location = new Point(12, y); y += 22; filtros.Controls.Add(l); return l; }
            T Pos<T>(T c, int largura, int altura = 30) where T : Control { c.Location = new Point(12, y); c.Width = largura; filtros.Controls.Add(c); y += altura + 10; return c; }
            var tf = Texto("FILTROS", FonteCartao, Amarelo);
            tf.Location = new Point(12, y);
            filtros.Controls.Add(tf);
            y += 26;
            Rot("Nome");
            _nome = Pos(new TextBox(), 300, 24);
            _nome.TextChanged += (_, _) => Filtrar();
            Rot("Posição");
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var b = new CheckBox
                {
                    Appearance = Appearance.Button,
                    Text = TeamCodec.PosicoesCurtas[i],
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = FonteNegrito,
                    FlatStyle = FlatStyle.Flat,
                    Size = new Size(46, 32),
                    Location = new Point(12 + 52 * i, y),
                    Checked = true,
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.CheckedBackColor = CoresPosicao[i];
                b.CheckedChanged += (_, _) => { PintarPosicao(k); Filtrar(); };
                _posicoes[i] = b;
                filtros.Controls.Add(b);
                PintarPosicao(i);
            }
            y += 42;
            Rot("País do jogador");
            _pais = Pos(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 300);
            Rot("Divisão");
            _divisao = Pos(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 300);
            Rot("Equipe");
            _equipe = Pos(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 300);
            Rot("Força (de / até)");
            _forcaMin = new TextBox { Location = new Point(12, y), Width = 80 };
            _forcaMax = new TextBox { Location = new Point(110, y), Width = 80 };
            filtros.Controls.AddRange(new Control[] { _forcaMin, _forcaMax });
            y += 34;
            var rn = Texto("Nota", Fonte, Cinza);
            rn.Location = new Point(12, y);
            var rl = Texto("Lesão", Fonte, Cinza);
            rl.Location = new Point(165, y);
            filtros.Controls.AddRange(new Control[] { rn, rl });
            y += 22;
            _notaMin = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, y), Width = 140 };
            _lesaoMax = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(165, y), Width = 140 };
            filtros.Controls.AddRange(new Control[] { _notaMin, _lesaoMax });
            y += 40;
            Rot("Comportamento");
            _compMax = Pos(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 300);
            CheckBox Marcar(string t)
            {
                var c = new CheckBox { Text = t, ForeColor = Color.White, AutoSize = true, Location = new Point(12, y), BackColor = Color.Transparent };
                c.CheckedChanged += (_, _) => Filtrar();
                filtros.Controls.Add(c);
                y += 28;
                return c;
            }
            _soEstrelas = Marcar("Só com estrela (✱)");
            _soEstrangeiros = Marcar("Só estrangeiros (contam no limite)");
            _semIndisponiveis = Marcar("Esconder suspensos e lesionados");
            _semHumanas = Marcar("Esconder as equipes humanas");
            y += 6;
            var limpar = Pos(BotaoClaro("Limpar filtros", 120), 120);
            limpar.Click += (_, _) => LimparFiltros();
            _notaMin.Items.Add("Qualquer");
            for (int n = 2; n <= 10; n++) _notaMin.Items.Add($"{n} ou mais");
            _lesaoMax.Items.Add("Qualquer");
            for (int n = 0; n <= 9; n++) _lesaoMax.Items.Add($"até {n}");
            _compMax.Items.Add("Qualquer");
            for (int c = 0; c < 6; c++) _compMax.Items.Add(c == 0 ? "Só Fair Play" : "Até " + SaveCodec.ComportamentoLabels[c]);
            foreach (var c in new[] { _pais, _divisao, _equipe, _notaMin, _lesaoMax, _compMax }) c.SelectedIndexChanged += (_, _) => Filtrar();
            foreach (var t in new[] { _forcaMin, _forcaMax }) t.TextChanged += (_, _) => Filtrar();

            // Tabela
            _lista = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                OwnerDraw = true,
                VirtualMode = true,
                BorderStyle = BorderStyle.None,
                BackColor = Cartao,
                ForeColor = Color.White,
                Font = FonteLista,
            };
            for (int i = 0; i < Titulos.Length; i++) _lista.Columns.Add(Titulos[i], Larguras[i], Alinhamento(i));
            _lista.SmallImageList = new ImageList { ImageSize = new Size(1, 26) };
            _lista.RetrieveVirtualItem += (s, e) =>
            {
                var l = _visiveis[e.ItemIndex];
                e.Item = new ListViewItem(Celulas(l)) { Tag = l };
            };
            _lista.ColumnClick += (_, e) => Ordenar(e.Column);
            _lista.DrawColumnHeader += (s, e) =>
            {
                using (var b = new SolidBrush(Cartao)) e.Graphics.FillRectangle(b, e.Bounds);
                var t = Titulos[e.ColumnIndex] + MarcaOrdem(e.ColumnIndex);
                TextRenderer.DrawText(e.Graphics, t, FonteCartao, Rectangle.Inflate(e.Bounds, -4, 0), Amarelo, Formato(Alinhamento(e.ColumnIndex)));
            };
            _lista.DrawSubItem += DesenharCelula;
            _conta = Texto("", FontePequena, Cinza);
            _conta.Dock = DockStyle.Bottom;
            _conta.AutoSize = false;
            _conta.Height = 22;
            var tabela = new Panel { Dock = DockStyle.Fill, BackColor = Cartao, Padding = new Padding(8) };
            tabela.Controls.Add(_lista);
            tabela.Controls.Add(_conta);
            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Verde };
            var espaco = new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Verde };
            corpo.Controls.Add(tabela);
            corpo.Controls.Add(espaco);
            corpo.Controls.Add(filtros);
            // Recolher os filtros da a largura toda para a tabela
            botaoFiltros.Click += (_, _) =>
            {
                filtros.Visible = espaco.Visible = !filtros.Visible;
                botaoFiltros.Text = filtros.Visible ? "Filtros ◂" : "Filtros ▸";
            };
            Controls.Add(corpo);
            Controls.Add(barra);
        }

        private void PintarPosicao(int i)
        {
            var b = _posicoes[i];
            b.BackColor = b.Checked ? CoresPosicao[i] : Desligado;
            b.ForeColor = b.Checked ? Color.White : Color.FromArgb(0xA9, 0xC4, 0xA9);
        }

        private string[] Celulas(Linha l)
        {
            var j = l.J;
            string sl = ((j.Suspensao > 0 ? "S" + j.Suspensao : "") + (j.JogosLesionado > 0 ? " L" + j.JogosLesionado : "")).Trim();
            string sit = sl.Length > 0 ? sl : l.Situacao == "Estrangeiro" ? "Estrang." : l.Situacao;
            return new[] { j.Posicao, j.Nome, j.Estrela ? "✱" : "", l.Score.ToString(), j.Pais, j.Forca.ToString(), j.Nota.ToString(), j.Lesao.ToString(),
                SaveCodec.ComportamentoLabels[Math.Max(0, Math.Min(5, j.Comportamento))], Score.TextoImpacto(l.Impacto),
                j.Jogos.ToString(), j.Gols.ToString(), j.Lesoes.ToString(), j.Expulsoes.ToString(), l.Equipe.NomeExibido,
                l.OrdemDivisao <= 4 ? l.OrdemDivisao + "ª" : l.OrdemDivisao == 5 ? "Dist." : "—", Milhar(j.Salario), sit };
        }

        private void DesenharCelula(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item?.Tag is not Linha l || e.SubItem == null) return;
            var fundo = e.Item.Selected ? Selecao : e.ItemIndex % 2 == 0 ? LinhaPar : Cartao;
            using (var b = new SolidBrush(fundo)) e.Graphics.FillRectangle(b, e.Bounds);
            if (e.ColumnIndex == 0)
            {
                var selo = new Rectangle(e.Bounds.X + (e.Bounds.Width - 28) / 2, e.Bounds.Y + 4, 28, e.Bounds.Height - 8);
                using (var b = new SolidBrush(l.Pos >= 0 ? CoresPosicao[l.Pos] : Color.Gray)) e.Graphics.FillRectangle(b, selo);
                TextRenderer.DrawText(e.Graphics, l.J.Posicao, FonteNegrito, selo, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            if (e.ColumnIndex == 4)
            {
                var img = Bandeira(l.J.Pais);
                if (img != null) e.Graphics.DrawImage(img, e.Bounds.X + 6, e.Bounds.Y + 6, 21, 14);
                TextRenderer.DrawText(e.Graphics, l.J.Pais, _lista.Font, new Rectangle(e.Bounds.X + 32, e.Bounds.Y, e.Bounds.Width - 32, e.Bounds.Height),
                    Color.White, TextFormatFlags.VerticalCenter);
                return;
            }
            var cor = Color.White;
            if (e.ColumnIndex == 14 && l.Humana) cor = Amarelo;
            else if (e.ColumnIndex == 5 && l.J.Forca > SaveCodec.FORCA_WARN_ABOVE) cor = Amarelo;
            else if (e.ColumnIndex == 2 || e.ColumnIndex == 3) cor = Ouro;
            else if (e.ColumnIndex == 9) cor = l.Impacto > 0.004 ? Ok : l.Impacto < -0.004 ? Alerta : Color.White;
            else if (e.ColumnIndex == 17) cor = e.SubItem.Text == "" || e.SubItem.Text == "Bosman" || e.SubItem.Text == "PLOP" ? Ok : Alerta;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, e.ColumnIndex == 1 || e.ColumnIndex == 3 || e.ColumnIndex == 5 ? FonteNegrito : _lista.Font,
                Rectangle.Inflate(e.Bounds, -6, 0), cor, Formato(Alinhamento(e.ColumnIndex)));
        }

        // ---- dados ----

        private void ListarSaves(bool manter)
        {
            var antes = manter ? _saveSel.SelectedItem as string : null;
            var arquivos = Directory.Exists(_jogosDir)
                ? Directory.GetFiles(_jogosDir).Where(f => f.EndsWith(".e98", StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).OrderBy(n => n).ToList()
                : new List<string>();
            _saveSel.Items.Clear();
            foreach (var a in arquivos) _saveSel.Items.Add(a);
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
                Filtrar();
                _conta.Text = "Não consegui ler o save: " + ex.Message;
                return;
            }
            var humanas = new HashSet<SaveTeam>(sf.Tecnicos.Where(t => t.Humano).Select(sf.TimeDoTecnico).Where(t => t != null)!);
            _todos = sf.Teams.SelectMany(t => t.Players.Select(j => new Linha
            {
                J = j,
                Equipe = t,
                Pos = Array.IndexOf(TeamCodec.PosicoesCurtas, j.Posicao),
                OrdemDivisao = OrdemDivisao(t.Divisao),
                Situacao = _regras.Situacao(j.Pais, t.Pais),
                Humana = humanas.Contains(t),
                Score = Score.Rendimento(j.Posicao, j.Nota, j.Lesao, j.Comportamento),
                Impacto = Score.Impacto(j.Posicao, j.Nota, j.Lesao, j.Comportamento),
            })).ToList();

            _montando = true;
            _pais.Items.Clear();
            _paisesFiltro = new List<string?> { null };
            _pais.Items.Add($"Todos ({_todos.Count})");
            foreach (var g in _todos.GroupBy(l => l.J.Pais).OrderBy(g => NomePais(g.Key), StringComparer.Create(new CultureInfo("pt-BR"), true)))
            {
                _paisesFiltro.Add(g.Key);
                _pais.Items.Add($"{NomePais(g.Key)} ({g.Count()})");
            }
            _pais.SelectedIndex = 0;
            _divisao.Items.Clear();
            _divisao.Items.Add("Todas");
            foreach (var d in sf.Teams.Select(t => t.Divisao).Where(d => d.Length > 0).Distinct().OrderBy(OrdemDivisao)) _divisao.Items.Add(d);
            _divisao.SelectedIndex = 0;
            _equipesOrdenadas = sf.Teams.OrderBy(t => t.NomeExibido).ToList();
            _equipe.Items.Clear();
            _equipe.Items.Add("Todas");
            foreach (var t in _equipesOrdenadas) _equipe.Items.Add((humanas.Contains(t) ? "★ " : "") + t.NomeExibido);
            _equipe.SelectedIndex = 0;
            _montando = false;
            Filtrar();
        }

        private void LimparFiltros()
        {
            _montando = true;
            _nome.Text = "";
            foreach (var b in _posicoes) b.Checked = true;
            foreach (var c in new[] { _pais, _divisao, _equipe, _notaMin, _lesaoMax, _compMax }) if (c.Items.Count > 0) c.SelectedIndex = 0;
            _forcaMin.Text = _forcaMax.Text = "";
            foreach (var cb in new[] { _soEstrelas, _soEstrangeiros, _semIndisponiveis, _semHumanas }) cb.Checked = false;
            _criterios.Clear();
            _criterios.Add(new Criterio { Coluna = 5, Desc = true });
            _montando = false;
            Filtrar();
        }

        private static int? Numero(TextBox tb) => int.TryParse(tb.Text.Trim(), out var v) ? v : (int?)null;

        private void Filtrar()
        {
            if (_montando) return;
            var termo = _nome.Text.Trim();
            var pais = _pais.SelectedIndex > 0 && _pais.SelectedIndex < _paisesFiltro.Count ? _paisesFiltro[_pais.SelectedIndex] : null;
            var divisao = _divisao.SelectedIndex > 0 ? _divisao.SelectedItem as string : null;
            var equipe = _equipe.SelectedIndex > 0 && _equipe.SelectedIndex - 1 < _equipesOrdenadas.Count ? _equipesOrdenadas[_equipe.SelectedIndex - 1] : null;
            int? fMin = Numero(_forcaMin), fMax = Numero(_forcaMax);
            int notaMin = _notaMin.SelectedIndex > 0 ? _notaMin.SelectedIndex + 1 : 0;
            int lesaoMax = _lesaoMax.SelectedIndex > 0 ? _lesaoMax.SelectedIndex - 1 : 10;
            int compMax = _compMax.SelectedIndex > 0 ? _compMax.SelectedIndex - 1 : 5;
            var lista = _todos.Where(l =>
                (termo.Length == 0 || l.J.Nome.IndexOf(termo, StringComparison.CurrentCultureIgnoreCase) >= 0) &&
                l.Pos >= 0 && _posicoes[l.Pos].Checked &&
                (pais == null || l.J.Pais == pais) &&
                (divisao == null || l.Equipe.Divisao == divisao) &&
                (equipe == null || l.Equipe == equipe) &&
                (fMin == null || l.J.Forca >= fMin) && (fMax == null || l.J.Forca <= fMax) &&
                l.J.Nota >= notaMin && l.J.Lesao <= lesaoMax && l.J.Comportamento <= compMax &&
                (!_soEstrelas.Checked || l.J.Estrela) &&
                (!_soEstrangeiros.Checked || l.Situacao == "Estrangeiro") &&
                (!_semIndisponiveis.Checked || (l.J.Suspensao == 0 && l.J.JogosLesionado == 0)) &&
                (!_semHumanas.Checked || !l.Humana)).ToList();
            _visiveis = OrdenarLista(lista);
            _lista.VirtualListSize = 0;
            _lista.VirtualListSize = _visiveis.Count;
            _lista.Invalidate();
            if (_todos.Count > 0) _conta.Text = $"{_visiveis.Count} de {_todos.Count} jogadores · clique nos títulos para ordenar por várias colunas (o 3º clique tira a coluna)";
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
