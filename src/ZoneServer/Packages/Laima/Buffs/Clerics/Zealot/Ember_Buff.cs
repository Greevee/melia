using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the ember Temper the Flame leaves behind (riding on the
	/// unused Fanaticism_Buff).
	/// The flame is out, but for a few seconds the Zealot still hits as
	/// hard as they did at the stage they were burning in — the bonus is
	/// frozen at the moment the fire went out, since Temper resets the
	/// floor and would otherwise take it with it.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Fanaticism_Buff)]
	public class Ember_BuffOverride : BuffHandler
	{
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.Fanaticism_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.TryGetBuff(BuffId.Fanaticism_Buff, out var buff))
				return;

			// NumArg1 carries the stage bonus, in percent, captured when the
			// flame was put out.
			var bonusPercent = buff.NumArg1;
			if (bonusPercent <= 0)
				return;

			modifier.DamageMultiplier *= 1f + bonusPercent / 100f;
		}
	}
}
