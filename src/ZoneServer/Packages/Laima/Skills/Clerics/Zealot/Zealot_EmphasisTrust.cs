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
	/// Per the concept this keeps the vanilla behavior for the first
	/// implementation: debuffs enemies around the Zealot; a debuffed enemy
	/// takes additional damage whenever it is attacked (see
	/// EmphasisTrust_Debuff handler). Deals no damage of its own.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_EmphasisTrust)]
	public class Zealot_EmphasisTrustOverride : IGroundSkillHandler
	{
		/// <summary>
		/// PLACEHOLDER values until the balance pass: debuff radius around
		/// the caster, target cap, and duration (shown via captionTime in
		/// skills_overrides.txt).
		/// </summary>
		private const float DebuffRadius = 150f;
		private const int MaxTargets = 10;
		private static readonly TimeSpan DebuffDuration = TimeSpan.FromSeconds(10);

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
				enemy.StartBuff(BuffId.EmphasisTrust_Debuff, skill.Level, 0f, DebuffDuration, caster, skill.Id);
		}
	}
}
