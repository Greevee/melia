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
	/// Handler for the Emphatic Trust mark: a marked enemy takes extra
	/// damage from the burning aura and from Zeal, and from nothing else.
	/// The narrow trigger is the point — it turns the mark into a setup
	/// press for the Zealot's own two damage sources instead of a generic
	/// damage-taken debuff that every party member would benefit from.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.EmphasisTrust_Debuff)]
	public class EmphasisTrust_DebuffOverride : BuffHandler
	{
		/// <summary>
		/// Extra damage the aura's ticks deal to a marked enemy.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float ImmolationBonus = 0.50f;

		/// <summary>
		/// Extra damage Zeal deals to a marked enemy — the activating strike
		/// and the per-second pulses alike.
		/// Shown in the tooltip via captionRatio2 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float ZealBonus = 0.20f;

		/// <summary>
		/// How many empowered hits one mark pays out before it is used up,
		/// counted per marked enemy. With the aura ticking once a second and
		/// Zeal pulsing once a second, twenty hits is roughly the twenty
		/// seconds the mark lasts, so whichever runs out first is a real
		/// limit rather than decoration.
		/// Shown in the tooltip via captionRatio3 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const int MaxHits = 20;

		private const string HitsVar = "Melia.Zealot.TrustHits";

		[CombatCalcModifier(CombatCalcPhase.BeforeBonuses, BuffId.EmphasisTrust_Debuff)]
		public void OnDefenseBeforeBonuses(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.TryGetBuff(BuffId.EmphasisTrust_Debuff, out var buff))
				return;

			// Only the Zealot's own two damage sources are empowered. Both
			// arrive under their skill's id: the aura ticks as Immolate, and
			// Zeal covers its activating strike and its pulses alike.
			var bonus = skill.Id switch
			{
				SkillId.Zealot_Immolation => ImmolationBonus,
				SkillId.Zealot_FanaticIllusion => ZealBonus,
				_ => 0f,
			};

			if (bonus <= 0)
				return;

			// Only the marker's own damage counts against the budget; an
			// ally's Immolate should not spend someone else's mark.
			if (attacker != buff.Caster)
				return;

			var hits = buff.Vars.GetInt(HitsVar);
			if (hits >= MaxHits)
				return;

			modifier.DamageMultiplier *= 1f + bonus;

			buff.Vars.SetInt(HitsVar, hits + 1);

			// Spending the last hit ends the mark early. Safe to stop from
			// inside this hook: unlike the heretic's brand, nothing here
			// pays out on the target's death.
			if (hits + 1 >= MaxHits)
				target.StopBuff(BuffId.EmphasisTrust_Debuff);
		}
	}
}
