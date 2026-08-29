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
	/// The off switch, and the class's one defensive press: it puts the
	/// fire out, heals hard, and leaves a window in which blows land as
	/// fire instead of all at once. It deals no damage at all.
	/// One behaviour, always the same — it used to climb a stage and only
	/// put the fire out at the top, which meant the same button either
	/// helped you or deleted the whole class state depending on a number
	/// nobody watches mid-fight.
	/// The cost is everything the flame was worth: the stage bonus, the
	/// aura, the burn that loads the Pyre. Immolate lights it again from
	/// the top, and Fanaticism digs back down.
	/// Only usable while the burn mode is active — there is nothing to
	/// temper otherwise.
	/// Note: the skill database lists this as useType "MeleeGround", but the
	/// client sends CZ_SKILL_SELF for it, so it is handled as a self skill.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_Invulnerable)]
	public class Zealot_InvulnerableOverride : ISelfSkillHandler
	{
		/// <summary>
		/// The heal, as a share of maximum health plus an amount per point of
		/// SPR. Big on purpose: this press has no damage behind it, so the
		/// number it puts on screen is the entire feedback the player gets,
		/// and it has to be worth a thirty second cooldown. SPR doubles it
		/// around three hundred points, which is the same stat that decides
		/// where the burn settles. PLACEHOLDER values.
		/// Shown in the tooltip via captionRatio1 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		private const float HealMaxHpShare = 0.30f;
		private const float HealPerSpr = 3f;

		/// <summary>
		/// How long blows keep landing as fire after the press. Half the
		/// cooldown, so the window is a state the Zealot is in rather than
		/// one they are always in.
		/// Shown in the tooltip via captionTime — keep the two in sync.
		/// </summary>
		private static readonly TimeSpan TemperedDuration = TimeSpan.FromSeconds(15);

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

			var healed = this.Quench(caster, skill.Level);

			// The window is granted before the flame goes out: it carries its
			// own tick, so it keeps working the deferred fire off with
			// nothing else burning.
			caster.StartBuff(BuffId.Fanaticism_Buff, skill.Level, 0f, TemperedDuration, caster, skill.Id);

			caster.StopBuff(BuffId.Immolation_Self_Buff);

			_ = caster.PlayEffectToGround("F_wizard_prominence_ground", caster.Position, 1.6f, duration: 1200f);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0,
				$"The flame is out  +{healed} HP");
		}

		/// <summary>
		/// The heal itself, returning what it actually restored so the press
		/// can say so on screen.
		/// </summary>
		private int Quench(ICombatEntity caster, int skillLevel)
		{
			if (caster is not Character character)
				return 0;

			var maxHp = caster.Properties.GetFloat(PropertyName.MHP);
			var spr = caster.Properties.GetFloat(PropertyName.MNA);

			var heal = maxHp * HealMaxHpShare + spr * HealPerSpr;

			var missing = maxHp - caster.Hp;
			if (missing <= 0)
				return 0;

			var healed = (int)Math.Min(heal, missing);
			if (healed <= 0)
				return 0;

			character.Heal(healed, 0);

			return healed;
		}
	}
}
