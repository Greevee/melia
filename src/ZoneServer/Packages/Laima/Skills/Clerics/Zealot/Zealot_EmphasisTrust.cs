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
	/// Handler for the Zealot skill Emphatic Trust, reworked into "Pyre".
	/// The kit's payoff button: one lash of fire per Fanaticism stack, all
	/// of them spent at once. The stacks decide how many times it hits, and
	/// the health the fire has eaten since the last Pyre decides how hard
	/// each one lands — so building stacks is the press and staying high
	/// above your stage is the investment.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_EmphasisTrust)]
	public class Zealot_EmphasisTrustOverride : IGroundSkillHandler
	{
		private const float StrikeRadius = 60f;

		/// <summary>
		/// Delay between the lashes, so a full pyre reads as a rain of fire
		/// rather than one lump of damage.
		/// </summary>
		private static readonly TimeSpan HitSpacing = TimeSpan.FromMilliseconds(120);

		/// <summary>
		/// The fire going up on the cast, and the column that lands on every
		/// enemy each lash. Swap candidates, all registered in packetstrings:
		/// F_explosion050_fire, F_explosion051_fire, F_burstup027_fire,
		/// F_burstup036_fire, F_burstup040_fire, AerialExplosion_Fire_Orange_01.
		/// Compare them in game with >testeffect &lt;name&gt; &lt;seconds&gt;.
		/// </summary>
		private const string CastEffectName = "F_explosion050_fire";
		private const string LashEffectName = "F_burstup030_fire";

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			var lashes = ZealotBurnFloor.GetStacks(caster);

			if (lashes <= 0)
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

			ZealotBurnFloor.ConsumeStacks(caster);
			var bonus = ZealotBurnFloor.ConsumePyre(caster);

			// One big answer where the Zealot is pointing, before the rain.
			_ = caster.PlayEffectToGround(CastEffectName, farPos, 1.6f, duration: 1200f);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, StrikeRadius, StrikeRadius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Strike(skill, caster, splashArea, lashes, bonus));
		}

		/// <summary>
		/// One ordinary skill hit per lash. Ordinary is the point: the damage
		/// runs through the normal pipeline with defence and resistances, so
		/// a full pyre is a hard hit rather than a way around the combat
		/// rules.
		/// </summary>
		private async Task Strike(Skill skill, ICombatEntity caster, ISplashArea splashArea, int lashes, float bonus)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			for (var i = 0; i < lashes; ++i)
			{
				var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
				var skillHits = new List<SkillHitInfo>();

				foreach (var enemy in targets.LimitBySDR(caster, skill))
				{
					var modifier = SkillModifier.Default;
					modifier.AttackAttribute = AttributeType.Fire;
					modifier.DamageMultiplier *= bonus;

					var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
					enemy.TakeDamage(skillHitResult.Damage, caster);

					skillHits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.Zero, TimeSpan.Zero));

					// A column on each enemy each lash, so the number that
					// pops has something to pop out of.
					_ = caster.PlayEffectToGround(LashEffectName, enemy.Position, 1.1f, duration: 700f);
				}

				if (skillHits.Count > 0)
					Send.ZC_SKILL_HIT_INFO(caster, skillHits);

				if (i < lashes - 1)
					await skill.Wait(HitSpacing);
			}
		}
	}
}
