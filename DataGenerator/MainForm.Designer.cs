namespace DataGenerator
{
	partial class MainForm
	{
		/// <summary>
		/// 必要なデザイナー変数です。
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// 使用中のリソースをすべてクリーンアップします。
		/// </summary>
		/// <param name="disposing">マネージ リソースが破棄される場合 true、破棄されない場合は false です。</param>
		protected override void Dispose( bool disposing )
		{
			if ( disposing && ( components != null ) )
			{
				components.Dispose ();
			}
			base.Dispose ( disposing );
		}

		#region Windows フォーム デザイナーで生成されたコード

		/// <summary>
		/// デザイナー サポートに必要なメソッドです。このメソッドの内容を
		/// コード エディターで変更しないでください。
		/// </summary>
		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
			this.previewGrid = new System.Windows.Forms.DataGridView();
			this.tableNamesComboBox = new System.Windows.Forms.ComboBox();
			this.btnExcel = new System.Windows.Forms.Button();
			this.btnInsert = new System.Windows.Forms.Button();
			this.btnPrevTable = new System.Windows.Forms.Button();
			this.btnNextTable = new System.Windows.Forms.Button();
			this.btnParse = new System.Windows.Forms.Button();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.utilitiesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.formatQueryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.txtQuery = new System.Windows.Forms.RichTextBox();
			this.btnClear = new System.Windows.Forms.Button();
			this.btnCancel = new System.Windows.Forms.Button();
			this.chkHeaderCopy = new System.Windows.Forms.CheckBox();
			this.btnCopy = new System.Windows.Forms.Button();
			this.statusStrip1 = new System.Windows.Forms.StatusStrip();
			this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
			((System.ComponentModel.ISupportInitialize)(this.previewGrid)).BeginInit();
			this.menuStrip1.SuspendLayout();
			this.statusStrip1.SuspendLayout();
			this.SuspendLayout();
			// 
			// previewGrid
			// 
			this.previewGrid.AllowUserToAddRows = false;
			this.previewGrid.AllowUserToDeleteRows = false;
			this.previewGrid.AllowUserToResizeRows = false;
			this.previewGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.previewGrid.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
			this.previewGrid.Location = new System.Drawing.Point(11, 58);
			this.previewGrid.Name = "previewGrid";
			this.previewGrid.RowHeadersVisible = false;
			this.previewGrid.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.AutoSizeToAllHeaders;
			this.previewGrid.RowTemplate.Height = 21;
			this.previewGrid.Size = new System.Drawing.Size(912, 152);
			this.previewGrid.TabIndex = 1;
			// 
			// tableNamesComboBox
			// 
			this.tableNamesComboBox.DisplayMember = "Text";
			this.tableNamesComboBox.FormattingEnabled = true;
			this.tableNamesComboBox.Location = new System.Drawing.Point(11, 32);
			this.tableNamesComboBox.Name = "tableNamesComboBox";
			this.tableNamesComboBox.Size = new System.Drawing.Size(348, 20);
			this.tableNamesComboBox.TabIndex = 2;
			this.tableNamesComboBox.ValueMember = "Value";
			this.tableNamesComboBox.SelectedIndexChanged += new System.EventHandler(this.tableNamesComboBox_SelectedIndexChanged);
			// 
			// btnExcel
			// 
			this.btnExcel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnExcel.Enabled = false;
			this.btnExcel.Location = new System.Drawing.Point(766, 518);
			this.btnExcel.Name = "btnExcel";
			this.btnExcel.Size = new System.Drawing.Size(75, 23);
			this.btnExcel.TabIndex = 0;
			this.btnExcel.Text = "Excel";
			this.btnExcel.UseVisualStyleBackColor = true;
			this.btnExcel.Click += new System.EventHandler(this.btnExcel_Click);
			// 
			// btnInsert
			// 
			this.btnInsert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnInsert.Enabled = false;
			this.btnInsert.Location = new System.Drawing.Point(847, 518);
			this.btnInsert.Name = "btnInsert";
			this.btnInsert.Size = new System.Drawing.Size(75, 23);
			this.btnInsert.TabIndex = 0;
			this.btnInsert.Text = "Insert DB";
			this.btnInsert.UseVisualStyleBackColor = true;
			// 
			// btnPrevTable
			// 
			this.btnPrevTable.Location = new System.Drawing.Point(365, 30);
			this.btnPrevTable.Name = "btnPrevTable";
			this.btnPrevTable.Size = new System.Drawing.Size(21, 23);
			this.btnPrevTable.TabIndex = 3;
			this.btnPrevTable.Text = "<";
			this.btnPrevTable.UseVisualStyleBackColor = true;
			this.btnPrevTable.Click += new System.EventHandler(this.btnPrevTable_Click);
			// 
			// btnNextTable
			// 
			this.btnNextTable.Location = new System.Drawing.Point(392, 30);
			this.btnNextTable.Name = "btnNextTable";
			this.btnNextTable.Size = new System.Drawing.Size(21, 23);
			this.btnNextTable.TabIndex = 3;
			this.btnNextTable.Text = ">";
			this.btnNextTable.UseVisualStyleBackColor = true;
			this.btnNextTable.Click += new System.EventHandler(this.btnNextTable_Click);
			// 
			// btnParse
			// 
			this.btnParse.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnParse.Location = new System.Drawing.Point(682, 518);
			this.btnParse.Name = "btnParse";
			this.btnParse.Size = new System.Drawing.Size(78, 23);
			this.btnParse.TabIndex = 6;
			this.btnParse.Text = "Parse Query";
			this.btnParse.UseVisualStyleBackColor = true;
			this.btnParse.Click += new System.EventHandler(this.btnParse_Click);
			// 
			// menuStrip1
			// 
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.utilitiesToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.RenderMode = System.Windows.Forms.ToolStripRenderMode.System;
			this.menuStrip1.Size = new System.Drawing.Size(936, 24);
			this.menuStrip1.TabIndex = 8;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// fileToolStripMenuItem
			// 
			this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
			this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
			this.fileToolStripMenuItem.Size = new System.Drawing.Size(41, 20);
			this.fileToolStripMenuItem.Text = "Main";
			// 
			// exitToolStripMenuItem
			// 
			this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
			this.exitToolStripMenuItem.Size = new System.Drawing.Size(90, 22);
			this.exitToolStripMenuItem.Text = "Exit";
			this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
			// 
			// utilitiesToolStripMenuItem
			// 
			this.utilitiesToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.formatQueryToolStripMenuItem});
			this.utilitiesToolStripMenuItem.Name = "utilitiesToolStripMenuItem";
			this.utilitiesToolStripMenuItem.Size = new System.Drawing.Size(57, 20);
			this.utilitiesToolStripMenuItem.Text = "Utilities";
			// 
			// formatQueryToolStripMenuItem
			// 
			this.formatQueryToolStripMenuItem.Name = "formatQueryToolStripMenuItem";
			this.formatQueryToolStripMenuItem.Size = new System.Drawing.Size(138, 22);
			this.formatQueryToolStripMenuItem.Text = "Format query";
			this.formatQueryToolStripMenuItem.Click += new System.EventHandler(this.formatQueryToolStripMenuItem_Click);
			// 
			// txtQuery
			// 
			this.txtQuery.AcceptsTab = true;
			this.txtQuery.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.txtQuery.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.txtQuery.Location = new System.Drawing.Point(11, 217);
			this.txtQuery.Name = "txtQuery";
			this.txtQuery.Size = new System.Drawing.Size(912, 295);
			this.txtQuery.TabIndex = 9;
			this.txtQuery.Text = "";
			this.txtQuery.WordWrap = false;
			// 
			// btnClear
			// 
			this.btnClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.btnClear.Location = new System.Drawing.Point(847, 28);
			this.btnClear.Name = "btnClear";
			this.btnClear.Size = new System.Drawing.Size(75, 23);
			this.btnClear.TabIndex = 10;
			this.btnClear.Text = "Clear";
			this.btnClear.UseVisualStyleBackColor = true;
			this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
			// 
			// btnCancel
			// 
			this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnCancel.Location = new System.Drawing.Point(229, 518);
			this.btnCancel.Name = "btnCancel";
			this.btnCancel.Size = new System.Drawing.Size(75, 23);
			this.btnCancel.TabIndex = 11;
			this.btnCancel.Text = "Cancel";
			this.btnCancel.UseVisualStyleBackColor = true;
			this.btnCancel.Visible = false;
			this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
			// 
			// chkHeaderCopy
			// 
			this.chkHeaderCopy.AutoSize = true;
			this.chkHeaderCopy.Checked = true;
			this.chkHeaderCopy.CheckState = System.Windows.Forms.CheckState.Checked;
			this.chkHeaderCopy.Location = new System.Drawing.Point(670, 32);
			this.chkHeaderCopy.Name = "chkHeaderCopy";
			this.chkHeaderCopy.Size = new System.Drawing.Size(90, 16);
			this.chkHeaderCopy.TabIndex = 12;
			this.chkHeaderCopy.Text = "Header Copy";
			this.chkHeaderCopy.UseVisualStyleBackColor = true;
			this.chkHeaderCopy.CheckedChanged += new System.EventHandler(this.chkHeaderCopy_CheckedChanged);
			// 
			// btnCopy
			// 
			this.btnCopy.Location = new System.Drawing.Point(766, 28);
			this.btnCopy.Name = "btnCopy";
			this.btnCopy.Size = new System.Drawing.Size(75, 23);
			this.btnCopy.TabIndex = 13;
			this.btnCopy.Text = "Copy";
			this.btnCopy.UseVisualStyleBackColor = true;
			this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
			// 
			// statusStrip1
			// 
			this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
			this.statusStrip1.Location = new System.Drawing.Point(0, 551);
			this.statusStrip1.Name = "statusStrip1";
			this.statusStrip1.Size = new System.Drawing.Size(936, 22);
			this.statusStrip1.TabIndex = 14;
			this.statusStrip1.Text = "statusStrip1";
			// 
			// lblStatus
			// 
			this.lblStatus.Name = "lblStatus";
			this.lblStatus.Size = new System.Drawing.Size(0, 17);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(936, 573);
			this.Controls.Add(this.statusStrip1);
			this.Controls.Add(this.btnCopy);
			this.Controls.Add(this.chkHeaderCopy);
			this.Controls.Add(this.btnCancel);
			this.Controls.Add(this.btnClear);
			this.Controls.Add(this.txtQuery);
			this.Controls.Add(this.btnParse);
			this.Controls.Add(this.btnNextTable);
			this.Controls.Add(this.btnPrevTable);
			this.Controls.Add(this.tableNamesComboBox);
			this.Controls.Add(this.previewGrid);
			this.Controls.Add(this.btnInsert);
			this.Controls.Add(this.btnExcel);
			this.Controls.Add(this.menuStrip1);
			this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "MainForm";
			this.Text = "Test Data Generator";
			((System.ComponentModel.ISupportInitialize)(this.previewGrid)).EndInit();
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.statusStrip1.ResumeLayout(false);
			this.statusStrip1.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.DataGridView previewGrid;
		private System.Windows.Forms.ComboBox tableNamesComboBox;
		private System.Windows.Forms.Button btnExcel;
		private System.Windows.Forms.Button btnInsert;
		private System.Windows.Forms.Button btnPrevTable;
		private System.Windows.Forms.Button btnNextTable;
		private System.Windows.Forms.Button btnParse;
		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
		private System.Windows.Forms.RichTextBox txtQuery;
		private System.Windows.Forms.ToolStripMenuItem utilitiesToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem formatQueryToolStripMenuItem;
		private System.Windows.Forms.Button btnClear;
		private System.Windows.Forms.Button btnCancel;
		private System.Windows.Forms.CheckBox chkHeaderCopy;
		private System.Windows.Forms.Button btnCopy;
		private System.Windows.Forms.StatusStrip statusStrip1;
		private System.Windows.Forms.ToolStripStatusLabel lblStatus;

	}
}

