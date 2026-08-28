using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Blind Faith.
	/// Turns Fanaticism into healing: while it runs it burns one stack per
	/// second and heals the Zealot and nearby allies, scaling with SPR. It
	/// deals no damage. The stacks are its clock, exactly like Zeal's — so
	/// the two are the same resource spent on survival or on damage.
	/// The healing itself is handled by Cleric_HolyAura_Buff.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BlindFaith)]
	public class Zealot_BlindFaithOverride : ISelfSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (ZealotBurnFloor.GetStacks(caster) <= 0)
			{
				caster.ServerMessage(Localization.Get("No Fanaticism to spend."));
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var farPos = new Position(originPos);
			farPos.X += 100;

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			// Duration TimeSpan.Zero means no timer: the stacks are the
			// clock, and the buff handler ends itself when they run out.
			// NumArg1 carries the skill level the healing scales with.
			if (!caster.IsBuffActive(BuffId.Cleric_HolyAura_Buff))
				caster.StartBuff(BuffId.Cleric_HolyAura_Buff, skill.Level, 0f, TimeSpan.Zero, caster, skill.Id);
		}
	}
}
