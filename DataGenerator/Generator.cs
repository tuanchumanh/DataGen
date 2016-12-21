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
			if (type == typeof(String))
			{
				if (columnName.IndexOf("Date", StringComparison.OrdinalIgnoreCase) != -1 && length == 8)
				{
					return RandomDateString();
				}

				return RandomString(length, columnName);
			}

			if (type == typeof(Decimal))
			{
				return RandomDecimal(numericPrecision, numericScale);
			}

			if (type == typeof(DateTime))
			{
				return RandomDate();
			}

			if (type == typeof(Byte))
			{
				return (byte)random.Next(Byte.MinValue, IntPow(10, length) - 1);
			}

			if (type == typeof(Int16)) 
			{
				return random.Next(Int16.MaxValue);
			}

			if (type == typeof(Int32) || type == typeof(Int64))
			{
				return random.Next(Int32.MaxValue);
			}

			return null;
		}

		public static string RandomString(int length, string columnName)
		{
			if (columnName.Length >= length)
			{
				return RandomString(length);
			}
			else
			{
				int randomStringLength = length - columnName.Length;
				string randomNum = random.Next(IntPow(10, randomStringLength) - 1).ToString().PadLeft(randomStringLength, '0');
				return string.Format("{0}{1}", columnName, RandomString(randomStringLength));
			}
		}

		public static string RandomString(int length)
		{
			const string chars = @"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			return new string(Enumerable.Repeat(chars, length)
					.Select(s => s[random.Next(s.Length)]).ToArray());
		}

		public static DateTime RandomDate()
		{
			return DateTime.UtcNow.AddDays(random.Next(365));
		}

		public static string RandomDateString(string format = "yyyyMMdd")
		{
			return RandomDate().ToString(format);
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

		private static int IntPow(int bas, int exp)
		{
			return Enumerable
				  .Repeat(bas, exp)
				  .Aggregate(1, (a, b) => a * b);
		}
	}
}
