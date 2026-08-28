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
	/// nearby allies, scaling with SPR. It deals no damage, and the stacks
	/// are its clock: it ends the moment they run out — the same rule Zeal
	/// runs on, which is what makes the two compete for one resource.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Cleric_HolyAura_Buff)]
	public class BlindFaith_Hot_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Healing per tick per point of SPR (the property table calls it
		/// MNA). PLACEHOLDER magnitude.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float HealPerSpr = 1.5f;

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
			this.HealAllies(caster, heal);
		}

		/// <summary>
		/// Healing per tick: SPR-driven, with a modest per-level bonus.
		/// </summary>
		private float GetHealAmount(ICombatEntity caster, float skillLevel)
		{
			var spr = caster.Properties.GetFloat(PropertyName.MNA);

			return spr * HealPerSpr * (1f + skillLevel * HealPerSkillLevel);
		}

		private void HealCaster(ICombatEntity caster, float heal)
		{
			if (caster is Character character)
				character.Heal(heal, 0);
		}

		/// <summary>
		/// Heals allied players around the Zealot. The caster is healed
		/// separately, so they are skipped here.
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
