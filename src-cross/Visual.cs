using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace ElifootLauncher
{
    // Visual comum dos editores (verde e amarelo do jogo) e o seletor de cores
    // (16 cores do jogo + R/G/B e hexadecimal)
    public static class Visual
    {
        public static readonly IBrush Verde = Cor(0x0B3D0B), VerdeTopo = Cor(0x062606), Cartao = Cor(0x145214),
            LinhaPar = Cor(0x114A11), Amarelo = Cor(0xFCFE04), Cinza = Cor(0xB8C9B8), CinzaFaixa = Cor(0x8FA88F),
            Branco = Brushes.White, Alerta = Cor(0xFF8A80), Ouro = Cor(0xFFD54F), Ok = Cor(0x9CE29C);
        // As 16 cores que o jogo usa nas equipes
        public static readonly int[] Paleta =
        {
            0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xC0C0C0,
            0x808080, 0xFF0000, 0x00FF00, 0xFFFF00, 0x0000FF, 0xFF00FF, 0x00FFFF, 0xFFFFFF,
        };

        public static IBrush Cor(int rgb) => new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

        public static TextBlock Texto(string t, double tamanho, IBrush cor, bool negrito = false) => new TextBlock
        {
            Text = t,
            FontSize = tamanho,
            Foreground = cor,
            FontWeight = negrito ? FontWeight.Bold : FontWeight.Normal,
        };

        public static Control CentroV(Control c)
        {
            c.VerticalAlignment = VerticalAlignment.Center;
            return c;
        }

        public static string Milhar(long v) => v.ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");

        public static string Fmt(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        // Estilos comuns das janelas verdes (campos brancos, botoes amarelos, linhas da tabela)
        public static void AplicarEstilos(Window w)
        {
            // Campos e listas brancos sobre o verde (no tema claro ficariam translucidos)
            foreach (var tipo in new[] { typeof(TextBox), typeof(ComboBox) })
                w.Styles.Add(new Style(x => x.OfType(tipo))
                {
                    Setters =
                    {
                        new Setter(TemplatedControl.BackgroundProperty, Brushes.White),
                        new Setter(TemplatedControl.ForegroundProperty, Brushes.Black),
                    },
                });
            // Botoes amarelos continuam amarelos com o mouse em cima ou pressionados
            foreach (var (estado, cor) in new[] { (":pointerover", 0xEEF000), (":pressed", 0xD8DA00) })
                w.Styles.Add(new Style(x => x.OfType<Button>().Class("amarelo").Class(estado).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
                {
                    Setters = { new Setter(ContentPresenter.BackgroundProperty, Cor(cor)) },
                });
            // Botoes claros: um cinza mais escuro com o mouse em cima (o tema deixaria transparente)
            foreach (var (estado, cor) in new[] { (":pointerover", 0xD0D0D0), (":pressed", 0xB8B8B8) })
                w.Styles.Add(new Style(x => x.OfType<Button>().Class("claro").Class(estado).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
                {
                    Setters = { new Setter(ContentPresenter.BackgroundProperty, Cor(cor)), new Setter(ContentPresenter.ForegroundProperty, Brushes.Black) },
                });
            foreach (var estado in new[] { ":pointerover", ":pressed" })
                w.Styles.Add(new Style(x => x.OfType<Button>().Class("amarelo").Class(estado).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
                {
                    Setters = { new Setter(ContentPresenter.ForegroundProperty, Brushes.Black) },
                });
            // Botao desativado legivel sobre o verde: fundo verde medio, texto claro, borda
            w.Styles.Add(new Style(x => x.OfType<Button>().Class(":disabled").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
            {
                Setters =
                {
                    new Setter(ContentPresenter.BackgroundProperty, Cor(0x2A5C2A)),
                    new Setter(ContentPresenter.ForegroundProperty, Cor(0xA9C4A9)),
                    new Setter(ContentPresenter.BorderBrushProperty, Cor(0x4A7C4A)),
                    new Setter(ContentPresenter.BorderThicknessProperty, new Thickness(1)),
                },
            });
            // Linhas da tabela ocupam o item todo; selecao em verde claro
            w.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters = { new Setter(ListBoxItem.PaddingProperty, new Thickness(0)), new Setter(ListBoxItem.MarginProperty, new Thickness(0)) },
            });
            foreach (var (estado, cor) in new[] { (":selected", 0x2E7D32), (":pointerover", 0x1B5E20) })
                w.Styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(estado).Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"))
                {
                    Setters = { new Setter(ContentPresenter.BackgroundProperty, Cor(cor)) },
                });
        }

        // Largura minima de uma tabela em grade: soma das colunas fixas, o minimo da coluna "*"
        // (o nome) e o que sobra de margens
        public static double LarguraMinima(string colunas, double estrela, double extra)
        {
            double total = extra;
            foreach (var c in colunas.Split(','))
                total += c.Trim() == "*" ? estrela : double.Parse(c, CultureInfo.InvariantCulture);
            return total;
        }

        // Cabecalho + lista numa rolagem lateral: com a janela estreita a tabela rola em vez de
        // espremer o nome e cortar as colunas; larga, a coluna "*" ocupa o que sobra
        public static ScrollViewer RolagemLateral(Control tabela, double larguraMinima)
        {
            tabela.MinWidth = larguraMinima;
            return new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                AllowAutoHide = false,  // a barra fica a vista: sem ela nao da pra saber que ha mais colunas
                Content = tabela,
            };
        }

        // Janela pedida maior que a tela (notebook): abre do tamanho da area util
        public static void CaberNaTela(Window w)
        {
            w.Opened += (_, _) =>
            {
                var tela = w.Screens.ScreenFromWindow(w) ?? w.Screens.Primary;
                if (tela == null) return;
                double escala = tela.Scaling > 0 ? tela.Scaling : 1;
                double larg = tela.WorkingArea.Width / escala - 16, alt = tela.WorkingArea.Height / escala - 40;
                bool mudou = false;
                if (w.Width > larg) { w.Width = Math.Max(w.MinWidth, larg); mudou = true; }
                if (w.Height > alt) { w.Height = Math.Max(w.MinHeight, alt); mudou = true; }
                if (mudou)
                    w.Position = new PixelPoint(tela.WorkingArea.X + (int)((tela.WorkingArea.Width - w.Width * escala) / 2),
                        tela.WorkingArea.Y + (int)((tela.WorkingArea.Height - w.Height * escala) / 2));
            };
        }

        public static Border NovoCartao(string titulo)
        {
            var t = Texto(titulo.ToUpperInvariant(), 12, Amarelo, true);
            t.LetterSpacing = 1;
            t.Margin = new Thickness(0, 0, 0, 4);
            return new Border
            {
                Background = Cartao,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 12),
                Child = new StackPanel { Spacing = 6, Children = { t } },
            };
        }

        // Rotulo com a faixa permitida embaixo, em letra menor
        public static Grid LinhaCampo(string rotulo, string faixa, Control campo, Control? extra = null)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,Auto") };
            var r = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Texto(rotulo, 14, Cinza), Texto(faixa, 11, CinzaFaixa) } };
            g.Children.Add(r);
            Grid.SetColumn(campo, 1);
            campo.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(campo);
            if (extra != null)
            {
                extra.Margin = new Thickness(8, 0, 0, 0);
                extra.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(extra, 2);
                g.Children.Add(extra);
            }
            return g;
        }

        public static Button BotaoAmarelo(string texto, double largura) => new Button
        {
            Classes = { "amarelo" },
            Content = texto,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.Black,
            Background = Amarelo,
            Width = largura,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        public static Button BotaoClaro(string texto) => new Button
        {
            Classes = { "claro" },
            Content = texto,
            Background = Cor(0xE6E6E6),
            Foreground = Brushes.Black,
            MinWidth = 90,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        public static Border Amostra() => new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(4),
            BorderBrush = Cor(0xB8C9B8),
            BorderThickness = new Thickness(1),
        };

        public static IBrush CorPosicao(string pos) => pos switch
        {
            "G" => Cor(0xE0B000),
            "D" => Cor(0x3D7BD9),
            "M" => Cor(0x2E9E4F),
            "A" => Cor(0xD9443D),
            _ => Brushes.Gray,
        };

        public static string NomePosicao(string pos) => pos switch
        {
            "G" => "Guarda-redes",
            "D" => "Defesa",
            "M" => "Médio",
            "A" => "Avançado",
            _ => pos,
        };

        public static Window Dialogo(string titulo) => new Window
        {
            Title = titulo,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = Dialogos.Icone(),
        };

        // 16 cores rapidas do jogo + seletor livre (R/G/B e hexadecimal); o save guarda RGB completo
        public static async Task<int?> EscolherCor(Window dono, string titulo, int corAtual)
        {
            int cor = corAtual;
            var w = Dialogo(titulo);
            var previa = new Border { Height = 40, CornerRadius = new CornerRadius(4), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
            var hex = new TextBox();
            var barras = new Slider[3];
            bool mudando = false;
            void Mostrar()
            {
                mudando = true;
                previa.Background = Cor(cor);
                for (int k = 0; k < 3; k++) barras[k].Value = (cor >> (16 - 8 * k)) & 0xFF;
                string h = $"#{cor:X6}";
                if (!string.Equals(hex.Text, h, StringComparison.OrdinalIgnoreCase)) hex.Text = h;
                mudando = false;
            }

            var grade = new WrapPanel { Width = 8 * 36 };
            foreach (int c in Paleta)
            {
                var q = new Border
                {
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    Background = Cor(c),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                q.PointerPressed += (_, _) => { cor = c; Mostrar(); };
                grade.Children.Add(q);
            }
            var livre = new StackPanel { Spacing = 4, Width = 240 };
            livre.Children.Add(new TextBlock { Text = "Outra cor" });
            string[] nomes = { "R", "G", "B" };
            for (int k = 0; k < 3; k++)
            {
                int canal = k;
                var s = new Slider { Minimum = 0, Maximum = 255, Width = 210 };
                s.PropertyChanged += (_, e) =>
                {
                    if (e.Property != Slider.ValueProperty || mudando) return;
                    int desloc = 16 - 8 * canal;
                    cor = (cor & ~(0xFF << desloc)) | ((int)Math.Round(s.Value) << desloc);
                    Mostrar();
                };
                barras[k] = s;
                livre.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { CentroV(new TextBlock { Text = nomes[k], Width = 14 }), s } });
            }
            hex.PropertyChanged += (_, e) =>
            {
                if (e.Property != TextBox.TextProperty || mudando) return;
                string h = (hex.Text ?? "").Trim().TrimStart('#');
                if (h.Length == 6 && int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) { cor = v; Mostrar(); }
            };
            livre.Children.Add(hex);
            Mostrar();

            var ok = new Button { Content = "OK", MinWidth = 90, IsDefault = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            var cancelar = new Button { Content = "Cancelar", MinWidth = 90, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
            ok.Click += (_, _) => w.Close(true);
            cancelar.Click += (_, _) => w.Close(false);
            w.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 24,
                        Children =
                        {
                            new StackPanel { Spacing = 8, Children = { previa, new TextBlock { Text = "Cores do jogo" }, grade } },
                            livre,
                        },
                    },
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancelar } },
                },
            };
            return await w.ShowDialog<bool>(dono) ? cor : null;
        }
    }
}
