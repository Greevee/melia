using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Which side of a fight a buff was applied to, and which direction of
	/// damage was then measured.
	/// </summary>
	public enum BuffSlot
	{
		/// <summary>
		/// On the player, measuring the damage they deal.
		/// </summary>
		SelfOffense,

		/// <summary>
		/// On the player, measuring the damage they take.
		/// </summary>
		SelfDefense,

		/// <summary>
		/// On the monster, measuring the damage the player deals into it.
		/// This is where an offensive debuff shows up.
		/// </summary>
		EnemyOffense,

		/// <summary>
		/// On the monster, measuring the damage it deals back. This is where
		/// a control or weakening debuff shows up.
		/// </summary>
		EnemyDefense,
	}

	/// <summary>
	/// What one buff did in one slot.
	/// </summary>
	public class BuffEffect
	{
		public BuffId Id { get; init; }
		public string Owner { get; init; }
		public BuffSlot Slot { get; init; }
		public int Level { get; init; }

		/// <summary>
		/// Caster INT the effect was measured at, which matters for the
		/// buffs that scale on it.
		/// </summary>
		public int CasterInt { get; init; }

		public float Baseline { get; init; }
		public float Buffed { get; init; }

		/// <summary>
		/// Buffed over baseline. Above 1 helps the measured side, below 1
		/// hurts it.
		/// </summary>
		public float Ratio => this.Baseline <= 0 ? 0 : this.Buffed / this.Baseline;

		/// <summary>
		/// Set when the buff could not be applied at all, which usually
		/// means its handler needs a real skill cast behind it.
		/// </summary>
		public string Error { get; init; }

		/// <summary>
		/// Properties the buff moved on the side it was applied to, as
		/// "NAME+delta" pairs.
		/// </summary>
		/// <remarks>
		/// Damage sampling alone cannot see a buff that changes movement
		/// speed, cooldowns or attack speed, so without this a great many
		/// working buffs would be filed as doing nothing.
		/// </remarks>
		public string PropertyDeltas { get; init; } = "";

		/// <summary>
		/// Whether the buff moved damage.
		/// </summary>
		public bool HasEffect => this.Error == null && Math.Abs(this.Ratio - 1f) > BuffProbe.EffectTolerance;

		/// <summary>
		/// Whether the buff did anything observable at all, damage or not.
		/// </summary>
		public bool IsLive => this.HasEffect || this.PropertyDeltas.Length > 0;

		public override string ToString()
			=> this.Error != null
				? $"{this.Id} {this.Slot}: FAILED ({this.Error})"
				: $"{this.Id} {this.Slot} lv{this.Level} int{this.CasterInt}: {this.Baseline:F0} -> {this.Buffed:F0} ({this.Ratio:F3}x) {this.PropertyDeltas}";
	}

	/// <summary>
	/// What a combination of buffs did together, against what they should
	/// have done if they simply multiplied.
	/// </summary>
	public class BuffStackEffect
	{
		public BuffId[] Ids { get; init; }
		public BuffSlot Slot { get; init; }
		public float Baseline { get; init; }
		public float Buffed { get; init; }

		/// <summary>
		/// Product of the members' individual ratios.
		/// </summary>
		public float Expected { get; init; }

		public float Ratio => this.Baseline <= 0 ? 0 : this.Buffed / this.Baseline;

		/// <summary>
		/// Measured over expected. Above 1 means the stack compounds harder
		/// than its parts, which is the shape that breaks a multiplier
		/// budget.
		/// </summary>
		public float StackFactor => this.Expected <= 0 ? 0 : this.Ratio / this.Expected;

		public override string ToString()
			=> $"[{string.Join(" + ", this.Ids)}] {this.Slot}: {this.Ratio:F2}x measured vs {this.Expected:F2}x expected ({this.StackFactor:F3})";
	}

	/// <summary>
	/// Measures what buffs and debuffs are worth by applying them and
	/// re-measuring damage through the real pipeline.
	/// </summary>
	public static class BuffProbe
	{
		/// <summary>
		/// Ratio movement below this counts as no effect, covering sampling
		/// noise on a distribution with crits in it.
		/// </summary>
		public const float EffectTolerance = 0.005f;

		/// <summary>
		/// Caster INT values stat-scaling buffs are reported at. Thaumaturge
		/// is the reason this exists: its buffs read the caster's INT, so a
		/// single measurement says nothing about the curve.
		/// </summary>
		public static readonly int[] StatSweepValues = [1, 50, 100, 200, 500, 1000, 1500];

		/// <summary>
		/// Samples per measurement. Lower than the default, since a sweep
		/// runs tens of thousands of them.
		/// </summary>
		public const int Samples = 300;

		/// <summary>
		/// Character level every probe runs at.
		/// </summary>
		public const int ProbeLevel = 50;

		/// <summary>
		/// Buff level every probe applies, which is the level a buff's
		/// handler reads from NumArg1.
		/// </summary>
		public const int ProbeBuffLevel = 10;

		private static readonly TimeSpan ProbeDuration = TimeSpan.FromHours(1);

		/// <summary>
		/// Properties watched for movement, covering every axis a buff can
		/// touch that damage sampling would not reveal on its own.
		/// </summary>
		private static readonly string[] WatchedProperties =
		[
			PropertyName.MAXPATK, PropertyName.MINPATK, PropertyName.MAXMATK, PropertyName.MINMATK,
			PropertyName.DEF, PropertyName.MDEF, PropertyName.MHP, PropertyName.MSP,
			PropertyName.HR, PropertyName.DR, PropertyName.BLK, PropertyName.BLK_BREAK,
			PropertyName.CRTHR, PropertyName.CRTDR, PropertyName.CRTATK,
			PropertyName.MSPD, PropertyName.NormalASPD, PropertyName.CastingSpeed,
			PropertyName.STR, PropertyName.CON, PropertyName.INT, PropertyName.MNA, PropertyName.DEX,
			PropertyName.SR, PropertyName.SDR,
		];

		/// <summary>
		/// Measures one buff in one slot.
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="slot"></param>
		/// <param name="job"></param>
		/// <param name="casterInt"></param>
		/// <param name="buffLevel"></param>
		public static BuffEffect Measure(BuffEntry entry, BuffSlot slot, JobEntry job = null, int casterInt = 0, int buffLevel = ProbeBuffLevel)
			=> MeasureStack([entry], slot, job, casterInt, buffLevel).Single();

		/// <summary>
		/// Measures a set of buffs applied together, returning one entry per
		/// buff when the set has one member and a combined reading otherwise.
		/// </summary>
		/// <param name="entries"></param>
		/// <param name="slot"></param>
		/// <param name="job"></param>
		/// <param name="casterInt"></param>
		/// <param name="buffLevel"></param>
		public static BuffEffect[] MeasureStack(BuffEntry[] entries, BuffSlot slot, JobEntry job = null, int casterInt = 0, int buffLevel = ProbeBuffLevel)
		{
			job ??= JobCatalog.Entries.First(e => e.SkillPrefix == "Swordman");

			var character = (Character)null;
			var support = (Character)null;
			var mob = (Mob)null;

			try
			{
				character = SyntheticActors.CreateCharacter(job.JobId, ProbeLevel, StatSpread.AllIn(JobCatalog.GetPrimaryStat(job), ProbeLevel));
				ReferenceGear.Equip(character, job);

				// A separate support character casts the buffs, so the ones
				// that read their caster's stats can be swept without
				// disturbing the build being measured.
				support = CreateSupport(casterInt);

				var mobData = SpawnCensus.FindReferenceMob(ProbeLevel, MonsterRank.Normal, tolerance: 8, out _);
				mob = SyntheticActors.CreateMob(mobData.Id, new Melia.Shared.World.Position(ScenarioMatrix.MeleeDistance, 0, 0));

				var attackSkill = SyntheticActors.GiveSkill(character, SkillId.Normal_Attack, 1);
				var mobSkill = new Skill(mob, SkillId.Normal_Attack, 1);

				var measuring = slot is BuffSlot.SelfOffense or BuffSlot.EnemyOffense;
				var target = slot is BuffSlot.EnemyOffense or BuffSlot.EnemyDefense ? (ICombatEntity)mob : character;

				var baseline = measuring
					? HitSampler.Sample(character, mob, attackSkill, Samples).EffectivePerCast
					: HitSampler.Sample(mob, character, mobSkill, Samples).EffectivePerCast;

				var before = Snapshot(target);
				var error = ApplyAll(entries, target, support, buffLevel);

				var buffed = error != null ? 0f : measuring
					? HitSampler.Sample(character, mob, attackSkill, Samples).EffectivePerCast
					: HitSampler.Sample(mob, character, mobSkill, Samples).EffectivePerCast;

				var deltas = error != null ? "" : Describe(before, Snapshot(target));

				return entries
					.Select(e => new BuffEffect
					{
						Id = e.Id,
						Owner = e.Owner,
						Slot = slot,
						Level = buffLevel,
						CasterInt = Math.Max(1, casterInt),
						Baseline = baseline,
						Buffed = buffed,
						Error = error,
						PropertyDeltas = deltas,
					})
					.ToArray();
			}
			finally
			{
				SyntheticActors.Cleanup(character, mob);
				SyntheticActors.Cleanup(support);
			}
		}

		/// <summary>
		/// Measures a combination and compares it against the product of the
		/// members' individual ratios.
		/// </summary>
		/// <param name="entries"></param>
		/// <param name="slot"></param>
		/// <param name="individual"></param>
		/// <param name="job"></param>
		/// <param name="casterInt"></param>
		public static BuffStackEffect MeasureCombination(BuffEntry[] entries, BuffSlot slot, IReadOnlyDictionary<BuffId, float> individual, JobEntry job = null, int casterInt = 0)
		{
			var measured = MeasureStack(entries, slot, job, casterInt).First();
			var expected = 1f;

			foreach (var entry in entries)
			{
				if (individual.TryGetValue(entry.Id, out var ratio) && ratio > 0)
					expected *= ratio;
			}

			return new BuffStackEffect
			{
				Ids = entries.Select(e => e.Id).ToArray(),
				Slot = slot,
				Baseline = measured.Baseline,
				Buffed = measured.Buffed,
				Expected = expected,
			};
		}

		/// <summary>
		/// Sweeps the caster's INT and returns the effect at each value, so a
		/// buff that scales on its caster's stats shows its whole curve.
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="slot"></param>
		/// <param name="job"></param>
		public static BuffEffect[] SweepCasterStat(BuffEntry entry, BuffSlot slot, JobEntry job = null)
		{
			return StatSweepValues
				.Select(value => Measure(entry, slot, job, value))
				.ToArray();
		}

		/// <summary>
		/// Returns true if the buff's effect changes with the caster's INT,
		/// checked at the ends of the sweep so the expensive full sweep only
		/// runs for the buffs that need it.
		/// </summary>
		/// <param name="entry"></param>
		/// <param name="slot"></param>
		/// <param name="job"></param>
		public static bool ScalesWithCasterStat(BuffEntry entry, BuffSlot slot, JobEntry job = null)
		{
			var low = Measure(entry, slot, job, StatSweepValues.First());
			var high = Measure(entry, slot, job, StatSweepValues.Last());

			if (low.Error != null || high.Error != null)
				return false;

			return Math.Abs(high.Ratio - low.Ratio) > EffectTolerance;
		}

		/// <summary>
		/// Applies every buff in the set, returning the first failure or null
		/// if they all took.
		/// </summary>
		/// <param name="entries"></param>
		/// <param name="target"></param>
		/// <param name="caster"></param>
		/// <param name="buffLevel"></param>
		private static string ApplyAll(BuffEntry[] entries, ICombatEntity target, Character caster, int buffLevel)
		{
			foreach (var entry in entries)
			{
				try
				{
					// NumArg1 is the skill level a buff handler reads; the
					// caster is what stat-scaling handlers look at.
					var buff = target.StartBuff(entry.Id, buffLevel, 0, ProbeDuration, caster);

					if (buff == null)
						return "handler returned no buff";
				}
				catch (Exception ex)
				{
					return ex.GetType().Name + ": " + ex.Message;
				}
			}

			target.Properties.InvalidateAll();

			return null;
		}

		/// <summary>
		/// Reads the watched properties off an entity.
		/// </summary>
		/// <param name="entity"></param>
		private static Dictionary<string, float> Snapshot(ICombatEntity entity)
		{
			var values = new Dictionary<string, float>();

			foreach (var property in WatchedProperties)
				values[property] = entity.Properties.GetFloat(property, 0);

			return values;
		}

		/// <summary>
		/// Returns the properties that moved between two snapshots.
		/// </summary>
		/// <param name="before"></param>
		/// <param name="after"></param>
		private static string Describe(Dictionary<string, float> before, Dictionary<string, float> after)
		{
			var changes = new List<string>();

			foreach (var property in WatchedProperties)
			{
				var delta = after[property] - before[property];

				if (Math.Abs(delta) < 0.01f)
					continue;

				changes.Add($"{property}{(delta > 0 ? "+" : "")}{delta:F0}");
			}

			return string.Join(";", changes);
		}

		/// <summary>
		/// Builds the support character whose INT the stat sweep varies.
		/// </summary>
		/// <param name="casterInt"></param>
		private static Character CreateSupport(int casterInt)
		{
			var support = JobCatalog.Entries.First(e => e.SkillPrefix == "Thaumaturge");
			var stats = new StatSpread { Int = Math.Max(1, casterInt) };
			var character = SyntheticActors.CreateCharacter(support.JobId, ProbeLevel, stats);

			ReferenceGear.Equip(character, support);

			return character;
		}
	}
}
