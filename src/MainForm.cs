using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class MainForm : Form
    {
        private readonly GameLauncher _launcher = new GameLauncher();
        private LauncherConfig _config = LauncherConfig.Load();

        public MainForm()
        {
            // Versao do release (o CI passa a tag no build: -p:Version)
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Text = ver != null ? $"Elifoot 98 Launcher v{ver.Major}.{ver.Minor}.{ver.Build}" : "Elifoot 98 Launcher";
            ClientSize = new Size(400, 435);
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
            var btnScout = MakeButton("Scout", 240);
            var btnPatch = MakeButton("Aplicar Patch", 285);
            var btnConfig = MakeButton("Configurações", 350, secondary: true);

            btnJogo.Click += (s, e) => SafeRun(() => _launcher.LaunchElifoot(_config));
            btnEditor.Click += (s, e) =>
            {
                TeamEditorForm f;
                try { f = new TeamEditorForm(_launcher.GameDir, () => SafeRun(() => _launcher.LaunchEditor(_config))); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Não consegui abrir o Editor de Equipes:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                using (f) f.ShowDialog(this);
            };
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
            btnScout.Click += (s, e) =>
            {
                using (var f = new ScoutForm(_launcher.JogosDir, _launcher.GameDir))
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

            Controls.AddRange(new Control[] { btnJogo, btnEditor, btnRefEditor, btnSaveEditor, btnScout, btnPatch, btnConfig });

            // Aviso de versao nova: busca em segundo plano, sem travar a janela (ver Atualizacao.cs;
            // ELIFOOT_VERSAO_URL troca o endereco do versao.json pra testar)
            Shown += (s, e) => Task.Run(async () =>
            {
                var info = await Atualizacao.VerificarAsync(ver, "windows").ConfigureAwait(false);
                if (info == null) return;
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                        BeginInvoke(new Action(() => { if (!IsDisposed) MostrarAviso(info); }));
                }
                catch { }
            });
        }

        // Faixa verde no topo da janela: "Nova versao X disponivel", novidades, Baixar / Agora nao.
        // Empurra os botoes pra baixo e cresce a janela; "Agora nao" desfaz.
        private void MostrarAviso(InfoAtualizacao info)
        {
            var temNovidades = info.Novidades.Length > 0;
            int altura = temNovidades ? 92 : 64;
            var verde = Color.FromArgb(0x00, 0x72, 0x00);
            var amarelo = Color.FromArgb(0xFC, 0xFE, 0x04);

            var faixa = new Panel { Location = new Point(0, 0), Size = new Size(ClientSize.Width, altura), BackColor = verde };
            faixa.Controls.Add(new Label
            {
                Text = $"Nova versão {info.Versao} disponível",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = amarelo,
                Location = new Point(10, 6),
                Size = new Size(ClientSize.Width - 20, 20),
                AutoEllipsis = true,
            });
            if (temNovidades)
                faixa.Controls.Add(new Label
                {
                    Text = info.Novidades,
                    Font = new Font("Segoe UI", 8.25F),
                    ForeColor = Color.White,
                    Location = new Point(10, 27),
                    Size = new Size(ClientSize.Width - 20, 30),
                    AutoEllipsis = true,
                });
            int yBotoes = altura - 32;
            var btnBaixar = new Button
            {
                Text = "Baixar",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = amarelo,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(ClientSize.Width - 200, yBotoes),
                Size = new Size(90, 26),
            };
            btnBaixar.FlatAppearance.BorderColor = Color.FromArgb(0xD8, 0xDA, 0x00);
            var btnAgoraNao = new Button
            {
                Text = "Agora não",
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.FromArgb(0xE6, 0xE6, 0xE6),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(ClientSize.Width - 104, yBotoes),
                Size = new Size(94, 26),
            };
            btnAgoraNao.FlatAppearance.BorderColor = Color.FromArgb(0xB8, 0xB8, 0xB8);
            faixa.Controls.Add(btnBaixar);
            faixa.Controls.Add(btnAgoraNao);

            SuspendLayout();
            foreach (Control c in Controls) c.Top += altura;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + altura);
            Controls.Add(faixa);
            ResumeLayout();

            btnBaixar.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(info.Link) { UseShellExecute = true }); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"Não consegui abrir o navegador:\n{ex.Message}\n\n{info.Link}", "Erro",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            btnAgoraNao.Click += (s, e) =>
            {
                SuspendLayout();
                Controls.Remove(faixa);
                foreach (Control c in Controls) c.Top -= altura;
                ClientSize = new Size(ClientSize.Width, ClientSize.Height - altura);
                ResumeLayout();
                faixa.Dispose();
            };
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
