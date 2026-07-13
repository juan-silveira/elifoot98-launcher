using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class SettingsForm : Form
    {
        public LauncherConfig Config { get; }
        private readonly GameLauncher _launcher = new GameLauncher();

        private readonly ComboBox _resolutionBox;
        private readonly CheckBox _fullscreenBox;

        // Com AppCompat 640X480 ativo, Elifoot desenha em 640x480. Resolucoes
        // maiores no launcher = mesma tela do jogo escalada (pixels maiores).
        private static readonly (int W, int H)[] Resolutions =
        {
            (640, 480),
            (800, 600),
            (1024, 768),
            (1280, 960),
            (1600, 1200),
            (1920, 1440),
        };

        public SettingsForm(LauncherConfig cfg)
        {
            Config = cfg;

            Text = "Configurações";
            ClientSize = new Size(400, 290);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var lblRes = new Label
            {
                Text = "Resolução:",
                Location = new Point(20, 24),
                AutoSize = true,
            };
            _resolutionBox = new ComboBox
            {
                Location = new Point(20, 46),
                Width = 320,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (var (w, h) in Resolutions)
                _resolutionBox.Items.Add($"{w} × {h}");
            _resolutionBox.SelectedIndex = FindResolutionIndex(cfg.ResolutionWidth, cfg.ResolutionHeight);

            _fullscreenBox = new CheckBox
            {
                Text = "Abrir em tela cheia",
                Location = new Point(20, 90),
                AutoSize = true,
                Checked = cfg.Fullscreen,
            };

            var lblNota = new Label
            {
                Text = "Com o compat layer 640×480 ativo, o jogo desenha nessa resolução independente da janela — nenhum elemento é cortado.",
                Location = new Point(20, 120),
                Size = new Size(360, 60),
                ForeColor = Color.FromArgb(100, 100, 100),
            };

            var btnExperiment = new Button
            {
                Text = "Experimentar todos os recursos",
                Location = new Point(20, 195),
                Size = new Size(360, 32),
                FlatStyle = FlatStyle.System,
            };
            btnExperiment.Click += (s, e) => ExperimentarRecursos();

            var btnOk = new Button
            {
                Text = "Salvar",
                DialogResult = DialogResult.OK,
                Location = new Point(220, 240),
                Size = new Size(80, 30),
            };
            var btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(310, 240),
                Size = new Size(80, 30),
            };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (s, e) =>
            {
                var (w, h) = Resolutions[_resolutionBox.SelectedIndex];
                Config.ResolutionWidth = w;
                Config.ResolutionHeight = h;
                Config.Fullscreen = _fullscreenBox.Checked;
                Config.Save();
            };

            Controls.AddRange(new Control[] { lblRes, _resolutionBox, _fullscreenBox, lblNota, btnExperiment, btnOk, btnCancel });
        }

        private void ExperimentarRecursos()
        {
            // 1) Fecha o Elifoot se estiver aberto (mata otvdmw.exe rodando ELIFOOT/EDITEQ)
            try
            {
                foreach (var p in Process.GetProcessesByName("otvdmw"))
                {
                    try { p.Kill(); p.WaitForExit(3000); } catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }

            // 2) Copia eli.cod + elif98.ini embutidos pra vendor/otvdm/WINDOWS
            var destDir = _launcher.OtvdmWindowsDir;
            try
            {
                Directory.CreateDirectory(destDir);
                ExtractEmbedded("ElifootLauncher.Embedded.eli.cod",
                                Path.Combine(destDir, "eli.cod"));
                ExtractEmbedded("ElifootLauncher.Embedded.elif98.ini",
                                Path.Combine(destDir, "elif98.ini"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Erro ao ativar recursos:\n{ex.Message}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show(this, "Recursos ativados. Abra o jogo pra experimentar.",
                "Pronto", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void ExtractEmbedded(string resourceName, string destPath)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                ?? throw new IOException($"Recurso {resourceName} nao encontrado no launcher");
            using var fs = File.Create(destPath);
            stream.CopyTo(fs);
        }

        private static int FindResolutionIndex(int w, int h)
        {
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].W == w && Resolutions[i].H == h)
                    return i;
            return 0;
        }
    }
}
