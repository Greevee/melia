using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the shield granted by the Zealot skill Blind Faith.
	/// Absorbs incoming damage until the pool runs out. Sized by the skill
	/// from the Fervor stacks it consumed.
	/// </summary>
	/// <remarks>
	/// NumArg1 carries the shield value, NumArg2 the stacks it was built from.
	/// Modelled on CARD_Shield, which is the reference implementation for
	/// absorption in this codebase, but hooked in via CombatCalcModifier,
	/// which supersedes the IBuffCombatDefense* interfaces.
	/// </remarks>
	[Package("laima")]
	[BuffHandler(BuffId.Cleric_HolyAura_Buff)]
	public class HolyAura_Shield_BuffOverride : BuffHandler
	{
		private const string ShieldValueKey = "Melia.Zealot.ShieldRemaining";

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			this.SetShield(buff, buff.NumArg1);
		}

		public override void OnExtend(Buff buff)
		{
			// Recasting refreshes the pool rather than adding to it, so the
			// shield cannot be stacked up by spamming the skill.
			this.SetShield(buff, buff.NumArg1);
		}

		public override void OnEnd(Buff buff)
		{
			buff.Vars.Remove(ShieldValueKey);
			Send.ZC_UPDATE_SHIELD(buff.Target, 0, 1);
		}

		private void SetShield(Buff buff, float value)
		{
			buff.Vars.SetFloat(ShieldValueKey, value);
			Send.ZC_UPDATE_SHIELD(buff.Target, (long)value, 1);
		}

		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Cleric_HolyAura_Buff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.TryGetBuff(BuffId.Cleric_HolyAura_Buff, out var buff))
				return;

			var remaining = buff.Vars.GetFloat(ShieldValueKey);

			if (remaining <= 0)
			{
				target.RemoveBuff(BuffId.Cleric_HolyAura_Buff);
				return;
			}

			var absorbed = Math.Min(remaining, skillHitResult.Damage);
			skillHitResult.Damage -= absorbed;
			remaining -= absorbed;

			buff.Vars.SetFloat(ShieldValueKey, remaining);
			Send.ZC_UPDATE_SHIELD(target, (long)remaining, 0);

			// Fully absorbed hits read as a miss, matching CARD_Shield.
			if (skillHitResult.Damage <= 0)
			{
				skillHitResult.Effect = HitEffect.SAFETY;
				skillHitResult.Result = HitResultType.Miss;
			}

			if (remaining <= 0)
				target.RemoveBuff(BuffId.Cleric_HolyAura_Buff);
		}
	}
}
