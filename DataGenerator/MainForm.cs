using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DataGenerator
{
	public partial class MainForm : Form
	{
		private Mapping setting = new Mapping();
		private Dictionary<string, DataTable> tempData = new Dictionary<string, DataTable>();
		private readonly static string connectionString = ConfigurationManager.ConnectionStrings["NULDEMOConnectionString"].ToString();
		private string[] commandLineArgs;

		public MainForm()
		{
			InitializeComponent();
			foreach (Table table in setting.Tables)
			{
				tableNamesComboBox.DataSource = setting.Tables.Select(t => new { Text = string.Format("{0} ({1})", t.Name, t.Alias), Value = t.Alias }).ToList();
			}
		}

		public MainForm(string[] args)
			: this()
		{
			this.commandLineArgs = args;
		}

		private void StartButton_Click(object sender, EventArgs e)
		{
			if (!DataExists(setting.Tables[0]))
			{
				foreach (Table table in setting.Tables)
				{
					DataTable dummyData = CreateDummyDataFromScratch(table);
					tempData[table.Alias] = dummyData;
				}

				foreach (Join join in setting.Tables.SelectMany(tbl => tbl.Joins))
				{
					tempData[join.Table2.Alias].Rows[0][join.Column2] = tempData[join.Table1.Alias].Rows[0][join.Column1];
				}
			}
			else
			{
				tempData.Add(setting.Tables[0].Alias, GetData(setting.Tables[0]));
				List<Join> failedJoins = CheckJoinsForTable(setting.Tables[0]);
				if (failedJoins.Count > 0)
				{
					DataSet dummyDataSet = CreateDummyData(failedJoins);
					foreach (DataTable table in dummyDataSet.Tables)
					{
						tempData[table.TableName] = table;
					}

					foreach (Join join in setting.Tables.SelectMany(tbl => tbl.Joins))
					{
						tempData[join.Table2.Alias].Rows[0][join.Column2] = tempData[join.Table1.Alias].Rows[0][join.Column1];
					}
				}
				else
				{
					// Data exists
					foreach (Table table in setting.Tables)
					{
						DataTable existingData = GetData(table);
						tempData[table.Alias] = existingData;
					}
				}
			}

			previewGrid.DataSource = tempData[setting.Tables[0].Alias];
			tableNamesComboBox.SelectedValue = setting.Tables[0].Alias;
		}

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

				foreach (Table joinTargetTable in
					table.Joins.GroupBy(j => j.Table2).Select(g => g.First().Table2))
				{
					int joinNo = 1;
					int paramIndex = 0;
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

						foreach (Condition cond1 in table.Conditions)
						{
							queryBuilder
								.AppendFormat(
									" {0}.{1} {2} @param{3} AND",
									join.Table1.Alias,
									cond1.Column,
									Mapping.GetOperatorForQuery(cond1.Operator),
									paramIndex)
								.AppendLine();
							cmd.Parameters.AddWithValue("@param" + paramIndex, cond1.Value);
							paramIndex++;
						}

						foreach (Condition cond2 in joinTargetTable.Conditions)
						{
							queryBuilder
								.AppendFormat(
									" {0}.{1} {2} @param{3} AND",
									join.Table2.Alias + joinNo,
									cond2.Column,
									Mapping.GetOperatorForQuery(cond2.Operator),
									paramIndex)
								.AppendLine();
							cmd.Parameters.AddWithValue("@param" + paramIndex, cond2.Value);
							paramIndex++;
						}

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
				foreach (Join join in table.Joins.OrderBy(j => j.Table2).ThenBy(j => j.Column2))
				{
					if ((int)result.Rows[0][colIdx++] != 1)
					{
						failedJoins.Add(join);
					}
				}
			}

			return failedJoins;
		}

		private static DataTable GetData(Table table)
		{
			using (SqlConnection sqlConnection1 = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.Connection = sqlConnection1;

				StringBuilder queryBuilder = new StringBuilder();
				queryBuilder.AppendFormat("SELECT TOP 1 * FROM {0} WHERE 1=1 AND", table.Name).AppendLine();

				int idx = 0;
				foreach (var cond in table.Conditions)
				{
					queryBuilder.AppendFormat(
							" {0} {1} @param{2} AND",
							cond.Column,
							Mapping.GetOperatorForQuery(cond.Operator),
							idx)
							.AppendLine();
					cmd.Parameters.AddWithValue("@param" + idx, cond.Value);
					idx++;
				}

				queryBuilder.Remove(queryBuilder.Length - 5, 3);
				cmd.CommandText = queryBuilder.ToString();

				sqlConnection1.Open();
				using (SqlDataReader reader = cmd.ExecuteReader())
				{
					DataTable result = new DataTable();
					result.Load(reader);
					return result;
				}
			}
		}

		private static bool DataExists(Table table)
		{
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
				cmd.CommandType = CommandType.Text;
				cmd.Connection = conn;

				StringBuilder queryBuilder = new StringBuilder();
				queryBuilder.AppendFormat("SELECT COUNT(*) FROM {0} WHERE 1=1 AND", table.Name).AppendLine();

				int idx = 0;
				foreach (var cond in table.Conditions)
				{
					queryBuilder
						.AppendFormat(
							" {0} {1} @param{2} AND",
							cond.Column,
							Mapping.GetOperatorForQuery(cond.Operator),
							idx)
						.AppendLine();
					cmd.Parameters.AddWithValue("@param" + idx, cond.Value);
					idx++;
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

					List<Join> successfulJoins = table1.Joins.Where(j => j.Table2 == table2 && !failedJoins.Contains(j)).OrderBy(j => j.Column2).ToList();
					if (successfulJoins.Count == 0)
					{
						// No sample data, create from scratch
						DataTable targetTable = new DataTable();
						targetTable = CreateDummyDataFromScratch(table2);
						targetTable.TableName = table2.Alias;
						result.Tables.Add(targetTable);
						continue;
					}

					// Has sample data
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

					int idx = 0;
					foreach (Condition cond in table1.Conditions)
					{
						conditionsBuilder
							.AppendFormat(
								" {0}.{1} {2} @param{3} AND",
								cond.Table.Alias,
								cond.Column,
								Mapping.GetOperatorForQuery(cond.Operator),
								idx)
							.AppendLine();
						cmd.Parameters.AddWithValue("@param" + idx, cond.Value);
						idx++;
					}

					foreach (Condition cond in table2.Conditions)
					{
						conditionsBuilder
							.AppendFormat(
								" {0}.{1} {2} @param{3} AND",
								cond.Table.Alias,
								cond.Column,
								Mapping.GetOperatorForQuery(cond.Operator),
								idx)
							.AppendLine();
						cmd.Parameters.AddWithValue("@param" + idx, cond.Value);
						idx++;
					}

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

		private static DataTable CreateDummyDataFromScratch(Table table)
		{
			using (SqlConnection conn = new SqlConnection(connectionString))
			using (SqlCommand cmd = new SqlCommand())
			{
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
					result.Columns.Add(columnInfo["ColumnName"] as string);
				}

				DataRow dummyRow = result.NewRow();
				foreach (DataRow columnInfo in schemaTable.Rows)
				{
					dummyRow[columnInfo["ColumnName"] as string] =
						Generator.GenerateDummyData(
							columnInfo["ColumnName"] as string,
							(Type)columnInfo["DataType"],
							(int)columnInfo["ColumnSize"],
							(short)columnInfo["NumericPrecision"],
							(short)columnInfo["NumericScale"]);
				}

				foreach (Condition cond in table.Conditions)
				{
					dummyRow[cond.Column] = cond.Value;
				}

				result.Rows.Add(dummyRow);

				return result;
			}
		}

		private void tableNamesComboBox_SelectedIndexChanged(object sender, EventArgs e)
		{
			DataTable dataTable;
			if (tempData.TryGetValue(tableNamesComboBox.SelectedValue.ToString(), out dataTable))
			{
				previewGrid.DataSource = dataTable;
			}
		}
	}
}
