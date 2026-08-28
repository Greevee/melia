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
	/// deals no damage. It ends when the stacks run out or after ten
	/// seconds, whichever comes first — the same resource Zeal burns, spent
	/// on survival instead of damage.
	/// The healing itself is handled by Cleric_HolyAura_Buff.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BlindFaith)]
	public class Zealot_BlindFaithOverride : ISelfSkillHandler
	{
		/// <summary>
		/// Hard ceiling on how long the faith can hold, whatever the stack
		/// count says — ten seconds, so it drains at most ten stacks and a
		/// full bar can still pay for something else.
		/// Shown in the tooltip via captionTime in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(10);

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

			// Ten seconds is the ceiling; the buff handler ends it earlier
			// when the stacks run out. NumArg1 carries the skill level the
			// healing scales with.
			if (!caster.IsBuffActive(BuffId.Cleric_HolyAura_Buff))
				caster.StartBuff(BuffId.Cleric_HolyAura_Buff, skill.Level, 0f, MaxDuration, caster, skill.Id);
		}
	}
}
