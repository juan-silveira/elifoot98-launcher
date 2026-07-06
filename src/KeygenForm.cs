using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class KeygenForm : Form
    {
        private readonly GameLauncher _launcher;

        private readonly TextBox _txtSenha;
        private readonly ComboBox _cmbTipo;
        private readonly TextBox _txtContraSenha;
        private readonly Button _btnGerar;
        private readonly Button _btnCopy;
        private readonly Label _lblStatus;

        private static readonly (int Num, string Label)[] Tipos =
        {
            (1, "1 - Registro simples"),
            (2, "2 - Registro VIP"),
            (3, "3 - Registro Super-VIP"),
            (4, "4 - Registro para amigo dos autores"),
            (5, "5 - Registro para grande amigo dos autores"),
            (6, "6 - Registro para experimentadores"),
            (7, "7 - Registro experimentador especial"),
            (8, "8 - Registro para autor 1"),
            (9, "9 - Registro para autor 2"),
        };

        public KeygenForm(GameLauncher launcher)
        {
            _launcher = launcher;

            Text = "Registrador (CRACK)";
            ClientSize = new Size(500, 320);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var lblSenha = new Label
            {
                Text = "Senha (mostrada pelo Elifoot):",
                Location = new Point(20, 20),
                AutoSize = true,
            };
            _txtSenha = new TextBox
            {
                Location = new Point(20, 42),
                Width = 460,
                Font = new Font(FontFamily.GenericMonospace, 11F),
            };

            var lblTipo = new Label
            {
                Text = "Tipo de registro:",
                Location = new Point(20, 82),
                AutoSize = true,
            };
            _cmbTipo = new ComboBox
            {
                Location = new Point(20, 104),
                Width = 460,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (var (_, label) in Tipos) _cmbTipo.Items.Add(label);
            _cmbTipo.SelectedIndex = 0;

            _btnGerar = new Button
            {
                Text = "Gerar Contra-Senha",
                Location = new Point(20, 148),
                Size = new Size(460, 40),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            };
            _btnGerar.Click += async (s, e) => await GerarAsync();

            _lblStatus = new Label
            {
                Text = "",
                Location = new Point(20, 194),
                Size = new Size(460, 20),
                ForeColor = Color.FromArgb(90, 90, 90),
            };

            var lblResult = new Label
            {
                Text = "Contra-Senha:",
                Location = new Point(20, 220),
                AutoSize = true,
            };
            _txtContraSenha = new TextBox
            {
                Location = new Point(20, 242),
                Width = 350,
                Font = new Font(FontFamily.GenericMonospace, 11F, FontStyle.Bold),
                ReadOnly = true,
                BackColor = Color.White,
            };
            _btnCopy = new Button
            {
                Text = "Copiar",
                Location = new Point(380, 240),
                Size = new Size(100, 28),
                Enabled = false,
            };
            _btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_txtContraSenha.Text))
                {
                    Clipboard.SetText(_txtContraSenha.Text.Trim());
                    _lblStatus.Text = "Contra-Senha copiada. Cole no diálogo Registo do Elifoot.";
                }
            };

            var btnFechar = new Button
            {
                Text = "Fechar",
                Location = new Point(380, 278),
                Size = new Size(100, 28),
                DialogResult = DialogResult.OK,
            };
            CancelButton = btnFechar;

            Controls.AddRange(new Control[]
            {
                lblSenha, _txtSenha,
                lblTipo, _cmbTipo,
                _btnGerar, _lblStatus,
                lblResult, _txtContraSenha, _btnCopy,
                btnFechar,
            });
        }

        private async Task GerarAsync()
        {
            var senha = _txtSenha.Text.Trim();
            if (senha.Length == 0)
            {
                MessageBox.Show(this, "Preencha a Senha primeiro.",
                    "Senha vazia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var tipo = Tipos[_cmbTipo.SelectedIndex].Num;

            _btnGerar.Enabled = false;
            _btnCopy.Enabled = false;
            _txtContraSenha.Text = "";
            _lblStatus.Text = "Rodando CRACK oculto... (pode levar ~10s)";
            Application.DoEvents();

            var res = await Task.Run(() => CrackHeadless.Generate(_launcher, tipo, senha));

            _btnGerar.Enabled = true;

            if (!res.Ok)
            {
                _lblStatus.Text = "Erro: " + res.Error;
                return;
            }

            _txtContraSenha.Text = res.ContraSenha;
            _btnCopy.Enabled = true;
            _lblStatus.Text = "Pronto! Clique Copiar e cole no Elifoot.";
        }
    }
}
