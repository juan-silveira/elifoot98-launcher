using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Editor de Arbitros (REFEREE.TXE) no visual verde dos outros editores.
    // Esquerda: a lista com bandeira. Direita: o arbitro escolhido, com o pais
    // escolhido entre os 217 do COUNTRY.TXE. "Original" volta a lista que veio
    // com o jogo (so grava ao Salvar).
    public class RefereeEditorForm : Form
    {
        private static readonly Color Verde = Color.FromArgb(0x0B, 0x3D, 0x0B), VerdeTopo = Color.FromArgb(0x06, 0x26, 0x06),
            Cartao = Color.FromArgb(0x14, 0x52, 0x14), LinhaPar = Color.FromArgb(0x11, 0x4A, 0x11), Selecao = Color.FromArgb(0x2E, 0x7D, 0x32),
            Amarelo = Color.FromArgb(0xFC, 0xFE, 0x04), Cinza = Color.FromArgb(0xB8, 0xC9, 0xB8), CinzaFaixa = Color.FromArgb(0x8F, 0xA8, 0x8F),
            Alerta = Color.FromArgb(0xFF, 0x8A, 0x80), SelecaoClara = Color.FromArgb(0xC8, 0xE6, 0xC9);
        private static readonly Font Fonte = new Font("Segoe UI", 9.5F), FonteNegrito = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            FontePequena = new Font("Segoe UI", 8F), FonteTitulo = new Font("Segoe UI", 16F, FontStyle.Bold),
            FonteCartao = new Font("Segoe UI", 8.5F, FontStyle.Bold), FonteLista = new Font("Segoe UI", 10F),
            FonteListaNegrito = new Font("Segoe UI", 10F, FontStyle.Bold);
        // Colunas da lista: numero, bandeira, codigo, nome
        private const int XNumero = 8, XBandeira = 52, XCodigo = 86, XNome = 140;

        private readonly string _refereeTxePath, _gameDir;
        private readonly List<Pais> _paises;
        private readonly Dictionary<string, Pais> _porCodigo = new Dictionary<string, Pais>();
        private readonly Dictionary<string, Image?> _bandeiras = new Dictionary<string, Image?>();
        private RefereeCodec.File _file = new RefereeCodec.File();
        private bool _alterado, _mostrando;
        private string _codigo = "";

        private ListBox _lista = null!, _listaPaises = null!;
        private Label _contagem = null!, _status = null!;
        private Panel _paisEscolhido = null!;
        private TextBox _busca = null!, _nome = null!;
        private Button _salvar = null!, _atualizar = null!, _remover = null!, _subir = null!, _descer = null!;

        public RefereeEditorForm(string refereeTxePath)
        {
            _refereeTxePath = refereeTxePath;
            _gameDir = Path.GetDirectoryName(refereeTxePath) ?? ".";
            try { _paises = TeamCodec.LerPaises(_gameDir); }
            catch { _paises = new List<Pais>(); }
            _paises = _paises.OrderBy(p => SemAcento(p.Nome), StringComparer.Ordinal).ToList();
            foreach (var p in _paises) _porCodigo[p.Codigo] = p;

            Text = "Editor de Árbitros";
            ClientSize = new Size(960, 700);
            MinimumSize = new Size(720, 480);
            Tela.CaberNaTela(this);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Verde;
            Font = Fonte;

            BuildUi();
            Carregar();
            FormClosing += (s, e) =>
            {
                if (_alterado && MessageBox.Show(this, "Há alterações não salvas nos árbitros. Fechar sem salvar?", "Fechar",
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

        private static string SemAcento(string s)
        {
            var n = s.Normalize(NormalizationForm.FormD);
            return new string(n.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
                .ToLowerInvariant();
        }

        // Bandeira, codigo e nome do pais num retangulo
        private void DesenharPais(Graphics g, Rectangle r, string codigo, string nome, Color cor)
        {
            var img = Bandeira(codigo);
            if (img != null) g.DrawImage(img, r.X + 4, r.Y + (r.Height - 16) / 2, 24, 16);
            TextRenderer.DrawText(g, codigo, FonteNegrito, new Rectangle(r.X + 34, r.Y, 42, r.Height), cor, TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, nome, Fonte, new Rectangle(r.X + 78, r.Y, r.Width - 80, r.Height), cor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        // ---- montagem ----

        private void BuildUi()
        {
            // Barra do topo: titulo, Original, Salvar e Fechar
            var barra = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = VerdeTopo };
            var titulo = Texto("Editor de Árbitros", FonteTitulo, Amarelo);
            titulo.Location = new Point(14, 10);
            barra.Controls.Add(titulo);
            var botoesTopo = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 11, 10, 0), BackColor = Color.Transparent };
            var original = Botao("Original", false, 100);
            original.Click += (_, _) => VoltarOriginal();
            _salvar = Botao("Salvar", true, 110);
            _salvar.Click += (_, _) => Salvar();
            var fechar = Botao("Fechar", false, 90);
            fechar.Click += (_, _) => Close();
            foreach (var b in new[] { original, _salvar, fechar }) { b.Margin = new Padding(6, 0, 0, 0); botoesTopo.Controls.Add(b); }
            barra.Controls.Add(botoesTopo);

            // Esquerda: a lista de arbitros
            var esquerda = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 6, 12), BackColor = Verde };
            var cartaoLista = new Panel { Dock = DockStyle.Fill, BackColor = Cartao, Padding = new Padding(12, 10, 12, 10) };
            var topoLista = new Panel { Dock = DockStyle.Top, Height = 22, BackColor = Cartao };
            var tl = Texto("ÁRBITROS", FonteCartao, Amarelo);
            tl.Dock = DockStyle.Left;
            _contagem = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopRight, Font = FontePequena, ForeColor = Cinza, BackColor = Color.Transparent };
            topoLista.Controls.Add(_contagem);
            topoLista.Controls.Add(tl);
            var cabecalho = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Cartao };
            foreach (var (t, x) in new[] { ("#", XNumero), ("País", XCodigo), ("Nome", XNome) })
            {
                var c = Texto(t, FonteCartao, Amarelo);
                c.Location = new Point(x, 4);
                cabecalho.Controls.Add(c);
            }
            _lista = new ListBox
            {
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 30,
                BorderStyle = BorderStyle.None,
                BackColor = Cartao,
                IntegralHeight = false,
            };
            _lista.DrawItem += DesenharArbitro;
            _lista.SelectedIndexChanged += (_, _) => MostrarSelecionado();
            _lista.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) Remover(); };
            cartaoLista.Controls.Add(_lista);
            cartaoLista.Controls.Add(cabecalho);
            cartaoLista.Controls.Add(topoLista);
            esquerda.Controls.Add(cartaoLista);

            // Direita: o arbitro escolhido (rola em telas baixas)
            var direita = new Panel { Dock = DockStyle.Right, Width = 384, AutoScroll = true, BackColor = Verde, AutoScrollMargin = new Size(0, 12) };
            var cartao = new Panel { Location = new Point(6, 12), Size = new Size(352, 590), BackColor = Cartao };
            var tc = Texto("ÁRBITRO", FonteCartao, Amarelo);
            tc.Location = new Point(12, 8);
            cartao.Controls.Add(tc);
            Rotulo(cartao, "País", "escolha na lista do jogo", 32);
            _paisEscolhido = new Panel { Location = new Point(12, 54), Size = new Size(328, 30), BackColor = Color.White };
            _paisEscolhido.Paint += (_, e) =>
            {
                var r = _paisEscolhido.ClientRectangle;
                if (_codigo.Length == 0)
                {
                    TextRenderer.DrawText(e.Graphics, "Escolha o país na lista abaixo", Fonte, Rectangle.Inflate(r, -6, 0), Color.Gray, TextFormatFlags.VerticalCenter);
                    return;
                }
                _porCodigo.TryGetValue(_codigo, out var p);
                DesenharPais(e.Graphics, r, _codigo, p?.Nome ?? "(fora da lista do jogo)", p != null ? Color.Black : Color.DarkRed);
            };
            cartao.Controls.Add(_paisEscolhido);
            Rotulo(cartao, "Buscar", "nome ou código do país", 92);
            _busca = new TextBox { Location = new Point(12, 114), Width = 328 };
            _busca.TextChanged += (_, _) => FiltrarPaises();
            _listaPaises = new ListBox
            {
                Location = new Point(12, 142),
                Size = new Size(328, 220),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 22,
                IntegralHeight = false,
                BackColor = Color.White,
            };
            _listaPaises.DrawItem += (_, e) =>
            {
                if (e.Index < 0 || e.Index >= _listaPaises.Items.Count) return;
                var p = (Pais)_listaPaises.Items[e.Index];
                bool sel = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(sel ? SelecaoClara : Color.White)) e.Graphics.FillRectangle(b, e.Bounds);
                DesenharPais(e.Graphics, e.Bounds, p.Codigo, p.Nome, Color.Black);
            };
            _listaPaises.SelectedIndexChanged += (_, _) =>
            {
                if (_mostrando || _listaPaises.SelectedItem is not Pais p) return;
                _codigo = p.Codigo;
                _paisEscolhido.Invalidate();
            };
            Rotulo(cartao, "Nome", "como aparece no jogo", 372);
            _nome = new TextBox { Location = new Point(12, 394), Width = 328, MaxLength = 251 };

            var adicionar = Botao("Adicionar", true, 100);
            adicionar.Click += (_, _) => Adicionar();
            _atualizar = Botao("Atualizar", false, 100);
            _atualizar.Click += (_, _) => Atualizar();
            _remover = Botao("Remover", false, 100);
            _remover.Click += (_, _) => Remover();
            _subir = Botao("↑ Subir", false, 100);
            _subir.Click += (_, _) => Mover(-1);
            _descer = Botao("↓ Descer", false, 100);
            _descer.Click += (_, _) => Mover(+1);
            adicionar.Location = new Point(12, 432);
            _atualizar.Location = new Point(120, 432);
            _remover.Location = new Point(228, 432);
            _subir.Location = new Point(12, 470);
            _descer.Location = new Point(120, 470);
            var dica = Texto("Adicionar entra logo abaixo do árbitro selecionado.", FontePequena, CinzaFaixa);
            dica.Location = new Point(12, 510);
            _status = new Label { Location = new Point(12, 532), Size = new Size(328, 48), Font = Fonte, ForeColor = Cinza, BackColor = Color.Transparent };
            cartao.Controls.AddRange(new Control[] { _busca, _listaPaises, _nome, adicionar, _atualizar, _remover, _subir, _descer, dica, _status });
            direita.Controls.Add(cartao);

            Controls.Add(esquerda);
            Controls.Add(direita);
            Controls.Add(barra);
        }

        // Rotulo com a explicacao do lado, em letra menor
        private static void Rotulo(Control pai, string rotulo, string faixa, int y)
        {
            var r = Texto(rotulo, Fonte, Cinza);
            r.Location = new Point(12, y);
            var f = Texto(faixa, FontePequena, CinzaFaixa);
            f.Location = new Point(12 + r.PreferredWidth + 6, y + 3);
            pai.Controls.Add(r);
            pai.Controls.Add(f);
        }

        private void DesenharArbitro(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _file.Records.Count) return;
            var a = _file.Records[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            var fundo = sel ? Selecao : e.Index % 2 == 0 ? LinhaPar : Cartao;
            using (var b = new SolidBrush(fundo)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = e.Bounds;
            var fmt = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, (e.Index + 1) + ".", FonteLista, new Rectangle(r.X + XNumero, r.Y, XBandeira - XNumero, r.Height), Cinza, fmt);
            var img = Bandeira(a.CountryCode);
            if (img != null) e.Graphics.DrawImage(img, r.X + XBandeira, r.Y + (r.Height - 16) / 2, 24, 16);
            TextRenderer.DrawText(e.Graphics, a.CountryCode, FonteListaNegrito, new Rectangle(r.X + XCodigo, r.Y, XNome - XCodigo, r.Height),
                _porCodigo.ContainsKey(a.CountryCode) ? Amarelo : Alerta, fmt);
            TextRenderer.DrawText(e.Graphics, a.Name, FonteListaNegrito, new Rectangle(r.X + XNome, r.Y, r.Width - XNome - 4, r.Height), Color.White, fmt);
        }

        // ---- estado ----

        private void Alterou(bool sim)
        {
            _alterado = sim;
            Text = sim ? "Editor de Árbitros — alterações não salvas" : "Editor de Árbitros";
            _salvar.Text = sim ? "Salvar •" : "Salvar";
            _contagem.Text = _file.Records.Count + " árbitros" + (sim ? " · alterações não salvas" : "");
        }

        private int Selecionado => _lista.SelectedIndex;

        private void MostrarLista(int selecionar)
        {
            _lista.BeginUpdate();
            _lista.Items.Clear();
            foreach (var a in _file.Records) _lista.Items.Add(a);
            _lista.EndUpdate();
            if (selecionar >= 0 && selecionar < _lista.Items.Count) _lista.SelectedIndex = selecionar;
            MostrarSelecionado();
        }

        // Campos da direita com o arbitro selecionado (o pais fica escolhido na lista)
        private void MostrarSelecionado()
        {
            int i = Selecionado;
            bool tem = i >= 0 && i < _file.Records.Count;
            _atualizar.Enabled = _remover.Enabled = tem;
            _subir.Enabled = tem && i > 0;
            _descer.Enabled = tem && i < _file.Records.Count - 1;
            if (!tem) { MostrarPais(); return; }
            var a = _file.Records[i];
            _codigo = a.CountryCode;
            _nome.Text = a.Name;
            if (_busca.Text.Length > 0) _busca.Text = "";  // FiltrarPaises mostra o pais
            else MostrarPais();
        }

        private void MostrarPais()
        {
            _paisEscolhido.Invalidate();
            _mostrando = true;
            _porCodigo.TryGetValue(_codigo, out var p);
            _listaPaises.SelectedIndex = p == null ? -1 : _listaPaises.Items.IndexOf(p);
            _mostrando = false;
        }

        private void FiltrarPaises()
        {
            var q = SemAcento(_busca.Text.Trim());
            _mostrando = true;
            _listaPaises.BeginUpdate();
            _listaPaises.Items.Clear();
            foreach (var p in _paises)
                if (q.Length == 0 || SemAcento(p.Nome).Contains(q) || SemAcento(p.Codigo).StartsWith(q, StringComparison.Ordinal))
                    _listaPaises.Items.Add(p);
            _listaPaises.EndUpdate();
            _mostrando = false;
            MostrarPais();
        }

        // ---- acoes ----

        private void Carregar()
        {
            FiltrarPaises();
            try
            {
                if (!File.Exists(_refereeTxePath))
                {
                    _status.Text = "REFEREE.TXE não encontrado.";
                    MostrarSelecionado();
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

        // Confere pais e nome dos campos; avisa se faltar algo
        private bool Campos(out string nome)
        {
            nome = _nome.Text.Trim();
            string? erro = _codigo.Length != 3 ? "Escolha o país do árbitro."
                : nome.Length == 0 ? "Digite o nome do árbitro."
                : nome.Any(c => c > 0xFF) ? "O nome tem letras que o jogo não mostra." : null;
            if (erro == null) return true;
            MessageBox.Show(this, erro, "Árbitro", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        private void Adicionar()
        {
            if (!Campos(out var nome)) return;
            int em = Selecionado >= 0 ? Selecionado + 1 : _file.Records.Count;
            _file.Records.Insert(em, new RefereeCodec.Record { CountryCode = _codigo, Name = nome });
            MostrarLista(em);
            Alterou(true);
            _status.Text = $"Adicionado na posição {em + 1}.";
        }

        private void Atualizar()
        {
            int i = Selecionado;
            if (i < 0 || i >= _file.Records.Count || !Campos(out var nome)) return;
            _file.Records[i].CountryCode = _codigo;
            _file.Records[i].Name = nome;
            MostrarLista(i);
            Alterou(true);
            _status.Text = $"Árbitro {i + 1} atualizado.";
        }

        private void Remover()
        {
            int i = Selecionado;
            if (i < 0 || i >= _file.Records.Count) return;
            var a = _file.Records[i];
            if (MessageBox.Show(this, $"Remover {a.Name} ({a.CountryCode}) da lista?", "Remover árbitro",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _file.Records.RemoveAt(i);
            MostrarLista(Math.Min(i, _file.Records.Count - 1));
            Alterou(true);
            _status.Text = $"{a.Name} removido.";
        }

        private void Mover(int direcao)
        {
            int i = Selecionado, j = i + direcao;
            if (i < 0 || j < 0 || j >= _file.Records.Count) return;
            var t = _file.Records[i];
            _file.Records[i] = _file.Records[j];
            _file.Records[j] = t;
            MostrarLista(j);
            Alterou(true);
        }

        // A lista que veio com o jogo; so vai pro disco ao Salvar
        private void VoltarOriginal()
        {
            if (MessageBox.Show(this,
                    "Voltar à lista de árbitros original do jogo? As mudanças só são gravadas quando você clicar em Salvar.",
                    "Árbitros originais", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
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
                MessageBox.Show(this, "Não consegui ler o original: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Salvar()
        {
            try
            {
                var backup = _refereeTxePath + ".bak";
                if (!File.Exists(backup) && File.Exists(_refereeTxePath))
                    File.Copy(_refereeTxePath, backup);
                RefereeCodec.Write(_file, _refereeTxePath);
                Alterou(false);
                _status.Text = "Salvo. Backup do anterior em REFEREE.TXE.bak";
                MessageBox.Show(this,
                    $"REFEREE.TXE salvo com {_file.Records.Count} árbitros.\n" +
                    $"Backup do anterior em: {backup}\n\n" +
                    "Da próxima vez que abrir o Elifoot, os árbitros novos aparecem.",
                    "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Erro ao salvar: " + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
