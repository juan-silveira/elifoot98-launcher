using System.Drawing;
using System.Windows.Forms;

namespace ElifootLauncher
{
    // Botao dos editores: desativado continua legivel sobre o verde (o Windows
    // pinta o texto desativado de cinza, sem contraste)
    public class BotaoJogo : Button
    {
        private static readonly Color Fundo = Color.FromArgb(0x2A, 0x5C, 0x2A), Borda = Color.FromArgb(0x4A, 0x7C, 0x4A),
            TextoDesativado = Color.FromArgb(0xA9, 0xC4, 0xA9);

        // Com o mouse em cima e pressionado: a propria cor um pouco mais escura
        // (sem isso o botao plano pega a cor do sistema e parece sumir)
        protected override void OnBackColorChanged(System.EventArgs e)
        {
            base.OnBackColorChanged(e);
            FlatAppearance.MouseOverBackColor = Escurecer(BackColor, 0.10);
            FlatAppearance.MouseDownBackColor = Escurecer(BackColor, 0.20);
        }

        private static Color Escurecer(Color c, double quanto) =>
            Color.FromArgb(c.A, (int)(c.R * (1 - quanto)), (int)(c.G * (1 - quanto)), (int)(c.B * (1 - quanto)));

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled) { base.OnPaint(e); return; }
            using (var b = new SolidBrush(Fundo)) e.Graphics.FillRectangle(b, ClientRectangle);
            using (var p = new Pen(Borda)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, TextoDesativado,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    // Janela pedida maior que a tela (notebook): abre do tamanho da area util, centrada
    public static class Tela
    {
        public static void CaberNaTela(Form f)
        {
            f.Load += (_, _) =>
            {
                var area = Screen.FromControl(f).WorkingArea;
                if (f.Width <= area.Width && f.Height <= area.Height) return;
                f.Size = new Size(System.Math.Min(f.Width, area.Width), System.Math.Min(f.Height, area.Height));
                f.Location = new Point(area.X + (area.Width - f.Width) / 2, area.Y + (area.Height - f.Height) / 2);
            };
        }
    }
}
