using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Melia.Shared.Game.Const;

namespace Melia.Test.Balance
{
	/// <summary>
	/// How many damage applications a skill's handler performs per cast.
	/// </summary>
	/// <remarks>
	/// The data's multiHitCount is dead - nothing reads it and its values are
	/// not hit counts. The real count lives in the handler, either as an
	/// explicit SkillModifier.MultiHit call or as repeated damage calls, and
	/// the direct-hit model cannot see either. Dagger Slash is the case that
	/// forced this: it runs two separate full-damage loops, so the profiler
	/// priced it at half its real output and reported it as the weaker of the
	/// two Swordsman/Scout fillers when it is nearly twice as strong.
	///
	/// Derived from the handler sources rather than declared, on the same
	/// principle as BuffCatalog, so it cannot drift from the code. Laima is
	/// scanned first because its overrides replace the base handler and the
	/// two can disagree - Shield Bash is MultiHit(4) in Laima and 8 in base.
	/// The counts are approximate: a handler whose damage calls sit in
	/// exclusive branches is over-counted, and one that loops a variable
	/// number of times is under-counted. Every resolved count is exposed so a
	/// wrong one is visible in the report rather than silently priced.
	/// </remarks>
	public static class HandlerHitCounts
	{
		private static readonly string[] HandlerRoots =
		{
			Path.Combine("src", "ZoneServer", "Packages", "Laima", "Skills"),
			Path.Combine("src", "ZoneServer", "Skills", "Handlers"),
		};

		private static readonly Regex HandlerAttribute = new(@"\[SkillHandler\(([^)]+)\)\]", RegexOptions.Compiled);
		private static readonly Regex SkillIdRef = new(@"SkillId\.(\w+)", RegexOptions.Compiled);
		private static readonly Regex MultiHitCall = new(@"MultiHit\(\s*(\d+)\s*\)", RegexOptions.Compiled);
		private static readonly Regex DamageCall = new(@"\bSCR_SkillHit\s*\(", RegexOptions.Compiled);

		private static Dictionary<string, int> _counts;
		private static readonly object _syncLock = new();

		/// <summary>
		/// Returns the handler-derived hit count for the skill, or 1 when the
		/// handler could not be read or performs a single damage application.
		/// </summary>
		/// <param name="skillId"></param>
		public static int Get(SkillId skillId)
		{
			Load();
			return _counts.TryGetValue(skillId.ToString(), out var count) ? count : 1;
		}

		/// <summary>
		/// Returns every skill whose handler applies damage more than once,
		/// so the report can show what the direct-hit model is relying on.
		/// </summary>
		public static IReadOnlyDictionary<string, int> All()
		{
			Load();
			return _counts;
		}

		/// <summary>
		/// Scans the handler sources once.
		/// </summary>
		private static void Load()
		{
			lock (_syncLock)
			{
				if (_counts != null)
					return;

				_counts = new Dictionary<string, int>();

				foreach (var root in HandlerRoots)
				{
					if (!Directory.Exists(root))
						continue;

					foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
						Scan(file);
				}
			}
		}

		/// <summary>
		/// Reads one handler file and records the hit count for every skill
		/// it handles.
		/// </summary>
		/// <param name="file"></param>
		private static void Scan(string file)
		{
			string text;

			try
			{
				text = File.ReadAllText(file);
			}
			catch (IOException)
			{
				return;
			}

			var skills = HandlerAttribute.Matches(text)
				.SelectMany(m => SkillIdRef.Matches(m.Groups[1].Value).Select(s => s.Groups[1].Value))
				.Distinct()
				.ToArray();

			if (skills.Length == 0)
				return;

			// An explicit MultiHit is authoritative; it already multiplies the
			// damage of the call it is attached to, so it is not additive with
			// the call count.
			var multiHit = MultiHitCall.Matches(text)
				.Select(m => int.Parse(m.Groups[1].Value))
				.DefaultIfEmpty(0)
				.Max();

			var damageCalls = DamageCall.Matches(text).Count;
			var count = Math.Max(1, Math.Max(multiHit, damageCalls));

			if (count <= 1)
				return;

			// Laima is scanned first and its overrides replace the base handler,
			// so the first count found for a skill is the one that runs.
			foreach (var skill in skills)
			{
				if (!_counts.ContainsKey(skill))
					_counts[skill] = count;
			}
		}
	}
}
