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
	/// caster's health down to the current burn floor and keeps it there,
	/// burns nearby enemies every second — a larger area per floor step,
	/// hotter the closer they stand — and converts missing health into
	/// damage on everything. Recasting Immolate adds the burst on top.
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
		private const string HurtVar = "Immolation.Hurt";

		/// <summary>
		/// Stacks granted for being hurt while burning, at most once per
		/// second. The third stack source, and the only one that pays more
		/// the more dangerous the fight is — which is exactly when Blind
		/// Faith is needed. PLACEHOLDER magnitude.
		/// </summary>
		private const int StacksPerPain = 1;

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
			this.PayForPain(buff, target);
		}

		/// <summary>
		/// Pain feeds the fanaticism: taking a hit while burning is worth a
		/// stack. Granted here rather than in the hook, so the aura's own
		/// once-a-second tick is the rate limit — a hail of small hits pays
		/// exactly as much as one big one.
		/// </summary>
		private void PayForPain(Buff buff, ICombatEntity target)
		{
			if (!buff.Vars.GetBool(HurtVar))
				return;

			buff.Vars.SetBool(HurtVar, false);
			ZealotBurnFloor.AddStacks(target, StacksPerPain);
		}

		/// <summary>
		/// Flags that the Zealot was hurt this second; the tick above turns
		/// it into a stack. Only enemy damage counts — the aura's own burn
		/// goes through ModifyHpSafe and never reaches combat calculation.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Immolation_Self_Buff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.TryGetBuff(BuffId.Immolation_Self_Buff, out var buff))
				return;

			if (skillHitResult.Damage <= 0)
				return;

			buff.Vars.SetBool(HurtVar, true);
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

			var enemies = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, range).ToList();
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
			modifier.DamageMultiplier *= 1f + ZealotBurnFloor.GetStageBonus(attacker);
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
		}
	}
}
