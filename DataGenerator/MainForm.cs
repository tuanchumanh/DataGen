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
using System.Windows.Forms;

namespace DataGenerator
{
	public partial class MainForm : Form
	{
		private Mapping setting = new Mapping ();
		private string connectionString = ConfigurationManager.ConnectionStrings["NULDEMOConnectionString"].ToString ();

		public MainForm()
		{
			InitializeComponent ();
			foreach ( DataGenerator.Mapping.Table table in setting.Tables )
			{
				tableNamesComboBox.DataSource = setting.Tables.Select ( t => new { Text = t.Name, Value = t.Alias } ).ToList();
			}
		}

		private void OutputExcel_Click( object sender, EventArgs e )
		{
			if ( BaseExists () )
			{
				previewGrid.DataSource = GetData ();
				tableNamesComboBox.SelectedValue = setting.Tables[0].Alias;
			}
		}

		private void CheckJoins()
		{

		}

		private DataTable GetData()
		{
			using ( SqlConnection sqlConnection1 = new SqlConnection ( connectionString ) )
			using (SqlCommand cmd = new SqlCommand ())
			{
				cmd.CommandText = string.Format ( "SELECT TOP 1000 * FROM {0}", setting.Tables[0].Name ); ;
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

		private string GetQuery()
		{
			StringBuilder builder = new StringBuilder ();
			return "";
		}
	}
}
