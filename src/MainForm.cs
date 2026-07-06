using System;
using System.Drawing;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class MainForm : Form
    {
        public MainForm()
        {
            Text = "Elifoot 98 Launcher";
            ClientSize = new Size(400, 260);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var title = new Label
            {
                Text = "Elifoot 98",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, 12),
                Width = ClientSize.Width,
                Height = 32,
            };
            Controls.Add(title);

            var btnJogo = MakeButton("Jogar Elifoot 98", 60);
            var btnEditor = MakeButton("Editor de Equipes", 105);
            var btnCrack = MakeButton("Registrador (CRACK)", 150);
            var btnConfig = MakeButton("Configurações", 205, secondary: true);

            btnJogo.Click += (s, e) => MessageBox.Show("TODO: rodar ELIFOOT.EXE via otvdm");
            btnEditor.Click += (s, e) => MessageBox.Show("TODO: rodar EDITEQ.EXE via otvdm");
            btnCrack.Click += (s, e) => MessageBox.Show("TODO: rodar CRACK.EXE via DOSBox embutido");
            btnConfig.Click += (s, e) => MessageBox.Show("TODO: abrir SettingsForm");

            Controls.AddRange(new Control[] { btnJogo, btnEditor, btnCrack, btnConfig });
        }

        private Button MakeButton(string text, int top, bool secondary = false)
        {
            return new Button
            {
                Text = text,
                Location = new Point(60, top),
                Size = new Size(280, secondary ? 32 : 38),
                Font = new Font("Segoe UI", secondary ? 9F : 10F),
                FlatStyle = FlatStyle.System,
            };
        }
    }
}
