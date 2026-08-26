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
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zeal judgement state (riding on FanaticIllusion_Buff).
	/// While it lasts, every attack the Zealot makes deals Holy property
	/// damage, and every second one Fanaticism stack burns away in a holy
	/// pulse around the Zealot. The state ends when the stacks run out.
	/// PLACEHOLDER values throughout.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.FanaticIllusion_Buff)]
	public class Zeal_Judgement_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Radius of the per-second holy pulse. PLACEHOLDER.
		/// </summary>
		private const float PulseRange = 60f;

		/// <summary>
		/// Share of a normal skill hit each pulse deals. PLACEHOLDER.
		/// </summary>
		private const float PulseFactor = 0.5f;

		public override void WhileActive(Buff buff)
		{
			var target = buff.Target;
			if (target.IsDead || target is not ICombatEntity caster)
				return;

			// The judgement burns one stack per second; when the fuel is
			// gone, the state ends.
			if (ZealotBurnFloor.GetStacks(caster) <= 0)
			{
				target.StopBuff(BuffId.FanaticIllusion_Buff);
				return;
			}

			ZealotBurnFloor.AddStacks(caster, -1);
			this.HolyPulse(buff, caster);
		}

		/// <summary>
		/// The per-second holy pulse around the Zealot.
		/// </summary>
		private void HolyPulse(Buff buff, ICombatEntity caster)
		{
			if (!caster.TryGetSkill(buff.SkillId, out var skill))
				return;

			var enemies = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, PulseRange).ToList();
			if (enemies.Count == 0)
				return;

			var hits = new List<SkillHitInfo>();

			foreach (var enemy in enemies)
			{
				var modifier = SkillModifier.Default;
				modifier.AttackAttribute = AttributeType.Holy;
				modifier.DamageMultiplier *= PulseFactor;

				var result = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(result.Damage, caster);

				hits.Add(new SkillHitInfo(caster, enemy, skill, result, TimeSpan.Zero, TimeSpan.Zero));
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		/// <summary>
		/// While judging, everything the Zealot does strikes with Holy.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.FanaticIllusion_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.FanaticIllusion_Buff))
				return;

			modifier.AttackAttribute = AttributeType.Holy;
		}
	}
}
