using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Blind Faith.
	/// A toggle, not a window: while it is on it burns one Fanaticism stack
	/// per second and heals the Zealot and nearby allies, and it stays on
	/// until the Zealot turns it off or the stacks run out. That is the
	/// point — the burn takes a share of current health per second, so a
	/// heal that runs continuously is what makes health settle somewhere
	/// instead of only falling, and where it settles is the Zealot's build
	/// decision: no SPR sits low, a lot of SPR floats near the top.
	/// The healing itself is handled by Cleric_HolyAura_Buff.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BlindFaith)]
	public class Zealot_BlindFaithOverride : ISelfSkillHandler
	{
		/// <summary>
		/// The buff runs until it is switched off or starved, so it is
		/// started without a timer of its own.
		/// </summary>
		private static readonly TimeSpan NoDuration = TimeSpan.Zero;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			var farPos = new Position(originPos);
			farPos.X += 100;

			// Pressing again puts the faith down. No cost, no refusal — a
			// toggle the player cannot switch off is a trap.
			if (caster.IsBuffActive(BuffId.Cleric_HolyAura_Buff))
			{
				Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
				Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
				Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

				caster.StopBuff(BuffId.Cleric_HolyAura_Buff);
				return;
			}

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

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			SetToggled(skill, caster, true);

			// NumArg1 carries the skill level the healing scales with.
			caster.StartBuff(BuffId.Cleric_HolyAura_Buff, skill.Level, 0f, NoDuration, caster, skill.Id);
		}

		/// <summary>
		/// Moves the skill's toggle state and tells the client, so the icon
		/// matches whether the faith is actually running. Called from the
		/// buff handler too, since the stacks running out switches it off
		/// without the player touching the button.
		/// </summary>
		public static void SetToggled(Skill skill, ICombatEntity caster, bool toggled)
		{
			skill.Vars.SetBool("Melia.Skill.Toggled", toggled);

			if (caster is Character character)
				Send.ZC_NORMAL.SkillToggle(character, toggled ? skill.Id : SkillId.None);
		}
	}
}
