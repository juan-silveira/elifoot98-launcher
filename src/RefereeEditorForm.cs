using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ElifootLauncher
{
    public class RefereeEditorForm : Form
    {
        private readonly string _refereeTxePath;
        private readonly ListView _list;
        private readonly TextBox _txtCountry;
        private readonly TextBox _txtName;
        private readonly Label _lblStatus;
        private RefereeCodec.File _file = new RefereeCodec.File();

        public RefereeEditorForm(string refereeTxePath)
        {
            _refereeTxePath = refereeTxePath;

            Text = "Editor de Árbitros";
            ClientSize = new Size(680, 500);
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;

            _list = new ListView
            {
                Location = new Point(10, 10),
                Size = new Size(400, 440),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
            };
            _list.Columns.Add("#", 40);
            _list.Columns.Add("País", 60);
            _list.Columns.Add("Nome", 280);
            _list.SelectedIndexChanged += (s, e) => LoadSelectedIntoFields();

            var lblC = new Label { Text = "Código país (3 letras):", Location = new Point(430, 15), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _txtCountry = new TextBox
            {
                Location = new Point(430, 35),
                Width = 100,
                Font = new Font(FontFamily.GenericMonospace, 10),
                MaxLength = 3,
                CharacterCasing = CharacterCasing.Upper,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            var lblN = new Label { Text = "Nome:", Location = new Point(430, 70), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _txtName = new TextBox
            {
                Location = new Point(430, 90),
                Width = 240,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };

            var btnUpdate = new Button { Text = "Atualizar", Location = new Point(430, 130), Size = new Size(80, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var btnAdd = new Button { Text = "Adicionar", Location = new Point(520, 130), Size = new Size(80, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var btnRemove = new Button { Text = "Remover", Location = new Point(610, 130), Size = new Size(60, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnUpdate.Click += (s, e) => UpdateSelected();
            btnAdd.Click += (s, e) => AddNew();
            btnRemove.Click += (s, e) => RemoveSelected();

            var btnMoveUp = new Button { Text = "↑", Location = new Point(430, 165), Size = new Size(40, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            var btnMoveDown = new Button { Text = "↓", Location = new Point(475, 165), Size = new Size(40, 28), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnMoveUp.Click += (s, e) => MoveSelected(-1);
            btnMoveDown.Click += (s, e) => MoveSelected(+1);

            var btnSave = new Button { Text = "Salvar arquivo", Location = new Point(430, 400), Size = new Size(120, 30), Font = new Font("Segoe UI", 9F, FontStyle.Bold), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btnSave.Click += (s, e) => SaveFile();
            var btnReload = new Button { Text = "Recarregar original", Location = new Point(560, 400), Size = new Size(110, 30), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btnReload.Click += (s, e) => LoadFile();

            _lblStatus = new Label
            {
                Location = new Point(430, 440),
                Size = new Size(240, 40),
                ForeColor = Color.FromArgb(90, 90, 90),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            };

            Controls.AddRange(new Control[] {
                _list, lblC, _txtCountry, lblN, _txtName,
                btnUpdate, btnAdd, btnRemove,
                btnMoveUp, btnMoveDown,
                btnSave, btnReload,
                _lblStatus,
            });

            LoadFile();
        }

        private void LoadFile()
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
                MessageBox.Show(this, "Erro ao ler REFEREE.TXE: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshList()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _file.Records.Count; i++)
            {
                var r = _file.Records[i];
                var item = new ListViewItem((i + 1).ToString());
                item.SubItems.Add(r.CountryCode);
                item.SubItems.Add(r.Name);
                _list.Items.Add(item);
            }
            _list.EndUpdate();
        }

        private void LoadSelectedIntoFields()
        {
            if (_list.SelectedIndices.Count == 0) return;
            int i = _list.SelectedIndices[0];
            if (i < 0 || i >= _file.Records.Count) return;
            var r = _file.Records[i];
            _txtCountry.Text = r.CountryCode;
            _txtName.Text = r.Name;
        }

        private void UpdateSelected()
        {
            if (_list.SelectedIndices.Count == 0) return;
            int i = _list.SelectedIndices[0];
            if (i < 0 || i >= _file.Records.Count) return;
            var country = _txtCountry.Text.Trim();
            var name = _txtName.Text.Trim();
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
            RefreshList();
            _list.Items[i].Selected = true;
            _list.EnsureVisible(i);
            _lblStatus.Text = "Atualizado (não salvo em disco ainda)";
        }

        private void AddNew()
        {
            var country = _txtCountry.Text.Trim();
            var name = _txtName.Text.Trim();
            if (country.Length != 3 || name.Length == 0)
            {
                _lblStatus.Text = "Preencha país (3) e nome.";
                return;
            }
            int insertAt = _list.SelectedIndices.Count > 0 ? _list.SelectedIndices[0] + 1 : _file.Records.Count;
            _file.Records.Insert(insertAt, new RefereeCodec.Record { CountryCode = country, Name = name });
            RefreshList();
            _list.Items[insertAt].Selected = true;
            _list.EnsureVisible(insertAt);
            _lblStatus.Text = $"Adicionado. Total: {_file.Records.Count}";
        }

        private void RemoveSelected()
        {
            if (_list.SelectedIndices.Count == 0) return;
            int i = _list.SelectedIndices[0];
            if (i < 0 || i >= _file.Records.Count) return;
            _file.Records.RemoveAt(i);
            RefreshList();
            int newSel = Math.Min(i, _file.Records.Count - 1);
            if (newSel >= 0)
            {
                _list.Items[newSel].Selected = true;
                _list.EnsureVisible(newSel);
            }
            _lblStatus.Text = $"Removido. Total: {_file.Records.Count}";
        }

        private void MoveSelected(int direction)
        {
            if (_list.SelectedIndices.Count == 0) return;
            int i = _list.SelectedIndices[0];
            int j = i + direction;
            if (j < 0 || j >= _file.Records.Count) return;
            var tmp = _file.Records[i];
            _file.Records[i] = _file.Records[j];
            _file.Records[j] = tmp;
            RefreshList();
            _list.Items[j].Selected = true;
            _list.EnsureVisible(j);
        }

        private void SaveFile()
        {
            try
            {
                var backup = _refereeTxePath + ".bak";
                if (!System.IO.File.Exists(backup))
                    System.IO.File.Copy(_refereeTxePath, backup);
                RefereeCodec.Write(_file, _refereeTxePath);
                _lblStatus.Text = $"Salvo! Backup em {Path.GetFileName(backup)}";
                MessageBox.Show(this,
                    $"Arquivo REFEREE.TXE salvo com {_file.Records.Count} árbitros.\n" +
                    $"Backup do original em: {backup}\n\n" +
                    "Da próxima vez que abrir o Elifoot, os novos árbitros aparecem.",
                    "Salvo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Erro ao salvar: " + ex.Message, "Erro",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
