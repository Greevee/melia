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
	/// Handler for the Zealot aura Immolation, which carries the entire class
	/// mechanic.
	/// Per the rework it is permanently active and does two things: it burns
	/// the caster down to their chosen burn floor, and it converts missing
	/// health into damage — one percent of extra damage on everything for
	/// every percent of health the caster is missing.
	/// At full health it does nothing at all. The player decides how far down
	/// to go with Fanaticism and Temper the Flame, and that decision is the
	/// class.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Immolation_Self_Buff)]
	public class Immolation_Self_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Share of maximum HP burned per second while above the floor. Flat,
		/// so reaching a floor takes a predictable amount of time instead of
		/// creeping towards it.
		/// Shown in the tooltip via Immolation's captionRatio1 in
		/// skills_overrides.txt — keep the two in sync.
		/// </summary>
		private const float BurnPerSecond = 0.05f;

		/// <summary>
		/// Never burns the caster to death; enemies should do that.
		/// </summary>
		private const float HpFloor = 1f;

		/// <summary>
		/// Damage bonus per percent of missing health. One to one, so a
		/// Zealot at 20% health deals 80% more damage with everything.
		/// </summary>
		private const float DamagePerMissingPercent = 0.01f;

		/// <summary>
		/// Radius of the burning aura around the caster.
		/// </summary>
		private const int AuraRange = 200;

		/// <summary>
		/// The db entry updates every 500 ms, so every second tick is one
		/// second of game time.
		/// </summary>
		private const int TicksPerSecond = 2;

		private const string TickVar = "Immolation.Tick";

		public override void WhileActive(Buff buff)
		{
			var target = buff.Target;
			if (target.IsDead)
				return;

			if (!this.IsFullSecond(buff))
				return;

			ZealotFervor.AddStack(target, buff.SkillId);

			// PoC: the burning-body visual, one pulse per second at the
			// current position, growing as the floor sinks. No cleanup
			// needed — each pulse expires on its own.
			ZealotBurnFloor.PulseAuraVisual(target, ZealotBurnFloor.Get(target));

			this.BurnTowardsFloor(target);
			this.DealAuraDamage(buff, target);
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

			character.ModifyHp(-burn);
		}

		/// <summary>
		/// Damages everything in range, scaled by how much health the caster
		/// is missing. At full health the aura deals nothing.
		/// </summary>
		private void DealAuraDamage(Buff buff, ICombatEntity target)
		{
			var missingPercent = ZealotBurnFloor.GetMissingHpPercent(target);
			if (missingPercent <= 0)
				return;

			if (buff.Caster is not ICombatEntity caster)
				return;

			if (!caster.TryGetSkill(buff.SkillId, out var skill))
				return;

			var enemies = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, AuraRange).ToList();
			if (enemies.Count == 0)
				return;

			var hits = new List<SkillHitInfo>();

			foreach (var enemy in enemies)
			{
				var modifier = SkillModifier.Default;

				// The aura only burns as hot as the caster is hurt.
				modifier.DamageMultiplier *= missingPercent / 100f;

				var result = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(result.Damage, caster);

				hits.Add(new SkillHitInfo(caster, enemy, skill, result, TimeSpan.Zero, TimeSpan.Zero));
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		/// <summary>
		/// The class damage bonus: everything the Zealot does hits harder the
		/// more health they are missing. Riding on the Immolation buff means
		/// it applies exactly while the aura is lit.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Immolation_Self_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.Immolation_Self_Buff))
				return;

			var missingPercent = ZealotBurnFloor.GetMissingHpPercent(attacker);
			if (missingPercent <= 0)
				return;

			modifier.DamageMultiplier *= 1f + (missingPercent * DamagePerMissingPercent);
		}
	}
}
