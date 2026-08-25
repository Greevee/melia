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
	/// Per the rework this is the brake to Fanaticism's accelerator: it
	/// raises the burn floor by one step and converts the Fervor built up
	/// while burning into healing. Pulling out of a deep floor therefore
	/// costs the resource that deep floor produced, which is what keeps the
	/// risk dial honest.
	/// Note: the skill database lists this as useType "MeleeGround", but the
	/// client sends CZ_SKILL_SELF for it, so it is handled as a self skill.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Invulnerable)]
	public class Zealot_InvulnerableOverride : ISelfSkillHandler
	{
		/// <summary>
		/// Share of maximum HP healed per Fervor stack consumed.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float HealPerStack = 0.05f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			var floor = ZealotBurnFloor.Get(caster);
			var stacks = ZealotFervor.GetStacks(caster);

			// Nothing to raise and nothing to spend — refuse rather than
			// waste the cooldown.
			if (floor >= ZealotBurnFloor.Max && stacks <= 0)
			{
				caster.ServerMessage(Localization.Get("The flame is already tempered."));
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var farPos = new Position(originPos);
			farPos.X += 100;

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			var newFloor = ZealotBurnFloor.Shift(caster, ZealotBurnFloor.StepUp);

			ZealotFervor.ConsumeAll(caster);
			var healed = this.HealByStacks(caster, stacks);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Floor {newFloor}%  +{healed} HP");
		}

		/// <summary>
		/// Heals for a share of maximum HP per consumed stack and returns the
		/// amount restored.
		/// </summary>
		private int HealByStacks(ICombatEntity caster, int stacks)
		{
			if (stacks <= 0 || caster is not Character character)
				return 0;

			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var healed = (int)(maxHp * HealPerStack * stacks);

			if (healed > 0)
				character.Heal(healed, 0);

			return healed;
		}
	}
}
