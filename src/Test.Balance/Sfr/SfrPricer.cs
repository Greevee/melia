using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// Prices a skill's SFR from its data and handler alone, with no prior
	/// factor.
	/// </summary>
	/// <remarks>
	/// Implements the model in SFR_OVERHAUL.md: a press is worth the basic
	/// attacks its whole cycle costs you, times how much better than swinging
	/// it is, divided by how wide it reaches. Nothing here reads the skill's
	/// current factor, so the pass is idempotent and does not compound with
	/// itself.
	/// </remarks>
	public static class SfrPricer
	{
		private static float? _calibration;
		private static readonly object _syncLock = new();

		/// <summary>
		/// Returns the scale that puts the anchor skill back on its factor.
		/// </summary>
		/// <remarks>
		/// Every term in the model is a ratio, so one anchor sets the level for
		/// the roster. Charging for width divides every price by a number above
		/// one, which would drop the whole roster rather than redistribute it.
		/// </remarks>
		public static float Calibration()
		{
			lock (_syncLock)
			{
				if (_calibration != null)
					return _calibration.Value;

				// Priced uncalibrated first, which is what the scale is measured against.
				_calibration = 1f;

				try
				{
					var raw = Price(SfrDials.AnchorSkill);
					_calibration = SfrDials.AnchorFactor / Math.Max(raw.Factor, 1);
				}
				catch (Exception)
				{
					_calibration = 1f;
				}

				return _calibration.Value;
			}
		}

		/// <summary>
		/// Returns the absolute SFR for a skill, from its data and handler only.
		/// </summary>
		/// <param name="skillName"></param>
		/// <param name="maxLevel"></param>
		public static SfrPrice Price(string skillName, int? maxLevel = null)
		{
			if (!SfrData.Skills.TryGetValue(skillName, out var entry))
				throw new KeyNotFoundException($"No skill data for '{skillName}'.");

			var cls = SfrData.ClassOf(skillName);

			if (!SfrData.BaseJob.ContainsKey(cls))
				throw new KeyNotFoundException($"{skillName}: '{cls}' is out of scope.");

			var levels = maxLevel ?? SfrData.SkillMaxLevel(skillName);
			var cast = entry.Num("basicCast") / 1000f;
			var shoot = entry.Num("shootTime") / 1000f;

			// A channel's shootTime is the longest it may be held, not what a
			// press costs, so the pad that delivers the damage bounds it.
			var padLife = PadLifetime(skillName, levels);
			if (shoot * 1000f >= SfrDials.ChannelShootMs && padLife > 0)
				shoot = Math.Min(shoot, padLife);

			// Cast time replaces the animation rather than adding to it: a skill
			// that casts is committed for the cast, and its shootTime is the
			// follow-through.
			var rawOccupancy = Math.Max(SfrDials.MinOccupancy, cast > 0 ? cast : shoot);
			var t = Math.Min(rawOccupancy, SfrDials.MaxOccupancy);
			var cycle = CycleSeconds(entry, t);

			var directHits = SfrHandlerAnalysis.HitsPerCast(skillName);
			var pads = SfrHandlerAnalysis.PadHits(skillName, levels);
			var buffs = SfrHandlerAnalysis.BuffHits(skillName, levels);
			var (dot, dotBuff, dotTicks) = SfrHandlerAnalysis.DotMultiple(skillName, levels);

			var riders = Riders();
			var riderMultiplier = 1f;
			var riderKinds = Array.Empty<string>();

			if (riders.TryGetValue(skillName, out var rider))
				(riderMultiplier, riderKinds) = rider;

			var buffTotal = buffs.Total;
			var buffUsed = buffs.Used;

			// The DoT multiple already prices this buff's ticks; counting them
			// here as well would divide the budget by the same hits twice.
			if (dotBuff != null)
			{
				buffTotal -= buffUsed.Where(b => b.Buff == dotBuff).Sum(b => b.Ticks);
				buffUsed = buffUsed.Where(b => b.Buff != dotBuff).ToList();
			}

			// A pad's ticks and a debuff's ticks are part of what one press
			// buys, so they belong in the same divisor as the direct hits.
			var hits = directHits + pads.Total + buffTotal;
			var dotDuration = dotBuff != null ? dotTicks * SfrData.BuffUpdateTime(dotBuff) : 0f;

			// A quantified DoT replaces the blanket rider, which exists only
			// because the multiple was unknown.
			if (dot > 0 && riderKinds.Contains("dot"))
				riderMultiplier = Math.Min(1f, riderMultiplier / SfrDials.DotRiderMultiplier);

			var channel = SfrHandlerAnalysis.IsChannel(skillName, entry);
			var (castPremium, premiumKinds) = CastPremium(entry, cast, channel);

			// Nothing here reads the occupancy on its own, so it pays for itself
			// only through the cycle and a cast lands at DPS parity with an
			// instant press. The ceiling bounds efficiency, not the cast.
			var efficiency = Math.Min(SfrDials.MaxEfficiency,
				SfrDials.BaseInstantEfficiency * CycleGate(cycle - t) * riderMultiplier);

			var cappedEfficiency = efficiency * castPremium;

			var shape = entry.Clone();
			SfrHandlerAnalysis.ApplyHandlerGeometry(skillName, shape);

			// A pad skill's geometry is read off the area the handler hands the
			// pad, so the pad's own capacity is the matching bound on it.
			var padCap = SfrHandlerAnalysis.PadTargetCap(skillName, levels);
			if (padCap > 0)
				shape.TargetCap = Math.Min(shape.TargetCap ?? padCap.Value, padCap.Value);

			// A Force skill's splash is centred on the one target it locked
			// rather than on the caster, so it reaches the same weighted count
			// as a caster-anchored shape less reliably.
			if (shape.Text("useType") == "Force" && shape.HandlerArea)
				cappedEfficiency *= SfrDials.ForceAoePremium;

			var reach = new Dictionary<string, float>();
			var targets = new Dictionary<string, (int Mine, float Theirs)>();

			foreach (var spec in SfrGeometry.PricedScenarios)
			{
				var offsets = SfrGeometry.Placement(spec, cast, out var aim);
				var mine = SfrGeometry.SplashTargets(shape, offsets, aim);
				var theirs = SfrGeometry.GenericBasicReach(offsets, aim);

				targets[spec.Id] = (mine, theirs);
				reach[spec.Id] = mine / Math.Max(theirs, SfrDials.MinYardstick);
			}

			// What one press is worth across the scenarios a player actually
			// meets, rather than on the single target of S1. Dividing by it is
			// what pays a narrow skill for being narrow.
			var weightSum = reach.Keys.Sum(s => SfrDials.ScenarioWeights[s]);
			var weighted = reach.Sum(r => SfrDials.ScenarioWeights[r.Key] * r.Value) / weightSum;
			var width = MathF.Pow(Math.Max(weighted, 1e-6f), SfrDials.AoeExponent);

			var basicRate = SfrData.GenericBasicRate();
			var baseSfr = Calibration() * cappedEfficiency * basicRate * cycle * 100f / width;

			// The spread gate is the multi-target ceiling: whatever the skill
			// does when everything is gathered may not exceed the cap times what
			// it does in the weighted-typical case.
			var widest = SfrDials.SpreadScenarios.Where(reach.ContainsKey).Select(s => reach[s]).DefaultIfEmpty(0f).Max();
			var gateSfr = widest > 0 ? baseSfr * SfrDials.SpreadCap * weighted / widest : baseSfr;
			var sfr = Math.Min(baseSfr, gateSfr);

			// A DoT does not stack, so one instance runs continuously and its
			// share of a press is the fraction of its duration the cycle covers.
			var dotShare = dot > 0 && dotDuration > 0 ? dot * Math.Min(1f, cycle / dotDuration) : 0f;

			sfr /= 1f + dotShare;
			sfr /= SfrDials.CritAllowance;

			// What advancing a circle buys beyond a later skill reaching its cap
			// in fewer points, so the circles carry an incentive of their own.
			var circlePremium = SfrData.CirclePremium(skillName);
			sfr *= circlePremium;

			var factor = sfr / Math.Max(hits, 1f) / SfrDials.LevelGrowth;

			return new SfrPrice
			{
				Skill = skillName,
				Class = cls,
				Levels = levels,
				Circle = SfrData.SkillCircle(skillName),
				CirclePremium = circlePremium,
				Occupancy = t,
				RawOccupancy = rawOccupancy,
				Cycle = cycle,
				Utilization = Math.Min(1f, t / cycle),
				DirectHits = directHits,
				PadHits = pads.Total,
				PadDetail = pads.Used,
				PadUnknown = pads.Unknown,
				BuffHits = buffTotal,
				BuffDetail = buffUsed,
				Hits = hits,
				Dot = dot,
				DotBuff = dotBuff,
				DotShare = dotShare,
				Efficiency = cappedEfficiency,
				GateEfficiency = efficiency,
				Cast = cast,
				CastPremium = castPremium,
				CastPremiumKinds = premiumKinds,
				IsChannel = channel,
				RiderMultiplier = riderMultiplier,
				RiderKinds = riderKinds,
				BasicRate = basicRate,
				Reach = reach,
				Targets = targets,
				WeightedReach = weighted,
				Sfr = sfr,
				SpreadCapped = gateSfr < baseSfr,
				Factor = (int)Math.Round(factor),
				FactorByLevel = MathF.Round(factor / Math.Max(1, levels), 1),
			};
		}

		/// <summary>
		/// Returns the seconds between presses: a full overheat volley plus its
		/// cooldown, per press.
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="occupancy"></param>
		public static float CycleSeconds(SkillEntryData entry, float occupancy)
		{
			var overheat = Math.Max(1f, entry.Num("overheatCount", 1f));
			var cooldown = entry.Num("cooldownTime") / 1000f;

			return Math.Max(occupancy, (overheat * occupancy + cooldown) / overheat);
		}

		/// <summary>
		/// Returns what waiting out a cooldown is worth, keyed on the wait
		/// alone.
		/// </summary>
		/// <remarks>
		/// The numerator is the SFR a wait is meant to buy and the denominator
		/// cancels the cycle the price already multiplies by, leaving the
		/// sub-parity discount that makes cooldown-gated burst worth less DPS
		/// than sustain. Normalized so an idle of zero scores one, which is what
		/// makes the base efficiency a spam skill's ceiling.
		/// </remarks>
		/// <param name="idle"></param>
		public static float CycleGate(float idle)
			=> SfrDials.ReferenceOccupancy * CooldownSfr(idle) / (SfrDials.ReferenceOccupancy + idle);

		/// <summary>
		/// Returns the SFR multiple a wait of the given seconds buys, against a
		/// zero-cooldown press.
		/// </summary>
		/// <remarks>
		/// Linear, so every second of cooldown is worth the same fixed slice of
		/// SFR. Below the ramp the line is discounted, because a button with no
		/// cooldown and no overheat is not gated by anything but SP.
		/// </remarks>
		/// <param name="idle"></param>
		public static float CooldownSfr(float idle)
			=> (1f + idle * SfrDials.CooldownSfrPerSecond) * NoGateDiscount(idle);

		/// <summary>
		/// Returns the discount on an ungated button, fading out as soon as a
		/// real cooldown exists.
		/// </summary>
		/// <param name="idle"></param>
		public static float NoGateDiscount(float idle)
		{
			if (idle >= SfrDials.NoGateRamp)
				return 1f;

			return SfrDials.NoGatePenalty + (1f - SfrDials.NoGatePenalty) * (idle / SfrDials.NoGateRamp);
		}

		/// <summary>
		/// Returns what a wind-up risks that its duration alone does not
		/// describe.
		/// </summary>
		/// <remarks>
		/// The cycle already pays a cast back to DPS parity with an instant
		/// press, so everything here is the premium above parity. A channel
		/// earns none of it: it is already delivering damage while it runs.
		/// </remarks>
		/// <param name="entry"></param>
		/// <param name="castSeconds"></param>
		/// <param name="channel"></param>
		public static (float Premium, string[] Kinds) CastPremium(SkillEntryData entry, float castSeconds, bool channel)
		{
			if (channel || castSeconds <= 0)
				return (1f, []);

			var premium = MathF.Pow(1f + castSeconds, SfrDials.CastLengthExponent);
			var kinds = new List<string> { $"cast {castSeconds:0.0}s x{premium:0.00}" };

			if (entry.Flag("castInterruptible"))
			{
				premium *= SfrDials.InterruptiblePremium;
				kinds.Add("interruptible");
			}

			if (!entry.Flag("enableCastMove"))
			{
				premium *= SfrDials.NoCastMovePremium;
				kinds.Add("rooted");
			}

			if (!entry.Flag("speedRateAffectedByDex"))
			{
				premium *= SfrDials.NoDexScalingPremium;
				kinds.Add("no-dex");
			}

			return (premium, kinds.ToArray());
		}

		/// <summary>
		/// Returns the longest lifetime among the pads a skill creates, in
		/// seconds.
		/// </summary>
		/// <param name="skillName"></param>
		/// <param name="levels"></param>
		private static float PadLifetime(string skillName, int levels)
		{
			var text = SfrSources.SkillHandler(skillName);
			var longest = 0f;

			foreach (var pad in SfrHandlerAnalysis.PadNames(skillName))
			{
				if (!SfrSources.Pads.TryGetValue(pad, out var info))
					continue;

				var profile = SfrHandlerAnalysis.PadProfile(info, text, levels);

				if (profile.Reason == null)
					longest = Math.Max(longest, profile.Life);
			}

			return longest;
		}

		private static Dictionary<string, (float, string[])> _riders;

		/// <summary>
		/// The rider multipliers, scanned once.
		/// </summary>
		private static Dictionary<string, (float, string[])> Riders()
		{
			lock (_syncLock)
				return _riders ??= SfrHandlerAnalysis.ScanRiders();
		}

		/// <summary>
		/// Warnings that do not block a skill from being written.
		/// </summary>
		private static readonly string[] NonBlocking = ["ceiling saturated"];

		/// <summary>
		/// Prices every skill the model can account for and optionally writes
		/// the result.
		/// </summary>
		/// <remarks>
		/// A factor of zero is the data's marker for a skill that deals no
		/// damage, not a value to price against. Nothing else here reads the
		/// current factor.
		/// </remarks>
		/// <param name="write"></param>
		public static SfrApplyResult ApplyAll(bool write)
		{
			var result = new SfrApplyResult();
			var lines = File.ReadAllLines(SfrData.OverridesPath);
			var priced = new Dictionary<string, SfrPrice>();

			foreach (var line in lines)
			{
				var name = Regex.Match(line, @"className: ""([^""]+)""");
				if (!name.Success)
					continue;

				var skillName = name.Groups[1].Value;
				var currentFactor = Regex.Match(line, @"\bfactor: ([0-9.]+)");

				if (!currentFactor.Success || SfrData.ParseFloat(currentFactor.Groups[1].Value) == 0)
					continue;

				var cls = SfrData.ClassOf(skillName);

				if (!SfrData.BaseJob.ContainsKey(cls) || !SfrData.Scope.Contains(cls))
					continue;

				SfrPrice price;

				try
				{
					price = Price(skillName);
				}
				catch (Exception)
				{
					result.NotPriceable++;
					continue;
				}

				var warnings = SfrHandlerAnalysis.DeliveryWarnings(skillName)
					.Where(w => !NonBlocking.Any(w.StartsWith))
					.ToList();

				if (price.RawOccupancy > SfrDials.MaxOccupancy)
					warnings.Insert(0, $"occupies {price.RawOccupancy:0}s per press - a duration, not an animation");

				if (price.Cast <= 0 && price.RawOccupancy > SfrDials.LongShoot && price.Hits <= 1)
					warnings.Insert(0, $"{price.RawOccupancy:0.0}s animation read as one hit - the hit count is in the animation");

				if (warnings.Count > 0)
				{
					result.NotPriceable++;
					result.Held.Add((skillName, SfrData.ParseFloat(currentFactor.Groups[1].Value), price.Factor, warnings[0]));
					continue;
				}

				priced[skillName] = price;
				result.Changes[skillName] = (price.Factor, price.FactorByLevel,
					price.Factor / Math.Max(SfrData.ParseFloat(currentFactor.Groups[1].Value), 1f));
			}

			if (write)
			{
				var rewritten = lines.Select(line =>
				{
					var name = Regex.Match(line, @"className: ""([^""]+)""");

					if (!name.Success || !priced.TryGetValue(name.Groups[1].Value, out var price))
						return line;

					line = Regex.Replace(line, @"\bfactor: [0-9.]+", "factor: " + price.Factor, RegexOptions.None);
					line = Regex.Replace(line, @"\bfactorByLevel: [0-9.]+",
						"factorByLevel: " + price.FactorByLevel.ToString("0.0", CultureInfo.InvariantCulture));

					return line;
				});

				File.WriteAllLines(SfrData.OverridesPath, rewritten);
			}

			return result;
		}
	}

	/// <summary>
	/// One skill's priced SFR, with every term that produced it.
	/// </summary>
	public class SfrPrice
	{
		public string Skill { get; init; }
		public string Class { get; init; }
		public int Levels { get; init; }
		public int Circle { get; init; }
		public float CirclePremium { get; init; }
		public float Occupancy { get; init; }
		public float RawOccupancy { get; init; }
		public float Cycle { get; init; }
		public float Utilization { get; init; }
		public int DirectHits { get; init; }
		public float PadHits { get; init; }
		public List<(string Pad, int Ticks, float PerTick)> PadDetail { get; init; }
		public List<(string Pad, string Reason)> PadUnknown { get; init; }
		public float BuffHits { get; init; }
		public List<(string Buff, int Ticks, float PerTick)> BuffDetail { get; init; }
		public float Hits { get; init; }
		public float Dot { get; init; }
		public string DotBuff { get; init; }
		public float DotShare { get; init; }
		public float Efficiency { get; init; }
		public float GateEfficiency { get; init; }
		public float Cast { get; init; }
		public float CastPremium { get; init; }
		public string[] CastPremiumKinds { get; init; }
		public bool IsChannel { get; init; }
		public float RiderMultiplier { get; init; }
		public string[] RiderKinds { get; init; }
		public float BasicRate { get; init; }
		public Dictionary<string, float> Reach { get; init; }
		public Dictionary<string, (int Mine, float Theirs)> Targets { get; init; }
		public float WeightedReach { get; init; }
		public float Sfr { get; init; }
		public bool SpreadCapped { get; init; }
		public int Factor { get; init; }
		public float FactorByLevel { get; init; }
	}

	/// <summary>
	/// What a full pricing pass changed, held back, or could not price.
	/// </summary>
	public class SfrApplyResult
	{
		/// <summary>
		/// Skill name to its new factor, factorByLevel and the ratio against
		/// the value it replaced.
		/// </summary>
		public Dictionary<string, (int Factor, float FactorByLevel, float Ratio)> Changes { get; } = [];

		/// <summary>
		/// Skills the model priced but refused to write, with the reason.
		/// </summary>
		public List<(string Skill, float OldFactor, int NewFactor, string Reason)> Held { get; } = [];

		/// <summary>
		/// How many in-scope skills could not be priced at all.
		/// </summary>
		public int NotPriceable { get; set; }
	}
}
