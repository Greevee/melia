using System;
using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot skill Invulnerable, reworked into
	/// "Temper the Flame".
	/// The panic button: one press puts the flame out and resets the whole
	/// burn state. The aura ends, the floor returns to ignition, every
	/// Fanaticism stack is dropped, and health is restored up to the
	/// ignition floor. The ember lingers: for ten seconds afterwards the
	/// Zealot keeps the damage bonus they had at the moment the fire went
	/// out, so retreating costs no offence right away (see Ember_Buff).
	/// Only usable while the burn mode is active — there is nothing to put
	/// out otherwise.
	/// Note: the skill database lists this as useType "MeleeGround", but the
	/// client sends CZ_SKILL_SELF for it, so it is handled as a self skill.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Invulnerable)]
	public class Zealot_InvulnerableOverride : ISelfSkillHandler
	{
		/// <summary>
		/// How long the ember keeps the damage bonus alive after the flame
		/// is out. Shown in the tooltip via captionTime in
		/// skills_overrides.txt — keep the two in sync.
		/// </summary>
		private static readonly TimeSpan EmberDuration = TimeSpan.FromSeconds(10);

		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Direction dir)
		{
			if (!caster.IsBuffActive(BuffId.Immolation_Self_Buff))
			{
				caster.ServerMessage(Localization.Get("The flame is not lit."));
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

			var farPos = new Position(originPos);
			farPos.X += 100;

			Send.ZC_SKILL_READY(caster, skill, 1, originPos, farPos);
			Send.ZC_NORMAL.UpdateSkillEffect(caster, 0, originPos, originPos.GetDirection(farPos), Position.Zero);
			Send.ZC_SKILL_MELEE_TARGET(caster, skill, caster);

			// Captured before the heal: the ember keeps the damage bonus the
			// Zealot had while dying, and healing up would erase it.
			var missingPercent = ZealotBurnFloor.GetMissingHpPercent(caster);

			// One press ends the burn state outright. Order matters: the heal
			// reads the ignition floor, so the floor is reset first, and the
			// aura is stopped last because its OnEnd drops the stacks.
			ZealotBurnFloor.Set(caster, ZealotBurnFloor.Ignition);

			var healed = this.RaiseToFloor(caster, ZealotBurnFloor.Ignition);

			caster.StopBuff(BuffId.Immolation_Self_Buff);

			// The fire is out, but the ember still burns: for a few seconds
			// the Zealot keeps hitting as hard as they did while dying, so
			// pulling out is not an instant loss of all offence.
			if (missingPercent > 0)
				caster.StartBuff(BuffId.Fanaticism_Buff, missingPercent, 0f, EmberDuration, caster, skill.Id);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0,
				"The flame is out" + (healed > 0 ? $"  +{healed} HP" : "")
				+ (missingPercent > 0 ? $"  (Ember +{(int)missingPercent}%)" : ""));
		}

		/// <summary>
		/// Raises health up to the new floor if it sits below it, returning
		/// the amount restored.
		/// </summary>
		private int RaiseToFloor(ICombatEntity caster, int floor)
		{
			if (caster is not Character character)
				return 0;

			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var floorHp = maxHp * (floor / 100f);

			var missing = (int)(floorHp - caster.Hp);
			if (missing <= 0)
				return 0;

			character.Heal(missing, 0);

			return missing;
		}
	}
}
