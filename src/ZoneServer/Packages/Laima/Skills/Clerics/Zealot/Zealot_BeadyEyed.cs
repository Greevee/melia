using System;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Beady Eyed, reworked into
	/// "Brand the Heretic".
	/// Teleports the Zealot behind the target, marks it as a heretic and
	/// strikes it — a single medium hit, deliberately no AoE. While the mark
	/// lasts the target takes more damage from everything, and a marked
	/// target that dies grants Fanaticism stacks — both live in the mark's
	/// buff handler (Heretic_Brand_Debuff).
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BeadyEyed)]
	public class Zealot_BeadyEyedOverride : IGroundSkillHandler
	{
		/// <summary>
		/// How long the mark lasts. Shown in the tooltip via captionTime in
		/// skills_overrides.txt — keep the two in sync.
		/// </summary>
		private static readonly TimeSpan MarkDuration = TimeSpan.FromSeconds(5);


		/// <summary>
		/// Distance behind the target the Zealot appears at.
		/// </summary>
		private const float BlinkDistance = 20f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (target == null || target.IsDead || !caster.IsEnemy(target))
			{
				caster.ServerMessage(Localization.Get("No target to brand."));
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
			Send.ZC_NORMAL.UpdateSkillEffect(caster, target.Handle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			// Appear behind the target.
			var behindPos = target.Position.GetRelative(target.Direction.Backwards, BlinkDistance);
			caster.Position = behindPos;
			Send.ZC_SET_POS(caster, behindPos);

			// NumArg2 is what a kill on this mark pays: the precise press is
			// the rewarding one.
			// The mark carries what the press cost, so killing what you jumped
			// on refunds it exactly — no bookkeeping, no separate resource.
			var spendSp = skill.Properties.GetFloat(PropertyName.SpendSP);

			target.StartBuff(BuffId.BeadyEyed_Debuff, skill.Level, spendSp, MarkDuration, caster, skill.Id);

			skill.Run(this.Strike(skill, caster, target));
		}

		/// <summary>
		/// The brand's own hit: one medium single-target strike (skill
		/// factor from the db), landing right after the blink. It already
		/// benefits from the mark it just applied, since the mark raises
		/// damage taken from the moment it lands.
		/// </summary>
		private async Task Strike(Skill skill, ICombatEntity caster, ICombatEntity target)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(150));

			if (target.IsDead)
				return;

			var modifier = SkillModifier.Default;
			var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);
			target.TakeDamage(skillHitResult.Damage, caster);

			var skillHit = new SkillHitInfo(caster, target, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero);
			Send.ZC_SKILL_HIT_INFO(caster, skillHit);
		}
	}
}
