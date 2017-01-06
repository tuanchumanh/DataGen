using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
		{
			Exception ex = (Exception)args.ExceptionObject;
			MessageBox.Show(ex.ToString(), ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
			lblStatus.Text = "Unexpected error occurred.";
			btnParse.Enabled = true;
			btnCancel_Click(sender, args);
		}

		public MainForm(string[] args)
			: this()
		{
			this.commandLineArgs = args;
		}

		/// <summary>
		/// Bat dau
		/// </summary>
		private bool Initialize(string query = "")
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

				return false;
			}

			// Tao data
			foreach (TableInfo table in this.setting.TablesInfo)
			{
				if (DataExists(table, true))
				{
					// Truong hop co data
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

					if (!this.tempData.ContainsKey(table.Alias))
					{
						DataTable existingData = GetData(table);
						this.tempData[table.Alias] = existingData;
					}
				}
				else if (!this.tempData.ContainsKey(table.Alias))
				{
					// Neu khong tim thay data o DB hoac data dummy chua duoc tao, tao o day
					this.tempData[table.Alias] = CreateData(table);
				}
			}

			foreach (string alias in this.tempData.Keys)
			{
				DataTable dataTable = this.tempData[alias];
				dataTable.TableName = this.setting.TablesInfo.FirstOrDefault(tbl => tbl.Alias == alias).Name;
			}

			// Set gia tri cac dieu kien join cho khop voi this.setting
			SetWhereConditionValues(this.setting.TablesInfo, this.tempData);
			return true;
		}

		/// <summary>
		/// Check cac dieu kien join cua bang xem da ton tai data chua
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private static List<Join> CheckJoinsForTable(TableInfo table)
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
				foreach (TableInfo joinTargetTable in
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
		private static DataTable GetData(TableInfo table)
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
				result.TableName = table.Name;
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
		/// <param name="conditionCheck">Co check ca dieu kien where hay khong</param>
		/// <returns></returns>
		private static bool DataExists(TableInfo table, bool conditionCheck)
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
		/// Tao data dummy dua theo dieu kien join bi fail
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

			TableInfo table1 = failedJoins.First().Table1;
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

				foreach (TableInfo table2 in
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

					conditionsBuilder.Remove(conditionsBuilder.Length - 5, 3);

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
						// TODO: Operator!!
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
		private static DataTable CreateData(TableInfo table)
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
				DataTable schemaTable = GetSchemaTable(table);
				foreach (DataRow resultRow in result.Rows)
				{
					SetWhereConditionsForDataRow(table, schemaTable, resultRow);
				}

				// Loop lai 1 lan nua de set cho dieu kien IN
				CreateDataForInClause(table, result);
			}

			result.TableName = table.Name;
			return result;
		}

		/// <summary>
		/// Tao data cho dieu kien IN
		/// </summary>
		/// <param name="table"></param>
		/// <param name="result"></param>
		private static void CreateDataForInClause(TableInfo table, DataTable result)
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
		private static DataTable CreateDummyDataFromScratch(TableInfo table)
		{
			// Get schema
			DataTable schemaTable = GetSchemaTable(table);

			DataTable result = new DataTable();
			result.TableName = table.Name;
			foreach (DataRow columnInfo in schemaTable.Rows)
			{
				string colName = columnInfo["ColumnName"] as string;

				DataColumn column = new DataColumn();
				column.ColumnName = colName;
				column.DataType = (Type)columnInfo["DataType"];

				result.Columns.Add(column);
			}

			DataRow dataRow = result.NewRow();
			foreach (DataRow columnInfo in schemaTable.Rows)
			{
				string colName = columnInfo["ColumnName"] as string;
				dataRow[colName] =
					Generator.GenerateDummyData(
						colName,
						(Type)columnInfo["DataType"],
						(int)columnInfo["ColumnSize"],
						(short)columnInfo["NumericPrecision"],
						(short)columnInfo["NumericScale"]);
			}

			SetWhereConditionsForDataRow(table, schemaTable, dataRow);
			result.Rows.Add(dataRow);

			// Loop lai 1 lan nua de set cho dieu kien IN
			CreateDataForInClause(table, result);

			return result;
		}

		/// <summary>
		/// Lay thong tin ve column, datatype cua bang
		/// </summary>
		/// <param name="table"></param>
		/// <returns></returns>
		private static DataTable GetSchemaTable(TableInfo table)
		{
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				// Get schema
				cmd.CommandText = string.Format("SELECT * FROM {0}", table.Name);
				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;

				conn.Open();
				using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo))
				{
					DataTable schemaTable = reader.GetSchemaTable();
					return schemaTable;
				}
			}
		}

		private static void SetWhereConditionsForDataRow(TableInfo table, DataTable schemaTable, DataRow dataRow)
		{
			if (schemaTable == null)
			{
				schemaTable = GetSchemaTable(table);
			}

			foreach (Condition condition in table.Conditions.OrderBy(cond => cond.Operator))
			{
				switch (condition.Operator)
				{
					case Operators.Between:
					case Operators.Equal:
					case Operators.GreaterThanOrEqual:
					case Operators.LessThanOrEqual:
						dataRow[condition.Column] = condition.Value;
						break;
					case Operators.NotEqual:
						if (!condition.Value.Equals(dataRow[condition.Column]))
						{
							break;
						}

						// Truong hop !=, generate dummy
						DataRow columnInfo = schemaTable.Rows.Cast<DataRow>().FirstOrDefault(colInfo => condition.Column.Equals(colInfo["ColumnName"] as string));
						object dummyData = condition.Value;
						do
						{
							 dummyData = Generator.GenerateDummyData(
											condition.Column,
											(Type)columnInfo["DataType"],
											(int)columnInfo["ColumnSize"],
											(short)columnInfo["NumericPrecision"],
											(short)columnInfo["NumericScale"]);
						}
						while (dummyData.Equals(condition.Value));

						dataRow[condition.Column] = dummyData;

						// TODO: Co join thi sao?

						break;
					case Operators.In:
						// Bo qua vi o tren set roi
						break;
					// TODO: Other Operators
					case Operators.GreaterThan:
					case Operators.LessThan:
						// object data = GetDataForInequalityOperator(condition.Operator, condition.Value, null);
						break;
					case Operators.NotBetween:
					default:
						break;
				}
			}
		}

		private static int paramIndex = 0;

		/// <summary>
		/// Them dieu kien where vao stringbuilder dua theo conditions
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
						paramIndex++;
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

					paramIndex++;
					paramIndex++;
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

					paramIndex++;
				}
			}
		}

		/// <summary>
		/// Sua lai gia tri trong dataDict dua theo dieu kien where (tableList)
		/// </summary>
		/// <param name="tableList"></param>
		/// <param name="dataDict"></param>
		private static void SetWhereConditionValues(List<TableInfo> tableList, Dictionary<string, DataTable> dataDict)
		{
			// Set gia tri cho dieu kien WHERE
			foreach (TableInfo table in tableList)
			{
				DataTable schemaTable = GetSchemaTable(table);
				foreach (DataRow dataRow in dataDict[table.Alias].Rows)
				{
					SetWhereConditionsForDataRow(table, schemaTable, dataRow);
				}
			}

			List<Condition> allConditions = tableList.SelectMany(tbl => tbl.Conditions).ToList();

			// Set gia tri cho dieu kien join
			// TODO: ALL ROWS
			foreach (Join join in tableList.SelectMany(tbl => tbl.Joins).OrderBy(j => j.Operator))
			{
				switch (join.Operator)
				{
					case Operators.Between:
					case Operators.Equal:
					case Operators.GreaterThanOrEqual:
					case Operators.In:
					case Operators.LessThanOrEqual:
						// Neu co dieu kien WHERE tuong ung voi dieu kien JOIN, uu tien
						if (allConditions.Exists(cnd => cnd.Table == join.Table1 && cnd.Column == join.Column1 && cnd.Operator >= Operators.Equal))
						{
							//dataDict[join.Table2.Alias].Rows[0][join.Column2] = dataDict[join.Table1.Alias].Rows[0][join.Column1];
							EditAllColumnData(join.Table2.Alias, join.Column2, dataDict[join.Table1.Alias].Rows[0][join.Column1], dataDict);
							break;
						}

						//dataDict[join.Table1.Alias].Rows[0][join.Column1] = dataDict[join.Table2.Alias].Rows[0][join.Column2];
						EditAllColumnData(join.Table1.Alias, join.Column1, dataDict[join.Table2.Alias].Rows[0][join.Column2], dataDict);
						break;
					// TODO: Other Operators
					case Operators.GreaterThan:
					case Operators.LessThan:
						object data = GetDataForInequalityOperator(
							join.Operator,
							dataDict[join.Table2.Alias].Rows[0][join.Column2],
							allConditions.Where(cond => cond.Table == join.Table1 && cond.Column == join.Column1).ToList());

						if (data != null)
						{
							//dataDict[join.Table1.Alias].Rows[0][join.Column1] = data;
							EditAllColumnData(join.Table1.Alias, join.Column1, data, dataDict);
						}

						break;
					case Operators.NotBetween:
					case Operators.NotEqual:
					default:
						break;
				}
			}
		}

		private static void EditAllColumnData(string tableName, string columnName, object data, Dictionary<string, DataTable> dataDict)
		{
			foreach (DataRow row in dataDict[tableName].Rows)
			{
				row[columnName] = data;
			}
		}

		const string dateFormat = "yyyyMMdd";

		/// <summary>
		/// Lay ra data cho dieu kien lon hon, nho hon
		/// </summary>
		/// <param name="op">Toan tu</param>
		/// <param name="compareValue">Gia tri so sanh</param>
		/// <param name="conditions">Dieu kien WHERE</param>
		/// <returns></returns>
		private static object GetDataForInequalityOperator(Operators op, object compareValue, List<Condition> conditions)
		{
			object parsedValue;
			Type objType = DetermineType(compareValue, out parsedValue);

			switch (op)
			{
				case Operators.GreaterThan:
					if (objType == typeof(DateTime))
					{
						return ((DateTime)parsedValue).AddDays(1).ToString(dateFormat);
					}

					if (objType == typeof(int))
					{
						return ((int)parsedValue) + 1;
					}

					if (objType == typeof(decimal))
					{
						return ((decimal)parsedValue) + 1;
 					}

					break;
				case Operators.LessThan:
					if (objType == typeof(DateTime))
					{
						return ((DateTime)parsedValue).AddDays(-1).ToString(dateFormat);
					}

					if (objType == typeof(int))
					{
						return ((int)parsedValue) - 1;
					}

					if (objType == typeof(decimal))
					{
						return ((decimal)parsedValue) - 1;
 					}

					break;
				default:
					break;

			}

			return null;
		}

		private static bool AreConditionsSatisfied()
		{
			return false;
		}

		private static Type DetermineType(object value, out object parsedValue)
		{
			string strVal = value as string;
			if (strVal != null)
			{
				DateTime dateVal;
				if (DateTime.TryParseExact(
							strVal,
							dateFormat,
							null,
							System.Globalization.DateTimeStyles.None,
							out dateVal))
				{
					parsedValue = dateVal;
					return typeof(DateTime);
				}

				int intVal;
				if (int.TryParse(strVal, out intVal))
				{
					parsedValue = intVal;
					return typeof(int);
				}

				decimal decVal;
				if (decimal.TryParse(strVal, out decVal))
				{
					parsedValue = decVal;
					return typeof(decimal);
				}

				parsedValue = strVal;
				return typeof(string);
			}

			parsedValue = null;
			return null;
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
				SetDataSource(dataTable);
			}
		}

		private static string fileName = null;

		private void btnExcel_Click(object sender, EventArgs e)
		{
			Button btnSender = (Button)sender;
			ContextMenu cm = new ContextMenu();
			cm.MenuItems.Add("Current table").Click += ExportExcelOne;
			cm.MenuItems.Add("All").Click += ExportExcelAll;

			Point ptLowerLeft = new Point(0, btnSender.Height);
			ptLowerLeft = btnSender.PointToScreen(ptLowerLeft);
			cm.Show(btnSender, btnSender.PointToClient(Cursor.Position));
		}

		private void ExportExcelOne(object sender, EventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "Excel|*.xlsx";
			saveFileDialog.Title = "Choose a location";
			saveFileDialog.FileName = Path.GetFileName(fileName);
			saveFileDialog.ShowDialog();

			if (!string.IsNullOrEmpty(saveFileDialog.FileName))
			{
				fileName = saveFileDialog.FileName;
				DataTable dataTable;
				if (this.tempData.TryGetValue(tableNamesComboBox.SelectedValue.ToString(), out dataTable))
				{
					ExcelWriter.Write(dataTable, tableNamesComboBox.SelectedValue.ToString(), fileName);
					lblStatus.Text = "Export to Excel successful.";
				}
			}
		}

		private void ExportExcelAll(object sender, EventArgs e)
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "Excel|*.xlsx";
			saveFileDialog.Title = "Choose a location";
			saveFileDialog.FileName = Path.GetFileName(fileName);
			saveFileDialog.ShowDialog();

			if (!string.IsNullOrEmpty(saveFileDialog.FileName))
			{
				fileName = saveFileDialog.FileName;
				ExcelWriter.Write(this.tempData, fileName);
				lblStatus.Text = "Export to Excel successful.";
			}
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
			SetDataSource(this.tempData[this.setting.TablesInfo[prevIndex].Alias]);
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
			SetDataSource(this.tempData[this.setting.TablesInfo[nextIndex].Alias]);
		}

		private async void btnParse_Click(object sender, EventArgs e)
		{
			this.btnClear_Click(sender, e);
			lblStatus.Text = string.Empty;
			string query = txtQuery.Text;
			if (string.IsNullOrEmpty(query))
			{
				lblStatus.Text = "Query is empty.";
				return;
			}

			btnParse.Enabled = false;

			cancellationSource = new CancellationTokenSource();
			this.rotateTask = Task.Run(() => this.RotateText(cancellationSource.Token), cancellationSource.Token);

			bool success = await Task.Run<bool>(() => this.Initialize(query));
			if (success)
			{
				this.btnCancel_Click(sender, e);

				if (tempData.Count > 0)
				{
					//btnExcel.Enabled = true;
					//btnInsert.Enabled = true;

					tableNamesComboBox.ValueMember = "Value";
					tableNamesComboBox.DisplayMember = "Text";
					tableNamesComboBox.DataSource = this.setting.TablesInfo.Select(t => new { Text = string.Format("{0} ({1})", t.Name, t.Alias), Value = t.Alias }).ToList();
					tableNamesComboBox.SelectedValue = this.setting.TablesInfo[0].Alias;

					SetDataSource(this.tempData[this.setting.TablesInfo[0].Alias]);
				}
			}

			btnParse.Enabled = true;
		}

		CancellationTokenSource cancellationSource = null;
		private Task rotateTask = null;

		private void RotateText(CancellationToken cancelToken)
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			while (true)
			{
				this.SetStatusText("Loading.");
				Thread.Sleep(1000);
				if (cancelToken.IsCancellationRequested)
				{
					break;
				}

				this.SetStatusText("Loading..");
				Thread.Sleep(1000);
				if (cancelToken.IsCancellationRequested)
				{
					break;
				}

				this.SetStatusText("Loading...");
				Thread.Sleep(1000);
				if (cancelToken.IsCancellationRequested)
				{
					break;
				}

				if (stopwatch.ElapsedMilliseconds >= 5000)
				{
					this.EnableCancellation();
				}
			}
		}

		delegate void EnableCancelCallback();
		private void EnableCancellation()
		{
			if (this.btnCancel.InvokeRequired)
			{
				EnableCancelCallback callback = new EnableCancelCallback(EnableCancellation);
				this.Invoke(callback);
			}
			else
			{
				// this.btnCancel.Visible = true;
			}
		}

		delegate void SetStatusTextCallback(string text);
		private void SetStatusText(string text)
		{
			//if (this.lblStatus.InvokeRequired)
			//{
			//	SetStatusTextCallback callback = new SetStatusTextCallback(SetStatusText);
			//	this.Invoke(callback, new object[] { text });
			//}
			//else
			//{
				this.lblStatus.Text = text;
			//}
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
			SetDataSource(null);
		}

		private void SetDataSource(DataTable table)
		{
			this.previewGrid.DataSource = null;
			this.previewGrid.DataSource = table;
			foreach (DataGridViewColumn col in this.previewGrid.Columns)
			{
				col.SortMode = DataGridViewColumnSortMode.NotSortable;
			}

			this.previewGrid.Focus();
			btnExcel.Enabled = (table != null);
		}

		private async void btnCancel_Click(object sender, EventArgs e)
		{
			if (this.rotateTask != null && this.rotateTask.Status == TaskStatus.Running && this.cancellationSource != null)
			{
				this.cancellationSource.Cancel();
				await this.rotateTask;
				lblStatus.Text = string.Empty;

				this.rotateTask.Dispose();
				this.rotateTask = null;

				this.cancellationSource.Dispose();
				this.cancellationSource = null;
			}
		}

		private void chkHeaderCopy_CheckedChanged(object sender, EventArgs e)
		{
			if (this.chkHeaderCopy.Checked)
			{
				this.previewGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
			}
			else
			{
				this.previewGrid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
			}
		}

		private void btnCopy_Click(object sender, EventArgs e)
		{
			this.previewGrid.SelectAll();
			Clipboard.SetDataObject(this.previewGrid.GetClipboardContent() ?? new DataObject(string.Empty));
		}
	}
}
