using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Test.Balance
{
	/// <summary>
	/// How a scenario arranges its monsters, which is what separates a
	/// skill's floor from its ceiling.
	/// </summary>
	public enum MobPlacement
	{
		/// <summary>
		/// One monster, in melee range.
		/// </summary>
		Single,

		/// <summary>
		/// Spaced at the distance the spawn data says that many monsters
		/// really stand apart. The ungathered case.
		/// </summary>
		MeasuredDensity,

		/// <summary>
		/// All on one point, as a pull leaves them. The gathered ceiling.
		/// </summary>
		Stacked,

		/// <summary>
		/// Closing on the caster while the skill casts, so the skill is
		/// aimed where they were rather than where they end up.
		/// </summary>
		Chasing,

		/// <summary>
		/// Holding position out past melee reach, so a short-ranged skill
		/// cannot reach them at all.
		/// </summary>
		Ranged,
	}

	/// <summary>
	/// One row of the scenario matrix.
	/// </summary>
	public class ScenarioSpec
	{
		public string Id { get; init; }
		public string Name { get; init; }
		public int MobCount { get; init; }
		public MonsterRank Rank { get; init; } = MonsterRank.Normal;

		/// <summary>
		/// Monster level relative to the character's.
		/// </summary>
		public int LevelOffset { get; init; }

		public MobPlacement Placement { get; init; }

		public override string ToString() => $"{this.Id} {this.Name}";
	}

	/// <summary>
	/// The S1-S8 matrix every damage skill is measured against, plus the
	/// level grid it runs on.
	/// </summary>
	public static class ScenarioMatrix
	{
		/// <summary>
		/// Character levels each skill is measured at, so factorByLevel is
		/// validated across the curve instead of at one point.
		/// </summary>
		public static readonly int[] CharacterLevels = [15, 50, 99];

		/// <summary>
		/// Skill levels each skill is measured at.
		/// </summary>
		public static readonly int[] SkillLevels = [1, 5, 10, 15];

		/// <summary>
		/// Distance melee monsters are placed at, close enough that any
		/// skill with a real range reaches them.
		/// </summary>
		public const float MeleeDistance = 30f;

		/// <summary>
		/// Distance the ranged scenario holds its monsters at. Past this,
		/// only skills with genuine reach connect.
		/// </summary>
		/// <remarks>
		/// Measured against the in-scope damage skills, maxRange clusters at
		/// 100 (76 skills) and at 130-250 (67). Holding at 130 splits them
		/// roughly evenly, so the scenario has both winners and losers. At the
		/// original 250 only three skills in the whole game reached, which made
		/// it a scenario nobody was good at rather than a reach test.
		/// </remarks>
		public const float RangedDistance = 130f;

		public static readonly ScenarioSpec[] All =
		[
			new ScenarioSpec
			{
				Id = "S1",
				Name = "1 stationary same-level Normal",
				MobCount = 1,
				Placement = MobPlacement.Single,
			},
			new ScenarioSpec
			{
				Id = "S2",
				Name = "3 Normals at measured passive density",
				MobCount = 3,
				Placement = MobPlacement.MeasuredDensity,
			},
			new ScenarioSpec
			{
				Id = "S3",
				Name = "8 Normals stacked on one point",
				MobCount = 8,
				Placement = MobPlacement.Stacked,
			},
			new ScenarioSpec
			{
				Id = "S4",
				Name = "5 Normals closing on the caster",
				MobCount = 5,
				Placement = MobPlacement.Chasing,
			},
			new ScenarioSpec
			{
				Id = "S5",
				Name = "5 Normals holding at range",
				MobCount = 5,
				Placement = MobPlacement.Ranged,
			},
			new ScenarioSpec
			{
				Id = "S6",
				Name = "1 Elite, same level",
				MobCount = 1,
				Rank = MonsterRank.Elite,
				Placement = MobPlacement.Single,
			},
			new ScenarioSpec
			{
				Id = "S7",
				Name = "1 Boss, same level",
				MobCount = 1,
				Rank = MonsterRank.Boss,
				Placement = MobPlacement.Single,
			},
			new ScenarioSpec
			{
				Id = "S8",
				Name = "1 Normal 20 levels above the caster",
				MobCount = 1,
				LevelOffset = 20,
				Placement = MobPlacement.Single,
			},
		];

		/// <summary>
		/// Returns the character levels the class can actually be measured at,
		/// since a rank 3 class does not exist at level 15.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="levels"></param>
		public static int[] CharacterLevelsFor(JobEntry job, int[] levels = null)
		{
			levels ??= CharacterLevels;

			var min = JobCatalog.GetMinLevel(job);
			var allowed = levels.Where(l => l >= min).ToArray();

			// A class whose rank puts it above every level in the grid is
			// still worth one reading, at the lowest level it can exist at.
			return allowed.Length > 0 ? allowed : [min];
		}

		/// <summary>
		/// Returns the skill levels the tree allows, so a 5-point skill is
		/// not measured at 15 and reported as weak for it.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="levels"></param>
		public static int[] SkillLevelsFor(SkillEntry skill, int[] levels = null)
		{
			levels ??= SkillLevels;

			var allowed = levels.Where(l => l <= skill.MaxLevel).ToArray();

			return allowed.Length > 0 ? allowed : [Math.Min(skill.MaxLevel, levels.Min())];
		}

		/// <summary>
		/// Returns the offsets from the caster that the given placement puts
		/// its monsters at, together with the point the skill is aimed at.
		/// </summary>
		/// <remarks>
		/// Chasing monsters are returned at where they end up, while the aim
		/// point stays where they started - which is exactly the penalty a
		/// long cast time should pay.
		/// </remarks>
		/// <param name="spec"></param>
		/// <param name="count"></param>
		/// <param name="castTimeMs"></param>
		/// <param name="mobSpeed"></param>
		/// <param name="levelBandMin"></param>
		/// <param name="levelBandMax"></param>
		/// <param name="aimDistance"></param>
		public static Position[] GetOffsets(ScenarioSpec spec, int count, float castTimeMs, float mobSpeed, int levelBandMin, int levelBandMax, out float aimDistance)
		{
			var offsets = new List<Position>();

			switch (spec.Placement)
			{
				case MobPlacement.Single:
					aimDistance = MeleeDistance;
					offsets.Add(new Position(MeleeDistance, 0, 0));
					break;

				case MobPlacement.Stacked:
					aimDistance = MeleeDistance;
					for (var i = 0; i < count; ++i)
						offsets.Add(new Position(MeleeDistance, 0, 0));
					break;

				case MobPlacement.MeasuredDensity:
				{
					// The radius that really contains this many monsters,
					// halved, is how far the others stand from the one being
					// fought. The first stays in melee range: a player is
					// engaging something, not standing off a cluster.
					var spacing = SpawnCensus.RadiusForDensity(count, levelBandMin, levelBandMax) / 2f;

					aimDistance = MeleeDistance;
					offsets.AddRange(Cluster(MeleeDistance, spacing, count));
					break;
				}

				case MobPlacement.Chasing:
				{
					// They end up in melee range; the aim point is where they
					// were when the cast started. An instant skill therefore
					// aims exactly at them and a long cast does not.
					var travel = Math.Min(RangedDistance, Math.Max(0f, castTimeMs / 1000f) * mobSpeed);

					aimDistance = MeleeDistance + travel;
					offsets.AddRange(Cluster(MeleeDistance, MeleeDistance, count));
					break;
				}

				case MobPlacement.Ranged:
					aimDistance = RangedDistance;
					offsets.AddRange(Cluster(RangedDistance, MeleeDistance, count));
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(spec), $"Unhandled placement '{spec.Placement}'.");
			}

			return offsets.ToArray();
		}

		/// <summary>
		/// Puts the first monster straight ahead at the given distance and
		/// spreads the rest on a circle of the given spacing around it.
		/// </summary>
		/// <remarks>
		/// The anchor matters: a scenario whose monsters all sit on a ring
		/// leaves nothing directly in front, so a tight cone measures zero
		/// targets even against a crowd.
		/// </remarks>
		/// <param name="distance"></param>
		/// <param name="spacing"></param>
		/// <param name="count"></param>
		private static IEnumerable<Position> Cluster(float distance, float spacing, int count)
		{
			yield return new Position(distance, 0, 0);

			for (var i = 1; i < count; ++i)
			{
				var angle = (i - 1) / (float)Math.Max(1, count - 1) * MathF.PI * 2f;

				yield return new Position(distance + MathF.Cos(angle) * spacing, 0, MathF.Sin(angle) * spacing);
			}
		}

		/// <summary>
		/// Returns the movement speed to close the gap with, which is what
		/// decides how far a chasing monster gets during a cast.
		/// </summary>
		/// <param name="mob"></param>
		public static float GetMoveSpeed(Mob mob)
		{
			var speed = mob.Properties.GetFloat(PropertyName.MSPD);

			return speed > 0 ? speed : 30f;
		}
	}
}
