using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataGenerator
{
	internal static class ExcelWriter
	{
		public static void Write(DataTable table, string alias, string fileName)
		{
			XLWorkbook workbook = new XLWorkbook();
			string tableName = table.TableName == alias ? alias : string.Format("{0}({1})", table.TableName, alias);
			IXLWorksheet worksheet = workbook.Worksheets.Add(tableName);
			ExcelWriter.WriteTableToWorksheet(table, worksheet);
			workbook.SaveAs(fileName);
		}

		public static void Write(Dictionary<string, DataTable> tables, string fileName)
		{
			XLWorkbook workbook = new XLWorkbook();
			foreach (string key in tables.Keys)
			{
				string tableName = tables[key].TableName == key ? key : string.Format("{0}({1})", tables[key].TableName, key);
				IXLWorksheet worksheet = workbook.Worksheets.Add(tableName);
				ExcelWriter.WriteTableToWorksheet(tables[key], worksheet);
			}

			workbook.SaveAs(fileName);
		}

		private static void WriteTableToWorksheet(DataTable table, IXLWorksheet worksheet)
		{
			// Header
			int colIdx = 1;
			foreach (DataColumn column in table.Columns)
			{
				worksheet.Cell(1, colIdx).Style.NumberFormat.Format = "@";
				worksheet.Cell(1, colIdx++).SetValue(column.ColumnName).SetDataType(XLCellValues.Text);
			}

			// Rows
			int rowIdx = 2;
			foreach (DataRow row in table.Rows)
			{
				colIdx = 1;
				foreach (DataColumn column in table.Columns)
				{
					worksheet.Cell(rowIdx, colIdx++).SetValue(row[column].ToString()).SetDataType(XLCellValues.Text);
				}

				rowIdx++;
			}
		}
	}
}
