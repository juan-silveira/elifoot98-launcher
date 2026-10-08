using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace ElifootLauncher
{
    // Lista com colunas (substitui o ListView em modo Details do WinForms):
    // cabecalho + ListBox cujas linhas sao Grids com as mesmas larguras.
    public class Linha
    {
        public string[] Celulas { get; }
        public object? Tag { get; }
        public Linha(object? tag, params string[] celulas) { Tag = tag; Celulas = celulas; }
    }

    public class Tabela : DockPanel
    {
        private readonly string _colunas;
        public ListBox Lista { get; }

        public Tabela(string colunas, params string[] cabecalhos)
        {
            _colunas = colunas;
            var cab = NovaGrade(cabecalhos, FontWeight.Bold);
            cab.Margin = new Thickness(12, 4, 12, 4);
            SetDock(cab, Dock.Top);
            Children.Add(cab);

            Lista = new ListBox
            {
                ItemTemplate = new FuncDataTemplate<Linha>((l, _) => l == null ? new Panel() : NovaGrade(l.Celulas, FontWeight.Normal)),
            };
            Children.Add(new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Child = Lista,
            });
        }

        public List<Linha> Itens
        {
            set => Lista.ItemsSource = value;
        }

        public int Selecionado
        {
            get => Lista.SelectedIndex;
            set
            {
                Lista.SelectedIndex = value;
                if (value >= 0) Lista.ScrollIntoView(value);
            }
        }

        private Grid NovaGrade(string[] textos, FontWeight peso)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions(_colunas) };
            for (int i = 0; i < textos.Length; i++)
            {
                var tb = new TextBlock
                {
                    Text = textos[i],
                    FontWeight = peso,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(tb, i);
                g.Children.Add(tb);
            }
            return g;
        }
    }
}
