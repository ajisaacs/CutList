using SawCut;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CutList.Forms
{
    public partial class ResultsForm : Form
    {
        private string filename;

        public ResultsForm(string filename)
        {
            InitializeComponent();
            dataGridView1.DrawRowNumbers();
            dataGridView2.DrawRowNumbers();

            this.filename = filename;
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

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Save();
        }

        public void Save(string filepath)
        {
            var writer = new BinFileSaver(Bins);
            writer.SaveBinsToFile(filepath);
        }

        public void Save()
        {
            var s = new SaveFileDialog();

            s.FileName = filename;
            s.Filter = "Text File|*.txt";

            if (s.ShowDialog() == DialogResult.OK)
                Save(s.FileName);
        }
    }
}