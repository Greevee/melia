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
	/// These read the data and the handler sources as text, so they need
	/// neither a booted server nor a database and run in under a second. Only
	/// the write is opt-in, because it rewrites skills_overrides.txt.
	/// </remarks>
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
		/// <param name="output"></param>
		public SfrPricingTests(ITestOutputHelper output)
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
			var price = SfrPricer.Price(SfrDials.AnchorSkill);

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
			var matrix = ScenarioMatrix.All.Select(s => s.Id).ToHashSet();

			foreach (var id in SfrDials.ScenarioWeights.Keys)
				Assert.Contains(id, matrix);

			foreach (var id in SfrDials.SpreadScenarios)
				Assert.Contains(id, SfrDials.ScenarioWeights.Keys);

			Assert.Equal(SfrDials.ScenarioWeights.Count, SfrGeometry.PricedScenarios.Count());
		}

		/// <summary>
		/// Prices the roster, and writes it when the apply variable is set.
		/// </summary>
		[Fact]
		public void PriceRoster()
		{
			var single = Environment.GetEnvironmentVariable(SkillVariable);

			if (!string.IsNullOrEmpty(single))
			{
				Explain(single);
				return;
			}

			var write = Environment.GetEnvironmentVariable(ApplyVariable) == "1";
			var result = SfrPricer.ApplyAll(write);

			Write($"{result.Changes.Count} skills priced, {result.NotPriceable} not priceable");
			Write("");
			Write($"{"circle",-8}{"count",6}{"cap",8}{"premium",9}  median factor");

			foreach (var circle in result.Changes.Keys.Select(SfrData.SkillCircle).Distinct().OrderBy(c => c))
			{
				var named = result.Changes.Keys.Where(n => SfrData.SkillCircle(n) == circle).ToArray();
				var caps = named.Select(SfrData.SkillMaxLevel).Distinct().OrderBy(v => v);
				var factors = named.Select(n => result.Changes[n].Factor).OrderBy(f => f).ToArray();
				var premium = SfrDials.CirclePremium.TryGetValue(circle, out var p) ? p : 1f;

				Write($"{circle,-8}{named.Length,6}{string.Join("/", caps),8}{premium,9:0.00}  {factors[factors.Length / 2]}");
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

			Write("");
			Write(write ? "written to " + SfrData.OverridesPath : "dry run - nothing written");

			_output.WriteLine("report saved to " + SaveReport());

			Assert.NotEmpty(result.Changes);
		}

		/// <summary>
		/// Writes out every term behind one skill's price.
		/// </summary>
		/// <param name="skillName"></param>
		private void Explain(string skillName)
		{
			var r = SfrPricer.Price(skillName);

			Write($"{r.Skill}  ({r.Class}, circle {r.Circle}, max level {r.Levels}, circle premium x{r.CirclePremium:0.00})");
			Write($"  occupancy t   {r.Occupancy:0.00} s      cycle T {r.Cycle:0.00} s      u {r.Utilization:0.00}");
			Write($"  hits/press   {r.Hits:0.0} (direct {r.DirectHits} + pads {r.PadHits:0.0})      basic swings/s {r.BasicRate:0.00}");
			Write($"  riders        {(r.RiderKinds.Length > 0 ? string.Join(", ", r.RiderKinds) : "none")} (x{r.RiderMultiplier:0.00})");

			if (r.Dot > 0)
				Write($"  dot           {r.DotBuff}, {r.Dot:0.0}x the direct hit, {r.DotShare * 100:0}% of a press");

			Write($"  e ceiling     {r.Efficiency:0.00}   ({(r.IsChannel ? "channel" : "cast")} {r.Cast:0.00} s)");
			Write($"  cast premium  x{r.CastPremium:0.00}  ({(r.CastPremiumKinds.Length > 0 ? string.Join(", ", r.CastPremiumKinds) : "none")})");
			Write("  targets       " + string.Join("  ", r.Targets.OrderBy(t => t.Key.Length).ThenBy(t => t.Key)
				.Select(t => $"{t.Key} {t.Value.Mine}/{t.Value.Theirs:0.0}")));
			Write($"  weighted reach {r.WeightedReach:0.00}  charged as {MathF.Pow(Math.Max(r.WeightedReach, 1e-6f), SfrDials.AoeExponent):0.00}"
				+ (r.SpreadCapped ? "   SPREAD-CAPPED" : ""));
			Write("");
			Write($"  total SFR at max level   {r.Sfr:0}%");
			Write($"  factor: {r.Factor}, factorByLevel: {r.FactorByLevel:0.0}");

			foreach (var warning in SfrHandlerAnalysis.DeliveryWarnings(skillName))
				Write("    - " + warning);

			_output.WriteLine("report saved to " + SaveReport());
		}
	}
}
