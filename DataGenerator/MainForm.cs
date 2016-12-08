using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Forms;

namespace DataGenerator
{
	public partial class MainForm : Form
	{
		private Mapping setting = new Mapping ();
		private Dictionary<string, DataTable> data = new Dictionary<string, DataTable> ();
		private readonly static string connectionString = ConfigurationManager.ConnectionStrings["NULDEMOConnectionString"].ToString ();

		public MainForm()
		{
			InitializeComponent ();
			foreach ( Table table in setting.Tables )
			{
				tableNamesComboBox.DataSource = setting.Tables.Select ( t => new { Text = t.Name, Value = t.Alias } ).ToList();
			}
		}

		private void OutputExcel_Click( object sender, EventArgs e )
		{
			if ( BaseExists () )
			{
				data.Add ( setting.Tables[0].Alias, GetData () );
				previewGrid.DataSource = data[setting.Tables[0].Alias];
				tableNamesComboBox.SelectedValue = setting.Tables[0].Alias;
				CheckJoins ();
			}
		}

		private List<Join> CheckJoins()
		{
			using ( SqlConnection sqlConnection1 = new SqlConnection ( connectionString ) )
			using ( SqlCommand cmd = new SqlCommand () )
			{
				StringBuilder queryBuilder = new StringBuilder ();
				queryBuilder.AppendFormat ( " SELECT TOP 1 1 AS {0}_Exists, ", setting.Tables[0].Alias );
				queryBuilder.AppendLine ();

				foreach ( Join joiningTable in
					setting.Tables[0].Joins.GroupBy(j => j.Table2).Select(g => g.First()) )
				{
					queryBuilder.AppendFormat ( @"
(CASE WHEN EXISTS 
	( SELECT TOP 1 1 FROM {0} {1} WHERE ", joiningTable.Table2.Name, joiningTable.Table2.Alias );
					queryBuilder.AppendLine();

					var joins = setting.Tables[0].Joins.Where ( j => j.Table2 == joiningTable.Table2 );

					foreach ( Join join in joins )
					{
						queryBuilder.AppendFormat (
							"\t\t{0}.{1} {2} {3}.{4} ",
							join.Table1.Alias,
							join.Column1,
							Mapping.GetOperatorForQuery ( join.Operator ),
							join.Table2.Alias,
							join.Column2 );

						queryBuilder.AppendLine ( " AND" );
					}

					// Removes AND: "AND\r\n" => "\r\n"
					queryBuilder.Remove ( queryBuilder.Length - 5, 3 );
					queryBuilder.AppendFormat ( @"	) THEN 1 ELSE 0 END) AS {0}_Exists,", joiningTable.Table2.Alias );
					queryBuilder.AppendLine ();
				}

				// Removes trailing comma
				queryBuilder.Remove ( queryBuilder.Length - 3, 1 );
				queryBuilder.AppendFormat ( " FROM {0} {1} ", setting.Tables[0].Name, setting.Tables[0].Alias );

				cmd.CommandText = queryBuilder.ToString ();
				cmd.CommandType = CommandType.Text;
				cmd.Connection = sqlConnection1;

				sqlConnection1.Open ();

				int count = (int) cmd.ExecuteScalar ();
				if ( count > 0 )
				{
					
				}
			}

			return new List<Join> ();
		}

		private DataTable GetData()
		{
			using ( SqlConnection sqlConnection1 = new SqlConnection ( connectionString ) )
			using ( SqlCommand cmd = new SqlCommand () )
			{
				cmd.CommandText = string.Format ( "SELECT TOP 1 * FROM {0}", setting.Tables[0].Name ); ;
				cmd.CommandType = CommandType.Text;
				cmd.Connection = sqlConnection1;

				sqlConnection1.Open ();
				using ( SqlDataReader reader = cmd.ExecuteReader () )
				{
					DataTable table = new DataTable ();
					table.Load ( reader );
					return table;
				}
			}
		}

		private bool BaseExists()
		{
			using ( SqlConnection sqlConnection1 = new SqlConnection ( connectionString ) )
			using ( SqlCommand cmd = new SqlCommand () )
			{
				cmd.CommandText = string.Format("SELECT TOP 1 1 FROM {0}", setting.Tables[0].Name);
				cmd.CommandType = CommandType.Text;
				cmd.Connection = sqlConnection1;

				sqlConnection1.Open ();

				int count = (int)cmd.ExecuteScalar ();
				if ( count > 0 )
				{
					return true;
				}
			}

			return false;
		}

		private static bool DataExistsFor(string query)
		{
			using ( SqlConnection sqlConnection1 = new SqlConnection ( connectionString ) )
			using ( SqlCommand cmd = new SqlCommand () )
			{
				cmd.CommandText = query;
				cmd.CommandType = CommandType.Text;
				cmd.Connection = sqlConnection1;

				sqlConnection1.Open ();

				int count = (int) cmd.ExecuteScalar ();
				if ( count > 0 )
				{
					return true;
				}
			}

			return false;
		}

		private static void CreateDummyData(Join join)
		{

		}
	}
}
