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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CutList.Forms
{
    public partial class MainForm : Form
    {
        private static readonly Random random = new Random();

        private BindingList<PartInputItem> parts;
        private BindingList<BinInputItem> bins;
        private Toolbox toolbox;
        private Document currentDocument;

        public MainForm()
        {
            InitializeComponent();

            dataGridView1.DrawRowNumbers();
            dataGridView2.DrawRowNumbers();

            itemBindingSource.DataSource = parts;
            itemBindingSource.ListChanged += ItemBindingSource_ListChanged;

            binInputItemBindingSource.DataSource = bins;
            binInputItemBindingSource.ListChanged += BinInputItemBindingSource_ListChanged;

            toolbox = new Toolbox();
            cutMethodComboBox.DataSource = toolbox.Tools;

            currentDocument = new Document();

#if DEBUG
            loadExampleDataButton.Visible = true;
#else
            loadExampleDataButton.Visible = false;
#endif

            LoadDocumentData();
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

            if (!bins.Any(i => i.Length > 0 && (i.Quantity > 0 || i.Quantity == -1)))
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
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Multiselect = false,
                    Filter = "Json File|*.json"
                };

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentDocument = Document.Load(openFileDialog.FileName);

                    if (currentDocument == null)
                        return;

                    LoadDocumentData();
                    UpdateRunButtonState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDocumentData()
        {
            parts = new BindingList<PartInputItem>(currentDocument.PartsToNest);
            bins = new BindingList<BinInputItem>(currentDocument.StockBins);

            itemBindingSource.DataSource = parts;
            binInputItemBindingSource.DataSource = bins;
        }

        private void DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dataGridView1.AutoResizeColumns();
            dataGridView2.AutoResizeColumns();
        }

        private void Save()
        {
            if (!currentDocument.Validate(out string validationMessage))
            {
                MessageBox.Show(validationMessage, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = currentDocument.LastFilePath == null ? "NewDocument.json" : Path.GetFileName(currentDocument.LastFilePath),
                Filter = "Json File|*.json"
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                currentDocument.Save(saveFileDialog.FileName);
            }
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
            var today = DateTime.Today;
            var year = today.Year.ToString();
            var month = today.Month.ToString().PadLeft(2, '0');
            var day = today.Day.ToString().PadLeft(2, '0');
            var name = $"Cut List {year}-{month}-{day}";

            return name;
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
            return cutMethodComboBox.SelectedItem as Tool;
        }

        private double GetRandomLength(double min, double max)
        {
            return Math.Round(random.NextDouble() * (max - min) + min, 2);
        }

        private void LoadExampleData(bool clearCurrentData = true)
        {
            const int PartCount = 25;
            const double Min = 1;
            const double Max = 60;

            if (clearCurrentData)
            {
                parts.Clear();
                bins.Clear();
            }

            var random = new Random();

            for (int i = 0; i < PartCount; i++)
            {
                var length = GetRandomLength(Min, Max);

                parts.Add(new PartInputItem
                {
                    Name = $"Part {i + 1}",
                    LengthInputValue = length.ToString(),
                    Quantity = random.Next(1, 100)
                });
            }

            bins.Add(new BinInputItem
            {
                LengthInputValue = "144\"",
                Quantity = 9999
            });
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            UpdateRunButtonState();
        }

        private void openFileButton_Click(object sender, EventArgs e)
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

        private void cutMethodComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var tool = cutMethodComboBox.SelectedItem as Tool;

            if (tool == null)
                return;

            cutWidthTextBox.Text = tool.Kerf.ToString();

            if (tool.AllowUserToChange)
            {
                cutWidthTextBox.ReadOnly = false;
                cutWidthTextBox.BackColor = Color.White;
            }
            else
            {
                cutWidthTextBox.ReadOnly = true;
                cutWidthTextBox.BackColor = SystemColors.Info;
            }
        }

        private void cutWidthTextBox_TextChanged(object sender, EventArgs e)
        {
            double value;

            if (!double.TryParse(cutWidthTextBox.Text, out value))
                return;

            var tool = cutMethodComboBox.SelectedItem as Tool;

            if (tool == null)
                return;

            if (!tool.AllowUserToChange)
                return;

            tool.Kerf = value;

            var tools = cutMethodComboBox.DataSource as List<Tool>;

            if (tools != null)
            {
                toolbox.Save();
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

        private void newDocumentButton_Click(object sender, EventArgs e)
        {
            parts = new BindingList<PartInputItem>();
            bins = new BindingList<BinInputItem>();

            itemBindingSource.DataSource = parts;
            binInputItemBindingSource.DataSource = bins;
            UpdateRunButtonState();
        }
        
        private void loadExampleDataButton_Click(object sender, EventArgs e)
        {
            var clearData = true;

            if (parts.Count > 0 || bins.Count > 0)
            {
                var dialogResult = MessageBox.Show("Are you sure you want to clear the current data?", "Clear Data", MessageBoxButtons.YesNoCancel);

                if (dialogResult == DialogResult.Cancel)
                    return;

                clearData = dialogResult == DialogResult.Yes;
            }

            LoadExampleData(clearData);
        }
    }
}