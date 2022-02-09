using CutList.Models;
using Newtonsoft.Json;
using SawCut;
using SawCut.Nesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CutList.Forms
{
    public partial class MainForm : Form
    {
        private BindingList<PartInputItem> parts;
        private BindingList<BinInputItem> bins;
        private string documentPath;

        public MainForm()
        {
            InitializeComponent();

            parts = new BindingList<PartInputItem>();
            bins = new BindingList<BinInputItem>();

            dataGridView1.DrawingRowNumbers();
            dataGridView2.DrawingRowNumbers();

            itemBindingSource.DataSource = parts;
            itemBindingSource.ListChanged += ItemBindingSource_ListChanged;

            binInputItemBindingSource.DataSource = bins;
            binInputItemBindingSource.ListChanged += BinInputItemBindingSource_ListChanged;

            LoadTools();
        }

        private void LoadTools()
        {
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

        private void UpdateRunButtonState()
        {
            var isValid = IsValid();

            runButton.Enabled = isValid;
            saveButton.Enabled = isValid;
        }

        private bool IsValid()
        {
            if (!parts.Any(i => i.Length > 0 && i.Quantity > 0))
                return false;

            if (!bins.Any(i => i.Length > 0 && i.Quantity > 0))
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
                documentPath = openFileDialog.FileName;
                var data = File.ReadAllText(documentPath);
                parts = JsonConvert.DeserializeObject<BindingList<PartInputItem>>(data);

                dataGridView1.ClearSelection();
                itemBindingSource.DataSource = parts;
                UpdateRunButtonState();
            }
        }

        private void Save()
        {
            var itemsToSave = parts;

            if (dataGridView1.Rows[parts.Count - 1].IsNewRow == true)
                itemsToSave.RemoveAt(itemsToSave.Count - 1);

            var json = JsonConvert.SerializeObject(itemsToSave, Formatting.Indented);

            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = documentPath == null ? null : Path.GetFileName(documentPath);
            saveFileDialog.Filter = "Json File|*.json";

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                File.WriteAllText(saveFileDialog.FileName, json);
        }

        private void Run()
        {
            dataGridView1.EndEdit();
            dataGridView2.EndEdit();

            var cutTool = GetSelectedTool();
            var stockBins = new List<MultiBin>();

            foreach (var item in bins)
            {
                stockBins.Add(new MultiBin
                {
                    Length = item.Length.Value,
                    Quantity = item.Quantity,
                    Priority = item.Priority
                });
            }

            var engine = new MultiBinEngine();
            engine.Spacing = cutTool.Kerf;
            engine.Bins = stockBins;

            var items = GetItems();
            var result = engine.Pack(items);

            var filename = GetResultsSaveName();
            var form = new ResultsForm(filename);
            form.Bins = result.Bins;
            form.ShowDialog();
        }

        private string GetResultsSaveName()
        {
            if (documentPath != null)
                return Path.GetFileNameWithoutExtension(documentPath);

            var today = DateTime.Today;
            var year = today.Year.ToString();
            var month = today.Month.ToString().PadLeft(2, '0');
            var day = today.Day.ToString().PadLeft(2, '0');
            var name = $"Cut List {year}-{month}-{day}";

            return name;
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

        private List<BinItem> GetItems()
        {
            var items2 = new List<BinItem>();

            foreach (var item in parts)
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

        private void SaveTools(IEnumerable<Tool> tools)
        {
            var json = JsonConvert.SerializeObject(tools, Formatting.Indented);
            File.WriteAllText(ToolsFilePath, json);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateRunButtonState();
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

        private void dataGridView2_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void dataGridView2_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == lengthInputValueDataGridViewTextBoxColumn.Index)
                dataGridView2.Refresh();
        }

        private void BinInputItemBindingSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateRunButtonState();
        }

        private void ItemBindingSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            UpdateRunButtonState();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            documentPath = null;
            parts = new BindingList<PartInputItem>();
            bins = new BindingList<BinInputItem>();

            itemBindingSource.DataSource = parts;
            binInputItemBindingSource.DataSource = bins;
            UpdateRunButtonState();
        }
    }

    public class Document
    {
        public Document()
        {

        }

        [JsonIgnore]
        public string SavePath { get; set; }

        public List<Tool> Tools { get; set; }

        public List<PartInputItem> PartsToNest  { get; set; }

        public List<BinInputItem> StockBins { get; set; }
    }
}