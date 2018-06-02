using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CutToLength
{
    public partial class MainForm : Form
    {
        private List<UIItem> items;

        public MainForm()
        {
            InitializeComponent();

            items = new List<UIItem>();

            itemBindingSource.DataSource = items;
        }

        private void Open()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "Json File|*.json";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var data = File.ReadAllText(openFileDialog.FileName);
                items = JsonConvert.DeserializeObject<List<UIItem>>(data);
                itemBindingSource.DataSource = items;
            }
        }

        private void Save()
        {
            var json = JsonConvert.SerializeObject(items, Formatting.Indented);

            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Json File|*.json";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                File.WriteAllText(saveFileDialog.FileName, json);
        }

        private void Run()
        {
            var bins = GetResults();

            var form = new ResultsForm();
            form.Bins = bins;
            form.ShowDialog();

            //var saveFileDialog = new SaveFileDialog();
            //saveFileDialog.Filter = "Text File|*.txt";

            //if (saveFileDialog.ShowDialog() == DialogResult.OK)
            //{
            //    SaveBins(saveFileDialog.FileName, bins);
            //}
        }

        private List<Bin> GetResults()
        {
            var items2 = GetItems().OrderByDescending(i => i.Length);
            var bins = new List<Bin>();
            var length = (double)numericUpDown1.Value;

            foreach (var item in items2)
            {
                Bin best_bin;

                if (!FindBin(bins.ToArray(), item.Length, out best_bin))
                {
                    if (item.Length > length)
                        continue;

                    best_bin = CreateBin();
                    bins.Add(best_bin);
                }

                best_bin.Items.Add(item);
            }

            return bins;
        }

        private List<BinItem> GetItems()
        {
            var items2 = new List<BinItem>();

            foreach (var item in items)
            {
                if (item.Length == 0)
                    continue;

                for (int i = 0; i < item.Quantity; i++)
                {
                    items2.Add(new BinItem
                    {
                        Name = item.Name,
                        Length = item.Length
                    });
                }
            }

            return items2;
        }

        private Bin CreateBin()
        {
            var length = (double)numericUpDown1.Value;
            var spacing = (double)numericUpDown2.Value;

            return new Bin(length)
            {
                Spacing = spacing
            };
        }

        private void SaveBins(string file, IEnumerable<Bin> bins)
        {
            var writer = new StreamWriter(file);
            writer.AutoFlush = true;

            var max = bins.Max(b => b.Items.Max(i => i.Length.ToString().Length));
            var id = 1;

            foreach (var bin in bins)
            {
                writer.WriteLine(id++.ToString() + ". " + bin.ToString());

                var groups = bin.Items.GroupBy(i => i.Name);

                foreach (var group in groups)
                {
                    writer.WriteLine("   {0}  {1}\" - {2}", ("(" + group.Count() + ")").PadRight(5), group.First().Length.ToString().PadLeft(max + 2), group.Key);
                }

                writer.WriteLine("---------------------------------------------------------------------");
                writer.WriteLine();
            }

            writer.Close();

            Process.Start(file);
        }

        private static bool FindBin(IEnumerable<Bin> bins, double length, out Bin found)
        {
            found = null;

            foreach (var bin in bins)
            {
                if (bin.RemainingLength < length)
                    continue;

                if (found == null)
                    found = bin;

                if (bin.RemainingLength < found.RemainingLength)
                    found = bin;
            }

            return (found != null);
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Open();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            Run();
        }
    }
}
