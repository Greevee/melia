using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Writes sweep results to CSV so a run can be diffed against the
	/// previous one and the blast radius of a change is visible.
	/// </summary>
	public static class SweepReport
	{
		/// <summary>
		/// Directory reports are written to, relative to the repo root
		/// RunHeadless navigates to.
		/// </summary>
		public const string OutputDirectory = "logs/balance";

		/// <summary>
		/// Writes rows to the named report and returns its full path.
		/// </summary>
		/// <param name="name"></param>
		/// <param name="header"></param>
		/// <param name="rows"></param>
		public static string Write(string name, string header, IEnumerable<string> rows)
		{
			Directory.CreateDirectory(OutputDirectory);

			var path = Path.Combine(OutputDirectory, name + ".csv");
			var builder = new StringBuilder();

			builder.AppendLine(header);

			foreach (var row in rows)
				builder.AppendLine(row);

			File.WriteAllText(path, builder.ToString(), Encoding.UTF8);

			return Path.GetFullPath(path);
		}

		/// <summary>
		/// Reads a report back as one dictionary per row, so the human-readable
		/// report can be regenerated from a previous sweep instead of costing
		/// another twenty minutes.
		/// </summary>
		/// <param name="name"></param>
		public static List<Dictionary<string, string>> Read(string name)
		{
			var path = Path.Combine(OutputDirectory, name + ".csv");
			var rows = new List<Dictionary<string, string>>();

			if (!File.Exists(path))
				return rows;

			var lines = File.ReadAllLines(path);

			if (lines.Length < 2)
				return rows;

			var header = Split(lines[0].TrimStart('﻿'));

			foreach (var line in lines.Skip(1))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				var values = Split(line);
				var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

				for (var i = 0; i < header.Length && i < values.Length; ++i)
					row[header[i]] = values[i];

				rows.Add(row);
			}

			return rows;
		}

		/// <summary>
		/// Splits one CSV line, honouring the quoting Format applies.
		/// </summary>
		/// <param name="line"></param>
		private static string[] Split(string line)
		{
			var values = new List<string>();
			var current = new StringBuilder();
			var quoted = false;

			for (var i = 0; i < line.Length; ++i)
			{
				var c = line[i];

				if (quoted)
				{
					if (c != '"')
						current.Append(c);
					else if (i + 1 < line.Length && line[i + 1] == '"')
						current.Append(line[++i]);
					else
						quoted = false;
				}
				else if (c == '"')
					quoted = true;
				else if (c == ',')
				{
					values.Add(current.ToString());
					current.Clear();
				}
				else
					current.Append(c);
			}

			values.Add(current.ToString());

			return values.ToArray();
		}

		/// <summary>
		/// Joins values into a CSV row, quoting anything that needs it.
		/// </summary>
		/// <param name="values"></param>
		public static string Row(params object[] values)
			=> string.Join(",", values.Select(Format));

		private static string Format(object value)
		{
			var text = value switch
			{
				null => "",
				float f => f.ToString("F3", CultureInfo.InvariantCulture),
				double d => d.ToString("F3", CultureInfo.InvariantCulture),
				_ => value.ToString(),
			};

			if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
				return "\"" + text.Replace("\"", "\"\"") + "\"";

			return text;
		}

		/// <summary>
		/// Returns the median of the values, which is the reference the
		/// outlier checks are taken against.
		/// </summary>
		/// <param name="values"></param>
		public static float Median(IEnumerable<float> values)
		{
			var sorted = values.Where(v => v > 0).OrderBy(v => v).ToArray();

			if (sorted.Length == 0)
				return 0;

			return sorted[sorted.Length / 2];
		}
	}
}
