using System;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Fanaticism — the stoker.
	/// Drives the fire one stage deeper, flares out as a fire strike, and
	/// opens the frenzy window. Its opposite number is not a button any
	/// more: the fire dies down a stage every half minute on its own, so
	/// pressing this on cooldown is what keeps the flame roaring and
	/// letting it rest is what puts it out.
	/// Only usable while the burn mode is active.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Fanaticism)]
	public class Zealot_FanaticismOverride : IGroundSkillHandler
	{
		/// <summary>
		/// How long the attack-speed window stays open. Ten seconds against
		/// the fifteen second cooldown, so it is up two thirds of the time
		/// and Fanaticism stays the clock the rest of the kit runs on.
		/// Shown in the tooltip via captionTime in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private static readonly TimeSpan RushDuration = TimeSpan.FromSeconds(10);

		/// <summary>
		/// How wide the flare reaches when the Zealot drops a stage.
		/// PLACEHOLDER.
		/// </summary>
		private const float StrikeRadius = 60f;

		/// <summary>
		/// Seconds of invulnerability the Martyrdom attribute grants per of
		/// its levels: three seconds at its maximum. PLACEHOLDER.
		/// </summary>
		private const float MartyrdomSecondsPerLevel = 0.6f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.ServerMessage(Localization.Get("The flame is not lit."));
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

			var targetHandle = target?.Handle ?? 0;
			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, targetHandle, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			// Stoking the fire: one stage deeper per press, against the decay
			// that pulls it one stage shallower every half minute. Pressed on
			// cooldown the fire climbs; left alone it dies. That tug is the
			// whole engine of the class now.
			var stageBefore = ZealotBurnFloor.GetStage(caster);
			var newFloor = ZealotBurnFloor.Shift(caster, -ZealotBurnFloor.Step);
			var stageNow = ZealotBurnFloor.GetStage(caster);

			this.GrantZealRush(skill, caster);
			this.GrantMartyrdom(skill, caster);

			_ = caster.PlayEffectToGround("F_explosion050_fire", caster.Position, 1.4f, duration: 900f);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0,
				stageNow > stageBefore ? $"Stage {stageNow}  ({newFloor}%)" : "Frenzy");

			// Throwing yourself deeper makes the fire flare. Without this the
			// press changed everything and showed nothing.
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, StrikeRadius, StrikeRadius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(ZealotStrike.Sweep(skill, caster, splashArea, AttributeType.Fire));
		}

		/// <summary>
		/// With the Martyrdom attribute, the press also buys a heartbeat of
		/// invulnerability — the one defensive beat a class that fights at a
		/// quarter of its health has. Scales to three seconds at max level.
		/// </summary>
		private void GrantMartyrdom(Skill skill, ICombatEntity caster)
		{
			if (!caster.TryGetActiveAbilityLevel(AbilityId.Zealot10, out var abilityLevel))
				return;

			var duration = TimeSpan.FromSeconds(abilityLevel * MartyrdomSecondsPerLevel);
			if (duration <= TimeSpan.Zero)
				return;

			caster.StartBuff(BuffId.Fanaticism_Martyrdom_Buff, abilityLevel, 0f, duration, caster, skill.Id);
		}

		/// <summary>
		/// The attack-speed window on every use (see Zeal_Rush_Buff).
		/// PLACEHOLDER values; duration shown via captionTime in
		/// skills_overrides.txt.
		/// </summary>
		private void GrantZealRush(Skill skill, ICombatEntity caster)
		{
			caster.StartBuff(BuffId.BeadyEyed_Buff, skill.Level, 0f, RushDuration, caster, skill.Id);
		}
	}
}
