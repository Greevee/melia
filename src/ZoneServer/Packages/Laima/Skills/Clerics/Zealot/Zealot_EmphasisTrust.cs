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
	/// The kit's attack skill, and the only one whose size the player builds
	/// themselves: the fire remembers every point of health it has taken
	/// since the last Pyre, and this releases it as one strike per tenth of
	/// a life. Standing at your stage adds nothing — the fire only feeds
	/// while it actually eats, so being healed back up is what reloads this,
	/// which is the whole trick of the class.
	/// Firing empties the pyre, so every Pyre is paid for by the burning
	/// that came before it.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_EmphasisTrust)]
	public class Zealot_EmphasisTrustOverride : IGroundSkillHandler
	{
		private const float StrikeRadius = 60f;

		/// <summary>
		/// Delay between the strikes, so a full pyre reads as a rain of fire
		/// rather than one lump of damage.
		/// </summary>
		private static readonly TimeSpan HitSpacing = TimeSpan.FromMilliseconds(120);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			var hits = ZealotBurnFloor.GetPyreHits(caster);

			if (hits <= 0)
			{
				caster.ServerMessage(Localization.Get("The pyre is cold."));
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

			ZealotBurnFloor.ConsumePyre(caster);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, StrikeRadius, StrikeRadius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Strike(skill, caster, splashArea, hits));
		}

		/// <summary>
		/// One ordinary skill hit per strike the pyre held. Ordinary is the
		/// point: the damage runs through the normal pipeline with defence
		/// and resistances, so a full pyre is a hard hit rather than a way
		/// around the combat rules.
		/// </summary>
		private async Task Strike(Skill skill, ICombatEntity caster, ISplashArea splashArea, int hits)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			for (var i = 0; i < hits; ++i)
			{
				var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
				var skillHits = new List<SkillHitInfo>();

				foreach (var enemy in targets.LimitBySDR(caster, skill))
				{
					var modifier = SkillModifier.Default;
					modifier.AttackAttribute = AttributeType.Fire;

					var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
					enemy.TakeDamage(skillHitResult.Damage, caster);

					skillHits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
				}

				if (skillHits.Count > 0)
					Send.ZC_SKILL_HIT_INFO(caster, skillHits);

				_ = caster.PlayEffectToGround(ZealotBurnFloor.AuraEffectName, caster.Position, 1.2f, duration: 600f);

				if (i < hits - 1)
					await skill.Wait(HitSpacing);
			}
		}
	}
}
