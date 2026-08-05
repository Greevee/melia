using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Melia.Shared.Packages;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.Skills.SplashAreas;
using Melia.Zone.World.Actors;
using Yggdrasil.Extensions;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Wizards.Wizard
{
	/// <summary>
	/// Handles the Wizard skill Magic Missile.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Wizard_MagicMissile)]
	public class Wizard_MagicMissileOverride : IGroundSkillHandler, IDynamicCasted
	{
		private const int MaxTargets = 5;
		private const int RicochetTargets = 3;
		private const float SubSplashAreaSize = 200;
		private const float RicochetSpeed = 150;
		private const int MinTravelTimeMs = 50;
		private const int MaxTravelTimeMs = 400;

		/// <summary>
		/// Handles the skill, shooting missiles at enemies.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="originPos"></param>
		/// <param name="farPos"></param>
		/// <param name="target"></param>
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, length: 130, width: 60, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Square, splashParam);

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea).Take(MaxTargets).ToList();
			var aniTime = TimeSpan.FromMilliseconds(50);
			var skillHitDelay = skill.Properties.HitDelay;

			var skillHits = new List<SkillHitInfo>();

			foreach (var missileTarget in targets)
			{
				var skillHitResult = SCR_SkillHit(caster, missileTarget, skill);
				missileTarget.TakeDamage(skillHitResult.Damage, caster);

				var skillHit = new SkillHitInfo(caster, missileTarget, skill, skillHitResult, aniTime, skillHitDelay);
				skillHits.Add(skillHit);
			}

			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos, skillHits);

			skill.Run(this.Ricochet(skill, caster, skillHits));
		}

		/// <summary>
		/// Shoots the ricochet bullets, dealing their damage once they arrive.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="skillHits"></param>
		private async Task Ricochet(Skill skill, ICombatEntity caster, List<SkillHitInfo> skillHits)
		{
			var bullets = new List<(ICombatEntity Target, SkillHitResult Result, int ForceId, TimeSpan Delay)>();

			foreach (var skillHit in skillHits)
			{
				var sourceTarget = skillHit.Target;

				var subSplashArea = Square.Centered(sourceTarget.Position, caster.Direction, SubSplashAreaSize, SubSplashAreaSize / 2);
				var subTargets = caster.Map.GetAttackableEnemiesIn(caster, subSplashArea).Where(a => a != sourceTarget).Take(RicochetTargets);

				foreach (var subTarget in subTargets)
				{
					var skillHitResult = SCR_SkillHit(caster, subTarget, skill);
					var forceId = ForceId.GetNew();
					var travelTime = GetTravelTime(sourceTarget.Position.Get2DDistance(subTarget.Position));

					bullets.Add((subTarget, skillHitResult, forceId, travelTime));

					Send.ZC_NORMAL.PlayForceEffect(forceId, caster, sourceTarget, subTarget, "I_force001_yellow", 1, "arrow_cast", "I_explosion004_yellow", 1, "arrow_blow", "SLOW", RicochetSpeed);
				}
			}

			var elapsed = TimeSpan.Zero;

			foreach (var (target, result, forceId, delay) in bullets.OrderBy(a => a.Delay))
			{
				if (delay > elapsed)
				{
					await skill.Wait(delay - elapsed);
					elapsed = delay;
				}

				target.TakeDamage(result.Damage, caster);

				var hit = new HitInfo(caster, target, skill, result.Damage, result.Result);
				hit.ForceId = forceId;

				Send.ZC_HIT_INFO(caster, target, hit);
			}
		}

		/// <summary>
		/// Returns how long a ricochet bullet takes to reach a target
		/// the given distance away.
		/// </summary>
		/// <param name="distance"></param>
		private static TimeSpan GetTravelTime(double distance)
		{
			var progress = Math.Clamp(distance / SubSplashAreaSize, 0, 1);
			var travelTime = MinTravelTimeMs + progress * (MaxTravelTimeMs - MinTravelTimeMs);

			return TimeSpan.FromMilliseconds(travelTime);
		}

		// A shot into a bunch of monsters. The character hit 3 different
		// monsters once and each monster creates at least 3 bullets of
		// its own, hitting other nearby monsters. The numbers are
		// shortened handles.
		// Notably, each original target gets hit 3 times in total. What's
		// still unclear is what "5 Bullets" in the description means.
		// If it were a limit, you would expect more than 4 hits out
		// of 829, because as the first ricochet source it should be
		// able to go up to the max.
		// 
		// character -> 829
		// character -> 805
		// character -> 460
		// 829 -> 460
		// 829 -> 810
		// 829 -> 805
		// 829 -> 460
		// 805 -> 810
		// 805 -> 886
		// 805 -> 829
		// 460 -> 805
		// 460 -> 829
		// 460 -> 810
		// 
		// Shot at 3 monsters
		// 
		// character -> 459
		// character -> 422
		// character -> 459
		// 459 -> 519
		// 459 -> 422
		// 422 -> 459
		// 422 -> 519
		// 459 -> 519
		// 459 -> 422
		// 
		// New theory. The numer of ricochets is the number of monsters
		// in the splash area - 1. If you have two targets, you get one
		// additional bullet out of each hit, matching up our findings
		// for hitting two targets. If you hit three targets, you get
		// two additional bullets, matching the test above.
		// The first log above is more difficult to explain, because
		// 829 sent four bullets and the others only three, but maybe
		// 829 had four targets in range and the others only three...?
		// Although it was a large group and that seems unlikely as well.
		// Regardless, there does appear to be some kind of scaling based
		// on the targets involved.
	}
}
