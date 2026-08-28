using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using static Melia.Zone.Skills.SkillUseFunctions;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zeal burning state (riding on FanaticIllusion_Buff).
	/// While it lasts, every attack the Zealot makes deals Fire property
	/// damage, and every second one Fanaticism stack burns away in a fire
	/// pulse that judges the nearest enemies.
	/// The stacks are the clock: attacks made during Zeal build them back,
	/// so a Zealot who keeps swinging keeps the state alive, and it ends the
	/// second the stacks run out.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.FanaticIllusion_Buff)]
	public class Zeal_Judgement_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Radius of the per-second fire pulse. PLACEHOLDER.
		/// </summary>
		private const float PulseRange = 60f;

		/// <summary>
		/// The AoE attack ratio each pulse spends. The nearest enemy is the
		/// primary target and always takes the hit; the rest of the ratio is
		/// spent on whatever else is in range, against their AoE defence —
		/// so against ordinary monsters a pulse reaches the primary plus two
		/// more. Zeal stays the focused spender; the Immolate burst is the
		/// pack spender.
		/// Shown via captionRatio2 in skills_overrides.txt — keep the two in
		/// sync.
		/// </summary>
		private const float PulseSr = 3f;

		/// <summary>
		/// Damage share of one pulse. PLACEHOLDER.
		/// </summary>
		private const float PulseFactor = 0.75f;

		/// <summary>
		/// How much brighter the burning body reads while Zeal is up. The
		/// flame is the only visual telling the player the state is live,
		/// so it doubles rather than shifting subtly.
		/// </summary>
		public const float AuraScaleFactor = 2f;

		public override void WhileActive(Buff buff)
		{
			var target = buff.Target;
			if (target.IsDead || target is not ICombatEntity caster)
				return;

			// The state burns one stack per second; when the fuel is gone,
			// it ends. Checked before spending so the last stack still buys
			// a pulse.
			if (ZealotBurnFloor.GetStacks(caster) <= 0)
			{
				target.StopBuff(BuffId.FanaticIllusion_Buff);
				return;
			}

			ZealotBurnFloor.AddStacks(caster, -1);
			this.FirePulse(buff, caster);
		}

		/// <summary>
		/// The per-second fire pulse around the Zealot.
		/// </summary>
		private void FirePulse(Buff buff, ICombatEntity caster)
		{
			if (!caster.TryGetSkill(buff.SkillId, out var skill))
				return;

			var enemies = this.SpendPulseSr(caster).ToList();
			if (enemies.Count == 0)
				return;

			var hits = new List<SkillHitInfo>();

			foreach (var enemy in enemies)
			{
				var modifier = SkillModifier.Default;
				modifier.AttackAttribute = AttributeType.Fire;
				modifier.DamageMultiplier *= PulseFactor;

				var result = SCR_SkillHit(caster, enemy, skill, modifier);
				enemy.TakeDamage(result.Damage, caster);

				ZealotBurnFloor.PulseFireHit(enemy);

				hits.Add(new SkillHitInfo(caster, enemy, skill, result, TimeSpan.Zero, TimeSpan.Zero));
			}

			Send.ZC_SKILL_HIT_INFO(caster, hits);
		}

		/// <summary>
		/// Picks the enemies one pulse reaches, spending PulseSr against
		/// their AoE defence ratios.
		/// </summary>
		/// <remarks>
		/// Deliberately not Extensions.LimitBySDR: that one reads the
		/// skill's own SR (Zeal's activating strike is far wider) and orders
		/// by SDR so the tankiest target soaks the ratio first. A pulse is
		/// meant to land on whatever the Zealot is standing next to, so this
		/// orders by distance and lets the nearest enemy be the primary
		/// target that always gets hit.
		/// </remarks>
		private IEnumerable<ICombatEntity> SpendPulseSr(ICombatEntity caster)
		{
			var enemies = caster.Map.GetAttackableEnemiesInPosition(caster, caster.Position, PulseRange)
				.OrderBy(a => caster.Position.Get2DDistance(a.Position));

			var sr = PulseSr;

			foreach (var enemy in enemies)
			{
				yield return enemy;

				sr -= enemy.Properties.GetFloat(PropertyName.SDR);
				if (sr <= 0)
					break;
			}
		}

		/// <summary>
		/// While Zeal burns, everything the Zealot does strikes with Fire.
		/// </summary>
		[CombatCalcModifier(CombatCalcPhase.BeforeCalc, BuffId.FanaticIllusion_Buff)]
		public void OnAttackBeforeCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!attacker.IsBuffActive(BuffId.FanaticIllusion_Buff))
				return;

			modifier.AttackAttribute = AttributeType.Fire;
		}

	}
}
