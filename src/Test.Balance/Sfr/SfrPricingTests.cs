using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// The SFR pricing pass, and the guard that keeps its anchor still.
	/// </summary>
	/// <remarks>
	/// Nothing here prices without a live measurement any more, so every test
	/// in this class needs the headless ZoneServer BalanceHost boots.
	/// PriceRoster is the generation procedure proper, and is a multi-minute
	/// run rather than a sub-second one. Only the write is opt-in, because it
	/// rewrites skills_overrides.txt.
	/// </remarks>
	[Collection(BalanceCollection.Name)]
	public class SfrPricingTests
	{
		/// <summary>
		/// Environment variable that lets the pass write.
		/// </summary>
		public const string ApplyVariable = "BALANCE_SFR_APPLY";

		/// <summary>
		/// Environment variable naming one skill to explain instead of the
		/// whole roster.
		/// </summary>
		public const string SkillVariable = "BALANCE_SFR_SKILL";

		/// <summary>
		/// Report the roster run leaves behind, alongside the sweep's own.
		/// </summary>
		public const string ReportName = "sfr-prices.md";

		private readonly ITestOutputHelper _output;
		private readonly List<string> _lines = [];

		/// <summary>
		/// Creates the fixture.
		/// </summary>
		/// <param name="host"></param>
		/// <param name="output"></param>
		public SfrPricingTests(BalanceHost host, ITestOutputHelper output)
			=> _output = output;

		/// <summary>
		/// Writes a line to the test output and to the report being built.
		/// </summary>
		/// <remarks>
		/// The report file is the reliable channel. dotnet test shows
		/// ITestOutputHelper only at detailed logger verbosity, and xunit
		/// captures both stdout and stderr, so there is no way to print here by
		/// default.
		/// </remarks>
		/// <param name="line"></param>
		private void Write(string line = "")
		{
			_output.WriteLine(line);
			_lines.Add(line);
		}

		/// <summary>
		/// Saves everything written so far next to the sweep's reports, so the
		/// run leaves a record whatever the logger is set to.
		/// </summary>
		private string SaveReport()
		{
			// Anchored on the project root rather than the working directory:
			// nothing boots the server here, so the run never navigates there.
			var directory = Path.Combine(SfrData.Root, SweepReport.OutputDirectory);

			Directory.CreateDirectory(directory);

			var path = Path.Combine(directory, ReportName);

			File.WriteAllText(path, string.Join(Environment.NewLine, _lines), Encoding.UTF8);

			return Path.GetFullPath(path);
		}

		/// <summary>
		/// The anchor holds the roster's level, so it moving means every other
		/// skill moved with it.
		/// </summary>
		[Fact]
		public void AnchorHoldsItsFactor()
		{
			if (!BalanceSuites.SfrEnabled)
			{
				_output.WriteLine(BalanceSuites.SkipMessage(BalanceSuites.SfrVariable));
				return;
			}

			using var pool = new ArenaPool(SfrDials.ExplainPoolSize);

			var press = SkillPressProbe.MeasureAll(SfrDials.AnchorSkill, measureDefense: false, pool: pool);
			SfrPricer.SetAnchorMeasurement(press);

			var price = SfrPricer.Price(SfrDials.AnchorSkill, null, press);

			Assert.Equal((int)SfrDials.AnchorFactor, price.Factor);

			// calc_skill.cs reads factor + factorByLevel * level, so this is
			// what the skill actually deals the moment it is learned.
			var levelOne = price.Factor + price.FactorByLevel;

			Assert.InRange(levelOne, 113f, 117f);
		}

		/// <summary>
		/// Every scenario the weights name has to exist in the matrix, and
		/// every priced scenario has to carry a weight.
		/// </summary>
		[Fact]
		public void ScenarioWeightsMatchTheMatrix()
		{
			if (!BalanceSuites.SfrEnabled)
			{
				_output.WriteLine(BalanceSuites.SkipMessage(BalanceSuites.SfrVariable));
				return;
			}

			var matrix = ScenarioMatrix.All.Select(s => s.Id).ToHashSet();

			foreach (var id in SfrDials.ScenarioWeights.Keys)
				Assert.Contains(id, matrix);

			foreach (var id in SfrDials.SpreadScenarios)
				Assert.Contains(id, SfrDials.ScenarioWeights.Keys);

			foreach (var id in SfrDials.PeakScenarios)
				Assert.Contains(id, SfrDials.ScenarioWeights.Keys);

			Assert.Equal(SfrDials.ScenarioWeights.Count, SfrGeometry.PricedScenarios.Count());
		}

		/// <summary>
		/// Prices the roster, and writes it when the apply variable is set.
		/// </summary>
		[Fact]
		public void PriceRoster()
		{
			if (!BalanceSuites.SfrEnabled)
			{
				_output.WriteLine(BalanceSuites.SkipMessage(BalanceSuites.SfrVariable));
				return;
			}

			var single = Environment.GetEnvironmentVariable(SkillVariable);

			if (!string.IsNullOrEmpty(single))
			{
				Explain(single);
				return;
			}

			var write = Environment.GetEnvironmentVariable(ApplyVariable) == "1";
			var started = DateTime.UtcNow;
			var result = SfrPricer.ApplyAll(write);

			Write($"{result.Changes.Count} skills priced, {result.SpChanges.Count} SP costs priced, {result.NotPriceable} not priceable, " +
				$"simulated in {(DateTime.UtcNow - started).TotalMinutes:0.0} min " +
				$"({SfrPricer.LastPoolBuildTime.TotalSeconds:0}s building {SfrDials.ArenaPoolSize} arenas, " +
				$"{SfrPricer.LastMeasureTime.TotalSeconds:0}s measuring on {SfrDials.SkillWorkers} workers)");
			Write("");
			Write($"{"circle",-8}{"count",6}{"cap",8}{"premium",9}{"slope",9}  median factor");

			foreach (var circle in result.Changes.Keys.Select(SfrData.SkillCircle).Distinct().OrderBy(c => c))
			{
				var named = result.Changes.Keys.Where(n => SfrData.SkillCircle(n) == circle).ToArray();
				var caps = named.Select(SfrData.SkillMaxLevel).Distinct().OrderBy(v => v);
				var factors = named.Select(n => result.Changes[n].Factor).OrderBy(f => f).ToArray();
				var premiums = named.Select(SfrData.CirclePremium).Distinct().OrderBy(v => v);
				var shares = named.Select(SfrData.SlopeShare).Distinct().OrderBy(v => v);

				Write($"{circle,-8}{named.Length,6}{string.Join("/", caps),8}{string.Join("/", premiums.Select(v => v.ToString("0.00"))),9}"
					+ $"{string.Join("/", shares.Select(v => v.ToString("0.00"))),9}  {factors[factors.Length / 2]}");
			}

			var untreed = result.Changes.Keys.Where(n => !SfrData.HasTreeRow(n)).OrderBy(n => n).ToArray();

			if (untreed.Length > 0)
			{
				Write("");
				Write($"{untreed.Length} skill(s) have no skilltree row - priced as circle 1: {string.Join(", ", untreed)}");
			}

			Write("");
			Write("largest raises (ratio is against the old value, and only reported):");

			foreach (var pair in result.Changes.OrderByDescending(c => c.Value.Ratio).Take(12))
				Write($"   {pair.Key,-34} x{pair.Value.Ratio:0.00} -> factor {pair.Value.Factor}, factorByLevel {pair.Value.FactorByLevel:0.0}");

			Write("");
			Write("largest cuts:");

			foreach (var pair in result.Changes.OrderBy(c => c.Value.Ratio).Take(12))
				Write($"   {pair.Key,-34} x{pair.Value.Ratio:0.00} -> factor {pair.Value.Factor}, factorByLevel {pair.Value.FactorByLevel:0.0}");

			if (result.SpChanges.Count > 0)
			{
				var costs = result.SpChanges.Values.Select(c => c.Sp).OrderBy(v => v).ToArray();

				Write("");
				Write($"SP costs: median {costs[costs.Length / 2]}, range {costs[0]}-{costs[^1]}"
					+ $"  (anchor {SfrDials.SpAnchorCost:0}, arcane x{SfrDials.SpArcaneMultiplier:0.00},"
					+ $" circle x{string.Join("/", SfrDials.CirclePremium.OrderBy(p => p.Key).Select(p => p.Value.ToString("0.00")))},"
					+ $" buff x{SfrDials.SpBuffMultiplier:0.0},"
					+ $" channel x{SfrDials.SpChannelMultiplier:0.00})");
				Write("largest SP raises:");

				foreach (var pair in result.SpChanges.OrderByDescending(c => c.Value.Ratio).Take(8))
					Write($"   {pair.Key,-34} x{pair.Value.Ratio:0.00} -> basicSp {pair.Value.Sp}, lvUpSpendSp {pair.Value.SpByLevel}");

				Write("largest SP cuts:");

				foreach (var pair in result.SpChanges.OrderBy(c => c.Value.Ratio).Take(8))
					Write($"   {pair.Key,-34} x{pair.Value.Ratio:0.00} -> basicSp {pair.Value.Sp}, lvUpSpendSp {pair.Value.SpByLevel}");
			}

			if (result.SpRepeatCharges.Count > 0)
			{
				Write("");
				Write($"{result.SpRepeatCharges.Count} skill(s) charge their SP cost more than once per press - the written cost is per charge:");

				foreach (var entry in result.SpRepeatCharges.OrderByDescending(s => s.Charges))
					Write($"   {entry.Skill,-34} {entry.Charges:0.0} charges -> basicSp {entry.Sp}");
			}

			if (result.SpUnmeasuredChannels.Count > 0)
			{
				Write("");
				Write($"{result.SpUnmeasuredChannels.Count} channel(s) kept their SP cost - the press was never measured, so how often it charges is unknown:");

				foreach (var entry in result.SpUnmeasuredChannels.OrderBy(s => s.Skill))
					Write($"   {entry.Skill,-34} basicSp {entry.OldSp:0.#}");
			}

			if (result.NewlyPriced.Count > 0)
			{
				Write("");
				Write($"{result.NewlyPriced.Count} skill(s) came off factor 0 - a live press showed them dealing damage:");

				foreach (var entry in result.NewlyPriced.OrderByDescending(n => n.Factor))
					Write($"   {entry.Skill,-34} factor {entry.Factor}");
			}

			if (result.Overrunning.Count > 0)
			{
				Write("");
				Write($"{result.Overrunning.Count} skill(s) still delivering when their own cycle was up - counted over the cycle:");

				foreach (var entry in result.Overrunning.OrderByDescending(o => o.Span - o.Cycle))
					Write($"   {entry.Skill,-34} span {entry.Span:0.0}s over a {entry.Cycle:0.0}s cycle, {entry.Hits:0.0} hit(s) counted");
			}

			if (result.Unmeasured.Count > 0)
			{
				Write("");
				Write($"NOT MEASURED - {result.Unmeasured.Count} skill(s) kept their existing factor:");

				foreach (var group in result.Unmeasured.GroupBy(u => u.Reason).OrderByDescending(g => g.Count()))
				{
					Write($"   {group.Key}:");

					foreach (var entry in group.OrderBy(u => u.Skill))
						Write($"      {entry.Skill,-34} factor {entry.OldFactor:0.#}");
				}
			}

			Write("");
			Write(write ? "written to " + SfrData.OverridesPath : "dry run - nothing written");

			_output.WriteLine("report saved to " + SaveReport());

			Assert.NotEmpty(result.Changes);
		}

		/// <summary>
		/// Environment variable that also runs the defensive/CC probe for the
		/// explained skill. Off by default: it costs SfrDials.DefenseProbeTrials
		/// full encounter windows on its own, which defeats the point of this
		/// being the fast single-skill loop.
		/// </summary>
		public const string DefenseVariable = "BALANCE_SFR_DEFENSE";

		/// <summary>
		/// Writes out every term behind one skill's price, measuring only that
		/// skill.
		/// </summary>
		/// <remarks>
		/// This is the fast loop the model's iterated against: one skill,
		/// across the priced scenario matrix, rather than the full roster.
		/// It calibrates against a freshly measured anchor - the anchor costs
		/// a second full skill's worth of scenarios, but there is no cheaper
		/// scale left to fall back to now that nothing here scans a handler.
		///
		/// Both measurements run on an arena pool, and against each other, so
		/// the wall time is the longest single window rather than the sum of
		/// forty of them. Unpooled, MeasureAll runs its scenarios, factor
		/// points and defence trials one at a time, and SfrDefenseProbe takes
		/// its serial path, which pays the full DefenseProbeTrials on every
		/// skill instead of scouting a no-CC press out in two pairs.
		/// </remarks>
		/// <param name="skillName"></param>
		private void Explain(string skillName)
		{
			var measureDefense = Environment.GetEnvironmentVariable(DefenseVariable) == "1";

			using var pool = new ArenaPool(SfrDials.ExplainPoolSize);

			SfrMeasuredPress press = null;
			var anchorPresses = new SfrMeasuredPress[SfrDials.AnchorTrials];
			Exception failure = null;

			var work = new List<Action>
			{
				() =>
				{
					try
					{
						press = SkillPressProbe.MeasureAll(skillName, measureDefense: measureDefense, pool: pool);
					}
					catch (Exception ex)
					{
						failure = ex;
					}
				},
			};

			// The anchor is measured as many times here as the roster run
			// measures it, so the two calibrate on the same statistic and an
			// explained factor matches the written one.
			for (var trial = 0; trial < SfrDials.AnchorTrials; ++trial)
			{
				var at = trial;
				work.Add(() => anchorPresses[at] = SkillPressProbe.MeasureAll(SfrDials.AnchorSkill, measureDefense: false, pool: pool));
			}

			SkillPressProbe.RunAll(work.ToArray());

			if (failure != null)
			{
				Write($"{skillName} could not be measured live: {failure.Message}");
				_output.WriteLine("report saved to " + SaveReport());
				return;
			}

			SfrPricer.CalibrateOnMedian(anchorPresses);

			Write($"measured: DirectHits {press.DirectHits} (fromDamage {press.HitsFromDamage}), " +
				$"HitsTruncated {press.HitsTruncated}, Delivered {press.Delivered}");

			if (press.HitsFailure != null)
				Write($"  hit count failed: {press.HitsFailure}");

			foreach (var scenario in press.Scenarios.OrderBy(s => s.Key))
			{
				var s = scenario.Value;
				Write($"  {s.ScenarioId}: {s}");
			}

			SfrPrice r;

			try
			{
				r = SfrPricer.Price(skillName, null, press);
			}
			catch (Exception ex)
			{
				Write($"{skillName}: {ex.Message}");
				_output.WriteLine("report saved to " + SaveReport());
				return;
			}

			Write($"{r.Skill}  ({r.Class}, circle {r.Circle}, max level {r.Levels}, circle premium x{r.CirclePremium:0.00}, "
				+ $"slope share {SfrData.SlopeShare(r.Skill):0.00}, channel x{r.ChannelPremium:0.00})");
			Write($"  occupancy t   {r.Occupancy:0.00} s      cycle T {r.Cycle:0.00} s      u {r.Utilization:0.00}");
			Write($"  hits/press   {r.Hits:0.0}      basic swings/s {r.BasicRate:0.00}");
			Write($"  damage span  {r.DamageSpan:0.0} s   burst {r.BurstFraction:0.00} of total   divisor {r.Divisor:0.0}");
			Write($"  counted over {r.CountWindow:0.0} s of a {r.FullDamageSpan:0.0} s delivery"
				+ (r.Overruns ? "   OVERRUNS ITS CYCLE" : ""));
			Write($"  riders        {(r.RiderKinds.Length > 0 ? string.Join(", ", r.RiderKinds) : "none")} (x{r.RiderMultiplier:0.00})");
			Write($"  e ceiling     {r.Efficiency:0.00}   ({(r.IsChannel ? "channel" : "cast")} {r.Cast:0.00} s)");
			Write($"  cast premium  x{r.CastPremium:0.00}  ({(r.CastPremiumKinds.Length > 0 ? string.Join(", ", r.CastPremiumKinds) : "none")})");
			Write("  targets       " + string.Join("  ", r.Targets.OrderBy(t => t.Key.Length).ThenBy(t => t.Key)
				.Select(t => $"{t.Key} {t.Value.Mine:0.0}/{t.Value.Theirs:0.0}")));
			Write($"  weighted reach {r.WeightedReach:0.00}  peak-blended {r.ChargedReach:0.00}"
				+ $"  charged as {MathF.Pow(Math.Max(r.ChargedReach, 1e-6f), SfrDials.AoeExponent):0.00}"
				+ (r.SpreadCapped ? "   SPREAD-CAPPED" : ""));
			Write("");
			Write($"  total SFR at max level   {r.Sfr:0}%");
			Write($"  factor: {r.Factor}, factorByLevel: {r.FactorByLevel:0.0}");
			Write($"  SP target {r.Sp.Target:0.0} over {r.Sp.Charges:0.0} charge(s)"
				+ $"{(r.Sp.Measured ? " (measured)" : " (assumed)")}"
				+ $"  {(r.Sp.Kinds.Length > 0 ? string.Join(", ", r.Sp.Kinds) : "plain")}");
			Write($"  basicSp: {r.Sp.Cost}, lvUpSpendSp: {r.Sp.CostByLevel}");

			_output.WriteLine("report saved to " + SaveReport());
		}
	}
}
