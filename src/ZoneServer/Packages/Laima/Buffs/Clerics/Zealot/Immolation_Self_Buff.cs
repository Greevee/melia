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
		/// Share of maximum HP burned per second while above the floor.
		/// PLACEHOLDER (concept: "Tickrate" is tuning-only). Flat, so
		/// reaching a floor takes a predictable amount of time.
		/// </summary>
		private const float BurnPerSecond = 0.05f;

		/// <summary>
		/// Never burns the caster to death; enemies should do that.
		/// </summary>
		private const float HpFloor = 1f;

		/// <summary>
		/// The db entry updates every 500 ms, so every second tick is one
		/// second of game time.
		/// </summary>
		private const int TicksPerSecond = 2;

		private const string TickVar = "Immolation.Tick";

		/// <summary>
		/// Damage bonus per percent of missing health while burning — the
		/// class identity: 1% extra damage on everything per 1% missing HP.
		/// PLACEHOLDER magnitude.
		/// </summary>
		private const float DamagePerMissingPercent = 0.01f;

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
		/// harder the more health they are missing. Riding on the Immolation
		/// buff means it applies exactly while the flame is lit.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Immolation_Self_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.Immolation_Self_Buff))
				return;

			var missingPercent = ZealotBurnFloor.GetMissingHpPercent(attacker);
			if (missingPercent <= 0)
				return;

			modifier.DamageMultiplier *= 1f + missingPercent * DamagePerMissingPercent;
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

			var burn = Math.Min(maxHp * BurnPerSecond, hp - Math.Max(floorHp, HpFloor));
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
