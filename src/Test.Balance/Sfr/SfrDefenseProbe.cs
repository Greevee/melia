using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Maps;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// What one press bought in damage prevented, measured against a mob that
	/// actually fights back rather than one that stands still.
	/// </summary>
	/// <remarks>
	/// This is the other half of "balance skills that provide crowd control
	/// debilitating monsters": SkillPressProbe answers what a press deals, and
	/// this answers what it stops the caster from taking, by running the same
	/// window twice against a live, aggressive mob - once with the press,
	/// once without - and reading the difference in incoming damage. Nothing
	/// here classifies a skill as "hard CC" or "defensive" by name; the mob's
	/// own reduced output is the measurement.
	/// </remarks>
	public class SfrDefenseResult
	{
		public string SkillClassName { get; init; }
		public int CharacterLevel { get; init; }
		public int SkillLevel { get; init; }

		/// <summary>
		/// Damage the mob landed on the caster when the caster did nothing.
		/// </summary>
		public float ControlDamageTaken { get; init; }

		/// <summary>
		/// Damage the mob landed on the caster after the press.
		/// </summary>
		public float TreatmentDamageTaken { get; init; }

		/// <summary>
		/// What the press bought in avoided damage, in units of the caster's
		/// own basic-attack swing against the same mob - the same unit
		/// SfrPricer's efficiency term is expressed in.
		/// </summary>
		public float SwingsPrevented { get; init; }

		public string Error { get; init; }

		public float DamagePrevented => Math.Max(0f, this.ControlDamageTaken - this.TreatmentDamageTaken);

		public override string ToString()
			=> this.Error != null
				? $"{this.SkillClassName}: FAILED ({this.Error})"
				: $"{this.SkillClassName} sk{this.SkillLevel} @lv{this.CharacterLevel}: " +
				  $"control {this.ControlDamageTaken:F0}, treatment {this.TreatmentDamageTaken:F0}, " +
				  $"{this.SwingsPrevented:0.00} swing(s) prevented";
	}

	/// <summary>
	/// Runs a skill's press against a live, hostile mob and reads what it
	/// bought in avoided damage.
	/// </summary>
	public static class SfrDefenseProbe
	{
		private const int TickMs = 25;

		/// <summary>
		/// Extra max HP so neither side's window ends in a corpse.
		/// </summary>
		private const float SurvivalHp = 100_000_000f;

		/// <summary>
		/// Measures one skill's defensive/CC value at single-target range.
		/// </summary>
		/// <remarks>
		/// Single target only (S1's placement): a mob that is free to move
		/// resolves the whole scenario matrix's fixed offsets meaningless the
		/// moment it starts chasing, so this reads one mob at melee range
		/// rather than trying to hold every scenario's geometry live too.
		/// </remarks>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="characterLevel"></param>
		/// <param name="windowMs"></param>
		/// <param name="arena"></param>
		/// <param name="pool">
		/// Arenas the trials run on. Each control/treatment pair is
		/// independent of the others, and every one of them is a full
		/// EncounterWindowMs of waiting, so running the trials serially made
		/// this the largest fixed cost of measuring a skill.
		/// </param>
		public static SfrDefenseResult Measure(JobEntry job, SkillId skillId, int skillLevel, int characterLevel, int windowMs = SfrDials.EncounterWindowMs, Map arena = null, ArenaPool pool = null)
		{
			var className = skillId.ToString();

			if (pool != null)
				return MeasureParallel(job, skillId, skillLevel, characterLevel, windowMs, pool);

			try
			{
				var map = arena ?? SyntheticActors.GetArena();
				var controlTotal = 0f;
				var treatmentTotal = 0f;
				var basicSwing = 0f;

				// A 10 s window holds only a handful of the mob's attacks, so
				// whether one of them lands a tick either side of the window
				// boundary swings the raw damage taken by a full swing's
				// worth on its own - real thread-scheduling jitter, not
				// something the RNG seed controls. Averaging several pairs is
				// what turns "prevented one swing or none, depending on
				// timing" into a stable number.
				for (var trial = 0; trial < SfrDials.DefenseProbeTrials; ++trial)
				{
					DeterministicRandom.Seed(SkillPressProbe.Seed + trial);
					try
					{
						controlTotal += RunWindow(job, skillId, skillLevel, characterLevel, windowMs, false, map, out className, out var swing);
						basicSwing = swing;

						DeterministicRandom.Seed(SkillPressProbe.Seed + trial);
						treatmentTotal += RunWindow(job, skillId, skillLevel, characterLevel, windowMs, true, map, out className, out _);
					}
					finally
					{
						DeterministicRandom.Reset();
					}
				}

				var control = controlTotal / SfrDials.DefenseProbeTrials;
				var treatment = treatmentTotal / SfrDials.DefenseProbeTrials;

				return new SfrDefenseResult
				{
					SkillClassName = className,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					ControlDamageTaken = control,
					TreatmentDamageTaken = treatment,
					SwingsPrevented = Math.Max(0f, control - treatment) / Math.Max(1f, basicSwing),
				};
			}
			catch (Exception ex)
			{
				return new SfrDefenseResult
				{
					SkillClassName = className,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					Error = ex.GetType().Name + ": " + ex.Message,
				};
			}
		}

		/// <summary>
		/// Runs every trial at once, each on its own pair of arenas.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="characterLevel"></param>
		/// <param name="windowMs"></param>
		/// <param name="pool"></param>
		private static SfrDefenseResult MeasureParallel(JobEntry job, SkillId skillId, int skillLevel, int characterLevel, int windowMs, ArenaPool pool)
		{
			// Nearly every skill in the roster buys no defensive value at all,
			// and a full DefenseProbeTrials on those is most of the run's
			// windows spent confirming a zero. A cheap first batch decides
			// whether the rest is worth measuring: only a skill that shows
			// something goes on to the trials that make the number stable
			// enough to price against.
			var scout = RunTrials(job, skillId, skillLevel, characterLevel, windowMs, pool, SfrDials.DefenseScoutTrials);

			if (scout.Error != null || scout.SwingsPrevented <= SfrDials.RiderDeadband * SfrDials.DefenseScoutMargin)
				return scout;

			return RunTrials(job, skillId, skillLevel, characterLevel, windowMs, pool, SfrDials.DefenseProbeTrials);
		}

		/// <summary>
		/// Runs the given number of control/treatment pairs at once and
		/// averages them.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="characterLevel"></param>
		/// <param name="windowMs"></param>
		/// <param name="pool"></param>
		/// <param name="count"></param>
		private static SfrDefenseResult RunTrials(JobEntry job, SkillId skillId, int skillLevel, int characterLevel, int windowMs, ArenaPool pool, int count)
		{
			var className = skillId.ToString();

			try
			{
				var controls = new float[count];
				var treatments = new float[count];
				var swings = new float[count];
				var names = new string[count];

				SkillPressProbe.RunAll(Enumerable.Range(0, count).Select(trial => (Action)(() =>
				{
					// Control and treatment stay on one thread so the seed they
					// share is the same thread-local RandomProvider.
					try
					{
						DeterministicRandom.Seed(SkillPressProbe.Seed + trial);
						controls[trial] = pool.Use(m => RunWindow(job, skillId, skillLevel, characterLevel, windowMs, false, m, out names[trial], out swings[trial]));

						DeterministicRandom.Seed(SkillPressProbe.Seed + trial);
						treatments[trial] = pool.Use(m => RunWindow(job, skillId, skillLevel, characterLevel, windowMs, true, m, out _, out _));
					}
					finally
					{
						DeterministicRandom.Reset();
					}
				})).ToArray());

				var control = controls.Average();
				var treatment = treatments.Average();
				var basicSwing = swings.FirstOrDefault(s => s > 0);

				return new SfrDefenseResult
				{
					SkillClassName = names.FirstOrDefault(n => n != null) ?? className,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					ControlDamageTaken = control,
					TreatmentDamageTaken = treatment,
					SwingsPrevented = Math.Max(0f, control - treatment) / Math.Max(1f, basicSwing),
				};
			}
			catch (Exception ex)
			{
				return new SfrDefenseResult
				{
					SkillClassName = className,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					Error = ex.GetType().Name + ": " + ex.Message,
				};
			}
		}

		/// <summary>
		/// Runs one half of the control/treatment pair and returns the
		/// caster's total damage taken.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="characterLevel"></param>
		/// <param name="windowMs"></param>
		/// <param name="useSkill"></param>
		/// <param name="arena"></param>
		/// <param name="className"></param>
		/// <param name="basicSwing"></param>
		private static float RunWindow(JobEntry job, SkillId skillId, int skillLevel, int characterLevel, int windowMs, bool useSkill,
			Map arena, out string className, out float basicSwing)
		{
			var character = (Character)null;
			var mob = (Mob)null;
			className = skillId.ToString();
			basicSwing = 0f;

			try
			{
				var stat = JobCatalog.GetPrimaryStat(job);

				character = SyntheticActors.CreateCharacter(job.JobId, characterLevel, StatSpread.AllIn(stat, characterLevel), arena: arena);
				ReferenceGear.Equip(character, job);

				var skill = SyntheticActors.GiveSkill(character, skillId, skillLevel);
				className = skill.Data.ClassName;

				var mobData = FindHostileReferenceMob(characterLevel);

				// Relative to the arena center, matching CreateMob/ResolvePosition's
				// contract - the character sits at center, so this is melee range.
				var meleeOffset = new Position(30f, 0f, 0f);

				mob = SyntheticActors.CreateHostileMob(mobData.Id, meleeOffset, character, arena);

				Fortify(character);
				Fortify(mob);
				Refill(character);

				character.Direction = character.Position.GetDirection(mob.Position);
				basicSwing = SfrDamageCurve.MitigatedAttack(AttackPower(character, skill), Defense(mob, skill));

				var tick = TimeSpan.FromMilliseconds(TickMs);

				using (var recorder = new SfrPressRecorder(character))
				{
					// Given the mob time to close and land its first swing
					// before either half starts counting, so the control run
					// is not measuring an empty aggro window.
					for (var elapsed = 0; elapsed < SfrDials.DefenseSettleMs; elapsed += TickMs)
					{
						Thread.Sleep(TickMs);
						arena.Update(tick);
						Refill(character);
					}

					recorder.Clear();

					if (useSkill)
						Dispatch(skill, character, mob);

					for (var elapsed = 0; elapsed < windowMs; elapsed += TickMs)
					{
						Thread.Sleep(TickMs);
						arena.Update(tick);
					}

					return recorder.DamageTakenByCaster();
				}
			}
			finally
			{
				SyntheticActors.Cleanup(character, mob);
			}
		}

		/// <summary>
		/// Returns a monster that can actually swing back: the plain
		/// reference mob (SpawnCensus.FindReferenceMob) is picked purely by
		/// HP for the reach probes, which never check whether it carries an
		/// attack skill at all - BasicMonster's Attack routine needs
		/// mob.Data.Skills non-empty or it just idles forever.
		/// </summary>
		/// <param name="level"></param>
		/// <param name="tolerance"></param>
		private static MonsterData FindHostileReferenceMob(int level, int tolerance = 30)
		{
			for (var offset = 0; offset <= tolerance; ++offset)
			{
				var candidateLevels = offset == 0 ? new[] { level } : new[] { level - offset, level + offset };

				foreach (var candidateLevel in candidateLevels)
				{
					var candidates = SpawnCensus.Mobs
						.Where(m => m.Data.Level == candidateLevel && m.Data.Rank == MonsterRank.Normal && m.Data.Skills.Count > 0)
						.OrderBy(m => m.Data.Hp)
						.ToArray();

					if (candidates.Length > 0)
						return candidates[candidates.Length / 2].Data;
				}
			}

			throw new InvalidOperationException(
				$"No attacking rank:Normal monster within {tolerance} levels of {level} spawns on an available map.");
		}

		/// <summary>
		/// Invokes the skill's handler once, mirroring what the packet
		/// handlers do for each use type.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="target"></param>
		private static void Dispatch(Skill skill, Character caster, Mob target)
		{
			var handlers = Melia.Zone.ZoneServer.Instance.SkillHandlers;
			var origin = caster.Position;
			var aimPos = target.Position;

			skill.Vars.Set(SkillPressProbe.GroundPosVariable, aimPos);

			switch (skill.Data.UseType)
			{
				case SkillUseType.MeleeGround when handlers.TryGetHandler<IMeleeGroundSkillHandler>(skill.Id, out var melee):
					melee.Handle(skill, caster, origin, aimPos, [target]);
					return;

				case SkillUseType.Self when handlers.TryGetHandler<ISelfSkillHandler>(skill.Id, out var self):
					self.Handle(skill, caster, origin, caster.Direction);
					return;

				case SkillUseType.Force when handlers.TryGetHandler<IForceSkillHandler>(skill.Id, out var force):
					force.Handle(skill, caster, origin, aimPos, target);
					return;

				case SkillUseType.ForceGround when handlers.TryGetHandler<IForceGroundSkillHandler>(skill.Id, out var forceGround):
					forceGround.Handle(skill, caster, origin, aimPos, target);
					return;
			}

			if (handlers.TryGetHandler<IGroundSkillHandler>(skill.Id, out var ground))
			{
				ground.Handle(skill, caster, origin, aimPos, target);
				return;
			}

			if (handlers.TryGetHandler<ITargetSkillHandler>(skill.Id, out var direct))
				direct.Handle(skill, caster, target);
		}

		/// <summary>
		/// Returns the attack the caster's own basic swing rolls against,
		/// averaged over its range, mirroring SCR_GetRandomAtk's choice of
		/// stat.
		/// </summary>
		/// <remarks>
		/// A caster's own PATK is near zero for a pure magic class, so
		/// reading it unconditionally floors basicSwing at the
		/// Math.Max(1f, ...) safety clamp in Measure() - dividing raw
		/// damage taken by 1 instead of by an actual swing, which is how
		/// Cryomancer_IciclePike measured 458 "swings" prevented. This
		/// mirrors SkillPressProbe.AttackPower's class-type branch so the
		/// unit means the same thing on both sides of the model.
		/// </remarks>
		/// <param name="caster"></param>
		/// <param name="skill"></param>
		private static float AttackPower(ICombatEntity caster, Skill skill)
		{
			var patk = (caster.Properties.GetFloat(PropertyName.MINPATK) + caster.Properties.GetFloat(PropertyName.MAXPATK)) / 2f;
			var matk = (caster.Properties.GetFloat(PropertyName.MINMATK) + caster.Properties.GetFloat(PropertyName.MAXMATK)) / 2f;

			return skill.Data.ClassType switch
			{
				SkillClassType.Responsive => Math.Max(patk, matk),
				<= SkillClassType.Missile => patk,
				_ => matk,
			};
		}

		/// <summary>
		/// Returns the defense the caster's own basic swing is opposed by,
		/// physical or magical as the skill's class type decides.
		/// </summary>
		/// <param name="target"></param>
		/// <param name="skill"></param>
		private static float Defense(ICombatEntity target, Skill skill)
		{
			var name = skill.Data.ClassType != SkillClassType.Magic ? PropertyName.DEF : PropertyName.MDEF;

			return target.Properties.GetFloat(name);
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
		/// Tops the caster back up so death or an empty SP pool never ends
		/// the window early.
		/// </summary>
		/// <param name="character"></param>
		private static void Refill(Character character)
		{
			character.Properties.SetFloat(PropertyName.HP, character.Properties.GetFloat(PropertyName.MHP));
			character.Properties.SetFloat(PropertyName.SP, character.Properties.GetFloat(PropertyName.MSP));
		}
	}
}
