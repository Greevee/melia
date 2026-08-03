using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Maps;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Why a row measured nothing, so an expected zero can be told apart
	/// from a broken one.
	/// </summary>
	public enum ZeroReason
	{
		/// <summary>
		/// The row measured damage.
		/// </summary>
		None,

		/// <summary>
		/// The skill's range cannot span the distance the scenario put its
		/// monsters at. This is what S5 is for.
		/// </summary>
		OutOfReach,

		/// <summary>
		/// The skill carries no usable splash shape at all.
		/// </summary>
		NoGeometry,

		/// <summary>
		/// The shape resolved but contained no monster.
		/// </summary>
		NoTargetsInShape,

		/// <summary>
		/// Monsters were reached and took nothing, which is what an indirect
		/// skill looks like from the direct-hit model.
		/// </summary>
		NoDirectDamage,

		/// <summary>
		/// The cast rhythm allows no casts, so damage per cast never becomes
		/// damage per second.
		/// </summary>
		NoCastRate,
	}

	/// <summary>
	/// One measured row: a skill, at a level, in a scenario.
	/// </summary>
	public class SkillProfile
	{
		public SkillRole Role { get; init; }

		/// <summary>
		/// Why the row is zero, or None when it is not.
		/// </summary>
		public ZeroReason Zero { get; init; }

		public string ScenarioId { get; init; }
		public string JobPrefix { get; init; }
		public string SkillClassName { get; init; }
		public int CharacterLevel { get; init; }
		public int SkillLevel { get; init; }
		public int MobLevel { get; init; }
		public MonsterRank MobRank { get; init; }
		public string MobClassName { get; init; }
		public string Gear { get; init; }

		/// <summary>
		/// Monsters the skill's geometry and SR actually reached. Zero means
		/// the scenario put them out of reach.
		/// </summary>
		public int TargetsReached { get; init; }

		/// <summary>
		/// Effective damage one cast lands in total, across every target it
		/// reached and every hit it makes, with dodges counted as zero.
		/// </summary>
		public float DamagePerCast { get; init; }

		/// <summary>
		/// The same measurement for a basic attack in the same scenario, so
		/// the skill can be priced against it rather than against a constant.
		/// </summary>
		public float BasicAttackPerCast { get; init; }

		/// <summary>
		/// Swings per second the same character lands with a basic attack, so
		/// a class can be priced against its own swing rate instead of a
		/// global constant.
		/// </summary>
		public float BasicCastsPerSecond { get; init; }

		/// <summary>
		/// STR or INT, derived from the class's own damage skills. Says
		/// whether BasicAttackPerCast is a meaningful yardstick for it.
		/// </summary>
		public string PrimaryStat { get; init; }

		/// <summary>
		/// Monsters the basic attack's own geometry reached in this scenario.
		/// A skill narrower than a swing is a cost, and without this the
		/// comparison silently credits it with the swing's reach.
		/// </summary>
		public int BasicTargetsReached { get; init; }

		/// <summary>
		/// Damage applications one cast makes against a single target, from
		/// the handler rather than the dead multiHitCount field.
		/// </summary>
		public int HitsPerCast { get; init; }

		public float CastsPerSecond { get; init; }
		public float SpPerSecond { get; init; }
		public float DodgeRate { get; init; }
		public float BlockRate { get; init; }
		public float CritRate { get; init; }
		public CastCycle Cycle { get; init; }

		/// <summary>
		/// Damage per second the cast rhythm sustains, which is the number
		/// scenarios are actually compared on.
		/// </summary>
		public float Dps => this.DamagePerCast * this.CastsPerSecond;

		/// <summary>
		/// How many times the skill's output beats a basic attack's in this
		/// scenario, on a per-second basis.
		/// </summary>
		public float TimesBasic { get; init; }

		/// <summary>
		/// The same against the reference basic attack - a STR character of
		/// the same level with the same gear tier - rather than the caster's
		/// own. A full-INT class cannot meaningfully swing its weapon, so its
		/// own basic attack is near zero and TimesBasic explodes; this is the
		/// unit every class can be compared in.
		/// </summary>
		public float TimesReference { get; init; }

		/// <summary>
		/// Damage the reference basic attack lands in one swing, in the same
		/// scenario.
		/// </summary>
		public float ReferenceDamagePerCast { get; init; }

		/// <summary>
		/// How big one press is against one swing. A skill on a long cooldown
		/// is supposed to be far above 1 here even when its sustained output
		/// is below it - that is what the cooldown buys.
		/// </summary>
		public float BurstTimesReference => this.ReferenceDamagePerCast <= 0 ? 0 : this.DamagePerCast / this.ReferenceDamagePerCast;

		/// <summary>
		/// Casts needed to kill the primary target, or zero if the skill
		/// cannot reach or hurt it.
		/// </summary>
		public float CastsToKill { get; init; }

		public float SecondsToKill => this.CastsPerSecond <= 0 ? 0 : this.CastsToKill / this.CastsPerSecond;

		/// <summary>
		/// Whether the SP pool sustains the rhythm for at least half a
		/// minute, which is the plan's SP-sustainability gate.
		/// </summary>
		public bool SpSustainable { get; init; }

		public override string ToString()
			=> $"{this.ScenarioId} {this.SkillClassName} sk{this.SkillLevel} @lv{this.CharacterLevel} vs lv{this.MobLevel} {this.MobRank}: " +
			   $"{this.TargetsReached} target(s), {this.DamagePerCast:F0}/cast, {this.Dps:F0} dps ({this.TimesBasic:F2}x basic), " +
			   $"{this.CastsToKill:F1} casts to kill, {this.SpPerSecond:F1} sp/s{(this.SpSustainable ? "" : " NOT SUSTAINABLE")}";
	}

	/// <summary>
	/// Runs a skill through the scenario matrix against the real handlers'
	/// damage pipeline, and reports what it actually does.
	/// </summary>
	public static class SkillProfiler
	{
		/// <summary>
		/// Seconds of continuous casting the SP pool has to cover for a
		/// skill to count as sustainable.
		/// </summary>
		public const float SustainSeconds = 30f;

		/// <summary>
		/// Samples per target. Lower than the smoke tests' default because a
		/// full matrix run does thousands of these.
		/// </summary>
		public const int Samples = 150;

		/// <summary>
		/// The class whose basic attack every other class is measured
		/// against. A plain STR Swordsman with a sword is the yardstick the
		/// plan's R1/R2 reference scenarios are written in.
		/// </summary>
		public const string ReferenceJob = "Swordman";

		private static readonly Dictionary<(string, int), (float PerCast, float Dps)> _referenceBasic = new();
		private static readonly object _referenceLock = new();

		/// <summary>
		/// Measures one skill in one scenario at one level.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="entry"></param>
		/// <param name="skillLevel"></param>
		/// <param name="spec"></param>
		/// <param name="characterLevel"></param>
		/// <param name="grade"></param>
		public static SkillProfile Measure(JobEntry job, SkillEntry entry, int skillLevel, ScenarioSpec spec, int characterLevel, ItemGrade grade = ItemGrade.Normal)
			=> Measure(job, entry, skillLevel, spec, characterLevel, grade, withReference: true);

		/// <summary>
		/// Returns the DPS a reference STR character of the given level lands
		/// with a plain basic attack in the given scenario, which is the unit
		/// every class's output is reported in.
		/// </summary>
		/// <remarks>
		/// Cached, since it depends only on the level and the scenario, and
		/// measured through the same path as everything else so the two
		/// numbers are commensurable.
		/// </remarks>
		/// <param name="spec"></param>
		/// <param name="characterLevel"></param>
		public static (float PerCast, float Dps) GetReferenceBasic(ScenarioSpec spec, int characterLevel)
		{
			var key = (spec.Id, characterLevel);

			lock (_referenceLock)
			{
				if (_referenceBasic.TryGetValue(key, out var cached))
					return cached;
			}

			var job = JobCatalog.Entries.First(e => e.SkillPrefix == ReferenceJob);
			var entry = JobCatalog.GetProfiledSkills(job).FirstOrDefault(s => s.Id == SkillId.Normal_Attack)
				?? new SkillEntry { Data = ZoneServer.Instance.Data.SkillDb.Find(SkillId.Normal_Attack), Role = SkillRole.Direct, MaxLevel = 1 };

			var profile = Measure(job, entry, 1, spec, characterLevel, ItemGrade.Normal, withReference: false);
			var result = (profile.DamagePerCast, profile.Dps);

			lock (_referenceLock)
				_referenceBasic[key] = result;

			return result;
		}

		/// <summary>
		/// Returns the reference basic attack's sustained output.
		/// </summary>
		/// <param name="spec"></param>
		/// <param name="characterLevel"></param>
		public static float GetReferenceBasicDps(ScenarioSpec spec, int characterLevel)
			=> GetReferenceBasic(spec, characterLevel).Dps;

		private static SkillProfile Measure(JobEntry job, SkillEntry entry, int skillLevel, ScenarioSpec spec, int characterLevel, ItemGrade grade, bool withReference)
		{
			var skillId = entry.Id;

			// Resolved before this measurement puts anything on the arena: the
			// reference run resolves its own targets there, and monsters left
			// standing by the caller would inflate it - permanently, since it
			// is cached.
			var reference = withReference ? GetReferenceBasic(spec, characterLevel) : default;

			var stat = JobCatalog.GetPrimaryStat(job);
			var character = SyntheticActors.CreateCharacter(job.JobId, characterLevel, StatSpread.AllIn(stat, characterLevel));
			var mobs = new List<Mob>();

			try
			{
				var gear = ReferenceGear.Equip(character, job, grade);

				var basicId = BasicAttacks.For(job, gear.Weapon?.Data.EquipType1 ?? EquipType.None);

				var skill = SyntheticActors.GiveSkill(character, skillId, skillLevel);
				var basic = SyntheticActors.GiveSkill(character, basicId, 1);

				var cycle = CastCycleModel.Measure(character, skill);
				var basicCycle = CastCycleModel.Measure(character, basic);

				var mobLevel = Math.Max(1, characterLevel + spec.LevelOffset);

				// Normals sit at nearly every level; elites and bosses are
				// sparse enough that a narrow search would just fail.
				var tolerance = spec.Rank == MonsterRank.Normal ? 8 : 30;

				var primaryData = SpawnCensus.FindReferenceMob(mobLevel, spec.Rank, tolerance, out var primaryLevel);
				var offsets = ScenarioMatrix.GetOffsets(spec, spec.MobCount, cycle.CastTimeMs, primaryData.RunSpeed,
					Math.Max(1, primaryLevel - 9), primaryLevel + 9, out var aimDistance);

				foreach (var offset in offsets)
					mobs.Add(SyntheticActors.CreateMob(primaryData.Id, offset));

				var aimPos = AimAt(character, aimDistance);

				var reached = ResolveTargets(character, skill, aimPos, out var outOfReach, out var hasGeometry);
				var basicReached = ResolveTargets(character, basic, aimPos, out _, out _);

				var samples = HitSampler.SampleAll(character, reached, skill, Samples);
				var basicSamples = HitSampler.SampleAll(character, basicReached, basic, Samples);

				var damagePerCast = samples.Sum(s => s.EffectivePerCast);
				var basicPerCast = basicSamples.Sum(s => s.EffectivePerCast);
				var basicDps = basicPerCast * basicCycle.CastsPerSecond;

				var primary = samples.FirstOrDefault();
				var primaryMob = reached.FirstOrDefault();
				var castsToKill = 0f;

				if (primary != null && primaryMob != null && primary.EffectivePerCast > 0)
					castsToKill = primaryMob.Properties.GetFloat(PropertyName.MHP) / primary.EffectivePerCast;

				var dps = damagePerCast * cycle.CastsPerSecond;
				var maxSp = character.Properties.GetFloat(PropertyName.MSP);

				return new SkillProfile
				{
					Role = entry.Role,
					Zero = Explain(dps, reached.Length, outOfReach, hasGeometry, cycle.CastsPerSecond),
					ScenarioId = spec.Id,
					JobPrefix = job.SkillPrefix,
					SkillClassName = skill.Data.ClassName,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					MobLevel = primaryLevel,
					MobRank = spec.Rank,
					MobClassName = primaryData.ClassName,
					Gear = gear.ToString(),
					TargetsReached = reached.Length,
					DamagePerCast = damagePerCast,
					BasicAttackPerCast = basicPerCast,
					BasicCastsPerSecond = basicCycle.CastsPerSecond,
					PrimaryStat = stat,
					BasicTargetsReached = basicReached.Length,
					HitsPerCast = primary?.HitsPerCast ?? 1,
					CastsPerSecond = cycle.CastsPerSecond,
					SpPerSecond = cycle.SpPerSecond,
					DodgeRate = primary?.DodgeRate ?? 0,
					BlockRate = primary?.BlockRate ?? 0,
					CritRate = primary?.CritRate ?? 0,
					Cycle = cycle,
					TimesBasic = basicDps <= 0 ? 0 : dps / basicDps,
					TimesReference = reference.Dps <= 0 ? 0 : dps / reference.Dps,
					ReferenceDamagePerCast = reference.PerCast,
					CastsToKill = castsToKill,
					SpSustainable = cycle.SpPerSecond <= 0 || maxSp / cycle.SpPerSecond >= SustainSeconds,
				};
			}
			finally
			{
				SyntheticActors.Cleanup(character, mobs.ToArray());
			}
		}

		/// <summary>
		/// Measures one skill across the whole matrix at the given levels.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="entry"></param>
		/// <param name="skillLevels"></param>
		/// <param name="characterLevels"></param>
		public static SkillProfile[] MeasureAll(JobEntry job, SkillEntry entry, int[] skillLevels = null, int[] characterLevels = null)
		{
			skillLevels ??= ScenarioMatrix.SkillLevels;
			characterLevels ??= ScenarioMatrix.CharacterLevels;

			var profiles = new List<SkillProfile>();

			foreach (var characterLevel in ScenarioMatrix.CharacterLevelsFor(job, characterLevels))
			{
				foreach (var skillLevel in ScenarioMatrix.SkillLevelsFor(entry, skillLevels))
				{
					foreach (var spec in ScenarioMatrix.All)
						profiles.Add(Measure(job, entry, skillLevel, spec, characterLevel));
				}
			}

			return profiles.ToArray();
		}

		/// <summary>
		/// Returns why a row measured nothing, so the report can separate a
		/// scenario doing its job from a skill the model cannot price.
		/// </summary>
		/// <param name="dps"></param>
		/// <param name="targets"></param>
		/// <param name="outOfReach"></param>
		/// <param name="hasGeometry"></param>
		/// <param name="castsPerSecond"></param>
		private static ZeroReason Explain(float dps, int targets, bool outOfReach, bool hasGeometry, float castsPerSecond)
		{
			if (dps > 0)
				return ZeroReason.None;

			if (outOfReach)
				return ZeroReason.OutOfReach;

			// Geometry only explains a zero when nothing was reached; a skill
			// with no usable shape still falls back to the nearest target, and
			// if that target took nothing the shape was never the problem.
			if (targets == 0)
				return hasGeometry ? ZeroReason.NoTargetsInShape : ZeroReason.NoGeometry;

			if (castsPerSecond <= 0)
				return ZeroReason.NoCastRate;

			return ZeroReason.NoDirectDamage;
		}

		/// <summary>
		/// Turns the character towards the aim point and returns it, since
		/// splash geometry is built from the caster's facing.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="distance"></param>
		private static Position AimAt(Character character, float distance)
		{
			var aimPos = new Position(character.Position.X + distance, character.Position.Y, character.Position.Z);

			character.Direction = character.Position.GetDirection(aimPos);

			return aimPos;
		}

		/// <summary>
		/// Returns the monsters the skill's splash geometry and SR actually
		/// reach, which is the term the plan calls EffTargets and the reason
		/// splashRate alone is not the answer.
		/// </summary>
		/// <param name="caster"></param>
		/// <param name="skill"></param>
		/// <param name="aimPos"></param>
		/// <param name="outOfReach"></param>
		/// <param name="hasGeometry"></param>
		private static ICombatEntity[] ResolveTargets(Character caster, Skill skill, Position aimPos, out bool outOfReach, out bool hasGeometry)
		{
			var map = SyntheticActors.GetArena();
			var reach = Math.Max(skill.Data.MaxRange, Math.Max(skill.Data.SplashHeight * 2, skill.Data.SplashRange * 2));

			hasGeometry = true;

			// A skill that cannot reach the aim point hits nothing, which is
			// how the ranged scenario prices melee-only reach.
			outOfReach = reach > 0 && caster.Position.Get2DDistance(aimPos) > reach;

			if (outOfReach)
				return [];

			var candidates = GetInSplash(caster, skill, aimPos, map, out hasGeometry);

			return candidates
				.OfType<Mob>()
				.Cast<ICombatEntity>()
				.LimitBySDR(caster, skill)
				.ToArray();
		}

		/// <summary>
		/// Returns everything inside the skill's splash shape, falling back
		/// to a plain radius for the splash types that have no shape.
		/// </summary>
		/// <remarks>
		/// This is a model of the client's hit list, not the server's own
		/// computation: for SkillUseType.MeleeGround the client sends which
		/// entities it hit and the server only validates them, so nothing
		/// server-side derives targets from the geometry for those skills.
		/// Handlers that do compute splash themselves use these same fields.
		/// </remarks>
		/// <param name="caster"></param>
		/// <param name="skill"></param>
		/// <param name="aimPos"></param>
		/// <param name="map"></param>
		/// <param name="hasGeometry"></param>
		private static List<ICombatEntity> GetInSplash(Character caster, Skill skill, Position aimPos, Map map, out bool hasGeometry)
		{
			var data = skill.Data;
			var type = data.SplashType;

			// splashHeight is 0 on most melee skills, which would build a
			// zero-length rectangle that contains nothing. waveLength is the
			// depth those skills carry instead.
			var length = data.SplashHeight > 0 ? data.SplashHeight * 2 : data.WaveLength * 2;
			var width = data.SplashRange * 2;

			// A projectile takes the one target the client picked, so its
			// splash fields describe nothing and resolving them finds nothing.
			if (data.UseType == SkillUseType.Force)
			{
				hasGeometry = true;

				return map.GetAttackableEntitiesInRange(caster, aimPos, ScenarioMatrix.MeleeDistance)
					.OrderBy(e => e.Position.Get2DDistance(aimPos))
					.Take(1)
					.ToList();
			}

			// A Fan with no angle is a rectangle, not a cone.
			if (type == SplashType.Fan && data.SplashAngle <= 0)
				type = SplashType.Square;

			// GetSplashArea centres a Circle on FarPos, which
			// GetSplashParameters places `length` ahead of the caster. A
			// ground-targeted circle belongs on the aim point instead, so the
			// length it is built from is the distance to it.
			if (type == SplashType.Circle)
				length = (float)caster.Position.Get2DDistance(aimPos);

			hasGeometry = width > 0 && (length > 0 || type == SplashType.Circle) && type is SplashType.Fan or SplashType.Square or SplashType.Circle;

			if (hasGeometry)
			{
				var param = skill.GetSplashParameters(caster, caster.Position, aimPos, length, width, data.SplashAngle);
				var area = skill.GetSplashArea(type, param);

				return map.GetAttackableEntitiesIn(caster, area);
			}

			// No usable geometry at all, so only whatever is nearest the aim
			// point is struck.
			return map.GetAttackableEntitiesInRange(caster, aimPos, ScenarioMatrix.MeleeDistance)
				.OrderBy(e => e.Position.Get2DDistance(aimPos))
				.Take(1)
				.ToList();
		}
	}
}
