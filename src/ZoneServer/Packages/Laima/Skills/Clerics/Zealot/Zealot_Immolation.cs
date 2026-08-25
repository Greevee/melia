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
	/// Handler for the Zealot skill Immolate.
	/// Per the concept (Zealot_Rework_Konzept.xlsx v1.0): the first cast
	/// activates the burn mode and sets the burn floor to 70%. While
	/// burning, further casts unleash a fire burst whose damage and area
	/// grow the lower the floor sits; the burst also consumes all
	/// Fanaticism stacks for extra damage.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Immolation)]
	public class Zealot_ImmolationOverride : IGroundSkillHandler
	{
		/// <summary>
		/// The aura never expires on its own; only death removes it, since
		/// the buff entry is flagged removeOnDeath.
		/// </summary>
		private static readonly TimeSpan AuraDuration = TimeSpan.Zero;

		/// <summary>
		/// Burst radius at the ignition floor. PLACEHOLDER (concept: area
		/// tuning only) — grows by BurstRadiusPerFloorPoint for every floor
		/// point below ignition (80 -> 40, 40 -> 80).
		/// </summary>
		private const float BurstBaseRadius = 40f;
		private const float BurstRadiusPerFloorPoint = 1f;

		/// <summary>
		/// Extra burst damage per floor point below ignition. PLACEHOLDER —
		/// +1% per point: floor 40 deals +40%.
		/// </summary>
		private const float BurstDamagePerFloorPoint = 0.01f;

		/// <summary>
		/// Extra burst damage per consumed Fanaticism stack. PLACEHOLDER
		/// (concept: burst consumes all stacks; magnitude TBD).
		/// </summary>
		private const float BurstDamagePerStack = 0.15f;

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

			// First cast only lights the flame; the bursts come after.
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.StartBuff(BuffId.Immolation_Self_Buff, skill.Level, 0f, AuraDuration, caster, skill.Id);
				ZealotBurnFloor.Set(caster, ZealotBurnFloor.Ignition);
				this.SpawnCastFire(caster, originPos);
				return;
			}

			var floor = ZealotBurnFloor.Get(caster);
			var stacks = ZealotBurnFloor.ConsumeStacks(caster);

			this.SpawnCastFire(caster, originPos);

			var radius = BurstBaseRadius + (ZealotBurnFloor.Ignition - floor) * BurstRadiusPerFloorPoint;
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, radius, radius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Burst(skill, caster, splashArea, floor, stacks));
		}

		/// <summary>
		/// A fire patch on the ground at the caster, growing the deeper the
		/// burn floor sits (floor 70 -> 0.8, floor 10 -> 2.0).
		/// </summary>
		private void SpawnCastFire(ICombatEntity caster, Position originPos)
		{
			var floor = ZealotBurnFloor.Get(caster);
			var scale = 0.8f + (ZealotBurnFloor.Ignition - floor) * 0.02f;

			// Duration is in MILLISECONDS here: PlayEffectToGround uses the
			// actor overload of PlayEffectAtPosition, which divides by 1000
			// (the conn overload behind >testeffect takes seconds instead).
			// Cast visual: the fire pillar, for comparison against the flat
			// prominence ground (F_wizard_prominence_ground) - swap back if
			// the pillar loses.
			_ = caster.PlayEffectToGround("F_archer_MagicArrow_ground_fire_loop", originPos, scale, duration: 1000f);
		}

		/// <summary>
		/// The fire burst around the caster: damage scales with how deep the
		/// floor sits and with the Fanaticism stacks it just consumed.
		/// </summary>
		private async Task Burst(Skill skill, ICombatEntity caster, ISplashArea splashArea, int floor, int stacks)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			var bonus = 1f
				+ (ZealotBurnFloor.Ignition - floor) * BurstDamagePerFloorPoint
				+ stacks * BurstDamagePerStack;

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
			var hits = new List<SkillHitInfo>();

			foreach (var enemy in targets.LimitBySDR(caster, skill))
			{
				var modifier = SkillModifier.Default;
				modifier.DamageMultiplier *= bonus;

				var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(skillHitResult.Damage, caster);

				hits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
			}

			if (hits.Count > 0)
				Send.ZC_SKILL_HIT_INFO(caster, hits);
		}
	}
}
