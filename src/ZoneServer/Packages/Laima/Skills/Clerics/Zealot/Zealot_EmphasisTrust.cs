using System;
using System.Linq;
using Melia.Shared.Data.Database;
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
	/// Handler for the Zealot skill Emphatic Trust.
	/// The crowd version of the heretic's brand: it lays the very same mark
	/// on everything around the Zealot for twenty seconds, so there is one
	/// mark in the kit rather than two sets of rules (see
	/// Heretic_Brand_Debuff). A kill on one of these pays far less than a
	/// kill on Brand's single mark. Deals no damage of its own.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_EmphasisTrust)]
	public class Zealot_EmphasisTrustOverride : IGroundSkillHandler
	{
		/// <summary>
		/// Debuff radius around the caster, target cap, and duration (shown
		/// via captionTime in skills_overrides.txt — keep the two in sync).
		/// The radius stays a PLACEHOLDER; the twenty seconds and twenty
		/// targets are the intended values.
		/// </summary>
		/// <summary>
		/// What a kill on one of these marks pays. Far below what Brand's
		/// single mark is worth, because this one lands on a whole pack:
		/// otherwise marking four enemies and bombing them would hand out a
		/// full bar from one press.
		/// Shown in the tooltip via captionRatio2 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float KillStackReward = 1f;

		private const float DebuffRadius = 150f;
		private const int MaxTargets = 20;
		private static readonly TimeSpan DebuffDuration = TimeSpan.FromSeconds(20);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, target?.Handle ?? 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			var enemies = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, DebuffRadius);

			foreach (var enemy in enemies.Take(MaxTargets))
				enemy.StartBuff(BuffId.BeadyEyed_Debuff, skill.Level, KillStackReward, DebuffDuration, caster, skill.Id);
		}
	}
}
