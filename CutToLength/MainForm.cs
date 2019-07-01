using Newtonsoft.Json;
using SimpleExpressionEvaluator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CutToLength
{
    public partial class MainForm : Form
    {
        private BindingList<UIItem> items;

        public MainForm()
        {
            InitializeComponent();

            items = new BindingList<UIItem>();
            items.ListChanged += Items_ListChanged;

            itemBindingSource.DataSource = items;
            itemBindingSource.ListChanged += ItemBindingSource_ListChanged;

            if (!File.Exists(ToolsFilePath))
            {
                var tools = new List<Tool>
                {
                    new Tool { Name = "Shear", Kerf = 0.0 },
                    new Tool { Name = "Saw", Kerf = 0.125 }
                };

                SaveTools(tools);

                comboBox1.DataSource = tools;
            }
            else
            {
                comboBox1.DataSource = GetTools();
            }
        }

        private void ItemBindingSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateRunButtonState();
        }

        private void Items_ListChanged(object sender, ListChangedEventArgs e)
        {
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateRunButtonState();
        }

        private void UpdateRunButtonState()
        {
            var hasItems = items.Any(i => i.Length > 0 && i.Quantity > 0);
            var validStockLength = !Double.IsNaN(StockLengthInches);

            runButton.Enabled = hasItems && validStockLength;
            saveButton.Enabled = hasItems;
        }

        private void Open()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "Json File|*.json";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var data = File.ReadAllText(openFileDialog.FileName);
                items = JsonConvert.DeserializeObject<BindingList<UIItem>>(data);

                dataGridView1.ClearSelection();
                itemBindingSource.DataSource = items;
            }
        }

        private void Save()
        {
            var itemsToSave = items;

            if (dataGridView1.Rows[items.Count - 1].IsNewRow == true)
                itemsToSave.RemoveAt(itemsToSave.Count - 1);

            var json = JsonConvert.SerializeObject(itemsToSave, Formatting.Indented);

            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Json File|*.json";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                File.WriteAllText(saveFileDialog.FileName, json);
        }

        private double StockLengthInches
        {
            get
            {
                var feet = GetDouble(feetBox);
                var inches = GetDouble(inchesBox);

                return feet * 12 + inches;
            }
        }

        private double GetDouble(TextBox tb)
        {
            try
            {
                var x = double.Parse(tb.Text);
                tb.ForeColor = SystemColors.WindowText;
                return x;

            }
            catch
            {
                tb.ForeColor = Color.Red;
                return double.NaN;
            }
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
            var length = StockLengthInches;

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
            var length = StockLengthInches;
            var spacing = GetSelectedTool().Kerf;

            return new Bin(length)
            {
                Spacing = spacing
            };
        }

        public Tool GetSelectedTool()
        {
            return comboBox1.SelectedItem as Tool;
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

        private void saveButton_Click(object sender, EventArgs e)
        {
            Save();
        }

        private void runButton_Click(object sender, EventArgs e)
        {
            Run();
        }

        private string ToolsFilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools.json"); }
        }

        private List<Tool> GetTools()
        {
            var json = File.ReadAllText(ToolsFilePath);
            var list = JsonConvert.DeserializeObject<List<Tool>>(json);

            return list;
        }

        private void SaveTools(IEnumerable<Tool> tools)
        {
            var json = JsonConvert.SerializeObject(tools, Formatting.Indented);
            File.WriteAllText(ToolsFilePath, json);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var tool = comboBox1.SelectedItem as Tool;

            if (tool == null)
                return;

            textBox1.Text = tool.Kerf.ToString();

            if (tool.AllowUserToChange)
            {
                textBox1.ReadOnly = false;
                textBox1.BackColor = Color.White;
            }
            else
            {
                textBox1.ReadOnly = true;
                textBox1.BackColor = SystemColors.Info;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            double value;

            if (!double.TryParse(textBox1.Text, out value))
                return;

            var tool = comboBox1.SelectedItem as Tool;

            if (tool == null)
                return;

            if (!tool.AllowUserToChange)
                return;

            tool.Kerf = value;

            var tools = comboBox1.DataSource as List<Tool>;

            if (tools != null)
            {
                SaveTools(tools);
            }
        }

        private void TextBox2_Validating(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                var ee = new ExpressionEvaluator();
                var x = ee.Evaluate(feetBox.Text);
                feetBox.Text = x.ToString();
                feetBox.ForeColor = SystemColors.WindowText;
            }
            catch
            {
                feetBox.ForeColor = Color.Red;
            }
        }

        private void TextBox2_KeyPress(object sender, KeyPressEventArgs e)
        {

        }

        private void DataGridView1_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            UpdateRunButtonState();

        }

        private void FeetBox_TextChanged(object sender, EventArgs e)
        {
            UpdateRunButtonState();

        }

        private void InchesBox_TextChanged(object sender, EventArgs e)
        {
            UpdateRunButtonState();

        }
    }
}
