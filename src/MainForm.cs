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
            ClientSize = new Size(400, 390);
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
            var btnRefEditor = MakeButton("Editor de Árbitros", 150);
            var btnSaveEditor = MakeButton("Editor de Save", 195);
            var btnPatch = MakeButton("Aplicar Patch", 240);
            var btnConfig = MakeButton("Configurações", 305, secondary: true);

            btnJogo.Click += (s, e) => SafeRun(() => _launcher.LaunchElifoot(_config));
            btnEditor.Click += (s, e) => SafeRun(() => _launcher.LaunchEditor(_config));
            btnRefEditor.Click += (s, e) =>
            {
                using (var f = new RefereeEditorForm(_launcher.RefereeTxePath))
                    f.ShowDialog(this);
            };
            btnSaveEditor.Click += (s, e) =>
            {
                using (var f = new SaveEditorForm(_launcher.JogosDir))
                    f.ShowDialog(this);
            };
            btnPatch.Click += (s, e) => AplicarPatch();
            btnConfig.Click += (s, e) =>
            {
                using (var f = new SettingsForm(_config))
                {
                    if (f.ShowDialog(this) == DialogResult.OK)
                        _config = LauncherConfig.Load();
                }
            };

            Controls.AddRange(new Control[] { btnJogo, btnEditor, btnRefEditor, btnSaveEditor, btnPatch, btnConfig });
        }

        private void AplicarPatch()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Patches Elifoot 98 (*.zip)|*.zip|Todos os arquivos (*.*)|*.*",
                Title = "Selecione o patch (.zip) para aplicar",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var confirm = MessageBox.Show(this,
                $"Aplicar patch:\n\n{dlg.FileName}\n\n" +
                "Todos os arquivos serão substituídos EXCETO a pasta EQUIPAS " +
                "(será renomeada pra EQUIPAS_OLD antes) e JOGOS (nunca tocada).\n\nContinuar?",
                "Confirmar patch", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            PatchResult res;
            try { res = PatchApplier.Apply(dlg.FileName, _launcher.GameDir); }
            finally { Cursor = Cursors.Default; }

            if (!res.Ok)
            {
                MessageBox.Show(this, $"Erro ao aplicar patch:\n{res.Error}",
                    "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var msg = $"Patch aplicado com sucesso!\n\n{res.FilesReplaced} arquivos substituídos.";
            if (res.EquipasBackupName != null)
                msg += $"\nEQUIPAS antiga preservada em: {res.EquipasBackupName}";
            if (res.IgnoredEntries.Count > 0)
                msg += $"\n\n{res.IgnoredEntries.Count} entradas em JOGOS/ foram ignoradas (saves preservados).";
            MessageBox.Show(this, msg, "Patch aplicado",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
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
