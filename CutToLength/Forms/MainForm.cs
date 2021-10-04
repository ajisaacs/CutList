using Newtonsoft.Json;
using SawCut;
using SawCut.Nesting;
using SimpleExpressionEvaluator;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CutToLength.Forms
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
            var isValid = IsValid();

            runButton.Enabled = isValid;
            saveButton.Enabled = isValid;
        }

        private bool IsValid()
        {
            if (!items.Any(i => i.Length > 0 && i.Quantity > 0))
                return false;

            if (Double.IsNaN(StockLengthInches))
                return false;

            for (int rowIndex = 0; rowIndex < dataGridView1.Rows.Count; rowIndex++)
            {
                var row = dataGridView1.Rows[rowIndex];

                if (!string.IsNullOrWhiteSpace(row.ErrorText))
                {
                    return false;
                }
            }

            return true;
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
                return GetLengthInches(stockLengthBox);
            }
        }

        private double GetLengthInches(TextBox tb)
        {
            try
            {
                double d;

                if (double.TryParse(tb.Text, out d))
                {
                    return d;
                }

                var x = ArchUnits.ParseToInches(tb.Text);
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
            var cutTool = GetSelectedTool();

            var engine = new Engine2();
            engine.Spacing = cutTool.Kerf;
            engine.StockLength = StockLengthInches;

            var items = GetItems();
            var result = engine.Pack(items);

            var form = new ResultsForm();
            form.Bins = result.Bins;
            form.ShowDialog();

            //var saveFileDialog = new SaveFileDialog();
            //saveFileDialog.Filter = "Text File|*.txt";

            //if (saveFileDialog.ShowDialog() == DialogResult.OK)
            //{
            //    SaveBins(saveFileDialog.FileName, bins);
            //}
        }

        private List<BinItem> GetItems()
        {
            var items2 = new List<BinItem>();

            foreach (var item in items)
            {
                if (item.Length == null || item.Length == 0)
                    continue;

                for (int i = 0; i < item.Quantity; i++)
                {
                    items2.Add(new BinItem
                    {
                        Name = item.Name,
                        Length = item.Length.Value
                    });
                }
            }

            return items2;
        }

        public Tool GetSelectedTool()
        {
            return comboBox1.SelectedItem as Tool;
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
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data\\Tools.json"); }
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

        private void DataGridView1_CellValidated(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == lengthDataGridViewTextBoxColumn.Index)
            {
                var item = dataGridView1.Rows[e.RowIndex].DataBoundItem as UIItem;

                if (item == null)
                    return;

                var errorText = string.Empty;

                if (item.Length == null)
                {
                    errorText = "Length is not in a valid format.";
                }

                dataGridView1.Rows[e.RowIndex].ErrorText = errorText;
            }

            UpdateRunButtonState();
        }

        private void StockLengthBox_TextChanged(object sender, EventArgs e)
        {
            UpdateRunButtonState();
        }

        private void dataGridView1_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == lengthDataGridViewTextBoxColumn.Index)
                dataGridView1.Refresh();
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            dataGridView1.Rows[e.RowIndex].ErrorText = e.Exception.InnerException?.Message;
            e.ThrowException = false;
        }
    }
}
