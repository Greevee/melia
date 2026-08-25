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
	/// Handler for the Zealot skill Immolation.
	/// Per the rework it does two jobs: it strikes everything around the
	/// caster, and it lights the aura that carries the class mechanic. The
	/// aura is never toggled off — once lit it burns the caster down to their
	/// burn floor and keeps them there, converting missing health into damage
	/// on everything they do.
	/// The burning itself lives in Immolation_Self_Buff.
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

		private const int SplashLength = 40;
		private const int SplashWidth = 40;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, target?.Handle ?? 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_GROUND(caster, skill, farPos);

			this.LightAura(skill, caster);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, SplashLength, SplashWidth, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Attack(skill, caster, splashArea));
		}

		/// <summary>
		/// Lights the aura if it is out, dropping the caster to the ignition
		/// floor as it catches. An already burning flame is left alone, so
		/// recasting for the strike does not reset a floor the player worked
		/// their way down to.
		/// </summary>
		private void LightAura(Skill skill, ICombatEntity caster)
		{
			if (caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				ZealotBurnFloor.ShowOnAura(caster, ZealotBurnFloor.Get(caster));
				return;
			}

			caster.StartBuff(BuffId.Immolation_Self_Buff, skill.Level, 0f, AuraDuration, caster, skill.Id);
			ZealotBurnFloor.Set(caster, ZealotBurnFloor.Ignition);
		}

		/// <summary>
		/// The strike itself, hitting everything around the caster.
		/// </summary>
		private async Task Attack(Skill skill, ICombatEntity caster, ISplashArea splashArea)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
			var hits = new List<SkillHitInfo>();

			foreach (var target in targets.LimitBySDR(caster, skill))
			{
				var modifier = SkillModifier.Default;

				var skillHitResult = SCR_SkillHit(caster, target, skill, modifier);
				target.TakeDamage(skillHitResult.Damage, caster);

				hits.Add(new SkillHitInfo(caster, target, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
			}

			if (hits.Count > 0)
				Send.ZC_SKILL_HIT_INFO(caster, hits);
		}
	}
}
