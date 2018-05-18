using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CutToLength
{
	class Class1 : Control
	{
		public Bin Bin { get; set; }

		private const int BorderPixels = 15;
		private const int BinHeightPixels = 100;

		public Class1()
		{
			SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var displayWidth = Width - BorderPixels * 2.0f;
			var maxHeight = Height - BorderPixels * 2.0f;

			if (displayWidth <= 0) return;
			if (maxHeight <= 0) return;

			var displayHeight = maxHeight < BinHeightPixels ? maxHeight : BinHeightPixels;

			var x = (Width - displayWidth) / 2.0f;
			var y = (Height - displayHeight) / 2.0f;

			var rect = new RectangleF(x, y, displayWidth, displayHeight);

			var drawX = (float)x;
			var id = 1;
			var scale = displayWidth / Bin.Length;
			var totalLength = 0.0;

			if (Bin != null)
			{
				foreach (var item in Bin.Items)
				{
					var xc = item.Length
					var w = item.Length / Bin.Length * displayWidth;
					var r = new RectangleF(drawX, y, (float)w, displayHeight);

					e.Graphics.DrawRectangle(Pens.Blue, r.X, r.Y, r.Width, r.Height);
					e.Graphics.DrawString(id++.ToString(), Font, Brushes.Blue, r, new StringFormat
					{
						 Alignment = StringAlignment.Center,
						 LineAlignment = StringAlignment.Center
					});

					drawX = r.Right;
				}
			}

			var scrapRect = new RectangleF(drawX, y, rect.Right - drawX, displayHeight);

			e.Graphics.FillRectangle(Brushes.Red, scrapRect);
			e.Graphics.DrawRectangle(Pens.Black, rect.X, rect.Y, rect.Width, rect.Height);
		}
	}
}
