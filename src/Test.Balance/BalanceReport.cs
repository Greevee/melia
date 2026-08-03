using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Where a skill sits against the rest of its scenario.
	/// </summary>
	public enum BalanceVerdict
	{
		/// <summary>
		/// Far above the scenario median. The shortlist Phase 4 cuts first.
		/// </summary>
		Overpowered,

		/// <summary>
		/// Above the band but not by enough to be the headline problem.
		/// </summary>
		Strong,

		/// <summary>
		/// Inside the band. Nothing to do.
		/// </summary>
		InBand,

		/// <summary>
		/// Below the band, so the class has no reason to press it.
		/// </summary>
		Weak,

		/// <summary>
		/// Measured nothing anywhere. Either the skill deals no direct damage
		/// or the model cannot reach it - the reason column says which.
		/// </summary>
		NoReading,
	}

	/// <summary>
	/// Everything the report knows about one skill, folded down from its
	/// rows across the level grid.
	/// </summary>
	public class SkillVerdict
	{
		public string JobPrefix { get; init; }
		public string SkillClassName { get; init; }
		public SkillRole Role { get; init; }
		public BalanceVerdict Verdict { get; init; }

		/// <summary>
		/// DPS against the scenario median, at measured density. This is the
		/// number the verdict is taken on.
		/// </summary>
		public float DensityRatio { get; init; }

		/// <summary>
		/// The same ratio with the monsters gathered onto one point, which is
		/// the ceiling a pull unlocks.
		/// </summary>
		public float GatheredRatio { get; init; }

		/// <summary>
		/// Output against the caster's own basic attack in the single-target
		/// scenario. Near-meaningless for a full-INT class, which cannot
		/// swing its weapon - use the reference figures for comparison.
		/// </summary>
		public float TimesBasic { get; init; }

		/// <summary>
		/// Output in reference basic attacks at measured density, which is
		/// the absolute anchor the verdict is taken on. The median only says
		/// how a skill compares to its peers; if every peer is overtuned, the
		/// median says nothing.
		/// </summary>
		public float DensityTimesBasic { get; init; }

		/// <summary>
		/// The same, once the monsters are gathered.
		/// </summary>
		public float GatheredTimesBasic { get; init; }

		/// <summary>
		/// How big one press is against one swing of the reference basic
		/// attack. A skill on a long cooldown should be far above 1 here even
		/// when its sustained output is below it - that is what a cooldown is
		/// supposed to buy, and it says whether a weak skill needs a bigger
		/// factor or a shorter cooldown.
		/// </summary>
		public float DensityBurst { get; init; }

		/// <summary>
		/// The scenario the skill is relatively strongest in.
		/// </summary>
		public string BestScenario { get; init; }

		/// <summary>
		/// The scenario it is relatively weakest in, which is the weakness
		/// every skill is supposed to have.
		/// </summary>
		public string WorstScenario { get; init; }

		/// <summary>
		/// Best scenario ratio over the mean across scenarios. Near 1 means
		/// the skill does the same thing everywhere and has no scenario it is
		/// for; high means it is a specialist.
		/// </summary>
		public float Specialisation { get; init; }

		/// <summary>
		/// Ratio to the scenario median, per scenario.
		/// </summary>
		public Dictionary<string, float> ByScenario { get; init; } = [];

		/// <summary>
		/// Targets the skill reaches at measured density, which is what
		/// separates a real AoE from a nominal one.
		/// </summary>
		public float DensityTargets { get; init; }

		/// <summary>
		/// Damage the encounter probe attributes to pads, summons and damage
		/// over time, as a share of the total.
		/// </summary>
		public float IndirectShare { get; init; }

		/// <summary>
		/// Casts per second at measured density. A skill with no cooldown and
		/// no overheat spams, and that is usually why its DPS is large.
		/// </summary>
		public float CastsPerSecond { get; init; }

		/// <summary>
		/// Whether the SP pool sustains the skill's rhythm in every scenario
		/// it was measured in.
		/// </summary>
		public bool SpSustainable { get; init; }

		/// <summary>
		/// How much of the caster's damage survives a 20-level gap.
		/// </summary>
		public float WallRatio { get; init; }

		public string Notes { get; init; }
	}

	/// <summary>
	/// Turns the sweep CSVs into a report a person can read, grouping every
	/// skill and buff into what is overpowered, what is in band and what is
	/// not worth pressing.
	/// </summary>
	public static class BalanceReport
	{
		/// <summary>
		/// How far above the scenario median counts as overpowered.
		/// </summary>
		public const float OverpoweredAt = 2.0f;

		/// <summary>
		/// Where the in-band region starts on the high side.
		/// </summary>
		public const float StrongAt = 1.4f;

		/// <summary>
		/// Where the in-band region ends on the low side.
		/// </summary>
		public const float WeakAt = 0.5f;

		/// <summary>
		/// A skill costs SP and a cooldown, so anything at or under a basic
		/// attack is not worth pressing at all.
		/// </summary>
		public const float BasicFloor = 1.0f;

		/// <summary>
		/// Where a skill starts being clearly better than just swinging.
		/// </summary>
		public const float BasicTarget = 1.5f;

		/// <summary>
		/// Top of the intended band against a basic attack, at measured
		/// density.
		/// </summary>
		public const float BasicBandTop = 4.0f;

		/// <summary>
		/// Past this, a skill is absurd rather than strong.
		/// </summary>
		public const float BasicCeiling = 8.0f;

		/// <summary>
		/// The same ceiling once the monsters are gathered, where the plan
		/// deliberately allows a much bigger number.
		/// </summary>
		public const float GatheredBasicCeiling = 16.0f;

		/// <summary>
		/// Scenario the verdict is taken at: the ungathered case a player is
		/// actually in most of the time.
		/// </summary>
		public const string PrimaryScenario = "S2";

		/// <summary>
		/// Scenario the ceiling is read from.
		/// </summary>
		public const string GatheredScenario = "S3";

		/// <summary>
		/// Writes the whole report and returns its path.
		/// </summary>
		/// <param name="profiles"></param>
		/// <param name="encounters"></param>
		/// <param name="buffs"></param>
		public static string Write(IEnumerable<SkillProfile> profiles, IEnumerable<EncounterResult> encounters, IEnumerable<BuffEffect> buffs)
		{
			var builder = new StringBuilder();

			profiles = profiles?.ToArray() ?? Array.Empty<SkillProfile>();
			encounters = encounters?.ToArray() ?? Array.Empty<EncounterResult>();
			buffs = buffs?.ToArray() ?? Array.Empty<BuffEffect>();

			builder.AppendLine("# Balance harness report");
			builder.AppendLine();
			builder.AppendLine("Generated by `Test.Balance`. Every number is measured through the real");
			builder.AppendLine("handler pipeline; nothing here is derived from a formula.");
			builder.AppendLine();

			var verdicts = Judge(profiles, encounters);

			// Buffs and toggles have no damage to price, so including them
			// would put a third of the roster in "loses to a basic attack"
			// and drag every median with it.
			var damage = verdicts.Where(v => v.Role != SkillRole.Utility).ToArray();

			WriteRunSummary(builder, profiles, encounters, buffs);
			WriteBasicAttackSection(builder, damage);
			WriteScenarioSection(builder, damage, profiles);
			WriteSkillSections(builder, damage);
			WriteClassTable(builder, damage);
			WriteUtilitySection(builder);
			WriteWallSection(builder, damage);
			WriteIndirectSection(builder, encounters);
			WriteBuffSection(builder, buffs);

			Directory.CreateDirectory(SweepReport.OutputDirectory);

			var path = Path.Combine(SweepReport.OutputDirectory, "report.md");

			File.WriteAllText(path, builder.ToString(), Encoding.UTF8);

			return Path.GetFullPath(path);
		}

		/// <summary>
		/// Folds every row of a skill down to one verdict.
		/// </summary>
		/// <param name="profiles"></param>
		/// <param name="encounters"></param>
		public static SkillVerdict[] Judge(IEnumerable<SkillProfile> profiles, IEnumerable<EncounterResult> encounters)
		{
			var rows = profiles.ToArray();
			var indirect = encounters
				.Where(e => e.Error == null && e.Dps > 0)
				.GroupBy(e => e.SkillClassName)
				.ToDictionary(g => g.Key, g => g.Max(e => e.Dps));

			// The median is taken per scenario and per character level, so a
			// level 15 reading is never compared against a level 99 one.
			var medians = rows
				.Where(r => r.Role == SkillRole.Direct && r.Dps > 0)
				.GroupBy(r => (r.ScenarioId, r.CharacterLevel))
				.ToDictionary(g => g.Key, g => SweepReport.Median(g.Select(r => r.Dps)));

			var verdicts = new List<SkillVerdict>();

			foreach (var group in rows.GroupBy(r => (r.JobPrefix, r.SkillClassName)))
			{
				var byScenario = group
					.Select(r => r.ScenarioId)
					.Distinct()
					.ToDictionary(id => id, id => Ratio(group, medians, id));

				var density = byScenario.GetValueOrDefault(PrimaryScenario);
				var gathered = byScenario.GetValueOrDefault(GatheredScenario);
				var single = group.Where(r => r.ScenarioId == "S1").ToArray();
				var role = group.First().Role;

				var densityBasic = TimesBasicIn(group, PrimaryScenario);
				var gatheredBasic = TimesBasicIn(group, GatheredScenario);
				var burst = Median(group.Where(r => r.ScenarioId == PrimaryScenario && r.Dps > 0).Select(r => r.BurstTimesReference));
				var timesBasic = single.Length == 0 ? 0 : Median(single.Select(r => r.TimesBasic));

				var live = byScenario.Where(p => p.Value > 0).ToArray();
				var best = live.Length == 0 ? default : live.MaxBy(p => p.Value);
				var worst = live.Length == 0 ? default : live.MinBy(p => p.Value);
				var mean = live.Length == 0 ? 0 : live.Average(p => p.Value);
				var targets = group.Where(r => r.ScenarioId == PrimaryScenario).Select(r => (float)r.TargetsReached).DefaultIfEmpty(0).Average();
				var sustainable = group.All(r => r.SpSustainable);

				var sameLevel = Median(single.Select(r => r.Dps));
				var aboveLevel = Median(group.Where(r => r.ScenarioId == "S8").Select(r => r.Dps));
				var wall = sameLevel <= 0 ? 0 : aboveLevel / sameLevel;

				var encounterDps = indirect.GetValueOrDefault(group.Key.SkillClassName, 0f);
				var directDps = Median(group.Where(r => r.ScenarioId == GatheredScenario).Select(r => r.Dps));
				var indirectShare = encounterDps <= 0 ? 0 : Math.Max(0, 1f - directDps / encounterDps);

				verdicts.Add(new SkillVerdict
				{
					JobPrefix = group.Key.JobPrefix,
					SkillClassName = group.Key.SkillClassName,
					Role = role,
					Verdict = Decide(density, gathered, densityBasic, gatheredBasic),
					DensityRatio = density,
					GatheredRatio = gathered,
					TimesBasic = timesBasic,
					DensityTimesBasic = densityBasic,
					GatheredTimesBasic = gatheredBasic,
					DensityBurst = burst,
					BestScenario = best.Key,
					WorstScenario = worst.Key,
					Specialisation = mean <= 0 ? 0 : best.Value / mean,
					ByScenario = byScenario,
					DensityTargets = targets,
					IndirectShare = indirectShare,
					CastsPerSecond = Median(group.Where(r => r.ScenarioId == PrimaryScenario).Select(r => r.CastsPerSecond)),
					SpSustainable = sustainable,
					WallRatio = wall,
					Notes = Describe(group, role, sustainable, timesBasic, encounterDps),
				});
			}

			return verdicts.ToArray();
		}

		/// <summary>
		/// Returns the skill's DPS against its scenario's median.
		/// </summary>
		/// <param name="group"></param>
		/// <param name="medians"></param>
		/// <param name="scenarioId"></param>
		private static float Ratio(IEnumerable<SkillProfile> group, Dictionary<(string, int), float> medians, string scenarioId)
		{
			var ratios = new List<float>();

			foreach (var row in group.Where(r => r.ScenarioId == scenarioId))
			{
				if (!medians.TryGetValue((row.ScenarioId, row.CharacterLevel), out var median) || median <= 0)
					continue;

				ratios.Add(row.Dps / median);
			}

			return Median(ratios);
		}

		/// <summary>
		/// Returns the skill's output against a basic attack in the given
		/// scenario, folded across the level grid.
		/// </summary>
		/// <param name="group"></param>
		/// <param name="scenarioId"></param>
		private static float TimesBasicIn(IEnumerable<SkillProfile> group, string scenarioId)
			=> Median(group.Where(r => r.ScenarioId == scenarioId && r.Dps > 0).Select(r => r.TimesReference));

		/// <summary>
		/// Places a skill in a band.
		/// </summary>
		/// <remarks>
		/// The basic attack is the absolute anchor and the scenario median is
		/// the relative one; a skill fails on either. The median alone cannot
		/// catch a whole game being overtuned, and the basic attack alone
		/// cannot catch one skill outclassing its peers.
		/// </remarks>
		/// <param name="density"></param>
		/// <param name="gathered"></param>
		/// <param name="densityBasic"></param>
		/// <param name="gatheredBasic"></param>
		private static BalanceVerdict Decide(float density, float gathered, float densityBasic, float gatheredBasic)
		{
			var best = Math.Max(density, gathered);

			if (best <= 0)
				return BalanceVerdict.NoReading;

			// A skill is only overpowered if it is above the band in the
			// ungathered case too. A big gathered number that needs a pull is
			// the ceiling the plan explicitly allows - but not an unbounded
			// one, hence the gathered ceiling.
			if (density >= OverpoweredAt || densityBasic >= BasicCeiling || gatheredBasic >= GatheredBasicCeiling)
				return BalanceVerdict.Overpowered;

			if (density >= StrongAt || densityBasic >= BasicBandTop)
				return BalanceVerdict.Strong;

			// Peer-relative, because the absolute floor currently catches the
			// entire roster - see the basic-attack section. A skill is weak
			// when it is behind its own class's options as well as behind a
			// swing, which is what makes it the one to retune first.
			if (best <= WeakAt || (densityBasic > 0 && densityBasic <= BasicFloor && density <= 1f))
				return BalanceVerdict.Weak;

			return BalanceVerdict.InBand;
		}

		/// <summary>
		/// Returns the short note the tables carry, which is where a zero
		/// explains itself.
		/// </summary>
		/// <param name="group"></param>
		/// <param name="role"></param>
		/// <param name="sustainable"></param>
		/// <param name="timesBasic"></param>
		/// <param name="encounterDps"></param>
		private static string Describe(IEnumerable<SkillProfile> group, SkillRole role, bool sustainable, float timesBasic, float encounterDps)
		{
			var notes = new List<string>();
			var rows = group.ToArray();

			if (rows.All(r => r.Dps <= 0))
			{
				var reason = rows
					.Where(r => r.Zero != ZeroReason.None)
					.GroupBy(r => r.Zero)
					.OrderByDescending(g => g.Count())
					.Select(g => g.Key)
					.FirstOrDefault();

				notes.Add(role == SkillRole.Indirect ? "no direct damage by design" : reason.ToString());

				if (encounterDps > 0)
					notes.Add($"encounter probe reads {encounterDps:F0} dps");
			}

			if (!sustainable)
				notes.Add("SP does not sustain 30 s");

			if (timesBasic > 0 && timesBasic < 1)
				notes.Add($"worse than a basic attack ({timesBasic:F2}x)");

			var unreachable = rows.Count(r => r.Zero == ZeroReason.OutOfReach);

			if (unreachable > 0 && rows.Any(r => r.Dps > 0))
				notes.Add($"out of reach in {unreachable} row(s)");

			return string.Join("; ", notes);
		}

		/// <summary>
		/// Writes what the run covered, so a partial sweep is never mistaken
		/// for a full one.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="profiles"></param>
		/// <param name="encounters"></param>
		/// <param name="buffs"></param>
		private static void WriteRunSummary(StringBuilder builder, IEnumerable<SkillProfile> profiles, IEnumerable<EncounterResult> encounters, IEnumerable<BuffEffect> buffs)
		{
			var rows = profiles.ToArray();

			builder.AppendLine("## What this run covered");
			builder.AppendLine();
			builder.AppendLine("| | |");
			builder.AppendLine("|---|---|");
			builder.AppendLine($"| classes | {rows.Select(r => r.JobPrefix).Distinct().Count()} |");
			builder.AppendLine($"| skills | {rows.Select(r => r.SkillClassName).Distinct().Count()} |");
			builder.AppendLine($"| rows | {rows.Length} |");
			builder.AppendLine($"| character levels | {Join(rows.Select(r => r.CharacterLevel))} |");
			builder.AppendLine($"| skill levels | {Join(rows.Select(r => r.SkillLevel))} |");
			builder.AppendLine($"| monster levels | {Join(rows.Select(r => r.MobLevel))} |");
			builder.AppendLine($"| scenarios | {Join(rows.Select(r => r.ScenarioId))} |");
			builder.AppendLine($"| encounters | {encounters.Count()} |");
			builder.AppendLine($"| buff readings | {buffs.Count()} |");
			builder.AppendLine();

			if (rows.Length == 0)
				return;

			builder.AppendLine("Median DPS by scenario and character level, which is the reference every");
			builder.AppendLine("ratio below is taken against.");
			builder.AppendLine();

			var levels = rows.Select(r => r.CharacterLevel).Distinct().OrderBy(l => l).ToArray();

			builder.AppendLine("| scenario | " + string.Join(" | ", levels.Select(l => "lv " + l)) + " |");
			builder.AppendLine("|---" + string.Concat(levels.Select(_ => "|---")) + "|");

			foreach (var scenario in rows.Select(r => r.ScenarioId).Distinct().OrderBy(s => s))
			{
				var cells = levels.Select(level =>
				{
					var median = SweepReport.Median(rows.Where(r => r.Role == SkillRole.Direct && r.ScenarioId == scenario && r.CharacterLevel == level).Select(r => r.Dps));

					return median <= 0 ? "-" : median.ToString("F0", CultureInfo.InvariantCulture);
				});

				builder.AppendLine($"| {scenario} | {string.Join(" | ", cells)} |");
			}

			builder.AppendLine();
		}

		/// <summary>
		/// Writes the distribution against a basic attack, which is the only
		/// anchor in the report that does not move when everything is
		/// overtuned together.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="verdicts"></param>
		private static void WriteBasicAttackSection(StringBuilder builder, SkillVerdict[] verdicts)
		{
			var measured = verdicts.Where(v => v.DensityTimesBasic > 0).ToArray();

			builder.AppendLine("## Against a basic attack");
			builder.AppendLine();
			builder.AppendLine($"A skill costs SP and a cooldown, so it has to beat swinging a weapon - and by");
			builder.AppendLine($"a bounded amount. The unit is the **reference basic attack**: a STR {SkillProfiler.ReferenceJob}");
			builder.AppendLine("of the same level in the same gear tier, measured in the same scenario. A");
			builder.AppendLine("class's own autoattack is not the unit, because a full-INT class has none.");
			builder.AppendLine($"Target is **{BasicTarget:F1}x-{BasicBandTop:F0}x** at measured density (S2), with");
			builder.AppendLine($"up to {GatheredBasicCeiling:F0}x allowed once the monsters are gathered (S3), since reaching");
			builder.AppendLine("that ceiling costs a pull.");
			builder.AppendLine();
			builder.AppendLine("Two numbers, and the gap between them is the diagnosis. **Burst** is one press");
			builder.AppendLine("against one swing; **sustained** is the same over time, so it divides burst by");
			builder.AppendLine("how much of the cooldown the skill spends waiting. A skill with a healthy burst");
			builder.AppendLine("and a poor sustained number needs a shorter cooldown; one that is low on both");
			builder.AppendLine("needs a bigger factor.");
			builder.AppendLine();

			var burst = measured.Where(v => v.DensityBurst > 0).ToArray();

			if (burst.Length > 0)
			{
				builder.AppendLine($"Median burst across {burst.Length} skills: **{Median(burst.Select(v => v.DensityBurst)):F2}x** one swing, " +
					$"against a median sustained **{Median(measured.Select(v => v.DensityTimesBasic)):F2}x**.");
				builder.AppendLine();
			}

			if (measured.Length == 0)
			{
				builder.AppendLine("Nothing measured.");
				builder.AppendLine();
				return;
			}

			var bands = new (string Label, Func<SkillVerdict, bool> Test)[]
			{
				($"absurd (>= {BasicCeiling:F0}x)", v => v.DensityTimesBasic >= BasicCeiling),
				($"hot ({BasicBandTop:F0}x - {BasicCeiling:F0}x)", v => v.DensityTimesBasic >= BasicBandTop && v.DensityTimesBasic < BasicCeiling),
				($"on target ({BasicTarget:F1}x - {BasicBandTop:F0}x)", v => v.DensityTimesBasic >= BasicTarget && v.DensityTimesBasic < BasicBandTop),
				($"marginal ({BasicFloor:F1}x - {BasicTarget:F1}x)", v => v.DensityTimesBasic > BasicFloor && v.DensityTimesBasic < BasicTarget),
				($"loses to a basic attack (<= {BasicFloor:F1}x)", v => v.DensityTimesBasic <= BasicFloor),
			};

			builder.AppendLine("| band | skills | share |");
			builder.AppendLine("|---|---|---|");

			foreach (var (label, test) in bands)
			{
				var count = measured.Count(test);

				builder.AppendLine($"| {label} | {count} | {(float)count / measured.Length:P0} |");
			}

			builder.AppendLine();
			builder.AppendLine($"Median across {measured.Length} skills: **{Median(measured.Select(v => v.DensityTimesBasic)):F2}x** a basic attack at measured density, " +
				$"**{Median(measured.Where(v => v.GatheredTimesBasic > 0).Select(v => v.GatheredTimesBasic)):F2}x** gathered.");
			builder.AppendLine();

			var absurd = measured.Where(v => v.DensityTimesBasic >= BasicCeiling).OrderByDescending(v => v.DensityTimesBasic).Take(20).ToArray();
			var losing = measured.Where(v => v.DensityTimesBasic <= BasicFloor).OrderBy(v => v.DensityTimesBasic).Take(20).ToArray();

			WriteBasicList(builder, "Furthest past the ceiling", absurd);
			WriteBasicList(builder, "Losing to a basic attack", losing);
		}

		/// <summary>
		/// Writes one of the basic-attack extremes.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="title"></param>
		/// <param name="listed"></param>
		private static void WriteBasicList(StringBuilder builder, string title, SkillVerdict[] listed)
		{
			builder.AppendLine($"### {title} ({listed.Length} shown)");
			builder.AppendLine();

			if (listed.Length == 0)
			{
				builder.AppendLine("None.");
				builder.AppendLine();
				return;
			}

			builder.AppendLine("| class | skill | burst/press | sustained S2 | sustained S3 | casts/s | targets |");
			builder.AppendLine("|---|---|---|---|---|---|---|");

			foreach (var v in listed)
				builder.AppendLine($"| {v.JobPrefix} | `{v.SkillClassName}` | {Times(v.DensityBurst)} | {Times(v.DensityTimesBasic)} | {Times(v.GatheredTimesBasic)} | {v.CastsPerSecond:F2} | {v.DensityTargets:F1} |");

			builder.AppendLine();
		}

		/// <summary>
		/// Writes whether the scenarios separate skills from each other at
		/// all, and which skills have no scenario they are for.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="verdicts"></param>
		/// <param name="profiles"></param>
		private static void WriteScenarioSection(StringBuilder builder, SkillVerdict[] verdicts, IEnumerable<SkillProfile> profiles)
		{
			var rows = profiles.ToArray();
			var measured = verdicts.Where(v => v.BestScenario != null).ToArray();

			builder.AppendLine("## Scenario spread");
			builder.AppendLine();
			builder.AppendLine("Every skill should have a scenario it is for and one it is bad at. A scenario");
			builder.AppendLine("nothing is best at is not discriminating, and a skill that is flat across all");
			builder.AppendLine("of them has no identity.");
			builder.AppendLine();

			if (measured.Length == 0)
			{
				builder.AppendLine("Nothing measured.");
				builder.AppendLine();
				return;
			}

			builder.AppendLine("| scenario | what it tests | skills best here | skills worst here | reached 0 targets |");
			builder.AppendLine("|---|---|---|---|---|");

			foreach (var spec in ScenarioMatrix.All)
			{
				var blank = rows.Count(r => r.ScenarioId == spec.Id && r.TargetsReached == 0);

				builder.AppendLine($"| {spec.Id} | {spec.Name} | {measured.Count(v => v.BestScenario == spec.Id)} | " +
					$"{measured.Count(v => v.WorstScenario == spec.Id)} | {blank} |");
			}

			builder.AppendLine();

			var flat = measured
				.Where(v => v.Verdict != BalanceVerdict.NoReading && v.Specialisation > 0 && v.Specialisation < 1.25f)
				.OrderBy(v => v.Specialisation)
				.Take(20)
				.ToArray();

			builder.AppendLine($"### Same everywhere ({flat.Length} shown)");
			builder.AppendLine();
			builder.AppendLine("Best scenario within 25% of their own average across scenarios. These are the");
			builder.AppendLine("skills that need a geometry or delivery change rather than a factor change.");
			builder.AppendLine();

			if (flat.Length == 0)
			{
				builder.AppendLine("None.");
				builder.AppendLine();
			}
			else
			{
				builder.AppendLine("| class | skill | spread | best | worst |");
				builder.AppendLine("|---|---|---|---|---|");

				foreach (var v in flat)
					builder.AppendLine($"| {v.JobPrefix} | `{v.SkillClassName}` | {v.Specialisation:F2}x | {v.BestScenario} {Times(v.ByScenario.GetValueOrDefault(v.BestScenario))} | {v.WorstScenario} {Times(v.ByScenario.GetValueOrDefault(v.WorstScenario))} |");

				builder.AppendLine();
			}

			WriteDominantSection(builder, measured);
		}

		/// <summary>
		/// Writes the skills that are their class's best answer in every
		/// scenario, which is the plan's "a class needs more than one button"
		/// gate read from the other side.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="verdicts"></param>
		private static void WriteDominantSection(StringBuilder builder, SkillVerdict[] verdicts)
		{
			var dominant = new List<SkillVerdict>();

			foreach (var group in verdicts.GroupBy(v => v.JobPrefix))
			{
				var scenarios = group.SelectMany(v => v.ByScenario.Where(p => p.Value > 0).Select(p => p.Key)).Distinct().ToArray();

				if (scenarios.Length < 2 || group.Count() < 2)
					continue;

				foreach (var candidate in group)
				{
					var wins = scenarios.Count(s => group.All(other => other == candidate || candidate.ByScenario.GetValueOrDefault(s) >= other.ByScenario.GetValueOrDefault(s)));

					if (wins == scenarios.Length)
						dominant.Add(candidate);
				}
			}

			builder.AppendLine($"### Best in every scenario their class was measured in ({dominant.Count})");
			builder.AppendLine();
			builder.AppendLine("A class should have a reason to press more than one button. These leave it none.");
			builder.AppendLine();

			if (dominant.Count == 0)
			{
				builder.AppendLine("None.");
				builder.AppendLine();
				return;
			}

			builder.AppendLine("| class | skill | x basic S2 | vs peers S2 | spread |");
			builder.AppendLine("|---|---|---|---|---|");

			foreach (var v in dominant.OrderByDescending(v => v.DensityRatio))
				builder.AppendLine($"| {v.JobPrefix} | `{v.SkillClassName}` | {Times(v.DensityTimesBasic)} | {Times(v.DensityRatio)} | {v.Specialisation:F2}x |");

			builder.AppendLine();
		}

		/// <summary>
		/// Writes one section per verdict band, strongest first.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="verdicts"></param>
		private static void WriteSkillSections(StringBuilder builder, SkillVerdict[] verdicts)
		{
			var sections = new (BalanceVerdict Verdict, string Title, string Blurb)[]
			{
				(BalanceVerdict.Overpowered, "Overpowered - cut these first",
					$"At or above {OverpoweredAt:F1}x the scenario median at measured density, or {BasicCeiling:F0}x a basic attack there, or {GatheredBasicCeiling:F0}x once gathered."),
				(BalanceVerdict.Strong, "Strong - above the band",
					$"Past {StrongAt:F1}x the median or {BasicBandTop:F0}x a basic attack, but short of absurd."),
				(BalanceVerdict.InBand, "In band - leave alone",
					$"Beats a basic attack, target {BasicTarget:F1}x-{BasicBandTop:F0}x, and inside {WeakAt:F1}x-{StrongAt:F1}x of its peers."),
				(BalanceVerdict.Weak, "Weak - no reason to press",
					$"At or below a basic attack in both the ungathered and gathered cases, or under {WeakAt:F1}x its peers. It costs SP and a cooldown and buys nothing."),
				(BalanceVerdict.NoReading, "No reading",
					"Measured nothing anywhere. The note says whether that is by design."),
			};

			foreach (var (verdict, title, blurb) in sections)
			{
				var listed = verdicts.Where(v => v.Verdict == verdict).OrderByDescending(v => v.DensityTimesBasic).ThenBy(v => v.SkillClassName).ToArray();

				builder.AppendLine($"## {title} ({listed.Length})");
				builder.AppendLine();
				builder.AppendLine(blurb);
				builder.AppendLine();

				if (listed.Length == 0)
				{
					builder.AppendLine("None.");
					builder.AppendLine();
					continue;
				}

				builder.AppendLine("| class | skill | role | burst/press | sustained S2 | sustained S3 | vs peers S2 | casts/s | best at | worst at | targets | notes |");
				builder.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");

				foreach (var v in listed)
				{
					builder.AppendLine($"| {v.JobPrefix} | `{v.SkillClassName}` | {v.Role} | {Times(v.DensityBurst)} | {Times(v.DensityTimesBasic)} | {Times(v.GatheredTimesBasic)} | " +
						$"{Times(v.DensityRatio)} | {v.CastsPerSecond:F2} | {v.BestScenario ?? "-"} | {v.WorstScenario ?? "-"} | {v.DensityTargets:F1} | {v.Notes} |");
				}

				builder.AppendLine();
			}
		}

		/// <summary>
		/// Writes the per-class roll-up, which is where a class with no
		/// in-band button at all becomes visible.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="verdicts"></param>
		private static void WriteClassTable(StringBuilder builder, SkillVerdict[] verdicts)
		{
			builder.AppendLine("## By class");
			builder.AppendLine();
			builder.AppendLine("A class wants more than one reason to press a button, so a row that is all");
			builder.AppendLine("in one column is a finding on its own.");
			builder.AppendLine();
			builder.AppendLine("| class | skills | OP | strong | in band | weak | no reading | best | worst |");
			builder.AppendLine("|---|---|---|---|---|---|---|---|---|");

			foreach (var group in verdicts.GroupBy(v => v.JobPrefix).OrderBy(g => g.Key))
			{
				var measured = group.Where(v => v.Verdict != BalanceVerdict.NoReading).ToArray();
				var best = measured.OrderByDescending(v => v.DensityRatio).FirstOrDefault();
				var worst = measured.OrderBy(v => v.DensityRatio).FirstOrDefault();

				builder.AppendLine($"| {group.Key} | {group.Count()} | " +
					$"{group.Count(v => v.Verdict == BalanceVerdict.Overpowered)} | " +
					$"{group.Count(v => v.Verdict == BalanceVerdict.Strong)} | " +
					$"{group.Count(v => v.Verdict == BalanceVerdict.InBand)} | " +
					$"{group.Count(v => v.Verdict == BalanceVerdict.Weak)} | " +
					$"{group.Count(v => v.Verdict == BalanceVerdict.NoReading)} | " +
					$"{Name(best)} | {Name(worst)} |");
			}

			builder.AppendLine();
		}

		/// <summary>
		/// Lists the buffs, toggles and passives, which are out of scope for
		/// every damage gate above and are named here so they are visibly
		/// excluded rather than quietly missing.
		/// </summary>
		/// <param name="builder"></param>
		private static void WriteUtilitySection(StringBuilder builder)
		{
			// Read from the catalog rather than from the measurements, since
			// the matrix deliberately does not measure them.
			var utility = JobCatalog.Entries
				.SelectMany(job => JobCatalog.GetProfiledSkills(job)
					.Where(s => s.Role == SkillRole.Utility)
					.Select(s => new SkillVerdict { JobPrefix = job.SkillPrefix, SkillClassName = s.ClassName }))
				.OrderBy(v => v.JobPrefix)
				.ThenBy(v => v.SkillClassName)
				.ToArray();

			builder.AppendLine($"## Utility skills, excluded from the damage gates ({utility.Length})");
			builder.AppendLine();
			builder.AppendLine("Passives, toggles and self-cast buffs. They carry a factor in the data but");
			builder.AppendLine("deal no damage, so pricing them against a basic attack says nothing. What");
			builder.AppendLine("they are worth is measured by the buff sweep instead.");
			builder.AppendLine();

			if (utility.Length == 0)
			{
				builder.AppendLine("None.");
				builder.AppendLine();
				return;
			}

			foreach (var group in utility.GroupBy(v => v.JobPrefix))
				builder.AppendLine($"- **{group.Key}**: {string.Join(", ", group.Select(v => "`" + v.SkillClassName + "`"))}");

			builder.AppendLine();
		}

		/// <summary>
		/// Writes what survives a 20-level gap, which is the accuracy wall
		/// read from the skill's side.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="verdicts"></param>
		private static void WriteWallSection(StringBuilder builder, SkillVerdict[] verdicts)
		{
			var measured = verdicts.Where(v => v.WallRatio > 0).OrderByDescending(v => v.WallRatio).ToArray();

			builder.AppendLine("## The +20 level wall");
			builder.AppendLine();
			builder.AppendLine("Share of single-target DPS that survives against a monster 20 levels above");
			builder.AppendLine("the caster (S8 over S1). The plan wants this punishing everywhere, so a");
			builder.AppendLine("skill near the top of this list is one that ignores the wall.");
			builder.AppendLine();

			if (measured.Length == 0)
			{
				builder.AppendLine("No skill measured damage in both scenarios.");
				builder.AppendLine();
				return;
			}

			builder.AppendLine($"Median across {measured.Length} skills: **{Median(measured.Select(v => v.WallRatio)):P0}** of same-level output.");
			builder.AppendLine();
			builder.AppendLine("| class | skill | survives |");
			builder.AppendLine("|---|---|---|");

			foreach (var v in measured.Take(15))
				builder.AppendLine($"| {v.JobPrefix} | `{v.SkillClassName}` | {v.WallRatio:P0} |");

			builder.AppendLine();
		}

		/// <summary>
		/// Writes the skills whose damage does not come from the direct hit,
		/// which the skill matrix cannot price at all.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="encounters"></param>
		private static void WriteIndirectSection(StringBuilder builder, IEnumerable<EncounterResult> encounters)
		{
			var measured = encounters.Where(e => e.Error == null && e.Dps > 0).ToArray();

			builder.AppendLine("## Delivered by pads, summons and damage over time");
			builder.AppendLine();
			builder.AppendLine("Measured by running the real handler and totalling everything the enemies");
			builder.AppendLine("lost. The skill matrix reads these as zero, so they are priced here.");
			builder.AppendLine();

			if (measured.Length == 0)
			{
				builder.AppendLine("No encounter sweep in this run. Run `SweepTests.Encounters` to fill it in.");
				builder.AppendLine();
				return;
			}

			builder.AppendLine("| class | skill | scenario | total dps | pads | summons |");
			builder.AppendLine("|---|---|---|---|---|---|");

			foreach (var e in measured.OrderByDescending(e => e.Dps).Take(30))
				builder.AppendLine($"| {e.JobPrefix} | `{e.SkillClassName}` | {e.ScenarioId} | {e.Dps:F0} | {e.PadsCreated} | {e.SummonsCreated} |");

			builder.AppendLine();
		}

		/// <summary>
		/// Writes the buffs that move damage furthest, which is what the
		/// multiplier budget has to fit around.
		/// </summary>
		/// <param name="builder"></param>
		/// <param name="buffs"></param>
		private static void WriteBuffSection(StringBuilder builder, IEnumerable<BuffEffect> buffs)
		{
			var live = buffs.Where(b => b.HasEffect).ToArray();

			builder.AppendLine("## Buffs and debuffs");
			builder.AppendLine();

			if (live.Length == 0)
			{
				builder.AppendLine("No buff sweep in this run. Run `SweepTests.Buffs` to fill it in.");
				builder.AppendLine();
				return;
			}

			foreach (var slot in live.GroupBy(b => b.Slot).OrderBy(g => g.Key))
			{
				var median = SweepReport.Median(slot.Select(b => Math.Abs(b.Ratio - 1f)));

				builder.AppendLine($"### {slot.Key}");
				builder.AppendLine();
				builder.AppendLine($"{slot.Count()} readings move damage, median swing {median:P1}.");
				builder.AppendLine();
				builder.AppendLine("| buff | owner | effect |");
				builder.AppendLine("|---|---|---|");

				foreach (var effect in slot.OrderByDescending(b => Math.Abs(b.Ratio - 1f)).Take(15))
					builder.AppendLine($"| `{effect.Id}` | {effect.Owner} | {effect.Ratio:F2}x |");

				builder.AppendLine();
			}
		}

		private static string Name(SkillVerdict verdict)
			=> verdict == null ? "-" : $"`{verdict.SkillClassName}` {Times(verdict.DensityRatio)}";

		private static string Times(float value)
			=> value <= 0 ? "-" : value.ToString("F2", CultureInfo.InvariantCulture) + "x";

		private static string Join(IEnumerable<int> values)
			=> string.Join(", ", values.Distinct().OrderBy(v => v));

		private static string Join(IEnumerable<string> values)
			=> string.Join(", ", values.Distinct().OrderBy(v => v));

		private static float Median(IEnumerable<float> values)
		{
			var sorted = values.OrderBy(v => v).ToArray();

			return sorted.Length == 0 ? 0 : sorted[sorted.Length / 2];
		}
	}
}
