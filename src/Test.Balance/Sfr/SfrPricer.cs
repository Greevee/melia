using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
		/// <summary>
		/// How long the last roster run spent building its arenas, which is
		/// CPU rather than the waiting the rest of a run is.
		/// </summary>
		public static TimeSpan LastPoolBuildTime { get; private set; }

		/// <summary>
		/// How long the last roster run spent actually measuring presses.
		/// </summary>
		public static TimeSpan LastMeasureTime { get; private set; }

		private static float? _measuredCalibration;
		private static SfrMeasuredPress _anchorMeasurement;
		private static readonly object _syncLock = new();

		/// <summary>
		/// The measured press for the anchor skill, once one has been taken.
		/// </summary>
		public static SfrMeasuredPress AnchorMeasurement
		{
			get
			{
				lock (_syncLock)
					return _anchorMeasurement;
			}
		}

		/// <summary>
		/// Registers a measured press for the anchor skill, so measured prices
		/// calibrate against a measured anchor.
		/// </summary>
		/// <param name="measured"></param>
		public static void SetAnchorMeasurement(SfrMeasuredPress measured)
		{
			if (measured != null && measured.Skill != SfrDials.AnchorSkill)
				throw new ArgumentException($"The anchor is '{SfrDials.AnchorSkill}', not '{measured.Skill}'.", nameof(measured));

			lock (_syncLock)
			{
				_anchorMeasurement = measured;
				_measuredCalibration = null;
			}
		}

		/// <summary>
		/// Returns the scale that puts the anchor skill back on its factor.
		/// </summary>
		/// <remarks>
		/// Every term in the model is a ratio, so one anchor sets the level for
		/// the roster. Charging for width divides every price by a number above
		/// one, which would drop the whole roster rather than redistribute it.
		/// There is one calibration now, against the measured anchor - nothing
		/// prices without a measured press any more, so there is no second
		/// scale to keep in sync with it.
		/// </remarks>
		public static float Calibration()
		{
			lock (_syncLock)
			{
				if (_measuredCalibration != null)
					return _measuredCalibration.Value;

				if (_anchorMeasurement == null)
					throw new InvalidOperationException("Calibration: the anchor has not been measured yet (SetAnchorMeasurement).");

				// Priced uncalibrated first, which is what the scale is measured against.
				_measuredCalibration = 1f;

				var scale = 1f;

				try
				{
					// Against RawFactor, not Factor: the scale is what puts the
					// anchor back on its factor, and dividing by the rounded
					// int leaves the anchor landing a point either side of it.
					var raw = Price(SfrDials.AnchorSkill, null, _anchorMeasurement);
					scale = SfrDials.AnchorFactor / Math.Max(raw.RawFactor, 1e-6f);
				}
				catch (Exception)
				{
					scale = 1f;
				}

				_measuredCalibration = scale;

				return scale;
			}
		}

		/// <summary>
		/// Returns the absolute SFR for a skill, from its data and a measured
		/// press only. Nothing here reads the handler's source.
		/// </summary>
		/// <remarks>
		/// Every input that used to come from regex-scanning the handler -
		/// hit count, pad/buff/DoT ticks, reach, rider value, whether a cast
		/// is really a channel - is measured instead: hit count and reach
		/// from SkillPressProbe, rider value from SfrDefenseProbe's live
		/// control/treatment pair. What is left is the policy layer alone -
		/// the cooldown curve, cast-time premiums, circle premium, width
		/// exponent - applied to what the press actually did.
		/// </remarks>
		/// <param name="skillName"></param>
		/// <param name="maxLevel"></param>
		/// <param name="measured"></param>
		public static SfrPrice Price(string skillName, int? maxLevel, SfrMeasuredPress measured)
		{
			if (measured == null)
				throw new ArgumentNullException(nameof(measured), $"{skillName}: nothing prices without a measured press.");

			if (!SfrData.Skills.TryGetValue(skillName, out var entry))
				throw new KeyNotFoundException($"No skill data for '{skillName}'.");

			var cls = SfrData.ClassOf(skillName);

			if (!SfrData.BaseJob.ContainsKey(cls))
				throw new KeyNotFoundException($"{skillName}: '{cls}' is out of scope.");

			// A heal pad's factor is a heal factor, not a damage factor, so
			// whatever chip damage it deals to undead is not what the number
			// means. Pricing it as a damage skill reads that chip damage
			// against a basic swing and returns nonsense.
			if (IsHealSkill(skillName))
				throw new InvalidOperationException($"{skillName}: heals, this model does not price healing.");

			// A measurement that landed nothing anywhere is an unobserved
			// press, not a narrow one, and its reach of zero would divide the
			// price into the millions.
			if (!measured.Delivered)
				throw new InvalidOperationException($"{skillName}: the measured press damaged nothing in any scenario.");

			// The hit count has to come from the HP the mobs actually lost;
			// the fallback of 1 is a guess, and a guess divided by a near-zero
			// reach is what prices a press into the thousands.
			if (!measured.HitsFromDamage)
				throw new InvalidOperationException($"{skillName}: no factor-scaled HP loss was observed, so the press could not be measured.");

			// A press still running when the window closed delivered more than
			// was counted, so its hit count is a floor and the price a ceiling.
			if (measured.Truncated)
				throw new InvalidOperationException($"{skillName}: the press outran the {SkillPressProbe.MaxPressMs} ms window, so its hit count is truncated.");

			var levels = maxLevel ?? SfrData.SkillMaxLevel(skillName);
			var cast = entry.Num("basicCast") / 1000f;
			var shoot = entry.Num("shootTime") / 1000f;

			// A channel delivers damage for as long as it runs rather than
			// landing one hit at the end of a wind-up, decided purely from
			// how long the data says the press is held - shootTime past the
			// channel threshold, with no cast committing to it first.
			var channel = cast <= 0 && shoot * 1000f >= SfrDials.ChannelShootMs;

			var (t, rawOccupancy, cycle) = SfrData.PressWindow(entry);

			var hits = Math.Max(1f, measured.HitEquivalents);

			// The only rider left is the one that can be measured: what a
			// live, hostile mob's own output dropped by after the press,
			// in units of the caster's own basic swing. Silent - a
			// multiplier of 1 - for any press SfrDefenseProbe found nothing
			// on.
			//
			// Floored, because DefenseValueScale was calibrated at a quarter
			// of a swing prevented and the hyperbola is being read at ten:
			// nothing measured says what a press that locks a mob down for a
			// whole window is worth, and unfloored it takes the price to zero.
			var riderMultiplier = RiderMultiplier(measured.SwingsPrevented);

			var riderKinds = measured.SwingsPrevented > 0
				? new[] { $"defensive (measured, {measured.SwingsPrevented:0.00} swings prevented)" }
				: Array.Empty<string>();

			var (castPremium, premiumKinds) = CastPremium(entry, cast, channel);

			// Nothing here reads the occupancy on its own, so it pays for itself
			// only through the cycle and a cast lands at DPS parity with an
			// instant press. The ceiling bounds efficiency, not the cast.
			var efficiency = Math.Min(SfrDials.MaxEfficiency,
				SfrDials.BaseInstantEfficiency * CycleGate(cycle - t) * riderMultiplier);

			var cappedEfficiency = efficiency * castPremium;

			var reach = new Dictionary<string, float>();
			var targets = new Dictionary<string, (int Mine, float Theirs)>();

			foreach (var spec in SfrGeometry.PricedScenarios)
			{
				var offsets = SfrGeometry.Placement(spec, cast, out var aim);

				// The yardstick stays resolved rather than measured: it is an
				// average over the five base-job swings, a property of the
				// model rather than of this skill.
				var mine = measured.Targets.TryGetValue(spec.Id, out var reached) ? reached : 0;
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

			sfr /= SfrDials.CritAllowance;

			// What advancing a circle buys beyond a later skill reaching its cap
			// in fewer points, so the circles carry an incentive of their own.
			var circlePremium = SfrData.CirclePremium(skillName);
			sfr *= circlePremium;

			// The press budget is spread over everything the press delivers,
			// so total damage lands back on the budget exactly. The retired
			// blend divided by hits x k with k <= 1, which handed a spread
			// press up to four times its own budget in total damage - that is
			// what priced Swordman_Thrust's 33-tick bleed at 102.
			var divisor = hits;

			var factor = sfr / Math.Max(divisor, 1f) / SfrDials.LevelGrowth;

			return new SfrPrice
			{
				Skill = skillName,
				Class = cls,
				Levels = levels,
				Circle = SfrData.SkillCircle(skillName),
				Measured = true,
				CirclePremium = circlePremium,
				Occupancy = t,
				RawOccupancy = rawOccupancy,
				Cycle = cycle,
				Utilization = Math.Min(1f, t / cycle),
				DirectHits = measured.DirectHits,
				PadHits = 0,
				PadDetail = [],
				PadUnknown = [],
				BuffHits = 0,
				BuffDetail = [],
				Hits = hits,
				DamageSpan = measured.DamageSpanSeconds,
				FullDamageSpan = measured.FullDamageSpanSeconds,
				CountWindow = measured.CountWindowSeconds,
				Overruns = measured.Overruns,
				BurstFraction = measured.BurstFraction,
				Divisor = divisor,
				Dot = 0,
				DotBuff = null,
				DotShare = 0,
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
				RawFactor = factor,
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
		/// Returns the rider multiplier a measured press earns.
		/// </summary>
		/// <param name="swingsPrevented"></param>
		public static float RiderMultiplier(float swingsPrevented)
			=> swingsPrevented > SfrDials.RiderDeadband
				? Math.Max(SfrDials.RiderFloor, 1f / (1f + SfrDials.DefenseValueScale * swingsPrevented))
				: 1f;

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
		/// Returns whether a skill's own handler, or a pad it creates, heals
		/// through SCR_CalculateHeal.
		/// </summary>
		/// <remarks>
		/// This is a plain text check against the handler source, not a
		/// measurement - there is nothing to measure a heal skill's damage
		/// against, since the whole point is that it is not one. It stands
		/// alone rather than through SfrHandlerAnalysis, which the measured
		/// pipeline no longer calls into for anything else.
		/// </remarks>
		/// <param name="skillName"></param>
		public static bool IsHealSkill(string skillName)
		{
			var text = SfrSources.SkillHandler(skillName);

			if (text != null && text.Contains("SCR_CalculateHeal"))
				return true;

			foreach (var pad in SfrHandlerAnalysis.PadNames(skillName))
			{
				if (SfrSources.Pads.TryGetValue(pad, out var info) && info.Text.Contains("SCR_CalculateHeal"))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Measures every named skill in parallel, across a fixed pool of
		/// independent arenas, and returns whatever came back clean.
		/// </summary>
		/// <remarks>
		/// A skill the probe throws on - no handler, an unreadable ground
		/// target, anything SkillPressProbe.Measure was not built to dispatch -
		/// is simply left out, and Price() falls back to the handler scan for
		/// it. This is the parallel path the roster run's time budget depends
		/// on: each arena is a separate Map instance (ArenaPool), and
		/// SfrPressRecorder's hook flows via AsyncLocal, so the pool's workers
		/// never see each other's presses even when a handler's own damage
		/// lands after an await hops to a different pool thread.
		///
		/// The work is almost entirely Thread.Sleep, not CPU - a press mostly
		/// waits out real wall-clock ticks. Concurrency ramps in
		/// SfrDials.RosterWaveCount waves of poolSize workers each, rather
		/// than starting at full width: every worker's first arena touch runs
		/// GetArenaCenter's clearance search, which is real CPU work, so
		/// starting the whole pool at once turns into a spike of every worker
		/// doing that search simultaneously. Each wave starts
		/// SfrDials.RosterRampUpMs after the last, once the previous wave has
		/// settled into steady-state waiting. ThreadPool.SetMinThreads is
		/// raised to the full width up front so no wave is throttled by the
		/// pool growing into it on its own.
		/// </remarks>
		/// <param name="skillNames"></param>
		/// <param name="poolSize"></param>
		private static Dictionary<string, SfrMeasuredPress> MeasureRoster(List<string> skillNames, int poolSize)
		{
			var results = new ConcurrentDictionary<string, SfrMeasuredPress>();
			var queue = new ConcurrentQueue<string>(skillNames);
			var workersPerWave = Math.Max(1, SfrDials.SkillWorkers / SfrDials.RosterWaveCount);

			// Every window is a sleep rather than work, and a skill now fans
			// its own windows out too, so the pool has to cover the product of
			// the two rather than just the skill count.
			ThreadPool.GetMinThreads(out _, out var minIocp);
			ThreadPool.SetMinThreads(poolSize + 8, minIocp);

			using var pool = new ArenaPool(poolSize);

			LastPoolBuildTime = pool.BuildTime;

			var measureStarted = DateTime.UtcNow;

			void Worker()
			{
				while (queue.TryDequeue(out var skillName))
				{
					try
					{
						results[skillName] = SkillPressProbe.MeasureAll(skillName, pool: pool);
					}
					catch (Exception)
					{
						// Left unmeasured; the skill keeps its existing factor.
					}
				}
			}

			var waves = new List<Task>();

			for (var wave = 0; wave < SfrDials.RosterWaveCount; ++wave)
			{
				// LongRunning for the same reason the windows inside a skill
				// are: these threads spend their lives blocked, and the pool
				// will not grow into that on its own.
				waves.AddRange(Enumerable.Range(0, workersPerWave)
					.Select(_ => Task.Factory.StartNew(Worker, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)));

				if (wave < SfrDials.RosterWaveCount - 1)
					Thread.Sleep(SfrDials.RosterRampUpMs);
			}

			Task.WaitAll(waves.ToArray());

			LastMeasureTime = DateTime.UtcNow - measureStarted;

			return new Dictionary<string, SfrMeasuredPress>(results);
		}

		/// <summary>
		/// Prices every skill the model can account for and optionally writes
		/// the result.
		/// </summary>
		/// <remarks>
		/// A factor of zero is the data's marker for a skill that deals no
		/// damage, not a value to price against. Nothing else here reads the
		/// current factor.
		///
		/// This is the generation procedure proper. Every in-scope skill is run
		/// through <see cref="SkillPressProbe"/> and <see cref="SfrDefenseProbe"/>
		/// before it is priced - hit count, pad/DoT/buff ticks, reach and
		/// rider value all come from watching the handler actually run, never
		/// from reading its source. A skill the probes cannot measure at all
		/// (a summon, a press that damages nothing, one that outruns the
		/// window) is held back rather than guessed at.
		/// </remarks>
		/// <param name="write"></param>
		/// <param name="arenaPoolSize">
		/// Concurrent arenas the measurement pass runs on. Defaults to
		/// SfrDials.ArenaPoolSize; raise or lower it to trade memory for time.
		/// </param>
		public static SfrApplyResult ApplyAll(bool write, int? arenaPoolSize = null)
		{
			var result = new SfrApplyResult();
			var lines = File.ReadAllLines(SfrData.OverridesPath);
			var priced = new Dictionary<string, SfrPrice>();
			var inScope = new List<string>();

			foreach (var line in lines)
			{
				var name = Regex.Match(line, @"className: ""([^""]+)""");
				if (!name.Success)
					continue;

				var skillName = name.Groups[1].Value;

				// The field has to exist for the rewrite to have something to
				// replace, but its value is never read - the "deals no damage"
				// marker is taken from the base data instead, which this pass
				// never writes.
				var currentFactor = Regex.Match(line, @"\bfactor: ([0-9.]+)");

				if (!currentFactor.Success || !SfrData.DealsDamage(skillName))
					continue;

				var cls = SfrData.ClassOf(skillName);

				if (!SfrData.BaseJob.ContainsKey(cls) || !SfrData.Scope.Contains(cls))
					continue;

				// Not worth a measurement window: Price() rejects these
				// unconditionally, and there is nothing a live press would
				// tell us that the handler source does not already say.
				if (IsHealSkill(skillName))
				{
					result.NotPriceable++;
					result.Unmeasured.Add((skillName, SfrData.ParseFloat(currentFactor.Groups[1].Value),
						"heals, this model does not price healing"));
					continue;
				}

				inScope.Add(skillName);
			}

			var measured = MeasureRoster(inScope, arenaPoolSize ?? SfrDials.ArenaPoolSize);

			// The anchor calibrates the whole roster (Calibration()); losing
			// it loses the whole pass rather than pricing against no scale
			// at all.
			if (!measured.TryGetValue(SfrDials.AnchorSkill, out var anchorPress))
			{
				result.NotPriceable = inScope.Count;
				return result;
			}

			SetAnchorMeasurement(anchorPress);

			foreach (var line in lines)
			{
				var name = Regex.Match(line, @"className: ""([^""]+)""");
				if (!name.Success)
					continue;

				var skillName = name.Groups[1].Value;
				var currentFactor = Regex.Match(line, @"\bfactor: ([0-9.]+)");

				if (!currentFactor.Success || !inScope.Contains(skillName))
					continue;

				var oldFactor = SfrData.ParseFloat(currentFactor.Groups[1].Value);

				if (!measured.TryGetValue(skillName, out var press))
				{
					result.NotPriceable++;
					result.Unmeasured.Add((skillName, oldFactor, "the probe could not dispatch the press"));
					continue;
				}

				SfrPrice price;

				try
				{
					price = Price(skillName, null, press);
				}
				catch (Exception ex)
				{
					result.NotPriceable++;
					result.Unmeasured.Add((skillName, oldFactor, ex.Message.Replace(skillName + ": ", "")));
					continue;
				}

				if (price.RawOccupancy > SfrDials.MaxOccupancy)
				{
					result.NotPriceable++;
					result.Held.Add((skillName, SfrData.ParseFloat(currentFactor.Groups[1].Value), price.Factor,
						$"occupies {price.RawOccupancy:0}s per press - a duration, not an animation"));
					continue;
				}

				priced[skillName] = price;

				// A ratio against zero says nothing, so a skill coming off the
				// zero marker is reported on its own rather than topping the
				// movers list with an arbitrary number.
				if (oldFactor == 0)
					result.NewlyPriced.Add((skillName, price.Factor));

				result.Changes[skillName] = (price.Factor, price.FactorByLevel,
					oldFactor > 0 ? price.Factor / oldFactor : 1f);

				if (price.Overruns)
					result.Overrunning.Add((skillName, price.FullDamageSpan, price.CountWindow, price.Hits));
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

		/// <summary>
		/// Whether the hit count and the reach came from a measured press
		/// rather than from the handler scan.
		/// </summary>
		public bool Measured { get; init; }

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

		/// <summary>
		/// Seconds the press spent delivering the damage that was counted.
		/// </summary>
		public float DamageSpan { get; init; }

		/// <summary>
		/// Seconds it kept delivering for in total, counted or not.
		/// </summary>
		public float FullDamageSpan { get; init; }

		/// <summary>
		/// Seconds of delivery the count was bounded to, which is the cycle.
		/// </summary>
		public float CountWindow { get; init; }

		/// <summary>
		/// Whether delivery outlasted the cycle, so some of it was left to a
		/// later press.
		/// </summary>
		public bool Overruns { get; init; }

		/// <summary>
		/// Share of the damage landing inside one burst window.
		/// </summary>
		public float BurstFraction { get; init; }

		/// <summary>
		/// The blended divisor the press budget is spread over.
		/// </summary>
		public float Divisor { get; init; }
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

		/// <summary>
		/// The factor before it is rounded to what the file carries, which is
		/// what the anchor's calibration has to be taken against.
		/// </summary>
		public float RawFactor { get; init; }

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
		/// Skills that could not be measured at all, with the reason. These
		/// keep whatever factor the file already carried.
		/// </summary>
		public List<(string Skill, float OldFactor, string Reason)> Unmeasured { get; } = [];

		/// <summary>
		/// Skills whose delivery outlasted their own cycle, with the span, the
		/// cycle it was counted over, and the hits that survived the bound.
		/// </summary>
		/// <remarks>
		/// Priced, not held - the bound is what makes them priceable. This is
		/// the diagnostic that says which skills the bound is load-bearing for.
		/// </remarks>
		public List<(string Skill, float Span, float Cycle, float Hits)> Overrunning { get; } = [];

		/// <summary>
		/// Skills that carried a factor of zero and now price at something,
		/// with the value they landed on.
		/// </summary>
		public List<(string Skill, int Factor)> NewlyPriced { get; } = [];

		/// <summary>
		/// How many in-scope skills could not be priced at all.
		/// </summary>
		public int NotPriceable { get; set; }
	}
}
