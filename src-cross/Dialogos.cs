using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace ElifootLauncher
{
    // Substitutos do MessageBox e do prompt de numero do WinForms
    public static class Dialogos
    {
        public static WindowIcon? Icone()
        {
            var s = typeof(Dialogos).Assembly.GetManifestResourceStream("elifoot98.png");
            return s == null ? null : new WindowIcon(s);
        }

        public static Task Mensagem(Window owner, string texto, string titulo) =>
            Mostrar(owner, texto, titulo, "OK", null);

        public static async Task<bool> Confirmar(Window owner, string texto, string titulo) =>
            await Mostrar(owner, texto, titulo, "Sim", "Não");

        private static Task<bool> Mostrar(Window owner, string texto, string titulo, string ok, string? cancelar)
        {
            var w = NovaJanela(titulo);
            var btnOk = new Button { Content = ok, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
            btnOk.Click += (_, _) => w.Close(true);
            var botoes = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            botoes.Children.Add(btnOk);
            if (cancelar != null)
            {
                var btnC = new Button { Content = cancelar, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
                btnC.Click += (_, _) => w.Close(false);
                botoes.Children.Add(btnC);
            }
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children = { new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 }, botoes },
            };
            return w.ShowDialog<bool>(owner);
        }

        // Pede um inteiro entre min e max; null se cancelado
        public static async Task<int?> PedirInteiro(Window owner, string texto, int atual, int min, int max)
        {
            var w = NovaJanela("Editar valor");
            var tb = new TextBox { Text = atual.ToString() };
            var btnOk = new Button { Content = "OK", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsDefault = true };
            var btnC = new Button { Content = "Cancelar", MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, IsCancel = true };
            btnOk.Click += (_, _) => w.Close(true);
            btnC.Click += (_, _) => w.Close(false);
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = texto, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 },
                    tb,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { btnOk, btnC } },
                },
            };
            w.Opened += (_, _) => { tb.Focus(); tb.SelectAll(); };
            if (!await w.ShowDialog<bool>(owner)) return null;
            if (!int.TryParse(tb.Text, out int v))
            {
                await Mensagem(owner, "Valor inválido.", "Erro");
                return null;
            }
            if (v < min || v > max)
            {
                await Mensagem(owner, $"Valor deve estar entre {min} e {max}.", "Fora dos limites");
                return null;
            }
            return v;
        }

        private static Window NovaJanela(string titulo) => new Window
        {
            Title = titulo,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = Icone(),
        };
    }
}
