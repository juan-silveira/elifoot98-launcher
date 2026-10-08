using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ElifootLauncher
{
    public class SaveEditorWindow : Window
    {
        private readonly string _jogosDir;
        private readonly ComboBox _saveSel;
        private readonly ComboBox _teamSel;
        private readonly TextBox _verbaField;
        private readonly Tabela _players;
        private readonly Button _save;
        private SaveFile? _current;
        private SaveTeam? _currentTeam;
        private List<SaveTeam> _teams = new List<SaveTeam>();
        private string _currentPath = "";

        public SaveEditorWindow(string jogosDir)
        {
            _jogosDir = jogosDir;
            Title = "Editor de Save";
            Width = 680;
            Height = 620;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();

            _saveSel = new ComboBox { Width = 300 };
            _saveSel.SelectionChanged += async (_, _) => await LoadSelected();
            var reload = new Button { Content = "Recarregar", Width = 100, HorizontalContentAlignment = HorizontalAlignment.Center };
            reload.Click += async (_, _) => await RefreshSaveList(preserveSelection: true);

            _teamSel = new ComboBox { Width = 300 };
            _teamSel.SelectionChanged += (_, _) => LoadTeam();

            _verbaField = new TextBox { Width = 200 };

            _players = new Tabela("30,40,*,70,90,130", "#", "Pos", "Nome", "Força", "Salário", "Comportamento");
            _players.Lista.DoubleTapped += async (_, _) => await EditarJogador();

            _save = new Button { Content = "Salvar", Width = 90, IsEnabled = false, HorizontalContentAlignment = HorizontalAlignment.Center };
            _save.Click += async (_, _) => await SaveCurrent();
            var close = new Button { Content = "Fechar", Width = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
            close.Click += (_, _) => Close();

            var topo = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    Rotulado("Save:", _saveSel, reload),
                    Rotulado("Time:", _teamSel),
                    new TextBlock { Text = "Clube", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) },
                    Rotulado("Verba (Reais):", _verbaField),
                    new TextBlock { Text = "Jogadores (duplo-clique pra editar força e salário)", FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) },
                },
            };
            var rodape = new StackPanel
            {
                Spacing = 6,
                Margin = new Thickness(0, 8, 0, 0),
                Children =
                {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, Children = { _save, close } },
                    new TextBlock
                    {
                        Text = "Backup .bak criado na primeira gravação. Força >50 emite aviso (jogo aceita até 9999).",
                        Foreground = Brushes.Gray,
                        FontSize = 11,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            };
            var raiz = new DockPanel { Margin = new Thickness(16) };
            DockPanel.SetDock(topo, Dock.Top);
            DockPanel.SetDock(rodape, Dock.Bottom);
            raiz.Children.Add(topo);
            raiz.Children.Add(rodape);
            raiz.Children.Add(_players);
            Content = raiz;

            Opened += async (_, _) => await RefreshSaveList(preserveSelection: false);
        }

        private static StackPanel Rotulado(string texto, params Control[] controles)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            sp.Children.Add(new TextBlock { Text = texto, Width = 100, VerticalAlignment = VerticalAlignment.Center });
            foreach (var c in controles) sp.Children.Add(c);
            return sp;
        }

        private async Task EditarJogador()
        {
            if (_currentTeam == null || _players.Lista.SelectedItem is not Linha { Tag: SavePlayer p }) return;
            int idx = _players.Selecionado;

            var forca = await Dialogos.PedirInteiro(this, $"Nova força para {p.Nome}\n(normal 1-50, jogo aceita até 9999):",
                p.Forca, SaveCodec.FORCA_MIN, SaveCodec.FORCA_MAX);
            if (forca.HasValue)
            {
                if (forca.Value <= SaveCodec.FORCA_WARN_ABOVE ||
                    await Dialogos.Confirmar(this,
                        $"Força {forca.Value} é bem acima do normal (1-{SaveCodec.FORCA_WARN_ABOVE}).\nContinuar mesmo assim?",
                        "Aviso"))
                    p.Forca = forca.Value;
            }
            var salario = await Dialogos.PedirInteiro(this, $"Novo salário para {p.Nome}:",
                p.Salario, SaveCodec.SALARIO_MIN, SaveCodec.SALARIO_MAX);
            if (salario.HasValue) p.Salario = salario.Value;

            LoadTeam();
            _players.Selecionado = idx;
        }

        private async Task RefreshSaveList(bool preserveSelection)
        {
            string? prev = preserveSelection ? _saveSel.SelectedItem as string : null;
            if (!Directory.Exists(_jogosDir))
            {
                await Dialogos.Mensagem(this, $"Pasta JOGOS não encontrada:\n{_jogosDir}", "Sem saves");
                return;
            }
            var files = Directory.GetFiles(_jogosDir, "*.e98", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).OrderBy(n => n).ToList();
            _saveSel.ItemsSource = files;
            if (files.Count > 0)
            {
                int found = prev != null ? files.IndexOf(prev) : -1;
                _saveSel.SelectedIndex = found >= 0 ? found : 0;
            }
        }

        private async Task LoadSelected()
        {
            if (_saveSel.SelectedItem is not string name) return;
            _currentPath = Path.Combine(_jogosDir, name);
            try
            {
                _current = SaveCodec.Read(_currentPath);
                _teams = _current.Teams.OrderBy(t => t.Nome, StringComparer.OrdinalIgnoreCase).ToList();
                _teamSel.ItemsSource = _teams.Select(t => $"{t.Nome} — {t.Verba:N0}").ToList();
                _teamSel.SelectedIndex = _teams.Count > 0 ? 0 : -1;
                _save.IsEnabled = true;
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, $"Falha ao ler save:\n{ex.Message}", "Erro");
                _current = null;
                _save.IsEnabled = false;
            }
        }

        private void LoadTeam()
        {
            int i = _teamSel.SelectedIndex;
            if (i < 0 || i >= _teams.Count) return;
            if (_currentTeam != _teams[i])
            {
                _currentTeam = _teams[i];
                _verbaField.Text = _currentTeam.Verba.ToString();
            }
            var linhas = new List<Linha>();
            int n = 1;
            foreach (var p in _currentTeam.Players)
            {
                string comp = p.Comportamento >= 0 && p.Comportamento < SaveCodec.ComportamentoLabels.Length
                    ? SaveCodec.ComportamentoLabels[p.Comportamento]
                    : "?";
                linhas.Add(new Linha(p, (n++).ToString(), p.Posicao, p.Estrela ? p.Nome + "*" : p.Nome,
                    p.Forca.ToString(), p.Salario.ToString(), comp));
            }
            _players.Itens = linhas;
        }

        private async Task SaveCurrent()
        {
            if (_current == null || string.IsNullOrEmpty(_currentPath) || _currentTeam == null) return;

            if (!long.TryParse(_verbaField.Text, out var verba) || verba < 0)
            {
                await Dialogos.Mensagem(this, "Verba deve ser número inteiro positivo.", "Valor inválido");
                return;
            }
            _currentTeam.Verba = verba;

            var bak = _currentPath + ".bak";
            try
            {
                if (!File.Exists(bak)) File.Copy(_currentPath, bak);
                SaveCodec.Write(_currentPath, _current);
                await Dialogos.Mensagem(this, "Save gravado com sucesso.", "OK");
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, $"Erro ao gravar:\n{ex.Message}", "Erro");
            }
        }
    }
}
