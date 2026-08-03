using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Aggregated result of sampling one skill against one target many
	/// times, seeded so the same inputs always produce the same summary.
	/// </summary>
	public class HitSample
	{
		public int Samples { get; init; }
		public float Mean { get; init; }
		public float P10 { get; init; }
		public float P90 { get; init; }
		public float DodgeRate { get; init; }
		public float BlockRate { get; init; }
		public float CritRate { get; init; }

		/// <summary>
		/// Hits landed per sampled cast, which is the skill's multi-hit
		/// count. A cast that dodges still counts as one cast.
		/// </summary>
		public int HitsPerCast { get; init; }

		/// <summary>
		/// Mean damage counting dodges as zero, which is the number that
		/// should be compared against sim.py's effective damage.
		/// </summary>
		public float EffectiveMean { get; init; }

		/// <summary>
		/// Effective damage a whole cast lands on one target, i.e. across
		/// all of its hits.
		/// </summary>
		public float EffectivePerCast => this.EffectiveMean * this.HitsPerCast;

		public override string ToString()
			=> $"mean {this.Mean:F1} (eff {this.EffectiveMean:F1}, cast {this.EffectivePerCast:F1} over {this.HitsPerCast} hit(s))  " +
			   $"p10 {this.P10:F1}  p90 {this.P90:F1}  " +
			   $"dodge {this.DodgeRate * 100:F0}%  block {this.BlockRate * 100:F0}%  crit {this.CritRate * 100:F0}%";
	}

	/// <summary>
	/// Runs the real combat pipeline repeatedly and summarises the result.
	/// Dodge, block and crit are all decided inside SCR_CalculateDamage, so
	/// sampling it is what puts them in the measurement.
	/// </summary>
	public static class HitSampler
	{
		/// <summary>
		/// Default sample count. High enough that a 50% dodge rate lands
		/// within about a point of its true value.
		/// </summary>
		public const int DefaultSamples = 2000;

		/// <summary>
		/// Seed every sample run starts from, so a rerun of the same
		/// scenario reproduces the same numbers.
		/// </summary>
		public const int DefaultSeed = 20260729;

		/// <summary>
		/// Samples SCR_SkillHit the given number of times and returns the
		/// damage distribution. The target's HP is never reduced.
		/// </summary>
		/// <param name="caster"></param>
		/// <param name="target"></param>
		/// <param name="skill"></param>
		/// <param name="samples"></param>
		/// <param name="seed"></param>
		/// <param name="modifier"></param>
		public static HitSample Sample(ICombatEntity caster, ICombatEntity target, Skill skill, int samples = DefaultSamples, int seed = DefaultSeed, SkillModifier modifier = null)
		{
			DeterministicRandom.Seed(seed);

			modifier ??= SkillModifier.Default;

			var damages = new float[samples];
			var dodges = 0;
			var blocks = 0;
			var crits = 0;

			for (var i = 0; i < samples; ++i)
			{
				var result = SCR_SkillHit(caster, target, skill, modifier);

				damages[i] = result.Damage;

				switch (result.Result)
				{
					case HitResultType.Dodge:
					case HitResultType.Miss:
						++dodges;
						break;

					case HitResultType.Block:
						++blocks;
						break;

					case HitResultType.Crit:
						++crits;
						break;
				}
			}

			var landed = damages.Where(d => d > 0).ToArray();
			Array.Sort(damages);

			return new HitSample
			{
				Samples = samples,
				Mean = landed.Length > 0 ? landed.Average() : 0,
				EffectiveMean = damages.Average(),
				P10 = damages[(int)(samples * 0.10)],
				P90 = damages[(int)(samples * 0.90)],
				DodgeRate = dodges / (float)samples,
				BlockRate = blocks / (float)samples,
				CritRate = crits / (float)samples,
				HitsPerCast = GetHitsPerCast(skill, modifier),
			};
		}

		/// <summary>
		/// Samples the skill against several targets and returns one sample
		/// per target, in the order the targets were given.
		/// </summary>
		/// <remarks>
		/// Each target is sampled from the same seed so a difference between
		/// them is the target's own stats, not variance.
		/// </remarks>
		/// <param name="caster"></param>
		/// <param name="targets"></param>
		/// <param name="skill"></param>
		/// <param name="samples"></param>
		/// <param name="seed"></param>
		/// <param name="modifier"></param>
		public static HitSample[] SampleAll(ICombatEntity caster, IEnumerable<ICombatEntity> targets, Skill skill, int samples = DefaultSamples, int seed = DefaultSeed, SkillModifier modifier = null)
		{
			return targets
				.Select(t => Sample(caster, t, skill, samples, seed, modifier))
				.ToArray();
		}

		/// <summary>
		/// Returns how many times one cast strikes a single target.
		/// </summary>
		/// <remarks>
		/// The data's multiHitCount is dead - nothing in the server reads it
		/// except one out-of-scope handler, and its values are not hit counts:
		/// Effigy carries 15 and Latent Venom 100. Treating it as one inflated
		/// exactly those skills to the top of the outlier list. A skill's real
		/// per-cast hit count lives in its handler, so an explicit modifier
		/// wins and HandlerHitCounts reads the rest out of the handler source.
		/// </remarks>
		/// <param name="skill"></param>
		/// <param name="modifier"></param>
		private static int GetHitsPerCast(Skill skill, SkillModifier modifier)
		{
			if (modifier != null && modifier.HitCount > 1)
				return modifier.HitCount;

			return HandlerHitCounts.Get(skill.Id);
		}
	}
}
