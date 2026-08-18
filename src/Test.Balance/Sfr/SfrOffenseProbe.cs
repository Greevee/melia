using System;
using System.Linq;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.Util;
using Melia.Shared.World;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Maps;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// What one press bought in damage on everything else the caster does,
	/// measured against a swing that is otherwise identical.
	/// </summary>
	/// <remarks>
	/// The third thing a press can pay in. SkillPressProbe answers what it
	/// deals, SfrDefenseProbe what it stops the caster from taking, and this
	/// answers what it makes the rest of the rotation hit for - a stacking
	/// self-buff like Scout_ObliqueFire's, a debuff that raises the damage a
	/// mob takes, an attack-speed buff that buys extra swings. All three are
	/// budget the press spends on something other than its own number, and
	/// only the first was priced.
	///
	/// Nothing here classifies a buff by name or reads a handler. The caster
	/// swings its own basic attack on a fixed cadence in both halves of a pair,
	/// and the reading is what those swings did after the press against what
	/// they did without it.
	/// </remarks>
	public class SfrOffenseResult
	{
		public string SkillClassName { get; init; }
		public int CharacterLevel { get; init; }
		public int SkillLevel { get; init; }

		/// <summary>
		/// Damage the caster's swings dealt with no press in front of them.
		/// </summary>
		public float ControlDamageDealt { get; init; }

		/// <summary>
		/// Damage the same swings dealt after the press.
		/// </summary>
		public float TreatmentDamageDealt { get; init; }

		/// <summary>
		/// How many times the treatment half pressed the skill before it
		/// started counting.
		/// </summary>
		public int Presses { get; init; }

		/// <summary>
		/// The fractional gain the press bought on the caster's other damage:
		/// 0.25 for a press that leaves everything else hitting 25% harder,
		/// zero for one that changes nothing.
		/// </summary>
		public float Amplification { get; init; }

		public string Error { get; init; }

		public override string ToString()
			=> this.Error != null
				? $"{this.SkillClassName}: FAILED ({this.Error})"
				: $"{this.SkillClassName} sk{this.SkillLevel} @lv{this.CharacterLevel}: " +
				  $"control {this.ControlDamageDealt:F0}, treatment {this.TreatmentDamageDealt:F0} after {this.Presses} press(es), " +
				  $"+{this.Amplification:P0} on everything else";
	}

	/// <summary>
	/// Runs a skill's press in front of a run of ordinary swings and reads what
	/// it added to them.
	/// </summary>
	public static class SfrOffenseProbe
	{
		private const int TickMs = 25;

		/// <summary>
		/// Measures what one skill amplifies the caster's other damage by.
		/// </summary>
		/// <remarks>
		/// Single target at melee range, like the defensive probe: what a buff
		/// is worth per point is not a function of the pull's shape, and the
		/// reach the press itself has is already priced as width.
		/// </remarks>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="characterLevel"></param>
		/// <param name="windowMs"></param>
		/// <param name="arena"></param>
		/// <param name="pool"></param>
		public static SfrOffenseResult Measure(JobEntry job, SkillId skillId, int skillLevel, int characterLevel, int windowMs = SfrDials.OffenseWindowMs, Map arena = null, ArenaPool pool = null)
		{
			var className = skillId.ToString();
			var count = Math.Max(1, SfrDials.OffenseProbeTrials);

			try
			{
				var controls = new float[count];
				var treatments = new float[count];
				var presses = new int[count];
				var names = new string[count];

				void Trial(int trial)
				{
					// Control and treatment share a seed, a thread and one arena,
					// so the difference between them belongs inside the pair -
					// the same arrangement the defence probe's windows use.
					try
					{
						Run(m =>
						{
							DeterministicRandom.Seed(SkillPressProbe.Seed + trial);
							controls[trial] = RunWindow(job, skillId, skillLevel, characterLevel, windowMs, 0, m, SkillPressProbe.Seed + trial, out names[trial], out _);

							DeterministicRandom.Seed(SkillPressProbe.Seed + trial);
							treatments[trial] = RunWindow(job, skillId, skillLevel, characterLevel, windowMs, SfrDials.OffensePresses, m, SkillPressProbe.Seed + trial, out _, out presses[trial]);

							return 0f;
						});
					}
					finally
					{
						DeterministicRandom.Reset();
					}
				}

				float Run(Func<Map, float> pair)
					=> pool == null ? pair(arena ?? SyntheticActors.GetArena()) : pool.Use(pair);

				if (pool == null)
				{
					for (var trial = 0; trial < count; ++trial)
						Trial(trial);
				}
				else
				{
					SkillPressProbe.RunAll(Enumerable.Range(0, count).Select(trial => (Action)(() => Trial(trial))).ToArray());
				}

				return new SfrOffenseResult
				{
					SkillClassName = names.FirstOrDefault(n => n != null) ?? className,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					ControlDamageDealt = controls.Average(),
					TreatmentDamageDealt = treatments.Average(),
					Presses = presses.Max(),
					Amplification = PairedTrimmedGain(controls, treatments),
				};
			}
			catch (Exception ex)
			{
				return new SfrOffenseResult
				{
					SkillClassName = className,
					CharacterLevel = characterLevel,
					SkillLevel = skillLevel,
					Error = ex.GetType().Name + ": " + ex.Message,
				};
			}
		}

		/// <summary>
		/// Returns the trimmed mean of the per-pair gains between two matched
		/// sets of windows.
		/// </summary>
		/// <remarks>
		/// Taken as a ratio inside the pair rather than between two means: the
		/// two halves share a seed, so what differs between them is the press
		/// and the swing count they happened to fit, and a ratio cancels the
		/// second where a difference carries it.
		///
		/// Floored at zero. A press that knocks its target back leaves the
		/// caster's next swings reaching for it, so the treatment half can read
		/// lower than its control - that is displacement, and it is already
		/// priced once as the defensive rider. Nothing here pays a skill for
		/// having amplified its own damage downwards.
		/// </remarks>
		/// <param name="controls"></param>
		/// <param name="treatments"></param>
		private static float PairedTrimmedGain(float[] controls, float[] treatments)
		{
			var gains = controls.Zip(treatments, (c, t) => c > 0 ? t / c - 1f : 0f).OrderBy(g => g).ToArray();

			if (gains.Length == 0)
				return 0f;

			var trim = (int)(gains.Length * SfrDials.DefenseTrimShare);
			var kept = gains.Skip(trim).Take(Math.Max(1, gains.Length - trim * 2)).ToArray();

			return Math.Max(0f, kept.Average());
		}

		/// <summary>
		/// Runs one half of the pair and returns what the caster's basic
		/// attacks dealt over the window.
		/// </summary>
		/// <remarks>
		/// The measured skill's own damage is left out by construction: the
		/// recorder attributes every application to the skill that made it, and
		/// only the basic attack's are summed. What is being read is the press's
		/// effect on other damage, not the press.
		///
		/// The mob is placed rather than aggroed, and put back where it started
		/// after every press. It never acts, so the only thing that can move
		/// the reading is the caster's own output - and a press that knocks it
		/// away would otherwise read as a damage cut for the swings that then
		/// miss.
		/// </remarks>
		/// <param name="job"></param>
		/// <param name="skillId"></param>
		/// <param name="skillLevel"></param>
		/// <param name="characterLevel"></param>
		/// <param name="windowMs"></param>
		/// <param name="pressCount"></param>
		/// <param name="arena"></param>
		/// <param name="seed"></param>
		/// <param name="className"></param>
		/// <param name="pressed"></param>
		private static float RunWindow(JobEntry job, SkillId skillId, int skillLevel, int characterLevel, int windowMs, int pressCount,
			Map arena, int seed, out string className, out int pressed)
		{
			var character = (Character)null;
			var mob = (Mob)null;
			className = skillId.ToString();
			pressed = 0;

			GameClock.Use(new VirtualClock());

			try
			{
				var stat = JobCatalog.GetPrimaryStat(job);

				character = SyntheticActors.CreateCharacter(job.JobId, characterLevel, StatSpread.AllIn(stat, characterLevel), arena: arena);
				ReferenceGear.Equip(character, job);

				var skill = SyntheticActors.GiveSkill(character, skillId, skillLevel);
				className = skill.Data.ClassName;

				var weapon = character.Inventory.GetItem(EquipSlot.RightHand);
				var basicId = weapon == null ? BasicAttacks.Default(job) : BasicAttacks.For(job, weapon.Data.EquipType1);
				var basicSkill = SyntheticActors.GiveSkill(character, basicId, 1);

				var mobData = SfrDefenseProbe.FindHostileReferenceMob(characterLevel);
				var meleeOffset = new Position(30f, 0f, 0f);

				mob = SyntheticActors.CreateMob(mobData.Id, meleeOffset, arena);

				SfrDefenseProbe.Fortify(character);
				SfrDefenseProbe.Fortify(mob);
				SfrDefenseProbe.Refill(character);

				character.Direction = character.Position.GetDirection(mob.Position);

				var home = mob.Position;
				var tick = TimeSpan.FromMilliseconds(TickMs);
				var clock = GameClock.Current;

				// Pressed on the skill's own cycle, so a stack the rotation
				// could never hold is never built, and capped so a long-cooldown
				// skill's lead-in does not run the window into the minutes.
				var pressInterval = Math.Max(SfrDials.OffensePressIntervalMs,
					(int)((SfrData.CycleFor(skill.Data.ClassName) ?? 0f) * 1000f));

				// At least one press whatever the cooldown is: a skill whose
				// cycle outruns the lead-in still buffs on the press a player
				// does make, and zero presses would read it as buffing nothing.
				var presses = Math.Max(1, Math.Min(SfrDials.OffensePresses, SfrDials.OffenseMaxLeadInMs / pressInterval));

				// Both halves run this same lead-in, whether or not they press
				// in it, and it ends just after the last press rather than a
				// full interval later. The length has to be identical because
				// the swing timer carries across it: a control that started
				// counting from a standing start lost most of a swing to the
				// first interval, which read as a 6-8% amplification on every
				// skill in the roster.
				var leadInMs = (presses - 1) * pressInterval + SfrDials.OffensePressIntervalMs;

				if (pressCount <= 0)
					presses = 0;
				var sinceSwing = 0f;

				// Position in the window, which is what the two halves realign
				// their rolls on.
				var at = 0;

				using var recorder = new SfrPressRecorder(character);

				// Both halves run the same lead-in and start counting at the same
				// moment; only whether it presses differs. Counting from the
				// press instead would give the control half the whole window's
				// swings against the treatment's remainder.
				void Swing()
				{
					// Realigned before the swing rather than after it, so the
					// damage roll it makes is at the same point of the same
					// stream in both halves however many rolls the press has
					// made by then. Without it a press that rolls anything
					// leaves every following swing rolling different numbers,
					// which reads as amplification the press never bought.
					DeterministicRandom.Realign(seed, at);
					at += TickMs;

					// Re-read every swing: ShootTime divides by SklSpdRate, so a
					// press that buys attack speed lands here rather than in the
					// per-hit damage.
					var interval = Math.Max(TickMs, basicSkill.Properties.GetFloat(PropertyName.ShootTime));

					if (sinceSwing >= interval)
					{
						SfrDefenseProbe.Dispatch(basicSkill, character, mob);
						sinceSwing = 0f;
					}

					SkillPressProbe.Step(clock, tick);
					arena.Update(tick);
					SfrDefenseProbe.Refill(character);

					sinceSwing += TickMs;
				}

				for (var elapsed = 0; elapsed < leadInMs; elapsed += TickMs)
				{
					if (pressed < presses && elapsed >= pressed * pressInterval)
					{
						try
						{
							SfrDefenseProbe.Dispatch(skill, character, mob);
						}
						catch (Exception)
						{
							// A press the probe cannot drive amplifies nothing,
							// which is the same reading as a press that does not.
						}

						mob.Position = home;
						pressed++;
					}

					Swing();
				}

				recorder.Clear();

				for (var elapsed = 0; elapsed < windowMs; elapsed += TickMs)
					Swing();

				return recorder.DamageDealtWith(basicId);
			}
			finally
			{
				GameClock.Use(null);
				SyntheticActors.Cleanup(character, mob);
			}
		}
	}
}
