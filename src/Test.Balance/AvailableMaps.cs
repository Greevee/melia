using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Melia.Test.Balance
{
	/// <summary>
	/// The maps players can normally reach. Scenario mobs may only be drawn
	/// from these, so balance is measured against content that exists.
	/// </summary>
	public static class AvailableMaps
	{
		/// <summary>
		/// Path to the list, relative to the repo root RunHeadless navigates
		/// to before anything else.
		/// </summary>
		public const string ListPath = "doc/packages/laima/available_maps.md";

		private static readonly object _loadLock = new();
		private static HashSet<string> _names;

		/// <summary>
		/// Returns every available map's class name.
		/// </summary>
		public static IReadOnlyCollection<string> Names => Load();

		/// <summary>
		/// Returns true if the given map is reachable by players.
		/// </summary>
		/// <param name="mapClassName"></param>
		public static bool Contains(string mapClassName)
			=> mapClassName != null && Load().Contains(mapClassName);

		/// <summary>
		/// Reads and caches the list. Map names are the only unindented
		/// single-word lines in the file; everything else is prose or a rule.
		/// </summary>
		private static HashSet<string> Load()
		{
			lock (_loadLock)
			{
				if (_names != null)
					return _names;

				if (!File.Exists(ListPath))
					throw new FileNotFoundException($"Available map list not found at '{Path.GetFullPath(ListPath)}'.", ListPath);

				var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

				foreach (var rawLine in File.ReadAllLines(ListPath))
				{
					var line = rawLine.Trim();

					if (line.Length == 0)
						continue;

					if (line.Any(char.IsWhiteSpace))
						continue;

					if (line.All(c => c == '=' || c == '-'))
						continue;

					names.Add(line);
				}

				if (names.Count == 0)
					throw new InvalidOperationException($"'{ListPath}' contained no map names.");

				_names = names;

				return _names;
			}
		}
	}
}
