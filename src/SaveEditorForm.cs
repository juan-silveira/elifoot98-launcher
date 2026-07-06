using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Editor de save (.e98). MVP v0.4: apenas Verba do clube.
    // Nomes/atributos de jogador ficam pra proxima versao (cifra ainda
    // nao 100% mapeada).
    public class SaveEditorForm : Form
    {
        private readonly string _jogosDir;
        private ComboBox _saveSel = null!;
        private TextBox _verbaField = null!;
        private Label _verbaOffsetLbl = null!;
        private Button _save = null!;
        private SaveFile? _current;
        private string _currentPath = "";

        public SaveEditorForm(string jogosDir)
        {
            _jogosDir = jogosDir;
            Text = "Editor de Save";
            ClientSize = new Size(440, 240);
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
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _saveSel.SelectedIndexChanged += (s, e) => LoadSelected();
            Controls.Add(_saveSel);
            var reload = new Button
            {
                Text = "Recarregar",
                Location = new Point(310, 14),
                Width = 110,
                FlatStyle = FlatStyle.System,
            };
            reload.Click += (s, e) => RefreshSaveList();
            Controls.Add(reload);

            var gb = new GroupBox
            {
                Text = "Clube",
                Location = new Point(16, 55),
                Size = new Size(408, 100),
            };
            Controls.Add(gb);

            gb.Controls.Add(new Label
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
            gb.Controls.Add(_verbaField);
            _verbaOffsetLbl = new Label
            {
                Text = "",
                Location = new Point(15, 60),
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8F),
            };
            gb.Controls.Add(_verbaOffsetLbl);

            _save = new Button
            {
                Text = "Salvar",
                Location = new Point(240, 175),
                Width = 90,
                FlatStyle = FlatStyle.System,
                Enabled = false,
            };
            _save.Click += (s, e) => SaveCurrent();
            Controls.Add(_save);

            var cancel = new Button
            {
                Text = "Fechar",
                Location = new Point(335, 175),
                Width = 90,
                FlatStyle = FlatStyle.System,
            };
            cancel.Click += (s, e) => Close();
            Controls.Add(cancel);

            var note = new Label
            {
                Text = "Backup .bak criado automaticamente na primeira gravação.",
                Location = new Point(16, 210),
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
                if (_current.VerbaOffset < 0)
                {
                    _verbaField.Text = "";
                    _verbaOffsetLbl.Text = "Não foi possível localizar a verba neste save.";
                    _save.Enabled = false;
                }
                else
                {
                    _verbaField.Text = _current.Verba.ToString();
                    _verbaOffsetLbl.Text = $"Offset detectado: 0x{_current.VerbaOffset:X}";
                    _save.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Falha ao ler save:\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _current = null;
                _save.Enabled = false;
            }
        }

        private void SaveCurrent()
        {
            if (_current == null || string.IsNullOrEmpty(_currentPath)) return;
            if (!long.TryParse(_verbaField.Text, out var verba) || verba < 0)
            {
                MessageBox.Show(this, "Verba deve ser número inteiro positivo.",
                    "Valor inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _current.Verba = verba;
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
    }
}
