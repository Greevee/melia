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
	/// The kit's payoff button: one lash of fire for every tenth of a life
	/// the Zealot has lost in the last ten seconds. Burning, blows taken,
	/// and the fire Temper deferred all count the same — the class loads
	/// its biggest press by taking punishment, which is the whole reason it
	/// stands where it stands.
	/// Firing does not empty the window; it keeps rolling, so whatever is
	/// left of those ten seconds is still there afterwards.
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
		/// The fire that spreads where the Zealot points, and the column that
		/// erupts on every enemy each lash. Both have been checked in game
		/// and actually read as fire — the explosion names picked off the
		/// string table turned out to look like light, which is the exact
		/// opposite of what this skill is.
		/// Compare them in game with >testeffect &lt;name&gt; &lt;seconds&gt;.
		/// </summary>
		private const string CastEffectName = "F_wizard_prominence_ground";
		private const string LashEffectName = "F_archer_MagicArrow_ground_fire_loop";

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			var lashes = ZealotBurnFloor.GetPyreLashes(caster);

			if (lashes <= 0)
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

			// One big answer where the Zealot is pointing, before the rain.
			_ = caster.PlayEffectToGround(CastEffectName, farPos, 2.0f, duration: 1500f);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, StrikeRadius, StrikeRadius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Strike(skill, caster, splashArea, lashes));
		}

		/// <summary>
		/// One ordinary skill hit per lash. Ordinary is the point: the damage
		/// runs through the normal pipeline with defence and resistances, so
		/// a full pyre is a hard hit rather than a way around the combat
		/// rules.
		/// </summary>
		private async Task Strike(Skill skill, ICombatEntity caster, ISplashArea splashArea, int lashes)
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

					var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
					enemy.TakeDamage(skillHitResult.Damage, caster);

					skillHits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.Zero, TimeSpan.Zero));

					// A column on each enemy each lash, so the number that
					// pops has something to pop out of.
					_ = caster.PlayEffectToGround(LashEffectName, enemy.Position, 0.9f, duration: 600f);
				}

				if (skillHits.Count > 0)
					Send.ZC_SKILL_HIT_INFO(caster, skillHits);

				if (i < lashes - 1)
					await skill.Wait(HitSpacing);
			}
		}
	}
}
