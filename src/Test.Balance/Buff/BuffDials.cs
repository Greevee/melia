using System;
using System.Collections.Generic;

namespace Melia.Test.Balance.Buff
{
	/// <summary>
	/// Every tunable of the buff pricing model, in one place, so a change to
	/// the model is a change to one file.
	/// </summary>
	/// <remarks>
	/// The damage model's equivalent is SfrDials. Anything shared with it -
	/// the level growth rule, the circle premium, the scenario geometry - is
	/// read from there rather than copied, so the two passes cannot drift.
	/// </remarks>
	public static class BuffDials
	{
		/// <summary>
		/// How much a point of effective HP is worth against a point of DPS.
		/// </summary>
		/// <remarks>
		/// value = gain_off * gain_def ^ DefenseWeight. At 1, a point of damage
		/// prevented is a point of damage dealt and the two sides of a fight
		/// price identically, which is what makes a 15% cut to damage taken
		/// cost what a 15% raise to damage dealt costs. A design call in
		/// BALANCE.md section 8's sense: chosen, not measured, and it moves
		/// every defensive buff against every offensive one.
		/// </remarks>
		public const float DefenseWeight = 1f;

		/// <summary>
		/// The buff the whole roster is calibrated onto.
		/// </summary>
		/// <remarks>
		/// Swordman_GungHo is the buff equivalent of Swordman_Bash: a base-job
		/// press, offense only, one ratio slot, no weapon requirement, and
		/// available to every Swordsman. Pinning it means a change to any dial
		/// moves the spread between buffs and never the roster's level.
		/// </remarks>
		public const string AnchorSkill = "Swordman_GungHo";

		/// <summary>
		/// Ratio the anchor is held at per skill level, in whatever unit its
		/// own caption declares.
		/// </summary>
		/// <remarks>
		/// Three a level on a cap of five, so Gung Ho reads 3% at level one and
		/// 15% maxed, which is what it already carries.
		/// </remarks>
		public const float AnchorRatio = 3f;

		/// <summary>
		/// Character level a single diagnostic reading is taken at, matching
		/// the damage pass's own middle probe level.
		/// </summary>
		/// <remarks>
		/// The pricer does not use this - it sweeps ScenarioMatrix's whole
		/// level grid, because half of what a buff grants is now flat and a
		/// flat bonus is not worth the same at 15 as it is at 99.
		/// </remarks>
		public const int ProbeLevel = 50;

		/// <summary>
		/// Buff level the probe measures at, which is what a handler reads
		/// from NumArg1.
		/// </summary>
		/// <remarks>
		/// Deliberately not the skill's cap: caps now differ by circle (5, 10
		/// or 15), and measuring each buff at its own cap would fold the level
		/// curve into the reading the curve is supposed to be derived from.
		/// One level for everything, and the cap is applied afterwards by the
		/// growth rule.
		///
		/// It does not decide what a reading is worth. BuffCaptionScope installs
		/// the seed flat, so a buff resolves to the same magnitude at every
		/// level and this only picks which NumArg1 the window runs with.
		/// </remarks>
		public const int ProbeBuffLevel = 5;

		/// <summary>
		/// How long each control and treatment window runs, in game
		/// milliseconds.
		/// </summary>
		/// <remarks>
		/// Long, because it is nearly free under the virtual clock and because
		/// the readings that matter here - crit rate, block rate, dodge - are
		/// rolled per swing and need swings to converge. Nine times the damage
		/// probe's press window, the same trade SfrDials.DefenseWindowMs makes.
		/// </remarks>
		public const int WindowMs = 600_000;

		/// <summary>
		/// Time the mobs are given to close and start swinging before either
		/// half of a pair begins counting.
		/// </summary>
		public const int SettleMs = 3_000;

		/// <summary>
		/// Monsters placed in a ring around the character, as targets.
		/// </summary>
		/// <remarks>
		/// More than one so a buff that widens what a swing reaches has
		/// something to reach. They do not fight back - see IncomingSamples.
		/// </remarks>
		public const int MobCount = 5;

		/// <summary>
		/// Swings of a monster's own attack put through the pipeline to read
		/// what the character takes per hit.
		/// </summary>
		/// <remarks>
		/// The defensive half is sampled rather than fought for, and that is a
		/// deliberate narrowing. Letting monster AI land the hits made the
		/// buff-free noise floor read a 30% defensive swing: how many swings a
		/// mob gets in over a window is decided by RNG that the harness does
		/// not seed on thread-pool threads (see BALANCE.md, "what is left"), and
		/// at roughly 130 hits per window that count is the whole reading.
		/// Sampling fixes the count by construction, so what moves between a
		/// control and a treatment window is mitigation and nothing else.
		///
		/// The cost is that a buff which prevents incoming swings rather than
		/// softening them - a stun, a knockback, a wall - reads as doing
		/// nothing here. That is the SFR defensive rider's job, and it already
		/// prices exactly that for the skills that carry it.
		/// </remarks>
		public const int IncomingSamples = 4000;

		/// <summary>
		/// Buffs solved at once by a roster pass.
		/// </summary>
		/// <remarks>
		/// Sized at core count rather than well past it, which is where this
		/// parts from SfrDials.SkillWorkers' reasoning. An SFR press spends
		/// nearly all its time in Thread.Sleep waiting out wall-clock ticks, so
		/// oversubscribing costs nothing; a buff window runs on a VirtualClock
		/// and never sleeps, so it is CPU end to end and more workers than cores
		/// only adds context switching.
		///
		/// Two buffs can be solved side by side because everything a window
		/// touches is either per-arena (ArenaPool hands each worker its own Map),
		/// per-flow (GameClock and SfrPressRecorder are both AsyncLocal) or per
		/// skill row (BuffCaptionScope's lock). The one piece of shared state
		/// that is none of those - the recorder's pinned-roll mode - is false for
		/// every window this pass runs.
		/// </remarks>
		public static readonly int RosterWorkers = Math.Max(1, Environment.ProcessorCount);

		/// <summary>
		/// Arenas a roster pass builds.
		/// </summary>
		/// <remarks>
		/// A worker holds exactly one arena at a time - the party scenario puts
		/// its four characters on one map rather than four - so this only has to
		/// cover the workers. Doubled so a worker never waits on a rent while
		/// another is scrubbing an arena on its way back into the pool.
		/// </remarks>
		public static readonly int ArenaPoolSize = RosterWorkers * 2;

		/// <summary>
		/// Control/treatment pairs each reading is taken over.
		/// </summary>
		/// <remarks>
		/// One, for the same reason SfrDials.ScenarioTrials is one: with the
		/// virtual clock driving the window and the incoming half sampled from
		/// a fixed seed, a pair replays identically and repeating it produces
		/// copies of the same number. NoiseFloorIsFlat is what proves that and
		/// what would catch it stopping being true; the machinery stays so the
		/// count can be raised again if it does.
		/// </remarks>
		public const int Trials = 1;

		/// <summary>
		/// Share trimmed off each end of the per-pair readings.
		/// </summary>
		public const float TrimShare = 0.2f;

		/// <summary>
		/// Movement below this counts as no effect, covering sampling noise on
		/// a distribution with crits in it.
		/// </summary>
		public const float EffectTolerance = 0.01f;

		/// <summary>
		/// Whether a buff's magnitude grows from nothing, so every point of it
		/// is bought with a skill point.
		/// </summary>
		/// <remarks>
		/// captionRatioN is written as zero and the whole magnitude lives in
		/// captionRatioNByLevel, which is the shape Swordman_GungHo already
		/// has: 3% a level, 15% at its cap of five, and nothing given away at
		/// level one. Deliberately not SfrDials' factor rule, where the base
		/// carries half the value and a level-one skill already reads most of
		/// what a maxed one does - a buff is pressed at whatever level it is
		/// taken to, and a flat base makes the first point worth many times
		/// what the last one is.
		/// </remarks>
		public const bool GrowsFromZero = true;

		/// <summary>
		/// Second scale the solver samples, so it has two points to fit a
		/// local power law through.
		/// </summary>
		/// <remarks>
		/// value(k) is not linear in k - crit rate saturates, block has a
		/// ceiling, and the defense curve returns proportionally more as the
		/// ratio falls - so the scale is solved rather than computed.
		/// </remarks>
		public const float ProbeScaleStep = 1.5f;

		/// <summary>
		/// Multiple the solver reaches for when a reading comes back under the
		/// noise floor, before it will call a buff worthless.
		/// </summary>
		/// <remarks>
		/// Wide rather than ProbeScaleStep's 1.5, because what it is trying to
		/// clear is a threshold rather than a slope: a block bonus that has not
		/// crossed BLK_BREAK reads exactly zero, and inching up finds the
		/// crossing point slowly and spends the whole solve budget doing it.
		/// </remarks>
		public const float EscalationStep = 3f;

		/// <summary>
		/// Escalations allowed before a buff that still reads neutral is held.
		/// </summary>
		/// <remarks>
		/// Counted apart from SolveIterations, so clearing a threshold does not
		/// cost the solve the measurements it needs afterwards. Three steps of
		/// three reaches MaxSlotScale from a seed of one.
		/// </remarks>
		public const int EscalationSteps = 3;

		/// <summary>
		/// How close to its target a buff has to land before the solver stops.
		/// </summary>
		public const float ConvergenceTolerance = 0.02f;

		/// <summary>
		/// Re-measurements the solver is allowed before it writes the closest
		/// scale it found.
		/// </summary>
		public const int SolveIterations = 5;

		/// <summary>
		/// Scales outside this range are rejected rather than written.
		/// </summary>
		/// <remarks>
		/// A buff whose value flattens - a block rate near its ceiling, a
		/// resistance already at cap - needs an absurd scale before the
		/// reading moves, and writing one produces a tooltip nobody believes.
		/// Rejection, not correction, matching how SFR treats a press it
		/// cannot price.
		/// </remarks>
		public const float MinSlotScale = 0.05f;

		public const float MaxSlotScale = 12f;

		/// <summary>
		/// Whether a later circle's buff is allowed to be worth more than an
		/// earlier one's, on the same premium the damage pass gives a factor.
		/// </summary>
		/// <remarks>
		/// Off. A buff is already the better point at the later circles - the
		/// damage skills there trade animation for cooldown, so a point in one
		/// moves less of the timeline than a point in a buff does. Paying the
		/// circle premium on magnitude as well widens that gap in the circles
		/// where it is already widest.
		/// </remarks>
		public const bool ApplyCirclePremium = false;

		/// <summary>
		/// Presses that declare caption ratios and are still not this pass's to
		/// price, with the reason each is here.
		/// </summary>
		/// <remarks>
		/// A list rather than a rule, because the data carries no marker that
		/// separates the two honestly. The factor column was tried and fails
		/// in both directions: it is the inert 100 on Hoplite_Finestra and
		/// Peltasta_HighGuard, which are pure buffs, and it is 38 on
		/// Paladin_StoneSkin and 350 on Priest_MassHeal, where it holds the
		/// magnitude of a block bonus and a heal rather than any damage. What
		/// makes a press ineligible is that its magnitudes are already priced
		/// somewhere else, and only a person can say that.
		///
		/// Empty is the honest starting state: the conversion in section 3.4
		/// only ever gave caption ratios to buffs and utility presses, and the
		/// audit behind it found none of them dealing damage. An entry here is
		/// a correction to that audit, not routine maintenance.
		/// </remarks>
		public static readonly Dictionary<string, string> Excluded = [];

		/// <summary>
		/// Buffs allowed more or less than the roster's budget, and what they
		/// are allowed instead.
		/// </summary>
		/// <remarks>
		/// The model reads a buff as pressed once and then owned for its
		/// duration, and uptime is the only term that argues with that. A press
		/// the character has to keep holding down is not that shape at all: it
		/// is up only while nothing else is being done, so what it is worth per
		/// moment has to be higher for it to be worth pressing. Below one is the
		/// mirror case: a buff whose cost the probe cannot see is measured as
		/// pure upside and has to be held under budget by hand.
		///
		/// The same kind of hand-set correction SfrDials.RiderMultipliers is, and
		/// it carries the same warning - every entry here is a design call, so
		/// each one needs a reason stated next to it rather than a number that
		/// looked right.
		/// </remarks>
		public static readonly Dictionary<string, float> Premiums = new()
		{
			// Held rather than pressed: the guard lasts as long as the button
			// does, and the character is doing nothing else meanwhile.
			["Highlander_CrossGuard"] = 2.0f,

			// Costs health it is never charged for. The drain is a share of max
			// HP per tick and neither axis can see it: Fortify puts the probe's
			// max HP at 100M, and the defensive gain is per-hit mitigation, which
			// no size of health pool enters. The potion the press consumes is
			// unpriced for the same reason.
			["Assassin_Hasisas"] = 0.6f,
		};
	}
}
