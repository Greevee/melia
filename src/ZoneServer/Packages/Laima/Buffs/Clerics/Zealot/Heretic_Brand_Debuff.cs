using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the heretic's mark applied by Brand the Heretic
	/// (riding on the BeadyEyed_Debuff buff).
	/// The next Zealot hit from the marker against the marked target is
	/// empowered and consumes the mark. A marked target that dies grants the
	/// marker Fervor; against bosses the Fervor is granted with the
	/// empowered hit instead, so single-target fights still generate the
	/// resource.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.BeadyEyed_Debuff)]
	public class Heretic_Brand_DebuffOverride : BuffHandler
	{
		/// <summary>
		/// Damage bonus of the empowered hit. PLACEHOLDER (concept: "Bonus
		/// nächster Zealot-Treffer TBD; Art und Stärke festlegen").
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float EmpoweredHitBonus = 0.5f;

		/// <summary>
		/// Fanaticism stacks granted by a marked kill, or by the empowered hit against
		/// bosses. PLACEHOLDER (concept: "Fervor bei markiertem Kill TBD").
		/// Shown in the tooltip via captionRatio2 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const int StackReward = 3;

		private const string RewardedVar = "Melia.Zealot.BrandRewarded";
		private const string SpentVar = "Melia.Zealot.BrandSpent";

		public override void OnEnd(Buff buff)
		{
			// removeOnDeath makes this fire when the marked target dies, so
			// no extra kill event is needed. The boss path rewards on the
			// empowered hit instead and sets the guard below.
			if (buff.Vars.GetBool(RewardedVar))
				return;

			if (!buff.Target.IsDead)
				return;

			if (buff.Caster is ICombatEntity marker && !marker.IsDead)
				ZealotBurnFloor.AddStacks(marker, StackReward);
		}

		[CombatCalcModifier(CombatCalcPhase.BeforeBonuses, BuffId.BeadyEyed_Debuff)]
		public void OnDefenseBeforeBonuses(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.TryGetBuff(BuffId.BeadyEyed_Debuff, out var buff))
				return;

			// Only the marker's own Zealot skills trigger the brand, once.
			if (attacker != buff.Caster || buff.Vars.GetBool(SpentVar))
				return;

			if (!skill.Data.ClassName.StartsWith("Zealot_"))
				return;

			// The branding strike itself must not consume its own fresh
			// mark - the empowered hit belongs to the follow-up.
			if (skill.Id == SkillId.Zealot_BeadyEyed)
				return;

			modifier.DamageMultiplier *= 1f + EmpoweredHitBonus;

			// The mark is only flagged as spent rather than stopped here:
			// this hook runs before the damage lands, and stopping the buff
			// now would swallow the kill reward when the empowered hit is
			// the killing blow (removeOnDeath fires OnEnd on death). A spent
			// mark idles out with its remaining duration.
			buff.Vars.SetBool(SpentVar, true);

			// Bosses rarely die while marked, so they pay out on the hit.
			if (target is Mob mob && mob.Rank == MonsterRank.Boss)
			{
				ZealotBurnFloor.AddStacks(attacker, StackReward);
				buff.Vars.SetBool(RewardedVar, true);
			}
		}
	}
}
