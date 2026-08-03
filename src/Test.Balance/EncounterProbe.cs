using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Maps;

namespace Melia.Test.Balance
{
	/// <summary>
	/// What a skill actually did over a stretch of combat, counting every
	/// source of damage rather than only the cast that started it.
	/// </summary>
	public class EncounterResult
	{
		public string JobPrefix { get; init; }
		public string SkillClassName { get; init; }
		public string ScenarioId { get; init; }
		public int CharacterLevel { get; init; }
		public int SkillLevel { get; init; }
		public int EnemyCount { get; init; }

		/// <summary>
		/// Times the handler was actually invoked.
		/// </summary>
		public int Casts { get; init; }

		public float Seconds { get; init; }

		/// <summary>
		/// Every point of HP taken off the enemies during the window, from
		/// direct hits, pad ticks, summons, damage over time and anything
		/// else the handler set in motion.
		/// </summary>
		public float TotalDamage { get; init; }

		/// <summary>
		/// Pads the skill left on the map.
		/// </summary>
		public int PadsCreated { get; init; }

		/// <summary>
		/// Monsters the caster ended up owning, which is how summons show up.
		/// </summary>
		public int SummonsCreated { get; init; }

		/// <summary>
		/// Whether an enemy died despite the healing, which would mean some
		/// damage went unmeasured.
		/// </summary>
		public bool EnemyDied { get; init; }

		public string Error { get; init; }

		public float Dps => this.Seconds <= 0 ? 0 : this.TotalDamage / this.Seconds;

		public override string ToString()
			=> this.Error != null
				? $"{this.ScenarioId} {this.SkillClassName}: FAILED ({this.Error})"
				: $"{this.ScenarioId} {this.SkillClassName} sk{this.SkillLevel} @lv{this.CharacterLevel}: " +
				  $"{this.Casts} cast(s) over {this.Seconds:F1}s, {this.TotalDamage:F0} total, {this.Dps:F0} dps" +
				  $"{(this.PadsCreated > 0 ? $", {this.PadsCreated} pad(s)" : "")}" +
				  $"{(this.SummonsCreated > 0 ? $", {this.SummonsCreated} summon(s)" : "")}";
	}

	/// <summary>
	/// Runs a skill through its real handler and measures everything it
	/// causes, rather than only the hit it lands itself.
	/// </summary>
	/// <remarks>
	/// This is what SkillProfiler cannot do. Sampling SCR_SkillHit prices the
	/// direct hit and nothing else, so pads, summoned attackers, damage over
	/// time and chained effects all read as zero - which understates whole
	/// classes whose damage is mostly indirect.
	///
	/// Handlers pace themselves with Task.Delay and pads tick on real
	/// intervals, so the window runs in wall-clock time. That makes this
	/// expensive; it is the measurement of record, not the one to iterate on.
	/// </remarks>
	public static class EncounterProbe
	{
		/// <summary>
		/// Default length of a measured window.
		/// </summary>
		public const float DefaultSeconds = 6f;

		/// <summary>
		/// How often the map is ticked. Short enough that pad intervals and
		/// summon AI behave, long enough not to burn the run on overhead.
		/// </summary>
		private const int TickMs = 50;

		/// <summary>
		/// Extra max HP given to everything involved, so a long window does
		/// not end early with a corpse.
		/// </summary>
		private const float SurvivalHp = 100_000_000f;

		/// <summary>
		/// Measures one skill in one scenario over a window of combat.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="spec"></param>
		/// <param name="characterLevel"></param>
		/// <param name="seconds"></param>
		public static EncounterResult Measure(JobEntry job, SkillId skillId, int skillLevel, ScenarioSpec spec, int characterLevel, float seconds = DefaultSeconds)
		{
			var character = (Character)null;
			var mobs = new List<Mob>();

			try
			{
				var stat = JobCatalog.GetPrimaryStat(job);

				character = SyntheticActors.CreateCharacter(job.JobId, characterLevel, StatSpread.AllIn(stat, characterLevel));
				ReferenceGear.Equip(character, job);

				var skill = SyntheticActors.GiveSkill(character, skillId, skillLevel);
				var cycle = CastCycleModel.Measure(character, skill);

				var tolerance = spec.Rank == MonsterRank.Normal ? 8 : 30;
				var mobData = SpawnCensus.FindReferenceMob(Math.Max(1, characterLevel + spec.LevelOffset), spec.Rank, tolerance, out _);
				var offsets = ScenarioMatrix.GetOffsets(spec, spec.MobCount, cycle.CastTimeMs, mobData.RunSpeed,
					Math.Max(1, characterLevel - 9), characterLevel + 9, out var aimDistance);

				foreach (var offset in offsets)
					mobs.Add(SyntheticActors.CreateMob(mobData.Id, offset));

				foreach (var mob in mobs)
					Fortify(mob);

				Fortify(character);

				var aimPos = new Position(character.Position.X + aimDistance, character.Position.Y, character.Position.Z);
				character.Direction = character.Position.GetDirection(aimPos);

				var map = SyntheticActors.GetArena();
				var padsBefore = CountPads(map);
				var monstersBefore = CountMonsters(map);

				var damage = 0f;
				var casts = 0;
				var died = false;
				var tick = TimeSpan.FromMilliseconds(TickMs);
				var ticks = (int)Math.Ceiling(seconds * 1000f / TickMs);

				// Some handlers never start their own cooldown, because in play
				// the packet handler does it. Without a floor those recast
				// every tick, so the cycle model paces them as well.
				var minCastMs = cycle.CastsPerCycle <= 0 ? TickMs : cycle.CycleMs / cycle.CastsPerCycle;
				var sinceCastMs = float.MaxValue;

				for (var i = 0; i < ticks; ++i)
				{
					if (sinceCastMs >= minCastMs && TryCast(skill, character, aimPos, mobs))
					{
						++casts;
						sinceCastMs = 0;
					}
					else
					{
						sinceCastMs += TickMs;
					}

					Thread.Sleep(TickMs);
					map.Update(tick);

					damage += Drain(mobs, ref died);

					// Keep the caster on its feet and able to keep casting, so
					// the window measures the skill rather than attrition.
					Refill(character);
				}

				damage += Drain(mobs, ref died);

				return new EncounterResult
				{
					JobPrefix = job.SkillPrefix,
					SkillClassName = skill.Data.ClassName,
					ScenarioId = spec.Id,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					EnemyCount = mobs.Count,
					Casts = casts,
					Seconds = seconds,
					TotalDamage = damage,
					PadsCreated = Math.Max(0, CountPads(map) - padsBefore),
					SummonsCreated = Math.Max(0, CountMonsters(map) - monstersBefore - mobs.Count),
					EnemyDied = died,
				};
			}
			catch (Exception ex)
			{
				return new EncounterResult
				{
					JobPrefix = job.SkillPrefix,
					SkillClassName = skillId.ToString(),
					ScenarioId = spec.Id,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					Seconds = seconds,
					Error = ex.GetType().Name + ": " + ex.Message,
				};
			}
			finally
			{
				SyntheticActors.Cleanup(character, mobs.ToArray());
			}
		}

		/// <summary>
		/// Invokes the skill's handler if it is ready, returning whether it
		/// was cast.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="aimPos"></param>
		/// <param name="mobs"></param>
		private static bool TryCast(Skill skill, Character caster, Position aimPos, List<Mob> mobs)
		{
			if (caster.IsOnCooldown(skill.Id) || caster.IsCasting())
				return false;

			var targets = mobs.Cast<ICombatEntity>().Where(caster.CanDamage).ToList();
			var handlers = ZoneServer.Instance.SkillHandlers;
			var origin = caster.Position;

			// Dispatch mirrors what the packet handlers do for each use type,
			// so the handler runs exactly as it would in play.
			switch (skill.Data.UseType)
			{
				case SkillUseType.MeleeGround when handlers.TryGetHandler<IMeleeGroundSkillHandler>(skill.Id, out var melee):
					melee.Handle(skill, caster, origin, aimPos, targets);
					return true;

				case SkillUseType.Self when handlers.TryGetHandler<ISelfSkillHandler>(skill.Id, out var self):
					self.Handle(skill, caster, origin, caster.Direction);
					return true;

				case SkillUseType.Force when handlers.TryGetHandler<IForceSkillHandler>(skill.Id, out var force):
					force.Handle(skill, caster, origin, aimPos, targets.FirstOrDefault());
					return true;

				case SkillUseType.ForceGround when handlers.TryGetHandler<IForceGroundSkillHandler>(skill.Id, out var forceGround):
					forceGround.Handle(skill, caster, origin, aimPos, targets.FirstOrDefault());
					return true;
			}

			if (handlers.TryGetHandler<IGroundSkillHandler>(skill.Id, out var ground))
			{
				ground.Handle(skill, caster, origin, aimPos, targets.FirstOrDefault());
				return true;
			}

			if (handlers.TryGetHandler<ITargetSkillHandler>(skill.Id, out var target))
			{
				target.Handle(skill, caster, targets.FirstOrDefault());
				return true;
			}

			return false;
		}

		/// <summary>
		/// Takes the damage dealt so far off the books and heals the enemies
		/// back up, which is how a window longer than one kill is measured at
		/// all.
		/// </summary>
		/// <param name="mobs"></param>
		/// <param name="died"></param>
		private static float Drain(List<Mob> mobs, ref bool died)
		{
			var total = 0f;

			foreach (var mob in mobs)
			{
				var max = mob.Properties.GetFloat(PropertyName.MHP);
				var current = mob.Properties.GetFloat(PropertyName.HP);

				if (mob.IsDead)
					died = true;

				if (current >= max)
					continue;

				total += max - current;
				mob.Properties.SetFloat(PropertyName.HP, max);
			}

			return total;
		}

		/// <summary>
		/// Gives an entity enough max HP that the window runs to its end.
		/// </summary>
		/// <param name="entity"></param>
		private static void Fortify(ICombatEntity entity)
		{
			entity.Properties.SetFloat(PropertyName.MHP_BM, SurvivalHp);
			entity.Properties.Invalidate(PropertyName.MHP);
			entity.Properties.SetFloat(PropertyName.HP, entity.Properties.GetFloat(PropertyName.MHP));
		}

		/// <summary>
		/// Tops the caster back up so neither death nor an empty SP pool ends
		/// the window early.
		/// </summary>
		/// <param name="character"></param>
		private static void Refill(Character character)
		{
			character.Properties.SetFloat(PropertyName.HP, character.Properties.GetFloat(PropertyName.MHP));
			character.Properties.SetFloat(PropertyName.SP, character.Properties.GetFloat(PropertyName.MSP));
		}

		private static int CountPads(Map map)
			=> map.GetPads(_ => true).Length;

		private static int CountMonsters(Map map)
			=> map.GetMonsters().Length;
	}
}
