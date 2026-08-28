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
	/// Two effects, no exceptions: it opens the frenzy — a window in which
	/// every attack builds a Fanaticism stack — and it drives the fire one
	/// stage deeper while there is a stage left to take. At the deepest
	/// stage only the frenzy opens.
	/// Only usable while the burn mode is active; deals no damage of its
	/// own. Temper the Flame is the way back up.
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

			// One rule, no exception: the frenzy always opens, and the fire
			// steps one stage deeper whenever there is a stage left to take.
			var stageBefore = ZealotBurnFloor.GetStage(caster);
			var newFloor = ZealotBurnFloor.Shift(caster, -ZealotBurnFloor.Step);
			var stageNow = ZealotBurnFloor.GetStage(caster);

			this.GrantZealRush(skill, caster);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0,
				stageNow > stageBefore ? $"Stage {stageNow}  ({newFloor}%)" : "Frenzy");
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
