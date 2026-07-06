using System;
using System.Drawing;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class MainForm : Form
    {
        private readonly GameLauncher _launcher = new GameLauncher();
        private LauncherConfig _config = LauncherConfig.Load();

        public MainForm()
        {
            Text = "Elifoot 98 Launcher";
            ClientSize = new Size(400, 280);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            try
            {
                var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("ElifootLauncher.elifoot.ico");
                if (iconStream != null) Icon = new System.Drawing.Icon(iconStream);
            }
            catch { }

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
            var btnConfig = MakeButton("Configurações", 210, secondary: true);

            btnJogo.Click += (s, e) => SafeRun(() => _launcher.LaunchElifoot(_config));
            btnEditor.Click += (s, e) => SafeRun(() => _launcher.LaunchEditor(_config));
            btnCrack.Click += (s, e) =>
            {
                using (var f = new KeygenForm(_launcher))
                    f.ShowDialog(this);
            };
            btnConfig.Click += (s, e) =>
            {
                using (var f = new SettingsForm(_config))
                {
                    if (f.ShowDialog(this) == DialogResult.OK)
                        _config = LauncherConfig.Load();
                }
            };

            Controls.AddRange(new Control[] { btnJogo, btnEditor, btnCrack, btnConfig });
        }

        private void SafeRun(Action a)
        {
            try { a(); }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show(this, ex.Message, "Arquivo faltando",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Erro ao iniciar",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
