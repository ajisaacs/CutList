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
        private const int BinHeightPixels = 100;

        private readonly HatchBrush hBrush = new HatchBrush(HatchStyle.DiagonalCross, Color.Pink, Color.Transparent);

        public BinLayoutView()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            //base.OnPaint(e);

            if (Bin == null)
                return;

            var displayWidth = Width - BorderPixels * 2.0f;
            var maxHeight = Height - BorderPixels * 2.0f;

            if (displayWidth <= 0) return;
            if (maxHeight <= 0) return;

            var displayHeight = maxHeight < BinHeightPixels ? maxHeight : BinHeightPixels;

            var x = (Width - displayWidth) / 2.0f;
            var y = (Height - displayHeight) / 2.0f;

            var rect = new RectangleF(x, y, displayWidth, displayHeight);

            var id = 1;
            var scale = displayWidth / (float)Bin.Length;

            var beginningScrap = rect.Left;

            if (Bin != null)
            {
                for (int i = 0; i < Bin.Items.Count; i++)
                {
                    var item = Bin.Items[i];

                    var w = item.Length / Bin.Length * displayWidth;
                    var r = new RectangleF(x, y, (float)w, displayHeight);

                    e.Graphics.DrawRectangle(Pens.Blue, r.X, r.Y, r.Width, r.Height);
                    e.Graphics.DrawString(id++.ToString(), Font, Brushes.Blue, r, new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    });

                    x += (float)item.Length * scale;

                    if (i < Bin.Items.Count - 1)
                        x += (float)Bin.Spacing;

                    if (r.Right > beginningScrap)
                        beginningScrap = r.Right;
                }
            }

            // add 1 pixel so the last items' border will not be drawn over.
            beginningScrap += 1;

            var scrapRect = new RectangleF(beginningScrap, y, rect.Right - beginningScrap, displayHeight);

            e.Graphics.FillRectangle(hBrush, scrapRect);
            e.Graphics.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }
}