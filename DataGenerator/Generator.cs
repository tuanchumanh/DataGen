using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataGenerator
{
	internal static class Generator
	{
		private static Random random = new Random();

		public static object GenerateDummyData(string columnName, Type type, int length, short numericPrecision, short numericScale)
		{
			if (type == typeof(System.String))
			{
				if (columnName.IndexOf("Date", StringComparison.OrdinalIgnoreCase) != -1 && length == 8)
				{
					return RandomDate();
				}

				return RandomString(length);
			}

			if (type == typeof(System.Decimal))
			{
				return RandomDecimal(numericPrecision, numericScale);
			}

			if (type == typeof(System.DateTime))
			{
				return RandomDate();
			}

			return null;
		}

		public static string RandomString(int length)
		{
			const string chars = @"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			return new string(Enumerable.Repeat(chars, length)
				.Select(s => s[random.Next(s.Length)]).ToArray());
		}

		public static string RandomDate(string format = "yyyyMMdd")
		{
			return DateTime.UtcNow.AddDays(random.Next(365)).ToString(format);
		}

		public static decimal RandomDecimal(short precision, short scale)
		{
			Decimal d = 0m;
			for (int i = 0; i < precision; i++)
			{
				int r = random.Next(0, 10);
				d = d * 10m + r;
			}

			for (int s = 0; s < scale; s++)
			{
				d /= 10m;
			}

			return d;
		}

	}
}
