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
	/// One activating AoE strike around the Zealot, then a burning state:
	/// while it lasts, every attack deals Fire property damage and every
	/// second one Fanaticism stack burns away in a fire pulse around the
	/// Zealot. Attacks made during the state build stacks back, so a Zealot
	/// who keeps swinging keeps Zeal alive — the state ends when the stacks
	/// run out (see Zeal_Judgement_Buff). The one-second cooldown is
	/// deliberate: the stack count is the real gate, not the timer.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_FanaticIllusion)]
	public class Zealot_FanaticIllusionOverride : IGroundSkillHandler
	{
		private const float StrikeRadius = 50f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (ZealotBurnFloor.GetStacks(caster) <= 0)
			{
				caster.ServerMessage(Localization.Get("No Fanaticism to spend."));
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
			Send.ZC_NORMAL.UpdateSkillEffect(caster, target?.Handle ?? 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			// Pressing Zeal costs fuel, so the stack count really is the cap
			// on how often it can be pressed — with a one-second cooldown the
			// SP bar alone would not be one.
			ZealotBurnFloor.AddStacks(caster, -1);

			// The burning state; it drains the stacks and ends itself when
			// they are gone. Duration TimeSpan.Zero means no timer at all
			// (Buff.HasDuration) — the stacks are the clock, which is what
			// lets attacks during Zeal prolong it.
			//
			// Only started when it is not already running. Re-starting an
			// active buff runs Buff.Activate, and its ExtendDuration pushes
			// NextUpdateTime a full update period out; at a one-second
			// cooldown that would postpone the stack drain forever and Zeal
			// would never end. A re-press buys the strike, not a new state.
			if (!caster.IsBuffActive(BuffId.FanaticIllusion_Buff))
				caster.StartBuff(BuffId.FanaticIllusion_Buff, skill.Level, 0f, TimeSpan.Zero, caster, skill.Id);

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
