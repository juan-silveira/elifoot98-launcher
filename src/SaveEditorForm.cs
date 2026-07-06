using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Editor de save (.e98). Verba + salarios dos jogadores.
    public class SaveEditorForm : Form
    {
        private readonly string _jogosDir;
        private ComboBox _saveSel = null!;
        private TextBox _verbaField = null!;
        private ListView _players = null!;
        private Button _save = null!;
        private Button _editSalary = null!;
        private SaveFile? _current;
        private string _currentPath = "";

        public SaveEditorForm(string jogosDir)
        {
            _jogosDir = jogosDir;
            Text = "Editor de Save";
            ClientSize = new Size(560, 540);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            BuildUi();
            RefreshSaveList();
        }

        private void BuildUi()
        {
            Controls.Add(new Label
            {
                Text = "Save:",
                Location = new Point(16, 18),
                AutoSize = true,
            });
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
            reload.Click += (s, e) => RefreshSaveList();
            Controls.Add(reload);

            var gbClube = new GroupBox
            {
                Text = "Clube",
                Location = new Point(16, 55),
                Size = new Size(528, 65),
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
                Text = "Jogadores (duplo-clique pra editar salário)",
                Location = new Point(16, 130),
                Size = new Size(528, 320),
            };
            Controls.Add(gbPlayers);
            _players = new ListView
            {
                Location = new Point(10, 20),
                Size = new Size(508, 290),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
            };
            _players.Columns.Add("#", 30);
            _players.Columns.Add("Nome", 250);
            _players.Columns.Add("Salário", 100);
            gbPlayers.Controls.Add(_players);
            _players.DoubleClick += (s, e) => EditSalary();

            _editSalary = new Button
            {
                Text = "Editar salário",
                Location = new Point(16, 460),
                Width = 130,
                FlatStyle = FlatStyle.System,
            };
            _editSalary.Click += (s, e) => EditSalary();
            Controls.Add(_editSalary);

            _save = new Button
            {
                Text = "Salvar",
                Location = new Point(360, 460),
                Width = 90,
                FlatStyle = FlatStyle.System,
                Enabled = false,
            };
            _save.Click += (s, e) => SaveCurrent();
            Controls.Add(_save);

            var cancel = new Button
            {
                Text = "Fechar",
                Location = new Point(455, 460),
                Width = 90,
                FlatStyle = FlatStyle.System,
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            var note = new Label
            {
                Text = "Backup .bak criado na primeira gravação. Salários: 50 a 9999.",
                Location = new Point(16, 500),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F),
            };
            Controls.Add(note);
        }

        private void RefreshSaveList()
        {
            _saveSel.Items.Clear();
            if (!Directory.Exists(_jogosDir))
            {
                MessageBox.Show(this, $"Pasta JOGOS não encontrada:\n{_jogosDir}",
                    "Sem saves", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var files = Directory.GetFiles(_jogosDir, "*.e98", SearchOption.TopDirectoryOnly);
            Array.Sort(files);
            foreach (var f in files)
                _saveSel.Items.Add(Path.GetFileName(f));
            if (_saveSel.Items.Count > 0)
                _saveSel.SelectedIndex = 0;
        }

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
                it.SubItems.Add(p.Nome);
                it.SubItems.Add(p.Salario.ToString());
                it.Tag = p;
                _players.Items.Add(it);
                n++;
            }
            _players.EndUpdate();
        }

        private void EditSalary()
        {
            if (_players.SelectedItems.Count == 0) return;
            var it = _players.SelectedItems[0];
            if (it.Tag is not SavePlayer p) return;

            var (min, max) = SaveCodec.SalarioLimits;
            var nn = PromptForInt($"Novo salário para {p.Nome}\n(entre {min} e {max}):",
                                  p.Salario, min, max);
            if (nn.HasValue)
            {
                p.Salario = nn.Value;
                it.SubItems[2].Text = p.Salario.ToString();
            }
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

        private static int? PromptForInt(string prompt, int defaultVal, int min, int max)
        {
            using var f = new Form
            {
                Width = 340,
                Height = 160,
                Text = "Editar valor",
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
            };
            var lbl = new Label { Left = 10, Top = 10, Text = prompt, AutoSize = true };
            var tb = new TextBox { Left = 10, Top = 60, Width = 310, Text = defaultVal.ToString() };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 150, Top = 90, Width = 80 };
            var cn = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 240, Top = 90, Width = 80 };
            f.AcceptButton = ok;
            f.CancelButton = cn;
            f.Controls.AddRange(new Control[] { lbl, tb, ok, cn });
            if (f.ShowDialog() != DialogResult.OK) return null;
            if (!int.TryParse(tb.Text, out int v))
            {
                MessageBox.Show("Valor inválido.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (v < min || v > max)
            {
                MessageBox.Show($"Valor deve estar entre {min} e {max}.",
                    "Fora dos limites", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return v;
        }
    }
}
