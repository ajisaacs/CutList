using SawCut;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CutList.Controls
{
    public class BinLayoutView : Control
    {
        public Bin Bin { get; set; }

        private const int BorderPixels = 15;
        private const int BorderPixelsX2 = BorderPixels * 2;
        private const int BinHeightPixels = 100;

        private readonly HatchBrush hBrush = new HatchBrush(HatchStyle.DiagonalCross, Color.Pink, Color.Transparent);

        public BinLayoutView()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        private static readonly StringFormat StringFormatCentered = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Bin == null)
                return;

            var rect = GetBinRectangle();

            e.Graphics.FillRectangle(hBrush, rect);

            var id = 1;
            var scale = rect.Width / (float)Bin.Length;
            var x = rect.X;

            for (int i = 0; i < Bin.Items.Count; i++)
            {
                var item = Bin.Items[i];

                var w = item.Length / Bin.Length * rect.Width;
                var r = new RectangleF(x, rect.Y, (float)w, rect.Height);

                e.Graphics.FillRectangle(Brushes.White, r);
                e.Graphics.DrawRectangle(Pens.Blue, r.X, r.Y, r.Width, r.Height);
                e.Graphics.DrawString(id++.ToString(), Font, Brushes.Blue, r, StringFormatCentered);

                x += (float)item.Length * scale;

                if (i < Bin.Items.Count - 1)
                    x += (float)Bin.Spacing * scale;
            }

            e.Graphics.DrawRectangle(Pens.Blue, rect.X, rect.Y, rect.Width, rect.Height);
        }

        private RectangleF GetBinRectangle()
        {
            var displayWidth = Width - BorderPixelsX2;
            var heightMinusBorder = Height - BorderPixelsX2;
            var displayHeight = Height > BinHeightPixels ? BinHeightPixels : Height;

            var x = (Width - displayWidth) / 2.0f;
            var y = (Height - displayHeight) / 2.0f;

            var rect = new RectangleF(x, y, displayWidth, displayHeight);

            return rect;
        }
    }
}