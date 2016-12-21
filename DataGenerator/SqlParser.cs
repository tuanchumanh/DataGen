
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;

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

			foreach (TSqlStatement statement in script.Batches.SelectMany(batch => batch.Statements))
			{
				if (statement is SelectStatement)
				{
					SelectStatement selectQuery = (SelectStatement)statement;
					QuerySpecification querySpec = (QuerySpecification)selectQuery.QueryExpression;

					// Table list
					List<Table> tableList = new List<Table>();
					SqlParser.GetTableListFromQuerySpec(querySpec, tableList);
					SqlParser.GetWhereConditionsForQuerySpec(querySpec, tableList);

					// Duplicate alias
					foreach (Table table in tableList)
					{
						if (tableList.Where(tbl => tbl != table).Any(tbl => tbl.Alias == table.Alias))
						{
							ParseError error = new ParseError(0, 0, 0, 0, string.Format("Duplicate alias: {0} {1}", table.Alias, table.Name));
							errors.Add(error);
						}
					}

					this.settings = tableList;
				}
				else
				{
					ParseError error = new ParseError(0, 0, 0, 0, "Not a select statement.");
					errors.Add(error);
				}
			}

			this.Errors = errors;
		}

		private static void GetTableListFromQuerySpec(QuerySpecification querySpec, List<Table> tableList)
		{
			TableReference tableRef = querySpec.FromClause.TableReferences[0];

			if (tableRef is NamedTableReference)
			{
				// Truong hop khong co join
				NamedTableReference namedTableRef = (NamedTableReference)tableRef;

				string alias = namedTableRef.Alias == null ? namedTableRef.SchemaObject.BaseIdentifier.Value : namedTableRef.Alias.Value;
				tableList.Add(new Table()
				{
					Alias = alias,
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

		private static void GetWhereConditionsForQuerySpec(QuerySpecification querySpec, List<Table> tableList)
		{
			// Lay ra dieu kien WHERE
			if (querySpec.WhereClause != null)
			{
				// Co JOIN
				if (querySpec.WhereClause.SearchCondition is BooleanBinaryExpression)
				{
					SqlParser.GetJoinConditions((BooleanBinaryExpression)querySpec.WhereClause.SearchCondition, tableList);
				}

				// Dieu kien WHERE so sanh
				if (querySpec.WhereClause.SearchCondition is BooleanComparisonExpression)
				{
					SqlParser.AddComparisonCondition((BooleanComparisonExpression)querySpec.WhereClause.SearchCondition, tableList);
				}

				// Dieu kien WHERE la IN
				if (querySpec.WhereClause.SearchCondition is InPredicate)
				{
					SqlParser.AddInPredicateCondition((InPredicate)querySpec.WhereClause.SearchCondition, tableList);
				}

				// Dieu kien WHERE la BETWEEN
				if (querySpec.WhereClause.SearchCondition is BooleanTernaryExpression)
				{
					SqlParser.AddBetweenCondition((BooleanTernaryExpression)querySpec.WhereClause.SearchCondition, tableList);
				}
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
			// Dieu kien ghep => recursive de lay ra het
			if (expression.FirstExpression is BooleanBinaryExpression)
			{
				SqlParser.GetJoinConditions((BooleanBinaryExpression)expression.FirstExpression, tableList);

				// Chi lay ra dieu kien so sanh column
				if (expression.SecondExpression is BooleanComparisonExpression)
				{
					SqlParser.AddComparisonCondition((BooleanComparisonExpression)expression.SecondExpression, tableList);
				}
				
				if (expression.SecondExpression is InPredicate)
				{
					// TODO: IN Subquery
					InPredicate inPredicate = (InPredicate)expression.SecondExpression;
					SqlParser.AddInPredicateCondition(inPredicate, tableList);
				}
			}

			// Dieu kien so sanh
			if (expression.FirstExpression is BooleanComparisonExpression)
			{
				SqlParser.AddComparisonCondition((BooleanComparisonExpression)expression.FirstExpression, tableList);
			}

			if (expression.SecondExpression is BooleanComparisonExpression)
			{
				SqlParser.AddComparisonCondition((BooleanComparisonExpression)expression.SecondExpression, tableList);
			}

			// Dieu kien IN
			if (expression.FirstExpression is BooleanTernaryExpression)
			{
				SqlParser.AddBetweenCondition((BooleanTernaryExpression)expression.FirstExpression, tableList);
			}

			if (expression.SecondExpression is BooleanTernaryExpression)
			{
				SqlParser.AddBetweenCondition((BooleanTernaryExpression)expression.SecondExpression, tableList);
			}
		}

		private static void AddInPredicateCondition(InPredicate inPredicate, List<Table> tableList)
		{
			ColumnReferenceExpression colRef = (ColumnReferenceExpression)inPredicate.Expression;

			// Lay ra table cua dieu kien
			string colName = colRef.MultiPartIdentifier.Identifiers[colRef.MultiPartIdentifier.Identifiers.Count - 1].Value;
			Table targetTable = SqlParser.GetTableHavingColumn(colName, colRef, tableList);

			// Truong hop IN chi dinh gia tri cu the
			if (inPredicate.Values.Count > 0)
			{
				InValues<string> value = new InValues<string>(inPredicate.Values.Select(v => ((Literal)v).Value));
				Condition condition = new Condition()
				{
					Table = targetTable,
					Column = colName,
					Value = value,
					Operator = Operators.In,
				};

				targetTable.Conditions.Add(condition);
			}

			// Truong hop IN subquery
			if (inPredicate.Subquery != null)
			{
				QuerySpecification querySpec = (QuerySpecification)inPredicate.Subquery.QueryExpression;
				List<Table> subQueryTableList = new List<Table>();
				SqlParser.GetTableListFromQuerySpec(querySpec, subQueryTableList);

				// Get ra column select cua subquery
				IList<SelectElement> selectElements = querySpec.SelectElements;
				if (selectElements.Count == 1)
				{
					SelectElement element = selectElements[0];
					if (element is SelectScalarExpression)
					{
						SelectScalarExpression expression = (SelectScalarExpression)element;
						if (expression.Expression is ColumnReferenceExpression)
						{
							// Neu select column, tim ra column thuoc bang nao
							ColumnReferenceExpression subColRef = (ColumnReferenceExpression)expression.Expression;
							string subQueryColName = subColRef.MultiPartIdentifier.Identifiers[subColRef.MultiPartIdentifier.Identifiers.Count - 1].Value;
							Table subQueryTargetTable = SqlParser.GetTableHavingColumn(subQueryColName, subColRef, subQueryTableList);
							if (subQueryTargetTable != null)
							{
								// Tim thay table => coi dieu kien IN nhu la 1 dieu kien join
								// Add them bang vao
								SqlParser.GetTableListFromQuerySpec(querySpec, tableList);

								// TODO: Where condition
								Join subQueryJoin = new Join()
								{
									Table1 = targetTable,
									Table2 = subQueryTargetTable,
									Operator = Operators.Equal,
									Column1 = colName,
									Column2 = subQueryColName,
								};

								targetTable.Joins.Add(subQueryJoin);
							}
						}

						if (expression.Expression is Literal)
						{
							// Neu select khong phai column, set value
							Condition condition = new Condition()
							{
								Table = targetTable,
								Column = colName,
								Value = ((Literal)expression.Expression).Value,
								Operator = Operators.Equal,
							};

							targetTable.Conditions.Add(condition);
						}
					}
				}
			}
		}

		private static void AddBetweenCondition(BooleanTernaryExpression expression, List<Table> tableList)
		{
			if (expression.FirstExpression is ColumnReferenceExpression &&
								  expression.SecondExpression is Literal &&
								  expression.ThirdExpression is Literal)
			{
				// 1 cai la Reference, 1 cai la value => Dieu kien
				ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)expression.FirstExpression;
				Literal value = (Literal)expression.SecondExpression;
				Literal value2 = (Literal)expression.ThirdExpression;

				// Lay ra table name cua dieu kien tu Identifier cua Reference
				string colName = joinLeft.MultiPartIdentifier.Identifiers[joinLeft.MultiPartIdentifier.Identifiers.Count - 1].Value; ;

				// Tim ra table tuong ung
				Table targetTable = SqlParser.GetTableHavingColumn(colName, joinLeft, tableList);

				if (targetTable != null)
				{
					Condition condition = new Condition()
					{
						Table = targetTable,
						Column = colName,
						Value = value.Value,
						Value2 = value2.Value,
						Operator = SqlParser.GetOperator(expression.TernaryExpressionType),
					};

					targetTable.Conditions.Add(condition);
				}
			}
		}

		private static void AddComparisonCondition(BooleanComparisonExpression compareExpression, List<Table> tableList)
		{
			if (compareExpression.FirstExpression is ColumnReferenceExpression
				&& compareExpression.SecondExpression is ColumnReferenceExpression)
			{
				// Ca 2 deu la reference => Dieu kien join
				ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)compareExpression.FirstExpression;
				ColumnReferenceExpression joinRight = (ColumnReferenceExpression)compareExpression.SecondExpression;

				string table1Alias = joinLeft.MultiPartIdentifier.Identifiers[0].Value;
				string table2Alias = joinRight.MultiPartIdentifier.Identifiers[0].Value;

				Table table1 = tableList.FirstOrDefault(tbl => tbl.Alias == table1Alias);
				Table table2 = tableList.FirstOrDefault(tbl => tbl.Alias == table2Alias);

				if (table1 != null && table2 != null)
				{
					Join join = new Join()
					{
						Table1 = table1,
						Column1 = joinLeft.MultiPartIdentifier.Identifiers[1].Value,
						Table2 = table2,
						Column2 = joinRight.MultiPartIdentifier.Identifiers[1].Value,
						Operator = SqlParser.GetOperator(compareExpression.ComparisonType),
					};

					tableList.First(tbl => tbl.Alias == table1Alias).Joins.Add(join);
				}
				else
				{
					// TODO Khong tim thay bang => Subquery
				}
			}
			else if (compareExpression.FirstExpression is ColumnReferenceExpression
				&& compareExpression.SecondExpression is Literal)
			{
				// 1 cai la Reference, 1 cai la value => Dieu kien
				ColumnReferenceExpression joinLeft = (ColumnReferenceExpression)compareExpression.FirstExpression;
				Literal stringValue = (Literal)compareExpression.SecondExpression;

				// Lay ra table name cua dieu kien tu Identifier cua Reference
				string colName = joinLeft.MultiPartIdentifier.Identifiers[joinLeft.MultiPartIdentifier.Identifiers.Count - 1].Value; ;

				// Tim ra table tuong ung
				Table targetTable = SqlParser.GetTableHavingColumn(colName, joinLeft, tableList);

				if (targetTable != null)
				{
					Condition condition = new Condition()
					{
						Table = targetTable,
						Column = colName,
						Value = stringValue.Value,
						Operator = SqlParser.GetOperator(compareExpression.ComparisonType),
					};

					targetTable.Conditions.Add(condition);
				}
			}
		}

		/// <summary>
		/// Tim xem column thuoc table nao va return table
		/// </summary>
		/// <param name="colName"></param>
		/// <param name="joinLeft"></param>
		/// <param name="tableList"></param>
		/// <returns></returns>
		private static Table GetTableHavingColumn(string colName, ColumnReferenceExpression joinLeft, List<Table> tableList)
		{
			Table targetTable = null;
			if (joinLeft.MultiPartIdentifier.Count == 2)
			{
				targetTable = tableList.FirstOrDefault(tbl => tbl.Alias == joinLeft.MultiPartIdentifier.Identifiers[0].Value);
			}
			else if (joinLeft.MultiPartIdentifier.Count == 1)
			{
				if (tableList.Count == 1)
				{
					targetTable = tableList[0];
				}
				else if (tableList.Count > 1)
				{
					foreach (Table table in tableList)
					{
						using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["NULDEMOConnectionString"].ToString()))
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

							DataRow colSpec = schemaTable.Rows.Cast<DataRow>().Where(col =>
								colName.Equals(col["ColumnName"] as string, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
							if (colSpec != null)
							{
								targetTable = table;
								break;
							}
						}
					}
				}
			}

			return targetTable;
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

		private static Operators GetOperator(BooleanTernaryExpressionType ternaryExpressionType)
		{
			switch (ternaryExpressionType)
			{
				case BooleanTernaryExpressionType.Between:
					return Operators.Between;
				case BooleanTernaryExpressionType.NotBetween:
					return Operators.NotBetween;
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
				string alias = namedTableRef.Alias == null ? namedTableRef.SchemaObject.BaseIdentifier.Value : namedTableRef.Alias.Value;
				tableList.Add(new Table()
				{
					Alias = alias,
					Name = namedTableRef.SchemaObject.BaseIdentifier.Value,
					Joins = new List<Join>(),
					Conditions = new List<Condition>(),
				});
			}

			if (join.SecondTableReference is NamedTableReference)
			{
				NamedTableReference namedTableRef2 = (NamedTableReference)join.SecondTableReference;
				string alias = namedTableRef2.Alias == null ? namedTableRef2.SchemaObject.BaseIdentifier.Value : namedTableRef2.Alias.Value;
				tableList.Add(new Table()
				{
					Alias = alias,
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

		private static void GetTableFromSubquery(QueryDerivedTable subQuery, List<Table> tableList)
		{
			QuerySpecification querySpec = (QuerySpecification)subQuery.QueryExpression;
			SqlParser.GetTableListFromQuerySpec(querySpec, tableList);
			SqlParser.GetWhereConditionsForQuerySpec(querySpec, tableList);
		}
	}
}
