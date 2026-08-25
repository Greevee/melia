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
				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"-10% HP  (+{MinFloorStacks} Fanaticism)");
				return;
			}

			var newFloor = ZealotBurnFloor.Shift(caster, -ZealotBurnFloor.Step);
			ZealotBurnFloor.AddStacks(caster, 1);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Burn Floor {newFloor}%  (+1 Fanaticism)");
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
