
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataGenerator
{
	internal class SqlParser
	{
		public IEnumerable<Table> TableSettings
		{
			get
			{
				return this.settings.AsReadOnly();
			}
		}

		public IList<ParseError> Errors { get; private set; }

		private List<Table> settings = new List<Table>();

		public SqlParser(string query)
		{
			TSqlParser parser = new TSql140Parser(true);
			IList<ParseError> errors;
			TSqlScript script;

			Errors = new List<ParseError>();

			using (TextReader reader = new StringReader(query))
			{
				script = (TSqlScript)parser.Parse(reader, out errors);
			}

			if (errors.Count > 0)
			{
				this.Errors = errors;
				return;
			}

			TSqlStatement statement = script.Batches[0].Statements[0];
			if (statement is SelectStatement)
			{
				SelectStatement selectQuery = (SelectStatement)statement;
				QuerySpecification querySpec = (QuerySpecification)selectQuery.QueryExpression;

				// Table list
				List<Table> tableList = new List<Table>();
				SqlParser.GetTableListFromQuerySpec(querySpec, tableList);

				// Lay ra dieu kien WHERE
				if (querySpec.WhereClause.SearchCondition is BooleanBinaryExpression)
				{
					GetJoinConditions((BooleanBinaryExpression)querySpec.WhereClause.SearchCondition, tableList);
				}

				this.settings = tableList;
			}
			else
			{
				ParseError error = new ParseError(0, 0, 0, 0, "Not a select statement.");
				errors.Add(error);
				this.Errors = errors;
				return;
			}
		}

		private static void GetTableListFromQuerySpec(QuerySpecification querySpec, List<Table> tableList)
		{
			TableReference tableRef = querySpec.FromClause.TableReferences[0];

			if (tableRef is NamedTableReference)
			{
				// Truong hop khong co join
				NamedTableReference namedTableRef = (NamedTableReference)tableRef;

				tableList.Add(new Table()
				{
					Alias = namedTableRef.Alias.Value,
					Name = namedTableRef.SchemaObject.BaseIdentifier.Value,
					Conditions = new List<Condition>(),
					Joins = new List<Join>(),
				});
			}

			// Truong hop co join, lay ra thong tin join cua cac table
			if (tableRef is QualifiedJoin)
			{
				QualifiedJoin join = (QualifiedJoin)tableRef;
				SqlParser.GetTable(join, tableList);

				// Lay ra chi tiet noi dung join
				SqlParser.GetJoins(join, tableList);
			}
		}

		private static void GetJoins(QualifiedJoin joinExpression, List<Table> tableList)
		{
			// Recursive de lay ra tat ca cac join
			if (joinExpression.FirstTableReference is QualifiedJoin)
			{
				SqlParser.GetJoins((QualifiedJoin)joinExpression.FirstTableReference, tableList);
			}

			// Lay ra dieu kien join
			BooleanExpression expression = joinExpression.SearchCondition;
			if (expression is BooleanBinaryExpression)
			{
				SqlParser.GetJoinConditions((BooleanBinaryExpression)expression, tableList);
			}
		}

		private static void GetJoinConditions(BooleanBinaryExpression expression, List<Table> tableList)
		{
			if (expression.FirstExpression is BooleanBinaryExpression)
			{
				SqlParser.GetJoinConditions((BooleanBinaryExpression)expression.FirstExpression, tableList);

				// Chi lay ra dieu kien so sanh column
				if (expression.SecondExpression is BooleanComparisonExpression)
				{
					SqlParser.AddComparisonExpression((BooleanComparisonExpression)expression.SecondExpression, tableList);
				}
				
				if (expression.SecondExpression is InPredicate)
				{
					// TODO: IN Subquery
					InPredicate inPredicate = (InPredicate)expression.SecondExpression;
					ColumnReferenceExpression colRef = (ColumnReferenceExpression)inPredicate.Expression;
					if (colRef.MultiPartIdentifier.Identifiers.Count == 2)
					{
						InValues<string> value = new InValues<string>(inPredicate.Values.Select(v => ((Literal)v).Value));

						Condition condition = new Condition()
						{
							Table = tableList.First(tbl => tbl.Alias == colRef.MultiPartIdentifier.Identifiers[0].Value),
							Column = colRef.MultiPartIdentifier.Identifiers[1].Value,
							Value = value,
							Operator = Operators.In,
						};

						tableList.First(tbl => tbl.Alias == colRef.MultiPartIdentifier.Identifiers[0].Value).Conditions.Add(condition);
					}
				}
			}

			if (expression.FirstExpression is BooleanComparisonExpression)
			{
				SqlParser.AddComparisonExpression((BooleanComparisonExpression)expression.FirstExpression, tableList);
			}
		}

		private static void AddComparisonExpression(BooleanComparisonExpression compareExpression, List<Table> tableList)
		{
			if (compareExpression.FirstExpression is ColumnReferenceExpression
				&& compareExpression.SecondExpression is ColumnReferenceExpression)
			{
				ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)compareExpression.FirstExpression;
				ColumnReferenceExpression joinRight = (ColumnReferenceExpression)compareExpression.SecondExpression;

				Join join = new Join()
				{
					Table1 = tableList.First(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value),
					Column1 = joinLeft.MultiPartIdentifier.Identifiers[1].Value,
					Table2 = tableList.First(tbl => tbl.Alias == joinRight.MultiPartIdentifier.Identifiers[0].Value),
					Column2 = joinRight.MultiPartIdentifier.Identifiers[1].Value,
					Operator = GetOperator(compareExpression.ComparisonType),
				};

				tableList.First(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value).Joins.Add(join);
			}
			else if (compareExpression.FirstExpression is ColumnReferenceExpression
				&& compareExpression.SecondExpression is Literal)
			{
				ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)compareExpression.FirstExpression;
				Literal stringValue = (Literal)compareExpression.SecondExpression;

				if (joinLeft.MultiPartIdentifier.Count == 2)
				{
					// Truong hop co ghi alias: B.CaseID = N'poPO'
					Condition condition = new Condition()
					{
						Table = tableList.First(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value),
						Column = joinLeft.MultiPartIdentifier.Identifiers[1].Value,
						Value = stringValue.Value,
						Operator = GetOperator(compareExpression.ComparisonType),
					};

					tableList.First(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value).Conditions.Add(condition);

				}
				else if (joinLeft.MultiPartIdentifier.Count == 1)
				{
					// Truong hop khong ghi alias: CaseUse = N'Y'
					// TODO: LAM THE NAO?
				}
			}
		}

		private static Operators GetOperator(BooleanComparisonType comparisonType)
		{
			switch (comparisonType)
			{
				case BooleanComparisonType.Equals:
					return Operators.Equal;
				case BooleanComparisonType.GreaterThan:
					return Operators.GreaterThan;
				case BooleanComparisonType.GreaterThanOrEqualTo:
					return Operators.GreaterThanOrEqual;
				case BooleanComparisonType.LessThan:
					return Operators.LessThan;
				case BooleanComparisonType.LessThanOrEqualTo:
					return Operators.LessThanOrEqual;
				case BooleanComparisonType.NotEqualToBrackets:
				case BooleanComparisonType.NotEqualToExclamation:
					return Operators.NotEqual;
				default:
					throw new InvalidOperationException("OPERATOR!!");
			}
		}

		/// <summary>
		/// Recursive de tim ra tat ca cac join cua cau lenh select
		/// </summary>
		/// <param name="join"></param>
		/// <param name="tableList"></param>
		private static void GetTable(QualifiedJoin join, List<Table> tableList)
		{
			if (join.FirstTableReference is QualifiedJoin)
			{
				SqlParser.GetTable((QualifiedJoin)join.FirstTableReference, tableList);
			}

			if (join.FirstTableReference is NamedTableReference)
			{
				NamedTableReference namedTableRef = (NamedTableReference)join.FirstTableReference;
				tableList.Add(new Table()
				{
					Alias = namedTableRef.Alias.Value,
					Name = namedTableRef.SchemaObject.BaseIdentifier.Value,
					Joins = new List<Join>(),
					Conditions = new List<Condition>(),
				});
			}

			if (join.SecondTableReference is NamedTableReference)
			{
				NamedTableReference namedTableRef2 = (NamedTableReference)join.SecondTableReference;
				tableList.Add(new Table()
				{
					Alias = namedTableRef2.Alias.Value,
					Name = namedTableRef2.SchemaObject.BaseIdentifier.Value,
					Joins = new List<Join>(),
					Conditions = new List<Condition>(),
				});
			}

			// TODO: Subquery
			if (join.FirstTableReference is QueryDerivedTable)
			{
				SqlParser.GetTableFromSubquery((QueryDerivedTable)join.FirstTableReference, tableList);
			}

			if (join.SecondTableReference is QueryDerivedTable)
			{
				SqlParser.GetTableFromSubquery((QueryDerivedTable)join.SecondTableReference, tableList);
			}
		}

		private static void GetTableFromSubquery(QueryDerivedTable table, List<Table> tableList)
		{
			QuerySpecification querySpec = (QuerySpecification)table.QueryExpression;
			SqlParser.GetTableListFromQuerySpec(querySpec, tableList);
		}
	}
}
