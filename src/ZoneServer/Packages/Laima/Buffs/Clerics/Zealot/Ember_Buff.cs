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
	/// The flame is out and the Zealot is healed, but for a few seconds
	/// they still hit as hard as they did while dying: the missing-health
	/// bonus is frozen at the value it had the moment the fire went out.
	/// Without that freeze the bonus would be meaningless here — Temper
	/// heals the missing health away as it extinguishes.
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

			// NumArg1 carries the missing-health percentage captured when the
			// flame was put out.
			var missingPercent = buff.NumArg1;
			if (missingPercent <= 0)
				return;

			modifier.DamageMultiplier *= 1f + missingPercent * Immolation_Self_BuffOverride.DamagePerMissingPercent;
		}
	}
}
