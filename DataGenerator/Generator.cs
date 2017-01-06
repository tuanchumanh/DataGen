using System;
using System.Collections.Concurrent;
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
		private static ConcurrentDictionary<string, long> numberDict = new ConcurrentDictionary<string, long>();

		public static object GenerateDummyData(string columnName, Type type, int length, short numericPrecision, short numericScale)
		{
			if (type == typeof(String))
			{
				if (columnName.IndexOf("Date", StringComparison.OrdinalIgnoreCase) != -1 && length == 8)
				{
					return RandomDateString();
				}

				if (columnName.IndexOf("Time", StringComparison.OrdinalIgnoreCase) != -1 && length == 4)
				{
					return RandomDateString("HHmm");
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
			if (length <= 2)
			{
				return RandomString(length);
			}

			columnName = GetAbbreviatedName(columnName);
			if (columnName.Length >= length + 1)
			{
				// return RandomString(length);
				return columnName[0] + numberDict.AddOrUpdate(columnName, 1, (key, oldValue) => oldValue + 1).ToString().PadLeft(length - 1, '0');
			}
			else
			{
				int randomStringLength = length - columnName.Length;
				//string randomNum = random.Next(IntPow(10, randomStringLength) - 1).ToString().PadLeft(randomStringLength, '0');
				// return string.Format("{0}{1}", columnName, RandomString(randomStringLength));
				return columnName + numberDict.AddOrUpdate(columnName, 1, (key, oldValue) => oldValue + 1).ToString().PadLeft(randomStringLength, '0');
			}
		}

		public static string RandomString(int length)
		{
			const string chars = @"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
			return new string(Enumerable.Repeat(chars, length)
					.Select(s => s[random.Next(s.Length)]).ToArray());
		}

		public static DateTime RandomDate(int numberOfDays = 365)
		{
			return DateTime.UtcNow.AddDays(random.Next(numberOfDays));
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

		private static string GetAbbreviatedName(string name)
		{
			name = name.Replace("C_", string.Empty);
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < name.Length; i++)
			{
				if (char.IsUpper(name[i]))
				{
					result.Append(name[i]);
				}
			}

			return result.ToString();
		}
	}
}
