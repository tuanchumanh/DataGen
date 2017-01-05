
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
		public IEnumerable<TableInfo> TableSettings
		{
			get
			{
				return this.settings.AsReadOnly();
			}
		}

		public IList<ParseError> Errors { get; private set; }

		private List<TableInfo> settings = new List<TableInfo>();

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

			List<TableInfo> tableList = new List<TableInfo>();
			foreach (TSqlStatement statement in script.Batches.SelectMany(batch => batch.Statements))
			{
				if (statement is SelectStatement)
				{
					SqlParser.GetTableListForSelectStatement((SelectStatement)statement, tableList);
				}
				else if (statement is ProcedureStatementBodyBase)
				{
					ProcedureStatementBodyBase procedureStatement = (ProcedureStatementBodyBase)statement;
					foreach (TSqlStatement procStatement in procedureStatement.StatementList.Statements)
					{
						if (procStatement is SelectStatement)
						{
							SqlParser.GetTableListForSelectStatement((SelectStatement)procStatement, tableList);
						}
					}
				}
				else if (statement is UseStatement == false && statement is PredicateSetStatement == false)
				{
					ParseError error = new ParseError(0, statement.StartColumn, statement.StartOffset, statement.StartLine, "Not a select statement.");
					errors.Add(error);
				}
			}

			// Duplicate alias
			foreach (TableInfo table in tableList)
			{
				if (tableList.Where(tbl => tbl != table).Any(tbl => tbl.Alias == table.Alias))
				{
					ParseError error = new ParseError(0, 0, 0, 0, string.Format("Duplicate alias: {0} {1}", table.Alias, table.Name));
					errors.Add(error);
				}
			}

			this.settings = tableList;
			this.Errors = errors;
		}

		private static void GetTableListForSelectStatement(SelectStatement selectQuery, List<TableInfo> tableList)
		{
			SqlParser.GetTableListFromQueryExpression(selectQuery.QueryExpression, tableList);
		}

		private static void GetTableListFromQueryExpression(QueryExpression expression, List<TableInfo> tableList)
		{
			if (expression is QuerySpecification)
			{
				QuerySpecification querySpec = (QuerySpecification)expression;

				// Table list
				SqlParser.GetTableListFromQuerySpec(querySpec, tableList, string.Empty);
				SqlParser.GetWhereConditionsForQuerySpec(querySpec, tableList);
			}
			else if (expression is BinaryQueryExpression)
			{
				BinaryQueryExpression binaryExpression = (BinaryQueryExpression)expression;
				// TODO: UNION EXCEPT Support?
				// SqlParser.GetTableListFromQueryExpression(binaryExpression.FirstQueryExpression, tableList);
				SqlParser.GetTableListFromQueryExpression(binaryExpression.SecondQueryExpression, tableList);
			}
			else if (expression is QueryParenthesisExpression)
			{
				QueryParenthesisExpression parenthesisExpression = (QueryParenthesisExpression)expression;
				SqlParser.GetTableListFromQueryExpression(parenthesisExpression.QueryExpression, tableList);
			}
		}

		private static void GetTableListFromQuerySpec(QuerySpecification querySpec, List<TableInfo> tableList, string subqueryAlias)
		{
			if (querySpec.FromClause == null)
			{
				return;
			}

			TableReference tableRef = querySpec.FromClause.TableReferences[0];

			if (tableRef is NamedTableReference)
			{
				// Truong hop khong co join
				NamedTableReference namedTableRef = (NamedTableReference)tableRef;

				string alias = namedTableRef.Alias == null ? namedTableRef.SchemaObject.BaseIdentifier.Value : namedTableRef.Alias.Value;
				tableList.Add(new TableInfo()
				{
					Alias = alias,
					Name = namedTableRef.SchemaObject.BaseIdentifier.Value,
					Conditions = new List<Condition>(),
					Joins = new List<Join>(),
					SubqueryAlias = subqueryAlias,
				});
			}

			// Truong hop co join, lay ra thong tin join cua cac table
			if (tableRef is QualifiedJoin)
			{
				QualifiedJoin join = (QualifiedJoin)tableRef;
				SqlParser.GetTable(join, tableList, subqueryAlias);

				// Lay ra chi tiet noi dung join
				SqlParser.GetJoins(join, tableList);
			}
		}

		private static void GetWhereConditionsForQuerySpec(QuerySpecification querySpec, List<TableInfo> tableList)
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

		private static void GetJoins(QualifiedJoin joinExpression, List<TableInfo> tableList)
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

			// TODO Subquery join conditions
			SqlParser.AddJoinsAndConditionsToTableList(expression, tableList);
		}

		private static void GetJoinConditions(BooleanBinaryExpression expression, List<TableInfo> tableList)
		{
			// Dieu kien ghep => recursive de lay ra het
			if (expression.FirstExpression is BooleanBinaryExpression)
			{
				SqlParser.GetJoinConditions((BooleanBinaryExpression)expression.FirstExpression, tableList);
			}

			SqlParser.AddJoinsAndConditionsToTableList(expression.FirstExpression, tableList);
			if (expression.BinaryExpressionType == BooleanBinaryExpressionType.And)
			{
				// EXPERIMENTAL: Neu la dieu kien OR chi lay ra 1 ben
				SqlParser.AddJoinsAndConditionsToTableList(expression.SecondExpression, tableList);
			}
		}

		private static void AddJoinsAndConditionsToTableList(BooleanExpression expression, List<TableInfo> tableList)
		{
			// Dieu kien so sanh
			if (expression is BooleanComparisonExpression)
			{
				SqlParser.AddComparisonCondition((BooleanComparisonExpression)expression, tableList);
			}

			// Dieu kien BETWEEN
			if (expression is BooleanTernaryExpression)
			{
				SqlParser.AddBetweenCondition((BooleanTernaryExpression)expression, tableList);
			}

			if (expression is InPredicate)
			{
				// TODO: IN Subquery
				SqlParser.AddInPredicateCondition((InPredicate)expression, tableList);
			}
		}

		private static void AddInPredicateCondition(InPredicate inPredicate, List<TableInfo> tableList)
		{
			ColumnReferenceExpression colRef = (ColumnReferenceExpression)inPredicate.Expression;

			// Lay ra table cua dieu kien
			string colName = colRef.MultiPartIdentifier.Identifiers[colRef.MultiPartIdentifier.Identifiers.Count - 1].Value;
			TableInfo targetTable = SqlParser.GetTableHavingColumn(colName, colRef, tableList);

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
				List<TableInfo> subQueryTableList = new List<TableInfo>();
				SqlParser.GetTableListFromQuerySpec(querySpec, subQueryTableList, string.Empty);
				SqlParser.GetWhereConditionsForQuerySpec(querySpec, subQueryTableList);

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
							TableInfo subQueryTargetTable = SqlParser.GetTableHavingColumn(subQueryColName, subColRef, subQueryTableList);
							if (subQueryTargetTable != null)
							{
								// Tim thay table => IN dang su dung doi voi subquery => coi dieu kien IN nhu la 1 dieu kien join
								foreach (TableInfo subQueryTable in subQueryTableList)
								{
									tableList.Add(subQueryTable);
								}

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

		private static void AddBetweenCondition(BooleanTernaryExpression expression, List<TableInfo> tableList)
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
				TableInfo targetTable = SqlParser.GetTableHavingColumn(colName, joinLeft, tableList);

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

		private static void AddComparisonCondition(BooleanComparisonExpression compareExpression, List<TableInfo> tableList)
		{
			if (compareExpression.FirstExpression is ColumnReferenceExpression
				&& compareExpression.SecondExpression is ColumnReferenceExpression)
			{
				// Ca 2 deu la reference => Dieu kien join
				ColumnReferenceExpression join1 = (ColumnReferenceExpression)compareExpression.FirstExpression;
				ColumnReferenceExpression join2 = (ColumnReferenceExpression)compareExpression.SecondExpression;

				string table1Alias = join1.MultiPartIdentifier.Identifiers[0].Value;
				string table2Alias = join2.MultiPartIdentifier.Identifiers[0].Value;

				string columnName1 = join1.MultiPartIdentifier.Identifiers[1].Value;
				string columnName2 = join2.MultiPartIdentifier.Identifiers[1].Value;

				TableInfo table1 = tableList.FirstOrDefault(tbl => tbl.Alias == table1Alias);
				TableInfo table2 = tableList.FirstOrDefault(tbl => tbl.Alias == table2Alias);

				if (table1 != null && table2 != null)
				{
					Join join = new Join()
					{
						Table1 = table1,
						Column1 = columnName1,
						Table2 = table2,
						Column2 = columnName2,
						Operator = SqlParser.GetOperator(compareExpression.ComparisonType),
					};

					table1.Joins.Add(join);
				}
				else if (table1 != null)
				{
					// Khong tim thay bang => Subquery
					// Lay ra danh sach bang trong subquery
					List<TableInfo> subQueryTableList = tableList
						.Where(tbl => tbl.SubqueryAlias.Contains(table2Alias))
						.ToList();

					TableInfo targetTable = SqlParser.GetTableHavingColumn(columnName2, subQueryTableList);
					if (targetTable != null)
					{
						Join join = new Join()
						{
							Table1 = table1,
							Column1 = columnName1,
							Table2 = targetTable,
							Column2 = columnName2,
							Operator = SqlParser.GetOperator(compareExpression.ComparisonType),
						};

						table1.Joins.Add(join);
					}
				}
				else if (table2 != null)
				{
					List<TableInfo> subQueryTableList = tableList
						.Where(tbl => tbl.SubqueryAlias.Contains(table1Alias))
						.ToList();

					TableInfo targetTable = SqlParser.GetTableHavingColumn(columnName1, subQueryTableList);
					if (targetTable != null)
					{
						Join join = new Join()
						{
							Table1 = table2,
							Column1 = columnName2,
							Table2 = targetTable,
							Column2 = columnName1,
							Operator = SqlParser.GetOperator(compareExpression.ComparisonType),
						};

						table2.Joins.Add(join);
					}
				}
				else
				{
					// Ca 2 ve deu la subquery
					List<TableInfo> subQueryTableList = tableList
						.Where(tbl => tbl.SubqueryAlias.Contains(table1Alias) || tbl.SubqueryAlias.Contains(table2Alias))
						.ToList();

					TableInfo targetTable1 = SqlParser.GetTableHavingColumn(columnName1, subQueryTableList);
					TableInfo targetTable2 = SqlParser.GetTableHavingColumn(columnName2, subQueryTableList);
					if (targetTable1 != null && targetTable2 != null)
					{

						Join join = new Join()
						{
							Table1 = targetTable1,
							Column1 = columnName1,
							Table2 = targetTable2,
							Column2 = columnName2,
							Operator = SqlParser.GetOperator(compareExpression.ComparisonType),
						};

						targetTable1.Joins.Add(join);
					}
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
				TableInfo targetTable = SqlParser.GetTableHavingColumn(colName, joinLeft, tableList);

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
		/// <param name="colRef"></param>
		/// <param name="tableList"></param>
		/// <returns></returns>
		private static TableInfo GetTableHavingColumn(string colName, ColumnReferenceExpression colRef, List<TableInfo> tableList)
		{
			TableInfo targetTable = null;
			if (colRef.MultiPartIdentifier.Count == 2)
			{
				targetTable = tableList.FirstOrDefault(tbl => tbl.Alias == colRef.MultiPartIdentifier.Identifiers[0].Value);
			}
			else if (colRef.MultiPartIdentifier.Count == 1)
			{
				if (tableList.Count == 1)
				{
					targetTable = tableList[0];
				}
				else if (tableList.Count > 1)
				{
					targetTable = SqlParser.GetTableHavingColumn(colName, tableList);
				}
			}

			return targetTable;
		}

		private static TableInfo GetTableHavingColumn(string colName, List<TableInfo> tableList)
		{
			TableInfo targetTable = null;
			foreach (TableInfo table in tableList)
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
		private static void GetTable(QualifiedJoin join, List<TableInfo> tableList, string queryAlias)
		{
			if (join.FirstTableReference is QualifiedJoin)
			{
				SqlParser.GetTable((QualifiedJoin)join.FirstTableReference, tableList, queryAlias);
			}

			if (join.FirstTableReference is NamedTableReference)
			{
				NamedTableReference namedTableRef = (NamedTableReference)join.FirstTableReference;
				string alias = namedTableRef.Alias == null ? namedTableRef.SchemaObject.BaseIdentifier.Value : namedTableRef.Alias.Value;
				tableList.Add(new TableInfo()
				{
					Alias = alias,
					Name = namedTableRef.SchemaObject.BaseIdentifier.Value,
					Joins = new List<Join>(),
					Conditions = new List<Condition>(),
					SubqueryAlias = queryAlias,
				});
			}

			if (join.SecondTableReference is NamedTableReference)
			{
				NamedTableReference namedTableRef2 = (NamedTableReference)join.SecondTableReference;
				string alias = namedTableRef2.Alias == null ? namedTableRef2.SchemaObject.BaseIdentifier.Value : namedTableRef2.Alias.Value;
				tableList.Add(new TableInfo()
				{
					Alias = alias,
					Name = namedTableRef2.SchemaObject.BaseIdentifier.Value,
					Joins = new List<Join>(),
					Conditions = new List<Condition>(),
					SubqueryAlias = queryAlias,
				});
			}

			// TODO: Subquery
			if (join.FirstTableReference is QueryDerivedTable)
			{
				SqlParser.GetTableFromSubquery((QueryDerivedTable)join.FirstTableReference, tableList, queryAlias);
			}

			if (join.SecondTableReference is QueryDerivedTable)
			{
				SqlParser.GetTableFromSubquery((QueryDerivedTable)join.SecondTableReference, tableList, queryAlias);
			}
		}

		private static void GetTableFromSubquery(QueryDerivedTable subQuery, List<TableInfo> tableList, string queryAlias)
		{
			// Alias;#ParentAlias;#GrandparentAlias
			string subqueryAlias = string.Format("{0};#{1}", subQuery.Alias.Value, queryAlias);
			QuerySpecification querySpec = (QuerySpecification)subQuery.QueryExpression;
			SqlParser.GetTableListFromQuerySpec(querySpec, tableList, subqueryAlias);
			SqlParser.GetWhereConditionsForQuerySpec(querySpec, tableList);
		}
	}
}
