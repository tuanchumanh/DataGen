
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

		private List<Table> settings;

		public SqlParser(string query)
		{
			TSqlParser parser = new TSql140Parser(true);
			IList<ParseError> errors;
			TSqlScript script;

			using (TextReader reader = new StringReader(query))
			{
				script = (TSqlScript)parser.Parse(reader, out errors);
			}

			if (errors.Count > 0)
			{
				return;
			}

			TSqlStatement statement = script.Batches[0].Statements[0];
			if (statement is SelectStatement)
			{
				SelectStatement selectQuery = (SelectStatement)statement;
				QuerySpecification querySpec = (QuerySpecification)selectQuery.QueryExpression;

				// Table list
				List<Table> tableList = new List<Table>();
				TableReference tableRef = querySpec.FromClause.TableReferences[0];

				if (tableRef is NamedTableReference)
				{
					NamedTableReference namedTableRef = (NamedTableReference)tableRef;

					tableList.Add(new Table()
					{
						Alias = namedTableRef.Alias.Value,
						Name = namedTableRef.SchemaObject.BaseIdentifier.Value,
					});
				}

				// Join list
				if (tableRef is QualifiedJoin)
				{
					// Truong hop co join
					QualifiedJoin join = (QualifiedJoin)tableRef;
					GetTable(join, tableList);

					// Lay ra chi tiet noi dung join
					GetJoins(join, tableList);
				}

				// Condition list
				if (querySpec.WhereClause.SearchCondition is BooleanBinaryExpression)
				{
					GetJoinConditions((BooleanBinaryExpression)querySpec.WhereClause.SearchCondition, tableList);
				}

				this.settings = tableList;
			}
			else
			{
				return;
			}
		}

		private static void GetJoins(QualifiedJoin joinExpression, List<Table> tableList)
		{
			if (joinExpression.FirstTableReference is QualifiedJoin)
			{
				GetJoins((QualifiedJoin)joinExpression.FirstTableReference, tableList);
			}

			var expression = joinExpression.SearchCondition;
			if (expression is BooleanBinaryExpression)
			{
				GetJoinConditions((BooleanBinaryExpression)expression, tableList);
			}
		}

		private static void GetJoinConditions(BooleanBinaryExpression expression, List<Table> tableList)
		{
			if (expression.FirstExpression is BooleanBinaryExpression)
			{
				GetJoinConditions((BooleanBinaryExpression)expression.FirstExpression, tableList);

				// Chi lay ra dieu kien so sanh column
				if (expression.SecondExpression is BooleanComparisonExpression)
				{
					BooleanComparisonExpression compareExpression = (BooleanComparisonExpression)expression.SecondExpression;
					if (compareExpression.FirstExpression is ColumnReferenceExpression
						&& compareExpression.SecondExpression is ColumnReferenceExpression)
					{
						ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)compareExpression.FirstExpression;
						ColumnReferenceExpression joinRight = (ColumnReferenceExpression)compareExpression.SecondExpression;

						Join join = new Join()
						{
							Table1 = tableList.Single(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value),
							Column1 = joinLeft.MultiPartIdentifier.Identifiers[1].Value,
							Table2 = tableList.Single(tbl => tbl.Alias == joinRight.MultiPartIdentifier.Identifiers[0].Value),
							Column2 = joinRight.MultiPartIdentifier.Identifiers[1].Value,
							Operator = GetOperator(compareExpression.ComparisonType),
						};

						tableList.Single(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value).Joins.Add(join);
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
								Table = tableList.Single(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value),
								Column = joinLeft.MultiPartIdentifier.Identifiers[1].Value,
								Value = stringValue.Value,
								Operator = GetOperator(compareExpression.ComparisonType),
							};

							tableList.Single(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value).Conditions.Add(condition);
						}
						else if (joinLeft.MultiPartIdentifier.Count == 1)
						{
							// Truong hop khong ghi alias: CaseUse = N'Y'
							// TODO: LAM THE NAO?
						}
					}
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
							Table = tableList.Single(tbl => tbl.Alias == colRef.MultiPartIdentifier.Identifiers[0].Value),
							Column = colRef.MultiPartIdentifier.Identifiers[1].Value,
							Value = value,
							Operator = Operators.In,
						};

						tableList.Single(tbl => tbl.Alias == colRef.MultiPartIdentifier.Identifiers[0].Value).Conditions.Add(condition);
					}
				}
			}

			if (expression.FirstExpression is BooleanComparisonExpression)
			{
				BooleanComparisonExpression compareExpression = (BooleanComparisonExpression)expression.FirstExpression;
				if (compareExpression.FirstExpression is ColumnReferenceExpression
					&& compareExpression.SecondExpression is ColumnReferenceExpression)
				{
					ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)compareExpression.FirstExpression;
					ColumnReferenceExpression joinRight = (ColumnReferenceExpression)compareExpression.SecondExpression;

					Join join = new Join()
					{
						Table1 = tableList.Single(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value),
						Column1 = joinLeft.MultiPartIdentifier.Identifiers[1].Value,
						Table2 = tableList.Single(tbl => tbl.Alias == joinRight.MultiPartIdentifier.Identifiers[0].Value),
						Column2 = joinRight.MultiPartIdentifier.Identifiers[1].Value,
						Operator = GetOperator(compareExpression.ComparisonType),
					};

					tableList.Single(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value).Joins.Add(join);
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
				GetTable((QualifiedJoin)join.FirstTableReference, tableList);
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

			NamedTableReference namedTableRef2 = (NamedTableReference)join.SecondTableReference;

			tableList.Add(new Table()
			{
				Alias = namedTableRef2.Alias.Value,
				Name = namedTableRef2.SchemaObject.BaseIdentifier.Value,
				Joins = new List<Join>(),
				Conditions = new List<Condition>(),
			});
		}
	}
}
