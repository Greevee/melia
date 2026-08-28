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
	/// The martyr's moment: the short window of invulnerability the
	/// Martyrdom attribute grants right after Fanaticism.
	/// A Zealot deliberately stands at a quarter of their health in melee,
	/// so the class needs one beat where the fire cannot kill them - and
	/// tying it to Fanaticism means the beat is bought with the same press
	/// that drives them deeper.
	/// </summary>
	/// <remarks>
	/// The buff's Invincibility tag is descriptive only; nothing in combat
	/// reads it, so the immunity is enforced here by zeroing the damage
	/// after it has been calculated.
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Fanaticism_Martyrdom_Buff)]
	public class Martyrdom_BuffOverride : BuffHandler
	{
		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Fanaticism_Martyrdom_Buff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.IsBuffActive(BuffId.Fanaticism_Martyrdom_Buff))
				return;

			skillHitResult.Damage = 0;
			skillHitResult.Result = HitResultType.Miss;
		}
	}
}
