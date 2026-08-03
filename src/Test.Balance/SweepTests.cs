using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Xunit;
using Xunit.Abstractions;

namespace Melia.Test.Balance
{
	/// <summary>
	/// The long-running sweeps that produce the CSVs Phase 4 works from.
	/// Opt-in, because a full run takes minutes and the rest of the suite is
	/// meant to stay fast.
	/// </summary>
	/// <remarks>
	/// Run with: BALANCE_SWEEP=1 dotnet test src/Test.Balance/Test.Balance.csproj
	/// </remarks>
	[Collection(BalanceCollection.Name)]
	public class SweepTests
	{
		/// <summary>
		/// Environment variable that enables the sweeps.
		/// </summary>
		public const string EnableVariable = "BALANCE_SWEEP";

		/// <summary>
		/// How far from the median counts as an outlier worth listing.
		/// </summary>
		private const float OutlierFactor = 2f;

		/// <summary>
		/// What the process has measured so far. Static because the report
		/// stitches every sweep together and they run as separate tests.
		/// </summary>
		private static List<SkillProfile> _profiles = [];
		private static List<EncounterResult> _encounters = [];
		private static List<BuffEffect> _buffs = [];

		private readonly ITestOutputHelper _output;

		public SweepTests(BalanceHost host, ITestOutputHelper output)
		{
			_output = output;
		}

		private static bool Enabled => Environment.GetEnvironmentVariable(EnableVariable) == "1";

		[Fact]
		public void SkillMatrix()
		{
			if (!Skip("SkillMatrix"))
				return;

			// The grid, not one point on it: factorByLevel and the reference
			// gear curve are both what is under test, so a single character
			// level would measure neither.
			var levels = ReadInts("BALANCE_SWEEP_CHAR_LEVELS", ScenarioMatrix.CharacterLevels);
			var skillLevels = ReadInts("BALANCE_SWEEP_SKILL_LEVELS", ScenarioMatrix.SkillLevels);
			var rows = new List<string>();
			var profiles = new List<SkillProfile>();

			foreach (var job in JobCatalog.Entries)
			{
				// Damage skills only. A zero-factor buff logs a
				// SCR_CalculateDamage warning on every one of its 500 samples,
				// which is tens of thousands of lines per skill and dominated
				// the run; they are listed in the report without being measured.
				foreach (var entry in JobCatalog.GetDamageSkills(job))
				{
					foreach (var characterLevel in ScenarioMatrix.CharacterLevelsFor(job, levels))
					{
						foreach (var skillLevel in ScenarioMatrix.SkillLevelsFor(entry, skillLevels))
						{
							foreach (var spec in ScenarioMatrix.All)
							{
								SkillProfile profile;

								try
								{
									profile = SkillProfiler.Measure(job, entry, skillLevel, spec, characterLevel);
								}
								catch (Exception ex)
								{
									rows.Add(SweepReport.Row(job.SkillPrefix, entry.ClassName, entry.Role, spec.Id, characterLevel, skillLevel,
										0, 0, 0, 0, 0, 0, 0, 0, "", 0, 0, 0, 0, 0, 0, 0, false, ZeroReason.None, ex.GetType().Name + ": " + ex.Message));
									continue;
								}

								profiles.Add(profile);
								rows.Add(SweepReport.Row(
									job.SkillPrefix, entry.ClassName, entry.Role, spec.Id, characterLevel, skillLevel,
									profile.MobLevel, profile.TargetsReached, profile.DamagePerCast, profile.Dps,
									profile.BasicAttackPerCast, profile.BasicCastsPerSecond,
									profile.BasicTargetsReached, profile.HitsPerCast, profile.PrimaryStat,
									profile.ReferenceDamagePerCast, profile.TimesBasic, profile.TimesReference, profile.CastsPerSecond, profile.SpPerSecond,
									profile.CastsToKill, profile.DodgeRate, profile.SpSustainable, profile.Zero, ""));
							}
						}
					}
				}
			}

			var path = SweepReport.Write("skill-matrix",
				"class,skill,role,scenario,charLevel,skillLevel,mobLevel,targets,damagePerCast,dps,basicAttackPerCast,basicCastsPerSecond,basicTargets,hitsPerCast,primaryStat,referenceDamagePerCast,timesBasic,timesReference,castsPerSecond,spPerSecond,castsToKill,dodgeRate,spSustainable,zeroReason,error",
				rows);

			_output.WriteLine($"{rows.Count} rows -> {path}");

			ReportSkillOutliers(profiles);
			ReportZeroes(profiles);
			WriteReport(profiles: profiles);

			Assert.NotEmpty(profiles);
		}

		/// <summary>
		/// Regenerates the human-readable report from the CSVs a previous
		/// sweep left behind, so the bands and the grouping can be retuned in
		/// seconds instead of by rerunning a twenty-minute measurement.
		/// </summary>
		[Fact]
		public void Report()
		{
			var profiles = SweepArchive.ReadProfiles();
			var encounters = SweepArchive.ReadEncounters();
			var buffs = SweepArchive.ReadBuffs();

			if (profiles.Length == 0)
			{
				_output.WriteLine($"No skill-matrix.csv under {SweepReport.OutputDirectory}. Run SkillMatrix first.");
				return;
			}

			_output.WriteLine($"{profiles.Length} profiles, {encounters.Length} encounters, {buffs.Length} buff readings");
			_output.WriteLine($"report -> {BalanceReport.Write(profiles, encounters, buffs)}");
		}

		[Fact]
		public void Encounters()
		{
			if (!Skip("Encounters"))
				return;

			// Wall-clock, so this is scoped by default. BALANCE_SWEEP_CLASSES
			// picks the classes; leaving it unset runs all of them and takes
			// hours.
			var only = Environment.GetEnvironmentVariable("BALANCE_SWEEP_CLASSES");
			var seconds = ReadInts("BALANCE_SWEEP_SECONDS", [(int)EncounterProbe.DefaultSeconds]).First();
			var characterLevel = ReadInts("BALANCE_SWEEP_CHAR_LEVELS", [50]).Last();
			var skillLevel = ReadInts("BALANCE_SWEEP_SKILL_LEVELS", [10]).Last();
			var scenarios = ScenarioMatrix.All.Where(s => s.Id is "S1" or "S2" or "S3").ToArray();

			var jobs = JobCatalog.Entries.AsEnumerable();

			if (!string.IsNullOrWhiteSpace(only))
			{
				var wanted = only.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
				jobs = jobs.Where(j => wanted.Contains(j.SkillPrefix, StringComparer.OrdinalIgnoreCase));
			}

			var rows = new List<string>();
			var results = new List<EncounterResult>();

			foreach (var job in jobs.ToArray())
			{
				foreach (var entry in JobCatalog.GetDamageSkills(job))
				{
					foreach (var spec in scenarios)
					{
						var encounter = EncounterProbe.Measure(job, entry.Id, skillLevel, spec, characterLevel, seconds);

						// The analytical profiler prices only the direct hit,
						// so the gap between the two is what pads, summons and
						// damage over time contribute.
						var direct = 0f;

						try
						{
							direct = SkillProfiler.Measure(job, entry, skillLevel, spec, characterLevel).Dps;
						}
						catch (Exception)
						{
							direct = 0;
						}

						var indirectShare = encounter.Dps <= 0 ? 0 : Math.Max(0, 1f - direct / encounter.Dps);

						results.Add(encounter);
						rows.Add(SweepReport.Row(job.SkillPrefix, encounter.SkillClassName, spec.Id, characterLevel, skillLevel,
							encounter.EnemyCount, encounter.Casts, encounter.Seconds, encounter.TotalDamage, encounter.Dps,
							direct, indirectShare, encounter.PadsCreated, encounter.SummonsCreated, encounter.EnemyDied, encounter.Error));
					}
				}
			}

			var path = SweepReport.Write("encounters",
				"class,skill,scenario,charLevel,skillLevel,enemies,casts,seconds,totalDamage,dps,directDps,indirectShare,pads,summons,enemyDied,error",
				rows);

			_output.WriteLine($"{rows.Count} encounters -> {path}");
			_output.WriteLine($"  {results.Count(r => r.PadsCreated > 0)} created pads, {results.Count(r => r.SummonsCreated > 0)} created summons");
			_output.WriteLine($"  {results.Count(r => r.Error != null)} failed, {results.Count(r => r.EnemyDied)} lost damage to a death");
			_output.WriteLine("");

			foreach (var result in results.Where(r => r.Error == null && r.Dps > 0).OrderByDescending(r => r.Dps).Take(25))
				_output.WriteLine($"  {result}");

			WriteReport(encounters: results);

			Assert.NotEmpty(results);
		}

		[Fact]
		public void Buffs()
		{
			if (!Skip("Buffs"))
				return;

			var slots = Enum.GetValues<BuffSlot>();
			var rows = new List<string>();
			var effects = new List<BuffEffect>();

			foreach (var entry in BuffCatalog.Entries)
			{
				foreach (var slot in slots)
				{
					var effect = BuffProbe.Measure(entry, slot);

					effects.Add(effect);
					rows.Add(SweepReport.Row(entry.Id, entry.Owner, entry.Handler, slot, effect.Level,
						effect.CasterInt, effect.Baseline, effect.Buffed, effect.Ratio, effect.HasEffect,
						effect.IsLive, effect.PropertyDeltas, effect.Error));
				}
			}

			var path = SweepReport.Write("buffs",
				"buff,owner,handler,slot,level,casterInt,baseline,buffed,ratio,movesDamage,isLive,propertyDeltas,error",
				rows);

			var live = effects.Where(e => e.HasEffect).ToArray();
			var statOnly = effects.Where(e => !e.HasEffect && e.IsLive).Select(e => e.Id).Distinct().Count();
			var failed = effects.Where(e => e.Error != null).Select(e => e.Id).Distinct().ToArray();
			var inert = effects.GroupBy(e => e.Id).Count(g => g.All(e => e.Error == null && !e.IsLive));

			_output.WriteLine($"{BuffCatalog.Entries.Length} buffs with handlers");
			_output.WriteLine($"  {live.Length} readings move damage");
			_output.WriteLine($"  {statOnly} move a property but not damage");
			_output.WriteLine($"  {inert} did nothing observable");
			_output.WriteLine($"  {failed.Length} could not be applied at all: {string.Join(", ", failed.Take(40))}");
			_output.WriteLine($"-> {path}");

			ReportBuffOutliers(live);
			SweepStatScalingBuffs(live);
			WriteReport(buffs: effects);

			Assert.NotEmpty(BuffCatalog.Entries);
		}

		[Fact]
		public void BuffCombinations()
		{
			if (!Skip("BuffCombinations"))
				return;

			// The power set is exponential, so it runs over the strongest
			// buffs rather than all of them. Everything dropped is logged.
			var size = ReadInts("BALANCE_SWEEP_STACK_SIZE", [10]).First();
			var rows = new List<string>();

			foreach (var slot in new[] { BuffSlot.SelfOffense, BuffSlot.EnemyOffense })
			{
				var individual = new Dictionary<BuffId, float>();
				var candidates = new List<BuffEntry>();

				foreach (var entry in BuffCatalog.Entries)
				{
					var effect = BuffProbe.Measure(entry, slot);

					if (!effect.HasEffect)
						continue;

					individual[entry.Id] = effect.Ratio;
					candidates.Add(entry);
				}

				var ranked = candidates
					.OrderByDescending(e => Math.Abs(individual[e.Id] - 1f))
					.ToArray();

				var chosen = ranked.Take(size).ToArray();

				_output.WriteLine($"{slot}: {candidates.Count} buffs with an effect, combining the strongest {chosen.Length} " +
					$"({Math.Max(0, ranked.Length - chosen.Length)} dropped, weakest first)");

				foreach (var subset in PowerSet(chosen))
				{
					if (subset.Length < 2)
						continue;

					var stack = BuffProbe.MeasureCombination(subset, slot, individual);

					rows.Add(SweepReport.Row(slot, subset.Length, string.Join(" + ", stack.Ids),
						stack.Baseline, stack.Buffed, stack.Ratio, stack.Expected, stack.StackFactor));
				}
			}

			var path = SweepReport.Write("buff-combinations",
				"slot,count,buffs,baseline,buffed,ratio,expected,stackFactor",
				rows);

			_output.WriteLine($"{rows.Count} combinations -> {path}");

			Assert.NotEmpty(rows);
		}

		/// <summary>
		/// Regenerates the human-readable report from everything the process
		/// has measured so far, so it does not matter which sweep finishes
		/// last or which ones were run at all.
		/// </summary>
		/// <param name="profiles"></param>
		/// <param name="encounters"></param>
		/// <param name="buffs"></param>
		private void WriteReport(List<SkillProfile> profiles = null, List<EncounterResult> encounters = null, List<BuffEffect> buffs = null)
		{
			if (profiles != null)
				_profiles = profiles;

			if (encounters != null)
				_encounters = encounters;

			if (buffs != null)
				_buffs = buffs;

			_output.WriteLine($"report -> {BalanceReport.Write(_profiles, _encounters, _buffs)}");
		}

		/// <summary>
		/// Groups the rows that measured nothing by why, which is what
		/// separates a scenario doing its job from a skill the direct-hit
		/// model cannot price.
		/// </summary>
		/// <param name="profiles"></param>
		private void ReportZeroes(List<SkillProfile> profiles)
		{
			var zeroes = profiles.Where(p => p.Dps <= 0).ToArray();

			_output.WriteLine("");
			_output.WriteLine($"{zeroes.Length} of {profiles.Count} rows measured no damage:");

			foreach (var group in zeroes.GroupBy(p => (p.Zero, p.Role)).OrderByDescending(g => g.Count()))
			{
				var skills = group.Select(p => p.SkillClassName).Distinct().ToArray();

				_output.WriteLine($"  {group.Count(),5} row(s)  {group.Key.Zero,-18} {group.Key.Role,-8} {skills.Length} skill(s): {string.Join(", ", skills.Take(6))}");
			}
		}

		/// <summary>
		/// Lists the skills that are far off their class's median, which is
		/// the shortlist Phase 4 retunes first.
		/// </summary>
		/// <param name="profiles"></param>
		private void ReportSkillOutliers(List<SkillProfile> profiles)
		{
			var damage = profiles.Where(p => p.Role == SkillRole.Direct).ToArray();

			foreach (var scenario in damage.GroupBy(p => (p.ScenarioId, p.CharacterLevel)).OrderBy(g => g.Key.ScenarioId).ThenBy(g => g.Key.CharacterLevel))
			{
				var median = SweepReport.Median(scenario.Select(p => p.Dps));

				if (median <= 0)
					continue;

				var outliers = scenario
					.Where(p => p.Dps > median * OutlierFactor || (p.Dps > 0 && p.Dps < median / OutlierFactor))
					.OrderByDescending(p => p.Dps)
					.ToArray();

				_output.WriteLine($"");
				_output.WriteLine($"{scenario.Key.ScenarioId} @lv{scenario.Key.CharacterLevel}: median {median:F0} dps, {outliers.Length} outliers beyond {OutlierFactor}x");

				foreach (var profile in outliers.Take(10))
					_output.WriteLine($"  {profile.Dps / median,6:F2}x  {profile.JobPrefix,-14} {profile.SkillClassName,-32} {profile.Dps,8:F0} dps, {profile.TargetsReached} target(s)");
			}
		}

		/// <summary>
		/// Lists the buffs whose effect is far from what the rest of them do.
		/// </summary>
		/// <param name="effects"></param>
		private void ReportBuffOutliers(BuffEffect[] effects)
		{
			foreach (var slot in effects.GroupBy(e => e.Slot).OrderBy(g => g.Key))
			{
				var median = SweepReport.Median(slot.Select(e => Math.Abs(e.Ratio - 1f)));

				_output.WriteLine($"");
				_output.WriteLine($"{slot.Key}: {slot.Count()} with an effect, median swing {median:P1}");

				var strongest = slot.OrderByDescending(e => Math.Abs(e.Ratio - 1f)).Take(15);

				foreach (var effect in strongest)
					_output.WriteLine($"  {effect.Ratio,7:F3}x  {effect.Id,-40} {effect.Owner}");
			}
		}

		/// <summary>
		/// Finds the buffs that read their caster's stats and reports their
		/// whole curve, since one measurement of those says nothing.
		/// </summary>
		/// <param name="effects"></param>
		private void SweepStatScalingBuffs(BuffEffect[] effects)
		{
			var rows = new List<string>();
			var scaling = 0;

			foreach (var group in effects.GroupBy(e => (e.Id, e.Slot)))
			{
				var entry = BuffCatalog.Entries.First(e => e.Id == group.Key.Id);

				if (!BuffProbe.ScalesWithCasterStat(entry, group.Key.Slot))
					continue;

				++scaling;

				foreach (var effect in BuffProbe.SweepCasterStat(entry, group.Key.Slot))
					rows.Add(SweepReport.Row(effect.Id, effect.Owner, effect.Slot, effect.CasterInt, effect.Baseline, effect.Buffed, effect.Ratio));
			}

			var path = SweepReport.Write("buff-stat-scaling",
				"buff,owner,slot,casterInt,baseline,buffed,ratio",
				rows);

			_output.WriteLine($"");
			_output.WriteLine($"{scaling} buff/slot pairs scale with caster INT, swept at {string.Join("/", BuffProbe.StatSweepValues)} -> {path}");
		}

		/// <summary>
		/// Returns every subset of the given entries.
		/// </summary>
		/// <param name="entries"></param>
		private static IEnumerable<BuffEntry[]> PowerSet(BuffEntry[] entries)
		{
			var total = 1 << entries.Length;

			for (var mask = 0; mask < total; ++mask)
			{
				var subset = new List<BuffEntry>();

				for (var i = 0; i < entries.Length; ++i)
				{
					if ((mask & (1 << i)) != 0)
						subset.Add(entries[i]);
				}

				yield return subset.ToArray();
			}
		}

		private static int[] ReadInts(string variable, int[] fallback)
		{
			var raw = Environment.GetEnvironmentVariable(variable);

			if (string.IsNullOrWhiteSpace(raw))
				return fallback;

			return raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(part => int.Parse(part.Trim()))
				.ToArray();
		}

		private bool Skip(string name)
		{
			if (Enabled)
				return true;

			_output.WriteLine($"{name} skipped. Set {EnableVariable}=1 to run it.");

			return false;
		}
	}
}
