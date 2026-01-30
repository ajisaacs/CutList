namespace CutList.Forms
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            dataGridView1 = new DataGridView();
            nameDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            lengthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            quantityDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            TotalLength = new DataGridViewTextBoxColumn();
            itemBindingSource = new BindingSource(components);
            toolStrip1 = new ToolStrip();
            newDocumentButton = new ToolStripButton();
            openFileButton = new ToolStripButton();
            saveButton = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            runButton = new ToolStripButton();
            loadExampleDataButton = new ToolStripButton();
            cutMethodComboBox = new ComboBox();
            cutWidthTextBox = new TextBox();
            cutMethodLabel = new Label();
            cutWidthLabel = new Label();
            tabControl1 = new TabControl();
            tabPage2 = new TabPage();
            tabPage1 = new TabPage();
            dataGridView2 = new DataGridView();
            lengthInputValueDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            quantityDataGridViewTextBoxColumn1 = new DataGridViewTextBoxColumn();
            TotalLengthString = new DataGridViewTextBoxColumn();
            priorityDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            binInputItemBindingSource = new BindingSource(components);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)itemBindingSource).BeginInit();
            toolStrip1.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)binInputItemBindingSource).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dataGridView1.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView1.ColumnHeadersHeight = 30;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { nameDataGridViewTextBoxColumn, lengthDataGridViewTextBoxColumn, quantityDataGridViewTextBoxColumn, TotalLength });
            dataGridView1.DataSource = itemBindingSource;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.GridColor = Color.FromArgb(224, 224, 224);
            dataGridView1.Location = new Point(3, 3);
            dataGridView1.Margin = new Padding(3, 4, 3, 4);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView1.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView1.RowTemplate.Height = 26;
            dataGridView1.Size = new Size(806, 573);
            dataGridView1.TabIndex = 11;
            dataGridView1.CellEndEdit += dataGridView1_CellEndEdit;
            dataGridView1.CellValidated += dataGridView1_CellEndEdit;
            dataGridView1.DataError += dataGridView1_DataError;
            // 
            // nameDataGridViewTextBoxColumn
            // 
            nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
            nameDataGridViewTextBoxColumn.HeaderText = "Name";
            nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            nameDataGridViewTextBoxColumn.Width = 200;
            // 
            // lengthDataGridViewTextBoxColumn
            // 
            lengthDataGridViewTextBoxColumn.DataPropertyName = "LengthInputValue";
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleRight;
            lengthDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle1;
            lengthDataGridViewTextBoxColumn.HeaderText = "Length";
            lengthDataGridViewTextBoxColumn.Name = "lengthDataGridViewTextBoxColumn";
            lengthDataGridViewTextBoxColumn.Width = 120;
            // 
            // quantityDataGridViewTextBoxColumn
            // 
            quantityDataGridViewTextBoxColumn.DataPropertyName = "Quantity";
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleRight;
            quantityDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle2;
            quantityDataGridViewTextBoxColumn.HeaderText = "Qty";
            quantityDataGridViewTextBoxColumn.Name = "quantityDataGridViewTextBoxColumn";
            quantityDataGridViewTextBoxColumn.Width = 50;
            // 
            // TotalLength
            // 
            TotalLength.DataPropertyName = "TotalLengthString";
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle3.BackColor = SystemColors.Info;
            dataGridViewCellStyle3.Format = "N3";
            TotalLength.DefaultCellStyle = dataGridViewCellStyle3;
            TotalLength.HeaderText = "Total Length";
            TotalLength.Name = "TotalLength";
            TotalLength.ReadOnly = true;
            TotalLength.Width = 150;
            // 
            // itemBindingSource
            // 
            itemBindingSource.DataSource = typeof(Models.PartInputItem);
            // 
            // toolStrip1
            // 
            toolStrip1.Items.AddRange(new ToolStripItem[] { newDocumentButton, openFileButton, saveButton, toolStripSeparator1, runButton, loadExampleDataButton });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(844, 39);
            toolStrip1.TabIndex = 0;
            toolStrip1.Text = "toolStrip1";
            // 
            // newDocumentButton
            // 
            newDocumentButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            newDocumentButton.Image = Properties.Resources.gnome_document_new;
            newDocumentButton.ImageScaling = ToolStripItemImageScaling.None;
            newDocumentButton.ImageTransparentColor = Color.Magenta;
            newDocumentButton.Name = "newDocumentButton";
            newDocumentButton.Padding = new Padding(5, 0, 5, 0);
            newDocumentButton.Size = new Size(46, 36);
            newDocumentButton.Text = "New";
            newDocumentButton.Click += newDocumentButton_Click;
            // 
            // openFileButton
            // 
            openFileButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            openFileButton.Image = Properties.Resources.Open_Folder_32;
            openFileButton.ImageScaling = ToolStripItemImageScaling.None;
            openFileButton.ImageTransparentColor = Color.Magenta;
            openFileButton.Name = "openFileButton";
            openFileButton.Padding = new Padding(5, 0, 5, 0);
            openFileButton.Size = new Size(46, 36);
            openFileButton.Text = "Open";
            openFileButton.Click += openFileButton_Click;
            // 
            // saveButton
            // 
            saveButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            saveButton.Image = Properties.Resources.Save_32;
            saveButton.ImageScaling = ToolStripItemImageScaling.None;
            saveButton.ImageTransparentColor = Color.Magenta;
            saveButton.Name = "saveButton";
            saveButton.Padding = new Padding(5, 0, 5, 0);
            saveButton.Size = new Size(46, 36);
            saveButton.Text = "Save";
            saveButton.Click += saveButton_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(6, 39);
            // 
            // runButton
            // 
            runButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            runButton.Image = Properties.Resources.Circled_Play_32;
            runButton.ImageScaling = ToolStripItemImageScaling.None;
            runButton.ImageTransparentColor = Color.Magenta;
            runButton.Name = "runButton";
            runButton.Padding = new Padding(5, 0, 5, 0);
            runButton.Size = new Size(46, 36);
            runButton.Text = "Run";
            runButton.Click += runButton_Click;
            // 
            // loadExampleDataButton
            // 
            loadExampleDataButton.Alignment = ToolStripItemAlignment.Right;
            loadExampleDataButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            loadExampleDataButton.ForeColor = Color.DimGray;
            loadExampleDataButton.Image = Properties.Resources.Circled_Play_32;
            loadExampleDataButton.ImageScaling = ToolStripItemImageScaling.None;
            loadExampleDataButton.ImageTransparentColor = Color.Magenta;
            loadExampleDataButton.Name = "loadExampleDataButton";
            loadExampleDataButton.Padding = new Padding(5, 0, 5, 0);
            loadExampleDataButton.Size = new Size(122, 36);
            loadExampleDataButton.Text = "Load Example Data";
            loadExampleDataButton.Click += loadExampleDataButton_Click;
            // 
            // cutMethodComboBox
            // 
            cutMethodComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            cutMethodComboBox.FormattingEnabled = true;
            cutMethodComboBox.Location = new Point(107, 22);
            cutMethodComboBox.Name = "cutMethodComboBox";
            cutMethodComboBox.Size = new Size(184, 25);
            cutMethodComboBox.TabIndex = 7;
            cutMethodComboBox.SelectedIndexChanged += cutMethodComboBox_SelectedIndexChanged;
            // 
            // cutWidthTextBox
            // 
            cutWidthTextBox.Location = new Point(107, 53);
            cutWidthTextBox.Name = "cutWidthTextBox";
            cutWidthTextBox.Size = new Size(184, 25);
            cutWidthTextBox.TabIndex = 9;
            cutWidthTextBox.TextChanged += cutWidthTextBox_TextChanged;
            // 
            // cutMethodLabel
            // 
            cutMethodLabel.AutoSize = true;
            cutMethodLabel.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            cutMethodLabel.ForeColor = Color.Blue;
            cutMethodLabel.Location = new Point(20, 25);
            cutMethodLabel.Name = "cutMethodLabel";
            cutMethodLabel.Size = new Size(81, 17);
            cutMethodLabel.TabIndex = 6;
            cutMethodLabel.Text = "Cut method";
            // 
            // cutWidthLabel
            // 
            cutWidthLabel.AutoSize = true;
            cutWidthLabel.Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 0);
            cutWidthLabel.ForeColor = Color.Blue;
            cutWidthLabel.Location = new Point(34, 56);
            cutWidthLabel.Name = "cutWidthLabel";
            cutWidthLabel.Size = new Size(67, 17);
            cutWidthLabel.TabIndex = 8;
            cutWidthLabel.Text = "Cut width";
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Location = new Point(12, 42);
            tabControl1.Name = "tabControl1";
            tabControl1.Padding = new Point(20, 3);
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(820, 609);
            tabControl1.TabIndex = 12;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(dataGridView1);
            tabPage2.Location = new Point(4, 26);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(812, 579);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "ITEMS TO NEST";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(dataGridView2);
            tabPage1.Controls.Add(cutWidthTextBox);
            tabPage1.Controls.Add(cutMethodComboBox);
            tabPage1.Controls.Add(cutWidthLabel);
            tabPage1.Controls.Add(cutMethodLabel);
            tabPage1.Location = new Point(4, 26);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(812, 579);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "STOCK LENGTHS";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // dataGridView2
            // 
            dataGridView2.AllowUserToResizeRows = false;
            dataGridView2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridView2.AutoGenerateColumns = false;
            dataGridView2.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dataGridView2.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView2.ColumnHeadersHeight = 30;
            dataGridView2.Columns.AddRange(new DataGridViewColumn[] { lengthInputValueDataGridViewTextBoxColumn, quantityDataGridViewTextBoxColumn1, TotalLengthString, priorityDataGridViewTextBoxColumn });
            dataGridView2.DataSource = binInputItemBindingSource;
            dataGridView2.GridColor = Color.FromArgb(224, 224, 224);
            dataGridView2.Location = new Point(6, 103);
            dataGridView2.Margin = new Padding(3, 4, 3, 4);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView2.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dataGridView2.RowTemplate.Height = 26;
            dataGridView2.Size = new Size(800, 467);
            dataGridView2.TabIndex = 12;
            dataGridView2.CellEndEdit += dataGridView2_CellEndEdit;
            // 
            // lengthInputValueDataGridViewTextBoxColumn
            // 
            lengthInputValueDataGridViewTextBoxColumn.DataPropertyName = "LengthInputValue";
            lengthInputValueDataGridViewTextBoxColumn.HeaderText = "Length";
            lengthInputValueDataGridViewTextBoxColumn.Name = "lengthInputValueDataGridViewTextBoxColumn";
            // 
            // quantityDataGridViewTextBoxColumn1
            // 
            quantityDataGridViewTextBoxColumn1.DataPropertyName = "Quantity";
            quantityDataGridViewTextBoxColumn1.HeaderText = "Quantity";
            quantityDataGridViewTextBoxColumn1.Name = "quantityDataGridViewTextBoxColumn1";
            // 
            // TotalLengthString
            // 
            TotalLengthString.DataPropertyName = "TotalLengthString";
            dataGridViewCellStyle4.BackColor = SystemColors.Info;
            TotalLengthString.DefaultCellStyle = dataGridViewCellStyle4;
            TotalLengthString.HeaderText = "Total Length";
            TotalLengthString.Name = "TotalLengthString";
            TotalLengthString.ReadOnly = true;
            // 
            // priorityDataGridViewTextBoxColumn
            // 
            priorityDataGridViewTextBoxColumn.DataPropertyName = "Priority";
            priorityDataGridViewTextBoxColumn.HeaderText = "Priority";
            priorityDataGridViewTextBoxColumn.Name = "priorityDataGridViewTextBoxColumn";
            // 
            // binInputItemBindingSource
            // 
            binInputItemBindingSource.DataSource = typeof(Models.BinInputItem);
            // 
            // MainForm
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(844, 663);
            Controls.Add(tabControl1);
            Controls.Add(toolStrip1);
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Margin = new Padding(3, 4, 3, 4);
            MinimumSize = new Size(570, 457);
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Cut List";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)itemBindingSource).EndInit();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabPage2.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ((System.ComponentModel.ISupportInitialize)binInputItemBindingSource).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.BindingSource itemBindingSource;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton openFileButton;
        private System.Windows.Forms.ToolStripButton saveButton;
        private System.Windows.Forms.ToolStripButton runButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ComboBox cutMethodComboBox;
        private System.Windows.Forms.TextBox cutWidthTextBox;
        private System.Windows.Forms.Label cutMethodLabel;
        private System.Windows.Forms.Label cutWidthLabel;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.BindingSource binInputItemBindingSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn lengthInputValueDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn quantityDataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalLengthString;
        private System.Windows.Forms.DataGridViewTextBoxColumn priorityDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn lengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn quantityDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn TotalLength;
        private System.Windows.Forms.ToolStripButton newDocumentButton;
        private System.Windows.Forms.ToolStripButton loadExampleDataButton;
    }
}

