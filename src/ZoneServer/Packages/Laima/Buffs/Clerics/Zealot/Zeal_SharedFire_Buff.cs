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
	/// The share of the Zealot's fire an ally carries while the Zealot zeals
	/// with the sharing art (Zealot16).
	/// NumArg1 carries the bonus in percent, resolved by the Zealot's stage
	/// when the share was handed out, so the ally's damage does not have to
	/// look anything up.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.FanaticIllusion_Abil_Buff)]
	public class Zeal_SharedFire_BuffOverride : BuffHandler
	{
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.FanaticIllusion_Abil_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.TryGetBuff(BuffId.FanaticIllusion_Abil_Buff, out var buff))
				return;

			var bonusPercent = buff.NumArg1;
			if (bonusPercent <= 0)
				return;

			modifier.DamageMultiplier *= 1f + bonusPercent / 100f;
		}
	}
}
