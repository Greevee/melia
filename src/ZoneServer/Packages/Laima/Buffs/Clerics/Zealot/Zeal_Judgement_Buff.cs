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
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zeal burning state (riding on FanaticIllusion_Buff).
	/// Zeal is the amplifier: while it burns, the stage damage bonus counts
	/// double and every attack the Zealot makes deals Fire property damage.
	/// It costs one Fanaticism stack per second, and the stacks are its
	/// clock — the burning itself pays part of that back, so at the deepest
	/// stage Zeal can hold indefinitely while a shallower one runs dry.
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

			// The state burns one stack per second; when the fuel is gone,
			// it ends. Checked before spending so the last stack still buys
			// a pulse.
			if (ZealotBurnFloor.GetStacks(caster) <= 0)
			{
				target.StopBuff(BuffId.FanaticIllusion_Buff);
				return;
			}

			ZealotBurnFloor.AddStacks(caster, -1);
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
