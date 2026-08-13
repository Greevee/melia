using System.Linq;
using Melia.Shared.Packages;
using Melia.Shared.Game.Const;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors;
using System;

namespace Melia.Zone.Buffs.Handlers
{
	/// <summary>
	/// Handler for the Concentration Buff, which increases the target's
	/// hit rate.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Concentration_Buff)]
	public class Concentration_Buff : BuffHandler
	{
		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			var bonus = GetCaptionRatio(buff, 1) / 100f;

			AddPropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM, bonus);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.HR_RATE_BM);
		}

		public override void WhileActive(Buff buff)
		{
			var targets = buff.Target.Map.GetAttackableEnemiesInPosition(buff.Target, buff.Target.Position, 100).Where(c => c.IsBuffActiveByKeyword(BuffTag.Cloaking)).ToList();
			foreach (var target in targets)
				target.StopBuffByTag(BuffTag.Cloaking);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeBonuses, BuffId.Concentration_Buff)]
		public void OnAttackBeforeBonuses(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.TryGetBuff(BuffId.Concentration_Buff, out var buff))
				return;

			// Archer39 makes hits never miss
			if (buff.Target.TryGetActiveAbilityLevel(AbilityId.Archer39, out _))
				modifier.ForcedHit = true;
		}
	}
}
