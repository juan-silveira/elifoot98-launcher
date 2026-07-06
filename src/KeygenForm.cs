using System;
using System.Drawing;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class KeygenForm : Form
    {
        private readonly GameLauncher _launcher;

        private readonly TextBox _txtSenha;
        private readonly ComboBox _cmbTipo;
        private readonly TextBox _txtContraSenha;

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
            ClientSize = new Size(560, 400);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var lblSenha = new Label
            {
                Text = "Senha (mostrada pelo Elifoot no diálogo Registo):",
                Location = new Point(20, 20),
                AutoSize = true,
            };
            _txtSenha = new TextBox
            {
                Location = new Point(20, 42),
                Width = 520,
                Font = new Font(FontFamily.GenericMonospace, 11F),
            };

            var lblTipo = new Label
            {
                Text = "Tipo de registro:",
                Location = new Point(20, 84),
                AutoSize = true,
            };
            _cmbTipo = new ComboBox
            {
                Location = new Point(20, 106),
                Width = 520,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (var (_, label) in Tipos) _cmbTipo.Items.Add(label);
            _cmbTipo.SelectedIndex = 0;

            var btnAbrir = new Button
            {
                Text = "Abrir CRACK (Ctrl+F5 pra colar no DOSBox)",
                Location = new Point(20, 150),
                Size = new Size(520, 40),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            };
            btnAbrir.Click += (s, e) => AbrirCrack();

            var lblAjuda = new Label
            {
                Text = "1) Preencha a Senha e escolha o tipo\n" +
                       "2) Clique acima — o texto já está no clipboard do Windows\n" +
                       "3) No DOSBox, aperte Ctrl+F5 pra colar as respostas\n" +
                       "4) Anote a Contra-Senha que aparecer, feche o DOSBox\n" +
                       "5) Cole a Contra-Senha abaixo pra copiar de volta pra Elifoot",
                Location = new Point(20, 200),
                Size = new Size(520, 90),
                ForeColor = Color.FromArgb(90, 90, 90),
            };

            var lblResult = new Label
            {
                Text = "Contra-Senha (o que apareceu no CRACK):",
                Location = new Point(20, 296),
                AutoSize = true,
            };
            _txtContraSenha = new TextBox
            {
                Location = new Point(20, 318),
                Width = 400,
                Font = new Font(FontFamily.GenericMonospace, 11F),
            };
            var btnCopy = new Button
            {
                Text = "Copiar",
                Location = new Point(430, 316),
                Size = new Size(110, 28),
            };
            btnCopy.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(_txtContraSenha.Text))
                {
                    Clipboard.SetText(_txtContraSenha.Text.Trim());
                    MessageBox.Show(this, "Contra-Senha copiada. Cole no Elifoot.",
                        "Copiado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };

            var btnFechar = new Button
            {
                Text = "Fechar",
                Location = new Point(430, 358),
                Size = new Size(110, 28),
                DialogResult = DialogResult.OK,
            };
            CancelButton = btnFechar;

            Controls.AddRange(new Control[]
            {
                lblSenha, _txtSenha,
                lblTipo, _cmbTipo,
                btnAbrir,
                lblAjuda,
                lblResult, _txtContraSenha, btnCopy,
                btnFechar,
            });
        }

        private void AbrirCrack()
        {
            var senha = _txtSenha.Text.Trim();
            if (senha.Length == 0)
            {
                MessageBox.Show(this, "Preencha a Senha primeiro.",
                    "Senha vazia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var tipo = Tipos[_cmbTipo.SelectedIndex].Num;

            // Coloca no clipboard as respostas pra sequencia: tipo, senha, S (sair).
            // No DOSBox-Staging, Ctrl+F5 cola clipboard char por char via BIOS.
            var payload = $"{tipo}\r\n{senha}\r\nS\r\n";
            try { Clipboard.SetText(payload); } catch { /* ignora falha rara */ }

            try
            {
                _launcher.LaunchCrack();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
