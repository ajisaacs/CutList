using CutList.Models;
using CutList.Presenters;
using CutList.Services;
using SawCut;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace CutList.Forms
{
    public partial class MainForm : Form, IMainView
    {
        private static readonly Random random = new Random();

        private BindingList<PartInputItem> parts;
        private BindingList<BinInputItem> bins;
        private Toolbox toolbox;
        private MainFormPresenter presenter;

        public MainForm()
        {
            InitializeComponent();

            var cutListService = new CutListService();
            var documentService = new DocumentService();
            presenter = new MainFormPresenter(this, cutListService, documentService);

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

            LoadDocumentData(new List<PartInputItem>(), new List<BinInputItem>());
        }

        // IMainView implementation
        public List<PartInputItem> Parts => parts.ToList();
        public List<BinInputItem> StockBins => bins.ToList();
        public Tool SelectedTool => cutMethodComboBox.SelectedItem as Tool;

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ShowWarning(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void ShowInfo(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public bool AskYesNo(string question, string title)
        {
            return MessageBox.Show(question, title, MessageBoxButtons.YesNo) == DialogResult.Yes;
        }

        public bool? AskYesNoCancel(string question, string title)
        {
            var result = MessageBox.Show(question, title, MessageBoxButtons.YesNoCancel);
            if (result == DialogResult.Cancel)
                return null;
            return result == DialogResult.Yes;
        }

        public bool PromptOpenFile(string filter, out string filePath)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = filter
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = openFileDialog.FileName;
                return true;
            }

            filePath = null;
            return false;
        }

        public bool PromptSaveFile(string filter, string defaultFileName, out string filePath)
        {
            var saveFileDialog = new SaveFileDialog
            {
                FileName = defaultFileName,
                Filter = filter
            };

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                filePath = saveFileDialog.FileName;
                return true;
            }

            filePath = null;
            return false;
        }

        public void LoadDocumentData(List<PartInputItem> partsData, List<BinInputItem> stockBinsData)
        {
            parts = new BindingList<PartInputItem>(partsData);
            bins = new BindingList<BinInputItem>(stockBinsData);

            itemBindingSource.DataSource = parts;
            binInputItemBindingSource.DataSource = bins;
        }

        public void ShowResults(List<Bin> binResults, string fileName)
        {
            var form = new ResultsForm(fileName);
            form.Bins = binResults;
            form.ShowDialog();
        }

        public void UpdateRunButtonState(bool enabled)
        {
            runButton.Enabled = enabled;
            saveButton.Enabled = enabled;
        }

        public void ClearData()
        {
            parts = new BindingList<PartInputItem>();
            bins = new BindingList<BinInputItem>();

            itemBindingSource.DataSource = parts;
            binInputItemBindingSource.DataSource = bins;
        }

        // Event handler delegates to presenter
        private void Open()
        {
            presenter.OpenDocument();
        }

        private void DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            dataGridView1.AutoResizeColumns();
            dataGridView2.AutoResizeColumns();
        }

        private void Save()
        {
            // Flush any in-cell edits that haven't committed yet
            dataGridView1.EndEdit();
            dataGridView2.EndEdit();

            presenter.SaveDocument();
        }

        private void Run()
        {
            // Flush any in-cell edits that haven't committed yet
            dataGridView1.EndEdit();
            dataGridView2.EndEdit();

            presenter.Run();
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
            presenter.UpdateRunButtonState();
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
            presenter.UpdateRunButtonState();
        }

        private void ItemBindingSource_ListChanged(object sender, ListChangedEventArgs e)
        {
            presenter.UpdateRunButtonState();
        }

        private void newDocumentButton_Click(object sender, EventArgs e)
        {
            presenter.NewDocument();
        }

        private void loadExampleDataButton_Click(object sender, EventArgs e)
        {
            presenter.OnLoadExampleDataRequested();
        }
    }
}