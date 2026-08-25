using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the attack-speed window Fanaticism opens on every use
	/// (riding on the unused BeadyEyed_Buff, shown as "Zeal").
	/// While it lasts, every auto attack builds one Fanaticism stack for
	/// the next Immolate burst or Blind Faith shield. PLACEHOLDER values.
	/// Design idea on file: an ability later toggles the payout type.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.BeadyEyed_Buff)]
	public class Zeal_Rush_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Attack speed while the window lasts. PLACEHOLDER.
		/// </summary>
		private const float AspdBonus = 200f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			if (buff.Target is Character)
				UpdatePropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM, AspdBonus);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM);
		}

		/// <summary>
		/// Every auto attack inside the window feeds the fanaticism: one
		/// stack per hit (multi-hit autos count per hit for now).
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.BeadyEyed_Buff)]
		public void OnAttackAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.BeadyEyed_Buff))
				return;

			if (skill.Id != SkillId.Normal_Attack)
				return;

			ZealotBurnFloor.AddStacks(attacker, 1);
		}
	}
}
