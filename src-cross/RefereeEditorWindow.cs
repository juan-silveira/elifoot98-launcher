using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ElifootLauncher
{
    public class RefereeEditorWindow : Window
    {
        private readonly string _refereeTxePath;
        private readonly Tabela _list;
        private readonly TextBox _txtCountry;
        private readonly TextBox _txtName;
        private readonly TextBlock _lblStatus;
        private RefereeCodec.File _file = new RefereeCodec.File();

        public RefereeEditorWindow(string refereeTxePath)
        {
            _refereeTxePath = refereeTxePath;

            Title = "Editor de Árbitros";
            Width = 720;
            Height = 520;
            MinWidth = 600;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = Dialogos.Icone();

            _list = new Tabela("40,60,*", "#", "País", "Nome");
            _list.Lista.SelectionChanged += (_, _) => LoadSelectedIntoFields();

            _txtCountry = new TextBox { Width = 100, MaxLength = 3, FontFamily = FontFamily.Parse("monospace"), HorizontalAlignment = HorizontalAlignment.Left };
            _txtCountry.TextChanged += (_, _) =>
            {
                var up = _txtCountry.Text?.ToUpperInvariant();
                if (up != _txtCountry.Text) _txtCountry.Text = up;
            };
            _txtName = new TextBox();

            var btnUpdate = Botao("Atualizar");
            var btnAdd = Botao("Adicionar");
            var btnRemove = Botao("Remover");
            btnUpdate.Click += (_, _) => UpdateSelected();
            btnAdd.Click += (_, _) => AddNew();
            btnRemove.Click += (_, _) => RemoveSelected();

            var btnMoveUp = Botao("↑");
            var btnMoveDown = Botao("↓");
            btnMoveUp.Click += (_, _) => MoveSelected(-1);
            btnMoveDown.Click += (_, _) => MoveSelected(+1);

            var btnSave = Botao("Salvar arquivo");
            btnSave.FontWeight = FontWeight.Bold;
            btnSave.HorizontalAlignment = HorizontalAlignment.Stretch;
            btnSave.Click += async (_, _) => await SaveFile();
            var btnReload = Botao("Recarregar original");
            btnReload.HorizontalAlignment = HorizontalAlignment.Stretch;
            btnReload.Click += async (_, _) => await LoadFile();

            _lblStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90)) };

            var lateral = new DockPanel { Width = 250, Margin = new Thickness(12, 0, 0, 0) };
            var rodape = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    btnSave,
                    btnReload,
                    _lblStatus,
                },
            };
            DockPanel.SetDock(rodape, Dock.Bottom);
            lateral.Children.Add(rodape);
            lateral.Children.Add(new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Código país (3 letras):" },
                    _txtCountry,
                    new TextBlock { Text = "Nome:" },
                    _txtName,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 8, 0, 0), Children = { btnUpdate, btnAdd, btnRemove } },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { btnMoveUp, btnMoveDown } },
                },
            });

            var raiz = new DockPanel { Margin = new Thickness(10) };
            DockPanel.SetDock(lateral, Dock.Right);
            raiz.Children.Add(lateral);
            raiz.Children.Add(_list);
            Content = raiz;

            Opened += async (_, _) => await LoadFile();
        }

        private static Button Botao(string texto) => new Button { Content = texto, HorizontalContentAlignment = HorizontalAlignment.Center };

        private async System.Threading.Tasks.Task LoadFile()
        {
            try
            {
                if (!System.IO.File.Exists(_refereeTxePath))
                {
                    _lblStatus.Text = "REFEREE.TXE não encontrado.";
                    return;
                }
                _file = RefereeCodec.Read(_refereeTxePath);
                RefreshList();
                _lblStatus.Text = $"Carregados {_file.Records.Count} árbitros";
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, "Erro ao ler REFEREE.TXE: " + ex.Message, "Erro");
            }
        }

        private void RefreshList(int selecionar = -1)
        {
            var itens = new List<Linha>();
            for (int i = 0; i < _file.Records.Count; i++)
                itens.Add(new Linha(_file.Records[i], (i + 1).ToString(), _file.Records[i].CountryCode, _file.Records[i].Name));
            _list.Itens = itens;
            _list.Selecionado = selecionar;
        }

        private void LoadSelectedIntoFields()
        {
            int i = _list.Selecionado;
            if (i < 0 || i >= _file.Records.Count) return;
            _txtCountry.Text = _file.Records[i].CountryCode;
            _txtName.Text = _file.Records[i].Name;
        }

        private void UpdateSelected()
        {
            int i = _list.Selecionado;
            if (i < 0 || i >= _file.Records.Count) return;
            var country = (_txtCountry.Text ?? "").Trim();
            var name = (_txtName.Text ?? "").Trim();
            if (country.Length != 3)
            {
                _lblStatus.Text = "Código de país deve ter 3 letras.";
                return;
            }
            if (name.Length == 0)
            {
                _lblStatus.Text = "Nome vazio.";
                return;
            }
            _file.Records[i].CountryCode = country;
            _file.Records[i].Name = name;
            RefreshList(i);
            _lblStatus.Text = "Atualizado (não salvo em disco ainda)";
        }

        private void AddNew()
        {
            var country = (_txtCountry.Text ?? "").Trim();
            var name = (_txtName.Text ?? "").Trim();
            if (country.Length != 3 || name.Length == 0)
            {
                _lblStatus.Text = "Preencha país (3) e nome.";
                return;
            }
            int insertAt = _list.Selecionado >= 0 ? _list.Selecionado + 1 : _file.Records.Count;
            _file.Records.Insert(insertAt, new RefereeCodec.Record { CountryCode = country, Name = name });
            RefreshList(insertAt);
            _lblStatus.Text = $"Adicionado. Total: {_file.Records.Count}";
        }

        private void RemoveSelected()
        {
            int i = _list.Selecionado;
            if (i < 0 || i >= _file.Records.Count) return;
            _file.Records.RemoveAt(i);
            RefreshList(Math.Min(i, _file.Records.Count - 1));
            _lblStatus.Text = $"Removido. Total: {_file.Records.Count}";
        }

        private void MoveSelected(int direction)
        {
            int i = _list.Selecionado;
            int j = i + direction;
            if (i < 0 || j < 0 || j >= _file.Records.Count) return;
            (_file.Records[i], _file.Records[j]) = (_file.Records[j], _file.Records[i]);
            RefreshList(j);
        }

        private async System.Threading.Tasks.Task SaveFile()
        {
            try
            {
                var backup = _refereeTxePath + ".bak";
                if (!System.IO.File.Exists(backup))
                    System.IO.File.Copy(_refereeTxePath, backup);
                RefereeCodec.Write(_file, _refereeTxePath);
                _lblStatus.Text = $"Salvo! Backup em {Path.GetFileName(backup)}";
                await Dialogos.Mensagem(this,
                    $"Arquivo REFEREE.TXE salvo com {_file.Records.Count} árbitros.\n" +
                    $"Backup do original em: {backup}\n\n" +
                    "Da próxima vez que abrir o Elifoot, os novos árbitros aparecem.",
                    "Salvo");
            }
            catch (Exception ex)
            {
                await Dialogos.Mensagem(this, "Erro ao salvar: " + ex.Message, "Erro");
            }
        }
    }
}
