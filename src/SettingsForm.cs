using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Configuracoes: cada opcao num seletor, e nada muda ate o Salvar. Resolucao, versao
    // do jogo, equipes por divisao, idioma, registro, estrangeiros e os times (originais
    // ou de um patch da biblioteca, Patches.cs).
    public class SettingsForm : Form
    {
        public LauncherConfig Config { get; }
        private readonly GameLauncher _launcher = new GameLauncher();

        private readonly ComboBox _resolutionBox;
        private readonly CheckBox _fullscreenBox;
        private readonly ComboBox _times;
        private readonly Button _removerPatch;
        private List<string> _patches = new List<string>();

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

        private const int Largura = 420;

        public SettingsForm(LauncherConfig cfg)
        {
            Config = cfg;

            Text = "Configurações";
            ClientSize = new Size(Largura + 40, 640);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var coluna = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Location = new Point(0, 0),
                Size = new Size(Largura + 40, 586),
                Padding = new Padding(16, 8, 16, 8),
            };

            _resolutionBox = Seletor(coluna, "Resolução da janela do jogo:", Resolutions.Select(r => $"{r.W} × {r.H}"), null);
            _resolutionBox.SelectedIndex = FindResolutionIndex(cfg.ResolutionWidth, cfg.ResolutionHeight);
            _fullscreenBox = new CheckBox { Text = "Abrir em tela cheia", AutoSize = true, Checked = cfg.Fullscreen };
            coluna.Controls.Add(_fullscreenBox);

            var versao = Seletor(coluna, "Versão do jogo:", new[]
            {
                "Modificada — cartões amarelos, Nota e Risco de lesão, equipes por divisão",
                "Original — o Elifoot 98 como era (98.003)",
            }, "Na original só há 8 equipes por divisão. Um jogo gravado na modificada mostra números estranhos de lesões e cartões na original.");
            versao.SelectedIndex = cfg.VersaoOriginal ? 1 : 0;

            var equipes = Seletor(coluna, "Equipes por divisão:", LauncherConfig.OpcoesEquipes.Select(n =>
                n == 8 ? "8 (original)" : $"{n} — {2 * (n - 1)} rodadas, mínimo de {LauncherConfig.MinimoDeEquipes(n)} equipes"),
                "Vale para o próximo jogo novo: um jogo gravado só continua com o mesmo número de equipes em que foi começado.");
            equipes.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesEquipes, cfg.EquipesPorDivisao));
            versao.SelectedIndexChanged += (s, e) => equipes.Enabled = versao.SelectedIndex == 0;
            equipes.Enabled = versao.SelectedIndex == 0;

            var idioma = Seletor(coluna, "Idioma do jogo:", LauncherConfig.NomesIdioma, null);
            idioma.SelectedIndex = Math.Max(0, Array.IndexOf(LauncherConfig.OpcoesIdioma, cfg.Idioma));

            var registro = Seletor(coluna, "Registro:", new[]
            {
                "Registrado — todos os recursos (Registro para autor)",
                "Não registrado — como a cópia de avaliação",
            }, "Vale a partir da próxima vez que o jogo abrir (na 1ª vez, depois que o jogo criar a senha).");
            registro.SelectedIndex = cfg.Registrado ? 0 : 1;

            // Estrangeiros: Original (BOSMAN.TXE que vem com o launcher: a UE atual e o
            // Reino Unido) ou Liberado (todos os paises do COUNTRY.TXE no BOSMAN.TXE,
            // como fez o Turbo Score)
            var estrangeiros = Seletor(coluna, "Jogadores estrangeiros:", new[]
            {
                "Original — até 5 por equipe (Bosman e língua portuguesa não contam)",
                "Liberado — sem limite de estrangeiros",
            }, "Vale no jogo e no Editor de Equipes.");
            bool liberadoAntes = false;
            try { liberadoAntes = TeamCodec.BosmanLiberado(_launcher.GameDir); } catch { estrangeiros.Enabled = false; }
            estrangeiros.SelectedIndex = liberadoAntes ? 1 : 0;

            _times = Seletor(coluna, "Times:", Array.Empty<string>(),
                "Trocar os times volta o jogo aos arquivos originais e aplica o patch escolhido: equipes editadas e árbitros são desfeitos. Os jogos salvos ficam.");
            var linhaPatch = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0) };
            var btnAdicionar = new Button { Text = "Adicionar patch…", Size = new Size(Largura / 2 - 4, 30) };
            _removerPatch = new Button { Text = "Remover da lista", Size = new Size(Largura / 2 - 4, 30) };
            linhaPatch.Controls.Add(btnAdicionar);
            linhaPatch.Controls.Add(_removerPatch);
            coluna.Controls.Add(linhaPatch);
            btnAdicionar.Click += (s, e) => AdicionarPatch();
            _removerPatch.Click += (s, e) => RemoverPatch();
            _times.SelectedIndexChanged += (s, e) => _removerPatch.Enabled = _times.SelectedIndex > 0;
            ListarPatches(Patches.EmUso(_launcher.GameDir));

            var btnRestaurar = new Button { Text = "Restaurar os arquivos originais…", Size = new Size(Largura, 30), Margin = new Padding(3, 14, 3, 3) };
            btnRestaurar.Click += (s, e) => Restaurar(estrangeiros);
            coluna.Controls.Add(btnRestaurar);
            coluna.Controls.Add(Nota("Desfaz equipes editadas ou criadas, patches, árbitros e a regra de estrangeiros. Os jogos salvos ficam."));

            var btnOk = new Button { Text = "Salvar", Location = new Point(Largura - 140, 600), Size = new Size(80, 30) };
            var btnCancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(Largura - 50, 600), Size = new Size(80, 30) };
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (s, e) =>
            {
                bool liberado = estrangeiros.SelectedIndex == 1;
                var patch = _times.SelectedIndex <= 0 ? "" : _patches[_times.SelectedIndex - 1];
                if (patch != Patches.EmUso(_launcher.GameDir))
                {
                    if (!TrocarTimes(patch)) return;
                    liberadoAntes = TeamCodec.BosmanLiberado(_launcher.GameDir);
                }
                if (estrangeiros.Enabled && liberado != liberadoAntes)
                {
                    try { TeamCodec.DefinirBosman(_launcher.GameDir, liberado); }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, $"Não consegui gravar o BOSMAN.TXE:\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                var (w, h) = Resolutions[_resolutionBox.SelectedIndex];
                Config.ResolutionWidth = w;
                Config.ResolutionHeight = h;
                Config.Fullscreen = _fullscreenBox.Checked;
                Config.VersaoOriginal = versao.SelectedIndex == 1;
                Config.EquipesPorDivisao = LauncherConfig.OpcoesEquipes[equipes.SelectedIndex];
                Config.Idioma = LauncherConfig.OpcoesIdioma[idioma.SelectedIndex];
                Config.Registrado = registro.SelectedIndex == 0;
                Config.Save();
                DialogResult = DialogResult.OK;
                Close();
            };

            Controls.AddRange(new Control[] { coluna, btnOk, btnCancel });
        }

        private bool TrocarTimes(string patch)
        {
            UseWaitCursor = true;
            try
            {
                var r = Patches.Trocar(_launcher.GameDir, _launcher.OriginaisDir, _launcher.BibliotecaDir, patch);
                if (!r.Ok)
                {
                    MessageBox.Show(this, "Não consegui trocar os times:\n" + r.Error, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não consegui trocar os times:\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally { UseWaitCursor = false; }
        }

        private void Restaurar(ComboBox estrangeiros)
        {
            if (MessageBox.Show(this,
                    "Todas as equipes voltam a ser as originais do jogo: equipes editadas ou criadas, patches aplicados, " +
                    "árbitros e a regra de estrangeiros são desfeitos.\n\nOs jogos salvos (saves) não são apagados.\n\nRestaurar?",
                    "Restaurar os arquivos originais", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (!TrocarTimes("")) return;
            try { TeamCodec.DefinirBosman(_launcher.GameDir, false); } catch { }
            ListarPatches("");
            estrangeiros.SelectedIndex = 0;
            MessageBox.Show(this, "Arquivos originais restaurados.", "Pronto", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AdicionarPatch()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Escolha o patch (.zip)",
                Filter = "Patches Elifoot 98 (*.zip)|*.zip|Todos os arquivos (*.*)|*.*",
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var nome = Patches.Adicionar(_launcher.BibliotecaDir, dlg.FileName);
                ListarPatches(nome);
                MessageBox.Show(this, $"\"{nome}\" está selecionado em Times. Clique em Salvar para aplicar.",
                    "Patch na lista", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não consegui guardar o patch:\n" + ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoverPatch()
        {
            if (_times.SelectedIndex <= 0) return;
            var nome = _patches[_times.SelectedIndex - 1];
            var emUso = nome == Patches.EmUso(_launcher.GameDir);
            if (MessageBox.Show(this, $"Tirar \"{nome}\" da lista de patches?" +
                    (emUso ? "\n\nEle está em uso: os times continuam os dele até você escolher outros." : ""),
                    "Remover da lista", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            Patches.Remover(_launcher.BibliotecaDir, nome);
            ListarPatches(emUso ? nome : "");
        }

        private void ListarPatches(string selecionado)
        {
            _patches = Patches.Listar(_launcher.BibliotecaDir);
            var emUso = Patches.EmUso(_launcher.GameDir);
            _times.Items.Clear();
            _times.Items.Add("Originais do jogo");
            foreach (var p in _patches) _times.Items.Add(p == emUso ? p + " (em uso)" : p);
            _times.SelectedIndex = Math.Max(0, _patches.IndexOf(selecionado) + 1);
            _removerPatch.Enabled = _times.SelectedIndex > 0;
        }

        private static ComboBox Seletor(FlowLayoutPanel coluna, string rotulo, IEnumerable<string> opcoes, string? nota)
        {
            coluna.Controls.Add(new Label { Text = rotulo, AutoSize = true, Margin = new Padding(3, 12, 3, 3), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) });
            var c = new ComboBox { Width = Largura, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = Largura + 120 };
            foreach (var o in opcoes) c.Items.Add(o);
            coluna.Controls.Add(c);
            if (nota != null) coluna.Controls.Add(Nota(nota));
            return c;
        }

        private static Label Nota(string texto) => new Label
        {
            Text = texto,
            MaximumSize = new Size(Largura, 0),
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 100, 100),
        };

        private static int FindResolutionIndex(int w, int h)
        {
            for (int i = 0; i < Resolutions.Length; i++)
                if (Resolutions[i].W == w && Resolutions[i].H == h)
                    return i;
            return 0;
        }
    }
}
