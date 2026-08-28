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
	/// (riding on the unused BeadyEyed_Buff, shown as "Fanatic Rush").
	/// While it lasts, every attack and auto attack builds one Fanaticism
	/// stack. This is the ONLY source of stacks, which makes Fanaticism the
	/// way to extend a running Zeal: Zeal drains a stack a second, and only
	/// this window puts them back.
	/// The window also owns the temporary floor dip Fanaticism takes at the
	/// minimum floor, and releases it when it ends.
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

			// Release the temporary dip Fanaticism takes at the minimum
			// floor. Checked rather than tracked: only that dip can put the
			// floor below Min, so a floor below Min is by definition one
			// this window is holding open.
			if (buff.Target is ICombatEntity target && ZealotBurnFloor.Get(target) < ZealotBurnFloor.Min)
				ZealotBurnFloor.Set(target, ZealotBurnFloor.Min);
		}

		/// <summary>
		/// Every attack and auto attack inside the window feeds the
		/// fanaticism: one stack per hit (multi-hit attacks count per hit
		/// for now). Exclusions, not an allowlist: player auto attacks
		/// arrive under weapon-specific skill ids, so filtering FOR
		/// Normal_Attack silently matched nothing.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.BeadyEyed_Buff)]
		public void OnAttackAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.BeadyEyed_Buff))
				return;

			// The passive sources don't count: the burning aura ticks with
			// the Immolate skill id, the judgement pulses with Zeal's.
			// Without this the Zeal pulse would refund the stack it just
			// spent and Zeal would never end.
			if (skill.Id == SkillId.Zealot_Immolation || skill.Id == SkillId.Zealot_FanaticIllusion)
				return;

			// Deliberately NOT gated on Zeal being inactive: building stacks
			// during Zeal is how the window prolongs it.
			ZealotBurnFloor.AddStacks(attacker, 1);
		}
	}
}
