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
		/// The scenarios the charged width takes its peak over, which are the
		/// gathered pulls an averaged swing actually reaches into.
		/// </summary>
		/// <remarks>
		/// S4 is out on top of S5 and S6: the pack has not arrived, so an
		/// averaged swing reaches 0.0 targets there and the yardstick falls to
		/// MinYardstick, scoring every target reached at double. The average
		/// dilutes that to a ninth; a max lets it decide the whole term, and it
		/// gave Peltasta_ShieldLob a peak of 8.0 against its next-best 4.0.
		/// </remarks>
		public static readonly string[] PeakScenarios = ["S2", "S3", "S9", "S10"];

		/// <summary>
		/// How many times as efficient a skill may be in its best gathered
		/// scenario as in the weighted-typical case.
		/// </summary>
		public const float SpreadCap = 2.5f;

		/// <summary>
		/// How much of the charged width comes from a skill's best gathered
		/// scenario rather than its average across the matrix.
		/// </summary>
		/// <remarks>
		/// A player gathers a pull before pressing, so the reach a skill is
		/// charged for is mostly the one it gets when the pull is gathered. The
		/// average alone charged Peltasta_ShieldLob more width than
		/// Swordman_Bash while it hit 4 of a stacked 8 to Bash's 7, because a
		/// hard target cap reads the same as real area once it is averaged.
		/// Read over SpreadScenarios, so range is still paid through the
		/// weighted term rather than through the floored S5 yardstick.
		/// </remarks>
		public const float WidthPeakShare = 0.75f;

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
		/// What an advanced class's skill is worth against a base-job skill, by
		/// the circle the skill belongs to.
		/// </summary>
		/// <remarks>
		/// The five rank-1 classes take 1.00 and keep the level the anchor
		/// sets; every advanced skill is lifted by its own circle. It is the
		/// only premium above the base pool - advancing out of a base job and
		/// advancing a circle are one multiplier, not two.
		/// </remarks>
		public static readonly Dictionary<int, float> CirclePremium = new()
		{
			[1] = 1.20f,
			[2] = 1.25f,
			[3] = 1.30f,
		};

		/// <summary>
		/// How much of a skill's ceiling is bought by levelling it rather than
		/// by unlocking it, by the circle the skill belongs to.
		/// </summary>
		/// <remarks>
		/// A later circle is more front-loaded, so it arrives near the value
		/// the previous circle's skill was already sitting at instead of at
		/// half of it. 0.50 is the base pool's share and reproduces the
		/// retired doubling rule exactly, which is what holds the anchor.
		/// </remarks>
		public static readonly Dictionary<int, float> SlopeShare = new()
		{
			[1] = 0.45f,
			[2] = 0.35f,
			[3] = 0.27f,
		};

		/// <summary>
		/// The slope share every base-job skill takes, whatever circle it
		/// sits in.
		/// </summary>
		public const float BaseSlopeShare = 0.50f;

		/// <summary>
		/// What a channel earns for paying its SP over the hold rather than up
		/// front.
		/// </summary>
		/// <remarks>
		/// Paired with SpChannelMultiplier: a channel is charged that much more
		/// SP and returns that much more SFR, so the trade is explicit and
		/// symmetric rather than one side of it being free.
		/// </remarks>
		public const float ChannelSfrPremium = 1.15f;

		/// <summary>
		/// SP one press of the anchor's shape costs: base job, no wind-up, no
		/// buff payload, on the anchor's own cycle.
		/// </summary>
		/// <remarks>
		/// Chosen so the priced roster's median lands on the median the file
		/// already carried, which is 18. Swordman_Bash's own 5 would have put
		/// the whole roster at roughly half its current cost.
		/// </remarks>
		public const float SpAnchorCost = 8f;

		/// <summary>
		/// How much of the cycle a press gates is charged as SP.
		/// </summary>
		/// <remarks>
		/// Sublinear: a press that holds the rotation for longer is worth more
		/// SP, but a 60 s ultimate is not worth thirty times a filler. 0.35 is
		/// the log-log slope of basicSp against the cycle across the in-scope
		/// roster as the file carries it today.
		/// </remarks>
		public const float SpCycleExponent = 0.35f;

		/// <summary>
		/// How hard a wind-up is charged in SP, as <c>(1 + cast)^k</c>.
		/// </summary>
		/// <remarks>
		/// The same shape the SFR cast premium takes, so what a cast earns in
		/// damage it also pays for at the bar. 0.60 is the measured slope of
		/// the current file's own cast skills.
		/// </remarks>
		public const float SpCastExponent = 0.60f;

		/// <summary>
		/// The longest cycle SP is charged for, in seconds.
		/// </summary>
		public const float SpMaxCycle = 60f;

		/// <summary>
		/// What a press carrying no damage factor at all costs, against one
		/// that does.
		/// </summary>
		/// <remarks>
		/// factor 0 is the data's marker for a buff or a utility press. Nothing
		/// in the damage model prices those, so SP is the only thing holding
		/// them, and it holds them harder.
		/// </remarks>
		public const float SpBuffMultiplier = 1.5f;

		/// <summary>
		/// What a Wizard- or Cleric-family press costs, against the rest.
		/// </summary>
		public const float SpArcaneMultiplier = 1.20f;

		/// <summary>
		/// What a channel is charged for spending its SP over the hold.
		/// </summary>
		public const float SpChannelMultiplier = 1.15f;

		/// <summary>
		/// The two basicSp values a press is measured at, so how many times a
		/// press charges its own cost can be read off the slope between them.
		/// </summary>
		/// <remarks>
		/// Pinned on the same two runs the factor line is taken from, so the
		/// SP measurement costs no window of its own. Most presses charge once
		/// and read a slope of 1; a channel or a pad that bills per tick reads
		/// its tick count, and the priced cost is divided by it so what the
		/// press actually spends lands on the budget.
		/// </remarks>
		public const float SpProbeLow = 50f;

		/// <summary>
		/// The second point of the SP line.
		/// </summary>
		public const float SpProbeHigh = 100f;

		/// <summary>
		/// Charges per press below which the SP measurement is treated as
		/// having found nothing, so the skill keeps the cost it carried.
		/// </summary>
		/// <remarks>
		/// A press whose cost does not move with basicSp at all is on a
		/// spendSpScript of its own - SCR_Get_SpendSP_Common_MovingForward
		/// reads a share of max SP and never touches the field - and writing
		/// the field would change nothing.
		/// </remarks>
		public const float MinSpChargeSlope = 0.25f;

		/// <summary>
		/// Floor on a written SP cost, and on its per-level growth.
		/// </summary>
		public const int MinSpCost = 1;

		/// <summary>
		/// Hard SFR multiplier applied to a named skill after the model prices
		/// it, for a cut the model itself has no term for.
		/// </summary>
		/// <remarks>
		/// A design override, not a measurement - it survives a re-price of the
		/// roster instead of being clobbered by it. 1.0 leaves a skill at what
		/// the model priced.
		/// </remarks>
		public static readonly Dictionary<string, float> SkillSfrMultipliers = new()
		{
			["Linker_JointPenalty"] = 0.1f,
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
		/// How long each half of a defence trial counts the mob's output over.
		/// </summary>
		/// <remarks>
		/// Much longer than the press window, and it costs nothing: under the
		/// virtual clock a window is advanced rather than waited out, so length
		/// is free where it used to be the run's whole budget. The reading is a
		/// difference in damage taken over a fixed window, so its noise is set
		/// by how many of the mob's swings the window holds - at 10 s that was
		/// a handful, and one swing landing either side of the boundary moved
		/// the answer. Lengthening the window is a better buy than more trials:
		/// noise falls with the square root of the swings either way, and this
		/// multiplies them without multiplying the setup.
		/// </remarks>
		public const int DefenseWindowMs = 90_000;

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
		/// Skills measured at once.
		/// </summary>
		/// <remarks>
		/// Sized near core count, not far past it. A window used to be almost
		/// entirely Thread.Sleep, so 110 workers were cheaper than 22 and the
		/// pool was deliberately oversized against a sleeping thread. Under the
		/// virtual clock a window is advanced instead of waited out, so the run
		/// is CPU-bound and oversubscribing only adds contention: the same pass
		/// measured 1.8 min on 110 workers and 1.1 min on 12.
		/// </remarks>
		public const int SkillWorkers = 12;

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
		/// Control/treatment pairs SfrDefenseProbe averages over.
		/// </summary>
		/// <remarks>
		/// Small, because DefenseWindowMs does the work instead: a long window
		/// holds many of the mob's swings, where more short windows each hold
		/// a handful. The remaining trials are there for the part of the
		/// defence path that is still not reproducible - unlike a press, the
		/// mob acts through its AI, and not every branch of that is on the
		/// clock yet.
		/// </remarks>
		public const int DefenseProbeTrials = 7;

		/// <summary>
		/// Share trimmed off each end of the defence trials before they are
		/// averaged.
		/// </summary>
		/// <remarks>
		/// What a press prevents is bimodal per trial - a knockback lands
		/// before the mob's next swing or after it - so the estimator has to be
		/// a mean to converge on the mix rather than a median, which jumps
		/// between the modes. The trim is what stops one window that caught an
		/// extra swing from carrying that mean.
		/// </remarks>
		public const float DefenseTrimShare = 0.2f;




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
		public const float RiderFloor = 0.75f;

		/// <summary>
		/// Independent measurements of the anchor the calibration takes the
		/// median of.
		/// </summary>
		/// <remarks>
		/// One, for the same reason ScenarioTrials is: the anchor is a press,
		/// and a press replays identically now. It was three because the
		/// anchor is pinned to AnchorFactor whatever it measures, so its own
		/// noise never showed in its own number - it landed in the scale that
		/// multiplies every other skill, and moved the whole roster inversely.
		/// The machinery stays because that failure mode is invisible in the
		/// anchor's own output and worth being able to re-arm.
		/// </remarks>
		public const int AnchorTrials = 1;

		/// <summary>
		/// How many times the whole low/high scenario pair is repeated.
		/// </summary>
		/// <remarks>
		/// One, because a press is reproducible now. Under the virtual clock
		/// every scenario reading - reach, hit count, SP charge slope - comes
		/// out bit-identical run to run, so repeating the pair costs three
		/// times the windows to produce three copies of the same number. It
		/// was three while the probe ran on the wall clock and a volley landed
		/// a different number of arrows inside the window each time. Raise it
		/// only if something reintroduces real time into a press.
		/// </remarks>
		public const int ScenarioTrials = 1;

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
