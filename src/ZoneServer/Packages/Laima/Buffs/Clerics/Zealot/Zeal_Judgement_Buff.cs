using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Geometry.Shapes;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zeal burning state (riding on FanaticIllusion_Buff).
	/// Zeal is the amplifier: while it burns, the stage damage bonus counts
	/// double and every attack the Zealot makes deals Fire property damage.
	/// It costs no Fanaticism at all: the stacks are Pyre's lash count, and
	/// an amplifier that ate them would just be a worse Pyre. Zeal is paid
	/// for by its cooldown and by having to be re-pressed.
	/// The sharing art (Zealot16) turns it from a personal amplifier into a
	/// party one: the doubling is dropped and every nearby ally carries part
	/// of the stage bonus instead.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.FanaticIllusion_Buff)]
	public class Zeal_Judgement_BuffOverride : BuffHandler
	{
		/// <summary>
		/// What Zeal does to the stage damage bonus while it burns: doubles
		/// it, so the deepest stage reaches +100%. This is the whole skill —
		/// it amplifies the class mechanic instead of adding a second one.
		/// Shown via captionRatio2 in skills_overrides.txt — keep the two in
		/// sync.
		/// </summary>
		public const float StageBonusFactor = 2f;

		/// <summary>
		/// How much brighter the burning body reads while Zeal is up. The
		/// flame is the only visual telling the player the state is live,
		/// so it doubles rather than shifting subtly.
		/// </summary>
		public const float AuraScaleFactor = 2f;

		public override void WhileActive(Buff buff)
		{
			var target = buff.Target;
			if (target.IsDead || target is not ICombatEntity caster)
				return;

			// Runs on its own timer now. It used to drain a stack a second,
			// which meant keeping the amplifier up and firing a Pyre were
			// the same budget — so the player did neither well.
			this.ShareWithAllies(caster);
		}

		/// <summary>
		/// With the sharing art the Zealot keeps only the plain stage bonus
		/// and hands this share of it to everyone nearby instead — worth it
		/// from a few allies on, pointless alone, which is the decision the
		/// art is there to create.
		/// </summary>
		private const float AllyShare = 0.5f;

		/// <summary>
		/// How far the shared fire reaches, and how long a share lasts. The
		/// tick refreshes it every second, so the duration only has to
		/// outlive one tick.
		/// </summary>
		private const float ShareRange = 200f;
		private static readonly TimeSpan ShareDuration = TimeSpan.FromSeconds(3);

		/// <summary>
		/// Hands every nearby ally a share of the stage bonus, as a plain
		/// percentage they carry themselves.
		/// </summary>
		private void ShareWithAllies(ICombatEntity caster)
		{
			if (!caster.TryGetActiveAbilityLevel(AbilityId.Zealot16, out _))
				return;

			var share = ZealotBurnFloor.GetStageBonus(caster) * AllyShare * 100f;
			if (share <= 0)
				return;

			var area = new CircleF(caster.Position, ShareRange);

			foreach (var ally in caster.Map.GetActorsIn<Character>(area))
			{
				if (ally == caster || ally.IsDead || !caster.IsAlly(ally))
					continue;

				ally.StartBuff(BuffId.FanaticIllusion_Abil_Buff, share, 0f, ShareDuration, caster, SkillId.Zealot_FanaticIllusion);
			}
		}

		/// <summary>
		/// While Zeal burns, everything the Zealot does strikes with Fire.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.FanaticIllusion_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.FanaticIllusion_Buff))
				return;

			modifier.AttackAttribute = AttributeType.Fire;
		}

	}
}
