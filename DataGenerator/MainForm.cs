using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DataGenerator
{
	public partial class MainForm : Form
	{
		private Mapping setting;
		private Dictionary<string, DataTable> tempData = new Dictionary<string, DataTable>();
		private readonly static string connectionString = ConfigurationManager.ConnectionStrings["NULDEMOConnectionString"].ToString();
		private string[] commandLineArgs;

		public MainForm()
		{
			InitializeComponent();
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			// this.Initialize();
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
		{
			Exception ex = (Exception)args.ExceptionObject;
			MessageBox.Show(ex.ToString(), ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
			lblStatus.Text = "Unexpected error occurred.";
		}

		public MainForm(string[] args)
			: this()
		{
			this.commandLineArgs = args;
		}

		/// <summary>
		/// Bat dau
		/// </summary>
		private void Initialize(string query = "")
		{
			this.tempData.Clear();
			this.setting = new Mapping(query);

			if (this.setting.Parser.Errors.Count > 0)
			{
				this.lblStatus.Text = "Parse error.";
				MessageBox.Show(string.Join(
					Environment.NewLine,
					this.setting.Parser.Errors.Select(err => string.Format("Line {0}, Column {1}: {2}", err.Line, err.Column, err.Message))),
					"Parse error");

				return;
			}

			foreach (Table table in this.setting.Tables)
			{
				tableNamesComboBox.ValueMember = "Value";
				tableNamesComboBox.DisplayMember = "Text";
				tableNamesComboBox.DataSource = this.setting.Tables.Select(t => new { Text = string.Format("{0} ({1})", t.Name, t.Alias), Value = t.Alias }).ToList();
			}

			// Base tren bang dau tien
			if (!DataExists(this.setting.Tables[0], true))
			{
				// Truong hop khong tim thay data bang dau tien, tao data cho tat ca cac bang
				foreach (Table table in this.setting.Tables)
				{
					DataTable dummyData = CreateData(table);
					this.tempData[table.Alias] = dummyData;
				}
			}
			else
			{
				foreach (Table table in this.setting.Tables)
				{
					// Join thu, lay ra cac bang khong join duoc theo dieu kien da chi dinh
					List<Join> failedJoins = CheckJoinsForTable(table);
					if (failedJoins.Count > 0)
					{
						// Tao data cho cac bang khong join duoc
						DataSet dummyDataSet = CreateDummyData(failedJoins);
						foreach (DataTable dataTable in dummyDataSet.Tables)
						{
							this.tempData[dataTable.TableName] = dataTable;
						}
					}

					if (!this.tempData.ContainsKey(table.Alias) && DataExists(table, true))
					{
						DataTable existingData = GetData(table);
						this.tempData[table.Alias] = existingData;
					}
				}
			}

			// Set gia tri cac dieu kien join cho khop voi this.setting
			foreach (Join join in this.setting.Tables.SelectMany(tbl => tbl.Joins))
			{
				switch (join.Operator)
				{
					case Operators.Between:
					case Operators.Equal:
					case Operators.GreaterThanOrEqual:
					case Operators.In: 
					case Operators.LessThanOrEqual:
						this.tempData[join.Table2.Alias].Rows[0][join.Column2] = this.tempData[join.Table1.Alias].Rows[0][join.Column1];

						// Neu co dieu kien WHERE thi set theo dieu kien WHERE
						Condition condition = this.setting.Tables
							.SelectMany(tbl => tbl.Conditions)
							.FirstOrDefault(cond => cond.Table == join.Table2 && string.Compare(cond.Column, join.Column2, true) == 0);
						if (condition != null)
						{
							this.tempData[join.Table2.Alias].Rows[0][join.Column2] = condition.Value;
							this.tempData[join.Table1.Alias].Rows[0][join.Column1] = condition.Value;
						}
						break;
					// TODO: Other Operators
					default:
						break;
				}
			}

			previewGrid.DataSource = this.tempData[this.setting.Tables[0].Alias];
			tableNamesComboBox.SelectedValue = this.setting.Tables[0].Alias;
		}

		/// <summary>
		/// Check cac dieu kien join cua bang xem da ton tai data chua
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private List<Join> CheckJoinsForTable(Table table)
		{
			List<Join> failedJoins = new List<Join>();
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;

				StringBuilder queryBuilder = new StringBuilder();
				queryBuilder.AppendFormat(" SELECT TOP 1 1 AS {0},", table.Alias);
				queryBuilder.AppendLine();

				// Loop qua tung doi tuong join cua bang
				foreach (Table joinTargetTable in
					table.Joins.OrderBy(j => j.Table2.Alias).GroupBy(j => j.Table2).Select(g => g.First().Table2))
				{
					int joinNo = 1;

					// Doi voi moi join them 1 query check
					foreach (Join join in table.Joins.Where(j => j.Table2 == joinTargetTable).OrderBy(j => j.Column2))
					{
						queryBuilder.AppendFormat(@"
							(CASE WHEN EXISTS 
							( SELECT TOP 1 * FROM {0} {1}{2} WHERE ", joinTargetTable.Name, joinTargetTable.Alias, joinNo);
						queryBuilder.AppendLine();

						queryBuilder
							.AppendFormat(
								" {0}.{1} {2} {3}.{4} AND",
								join.Table1.Alias,
								join.Column1,
								Mapping.GetOperatorForQuery(join.Operator),
								join.Table2.Alias + joinNo,
								join.Column2)
							.AppendLine();

						// Them tat ca dieu kien where vao query check
						// Them dieu kien where cua bang hien tai
						AppendWhereConditions(join.Table1.Alias, cmd.Parameters, table.Conditions, queryBuilder);
						AppendWhereConditions(join.Table2.Alias + joinNo, cmd.Parameters, joinTargetTable.Conditions, queryBuilder);

						queryBuilder.Remove(queryBuilder.Length - 5, 3);

						queryBuilder.AppendFormat(@" ) THEN 1 ELSE 0 END) AS {0}{1},", joinTargetTable.Alias, joinNo);
						joinNo++;
					}

					queryBuilder.AppendLine();
				}

				// Removes trailing comma
				queryBuilder.Remove(queryBuilder.Length - 3, 1);
				queryBuilder.AppendFormat(" FROM {0} {1} ", table.Name, table.Alias);

				cmd.CommandText = queryBuilder.ToString();

				if (conn.State != ConnectionState.Open)
				{
					conn.Open();
				}

				DataTable result = new DataTable();
				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					result.Load(reader);
				}

				int colIdx = 1;
				foreach (Join join in table.Joins.OrderBy(j => j.Table2.Alias).ThenBy(j => j.Column2))
				{
					// Khi ton tai se tra ve 1, khong ton tai se tra ve 0
					// Add nhung gia tri khong phai 1 vao danh sach join that bai
					if ((int)result.Rows[0][colIdx++] != 1)
					{
						failedJoins.Add(join);
					}
				}
			}

			return failedJoins;
		}

		/// <summary>
		/// Lay ra du lieu cua bang
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private static DataTable GetData(Table table)
		{
			using (SqlConnection sqlConnection1 = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.Connection = sqlConnection1;

				StringBuilder queryBuilder = new StringBuilder();
				queryBuilder.AppendFormat("SELECT TOP 1 * FROM {0} WHERE 1=1 AND", table.Name).AppendLine();

				AppendWhereConditions(table.Name, cmd.Parameters, table.Conditions, queryBuilder);

				queryBuilder.Remove(queryBuilder.Length - 5, 3);
				cmd.CommandText = queryBuilder.ToString();

				sqlConnection1.Open();
				DataTable result = new DataTable();
				using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
				{
					result.Load(reader);
				}

				// Them data cho dieu kien IN
				CreateDataForInClause(table, result);
				return result;
			}
		}

		/// <summary>
		/// Check xem data cua bang da ton tai chua
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private static bool DataExists(Table table, bool conditionCheck)
		{
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;

				StringBuilder queryBuilder = new StringBuilder();
				queryBuilder.AppendFormat("SELECT COUNT(*) FROM {0} WHERE 1=1 AND", table.Name).AppendLine();

				if (conditionCheck)
				{
					// Check dieu kien where
					AppendWhereConditions(table.Name, cmd.Parameters, table.Conditions, queryBuilder);
				}

				queryBuilder.Remove(queryBuilder.Length - 5, 3);

				cmd.CommandText = queryBuilder.ToString();
				conn.Open();

				int count = (int)cmd.ExecuteScalar();
				if (count > 0)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Tao data dummy
		/// </summary>
		/// <param name="failedJoins"></param>
		/// <returns></returns>
		private static DataSet CreateDummyData(IEnumerable<Join> failedJoins)
		{
			DataSet result = null;
			if (failedJoins.Count() == 0)
			{
				return result;
			}

			Table table1 = failedJoins.First().Table1;
			if (failedJoins.Any(j => j.Table1 != table1))
			{
				return result;
			}

			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				result = new DataSet();

				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;

				foreach (Table table2 in
						table1.Joins.GroupBy(j => j.Table2).Select(g => g.First().Table2))
				{
					StringBuilder joinCondBuilder = new StringBuilder();
					joinCondBuilder.AppendFormat(" INNER JOIN {0} {1} ON", table2.Name, table2.Alias);
					joinCondBuilder.AppendLine();

					// Trong tat ca cac dieu kien join, check xem co dieu kien join thanh cong hay khong
					List<Join> successfulJoins = table1.Joins.Where(j => j.Table2 == table2 && !failedJoins.Contains(j)).OrderBy(j => j.Column2).ToList();
					if (successfulJoins.Count == 0)
					{
						// Neu doi voi tat ca cac dieu kien join deu khong co data, tao data random
						DataTable targetTable = new DataTable();
						targetTable = CreateData(table2);
						targetTable.TableName = table2.Alias;
						result.Tables.Add(targetTable);
						continue;
					}

					// Neu co ton tai data, base tren dieu kien join thanh cong de tao data
					foreach (Join join in successfulJoins)
					{
						joinCondBuilder.AppendFormat(
							" {0}.{1} {2} {3}.{4} AND",
							join.Table1.Alias,
							join.Column1,
							Mapping.GetOperatorForQuery(join.Operator),
							join.Table2.Alias,
							join.Column2);
					}

					joinCondBuilder.Remove(joinCondBuilder.Length - 3, 3);

					StringBuilder conditionsBuilder = new StringBuilder();
					conditionsBuilder.AppendLine(" WHERE 1=1 AND");

					AppendWhereConditions(table1.Alias, cmd.Parameters, table1.Conditions, conditionsBuilder);
					AppendWhereConditions(table2.Alias, cmd.Parameters, table1.Conditions, conditionsBuilder);

					string joinConditions = joinCondBuilder.ToString();
					string whereConditions = conditionsBuilder.ToString();

					StringBuilder mainQueryBuilder = new StringBuilder();
					// left
					mainQueryBuilder.AppendFormat(" SELECT TOP 1 {1}.* FROM {0} {1}", table1.Name, table1.Alias);
					mainQueryBuilder.AppendLine();
					mainQueryBuilder.AppendLine(joinConditions);
					mainQueryBuilder.AppendLine(whereConditions);

					// right
					mainQueryBuilder.AppendFormat(" SELECT TOP 1 {2}.* FROM {0} {1}", table1.Name, table1.Alias, table2.Alias);
					mainQueryBuilder.AppendLine();
					mainQueryBuilder.AppendLine(joinConditions);
					mainQueryBuilder.AppendLine(whereConditions);

					cmd.CommandText = mainQueryBuilder.ToString();

					conn.Open();

					using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
					{
						adapter.TableMappings.Add("Table", table1.Alias);
						adapter.TableMappings.Add("Table1", table2.Alias);
						adapter.Fill(result);
					}

					foreach (Join failedJoin in failedJoins.Where(j => j.Table1 == table1 && j.Table2 == table2))
					{
						result.Tables[table2.Alias].Rows[0][failedJoin.Column2] = result.Tables[table1.Alias].Rows[0][failedJoin.Column1];
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Tao data cho table, co gang su dung data co san trong database
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private static DataTable CreateData(Table table)
		{
			DataTable result = new DataTable();
			bool conditionsMatch = false;

			if (DataExists(table, true))
			{
				// Lay data phu hop voi dieu kien where
				conditionsMatch = true;
			}
			else if (DataExists(table, false))
			{
				// Lay data dua tren data co san cua bang -> skip qua xu ly nay
			}
			else
			{
				// Tao data dummy
				result = CreateDummyDataFromScratch(table);
				return result;
			}

			// Tan dung data co san trong DB
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;
				StringBuilder queryBuilder = new StringBuilder();
				queryBuilder.AppendFormat("SELECT TOP 1 * FROM {0} WHERE 1=1 AND", table.Name).AppendLine();

				if (conditionsMatch)
				{
					AppendWhereConditions(table.Name, cmd.Parameters, table.Conditions, queryBuilder);
				}

				queryBuilder.Remove(queryBuilder.Length - 5, 3);
				cmd.CommandText = queryBuilder.ToString();
				conn.Open();

				using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
				{
					result.Load(reader);
				}

				// Dua tren data lay tu DB, chi thay doi dieu kien where cho match voi query
				foreach (Condition cond in table.Conditions)
				{
					if (cond.Value is InValues)
					{
						// Dieu kien IN set sau
						continue;
					}

					foreach (DataRow resultRow in result.Rows)
					{
						resultRow[cond.Column] = cond.Value;
						// TODO: Operator
					}
				}

				// Loop lai 1 lan nua de set cho dieu kien IN
				CreateDataForInClause(table, result);
			}

			return result;
		}

		/// <summary>
		/// Tao data cho dieu kien IN
		/// </summary>
		/// <param name="table"></param>
		/// <param name="result"></param>
		private static void CreateDataForInClause(Table table, DataTable result)
		{
			foreach (Condition cond in table.Conditions)
			{
				if (!(cond.Value is InValues))
				{
					continue;
				}

				// TODO: IN Subquery
				List<object> values = ((IEnumerable)cond.Value).Cast<object>().ToList();
				if (values.Count == 0)
				{
					continue;
				}

				result.Rows[0][cond.Column] = values[0];
				// TODO: IN Variations
				foreach (var value in values.Skip(1))
				{
					// Them row ung voi moi gia tri cua IN clause
					DataRow newRow = result.NewRow();
					DataRow sourceRow = result.Rows[0];
					newRow.ItemArray = sourceRow.ItemArray.Clone() as object[];

					newRow[cond.Column] = value;

					// Thay doi PK cho khac nhau
					foreach (DataColumn key in result.PrimaryKey.Where(key =>
							!table.Conditions.Select(c => c.Column.ToLower()).Contains(key.ColumnName.ToLower())))
					{
						newRow[key] =
							Generator.GenerateDummyData(
								key.ColumnName,
								key.DataType,
								key.MaxLength,
								1,
								0);
					}

					result.Rows.Add(newRow);
				}
			}
		}

		/// <summary>
		/// Tao data dummy cho bang
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private static DataTable CreateDummyDataFromScratch(Table table)
		{
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				// Get schema
				cmd.CommandText = string.Format("SELECT * FROM {0}", table.Name);
				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;

				conn.Open();
				DataTable schemaTable;
				using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
				{
					schemaTable = reader.GetSchemaTable();
				}

				DataTable result = new DataTable();
				foreach (DataRow columnInfo in schemaTable.Rows)
				{
					string colName = columnInfo["ColumnName"] as string;

					DataColumn column = new DataColumn();
					column.ColumnName = colName;
					column.DataType = (Type)columnInfo["DataType"];

					result.Columns.Add(column);
				}

				DataRow dummyRow = result.NewRow();
				foreach (DataRow columnInfo in schemaTable.Rows)
				{
					string colName = columnInfo["ColumnName"] as string;
					dummyRow[colName] =
						Generator.GenerateDummyData(
							colName,
							(Type)columnInfo["DataType"],
							(int)columnInfo["ColumnSize"],
							(short)columnInfo["NumericPrecision"],
							(short)columnInfo["NumericScale"]);
				}

				foreach (Condition cond in table.Conditions)
				{
					// Equals (=)
					dummyRow[cond.Column] = cond.Value;

					// TODO: Operator
				}

				result.Rows.Add(dummyRow);

				return result;
			}
		}

		private static int paramIndex = 0;

		/// <summary>
		/// Them where vao cau lenh
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="paramCollection"></param>
		/// <param name="conditions"></param>
		/// <param name="builder"></param>
		private static void AppendWhereConditions(
			string tableName,
			SqlParameterCollection paramCollection,
			List<Condition> conditions,
			StringBuilder builder)
		{
			foreach (Condition condition in conditions)
			{
				if (condition.Operator == Operators.In)
				{
					IEnumerable values = (IEnumerable)condition.Value;
					if (values.Cast<object>().Count() == 0)
					{
						continue;
					}

					int inParamIndex = paramIndex;
					builder
						.AppendFormat(
							" {0}.{1} {2} ({3}) AND",
							tableName,
							condition.Column,
							Mapping.GetOperatorForQuery(condition.Operator),
							string.Join(",", values.Cast<object>().Select(x => "@param" + inParamIndex++)))
						.AppendLine();

					inParamIndex = 0;
					foreach (var value in (IEnumerable)condition.Value)
					{
						paramCollection.AddWithValue("@param" + paramIndex, value);

						// paramIndex++
						Interlocked.Increment(ref paramIndex);
					}
				}
				else if (
					condition.Operator == Operators.Between ||
					condition.Operator == Operators.NotBetween)
				{
					builder
						.AppendFormat(
							" {0}.{1} {2} @param{3} AND @param{4} AND",
							tableName,
							condition.Column,
							Mapping.GetOperatorForQuery(condition.Operator),
							paramIndex,
							paramIndex + 1)
						.AppendLine();
					paramCollection.AddWithValue("@param" + paramIndex, condition.Value);
					paramCollection.AddWithValue("@param" + (paramIndex + 1), condition.Value);

					// paramIndex++
					Interlocked.Increment(ref paramIndex);
					Interlocked.Increment(ref paramIndex);
				}
				else
				{
					builder
						.AppendFormat(
							" {0}.{1} {2} @param{3} AND",
							tableName,
							condition.Column,
							Mapping.GetOperatorForQuery(condition.Operator),
							paramIndex)
						.AppendLine();
					paramCollection.AddWithValue("@param" + paramIndex, condition.Value);

					// paramIndex++
					Interlocked.Increment(ref paramIndex);
				}
			}
		}

		private void tableNamesComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			if (this.tempData == null || this.tempData.Count == 0)
			{
				return;
			}

			DataTable dataTable;
			if (this.tempData.TryGetValue(tableNamesComboBox.SelectedValue.ToString(), out dataTable))
			{
				previewGrid.DataSource = dataTable;
			}
		}

		private void btnExcel_Click(object sender, EventArgs e)
		{
		}

		private void btnPrevTable_Click(object sender, EventArgs e)
		{
			if (this.tempData == null || this.tempData.Count == 0)
			{
				return;
			}

			int index = tableNamesComboBox.SelectedIndex;
			int prevIndex = index - 1;
			if (prevIndex == -1)
			{
				prevIndex = tableNamesComboBox.Items.Count - 1;
			}

			tableNamesComboBox.SelectedIndex = prevIndex;
			previewGrid.DataSource = this.tempData[this.setting.Tables[prevIndex].Alias];
		}

		private void btnNextTable_Click(object sender, EventArgs e)
		{
			if (this.tempData == null || this.tempData.Count == 0)
			{
				return;
			}

			int index = tableNamesComboBox.SelectedIndex;
			int nextIndex = index + 1;
			if (nextIndex == tableNamesComboBox.Items.Count)
			{
				nextIndex = 0;
			}

			tableNamesComboBox.SelectedIndex = nextIndex;
			previewGrid.DataSource = this.tempData[this.setting.Tables[nextIndex].Alias];
		}

		private void btnReload_Click(object sender, EventArgs e)
		{
			this.Initialize();
			lblStatus.Text = string.Empty;
		}

		private void btnParse_Click(object sender, EventArgs e)
		{
			lblStatus.Text = string.Empty;
			if (string.IsNullOrEmpty(txtQuery.Text))
			{
				lblStatus.Text = "Query is empty.";
				return;
			}

			this.Initialize(txtQuery.Text);

			if (tempData.Count > 0)
			{
				//btnExcel.Enabled = true;
				//btnInsert.Enabled = true;
			}
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void formatQueryToolStripMenuItem_Click(object sender, EventArgs e)
		{
			PoorMansTSqlFormatterLib.SqlFormattingManager formatManager = new PoorMansTSqlFormatterLib.SqlFormattingManager();
			txtQuery.Text = formatManager.Format(txtQuery.Text);
		}

		private void btnClear_Click(object sender, EventArgs e)
		{
			this.tempData = new Dictionary<string, DataTable>();
			this.tableNamesComboBox.DataSource = null;
			this.tableNamesComboBox.ResetText();
			this.tableNamesComboBox.SelectedIndex = -1;
			this.previewGrid.DataSource = null;
		}
	}
}
