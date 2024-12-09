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
            var s = new SaveFileDialog();

            s.FileName = filename;
            s.Filter = "Text File|*.txt";

            if (s.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            SaveBins(s.FileName);
        }

        private void SaveBins(string file)
        {
            var writer = new StreamWriter(file);
            writer.AutoFlush = true;

            var max = Bins.Max(b => b.Items.Max(i => SawCut.Helper.ConvertToMixedFraction(i.Length).Length));
            var id = 1;

            foreach (var bin in Bins)
            {
                var totalLength = SawCut.Helper.ConvertToMixedFraction(bin.Length);
                var remainingLength = SawCut.Helper.ConvertToMixedFraction(bin.RemainingLength);
                var utilitation = Math.Round(bin.Utilization * 100, 2);

                var binDescription = $"{id++}. Length: {{totalLength}}, {remainingLength} remaining, {bin.Items.Count} items, {utilitation}% utilization";
                writer.WriteLine(binDescription);

                var groups = bin.Items.GroupBy(i => $"{i.Name} {i.Length}");

                foreach (var group in groups)
                {
                    var first = group.First();
                    var count = group.Count();
                    var length = SawCut.Helper.ConvertToMixedFraction(first.Length).PadLeft(max);
                    var name = first.Name;

                    var pcsSingularOrPlural = count == 1 ? "pc " : "pcs";

                    writer.WriteLine($"   {count}{pcsSingularOrPlural}  @  {length} LG   Tag: {name}");
                }

                writer.WriteLine("---------------------------------------------------------------------");
                //writer.WriteLine();
            }

            writer.Close();

            Process.Start(file);
        }
    }
}