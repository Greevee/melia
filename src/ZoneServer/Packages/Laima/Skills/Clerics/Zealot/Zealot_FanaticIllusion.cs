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
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Fanatic Illusion, reworked into "Zeal".
	/// A hard AoE strike that lights the fire if it is out, and then the
	/// amplifier state: the stage damage bonus counts double and every
	/// attack strikes with Fire, for one Fanaticism stack per second (see
	/// Zeal_Judgement_Buff). Re-pressing repeats the strike; the state
	/// itself keeps running on its own fuel.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_FanaticIllusion)]
	public class Zealot_FanaticIllusionOverride : IGroundSkillHandler
	{
		private const float StrikeRadius = 50f;

		/// <summary>
		/// How long the amplifier holds after a press. Slightly longer than
		/// the cooldown, so keeping Zeal up is a thing the player does with
		/// their hands rather than a state that simply exists.
		/// PLACEHOLDER.
		/// </summary>
		private static readonly TimeSpan ZealDuration = TimeSpan.FromSeconds(6);

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
				ZealotBurnFloor.Set(caster, ZealotBurnFloor.Ignition);
			}

			// The amplifier runs on its own timer and every press refreshes
			// it. Free of Fanaticism on purpose: the stacks are Pyre's lash
			// count, so an amplifier that ate them would just be a worse
			// Pyre. What Zeal costs is the cooldown and the press itself.
			caster.StartBuff(BuffId.FanaticIllusion_Buff, skill.Level, 0f, ZealDuration, caster, skill.Id);

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
