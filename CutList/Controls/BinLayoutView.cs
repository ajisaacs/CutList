using SawCut;
using System;
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
        private const int DefaultBinHeightPixels = 100;

        public Color ItemBackgroundColor { get; set; } = Color.White;
        public Color ItemBorderColor { get; set; } = Color.Blue;
        public Color BinBackgroundColor { get; set; } = Color.Pink;

        private readonly HatchBrush binBackgroundBrush;

        public BinLayoutView()
        {
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            binBackgroundBrush = new HatchBrush(HatchStyle.DiagonalCross, BinBackgroundColor, Color.Transparent);
        }

        private static readonly StringFormat StringFormatCentered = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Bin == null || Bin.Items == null || Bin.Length <= 0)
                return;

            var binRectangle = GetBinRectangle();

            DrawBinBackground(e.Graphics, binRectangle);
            DrawBinItems(e.Graphics, binRectangle);
            DrawBinBorder(e.Graphics, binRectangle);
        }

        private RectangleF GetBinRectangle()
        {
            var displayWidth = Width - BorderPixelsX2;
            var displayHeight = Math.Min(Height - BorderPixelsX2, DefaultBinHeightPixels);

            var x = (Width - displayWidth) / 2.0f;
            var y = (Height - displayHeight) / 2.0f;

            return new RectangleF(x, y, displayWidth, displayHeight);
        }

        private void DrawBinBackground(Graphics graphics, RectangleF binRectangle)
        {
            graphics.FillRectangle(binBackgroundBrush, binRectangle);
        }

        private void DrawBinItems(Graphics graphics, RectangleF binRectangle)
        {
            float scale = binRectangle.Width / (float)Bin.Length;
            float currentX = binRectangle.X;
            int id = 1;

            foreach (var item in Bin.Items)
            {
                float itemWidth = (float)item.Length * scale;
                var itemRect = new RectangleF(currentX, binRectangle.Y, itemWidth, binRectangle.Height);

                graphics.FillRectangle(new SolidBrush(ItemBackgroundColor), itemRect);
                graphics.DrawRectangle(new Pen(ItemBorderColor), itemRect.X, itemRect.Y, itemRect.Width, itemRect.Height);
                graphics.DrawString(id++.ToString(), Font, new SolidBrush(ItemBorderColor), itemRect, StringFormatCentered);

                currentX += itemWidth + ((float)Bin.Spacing * scale);
            }
        }

        private void DrawBinBorder(Graphics graphics, RectangleF binRectangle)
        {
            graphics.DrawRectangle(new Pen(ItemBorderColor), binRectangle.X, binRectangle.Y, binRectangle.Width, binRectangle.Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                binBackgroundBrush?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}