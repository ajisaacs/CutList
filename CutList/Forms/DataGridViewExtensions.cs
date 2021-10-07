using System.Drawing;
using System.Windows.Forms;

namespace CutList.Forms
{
    public static class DataGridViewExtensions
    {
        private static readonly StringFormat CenterVerticallyFormat = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center
        };

        public static void DrawingRowNumbers(this DataGridView dataGridView)
        {
            dataGridView.RowPostPaint += (sender, e) =>
            {
                var rowNumber = (e.RowIndex + 1).ToString();
                var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, dataGridView.RowHeadersWidth - 4, e.RowBounds.Height);

                e.Graphics.DrawString(
                    rowNumber,
                    dataGridView.Font,
                    Brushes.Blue,
                    headerBounds,
                    CenterVerticallyFormat);
            };
        }
    }
}