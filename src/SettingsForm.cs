using System;
using System.Drawing;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class SettingsForm : Form
    {
        public LauncherConfig Config { get; }

        private readonly ComboBox _resolutionBox;
        private readonly CheckBox _fullscreenBox;

        private static readonly (int W, int H)[] Resolutions =
        {
            (640, 480),
            (800, 600),
            (1024, 768),
            (1280, 960),
            (1366, 768),
            (1600, 1200),
            (1920, 1080),
            (1920, 1440),
            (2560, 1440),
            (3840, 2160),
        };

        public SettingsForm(LauncherConfig cfg)
        {
            Config = cfg;

            Text = "Configurações";
            ClientSize = new Size(360, 200);
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

            var btnOk = new Button
            {
                Text = "Salvar",
                DialogResult = DialogResult.OK,
                Location = new Point(180, 150),
                Size = new Size(80, 30),
            };
            var btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(270, 150),
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

            Controls.AddRange(new Control[] { lblRes, _resolutionBox, _fullscreenBox, btnOk, btnCancel });
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
