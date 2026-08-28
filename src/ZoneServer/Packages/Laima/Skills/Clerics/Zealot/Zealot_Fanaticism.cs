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
	/// Escalates the active burn mode one step (floor 75 -> 50) and opens a
	/// short attack-speed window; attacks inside the window build Fanaticism
	/// stacks, which is also how Zeal gets extended.
	/// Once already at the minimum floor, another use dips the floor one
	/// further step for the length of that window only, then releases it
	/// back. That dip is the whole price — the aura burning the Zealot down
	/// to it is the cost, so nothing is charged on top.
	/// Only usable while the burn mode is active; deals no damage of its
	/// own. Temper the Flame is the way out.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Fanaticism)]
	public class Zealot_FanaticismOverride : IGroundSkillHandler
	{
		/// <summary>
		/// How long the attack-speed window stays open. Ten seconds against
		/// the fifteen second cooldown, so it is up two thirds of the time
		/// and Fanaticism stays the clock the rest of the kit runs on.
		/// Shown in the tooltip via captionTime in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private static readonly TimeSpan RushDuration = TimeSpan.FromSeconds(10);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.ServerMessage(Localization.Get("The flame is not lit."));
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

			var floor = ZealotBurnFloor.Get(caster);

			// Already at the bottom of the ladder: dip one step further for
			// the length of the window only. Zeal_Rush_Buff.OnEnd releases
			// it back to Min, so the deeper floor is exactly as long-lived
			// as the window it came with.
			if (floor <= ZealotBurnFloor.Min)
			{
				ZealotBurnFloor.Set(caster, ZealotBurnFloor.TempMin);
				this.GrantZealRush(skill, caster);

				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Burn Floor {ZealotBurnFloor.TempMin}%  (temporary)");
				return;
			}

			var newFloor = ZealotBurnFloor.Shift(caster, -ZealotBurnFloor.Step);
			this.GrantZealRush(skill, caster);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Burn Floor {newFloor}%");
		}

		/// <summary>
		/// The attack-speed window on every use. Auto attacks inside it
		/// build Fanaticism stacks (see Zeal_Rush_Buff). PLACEHOLDER values;
		/// duration shown via captionTime in skills_overrides.txt.
		/// </summary>
		private void GrantZealRush(Skill skill, ICombatEntity caster)
		{
			caster.StartBuff(BuffId.BeadyEyed_Buff, skill.Level, 0f, RushDuration, caster, skill.Id);
		}
	}
}
