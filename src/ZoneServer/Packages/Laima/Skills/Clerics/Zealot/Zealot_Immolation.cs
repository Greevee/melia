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
	/// The first cast lights the fire at the first stage. While burning,
	/// further casts unleash a blast whose damage and area grow with the
	/// stage, spending every Fanaticism stack for extra damage.
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
		/// Blast radius at the first stage, growing with every point of floor
		/// below it: 40 at stage one, 90 at the deepest. The stage buys area
		/// here, not damage. PLACEHOLDER.
		/// </summary>
		private const float BurstBaseRadius = 40f;
		private const float BurstRadiusPerFloorPoint = 1f;

		/// <summary>
		/// Extra blast damage per consumed Fanaticism stack. The blast spends
		/// the whole bar, so this is what banking stacks is worth.
		/// PLACEHOLDER.
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

			// Lighting the flame is itself a strike. It used to be a free
			// press that dealt nothing, which meant every fight opened on a
			// button that did not answer.
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.StartBuff(BuffId.Immolation_Self_Buff, skill.Level, 0f, AuraDuration, caster, skill.Id);
				ZealotBurnFloor.Set(caster, ZealotBurnFloor.Ignition);
			}

			var floor = ZealotBurnFloor.Get(caster);
			var stacks = ZealotBurnFloor.ConsumeStacks(caster);

			this.SpawnCastFire(caster, originPos);

			var radius = BurstBaseRadius + (ZealotBurnFloor.Ignition - floor) * BurstRadiusPerFloorPoint;
			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, radius, radius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Burst(skill, caster, splashArea, stacks));
		}

		/// <summary>
		/// A fire patch on the ground at the caster, growing with the stage.
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
		/// The fire burst around the caster: the area comes from the stage,
		/// the damage from the Fanaticism stacks it just consumed.
		/// </summary>
		private async Task Burst(Skill skill, ICombatEntity caster, ISplashArea splashArea, int stacks)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			// Depth is already paid for by the stage bonus every attack
			// carries (Immolation_Self_Buff); paying for it a second time
			// here made the blast impossible to explain in a tooltip. The
			// stacks are the only thing this skill scales on itself.
			var bonus = 1f + stacks * BurstDamagePerStack;

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
