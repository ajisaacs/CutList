namespace CutList.Forms
{
    partial class ResultsForm
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
            dataGridView1 = new DataGridView();
            countDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            spacingDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            lengthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            usedLengthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            remainingLengthDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            utilizationDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            binBindingSource = new BindingSource(components);
            splitContainer1 = new SplitContainer();
            splitContainer2 = new SplitContainer();
            dataGridView2 = new DataGridView();
            binLayoutView1 = new CutList.Controls.BinLayoutView();
            label1 = new Label();
            uIItemBindingSource = new BindingSource(components);
            menuStrip1 = new MenuStrip();
            saveToolStripMenuItem = new ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)binBindingSource).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
            splitContainer2.Panel1.SuspendLayout();
            splitContainer2.Panel2.SuspendLayout();
            splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)uIItemBindingSource).BeginInit();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.BackgroundColor = Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView1.ColumnHeadersHeight = 30;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { countDataGridViewTextBoxColumn, spacingDataGridViewTextBoxColumn, lengthDataGridViewTextBoxColumn, usedLengthDataGridViewTextBoxColumn, remainingLengthDataGridViewTextBoxColumn, utilizationDataGridViewTextBoxColumn });
            dataGridView1.DataSource = binBindingSource;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.GridColor = Color.FromArgb(224, 224, 224);
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView1.Size = new Size(959, 469);
            dataGridView1.TabIndex = 0;
            dataGridView1.RowEnter += dataGridView1_RowEnter;
            // 
            // countDataGridViewTextBoxColumn
            // 
            countDataGridViewTextBoxColumn.DataPropertyName = "Count";
            countDataGridViewTextBoxColumn.HeaderText = "Count";
            countDataGridViewTextBoxColumn.Name = "countDataGridViewTextBoxColumn";
            countDataGridViewTextBoxColumn.ReadOnly = true;
            countDataGridViewTextBoxColumn.Width = 60;
            // 
            // spacingDataGridViewTextBoxColumn
            // 
            spacingDataGridViewTextBoxColumn.DataPropertyName = "Spacing";
            spacingDataGridViewTextBoxColumn.HeaderText = "Spacing";
            spacingDataGridViewTextBoxColumn.Name = "spacingDataGridViewTextBoxColumn";
            spacingDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // lengthDataGridViewTextBoxColumn
            // 
            lengthDataGridViewTextBoxColumn.DataPropertyName = "Length";
            lengthDataGridViewTextBoxColumn.HeaderText = "Length";
            lengthDataGridViewTextBoxColumn.Name = "lengthDataGridViewTextBoxColumn";
            lengthDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // usedLengthDataGridViewTextBoxColumn
            // 
            usedLengthDataGridViewTextBoxColumn.DataPropertyName = "UsedLength";
            usedLengthDataGridViewTextBoxColumn.HeaderText = "Used Length";
            usedLengthDataGridViewTextBoxColumn.Name = "usedLengthDataGridViewTextBoxColumn";
            usedLengthDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // remainingLengthDataGridViewTextBoxColumn
            // 
            remainingLengthDataGridViewTextBoxColumn.DataPropertyName = "RemainingLength";
            remainingLengthDataGridViewTextBoxColumn.HeaderText = "Remaining Length";
            remainingLengthDataGridViewTextBoxColumn.Name = "remainingLengthDataGridViewTextBoxColumn";
            remainingLengthDataGridViewTextBoxColumn.ReadOnly = true;
            remainingLengthDataGridViewTextBoxColumn.Width = 150;
            // 
            // utilizationDataGridViewTextBoxColumn
            // 
            utilizationDataGridViewTextBoxColumn.DataPropertyName = "Utilization";
            dataGridViewCellStyle1.Format = "P2";
            utilizationDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle1;
            utilizationDataGridViewTextBoxColumn.HeaderText = "Utilization";
            utilizationDataGridViewTextBoxColumn.Name = "utilizationDataGridViewTextBoxColumn";
            utilizationDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // binBindingSource
            // 
            binBindingSource.DataSource = typeof(Core.BinGroup);
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.FixedPanel = FixedPanel.Panel2;
            splitContainer1.Location = new Point(0, 24);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(dataGridView1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(splitContainer2);
            splitContainer1.Panel2.Controls.Add(label1);
            splitContainer1.Size = new Size(959, 723);
            splitContainer1.SplitterDistance = 469;
            splitContainer1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            splitContainer2.Dock = DockStyle.Fill;
            splitContainer2.Location = new Point(0, 34);
            splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            splitContainer2.Panel1.Controls.Add(dataGridView2);
            // 
            // splitContainer2.Panel2
            // 
            splitContainer2.Panel2.Controls.Add(binLayoutView1);
            splitContainer2.Size = new Size(959, 216);
            splitContainer2.SplitterDistance = 266;
            splitContainer2.TabIndex = 1;
            // 
            // dataGridView2
            // 
            dataGridView2.BackgroundColor = Color.White;
            dataGridView2.BorderStyle = BorderStyle.None;
            dataGridView2.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView2.ColumnHeadersHeight = 30;
            dataGridView2.Dock = DockStyle.Fill;
            dataGridView2.GridColor = Color.FromArgb(224, 224, 224);
            dataGridView2.Location = new Point(0, 0);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dataGridView2.Size = new Size(266, 216);
            dataGridView2.TabIndex = 1;
            // 
            // binLayoutView1
            // 
            binLayoutView1.BackColor = Color.White;
            binLayoutView1.Bin = null;
            binLayoutView1.BinBackgroundColor = Color.Pink;
            binLayoutView1.Dock = DockStyle.Fill;
            binLayoutView1.ItemBackgroundColor = Color.White;
            binLayoutView1.ItemBorderColor = Color.Blue;
            binLayoutView1.Location = new Point(0, 0);
            binLayoutView1.Name = "binLayoutView1";
            binLayoutView1.Size = new Size(689, 216);
            binLayoutView1.TabIndex = 1;
            binLayoutView1.Text = "class11";
            // 
            // label1
            // 
            label1.BackColor = Color.LightSlateGray;
            label1.Dock = DockStyle.Top;
            label1.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.White;
            label1.Location = new Point(0, 0);
            label1.Name = "label1";
            label1.Size = new Size(959, 34);
            label1.TabIndex = 2;
            label1.Text = "Items";
            label1.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // uIItemBindingSource
            // 
            uIItemBindingSource.DataSource = typeof(Models.PartInputItem);
            // 
            // menuStrip1
            // 
            menuStrip1.Items.AddRange(new ToolStripItem[] { saveToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(959, 24);
            menuStrip1.TabIndex = 5;
            menuStrip1.Text = "menuStrip1";
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.Size = new Size(43, 20);
            saveToolStripMenuItem.Text = "Save";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // ResultsForm
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(959, 747);
            Controls.Add(splitContainer1);
            Controls.Add(menuStrip1);
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            Name = "ResultsForm";
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Results";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)binBindingSource).EndInit();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            splitContainer2.Panel1.ResumeLayout(false);
            splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
            splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ((System.ComponentModel.ISupportInitialize)uIItemBindingSource).EndInit();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private Controls.BinLayoutView binLayoutView1;
        private System.Windows.Forms.BindingSource binBindingSource;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.BindingSource uIItemBindingSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn countDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn spacingDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn lengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn usedLengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn remainingLengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn utilizationDataGridViewTextBoxColumn;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.DataGridView dataGridView2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
    }
}