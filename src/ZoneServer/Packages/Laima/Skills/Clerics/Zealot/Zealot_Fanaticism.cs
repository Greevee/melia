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
	/// Handler for the Zealot skill Fanaticism.
	/// Per the concept (Zealot_Rework_Konzept.xlsx v1.0) this escalates the
	/// active burn mode by one step: the burn floor sinks 70 -> 50 -> 30 ->
	/// 10 and the Zealot gains one Fanaticism stack, which the next Immolate
	/// burst consumes. Only usable while the burn mode is active; deals no
	/// damage of its own. Temper the Flame is the way back up.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Fanaticism)]
	public class Zealot_FanaticismOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.ServerMessage(Localization.Get("The flame is not lit."));
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			var floor = ZealotBurnFloor.Get(caster);

			if (floor <= ZealotBurnFloor.Min)
			{
				caster.ServerMessage(Localization.Get("The flame cannot burn any lower."));
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

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			var newFloor = ZealotBurnFloor.Shift(caster, -ZealotBurnFloor.Step);
			ZealotBurnFloor.AddStack(caster);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Burn Floor {newFloor}%  (+1 Fanaticism)");
		}
	}
}
