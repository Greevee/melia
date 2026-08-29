using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The window Temper the Flame leaves behind (riding on the otherwise
	/// unused Fanaticism_Buff).
	/// A share of every blow does not land all at once: the fire takes it
	/// and works it off over the next few seconds. Nothing is prevented and
	/// nothing is reduced — the same total arrives, as a burn rather than a
	/// spike, which is the one shape a Zealot's own healing can answer.
	/// It carries its own tick because Temper puts the fire out: with no
	/// aura left there would otherwise be nothing to work the deferred fire
	/// off, and the window would quietly do nothing at the exact moment it
	/// is meant to matter.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Fanaticism_Buff)]
	public class Tempered_BuffOverride : BuffHandler
	{
		public override void WhileActive(Buff buff)
		{
			if (buff.Target is not ICombatEntity target || target.IsDead)
				return;

			// While the flame burns, the aura's tick already does this. Only
			// one of the two may run in a second or the fire burns off at
			// double speed.
			if (target.IsBuffActive(BuffId.Immolation_Self_Buff))
				return;

			Immolation_Self_BuffOverride.BurnOffDeferred(target);
		}

		public override void OnEnd(Buff buff)
		{
			// Whatever is left has nothing to burn it off any more.
			if (buff.Target is ICombatEntity target && !target.IsBuffActive(BuffId.Immolation_Self_Buff))
				ZealotBurnFloor.ClearDeferred(target);
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Fanaticism_Buff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.IsBuffActive(BuffId.Fanaticism_Buff))
				return;

			if (skillHitResult.Damage <= 0)
				return;

			var deferred = skillHitResult.Damage * ZealotBurnFloor.TemperedDeferredShare;

			skillHitResult.Damage -= deferred;
			ZealotBurnFloor.AddDeferred(target, deferred);
		}
	}
}
