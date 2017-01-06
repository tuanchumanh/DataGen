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
		public List<TableInfo> TablesInfo = new List<TableInfo>();
		public SqlParser Parser { get; private set; }

		public Mapping(string query)
		{
			if (string.IsNullOrEmpty(query))
			{
				return;
			}

			this.Parser = new SqlParser(query);
			this.TablesInfo = this.Parser.TableSettings.ToList();
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
				case Operators.Between:
					return "BETWEEN";
				case Operators.NotBetween:
					return "NOT BETWEEN";
				case Operators.Like:
					return "LIKE";
				default:
					throw new ArgumentException("OPERATOR??");
			}
		}

		//private static Operators GetOperator(string settingValue)
		//{
		//	switch (settingValue)
		//	{
		//		case "=":
		//			return Operators.Equal;
		//		case "≠":
		//			return Operators.NotEqual;
		//		case "≦":
		//			return Operators.GreaterThan;
		//		case "≧":
		//			return Operators.GreaterThanOrEqual;
		//		case ">":
		//			return Operators.LessThan;
		//		case "<":
		//			return Operators.LessThanOrEqual;
		//		case "IN":
		//			return Operators.In;
		//		case "BETWEEN":
		//			return Operators.Between;
		//		case "LIKE":
		//			return Operators.Like;
		//		default:
		//			throw new ArgumentException("OPERATOR??");
		//	}
		//}
	}

	/// <summary>
	/// ≠,=,≧,≦,&lt;,&gt;,IN
	/// </summary>
	public enum Operators
	{
		// Inequality
		NotEqual = 0,
		GreaterThan = 1,
		LessThan = 2,
		NotBetween = 3,
		// Equality
		Equal = 4,
		GreaterThanOrEqual = 5,
		LessThanOrEqual = 6,
		In = 7,
		Between = 8,
		Like = 9,
	}

	public class TableInfo : IComparable
	{
		public string Alias { get; set; }
		public string Name { get; set; }
		public List<Join> Joins { get; set; }
		public List<Condition> Conditions { get; set; }
		public string SubqueryAlias { get; set; }
		private List<string> keys = new List<string>();

		public override string ToString()
		{
			return string.Format("{0}:{1}", this.Alias, this.Name);
		}

		public int CompareTo(object obj)
		{
			if (obj == null)
			{
				return 1;
			}

			TableInfo tblObj = (TableInfo)obj;
			int result = this.Name.CompareTo(tblObj.Name);
			if (result == 0)
			{
				result = this.Alias.CompareTo(tblObj.Alias);
			}

			return (result);
		}
	}

	public class Join
	{
		public TableInfo Table1 { get; set; }
		public TableInfo Table2 { get; set; }
		public string Column1 { get; set; }
		public string Column2 { get; set; }
		public Operators Operator { get; set; }

		public override string ToString()
		{
			return string.Format("{0}.{1} {2} {3}.{4}", Table1, Column1, Mapping.GetOperatorForQuery(Operator), Table2, Column2);
		}
	}

	public class Condition
	{
		public TableInfo Table { get; set; }
		public string Column { get; set; }
		public object Value { get; set; }
		public object Value2 { get; set; }
		public Operators Operator { get; set; }

		public override string ToString()
		{
			return string.Format("{0}.{1} {2} {3} {4}", Table, Column, Mapping.GetOperatorForQuery(Operator), Value, Value2);
		}
	}

	public class Ranking : IComparable
	{
		public RankingType RankingType { get; set; }
		public TableInfo Table { get; set; }
		public string Column { get; set; }
		public object Value { get; set; }
		public int RankValue { get; set; }

		public override string ToString()
		{
			return string.Format("{0}:{1}:{2}{3}", RankValue, RankingType, Column, Value);
		}

		public int CompareTo(object obj)
		{
			Ranking rankObj = (Ranking)obj;
			return (this.RankValue.CompareTo(rankObj.RankValue));
		}
	}

	public enum RankingType
	{
		TableColumn,
		Value,
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
