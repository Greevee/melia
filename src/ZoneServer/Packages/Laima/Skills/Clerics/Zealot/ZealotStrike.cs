using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The plain area strike shared by the Zealot's state skills.
	/// Temper the Flame and Fanaticism move the stage, which is the whole
	/// point of them — but a press that produces no number on an enemy
	/// reads as a press that did nothing, however much it changed. Both now
	/// answer with fire as well.
	/// </summary>
	public static class ZealotStrike
	{
		/// <summary>
		/// Hits everything in the area once, after the usual short wind-up.
		/// </summary>
		public static async Task Sweep(Skill skill, ICombatEntity caster, ISplashArea splashArea, AttributeType attribute)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
			var hits = new List<SkillHitInfo>();

			foreach (var enemy in targets.LimitBySDR(caster, skill))
			{
				var modifier = SkillModifier.Default;
				modifier.AttackAttribute = attribute;

				var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(skillHitResult.Damage, caster);

				hits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
			}

			if (hits.Count > 0)
				Send.ZC_SKILL_HIT_INFO(caster, hits);
		}
	}
}
