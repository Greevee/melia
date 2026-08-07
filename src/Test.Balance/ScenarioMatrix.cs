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

		/// <summary>
		/// Spread evenly over the ground around the caster, a few per ring at
		/// steadily increasing distance. The area case: what a skill covers
		/// rather than what it catches in one clump.
		/// </summary>
		Area,

		/// <summary>
		/// Ranks of monsters carrying different AoE defence ratios, so a
		/// skill's splash has to spend its AoE attack ratio getting past the
		/// tanky ones to reach anything behind them.
		/// </summary>
		Penetration,
	}

	/// <summary>
	/// One monster's place in a scenario, and the AoE defence ratio it stands
	/// there with.
	/// </summary>
	/// <param name="X">Offset ahead of the caster, who faces +x.</param>
	/// <param name="Z">Offset to the caster's side.</param>
	/// <param name="Sdr">
	/// SDR the scenario forces on this monster, or zero to leave the monster's
	/// own.
	/// </param>
	public readonly record struct ScenarioMob(float X, float Z, float Sdr = 0f)
	{
		/// <summary>
		/// The offset as a world position on the ground plane.
		/// </summary>
		public Position Offset => new(this.X, 0, this.Z);
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
	/// The S1-S10 matrix every damage skill is measured against, plus the
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
		/// Deliberately past what most of the roster can touch. Every other
		/// scenario engages at 30, so without one that does not, the matrix
		/// prices the whole game as a melee game and a genuinely long-ranged
		/// skill is never paid for its range.
		/// </remarks>
		public const float RangedDistance = 200f;

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
				Name = "5 Normals charging in from 150 at mixed speeds",
				MobCount = 5,
				Placement = MobPlacement.Chasing,
			},
			new ScenarioSpec
			{
				Id = "S5",
				Name = "5 Normals in a column from 200 outwards",
				MobCount = 5,
				Placement = MobPlacement.Ranged,
			},
			new ScenarioSpec
			{
				Id = "S6",
				Name = "9 Normals in 3 ranks of mixed SDR",
				MobCount = 9,
				Placement = MobPlacement.Penetration,
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
			new ScenarioSpec
			{
				Id = "S9",
				Name = "15 Normals spread evenly over the ground",
				MobCount = 15,
				Placement = MobPlacement.Area,
			},
			new ScenarioSpec
			{
				Id = "S10",
				Name = "25 Normals spread evenly over the ground",
				MobCount = 25,
				Placement = MobPlacement.Area,
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
		/// Returns where the given placement puts its monsters, together with
		/// the point the skill is aimed at.
		/// </summary>
		/// <remarks>
		/// The single implementation both the damage sweep and the SFR pricer
		/// read, so the two cannot drift. Density spacing is passed in rather
		/// than measured here, because SpawnCensus needs a booted world and the
		/// pricer runs without one.
		/// </remarks>
		/// <param name="spec"></param>
		/// <param name="count"></param>
		/// <param name="castTimeMs"></param>
		/// <param name="mobSpeed"></param>
		/// <param name="densitySpacing"></param>
		/// <param name="aimDistance"></param>
		public static ScenarioMob[] Layout(ScenarioSpec spec, int count, float castTimeMs, float mobSpeed, float densitySpacing, out float aimDistance)
		{
			var mobs = new List<ScenarioMob>();

			switch (spec.Placement)
			{
				case MobPlacement.Single:
					aimDistance = MeleeDistance;
					mobs.Add(new ScenarioMob(MeleeDistance, 0));
					break;

				case MobPlacement.Stacked:
					aimDistance = MeleeDistance;
					for (var i = 0; i < count; ++i)
						mobs.Add(new ScenarioMob(MeleeDistance, 0));
					break;

				case MobPlacement.MeasuredDensity:
					// The first monster stays in melee range: a player is
					// engaging something, not standing off a cluster.
					aimDistance = MeleeDistance;
					mobs.AddRange(Cluster(MeleeDistance, densitySpacing, count));
					break;

				case MobPlacement.Chasing:
				{
					// A pack charging in from range at their own speeds, with
					// the player holding a beat before committing. Where each
					// one has got to when the skill lands is the whole test, so
					// a long cast catches the pack and an instant press does
					// not - the reverse of what a stationary pull rewards.
					var elapsed = ChasePreCastSeconds + Math.Max(0f, castTimeMs / 1000f);

					aimDistance = MeleeDistance;

					for (var i = 0; i < count; ++i)
					{
						var speed = mobSpeed * ChaseSpeedSpread(i, count);
						var travelled = speed * elapsed;

						mobs.Add(new ScenarioMob(Math.Max(MeleeDistance, ChaseStartDistance - travelled), 0));
					}

					break;
				}

				case MobPlacement.Ranged:
					// A column receding away from the caster, aimed at its near
					// end. Nothing short-ranged participates at all, which is
					// what makes this the reach test.
					aimDistance = RangedDistance;

					for (var i = 0; i < count; ++i)
						mobs.Add(new ScenarioMob(RangedDistance + i * RangedSpacing, 0));

					break;

				case MobPlacement.Penetration:
					// Three ranks of three, each rank holding one monster of
					// every SDR. LimitBySDR spends the skill's SR on the
					// highest SDR first, so the tanky ones soak the splash
					// before it reaches anything behind them.
					aimDistance = PenetrationDistances[0];
					mobs.AddRange(PenetrationRanks());
					break;

				case MobPlacement.Area:
					// Aimed at the nearest ring, since that is where a player
					// engages. Reaching the outer rings is the skill's problem.
					aimDistance = MeleeDistance;
					mobs.AddRange(Rings(count));
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(spec), $"Unhandled placement '{spec.Placement}'.");
			}

			return mobs.ToArray();
		}

		/// <summary>
		/// Returns the monsters for a scenario, measuring the density spacing
		/// from the live spawner registry.
		/// </summary>
		/// <param name="spec"></param>
		/// <param name="count"></param>
		/// <param name="castTimeMs"></param>
		/// <param name="mobSpeed"></param>
		/// <param name="levelBandMin"></param>
		/// <param name="levelBandMax"></param>
		/// <param name="aimDistance"></param>
		public static ScenarioMob[] GetOffsets(ScenarioSpec spec, int count, float castTimeMs, float mobSpeed, int levelBandMin, int levelBandMax, out float aimDistance)
		{
			var spacing = spec.Placement == MobPlacement.MeasuredDensity
				? SpawnCensus.RadiusForDensity(count, levelBandMin, levelBandMax) / 2f
				: 0f;

			return Layout(spec, count, castTimeMs, mobSpeed, spacing, out aimDistance);
		}

		/// <summary>
		/// How long the player holds before committing the skill, in seconds.
		/// </summary>
		public const float ChasePreCastSeconds = 1f;

		/// <summary>
		/// How far out a charging pack starts.
		/// </summary>
		public const float ChaseStartDistance = 150f;

		/// <summary>
		/// Returns the fraction of the reference run speed the given monster
		/// in a charging pack moves at.
		/// </summary>
		/// <remarks>
		/// Spread so the pack arrives strung out rather than as one wall,
		/// which is what makes a wide skill and a well-timed one score
		/// differently here.
		/// </remarks>
		/// <param name="index"></param>
		/// <param name="count"></param>
		public static float ChaseSpeedSpread(int index, int count)
			=> count <= 1 ? 1f : 0.6f + 0.8f * (index / (float)(count - 1));

		/// <summary>
		/// How far apart the monsters in the ranged column stand.
		/// </summary>
		public const float RangedSpacing = 30f;

		/// <summary>
		/// Distances the penetration ranks stand at.
		/// </summary>
		public static readonly float[] PenetrationDistances = [30f, 60f, 90f];

		/// <summary>
		/// SDR carried by the three monsters in each penetration rank.
		/// </summary>
		public static readonly float[] PenetrationSdr = [3f, 2f, 1f];

		/// <summary>
		/// How far apart the monsters within one penetration rank stand.
		/// </summary>
		public const float PenetrationSpacing = 30f;

		/// <summary>
		/// Builds the penetration ranks: one monster of each SDR at every
		/// distance, so SDR and range vary independently.
		/// </summary>
		private static IEnumerable<ScenarioMob> PenetrationRanks()
		{
			foreach (var distance in PenetrationDistances)
			{
				for (var i = 0; i < PenetrationSdr.Length; ++i)
				{
					var offset = (i - (PenetrationSdr.Length - 1) / 2f) * PenetrationSpacing;

					yield return new ScenarioMob(distance, offset, PenetrationSdr[i]);
				}
			}
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
		private static IEnumerable<ScenarioMob> Cluster(float distance, float spacing, int count)
		{
			yield return new ScenarioMob(distance, 0);

			for (var i = 1; i < count; ++i)
			{
				var angle = (i - 1) / (float)Math.Max(1, count - 1) * MathF.PI * 2f;

				yield return new ScenarioMob(distance + MathF.Cos(angle) * spacing, MathF.Sin(angle) * spacing);
			}
		}

		/// <summary>
		/// Monsters per ring in the area placement.
		/// </summary>
		public const int MobsPerRing = 3;

		/// <summary>
		/// How much further out each successive ring sits.
		/// </summary>
		public const float RingSpacing = 30f;

		/// <summary>
		/// Seed the area placement's ring rotations are drawn from.
		/// </summary>
		/// <remarks>
		/// Fixed, so a skill's coverage is the same on every run and two
		/// scenarios at different counts share the rings they have in common.
		/// A local generator rather than RandomProvider, so nothing else in the
		/// harness can perturb the layout.
		/// </remarks>
		public const int AreaSeed = 20260807;

		/// <summary>
		/// Spreads monsters evenly over the ground: a few per ring, each ring
		/// one spacing further out, every ring rotated by its own fixed angle.
		/// </summary>
		/// <remarks>
		/// The rotation is what keeps this from being a straight line of
		/// targets, which a single narrow cone would sweep end to end and score
		/// as full coverage. The innermost ring is left unrotated so one
		/// monster stands directly ahead, at the point the skill is aimed: with
		/// every ring rotated, nothing sits on the aim point and every
		/// ground-targeted circle measures zero targets against a full field.
		/// </remarks>
		/// <param name="count"></param>
		public static IEnumerable<ScenarioMob> Rings(int count)
		{
			var rnd = new Random(AreaSeed);
			var placed = 0;

			for (var ring = 1; placed < count; ++ring)
			{
				var distance = ring * RingSpacing;
				var rotation = ring == 1 ? 0f : (float)(rnd.NextDouble() * Math.PI * 2);
				var here = Math.Min(MobsPerRing, count - placed);

				for (var i = 0; i < here; ++i)
				{
					var angle = rotation + i / (float)MobsPerRing * MathF.PI * 2f;

					yield return new ScenarioMob(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance);
				}

				placed += here;
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
