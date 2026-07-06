using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class SaveEditorForm : Form
    {
        private readonly string _jogosDir;
        private ComboBox _saveSel = null!;
        private TextBox _verbaField = null!;
        private ListView _players = null!;
        private Button _save = null!;
        private SaveFile? _current;
        private string _currentPath = "";

        public SaveEditorForm(string jogosDir)
        {
            _jogosDir = jogosDir;
            Text = "Editor de Save";
            ClientSize = new Size(620, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildUi();
            RefreshSaveList(preserveSelection: false);
        }

        private void BuildUi()
        {
            Controls.Add(new Label { Text = "Save:", Location = new Point(16, 18), AutoSize = true });
            _saveSel = new ComboBox
            {
                Location = new Point(60, 15),
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _saveSel.SelectedIndexChanged += (s, e) => LoadSelected();
            Controls.Add(_saveSel);
            var reload = new Button
            {
                Text = "Recarregar",
                Location = new Point(370, 14),
                Width = 100,
                FlatStyle = FlatStyle.System,
            };
            reload.Click += (s, e) => RefreshSaveList(preserveSelection: true);
            Controls.Add(reload);

            var gbClube = new GroupBox
            {
                Text = "Clube",
                Location = new Point(16, 55),
                Size = new Size(588, 65),
            };
            Controls.Add(gbClube);
            gbClube.Controls.Add(new Label
            {
                Text = "Verba (Reais):",
                Location = new Point(15, 30),
                AutoSize = true,
            });
            _verbaField = new TextBox
            {
                Location = new Point(115, 27),
                Width = 200,
            };
            gbClube.Controls.Add(_verbaField);

            var gbPlayers = new GroupBox
            {
                Text = "Jogadores (duplo-clique pra editar força ou salário)",
                Location = new Point(16, 130),
                Size = new Size(588, 340),
            };
            Controls.Add(gbPlayers);
            _players = new ListView
            {
                Location = new Point(10, 20),
                Size = new Size(568, 310),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
            };
            _players.Columns.Add("#", 30);
            _players.Columns.Add("Pos", 40);
            _players.Columns.Add("Nome", 260);
            _players.Columns.Add("Força", 80);
            _players.Columns.Add("Salário", 100);
            gbPlayers.Controls.Add(_players);
            _players.MouseDoubleClick += Players_DoubleClick;

            _save = new Button
            {
                Text = "Salvar",
                Location = new Point(420, 480),
                Width = 90,
                FlatStyle = FlatStyle.System,
                Enabled = false,
            };
            _save.Click += (s, e) => SaveCurrent();
            Controls.Add(_save);

            var cancel = new Button
            {
                Text = "Fechar",
                Location = new Point(515, 480),
                Width = 90,
                FlatStyle = FlatStyle.System,
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            var note = new Label
            {
                Text = "Backup .bak criado na primeira gravação. Força >50 emite aviso (jogo aceita até 9999).",
                Location = new Point(16, 520),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F),
            };
            Controls.Add(note);
        }

        private void Players_DoubleClick(object? sender, MouseEventArgs e)
        {
            var hit = _players.HitTest(e.Location);
            if (hit.Item?.Tag is not SavePlayer p) return;
            int colIdx = -1;
            for (int i = 0; i < hit.Item.SubItems.Count; i++)
                if (hit.Item.SubItems[i] == hit.SubItem) { colIdx = i; break; }
            // 0=#, 1=Pos, 2=Nome, 3=Forca, 4=Salario
            if (colIdx == 3) EditForca(hit.Item, p);
            else if (colIdx == 4) EditSalario(hit.Item, p);
            else EditForca(hit.Item, p); // padrao: força
        }

        private void EditForca(ListViewItem it, SavePlayer p)
        {
            var v = PromptForInt($"Nova força para {p.Nome}\n(normal 1-50, jogo aceita até 9999):",
                                 p.Forca, SaveCodec.FORCA_MIN, SaveCodec.FORCA_MAX);
            if (!v.HasValue) return;
            if (v.Value > SaveCodec.FORCA_WARN_ABOVE)
            {
                var r = MessageBox.Show(this,
                    $"Força {v.Value} é bem acima do normal (1-{SaveCodec.FORCA_WARN_ABOVE}).\n" +
                    "O jogo aceita, mas o jogador vai ficar SUPER forte. Continuar?",
                    "Aviso", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return;
            }
            p.Forca = v.Value;
            it.SubItems[3].Text = p.Forca.ToString();
        }

        private void EditSalario(ListViewItem it, SavePlayer p)
        {
            var v = PromptForInt($"Novo salário para {p.Nome}:",
                                 p.Salario, SaveCodec.SALARIO_MIN, SaveCodec.SALARIO_MAX);
            if (!v.HasValue) return;
            p.Salario = v.Value;
            it.SubItems[4].Text = p.Salario.ToString();
        }

        private void RefreshSaveList(bool preserveSelection)
        {
            string? previouslySelected = preserveSelection && _saveSel.SelectedItem != null
                ? _saveSel.SelectedItem.ToString()
                : null;

            _saveSel.SelectedIndexChanged -= OnSelChanged;
            _saveSel.Items.Clear();

            if (!Directory.Exists(_jogosDir))
            {
                MessageBox.Show(this, $"Pasta JOGOS não encontrada:\n{_jogosDir}",
                    "Sem saves", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _saveSel.SelectedIndexChanged += OnSelChanged;
                return;
            }

            var files = Directory.GetFiles(_jogosDir, "*.e98", SearchOption.TopDirectoryOnly);
            Array.Sort(files);
            foreach (var f in files)
                _saveSel.Items.Add(Path.GetFileName(f));

            _saveSel.SelectedIndexChanged += OnSelChanged;

            if (_saveSel.Items.Count > 0)
            {
                int idx = 0;
                if (previouslySelected != null)
                {
                    int found = _saveSel.Items.IndexOf(previouslySelected);
                    if (found >= 0) idx = found;
                }
                _saveSel.SelectedIndex = idx;
                LoadSelected();
            }
        }

        private void OnSelChanged(object? s, EventArgs e) => LoadSelected();

        private void LoadSelected()
        {
            if (_saveSel.SelectedItem == null) return;
            var name = _saveSel.SelectedItem.ToString()!;
            _currentPath = Path.Combine(_jogosDir, name);
            try
            {
                _current = SaveCodec.Read(_currentPath);
                Populate();
                _save.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Falha ao ler save:\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _current = null;
                _save.Enabled = false;
            }
        }

        private void Populate()
        {
            if (_current == null) return;
            if (_current.VerbaOffset < 0)
                _verbaField.Text = "(não localizado)";
            else
                _verbaField.Text = _current.Verba.ToString();

            _players.BeginUpdate();
            _players.Items.Clear();
            int n = 1;
            foreach (var p in _current.Players)
            {
                var it = new ListViewItem(n.ToString());
                it.SubItems.Add(GuessPosition(n, _current.Players.Count));
                it.SubItems.Add(p.Nome);
                it.SubItems.Add(p.Forca.ToString());
                it.SubItems.Add(p.Salario.ToString());
                it.Tag = p;
                _players.Items.Add(it);
                n++;
            }
            _players.EndUpdate();
        }

        // Elifoot 98 padroniza time em 2G + 4D + 5M + 5A = 16 jogadores.
        // Sem byte confirmado de posicao, inferimos pela ordem.
        private static string GuessPosition(int idx, int total)
        {
            if (idx <= 2) return "G";
            if (idx <= 6) return "D";
            if (idx <= 11) return "M";
            return "A";
        }

        private void SaveCurrent()
        {
            if (_current == null || string.IsNullOrEmpty(_currentPath)) return;

            if (!string.IsNullOrEmpty(_verbaField.Text) && _verbaField.Text != "(não localizado)")
            {
                if (!long.TryParse(_verbaField.Text, out var verba) || verba < 0)
                {
                    MessageBox.Show(this, "Verba deve ser número inteiro positivo.",
                        "Valor inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _current.Verba = verba;
            }

            var bak = _currentPath + ".bak";
            try
            {
                if (!File.Exists(bak)) File.Copy(_currentPath, bak);
                SaveCodec.Write(_currentPath, _current);
                MessageBox.Show(this, "Save gravado com sucesso.", "OK",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao gravar:\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private int? PromptForInt(string prompt, int defaultVal, int min, int max)
        {
            using var f = new Form
            {
                Width = 360,
                Height = 180,
                Text = "Editar valor",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
            };
            var lbl = new Label { Left = 10, Top = 10, Text = prompt, AutoSize = true };
            var tb = new TextBox { Left = 10, Top = 70, Width = 320, Text = defaultVal.ToString() };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 160, Top = 105, Width = 80 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 250, Top = 105, Width = 80 };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { lbl, tb, ok, cn });
            if (f.ShowDialog(this) != DialogResult.OK) return null;
            if (!int.TryParse(tb.Text, out int v))
            {
                MessageBox.Show(this, "Valor inválido.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (v < min || v > max)
            {
                MessageBox.Show(this, $"Valor deve estar entre {min} e {max}.",
                    "Fora dos limites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return v;
        }
    }
}
