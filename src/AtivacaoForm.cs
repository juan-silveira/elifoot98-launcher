using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Pede a Senha mostrada pelo Elifoot (menu Registo) e gera a
    // Contra-Senha de "Registro para autor 2" com o CRACK.EXE escondido.
    public class AtivacaoForm : Form
    {
        private readonly GameLauncher _launcher;
        private readonly TextBox _txtSenha;
        private readonly TextBox _txtContraSenha;
        private readonly Button _btnGerar;
        private readonly Button _btnCopiar;
        private readonly Label _lblStatus;

        public AtivacaoForm(GameLauncher launcher)
        {
            _launcher = launcher;

            Text = "Ativar todos os recursos";
            ClientSize = new Size(420, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var lblInstrucao = new Label
            {
                Text = "1. Abra o Elifoot e vá no menu Registo.\n2. Digite abaixo a Senha que aparece lá.",
                Location = new Point(20, 16),
                Size = new Size(380, 36),
            };

            var lblSenha = new Label { Text = "Senha:", Location = new Point(20, 60), AutoSize = true };
            _txtSenha = new TextBox
            {
                Location = new Point(20, 80),
                Width = 380,
                Font = new Font(FontFamily.GenericMonospace, 11F),
            };

            _btnGerar = new Button
            {
                Text = "Gerar contra-senha",
                Location = new Point(20, 116),
                Size = new Size(380, 32),
                FlatStyle = FlatStyle.System,
            };
            _btnGerar.Click += async (s, e) => await GerarAsync();
            AcceptButton = _btnGerar;

            var lblContra = new Label { Text = "Contra-senha:", Location = new Point(20, 162), AutoSize = true };
            _txtContraSenha = new TextBox
            {
                Location = new Point(20, 182),
                Width = 290,
                ReadOnly = true,
                Font = new Font(FontFamily.GenericMonospace, 11F),
            };
            _btnCopiar = new Button
            {
                Text = "Copiar",
                Location = new Point(320, 180),
                Size = new Size(80, 28),
                Enabled = false,
            };
            _btnCopiar.Click += (s, e) =>
            {
                Clipboard.SetText(_txtContraSenha.Text);
                _lblStatus!.Text = "Copiada. Cole no campo Contra-senha do Registo do Elifoot.";
            };

            _lblStatus = new Label
            {
                Location = new Point(20, 222),
                Size = new Size(380, 36),
                ForeColor = Color.FromArgb(100, 100, 100),
            };

            var btnFechar = new Button
            {
                Text = "Fechar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(320, 262),
                Size = new Size(80, 28),
            };
            CancelButton = btnFechar;

            Controls.AddRange(new Control[]
            {
                lblInstrucao, lblSenha, _txtSenha, _btnGerar,
                lblContra, _txtContraSenha, _btnCopiar, _lblStatus, btnFechar,
            });
        }

        private async Task GerarAsync()
        {
            // So digitos e hifens chegam ao CRACK
            var senha = new string(_txtSenha.Text.Where(c => char.IsDigit(c) || c == '-').ToArray());
            if (!senha.Any(char.IsDigit))
            {
                MessageBox.Show(this, "Digite a Senha mostrada no Registo do Elifoot.",
                    "Senha vazia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnGerar.Enabled = false;
            _btnCopiar.Enabled = false;
            _txtContraSenha.Text = "";
            _lblStatus.Text = "Gerando a contra-senha, aguarde...";
            try
            {
                var contraSenha = await Task.Run(() => CrackRunner.GerarContraSenha(_launcher, senha));
                _txtContraSenha.Text = contraSenha;
                _btnCopiar.Enabled = true;
                Clipboard.SetText(contraSenha);
                _lblStatus.Text = "Contra-senha copiada. Cole no campo Contra-senha do Registo do Elifoot.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "";
                MessageBox.Show(this, ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnGerar.Enabled = true;
            }
        }
    }
}
