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
	/// Handler for the Zealot skill Fanaticism.
	/// Revised design: escalates the active burn mode one step (floor 80 ->
	/// 60 -> 40) and opens a short attack-speed window; auto attacks inside
	/// the window build Fanaticism stacks. At the minimum floor the floor
	/// stays and health is paid directly for two stacks. Only usable while
	/// the burn mode is active; deals no damage of its own. Temper the
	/// Flame is the way back up.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Fanaticism)]
	public class Zealot_FanaticismOverride : IGroundSkillHandler
	{
		/// <summary>
		/// Share of maximum HP paid when used at the minimum floor, and the
		/// stacks granted for it. PLACEHOLDER values.
		/// Shown in the tooltip via captionRatio3 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float MinFloorHpCost = 0.10f;
		private const int MinFloorStacks = 2;

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

			// At the minimum floor the fire feeds on substance instead: the
			// floor stays, health is paid directly (dropping below the floor
			// is allowed and intended) and two stacks are granted.
			if (floor <= ZealotBurnFloor.Min)
			{
				this.PayHealth(caster);
				ZealotBurnFloor.AddStacks(caster, MinFloorStacks);
				this.GrantZealRush(skill, caster);
				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"-10% HP  (+{MinFloorStacks} Fanaticism)");
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
			caster.StartBuff(BuffId.BeadyEyed_Buff, skill.Level, 0f, TimeSpan.FromSeconds(5), caster, skill.Id);
		}

		/// <summary>
		/// Pays the minimum-floor blood price: a share of maximum HP,
		/// removed silently (no damage flash), never lethal.
		/// </summary>
		private void PayHealth(ICombatEntity caster)
		{
			if (caster is not Character character)
				return;

			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var cost = Math.Min(maxHp * MinFloorHpCost, Math.Max(0, caster.Hp - 1));
			if (cost <= 0)
				return;

			character.ModifyHpSafe(-cost, out _, out var priority);
			Send.ZC_UPDATE_ALL_STATUS(character, priority);
		}
	}
}
