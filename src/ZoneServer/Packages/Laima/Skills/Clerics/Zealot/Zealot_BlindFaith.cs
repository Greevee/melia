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
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Blind Faith.
	/// A short-cooldown lash of holy fire around the Zealot that gives back
	/// a share of everything it deals. The healing that used to live here
	/// runs on its own inside the burning aura now, because a counterweight
	/// to the burn only works if it never stops — what the button does
	/// instead is the thing the kit was missing: something to press that
	/// answers immediately.
	/// Faith that feeds on the fight, rather than faith you have to stop and
	/// maintain.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_BlindFaith)]
	public class Zealot_BlindFaithOverride : ISelfSkillHandler
	{
		/// <summary>
		/// How far the lash reaches. Handled as a self skill because that is
		/// what the client sends for this one, so the area is always centred
		/// on the Zealot. PLACEHOLDER.
		/// </summary>
		private const float StrikeRadius = 70f;

		/// <summary>
		/// The share of the damage dealt that comes back as health, per
		/// enemy hit. Shown in the tooltip via captionRatio2 in
		/// skills_overrides.txt — keep the two in sync. PLACEHOLDER.
		/// </summary>
		private const float LifestealShare = 0.25f;

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!caster.TrySpendSp(skill))
			{
				caster.ServerMessage(Localization.Get("Not enough SP."));
				Send.ZC_SKILL_DISABLE(caster);
				return;
			}

			skill.IncreaseOverheat();
			caster.SetAttackState(true);

			var farPos = new Position(originPos);
			farPos.X += 100;

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			_ = caster.PlayEffectToGround("F_burstup036_fire", caster.Position, 1.2f, duration: 800f);

			var splashParam = skill.GetSplashParameters(caster, originPos, farPos, StrikeRadius, StrikeRadius, angle: 0);
			var splashArea = skill.GetSplashArea(SplashType.Circle, splashParam);

			skill.Run(this.Lash(skill, caster, splashArea));
		}

		/// <summary>
		/// Strikes everything around the Zealot and gives back a share of
		/// what it took. Ordinary damage, so defence and resistances apply
		/// and the healing follows what actually landed rather than what was
		/// theoretically dealt.
		/// </summary>
		private async Task Lash(Skill skill, ICombatEntity caster, ISplashArea splashArea)
		{
			await skill.Wait(TimeSpan.FromMilliseconds(100));

			var targets = caster.Map.GetAttackableEnemiesIn(caster, splashArea);
			var hits = new List<SkillHitInfo>();
			var dealt = 0f;

			foreach (var enemy in targets.LimitBySDR(caster, skill))
			{
				var modifier = SkillModifier.Default;
				modifier.AttackAttribute = AttributeType.Holy;

				var skillHitResult = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(skillHitResult.Damage, caster);
				dealt += skillHitResult.Damage;

				hits.Add(new SkillHitInfo(caster, enemy, skill, skillHitResult, TimeSpan.FromMilliseconds(50), TimeSpan.Zero));
			}

			if (hits.Count > 0)
				Send.ZC_SKILL_HIT_INFO(caster, hits);

			var heal = dealt * LifestealShare;
			if (heal > 0 && caster is Character character)
				character.Heal(heal, 0);
		}
	}
}
