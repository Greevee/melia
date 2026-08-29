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
	/// A lash of fire around the Zealot that gives back a share of
	/// everything it deals, and opens the state Zeal used to carry: for a
	/// few seconds the stage counts double and every attack strikes with
	/// Fire. Zeal is the plain press that builds towards Immolation; this
	/// is the one that makes all of it hit harder while it holds.
	/// The sustaining part of the old Blind Faith runs on its own inside
	/// the burning aura now, because a counterweight to the burn only works
	/// if it never stops.
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

		/// <summary>
		/// How long the amplifier holds after a press. PLACEHOLDER — at the
		/// skill's current five second cooldown this is longer than the gap
		/// between presses, so the state is effectively permanent; one of
		/// the two numbers wants to move in the scaling pass.
		/// Shown in the tooltip via captionTime — keep the two in sync.
		/// </summary>
		private static readonly TimeSpan JudgementDuration = TimeSpan.FromSeconds(6);

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

			caster.StartBuff(BuffId.FanaticIllusion_Buff, skill.Level, 0f, JudgementDuration, caster, skill.Id);

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
				modifier.AttackAttribute = AttributeType.Fire;

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
