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
	/// Handler for the Zealot skill Beady Eyed, reworked into
	/// "Brand the Heretic".
	/// Per the concept (Zealot_Rework_Konzept.xlsx v1.0): teleports the
	/// Zealot behind the target and marks it as a heretic. The next Zealot
	/// hit against the marked target is empowered; a marked target that dies
	/// grants Fervor. Against bosses the Fervor is granted once when the
	/// empowered hit triggers instead — both live in the mark's buff handler
	/// (Heretic_Brand_Debuff).
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BeadyEyed)]
	public class Zealot_BeadyEyedOverride : IGroundSkillHandler
	{
		/// <summary>
		/// How long the mark lasts. PLACEHOLDER (concept: "Brand-Dauer TBD").
		/// Shown in the tooltip via captionTime in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private static readonly TimeSpan MarkDuration = TimeSpan.FromSeconds(10);

		/// <summary>
		/// Distance behind the target the Zealot appears at.
		/// </summary>
		private const float BlinkDistance = 20f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (target == null || target.IsDead || !caster.IsEnemy(target))
			{
				caster.ServerMessage(Localization.Get("No target to brand."));
				return;
			}

			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, target.Handle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			// Appear behind the target, facing it.
			var behindPos = target.Position.GetRelative(target.Direction.Backwards, BlinkDistance);
			caster.Position = behindPos;
			Send.ZC_SET_POS(caster, behindPos);

			target.StartBuff(BuffId.BeadyEyed_Debuff, skill.Level, 0f, MarkDuration, caster, skill.Id);
		}
	}
}
