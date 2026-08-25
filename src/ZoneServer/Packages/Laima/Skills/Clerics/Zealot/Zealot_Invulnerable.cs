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
	/// Handler for the Zealot skill Invulnerable, reworked into
	/// "Temper the Flame".
	/// Per the concept (Zealot_Rework_Konzept.xlsx v1.0) this spends Fervor
	/// to reduce the active burn mode by one step: the floor rises 10 -> 30
	/// -> 50 -> 70, health below the new floor is raised up to it, and all
	/// Fanaticism stacks are removed. Used at the top step, it puts the
	/// flame out entirely. Only usable while the burn mode is active.
	/// Note: the skill database lists this as useType "MeleeGround", but the
	/// client sends CZ_SKILL_SELF for it, so it is handled as a self skill.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Invulnerable)]
	public class Zealot_InvulnerableOverride : ISelfSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
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

			var farPos = new Position(originPos);
			farPos.X += 100;

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			var floor = ZealotBurnFloor.Get(caster);

			// At the top step, another use puts the flame out entirely.
			if (floor >= ZealotBurnFloor.Ignition)
			{
				caster.StopBuff(BuffId.Immolation_Self_Buff);
				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, "The flame is out");
				return;
			}

			var newFloor = ZealotBurnFloor.Shift(caster, ZealotBurnFloor.Step);
			ZealotBurnFloor.ConsumeStacks(caster);

			var healed = this.RaiseToFloor(caster, newFloor);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Burn Floor {newFloor}%" + (healed > 0 ? $"  +{healed} HP" : ""));
		}

		/// <summary>
		/// Raises health up to the new floor if it sits below it, returning
		/// the amount restored.
		/// </summary>
		private int RaiseToFloor(ICombatEntity caster, int floor)
		{
			if (caster is not Character character)
				return 0;

			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var floorHp = maxHp * (floor / 100f);

			var missing = (int)(floorHp - caster.Hp);
			if (missing <= 0)
				return 0;

			character.Heal(missing, 0);

			return missing;
		}
	}
}
