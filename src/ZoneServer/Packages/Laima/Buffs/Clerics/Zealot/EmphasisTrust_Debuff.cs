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
	/// Handler for the Emphatic Trust debuff: a judged enemy takes
	/// additional damage from every attack while the mark lasts —
	/// vanilla-style behavior for the first implementation.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.EmphasisTrust_Debuff)]
	public class EmphasisTrust_DebuffOverride : BuffHandler
	{
		/// <summary>
		/// Extra damage taken per hit while judged. PLACEHOLDER until the
		/// balance pass (the concept keeps the vanilla values; those are
		/// factor-based and will replace this flat percentage). Shown in
		/// the tooltip via captionRatio1 in skills_overrides.txt — keep the
		/// two in sync.
		/// </summary>
		private const float BonusDamageTaken = 0.15f;

		[CombatCalcModifier(CombatCalcPhase.BeforeBonuses, BuffId.EmphasisTrust_Debuff)]
		public void OnDefenseBeforeBonuses(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.TryGetBuff(BuffId.EmphasisTrust_Debuff, out var buff))
				return;

			modifier.DamageMultiplier *= 1f + BonusDamageTaken;
		}
	}
}
