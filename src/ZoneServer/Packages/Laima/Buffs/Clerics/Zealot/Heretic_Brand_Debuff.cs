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
	/// A marked target takes more damage from the Zealot who marked it — and
	/// from nobody else — for the short life of the mark, and grants that
	/// Zealot Fanaticism stacks when it dies. Bosses are paid out on the
	/// first marked hit instead, so single-target fights still generate the
	/// resource.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.BeadyEyed_Debuff)]
	public class Heretic_Brand_DebuffOverride : BuffHandler
	{
		/// <summary>
		/// Extra damage the marked target takes from the Zealot who marked
		/// it, for as long as the mark lasts. A flat window rather than a
		/// single empowered hit: the Zealot's damage arrives as aura ticks
		/// and judgement pulses, which a one-shot bonus would spend on
		/// whichever tick happened to land first.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float BonusDamageTaken = 0.10f;

		/// <summary>
		/// Fanaticism stacks granted by a marked kill, or by the first
		/// marked hit against a boss.
		/// Shown in the tooltip via captionRatio2 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const int StackReward = 3;

		private const string RewardedVar = "Melia.Zealot.BrandRewarded";

		public override void OnEnd(Buff buff)
		{
			// removeOnDeath makes this fire when the marked target dies, so
			// no extra kill event is needed. The boss path rewards on the
			// first hit instead and sets the guard below.
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

			// The brand is the Zealot's own setup, not a party-wide damage
			// window: only the Zealot who applied it benefits from it.
			if (attacker != buff.Caster)
				return;

			modifier.DamageMultiplier *= 1f + BonusDamageTaken;

			// Bosses rarely die while marked, so the marker is paid on the
			// first hit that lands on the mark instead.
			if (buff.Vars.GetBool(RewardedVar))
				return;

			if (target is Mob mob && mob.Rank == MonsterRank.Boss)
			{
				ZealotBurnFloor.AddStacks(attacker, StackReward);
				buff.Vars.SetBool(RewardedVar, true);
			}
		}
	}
}
