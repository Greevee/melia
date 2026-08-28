using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Geometry.Shapes;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for Blind Faith, the Zealot's heal over time (riding on
	/// Cleric_HolyAura_Buff).
	/// Every second it burns one Fanaticism stack and heals the Zealot and
	/// nearby allies, scaling with SPR. It deals no damage and has no timer:
	/// it runs until the Zealot switches it off or the stacks run out. Held
	/// continuously it is the counterweight to the burn, and where the two
	/// balance out is what the Zealot's SPR buys.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Cleric_HolyAura_Buff)]
	public class BlindFaith_Hot_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Healing per tick per point of SPR (the property table calls it
		/// MNA), on top of the flat share of maximum health below.
		/// PLACEHOLDER magnitude.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float HealPerSpr = 1.5f;

		/// <summary>
		/// Share of maximum health healed per tick regardless of SPR, so the
		/// skill works on the strength build the class actually wants. SPR
		/// stays worth investing in on top: it is what lets a Zealot
		/// out-heal the burn and hold a higher position above their stage,
		/// which is what feeds Pyre. PLACEHOLDER.
		/// </summary>
		private const float HealPerMaxHp = 0.02f;

		/// <summary>
		/// Extra healing per skill level, as a share of the SPR-based
		/// amount. PLACEHOLDER.
		/// </summary>
		private const float HealPerSkillLevel = 0.10f;

		/// <summary>
		/// How far the faith reaches. PLACEHOLDER.
		/// Shown in the tooltip via captionRatio2 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float HealRadius = 200f;

		/// <summary>
		/// The share of the Zealot's own healing that reaches an ally. Half,
		/// because this is a damage class borrowing a support tool: the
		/// party feels it without the Zealot competing with an actual
		/// healer. PLACEHOLDER.
		/// </summary>
		private const float AllyHealShare = 0.5f;

		/// <summary>
		/// However the faith stopped — switched off, starved, death — the
		/// skill icon has to stop showing it as running.
		/// </summary>
		public override void OnEnd(Buff buff)
		{
			if (buff.Target is not ICombatEntity caster)
				return;

			if (caster.TryGetSkill(buff.SkillId, out var skill))
				Zealot_BlindFaithOverride.SetToggled(skill, caster, false);
		}

		public override void WhileActive(Buff buff)
		{
			if (buff.Target is not ICombatEntity caster || caster.IsDead)
				return;

			// The stacks are the clock; when the fuel is gone, the faith
			// goes quiet. Checked before spending, so the last stack still
			// buys a tick.
			if (ZealotBurnFloor.GetStacks(caster) <= 0)
			{
				caster.StopBuff(BuffId.Cleric_HolyAura_Buff);
				return;
			}

			ZealotBurnFloor.AddStacks(caster, -1);

			// NumArg1 carries the skill level the buff was started with.
			var heal = this.GetHealAmount(caster, buff.NumArg1);
			if (heal <= 0)
				return;

			this.HealCaster(caster, heal);
			this.HealAllies(caster, heal * AllyHealShare);
		}

		/// <summary>
		/// Healing per tick: a share of maximum health so the skill works on
		/// the build the class actually has, plus SPR for players who choose
		/// to invest in out-healing the burn, with a modest per-level bonus
		/// on top of both.
		/// </summary>
		private float GetHealAmount(ICombatEntity caster, float skillLevel)
		{
			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var spr = caster.Properties.GetFloat(PropertyName.MNA);

			var baseHeal = maxHp * HealPerMaxHp + spr * HealPerSpr;

			return baseHeal * (1f + skillLevel * HealPerSkillLevel);
		}

		private void HealCaster(ICombatEntity caster, float heal)
		{
			if (caster is Character character)
				character.Heal(heal, 0);
		}

		/// <summary>
		/// Heals allied players around the Zealot, for a share of what the
		/// Zealot heals themselves. The caster is healed separately, so they
		/// are skipped here.
		/// </summary>
		private void HealAllies(ICombatEntity caster, float heal)
		{
			var area = new CircleF(caster.Position, HealRadius);

			foreach (var ally in caster.Map.GetActorsIn<Character>(area))
			{
				if (ally == caster || ally.IsDead)
					continue;

				if (!caster.IsAlly(ally))
					continue;

				ally.Heal(heal, 0);
			}
		}
	}
}
