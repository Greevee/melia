using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Melia.Shared.Game.Const;
using Melia.Test.Balance.Sfr;

namespace Melia.Test.Balance.Buff
{
	/// <summary>
	/// One buff-granting press the pass can price, with everything the model
	/// needs about it.
	/// </summary>
	public class BuffSubject
	{
		/// <summary>
		/// Class name of the skill that grants the buff, which is the row the
		/// pass reads and writes.
		/// </summary>
		public string SkillClassName { get; init; }

		public SkillId SkillId { get; init; }

		/// <summary>
		/// The class the skill belongs to, for the per-class budget.
		/// </summary>
		public string ClassName { get; init; }

		/// <summary>
		/// Buffs the skill's handler can apply, in source order.
		/// </summary>
		public BuffId[] Buffs { get; init; }

		/// <summary>
		/// The caption ratio slots the row declares, and the magnitude each
		/// holds at the skill's own cap.
		/// </summary>
		/// <remarks>
		/// At the cap rather than at the probe's buff level, because the cap is
		/// what the pass prices and what it writes. Reading the row anywhere
		/// else makes the seed and the written value two different quantities,
		/// and a written row then re-seeds at cap/ProbeBuffLevel of what it was
		/// priced at - which is the whole of why a second pass over a written
		/// file used to solve most of the roster back to a scale of three.
		/// </remarks>
		public IReadOnlyDictionary<int, float> Slots { get; init; }

		/// <summary>
		/// Whether the press puts its buff on the caster's party rather than
		/// only on the caster.
		/// </summary>
		public bool IsPartyWide { get; init; }

		/// <summary>
		/// The skill's level cap on a fully advanced job: 5 for a base job,
		/// and 15 / 10 / 5 by circle for an advanced one.
		/// </summary>
		public int MaxLevel { get; init; }

		/// <summary>
		/// Seconds the buff lasts, or zero when it is a toggle that never
		/// expires on its own.
		/// </summary>
		public float DurationSeconds { get; init; }

		/// <summary>
		/// Seconds between presses, from the skill's own cooldown and
		/// overheat.
		/// </summary>
		public float CycleSeconds { get; init; }

		/// <summary>
		/// The share of the rotation the buff is actually up for.
		/// </summary>
		/// <remarks>
		/// A toggle and a buff that outlasts its own cooldown are both simply
		/// permanent, which is the case the price has to be hardest on.
		/// </remarks>
		public float Uptime
			=> this.DurationSeconds <= 0 || this.CycleSeconds <= 0
				? 1f
				: Math.Min(1f, this.DurationSeconds / this.CycleSeconds);

		public bool IsAnchor => this.SkillClassName == BuffDials.AnchorSkill;

		public override string ToString()
			=> $"{this.SkillClassName} [{string.Join(", ", this.Buffs)}] " +
			   $"slots {string.Join("/", this.Slots.OrderBy(s => s.Key).Select(s => $"{s.Key}:{s.Value:0.##}"))} " +
			   $"cap {this.MaxLevel} uptime {this.Uptime:0.00}";
	}

	/// <summary>
	/// Which buff-granting presses are in scope, and what the model knows
	/// about each without measuring anything.
	/// </summary>
	/// <remarks>
	/// Scope is "the row declares at least one caption ratio". That is the
	/// declaration a handler makes when it is converted to read its magnitudes
	/// from data, so scope grows exactly as the conversion does and nothing
	/// needs a second list to be kept in step. A handler still carrying its
	/// constants is invisible here rather than mispriced.
	/// </remarks>
	public static class BuffScope
	{
		/// <summary>
		/// Roots holding the skill handlers whose sources name the buffs they
		/// apply.
		/// </summary>
		private static readonly string[] SkillHandlerRoots =
		[
			"src/ZoneServer/Skills/Handlers",
			"src/ZoneServer/Packages/Laima/Skills",
		];

		private static readonly Regex SkillHandlerAttribute = new(@"\[SkillHandler\(SkillId\.(\w+)\)\]", RegexOptions.Compiled);

		/// <summary>
		/// A buff the handler actually applies, rather than one it merely
		/// names.
		/// </summary>
		/// <remarks>
		/// Matching a bare BuffId reference made every handler that checks for
		/// a buff, stops one or excludes one look like its owner.
		/// SwashBuckling_Debuff read as "granted by six Peltasta skills" and was
		/// filed as unpriceable on that basis, when only Peltasta_SwashBuckling
		/// starts it and the other five are the base handlers it overrides.
		/// </remarks>
		private static readonly Regex BuffApplication = new(@"StartBuff\w*\(\s*BuffId\.(\w+)", RegexOptions.Compiled);

		/// <summary>
		/// A press that puts its buff on the caster's party rather than only on
		/// the caster.
		/// </summary>
		/// <remarks>
		/// Scanned rather than measured, and it is the one thing here that has
		/// to be: the harness has no second connected character, so
		/// Connection.Party is null and a party press applies to the caster
		/// alone however it is dispatched. Without this a party buff and a self
		/// buff are indistinguishable, and Priest_Blessing prices as though it
		/// buffed one person.
		/// </remarks>
		private static readonly Regex PartyApplication = new(@"member\.StartBuff|foreach\s*\(\s*var\s+member", RegexOptions.Compiled);

		private static readonly object _syncLock = new();
		private static BuffSubject[] _subjects;
		private static Dictionary<string, BuffId[]> _grants;
		private static HashSet<string> _partyWide;

		/// <summary>
		/// Every buff-granting press the pass can price.
		/// </summary>
		public static BuffSubject[] Subjects
		{
			get
			{
				lock (_syncLock)
					return _subjects ??= Discover();
			}
		}

		/// <summary>
		/// Returns the subject for one skill, or null when it declares no
		/// caption ratios.
		/// </summary>
		/// <param name="skillClassName"></param>
		/// <summary>
		/// Narrows a subject list to what a comma-separated filter names, or
		/// returns it whole when the filter is empty.
		/// </summary>
		/// <remarks>
		/// A list rather than a single name, so fixing one skill does not mean
		/// re-pricing the roster and a related handful can be checked together.
		/// The anchor is measured regardless of what is named here - it sets
		/// every other buff's budget, so a filtered run is still calibrated the
		/// same way a full one is.
		/// </remarks>
		/// <param name="subjects"></param>
		/// <param name="only"></param>
		public static BuffSubject[] Filter(BuffSubject[] subjects, string only)
		{
			if (string.IsNullOrWhiteSpace(only))
				return subjects;

			var names = only
				.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToHashSet(StringComparer.OrdinalIgnoreCase);

			var filtered = subjects.Where(s => names.Contains(s.SkillClassName)).ToArray();

			if (filtered.Length == 0)
				throw new ArgumentException($"No buff in scope matches '{only}'.", nameof(only));

			return filtered;
		}

		public static BuffSubject Find(string skillClassName)
			=> Subjects.FirstOrDefault(s => s.SkillClassName.Equals(skillClassName, StringComparison.OrdinalIgnoreCase));

		/// <summary>
		/// Builds the scope from the skill data and the handler sources.
		/// </summary>
		private static BuffSubject[] Discover()
		{
			var grants = Grants();
			var subjects = new List<BuffSubject>();

			foreach (var (skillName, entry) in SfrData.Skills)
			{
				var maxLevel = SfrData.SkillMaxLevel(skillName);
				var slots = new Dictionary<int, float>();

				for (var slot = 1; slot <= 3; ++slot)
				{
					var baseValue = entry.Num($"captionRatio{slot}", 0);
					var byLevel = entry.Num($"captionRatio{slot}ByLevel", 0);

					if (baseValue == 0 && byLevel == 0)
						continue;

					slots[slot] = baseValue + byLevel * maxLevel;
				}

				if (slots.Count == 0)
					continue;

				if (!Enum.TryParse<SkillId>(skillName, out var skillId))
					continue;

				subjects.Add(new BuffSubject
				{
					SkillClassName = skillName,
					SkillId = skillId,
					ClassName = SfrData.ClassOf(skillName),
					Buffs = grants.GetValueOrDefault(skillName, []),
					IsPartyWide = PartyWide().Contains(skillName),
					Slots = slots,
					MaxLevel = maxLevel,
					DurationSeconds = entry.Num("captionTime", 0) + entry.Num("captionTimeByLevel", 0) * maxLevel,
					CycleSeconds = CycleSeconds(entry),
				});
			}

			return subjects.OrderBy(s => s.SkillClassName, StringComparer.Ordinal).ToArray();
		}

		/// <summary>
		/// Returns the seconds between presses, from the skill's own cooldown
		/// and overheat charges.
		/// </summary>
		/// <param name="entry"></param>
		private static float CycleSeconds(SkillEntryData entry)
		{
			var cooldown = entry.Num("cooldownTime", 0) / 1000f;
			var charges = Math.Max(1f, entry.Num("overheatCount", 0));

			return cooldown / charges;
		}

		/// <summary>
		/// Returns the skills whose press puts its buff on the whole party.
		/// </summary>
		private static HashSet<string> PartyWide()
		{
			lock (_syncLock)
			{
				if (_partyWide != null)
					return _partyWide;

				_partyWide = [];

				foreach (var root in SkillHandlerRoots)
				{
					var path = Path.Combine(SfrData.Root, root);

					if (!Directory.Exists(path))
						continue;

					foreach (var file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
					{
						var text = File.ReadAllText(file);
						var handler = SkillHandlerAttribute.Match(text);

						if (handler.Success && PartyApplication.IsMatch(text))
							_partyWide.Add(handler.Groups[1].Value);
					}
				}

				return _partyWide;
			}
		}

		/// <summary>
		/// Maps each skill class name to the buffs its handler references.
		/// </summary>
		/// <remarks>
		/// Scanned rather than measured, and deliberately so: this is only the
		/// fallback list for a press the probe cannot dispatch. A press that
		/// dispatches has its buffs read off the character afterwards, which
		/// is authoritative and needs no source at all.
		/// </remarks>
		private static Dictionary<string, BuffId[]> Grants()
		{
			lock (_syncLock)
			{
				if (_grants != null)
					return _grants;

				_grants = [];

				foreach (var root in SkillHandlerRoots)
				{
					var path = Path.Combine(SfrData.Root, root);

					if (!Directory.Exists(path))
						continue;

					foreach (var file in Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories))
					{
						var text = File.ReadAllText(file);
						var handler = SkillHandlerAttribute.Match(text);

						if (!handler.Success)
							continue;

						var buffs = BuffApplication.Matches(text)
							.Select(m => Enum.TryParse<BuffId>(m.Groups[1].Value, out var id) ? id : BuffId.None)
							.Where(id => id != BuffId.None)
							.Distinct()
							.ToArray();

						if (buffs.Length == 0)
							continue;

						var skillName = handler.Groups[1].Value;

						// A Laima override and the handler it replaces both
						// match, and both name the same buffs, so the union is
						// what a press can apply either way.
						_grants[skillName] = _grants.TryGetValue(skillName, out var existing)
							? existing.Union(buffs).ToArray()
							: buffs;
					}
				}

				return _grants;
			}
		}
	}
}
