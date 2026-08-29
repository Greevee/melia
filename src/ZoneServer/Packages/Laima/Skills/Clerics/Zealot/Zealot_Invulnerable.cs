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
	/// The class's one defensive press, and it deals no damage at all: a
	/// heal large enough to matter, one step back up the ladder, and a
	/// window in which blows land as fire instead of all at once.
	/// The cost is the stage it gives up — with nothing carrying the old
	/// bonus over any more, quenching the flame really does cost the
	/// offence, and Fanaticism is how you buy it back. That is the whole
	/// ladder: Temper up for safety, Fanaticism down for damage.
	/// At the top there is nothing left to climb, so the press puts the
	/// fire out entirely — the way out of the mode.
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
			var atTop = ZealotBurnFloor.Get(caster) >= ZealotBurnFloor.Ignition;

			if (atTop)
			{
				// Nowhere left to climb: the press is the way out of the mode.
				// The aura's OnEnd drops the Fanaticism and the deferred fire
				// with it — putting the flame out puts all of it out.
				caster.StopBuff(BuffId.Immolation_Self_Buff);

				Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0,
					$"The flame is out  +{healed} HP");

				return;
			}

			var newFloor = ZealotBurnFloor.Shift(caster, ZealotBurnFloor.Step);

			// The window only makes sense while something is burning: the
			// aura's tick is what works the deferred fire off.
			caster.StartBuff(BuffId.Fanaticism_Buff, skill.Level, 0f, TemperedDuration, caster, skill.Id);

			_ = caster.PlayEffectToGround("F_wizard_prominence_ground", caster.Position, 1.2f, duration: 1000f);

			Send.ZC_NORMAL.PlayTextEffect(caster, caster, "SHOW_CUSTOM_TEXT", 0,
				$"Tempered to {newFloor}%  +{healed} HP");
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
