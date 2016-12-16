using Excel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataGenerator
{
	internal class Mapping
	{
		public List<Table> Tables = new List<Table>();
		public SqlParser Parser { get; private set; }

		public Mapping()
		{
			this.ReadSettingFile();
		}

		public Mapping(string query)
		{
			if (string.IsNullOrEmpty(query))
			{
				this.ReadSettingFile();
				return;
			}

			this.Parser = new SqlParser(query);
			this.Tables = this.Parser.TableSettings.ToList();
		}

		protected void ReadSettingFile()
		{
			FileStream stream = File.Open(ConfigurationManager.AppSettings["SettingFilePath"], FileMode.Open, FileAccess.Read);
			IExcelDataReader excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream);

			excelReader.IsFirstRowAsColumnNames = true;
			DataSet result = excelReader.AsDataSet();

			if (result.Tables.Contains("Tables"))
			{
				DataTable tablesDt = result.Tables["Tables"];
				DataTable joinsDt = result.Tables.Contains("Joins") ? result.Tables["Joins"] : new DataTable();
				DataTable condsDt = result.Tables.Contains("Conditions") ? result.Tables["Conditions"] : new DataTable();

				foreach (DataRow table in tablesDt.Rows)
				{
					Table tableSetting = new Table
					{
						Name = table["TableName"] as string,
						Alias = table["Alias"] as string,
					};

					Tables.Add(tableSetting);
				}

				foreach (Table tableSetting in Tables)
				{
					tableSetting.Joins = new List<Join>();
					foreach (DataRow joins in joinsDt.Rows.Cast<DataRow>().Where(row => row["Table1"] as string == tableSetting.Alias))
					{
						tableSetting.Joins.Add(new Join
						{
							Column1 = joins["Column1"] as string,
							Column2 = joins["Column2"] as string,
							Operator = Mapping.GetOperator(joins["Operator"] as string),
							Table1 = tableSetting,
							Table2 = Tables.Where(tableSetting2 => tableSetting2.Alias == joins["Table2"] as string).First(),
						});
					}

					tableSetting.Conditions = new List<Condition>();
					foreach (DataRow joins in condsDt.Rows.Cast<DataRow>().Where(row => row["Table"] as string == tableSetting.Alias))
					{
						tableSetting.Conditions.Add(new Condition
						{
							Column = joins["Column"] as string,
							Operator = Mapping.GetOperator(joins["Operator"] as string),
							Value = joins["Value"],
							Table = tableSetting,
						});
					}
				}
			}
			else
			{
				throw new ArgumentException("Settings read failed.");
			}

			excelReader.Close();
		}

		public static string GetOperatorForQuery(Operators op)
		{
			switch (op)
			{
				case Operators.Equal:
					return "=";
				case Operators.NotEqual:
					return "!=";
				case Operators.GreaterThan:
					return ">";
				case Operators.GreaterThanOrEqual:
					return ">=";
				case Operators.LessThan:
					return "<";
				case Operators.LessThanOrEqual:
					return "<=";
				case Operators.In:
					return "IN";
				default:
					throw new ArgumentException("OPERATOR??");
			}
		}

		private static Operators GetOperator(string settingValue)
		{
			switch (settingValue)
			{
				case "=":
					return Operators.Equal;
				case "≠":
					return Operators.NotEqual;
				case "≦":
					return Operators.GreaterThan;
				case "≧":
					return Operators.GreaterThanOrEqual;
				case ">":
					return Operators.LessThan;
				case "<":
					return Operators.LessThanOrEqual;
				case "IN":
					return Operators.In;
				default:
					throw new ArgumentException("OPERATOR??");
			}
		}
	}

	/// <summary>
	/// ≠,=,≧,≦,&lt;,&gt;,IN
	/// </summary>
	public enum Operators
	{
		Equal,
		NotEqual,
		GreaterThan,
		GreaterThanOrEqual,
		LessThan,
		LessThanOrEqual,
		In,
	}

	public class Table
	{
		public string Alias { get; set; }
		public string Name { get; set; }
		public List<Join> Joins { get; set; }
		public List<Condition> Conditions { get; set; }
		private List<string> keys = new List<string>();
	}

	public class Join
	{
		public Table Table1 { get; set; }
		public Table Table2 { get; set; }
		public string Column1 { get; set; }
		public string Column2 { get; set; }
		public Operators Operator { get; set; }
	}

	public class Condition
	{
		public Table Table { get; set; }
		public string Column { get; set; }
		public object Value { get; set; }
		public Operators Operator { get; set; }
	}

	public interface InValues
	{

	}

	public class InValues<T> : IEnumerable<T>, InValues
	{
		private List<T> values = new List<T>();

		public InValues(IEnumerable<T> values)
		{
			this.values = values.ToList();
		}

		public override string ToString()
		{
			if (typeof(T) == typeof(String))
			{
				return string.Format("({0})", string.Join(",", values.Select(v => string.Format("N'{0}'", v))));
			}

			return string.Format("({0})", string.Join(",", values));
		}

		public IEnumerator<T> GetEnumerator()
		{
			return this.values.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}
}
