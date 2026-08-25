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
	/// Per the rework, this is what makes burning yourself down survivable:
	/// it spends every Fervor stack and converts them into a shield worth 5%
	/// of maximum HP each. Immolation drives the caster low and builds the
	/// stacks; Blind Faith turns that accumulated risk into a buffer against
	/// the burst that would otherwise finish them.
	/// The shield itself is handled by Cleric_HolyAura_Buff.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BlindFaith)]
	public class Zealot_BlindFaithOverride : ISelfSkillHandler
	{
		/// <summary>
		/// Share of maximum HP each consumed Fervor stack is worth.
		/// At the twenty stack cap this is the caster's whole health bar.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float ShieldPerStack = 0.05f;

		/// <summary>
		/// Shown in the tooltip via captionTime in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private static readonly TimeSpan ShieldDuration = TimeSpan.FromSeconds(15);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			var stacks = ZealotFervor.GetStacks(caster);

			if (stacks <= 0)
			{
				caster.ServerMessage(Localization.Get("No Fervor to spend."));
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

			ZealotFervor.ConsumeAll(caster);

			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var shield = maxHp * ShieldPerStack * stacks;

			// The shield value travels as NumArg1, which the buff handler
			// reads to size the absorption pool.
			caster.StartBuff(BuffId.Cleric_HolyAura_Buff, shield, stacks, ShieldDuration, caster, skill.Id);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0, $"Shield {(int)shield} ({stacks} Fervor)");
		}
	}
}
