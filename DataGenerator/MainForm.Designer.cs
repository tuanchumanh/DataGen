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
			this.startButton = new System.Windows.Forms.Button();
			this.previewGrid = new System.Windows.Forms.DataGridView();
			this.tableNamesComboBox = new System.Windows.Forms.ComboBox();
			((System.ComponentModel.ISupportInitialize)(this.previewGrid)).BeginInit();
			this.SuspendLayout();
			// 
			// startButton
			// 
			this.startButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.startButton.Location = new System.Drawing.Point(710, 418);
			this.startButton.Name = "startButton";
			this.startButton.Size = new System.Drawing.Size(75, 23);
			this.startButton.TabIndex = 0;
			this.startButton.Text = "Start";
			this.startButton.UseVisualStyleBackColor = true;
			this.startButton.Click += new System.EventHandler(this.StartButton_Click);
			// 
			// previewGrid
			// 
			this.previewGrid.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
			this.previewGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.previewGrid.Location = new System.Drawing.Point(12, 41);
			this.previewGrid.Name = "previewGrid";
			this.previewGrid.RowTemplate.Height = 21;
			this.previewGrid.Size = new System.Drawing.Size(773, 359);
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
			// MainForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(797, 453);
			this.Controls.Add(this.tableNamesComboBox);
			this.Controls.Add(this.previewGrid);
			this.Controls.Add(this.startButton);
			this.Name = "MainForm";
			this.Text = "Untitled";
			((System.ComponentModel.ISupportInitialize)(this.previewGrid)).EndInit();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Button startButton;
		private System.Windows.Forms.DataGridView previewGrid;
		private System.Windows.Forms.ComboBox tableNamesComboBox;

	}
}

