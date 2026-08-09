using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// The game data the pricer reads: merged skill entries, buff tick rates
	/// and the skill tree that sets every level cap.
	/// </summary>
	/// <remarks>
	/// Read as text rather than through MeliaData, because the pricer runs
	/// without a booted server or a database - the whole point of it is that it
	/// can price a skill that has never been measured.
	/// </remarks>
	public static class SfrData
	{
		private static readonly Regex ClassNamePattern = new(@"className: ""([^""]+)""", RegexOptions.Compiled);
		private static readonly Regex FieldPattern = new(@"\b(\w+): (-?[0-9.]+|""[^""]*""|true|false)", RegexOptions.Compiled);
		private static readonly Regex SkillIdNamePattern = new(@"skillId: (\d+), className: ""([^""]+)""", RegexOptions.Compiled);
		private static readonly Regex TreeRowPattern = new(@"skillId: (\d+),.*?unlockLevel: (\d+), maxLevel: (\d+)", RegexOptions.Compiled);
		private static readonly Regex UpdateTimePattern = new(@"\bupdateTime: ([0-9.]+)", RegexOptions.Compiled);
		private static readonly Regex AllowedClassPattern = new(@"""([A-Za-z]+)""", RegexOptions.Compiled);

		private static Dictionary<string, SkillEntryData> _skills;
		private static Dictionary<string, float> _buffUpdateTimes;
		private static Dictionary<string, (int Unlock, int MaxLevel)> _tree;
		private static HashSet<string> _scope;
		private static string _root;

		private static readonly object _syncLock = new();

		/// <summary>
		/// Levels a job gains before it advances a circle, per jobs.conf.
		/// </summary>
		public const int LevelsPerCircle = 15;

		/// <summary>
		/// Skill levels one circle of advancement unlocks.
		/// </summary>
		public const int SkillLevelsPerCircle = 5;

		/// <summary>
		/// The highest circle a job reaches.
		/// </summary>
		public const int MaxCircle = 3;

		/// <summary>
		/// Unlock level and tree cap assumed for a skill with no skilltree row.
		/// </summary>
		public static readonly (int Unlock, int MaxLevel) DefaultTree = (1, 10);

		/// <summary>
		/// The base jobs, which never advance past their first circle.
		/// </summary>
		public static readonly HashSet<string> BaseClasses = ["Swordman", "Archer", "Wizard", "Cleric", "Scout"];

		/// <summary>
		/// Class name to base job, for every class in scope.
		/// </summary>
		public static readonly Dictionary<string, string> BaseJob = new()
		{
			["Swordman"] = "Swordsman", ["Highlander"] = "Swordsman", ["Peltasta"] = "Swordsman",
			["Hoplite"] = "Swordsman", ["Barbarian"] = "Swordsman", ["Cataphract"] = "Swordsman",
			["Rodelero"] = "Swordsman",
			["Wizard"] = "Wizard", ["Pyromancer"] = "Wizard", ["Cryomancer"] = "Wizard",
			["Psychokino"] = "Wizard", ["Chronomancer"] = "Wizard", ["Elementalist"] = "Wizard",
			["Bokor"] = "Wizard",
			["Archer"] = "Archer", ["QuarrelShooter"] = "Archer", ["Ranger"] = "Archer",
			["Sapper"] = "Archer", ["Wugushi"] = "Archer", ["Fletcher"] = "Archer", ["Hunter"] = "Archer",
			["Cleric"] = "Cleric", ["Priest"] = "Cleric", ["Kriwi"] = "Cleric", ["Paladin"] = "Cleric",
			["Dievdirbys"] = "Cleric", ["Sadhu"] = "Cleric", ["Monk"] = "Cleric",
			["Scout"] = "Scout", ["Assassin"] = "Scout", ["OutLaw"] = "Scout", ["Corsair"] = "Scout",
			["Thaumaturge"] = "Scout", ["Linker"] = "Scout", ["Rogue"] = "Scout",
		};

		/// <summary>
		/// The basic attacks the generic yardstick is averaged over.
		/// </summary>
		/// <remarks>
		/// Every basic attack in the data is factor 100, so one swing is 100%
		/// SFR for everyone and only the rate and the geometry differ. Both are
		/// averaged across the roster rather than taken from the caster's own
		/// weapon, which otherwise let weapon speed set a class's whole ceiling.
		/// </remarks>
		public static readonly string[] GenericBasicAttacks =
			["Bow_Attack", "Hammer_Attack", "Magic_Attack", "Normal_Attack"];

		/// <summary>
		/// Environment variable that names the project root outright.
		/// </summary>
		/// <remarks>
		/// A last resort, needed only when the build output sits outside the
		/// project entirely. The walk below finds the root on its own for any
		/// normal run.
		/// </remarks>
		public const string RootVariable = "MELIA_ROOT";

		/// <summary>
		/// The project root, found by walking up until the data folders appear.
		/// </summary>
		/// <remarks>
		/// Both the assembly's folder and the working directory are walked, and
		/// each step also looks for the project as a subfolder, so a run started
		/// from anywhere in or beside the tree resolves.
		/// </remarks>
		public static string Root
		{
			get
			{
				if (_root != null)
					return _root;

				var named = Environment.GetEnvironmentVariable(RootVariable);
				if (!string.IsNullOrEmpty(named) && IsRoot(named))
					return _root = Path.GetFullPath(named);

				foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
				{
					var dir = new DirectoryInfo(start);

					for (var i = 0; i < 12 && dir != null; ++i)
					{
						if (IsRoot(dir.FullName))
							return _root = dir.FullName;

						var nested = Path.Combine(dir.FullName, ProjectFolder);
						if (IsRoot(nested))
							return _root = nested;

						dir = dir.Parent;
					}
				}

				throw new DirectoryNotFoundException(
					$"Failed to find the project root from '{Directory.GetCurrentDirectory()}'. " +
					$"Run from inside the project, or set {RootVariable}.");
			}
		}

		/// <summary>
		/// Folder the project sits in when the run starts beside it rather than
		/// inside it.
		/// </summary>
		private const string ProjectFolder = "melia";

		/// <summary>
		/// Returns whether a folder carries the data the pricer reads.
		/// </summary>
		/// <param name="path"></param>
		private static bool IsRoot(string path)
			=> Directory.Exists(Path.Combine(path, "packages")) && Directory.Exists(Path.Combine(path, "src"));

		/// <summary>
		/// Path to the base skill data.
		/// </summary>
		public static string SkillsPath => Path.Combine(Root, "packages", "laima", "db", "skills.txt");

		/// <summary>
		/// Path to the override file the pricer writes.
		/// </summary>
		public static string OverridesPath => Path.Combine(Root, "packages", "laima", "db", "skills_overrides.txt");

		/// <summary>
		/// Path to the buff data the tick rates come from.
		/// </summary>
		public static string BuffsPath => Path.Combine(Root, "packages", "laima", "db", "buffs.txt");

		/// <summary>
		/// Path to the skill tree the level caps come from.
		/// </summary>
		public static string SkillTreePath => Path.Combine(Root, "packages", "laima", "db", "skilltree.txt");

		/// <summary>
		/// Path to the class list that bounds the pricer's scope.
		/// </summary>
		public static string GemClassesPath => Path.Combine(Root, "packages", "laima", "scripts", "zone", "core", "skill_gem_classes.cs");

		/// <summary>
		/// Path to the measured sweep, read only for the basic-attack rates.
		/// </summary>
		public static string MatrixPath => Path.Combine(Root, "logs", "balance", "skill-matrix.csv");

		private static Dictionary<string, float> _baseFactors;

		/// <summary>
		/// Each skill's factor as the base data carries it, before any
		/// override.
		/// </summary>
		/// <remarks>
		/// This is what says whether a skill is a damage skill at all. A factor
		/// of zero there is the client data's own marker for a buff or a
		/// utility press - Swordman_GungHo, Swordman_Bear, Priest_Blessing -
		/// and there is nothing for this model to price on one.
		///
		/// Read from skills.txt rather than from skills_overrides.txt, because
		/// the pass writes the overrides: reading the marker out of its own
		/// output meant a skill zeroed there could never come back, however
		/// much damage a live press showed it doing. Priest_TurnUndead carries
		/// factor 302 in the base data and 0 in the overrides, and was skipped
		/// for that reason alone. Of the 33 overridden zeroes it is the only
		/// one the base data disagrees with.
		/// </remarks>
		public static IReadOnlyDictionary<string, float> BaseFactors
		{
			get
			{
				lock (_syncLock)
				{
					if (_baseFactors != null)
						return _baseFactors;

					_baseFactors = new Dictionary<string, float>();

					foreach (var line in File.ReadLines(SkillsPath))
					{
						var name = ClassNamePattern.Match(line);
						if (!name.Success)
							continue;

						var factor = Regex.Match(line, @"\bfactor: ([0-9.]+)");

						if (factor.Success)
							_baseFactors[name.Groups[1].Value] = ParseFloat(factor.Groups[1].Value);
					}

					return _baseFactors;
				}
			}
		}

		/// <summary>
		/// Returns whether the base data says this skill deals damage at all.
		/// </summary>
		/// <param name="skillName"></param>
		public static bool DealsDamage(string skillName)
			=> BaseFactors.TryGetValue(skillName, out var factor) && factor > 0;

		/// <summary>
		/// Every skill entry, with the overrides winning field by field.
		/// </summary>
		public static IReadOnlyDictionary<string, SkillEntryData> Skills
		{
			get
			{
				lock (_syncLock)
				{
					if (_skills != null)
						return _skills;

					_skills = new Dictionary<string, SkillEntryData>();

					foreach (var path in new[] { SkillsPath, OverridesPath })
					{
						if (!File.Exists(path))
							continue;

						foreach (var line in File.ReadLines(path))
						{
							var name = ClassNamePattern.Match(line);
							if (!name.Success)
								continue;

							if (!_skills.TryGetValue(name.Groups[1].Value, out var entry))
								_skills[name.Groups[1].Value] = entry = new SkillEntryData(name.Groups[1].Value);

							foreach (Match field in FieldPattern.Matches(line))
								entry.Fields[field.Groups[1].Value] = field.Groups[2].Value.Trim('"');
						}
					}

					return _skills;
				}
			}
		}

		/// <summary>
		/// Returns the tick interval of a damaging buff, in seconds, or zero
		/// when it does not tick.
		/// </summary>
		/// <param name="buffName"></param>
		public static float BuffUpdateTime(string buffName)
		{
			lock (_syncLock)
			{
				if (_buffUpdateTimes == null)
				{
					_buffUpdateTimes = new Dictionary<string, float>();

					if (File.Exists(BuffsPath))
					{
						foreach (var line in File.ReadLines(BuffsPath))
						{
							var name = ClassNamePattern.Match(line);
							var tick = UpdateTimePattern.Match(line);

							if (name.Success && tick.Success)
								_buffUpdateTimes[name.Groups[1].Value] = ParseFloat(tick.Groups[1].Value) / 1000f;
						}
					}
				}
			}

			if (buffName == null)
				return 0f;

			return _buffUpdateTimes.TryGetValue(buffName, out var value) ? value : 0f;
		}

		/// <summary>
		/// Returns the skill tree's unlock level and level cap for a skill.
		/// </summary>
		/// <param name="skillName"></param>
		public static (int Unlock, int MaxLevel) TreeRow(string skillName)
		{
			lock (_syncLock)
			{
				if (_tree == null)
				{
					_tree = new Dictionary<string, (int, int)>();

					var names = new Dictionary<int, string>();

					foreach (var path in new[] { SkillsPath, OverridesPath })
					{
						if (!File.Exists(path))
							continue;

						foreach (var line in File.ReadLines(path))
						{
							var m = SkillIdNamePattern.Match(line);
							if (m.Success)
								names[int.Parse(m.Groups[1].Value)] = m.Groups[2].Value;
						}
					}

					if (File.Exists(SkillTreePath))
					{
						foreach (var line in File.ReadLines(SkillTreePath))
						{
							var m = TreeRowPattern.Match(line);
							if (!m.Success)
								continue;

							if (names.TryGetValue(int.Parse(m.Groups[1].Value), out var name))
								_tree[name] = (int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
						}
					}
				}
			}

			return _tree.TryGetValue(skillName, out var row) ? row : DefaultTree;
		}

		/// <summary>
		/// Returns whether the skill tree carries a row for this skill, since a
		/// skill without one is priced as circle one by fallback.
		/// </summary>
		/// <param name="skillName"></param>
		public static bool HasTreeRow(string skillName)
		{
			TreeRow(skillName);
			return _tree.ContainsKey(skillName);
		}

		/// <summary>
		/// Returns the circle a skill belongs to, from the banding of its
		/// unlock level.
		/// </summary>
		/// <param name="skillName"></param>
		public static int SkillCircle(string skillName)
		{
			var unlock = TreeRow(skillName).Unlock;
			var circle = (unlock + LevelsPerCircle - 1) / LevelsPerCircle;

			return Math.Max(1, Math.Min(MaxCircle, circle));
		}

		/// <summary>
		/// Returns the circle the skill's own job reaches.
		/// </summary>
		/// <param name="skillName"></param>
		public static int JobCircle(string skillName)
			=> BaseClasses.Contains(ClassOf(skillName)) ? 1 : MaxCircle;

		/// <summary>
		/// Returns a skill's level cap on a fully advanced job, mirroring
		/// JobCircleHelper.GetSkillMaxLevel.
		/// </summary>
		/// <remarks>
		/// A skill gets five levels for its own circle and five more for every
		/// circle gained past it, so on a C3 job a C1 skill reaches 15, a C2
		/// skill 10 and a C3 skill 5. Base-job skills sit on a job that never
		/// advances, so they stay at 5.
		/// </remarks>
		/// <param name="skillName"></param>
		public static int SkillMaxLevel(string skillName)
		{
			var dataMax = TreeRow(skillName).MaxLevel;
			if (dataMax <= 1)
				return Math.Max(1, dataMax);

			var unlocked = JobCircle(skillName) - SkillCircle(skillName) + 1;

			return Math.Max(1, unlocked * SkillLevelsPerCircle);
		}

		/// <summary>
		/// Returns what advancing to this skill's circle is worth in SFR.
		/// </summary>
		/// <param name="skillName"></param>
		public static float CirclePremium(string skillName)
			=> SfrDials.CirclePremium.TryGetValue(SkillCircle(skillName), out var v) ? v : 1f;

		/// <summary>
		/// Returns the seconds one press occupies the timeline, and the seconds
		/// between presses, from the skill data alone.
		/// </summary>
		/// <remarks>
		/// The pricer's budget is a whole cycle, so the delivery it divides by
		/// has to be one cycle's worth too. Both sides read this rather than
		/// computing the window themselves, so the two cannot drift.
		/// </remarks>
		/// <param name="entry"></param>
		public static (float Occupancy, float RawOccupancy, float Cycle) PressWindow(SkillEntryData entry)
		{
			var cast = entry.Num("basicCast") / 1000f;
			var shoot = entry.Num("shootTime") / 1000f;

			// Cast time replaces the animation rather than adding to it: a skill
			// that casts is committed for the cast, and its shootTime is the
			// follow-through.
			var raw = Math.Max(SfrDials.MinOccupancy, cast > 0 ? cast : shoot);
			var occupancy = Math.Min(raw, SfrDials.MaxOccupancy);

			return (occupancy, raw, SfrPricer.CycleSeconds(entry, occupancy));
		}

		/// <summary>
		/// Returns the cycle for a named skill, or null when it has no data.
		/// </summary>
		/// <param name="skillName"></param>
		public static float? CycleFor(string skillName)
			=> Skills.TryGetValue(skillName, out var entry) ? PressWindow(entry).Cycle : null;

		/// <summary>
		/// The classes the pricer is allowed to touch, read from the gem class
		/// list so it cannot drift from the scripts.
		/// </summary>
		public static HashSet<string> Scope
		{
			get
			{
				lock (_syncLock)
				{
					if (_scope != null)
						return _scope;

					var text = File.ReadAllText(GemClassesPath);
					var body = text[text.IndexOf("AllowedClasses", StringComparison.Ordinal)..];

					return _scope = AllowedClassPattern.Matches(body)
						.Select(m => m.Groups[1].Value)
						.ToHashSet();
				}
			}
		}

		/// <summary>
		/// Returns the class prefix of a skill's class name.
		/// </summary>
		/// <param name="skillName"></param>
		public static string ClassOf(string skillName)
		{
			var at = skillName.IndexOf('_');
			return at < 0 ? skillName : skillName[..at];
		}

		/// <summary>
		/// Returns the mean basic-attack swing rate across every class the
		/// sweep measured, falling back to the roster mean.
		/// </summary>
		/// <remarks>
		/// Unweighted by class, so a weapon type is not counted once per class
		/// that carries it.
		/// </remarks>
		public static float GenericBasicRate()
		{
			if (!File.Exists(MatrixPath))
				return SfrDials.GenericBasicRate;

			var rates = new Dictionary<string, float>();
			var lines = File.ReadAllLines(MatrixPath);
			if (lines.Length < 2)
				return SfrDials.GenericBasicRate;

			var header = SplitCsv(lines[0].TrimStart('﻿'));
			var classAt = Array.IndexOf(header, "class");
			var rateAt = Array.IndexOf(header, "basicCastsPerSecond");

			if (classAt < 0 || rateAt < 0)
				return SfrDials.GenericBasicRate;

			foreach (var line in lines.Skip(1))
			{
				var cells = SplitCsv(line);
				if (cells.Length <= Math.Max(classAt, rateAt))
					continue;

				if (!float.TryParse(cells[rateAt], NumberStyles.Float, CultureInfo.InvariantCulture, out var rate) || rate <= 0)
					continue;

				if (!rates.ContainsKey(cells[classAt]))
					rates[cells[classAt]] = rate;
			}

			return rates.Count > 0 ? rates.Values.Average() : SfrDials.GenericBasicRate;
		}

		/// <summary>
		/// Splits a CSV line on commas, which is all the sweep's own writer
		/// ever emits.
		/// </summary>
		/// <param name="line"></param>
		private static string[] SplitCsv(string line)
			=> line.Split(',');

		/// <summary>
		/// Parses a number in the invariant culture, since the data files are
		/// culture-independent.
		/// </summary>
		/// <param name="text"></param>
		public static float ParseFloat(string text)
			=> float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// One skill's merged data fields, plus the geometry a handler overrides
	/// them with.
	/// </summary>
	public class SkillEntryData
	{
		/// <summary>
		/// The skill's class name.
		/// </summary>
		public string ClassName { get; }

		/// <summary>
		/// Raw field values, as they appear in the data.
		/// </summary>
		public Dictionary<string, string> Fields { get; } = [];

		/// <summary>
		/// Whether the handler builds its own splash area, which overrides the
		/// single-target rule a Force skill would otherwise take.
		/// </summary>
		public bool HandlerArea { get; set; }

		/// <summary>
		/// Whether the resolved targets pass through LimitBySDR.
		/// </summary>
		public bool UseSdr { get; set; } = true;

		/// <summary>
		/// Hard cap on targets the handler or its pad imposes, if any.
		/// </summary>
		public int? TargetCap { get; set; }

		/// <summary>
		/// Creates a new entry.
		/// </summary>
		/// <param name="className"></param>
		public SkillEntryData(string className)
			=> this.ClassName = className;

		/// <summary>
		/// Returns a numeric field, or the default when it is absent or
		/// unparseable.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="defaultValue"></param>
		public float Num(string key, float defaultValue = 0f)
		{
			if (!this.Fields.TryGetValue(key, out var raw))
				return defaultValue;

			return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : defaultValue;
		}

		/// <summary>
		/// Returns a bool field, absent meaning false, as ReadBool does in
		/// Skills.cs.
		/// </summary>
		/// <param name="key"></param>
		public bool Flag(string key)
			=> this.Fields.TryGetValue(key, out var raw) && raw == "true";

		/// <summary>
		/// Returns a string field, or null when it is absent.
		/// </summary>
		/// <param name="key"></param>
		public string Text(string key)
			=> this.Fields.TryGetValue(key, out var raw) ? raw : null;

		/// <summary>
		/// Returns a copy of this entry, so a handler's geometry overrides can
		/// be layered on without touching the shared data.
		/// </summary>
		public SkillEntryData Clone()
		{
			var copy = new SkillEntryData(this.ClassName)
			{
				HandlerArea = this.HandlerArea,
				UseSdr = this.UseSdr,
				TargetCap = this.TargetCap,
			};

			foreach (var pair in this.Fields)
				copy.Fields[pair.Key] = pair.Value;

			return copy;
		}
	}
}
