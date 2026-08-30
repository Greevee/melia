using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Buffs.Handlers.Clerics.Zealot;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Fanatic Illusion, reworked into "Zeal".
	/// The kit's plain attack: a hard AoE strike that lights the fire if it
	/// is out, and banks a charge towards the next Immolation. Three
	/// charges, three presses, then the blast pays them all back at once —
	/// that is the loop the class runs on minute to minute, and it is all
	/// this skill does. The amplifying state it used to carry as well now
	/// lives on Blind Faith.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_FanaticIllusion)]
	public class Zealot_FanaticIllusionOverride : IGroundSkillHandler
	{
		private const float StrikeRadius = 50f;


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

			// The strike lights the fire if it is not burning yet, so Zeal
			// works as an opener as well as an amplifier.
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.StartBuff(BuffId.Immolation_Self_Buff, skill.Level, 0f, TimeSpan.Zero, caster, SkillId.Zealot_Immolation);
				// Lighting is the commitment, so it brings the first stage
				// along: the cast burns from the start instead of opening a
				// free, inert mode.
				ZealotBurnFloor.Set(caster, ZealotBurnFloor.Ignition - ZealotBurnFloor.Step);
			}

			// Three charges, and every one makes the next Immolate hit
			// harder. One press, one job.
			Zeal_Charge_BuffOverride.Add(caster, skill.Id);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, StrikeRadius, StrikeRadius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Strike(skill, caster, splashArea));
		}

		/// <summary>
		/// The activating strike around the Zealot.
		/// </summary>
		private async Task Strike(Skill skill, ICombatEntity caster, ISplashArea splashArea)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
			var hits = new List<SkillHitInfo>();

			foreach (var enemy in targets.LimitBySDR(caster, skill))
			{
				var modifier = SkillModifier.Default;
				modifier.AttackAttribute = AttributeType.Fire;

				var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(skillHitResult.Damage, caster);

				ZealotBurnFloor.PulseFireHit(enemy);

				hits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
			}

			if (hits.Count > 0)
				Send.ZC_SKILL_HIT_INFO(caster, hits);
		}
	}
}
