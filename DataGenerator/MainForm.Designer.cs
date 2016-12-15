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
			this.previewGrid = new System.Windows.Forms.DataGridView();
			this.tableNamesComboBox = new System.Windows.Forms.ComboBox();
			this.btnExcel = new System.Windows.Forms.Button();
			this.btnInsert = new System.Windows.Forms.Button();
			this.btnPrevTable = new System.Windows.Forms.Button();
			this.btnNextTable = new System.Windows.Forms.Button();
			this.btnReload = new System.Windows.Forms.Button();
			this.txtQuery = new System.Windows.Forms.TextBox();
			this.btnParse = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)(this.previewGrid)).BeginInit();
			this.SuspendLayout();
			// 
			// previewGrid
			// 
			this.previewGrid.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
			this.previewGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.previewGrid.Location = new System.Drawing.Point(12, 43);
			this.previewGrid.Name = "previewGrid";
			this.previewGrid.RowTemplate.Height = 21;
			this.previewGrid.Size = new System.Drawing.Size(773, 98);
			this.previewGrid.TabIndex = 1;
			// 
			// tableNamesComboBox
			// 
			this.tableNamesComboBox.DisplayMember = "Text";
			this.tableNamesComboBox.FormattingEnabled = true;
			this.tableNamesComboBox.Location = new System.Drawing.Point(13, 13);
			this.tableNamesComboBox.Name = "tableNamesComboBox";
			this.tableNamesComboBox.Size = new System.Drawing.Size(133, 20);
			this.tableNamesComboBox.TabIndex = 2;
			this.tableNamesComboBox.ValueMember = "Value";
			this.tableNamesComboBox.SelectedIndexChanged += new System.EventHandler(this.tableNamesComboBox_SelectedIndexChanged);
			// 
			// btnExcel
			// 
			this.btnExcel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.btnExcel.Location = new System.Drawing.Point(629, 395);
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
			this.btnInsert.Location = new System.Drawing.Point(710, 395);
			this.btnInsert.Name = "btnInsert";
			this.btnInsert.Size = new System.Drawing.Size(75, 23);
			this.btnInsert.TabIndex = 0;
			this.btnInsert.Text = "Insert DB";
			this.btnInsert.UseVisualStyleBackColor = true;
			// 
			// btnPrevTable
			// 
			this.btnPrevTable.Location = new System.Drawing.Point(153, 9);
			this.btnPrevTable.Name = "btnPrevTable";
			this.btnPrevTable.Size = new System.Drawing.Size(21, 23);
			this.btnPrevTable.TabIndex = 3;
			this.btnPrevTable.Text = "<";
			this.btnPrevTable.UseVisualStyleBackColor = true;
			this.btnPrevTable.Click += new System.EventHandler(this.btnPrevTable_Click);
			// 
			// btnNextTable
			// 
			this.btnNextTable.Location = new System.Drawing.Point(180, 9);
			this.btnNextTable.Name = "btnNextTable";
			this.btnNextTable.Size = new System.Drawing.Size(21, 23);
			this.btnNextTable.TabIndex = 3;
			this.btnNextTable.Text = ">";
			this.btnNextTable.UseVisualStyleBackColor = true;
			this.btnNextTable.Click += new System.EventHandler(this.btnNextTable_Click);
			// 
			// btnReload
			// 
			this.btnReload.Location = new System.Drawing.Point(709, 13);
			this.btnReload.Name = "btnReload";
			this.btnReload.Size = new System.Drawing.Size(75, 23);
			this.btnReload.TabIndex = 4;
			this.btnReload.Text = "Reload";
			this.btnReload.UseVisualStyleBackColor = true;
			this.btnReload.Click += new System.EventHandler(this.btnReload_Click);
			// 
			// txtQuery
			// 
			this.txtQuery.AcceptsReturn = true;
			this.txtQuery.AcceptsTab = true;
			this.txtQuery.AllowDrop = true;
			this.txtQuery.Location = new System.Drawing.Point(12, 148);
			this.txtQuery.Multiline = true;
			this.txtQuery.Name = "txtQuery";
			this.txtQuery.Size = new System.Drawing.Size(772, 241);
			this.txtQuery.TabIndex = 5;
			// 
			// btnParse
			// 
			this.btnParse.Location = new System.Drawing.Point(548, 395);
			this.btnParse.Name = "btnParse";
			this.btnParse.Size = new System.Drawing.Size(75, 23);
			this.btnParse.TabIndex = 6;
			this.btnParse.Text = "Parse";
			this.btnParse.UseVisualStyleBackColor = true;
			this.btnParse.Click += new System.EventHandler(this.btnParse_Click);
			// 
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(797, 430);
			this.Controls.Add(this.btnParse);
			this.Controls.Add(this.txtQuery);
			this.Controls.Add(this.btnReload);
			this.Controls.Add(this.btnNextTable);
			this.Controls.Add(this.btnPrevTable);
			this.Controls.Add(this.tableNamesComboBox);
			this.Controls.Add(this.previewGrid);
			this.Controls.Add(this.btnInsert);
			this.Controls.Add(this.btnExcel);
			this.Name = "MainForm";
			this.Text = "Test Data Utility";
			((System.ComponentModel.ISupportInitialize)(this.previewGrid)).EndInit();
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
		private System.Windows.Forms.Button btnReload;
		private System.Windows.Forms.TextBox txtQuery;
		private System.Windows.Forms.Button btnParse;

	}
}

