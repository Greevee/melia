using System.Collections.Generic;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// Every tunable in the SFR pricing model, in one place.
	/// </summary>
	/// <remarks>
	/// Each of these is a design call rather than a measurement. The model is
	/// a chain of ratios, so the anchor alone sets the roster's level and
	/// everything else only redistributes between skills.
	/// </remarks>
	public static class SfrDials
	{
		/// <summary>
		/// How much of the roster's play each scenario represents.
		/// </summary>
		/// <remarks>
		/// Flat, so no encounter shape is assumed to dominate and a skill's
		/// price is its average across the whole matrix. The weighted reach
		/// normalizes by the sum, so these need not add to one.
		/// </remarks>
		public static readonly Dictionary<string, float> ScenarioWeights = new()
		{
			["S1"] = 1f,
			["S2"] = 1f,
			["S3"] = 1f,
			["S4"] = 1f,
			["S5"] = 1f,
			["S6"] = 1f,
			["S7"] = 1f,
			["S9"] = 1f,
			["S10"] = 1f,
		};

		/// <summary>
		/// The scenarios the spread cap reads, which are the gathered pulls it
		/// is about.
		/// </summary>
		/// <remarks>
		/// S5 and S6 are deliberately out: both put an averaged swing near zero,
		/// so a skill scores hugely there for having range or ignoring SDR, and
		/// the cap would fire for something other than width. Both are still
		/// paid for, through the weighted reach, which reads every scenario.
		/// </remarks>
		public static readonly string[] SpreadScenarios = ["S2", "S3", "S4", "S9", "S10"];

		/// <summary>
		/// How many times as efficient a skill may be in its best gathered
		/// scenario as in the weighted-typical case.
		/// </summary>
		public const float SpreadCap = 2.5f;

		/// <summary>
		/// How much of a skill's width it is charged for.
		/// </summary>
		/// <remarks>
		/// The price divides by the play-weighted reach, so a skill that hits
		/// three targets pays three times over unless this discounts it. 1.0
		/// charges width in full, 0.0 ignores it.
		/// </remarks>
		public const float AoeExponent = 0.85f;

		/// <summary>
		/// The skill the whole roster floats against.
		/// </summary>
		public const string AnchorSkill = "Swordman_Bash";

		/// <summary>
		/// The factor the anchor is held at.
		/// </summary>
		/// <remarks>
		/// calc_skill.cs reads factor + factorByLevel * level, so 96 is 115 SFR
		/// the moment the skill is learned. The anchor is that level-one press,
		/// not the factor field.
		/// </remarks>
		public const float AnchorFactor = 96f;

		/// <summary>
		/// What a skill's SFR at its max level is worth against its factor.
		/// </summary>
		/// <remarks>
		/// calc_skill.cs multiplies factorByLevel by the level itself rather
		/// than the level minus one, so factorByLevel = factor / maxLevel lands
		/// every skill at exactly double its factor on its last level.
		/// </remarks>
		public const float LevelGrowth = 2f;

		/// <summary>
		/// The longest a single press may be priced as occupying, in seconds.
		/// </summary>
		/// <remarks>
		/// SFR is linear in the cycle, so an entry whose basicCast is a duration
		/// rather than a wind-up prices as if one hit were worth 200 presses.
		/// Set above the longest real wind-up in the data.
		/// </remarks>
		public const float MaxOccupancy = 12f;

		/// <summary>
		/// Seconds of animation past which a press landing one hit is rejected.
		/// </summary>
		/// <remarks>
		/// A long wind-up landing one hit is a nuke and prices fine. A long
		/// animation landing one hit is a hit count the scanner failed to read,
		/// because the animation is the delivery.
		/// </remarks>
		public const float LongShoot = 1.5f;

		/// <summary>
		/// Floor on how long a press occupies the timeline, in seconds.
		/// </summary>
		public const float MinOccupancy = 0.15f;

		/// <summary>
		/// Absolute ceiling on efficiency, kept as a backstop.
		/// </summary>
		public const float MaxEfficiency = 6f;

		/// <summary>
		/// What an ungated instant press is worth, in basic-attack swings per
		/// swing of time it costs.
		/// </summary>
		public const float BaseInstantEfficiency = 2f;

		/// <summary>
		/// The occupancy a cooldown wait is scored against, in seconds.
		/// </summary>
		public const float ReferenceOccupancy = 0.4f;

		/// <summary>
		/// SFR bought per second of cooldown waited.
		/// </summary>
		/// <remarks>
		/// Linear, so 5 s buys 1.5x, 10 s buys 2.0x and 20 s buys 3.0x. It does
		/// not saturate, which is what makes an ultimate's cooldown load-bearing.
		/// </remarks>
		public const float CooldownSfrPerSecond = 0.10f;

		/// <summary>
		/// What a button with no gate at all is worth against that line.
		/// </summary>
		/// <remarks>
		/// A button with no cooldown and no overheat is gated by nothing but SP:
		/// it is the rotation rather than displacing a press inside one, so the
		/// line extrapolated to zero paid it as though pressing it were a choice.
		/// </remarks>
		public const float NoGatePenalty = 0.75f;

		/// <summary>
		/// The wait by which the ungated discount is gone, in seconds.
		/// </summary>
		public const float NoGateRamp = 1f;

		/// <summary>
		/// Premium for a cast that can be interrupted.
		/// </summary>
		public const float InterruptiblePremium = 1.15f;

		/// <summary>
		/// Premium for a cast that roots the caster.
		/// </summary>
		public const float NoCastMovePremium = 1.15f;

		/// <summary>
		/// Premium for a cast whose length DEX cannot buy down.
		/// </summary>
		public const float NoDexScalingPremium = 1.30f;

		/// <summary>
		/// Premium for a Force skill whose splash is centred on the target
		/// rather than the caster.
		/// </summary>
		/// <remarks>
		/// A caster-anchored shape (a box, a fan) always covers the ground the
		/// caster is standing in or walking through, so it catches stragglers
		/// for free. A circle built around the one enemy a projectile locked
		/// is anchored wherever that enemy happens to be standing, which is
		/// not the same ground - a strung-out pull or a loose ring reaches
		/// less of it. width already charges for how much is reached; this
		/// charges for the shape being worse at reaching it in the first
		/// place.
		/// </remarks>
		public const float ForceAoePremium = 1.15f;

		/// <summary>
		/// What casting at all is worth, before the flags.
		/// </summary>
		/// <remarks>
		/// Grows with the wind-up because a longer one is harder to land: more
		/// time to be walked out of, LoS-broken or peeled off. Written as
		/// (1 + cast)^k so it starts at 1.0 and never discounts a short cast.
		/// </remarks>
		public const float CastLengthExponent = 0.50f;

		/// <summary>
		/// Blanket DoT rider applied when the multiple is unknown.
		/// </summary>
		public const float DotRiderMultiplier = 0.5f;

		/// <summary>
		/// Headroom for the crit the measured sweep includes and this does not.
		/// </summary>
		public const float CritAllowance = 1.06f;

		/// <summary>
		/// Window the peak-damage burst is measured over, in milliseconds.
		/// </summary>
		public const int BurstWindowMs = 1000;

		/// <summary>
		/// Share of the press divisor taken from total damage delivered.
		/// </summary>
		/// <remarks>
		/// The divisor is a blend rather than the raw hit count, because a
		/// press that spends 24s delivering its damage is not worth what the
		/// same damage landing at once is. The three views are blended as
		/// divisors, not as prices - blending prices lets the smallest of
		/// them dominate, and a slow pad's burst term is near zero.
		/// </remarks>
		public const float TotalDamageWeight = 0.25f;

		/// <summary>
		/// Share taken from damage per second of the press's own occupancy.
		/// </summary>
		public const float DpsWeight = 0.50f;

		/// <summary>
		/// Share taken from the peak burst, so front-loaded damage is priced
		/// lower per hit than the same damage spread out.
		/// </summary>
		public const float BurstWeight = 0.25f;

		/// <summary>
		/// Fraction of a pad's ticks a target is assumed to stay inside it for.
		/// </summary>
		/// <remarks>
		/// No real fight holds a target in a ground pad for its whole life, so
		/// a 24-tick fire is not 24 hits.
		/// </remarks>
		public const float PadUptime = 0.6f;

		/// <summary>
		/// shootTime past which a press is a channel hold, not an animation.
		/// </summary>
		public const float ChannelShootMs = 6000f;

		/// <summary>
		/// Ticks past which a pad outlives the press that made it.
		/// </summary>
		public const int MaxPadTicks = 60;

		/// <summary>
		/// Ticks past which a damaging debuff outlives the press that applied it.
		/// </summary>
		public const int MaxBuffTicks = 30;

		/// <summary>
		/// Base SR every character carries, per calc_character.cs.
		/// </summary>
		public const int CharacterSr = 3;

		/// <summary>
		/// Fallback swing rate when no measured matrix is available.
		/// </summary>
		public const float GenericBasicRate = 2.07f;

		/// <summary>
		/// Floor on the basic-attack yardstick a scenario is scored against.
		/// </summary>
		/// <remarks>
		/// Reach is targets-reached over what an averaged swing reaches, which
		/// is undefined rather than zero when the swing reaches nothing - and a
		/// plain division scored the only skills that can act there at zero.
		/// A pack charging in from 200 gives every instant press that reading.
		/// Floored rather than made infinite because the player's real
		/// alternative is not swinging at nothing, it is closing the gap.
		/// </remarks>
		public const float MinYardstick = 0.5f;

		/// <summary>
		/// What advancing a circle buys beyond reaching a cap in fewer points.
		/// </summary>
		public static readonly Dictionary<int, float> CirclePremium = new()
		{
			[1] = 1.00f,
			[2] = 1.05f,
			[3] = 1.10f,
		};

		/// <summary>
		/// Rider multipliers for the non-damage payload the direct-hit model
		/// cannot see.
		/// </summary>
		public static readonly Dictionary<string, float> RiderMultipliers = new()
		{
			["dot"] = 0.5f,
			["defensive"] = 0.8f,
			["hardcc"] = 0.8f,
			["softcc"] = 0.95f,
			["selfbuff"] = 0.9f,
		};

		/// <summary>
		/// Buff-name patterns each rider kind is matched on.
		/// </summary>
		public static readonly (string Kind, string Pattern)[] RiderPatterns =
		[
			("dot", @"Bleed|Poison|Dot_|_Dot|Pollution|Miasma|Burn|Infect|Venom|Gu_"),
			("defensive", @"NoDamage|MomentaryImmune|MomentaryBlock|MomentaryEvasion|Invincible|BlkAbil|Shield_Buff|Barrier|Cloaking"),
			("hardcc", @"Stun|Freeze|Sleep|Hold|Silence|Petri|Knockdown|Fear"),
			("softcc", @"Slow|Shock|Blind|Weakness|Decrease"),
			("selfbuff", @"_Buff$"),
		];

		/// <summary>
		/// How long a live press is waited out before it counts as a channel or
		/// a pad rather than this press - the "~10 s" encounter window.
		/// </summary>
		/// <remarks>
		/// This is what turns pad ticks, DoT ticks and buff-driven hits from a
		/// regex guess at the handler source into a real count: the press is
		/// simply watched for the whole window instead of ~5 s, so anything a
		/// skill's own pad or debuff still does by the 10 s mark is captured by
		/// the recorder rather than inferred from source text.
		/// </remarks>
		public const int EncounterWindowMs = 10_000;

		/// <summary>
		/// How long the caster is left standing still, mobs already hostile,
		/// before the defensive/CC probe's control half starts counting - long
		/// enough that aggro has resolved and the first swing has landed.
		/// </summary>
		public const int DefenseSettleMs = 500;

		/// <summary>
		/// Workers each wave of the parallel roster run adds. MeasureRoster
		/// starts RosterWaveCount waves of this many workers, RosterRampUpMs
		/// apart, so the pool it actually builds is sized at
		/// ArenaPoolSize * RosterWaveCount.
		/// </summary>
		/// <remarks>
		/// A press spends nearly all its time in Thread.Sleep waiting out
		/// real wall-clock ticks, not computing, so this is sized well past
		/// core count rather than tied to ProcessorCount - the cost of an
		/// idle arena is cheap relative to a sleeping thread.
		/// </remarks>
		public const int ArenaPoolSize = 640;

		/// <summary>
		/// Skills measured at once. Each one fans its own scenarios, factor
		/// points and defence trials out across the pool on top of this, so the
		/// arenas in flight peak near SkillWorkers * ScenarioCount.
		/// </summary>
		public const int SkillWorkers = 110;

		/// <summary>
		/// Arenas the single-skill diagnostic runs on.
		/// </summary>
		/// <remarks>
		/// It measures the named skill and the anchor at once, and each of
		/// those fans its own windows out: nine scenarios, two factor points
		/// and, for the named skill, DefenseProbeTrials control/treatment
		/// pairs. Sized above that peak so no window ever blocks waiting for
		/// an arena, which is what turns the run's wall time into the longest
		/// single window rather than their sum.
		/// </remarks>
		public const int ExplainPoolSize = 48;

		/// <summary>
		/// How many waves of SkillWorkers MeasureRoster ramps up to.
		/// </summary>
		public const int RosterWaveCount = 2;

		/// <summary>
		/// How long MeasureRoster waits between starting each wave.
		/// </summary>
		/// <remarks>
		/// Every worker's first arena touch runs GetArenaCenter's clearance
		/// search, real CPU work rather than the Thread.Sleep the rest of a
		/// press is - starting a whole wave at once turns that into every one
		/// of its workers doing the search at the same moment. Staggering
		/// each wave behind this delay spreads that cost out instead.
		/// </remarks>
		public const int RosterRampUpMs = 1_500;

		/// <summary>
		/// Control/treatment pairs SfrDefenseProbe averages over. A single
		/// 10 s window holds only a handful of the mob's attacks, so whether
		/// one lands just inside or outside the window is real scheduling
		/// jitter large enough to swing the result by a full swing either
		/// way; averaging several pairs is what makes the number stable
		/// enough to price against.
		/// </summary>
		public const int DefenseProbeTrials = 9;

		/// <summary>
		/// Swings prevented below which a press is treated as having bought
		/// no defensive value at all.
		/// </summary>
		/// <remarks>
		/// The measurement is a difference between two windows holding only a
		/// handful of the mob's attacks, so a press that really prevents
		/// nothing still reads a fraction of a swing either way. Left ungated,
		/// the rider flickered between 1.00 and the 0.50 floor across runs and
		/// the whole pass oscillated 2x per run rather than converging - the
		/// movers list was mostly skills whose ratio was exactly 2.00 or 0.48.
		/// </remarks>
		public const float RiderDeadband = 0.25f;

		/// <summary>
		/// Control/treatment pairs run before deciding whether a skill has any
		/// defensive value worth measuring properly.
		/// </summary>
		/// <remarks>
		/// The defence probe is over half the windows in a roster run, and
		/// almost all of them are spent confirming that a skill with no CC
		/// still has no CC. Scouting first and only paying for the full
		/// DefenseProbeTrials on a skill that showed something keeps the
		/// stability where it matters without paying for it everywhere.
		/// </remarks>
		public const int DefenseScoutTrials = 2;

		/// <summary>
		/// How far below the deadband the scout has to land for a skill to be
		/// dismissed without the full trials. Above one, so a skill sitting
		/// near the deadband is measured properly rather than dismissed on a
		/// noisy pair.
		/// </summary>
		public const float DefenseScoutMargin = 1.5f;

		/// <summary>
		/// How hard a measured defensive/CC value discounts the rider
		/// multiplier: <c>1 / (1 + DefenseValueScale * swingsPrevented)</c>.
		/// </summary>
		public const float DefenseValueScale = 1.0f;

		/// <summary>
		/// Floor on the measured rider multiplier, so no non-damage payload can
		/// take more than half of a skill's damage budget.
		/// </summary>
		/// <remarks>
		/// DefenseValueScale is calibrated at a quarter of a swing prevented
		/// (Peltasta_Langort) and 1/(1+s) is unbounded below, so a press that
		/// walls a mob off for a whole window - Cryomancer_IceWall - reads at
		/// ten-plus swings and prices itself to nothing. Nothing measured says
		/// what the curve does out there, and a class whose budget is CC is
		/// already paid for it once through its §4 weight.
		/// </remarks>
		public const float RiderFloor = 0.5f;

		/// <summary>
		/// Floor on the window a press's delivery is counted over, in
		/// milliseconds.
		/// </summary>
		/// <remarks>
		/// Delivery is counted over the skill's own cycle, and an instant
		/// spam skill's cycle is a fraction of a second - short enough that
		/// the map has barely ticked. This keeps the count over something the
		/// probe can actually resolve.
		/// </remarks>
		public const int MinCountWindowMs = 1_500;

		/// <summary>
		/// How long a press that has landed nothing yet is held before it is
		/// accepted as having landed nothing at all, in milliseconds of the
		/// press's own tick clock.
		/// </summary>
		/// <remarks>
		/// The plain settle window reads skill.IsRunning, which is false in the
		/// gap between a handler returning and its own continuation being
		/// scheduled. On a wide roster run that gap is however long the
		/// continuation waits behind other threads, so a real press could be
		/// recorded as damaging nothing purely because the run was busy -
		/// coverage fell from 105 skills to 78 at 110 workers. A press that has
		/// already landed damage still exits on the short settle.
		/// </remarks>
		public const int EmptyPressSettleMs = 4_000;
	}
}
