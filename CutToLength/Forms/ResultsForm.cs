using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CutToLength.Forms
{
	public partial class ResultsForm : Form
	{
		public ResultsForm()
		{
			InitializeComponent();

			dataGridView1.RowPostPaint += DataGridView1_RowPostPaint;
		}

		private void DataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
		{
			var grid = sender as DataGridView;
			var rowIdx = (e.RowIndex + 1).ToString();

			var centerFormat = new StringFormat()
			{
				Alignment = StringAlignment.Far,
				LineAlignment = StringAlignment.Center
			};

			var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth - 4, e.RowBounds.Height);
			e.Graphics.DrawString(rowIdx, this.Font, Brushes.Blue, headerBounds, centerFormat);
		}

		private void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
		{
			var selectedBin = dataGridView1.Rows[e.RowIndex].DataBoundItem as Bin;

			if (selectedBin == null)
				return;

			binLayoutView1.Bin = selectedBin;
			binLayoutView1.Invalidate();

			dataGridView2.DataSource = selectedBin.Items;
		}

		public List<Bin> Bins
		{
			get { return dataGridView1.DataSource as List<Bin>; }
			set { dataGridView1.DataSource = value; }
		}
	}
}
