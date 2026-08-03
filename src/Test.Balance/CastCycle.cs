using System;
using Melia.Shared.Game.Const;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors;

namespace Melia.Test.Balance
{
	/// <summary>
	/// How often a skill can actually be pressed, and what that costs.
	/// Damage per cast means nothing without this — a 3 s cast on a 20 s
	/// cooldown and an instant 3-overheat filler are not comparable per hit.
	/// </summary>
	public class CastCycle
	{
		/// <summary>
		/// Cast time after the caster's casting speed is applied. DEX buys
		/// down 70% of it; the other 30% is fixed.
		/// </summary>
		public float CastTimeMs { get; init; }

		/// <summary>
		/// The skill's own cast time before casting speed, which is what
		/// skills_overrides.txt sets.
		/// </summary>
		public float BaseCastTimeMs { get; init; }

		/// <summary>
		/// Time the skill occupies after firing, already divided by the
		/// skill speed rate (which DEX feeds when the skill allows it).
		/// </summary>
		public float ShootTimeMs { get; init; }

		public float DelayMs { get; init; }
		public float CooldownMs { get; init; }
		public int OverheatCount { get; init; }

		/// <summary>
		/// The skill data's overheat delay. Reported for reference only: the
		/// server never reads it, so it does not lengthen the cycle.
		/// </summary>
		public float OverheatDelayMs { get; init; }

		/// <summary>
		/// Casts available per cooldown cycle.
		/// </summary>
		public int CastsPerCycle { get; init; }

		/// <summary>
		/// Wall-clock length of one full cycle.
		/// </summary>
		public float CycleMs { get; init; }

		public float SpPerCast { get; init; }

		/// <summary>
		/// Whether the caster's own DEX shortens the post-fire time.
		/// </summary>
		public bool SpeedScalesWithDex { get; init; }

		/// <summary>
		/// Casting speed the cast time was computed with, where 100 is
		/// unmodified and lower is faster.
		/// </summary>
		public float CastingSpeed { get; init; }

		public float CastsPerSecond => this.CycleMs <= 0 ? 0 : this.CastsPerCycle / (this.CycleMs / 1000f);
		public float SpPerSecond => this.SpPerCast * this.CastsPerSecond;

		public override string ToString()
			=> $"cast {this.CastTimeMs:F0}ms (base {this.BaseCastTimeMs:F0}, spd {this.CastingSpeed:F0}) " +
			   $"shoot {this.ShootTimeMs:F0}ms cd {this.CooldownMs:F0}ms oh {this.OverheatCount} " +
			   $"-> {this.CastsPerSecond:F2} casts/s, {this.SpPerSecond:F1} sp/s";
	}

	/// <summary>
	/// Derives a skill's cast rhythm from the live skill properties, so cast
	/// time, DEX, skill speed and overheat are all read from the same place
	/// the server reads them.
	/// </summary>
	public static class CastCycleModel
	{
		/// <summary>
		/// Measures the caster's cycle for the given skill.
		/// </summary>
		/// <param name="caster"></param>
		/// <param name="skill"></param>
		public static CastCycle Measure(ICombatEntity caster, Skill skill)
		{
			var castingSpeed = caster.Properties.GetFloat(PropertyName.CastingSpeed, 100);
			var baseCast = skill.Data.BasicCast;
			var castTime = baseCast * castingSpeed / 100f;

			var shootTime = skill.Properties.GetFloat(PropertyName.ShootTime);
			var delay = skill.Properties.GetFloat(PropertyName.Skill_Delay);
			var cooldown = skill.Properties.GetFloat(PropertyName.CoolDown);
			var sp = skill.Properties.GetFloat(PropertyName.SpendSP);

			var overheat = Math.Max(0, skill.Data.OverheatCount);
			var overheatDelay = (float)skill.Data.OverHeatDelay.TotalMilliseconds;

			// A cast occupies the caster for its cast time plus whichever of
			// shoot time and post-skill delay runs longer; they overlap
			// rather than add.
			var perCast = castTime + Math.Max(shootTime, delay);

			// Nothing may be instantaneous, or casts per second is infinite.
			perCast = Math.Max(1f, perCast);

			int castsPerCycle;
			float cycle;

			if (overheat > 1)
			{
				// Skill.IncreaseOverheat only starts the cooldown once the
				// last charge is spent, and nothing reads the data's overheat
				// delay, so charges run back to back.
				castsPerCycle = overheat;
				cycle = overheat * perCast + cooldown;
			}
			else
			{
				castsPerCycle = 1;
				cycle = Math.Max(perCast, cooldown);
			}

			return new CastCycle
			{
				BaseCastTimeMs = baseCast,
				CastTimeMs = castTime,
				CastingSpeed = castingSpeed,
				ShootTimeMs = shootTime,
				DelayMs = delay,
				CooldownMs = cooldown,
				OverheatCount = overheat,
				OverheatDelayMs = overheatDelay,
				CastsPerCycle = castsPerCycle,
				CycleMs = cycle,
				SpPerCast = sp,
				SpeedScalesWithDex = skill.Data.SpeedRateAffectedByDex,
			};
		}
	}
}
