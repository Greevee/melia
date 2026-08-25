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
	/// Per the rework this is the accelerator: each use drops the burn floor
	/// by one step, so Immolation eats further into the caster's health and
	/// the class damage bonus climbs with it. It deals no damage of its own —
	/// what it does is decide how dangerous the next fight will be.
	/// Temper the Flame is the way back up.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Fanaticism)]
	public class Zealot_FanaticismOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			var floor = ZealotBurnFloor.Get(caster);

			if (floor <= ZealotBurnFloor.Min)
			{
				caster.ServerMessage(Localization.Get("The flame cannot burn any lower."));
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			var newFloor = ZealotBurnFloor.Shift(caster, -ZealotBurnFloor.StepDown);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Burn Floor {newFloor}%");
		}
	}
}
