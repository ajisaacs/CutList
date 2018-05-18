using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CutToLength
{
    public partial class Form1 : Form
    {
        private List<UIItem> items;

        public Form1()
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

			var form = new Form2();
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

    public class Bin
    {
        public List<BinItem> Items;

        public Bin(double length)
        {
            Items = new List<BinItem>();
            Length = length;
        }

        public double Spacing { get; set; }

        public double Length { get; set; }

        public double UsedLength
        {
            get
            {
                return Items.Sum(i => i.Length) + Spacing * Items.Count;
            }
        }

        public double RemainingLength
        {
            get { return Length - UsedLength; }
        }

        public double Utilization
        {
            get { return (UsedLength / Length * 100.0); }
        }

        public override string ToString()
        {
            return string.Format(
                "Length: {0}\", {1}\" remaining, {2} items, {3}% utilization",
                Math.Round(Length, 4),
                Math.Round(RemainingLength, 4),
                Items.Count,
                Math.Round(Utilization, 2));
        }
    }

    public class BinItem
    {
        public string Name { get; set; }

        public double Length { get; set; }
    }

    public class UIItem
    {
        private string name;
        
        public UIItem()
        {
        }

        public string Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(name) ?
                    GetDefaultName() :
                    name;
            }
            set
            {
                name = value;
            }
        }

        public double Length { get; set; }

        public double TotalLength
        {
            get { return Length * Quantity; }
        }

        public int Quantity { get; set; } = 1;

        private string GetDefaultName()
        {
            if (Length == 0)
                return "-";

            return string.Format("{0}\" LG", Length);
        }
    }
}
