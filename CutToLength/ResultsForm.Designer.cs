namespace CutToLength
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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.binBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.class11 = new CutToLength.BinLayoutView();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.uIItemBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.spacingDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lengthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.usedLengthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.remainingLengthDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.utilizationDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.binBindingSource)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.uIItemBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // dataGridView1
            // 
            this.dataGridView1.AutoGenerateColumns = false;
            this.dataGridView1.BackgroundColor = System.Drawing.Color.White;
            this.dataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.dataGridView1.ColumnHeadersHeight = 30;
            this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.spacingDataGridViewTextBoxColumn,
            this.lengthDataGridViewTextBoxColumn,
            this.usedLengthDataGridViewTextBoxColumn,
            this.remainingLengthDataGridViewTextBoxColumn,
            this.utilizationDataGridViewTextBoxColumn});
            this.dataGridView1.DataSource = this.binBindingSource;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.GridColor = System.Drawing.Color.FromArgb(((int)(((byte)(224)))), ((int)(((byte)(224)))), ((int)(((byte)(224)))));
            this.dataGridView1.Location = new System.Drawing.Point(0, 0);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.dataGridView1.RowTemplate.Height = 25;
            this.dataGridView1.Size = new System.Drawing.Size(892, 256);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.RowEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_RowEnter);
            this.dataGridView1.SelectionChanged += new System.EventHandler(this.dataGridView1_SelectionChanged);
            // 
            // binBindingSource
            // 
            this.binBindingSource.DataSource = typeof(CutToLength.Bin);
            // 
            // class11
            // 
            this.class11.BackColor = System.Drawing.Color.White;
            this.class11.Bin = null;
            this.class11.Dock = System.Windows.Forms.DockStyle.Fill;
            this.class11.Location = new System.Drawing.Point(0, 0);
            this.class11.Name = "class11";
            this.class11.Size = new System.Drawing.Size(892, 181);
            this.class11.TabIndex = 1;
            this.class11.Text = "class11";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.dataGridView1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.class11);
            this.splitContainer1.Size = new System.Drawing.Size(892, 441);
            this.splitContainer1.SplitterDistance = 256;
            this.splitContainer1.TabIndex = 2;
            // 
            // uIItemBindingSource
            // 
            this.uIItemBindingSource.DataSource = typeof(CutToLength.UIItem);
            // 
            // spacingDataGridViewTextBoxColumn
            // 
            this.spacingDataGridViewTextBoxColumn.DataPropertyName = "Spacing";
            this.spacingDataGridViewTextBoxColumn.HeaderText = "Spacing";
            this.spacingDataGridViewTextBoxColumn.Name = "spacingDataGridViewTextBoxColumn";
            // 
            // lengthDataGridViewTextBoxColumn
            // 
            this.lengthDataGridViewTextBoxColumn.DataPropertyName = "Length";
            this.lengthDataGridViewTextBoxColumn.HeaderText = "Length";
            this.lengthDataGridViewTextBoxColumn.Name = "lengthDataGridViewTextBoxColumn";
            // 
            // usedLengthDataGridViewTextBoxColumn
            // 
            this.usedLengthDataGridViewTextBoxColumn.DataPropertyName = "UsedLength";
            this.usedLengthDataGridViewTextBoxColumn.HeaderText = "UsedLength";
            this.usedLengthDataGridViewTextBoxColumn.Name = "usedLengthDataGridViewTextBoxColumn";
            this.usedLengthDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // remainingLengthDataGridViewTextBoxColumn
            // 
            this.remainingLengthDataGridViewTextBoxColumn.DataPropertyName = "RemainingLength";
            this.remainingLengthDataGridViewTextBoxColumn.HeaderText = "RemainingLength";
            this.remainingLengthDataGridViewTextBoxColumn.Name = "remainingLengthDataGridViewTextBoxColumn";
            this.remainingLengthDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // utilizationDataGridViewTextBoxColumn
            // 
            this.utilizationDataGridViewTextBoxColumn.DataPropertyName = "Utilization";
            dataGridViewCellStyle1.Format = "P2";
            this.utilizationDataGridViewTextBoxColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.utilizationDataGridViewTextBoxColumn.HeaderText = "Utilization";
            this.utilizationDataGridViewTextBoxColumn.Name = "utilizationDataGridViewTextBoxColumn";
            this.utilizationDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // ResultsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(892, 441);
            this.Controls.Add(this.splitContainer1);
            this.Name = "ResultsForm";
            this.Text = "Form2";
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.binBindingSource)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.uIItemBindingSource)).EndInit();
            this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.DataGridView dataGridView1;
		private BinLayoutView class11;
		private System.Windows.Forms.BindingSource binBindingSource;
		private System.Windows.Forms.SplitContainer splitContainer1;
		private System.Windows.Forms.BindingSource uIItemBindingSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn spacingDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn lengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn usedLengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn remainingLengthDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn utilizationDataGridViewTextBoxColumn;
    }
}