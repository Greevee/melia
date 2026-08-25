using System;
using Melia.Shared.Game.Const;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The burn floor: the share of maximum HP that Immolation burns the
	/// Zealot down to, and the single number the whole class revolves around.
	/// Lower floor means less health, and less health means more damage on
	/// everything — so the floor is the player's risk dial.
	/// Fanaticism lowers it, Temper the Flame raises it again.
	/// </summary>
	// The step, minimum and ignition values below are shown in the tooltips
	// via the captionRatio fields of the Zealot skills in
	// skills_overrides.txt — keep the two in sync:
	// Fanaticism captionRatio1 = StepDown, captionRatio2 = Min;
	// Temper the Flame captionRatio2 = StepUp;
	// Immolation captionRatio2 = Ignition, captionRatio3 = Fervor max.
	public static class ZealotBurnFloor
	{
		private const string FloorVar = "Zealot.BurnFloor";

		/// <summary>
		/// Full health: Immolation has nothing to burn and the class deals
		/// no bonus damage at all.
		/// </summary>
		public const int Default = 100;

		/// <summary>
		/// How far Fanaticism drops the floor per use.
		/// </summary>
		public const int StepDown = 20;

		/// <summary>
		/// How far Temper the Flame raises it. Deliberately larger than the
		/// way down: descending is gradual, pulling out is decisive.
		/// </summary>
		public const int StepUp = 40;

		/// <summary>
		/// Lowest floor currently reachable. Zero is deliberately left out:
		/// hitting it is meant to unlock something of its own later, so the
		/// dial stops one step short for now.
		/// </summary>
		public const int Min = 20;

		public const int Max = 100;

		/// <summary>
		/// Returns the entity's current floor, defaulting to full health.
		/// </summary>
		public static int Get(ICombatEntity entity)
		{
			var value = (int)entity.GetTempVar(FloorVar);

			// Unset variables read as zero, which would mean "burn everything"
			// rather than "untouched".
			if (value <= 0)
				return Default;

			return value;
		}

		/// <summary>
		/// The floor Immolation sets when it is first lit.
		/// </summary>
		public const int Ignition = 80;

		/// <summary>
		/// Moves the floor by the given amount and returns the new value,
		/// clamped to the allowed range.
		/// </summary>
		/// <remarks>
		/// Reaching full health puts the flame out entirely — there is
		/// nothing left to burn, so the aura and its damage bonus go with it.
		/// Immolation has to be cast again to relight it.
		/// </remarks>
		public static int Shift(ICombatEntity entity, int delta)
		{
			var value = Math.Clamp(Get(entity) + delta, Min, Max);
			entity.SetTempVar(FloorVar, value);

			if (value >= Max)
			{
				entity.StopBuff(BuffId.Immolation_Self_Buff);
				return value;
			}

			ShowOnAura(entity, value);

			return value;
		}

		/// <summary>
		/// Sets the floor outright, used when Immolation is lit.
		/// </summary>
		public static void Set(ICombatEntity entity, int value)
		{
			value = Math.Clamp(value, Min, Max);
			entity.SetTempVar(FloorVar, value);

			ShowOnAura(entity, value);
		}

		/// <summary>
		/// Displays the current floor as the stack count on the Immolation
		/// buff, so the player can read their risk setting off the buff bar.
		/// </summary>
		/// <remarks>
		/// Requires overBuff on the buff entry to allow counts up to 100;
		/// see buffs_overrides.txt. As with any stack change, the client only
		/// learns about it through NotifyUpdate.
		/// </remarks>
		public static void ShowOnAura(ICombatEntity entity, int floor)
		{
			if (!entity.TryGetBuff(BuffId.Immolation_Self_Buff, out var aura))
				return;

			aura.OverbuffCounter = floor;
			aura.NotifyUpdate();
		}

		/// <summary>
		/// The flame the burning Zealot stands in, respawned every aura tick
		/// at the current position. PoC values, deliberately oversized: the
		/// deeper the floor, the bigger the fire.
		/// </summary>
		/// <remarks>
		/// Effect names MUST exist in the packet string db
		/// (system/db/packetstrings.txt) — AddStringId throws for unknown
		/// names. Delivery uses PlayEffectAtPosition, the only effect channel
		/// verified to render on this client build: AttachEffect is a no-op
		/// and AddEffect/RemoveEffectByName are accepted but draw nothing.
		/// </remarks>
		public const string AuraEffectName = "I_sphere009_fire";

		/// <summary>
		/// Plays one pulse of the burning-body fire on the entity, sized by
		/// the current floor: 0.5 at ignition (floor 80), growing linearly
		/// to 1.5 at the deepest floor (20).
		/// </summary>
		/// <summary>
		/// The skeleton node the flame is attached to. Dummy_body is the
		/// torso: it follows the character without the wild spinning that
		/// hand bones add while running.
		/// </summary>
		public const string AuraNodeName = "Dummy_body";

		/// <remarks>
		/// PlayEffectNode attaches the pulse to a skeleton node, so the flame
		/// truly moves with the model (plain PlayEffect renders at the spawn
		/// position instead). One pulse per aura tick; the effect is a
		/// one-shot and ends on its own.
		/// </remarks>
		public static void PulseAuraVisual(ICombatEntity entity, int floor)
		{
			var scale = Math.Clamp(0.5f + (Ignition - floor) / 60f, 0.5f, 1.5f);
			entity.PlayEffectNode(AuraEffectName, scale, AuraNodeName);
		}

		/// <summary>
		/// Returns the share of maximum HP the entity is currently missing,
		/// as a percentage. This is the class damage bonus.
		/// </summary>
		public static float GetMissingHpPercent(ICombatEntity entity)
		{
			var maxHp = entity.Properties.GetFloat(PropertyName.MHP);
			if (maxHp <= 0)
				return 0;

			var missing = 100f * (1f - (entity.Hp / maxHp));

			return Math.Clamp(missing, 0f, 100f);
		}
	}
}
