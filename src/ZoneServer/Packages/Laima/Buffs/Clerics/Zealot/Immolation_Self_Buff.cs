using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot burn mode carried by Immolate.
	/// Per the concept (Zealot_Rework_Konzept.xlsx v1.0) the aura burns the
	/// caster's health down to the current stage and keeps it there, burns
	/// nearby enemies every second — a larger area per stage, hotter the
	/// closer they stand — pays a stack every few seconds, and grants the
	/// stage damage bonus on everything the Zealot does (doubled while Zeal
	/// burns). Recasting Immolate adds the burst on top.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Immolation_Self_Buff)]
	public class Immolation_Self_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Share of CURRENT HP burned per second while above the floor, so
		/// the descent eases off as it approaches. PLACEHOLDER.
		/// </summary>
		private const float BurnPerSecond = 0.10f;

		/// <summary>
		/// Never burns the caster to death; enemies should do that.
		/// </summary>
		private const float HpFloor = 1f;

		/// <summary>
		/// The db entry updates every 500 ms, so every second tick is one
		/// second of game time.
		/// </summary>
		private const int TicksPerSecond = 2;

		/// <summary>
		/// Fire resistance needed for the full mitigation below, and the
		/// ceiling on it. PLACEHOLDER values.
		/// </summary>
		private const float ResFireForMaxMitigation = 2000f;
		private const float MaxFireMitigation = 0.5f;

		private const string TickVar = "Immolation.Tick";
		private const string StackTickVar = "Immolation.StackTick";

		/// <summary>
		/// The burning aura around the Zealot: radius grows one step per
		/// floor step (80 -> 50, 60 -> 75, 40 -> 100), and enemies closer to
		/// the Zealot take more damage, falling off linearly to the edge.
		/// PLACEHOLDER values.
		/// </summary>
		private const float AuraBaseRange = 50f;
		private const float AuraRangePerStep = 25f;
		private const float AuraTickFactor = 0.3f;
		private const float AuraEdgeDamageShare = 0.5f;

		/// <summary>
		/// How many enemies the aura burns at once. Deliberately far above
		/// the splash limits the cast skills use — this is a fire the Zealot
		/// stands inside, not a swing, so being surrounded should be the
		/// good case. It is capped only so a huge pull cannot make the aura
		/// the whole class. PLACEHOLDER values.
		/// </summary>
		private const int AuraBaseTargets = 15;
		private const int AuraTargetsPerLevel = 1;

		public override void OnEnd(Buff buff)
		{
			// Ending the mode resets the risk dial entirely.
			if (buff.Target is ICombatEntity target)
				ZealotBurnFloor.ConsumeStacks(target);
		}

		public override void WhileActive(Buff buff)
		{
			var target = buff.Target;
			if (target.IsDead)
				return;

			if (!this.IsFullSecond(buff))
				return;

			// Zeal makes the body burn visibly hotter, which is the only cue
			// that the state is live now that it carries no screen overlay.
			var visualScale = target.IsBuffActive(BuffId.FanaticIllusion_Buff)
				? Zeal_Judgement_BuffOverride.AuraScaleFactor
				: 1f;

			ZealotBurnFloor.PulseAuraVisual(target, visualScale);

			this.BurnTowardsFloor(target);
			this.DealAuraDamage(buff, target);
			this.BuildStacks(buff, target);
		}

		/// <summary>
		/// The burning itself pays: one stack every few seconds, faster the
		/// deeper the stage — the deepest one funds a channel outright.
		/// Intervals live in ZealotBurnFloor.
		/// </summary>
		private void BuildStacks(Buff buff, ICombatEntity target)
		{
			var tick = buff.Vars.GetInt(StackTickVar) + 1;

			if (tick < ZealotBurnFloor.GetStackInterval(target))
			{
				buff.Vars.SetInt(StackTickVar, tick);
				return;
			}

			buff.Vars.SetInt(StackTickVar, 0);
			ZealotBurnFloor.AddStacks(target, 1);
		}

		/// <summary>
		/// Damages everything inside the burning aura once per second. The
		/// radius grows with the floor steps; the closer an enemy stands to
		/// the Zealot, the more it takes (linear falloff to the edge).
		/// </summary>
		private void DealAuraDamage(Buff buff, ICombatEntity target)
		{
			if (buff.Caster is not ICombatEntity caster)
				return;

			if (!caster.TryGetSkill(buff.SkillId, out var skill))
				return;

			var floor = ZealotBurnFloor.Get(target);
			var range = AuraBaseRange + (ZealotBurnFloor.Ignition - floor) / (float)ZealotBurnFloor.Step * AuraRangePerStep;

			// Nearest first, so when the cap bites it keeps the enemies the
			// aura actually burns hardest — the falloff makes the far ones
			// the cheap ones anyway.
			var maxTargets = AuraBaseTargets + skill.Level * AuraTargetsPerLevel;

			var enemies = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, range)
				.OrderBy(a => a.Position.Get2DDistance(caster.Position))
				.Take(maxTargets)
				.ToList();

			if (enemies.Count == 0)
				return;

			var hits = new List<SkillHitInfo>();

			foreach (var enemy in enemies)
			{
				var distance = (float)caster.Position.Get2DDistance(enemy.Position);
				var proximity = 1f - Math.Clamp(distance / range, 0f, 1f) * (1f - AuraEdgeDamageShare);

				var modifier = SkillModifier.Default;
				modifier.DamageMultiplier *= AuraTickFactor * proximity;

				var result = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(result.Damage, caster);

				hits.Add(new SkillHitInfo(caster, enemy, skill, result, TimeSpan.Zero, TimeSpan.Zero));
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		/// <summary>
		/// The class damage bonus: everything the burning Zealot does hits
		/// harder the deeper they have committed. Riding on the Immolation
		/// buff means it applies exactly while the flame is lit; the values
		/// per stage live in ZealotBurnFloor.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Immolation_Self_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.Immolation_Self_Buff))
				return;

			// Reads the stage, not current health: the reward is for how deep
			// the Zealot committed, so a healer topping them up no longer
			// takes it away.
			// Zeal is the amplifier: while it burns, the stage counts double
			// — unless the sharing art is taken, which spends that doubling
			// on the party instead (see Zeal_Judgement_BuffOverride).
			var bonus = ZealotBurnFloor.GetStageBonus(attacker);
			if (attacker.IsBuffActive(BuffId.FanaticIllusion_Buff)
				&& !attacker.TryGetActiveAbilityLevel(AbilityId.Zealot16, out _))
				bonus *= Zeal_Judgement_BuffOverride.StageBonusFactor;

			modifier.DamageMultiplier *= 1f + bonus;
		}

		/// <summary>
		/// How much of the self-burn the Zealot's fire resistance takes off.
		/// Capped, so no amount of resistance makes burning free — the stage
		/// bonus always has to be paid for. PLACEHOLDER values.
		/// </summary>
		private float GetFireMitigation(ICombatEntity target)
		{
			var resFire = target.Properties.GetFloat(PropertyName.ResFire, 0);
			if (resFire <= 0)
				return 0;

			return Math.Clamp(resFire / ResFireForMaxMitigation, 0f, MaxFireMitigation);
		}

		/// <summary>
		/// Counts update ticks and returns true once per second.
		/// </summary>
		private bool IsFullSecond(Buff buff)
		{
			var tick = buff.Vars.GetInt(TickVar) + 1;

			if (tick < TicksPerSecond)
			{
				buff.Vars.SetInt(TickVar, tick);
				return false;
			}

			buff.Vars.SetInt(TickVar, 0);
			return true;
		}

		/// <summary>
		/// Burns health until the caster sits at their burn floor, then stops.
		/// Healing above the floor simply gets burned off again.
		/// </summary>
		private void BurnTowardsFloor(ICombatEntity target)
		{
			if (target is not Character character)
				return;

			var maxHp = target.Properties.GetFloat(PropertyName.MHP);
			if (maxHp <= 0)
				return;

			var floorHp = maxHp * (ZealotBurnFloor.Get(target) / 100f);
			var hp = target.Hp;

			// At or below the floor there is nothing to burn — this is where
			// the aura idles.
			if (hp <= floorHp || hp <= HpFloor)
				return;

			// Ten percent of what is left, not of the maximum: the fall is
			// fast at first and eases off towards the floor, so descending
			// always feels like the fire eating into you.
			var burn = Math.Min(hp * BurnPerSecond, hp - Math.Max(floorHp, HpFloor));
			if (burn <= 0)
				return;

			// The fire is fire: resistance to it makes the self-burn hurt
			// less, which is what makes Immolate scale with gear and with
			// the class's own fire-resist attribute.
			burn *= 1f - this.GetFireMitigation(target);
			if (burn <= 0)
				return;

			// ModifyHp would send ZC_ADD_HP, which the client answers with
			// its damage-taken flash - the whole character blinking brighter
			// once per second. The safe variant plus a plain status update
			// moves the HP bar without triggering that feedback.
			character.ModifyHpSafe(-burn, out _, out var priority);
			Send.ZC_UPDATE_ALL_STATUS(character, priority);

			// Only health the fire really took feeds the pyre: at the stage
			// there is nothing to burn, so standing still adds nothing and
			// being healed back up is what reloads Pyre.
			ZealotBurnFloor.AddBurned(target, burn);
		}
	}
}
