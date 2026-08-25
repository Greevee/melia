using System;
using Melia.Shared.Game.Const;
using Melia.Zone.Network;
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

			UpdateAuraVisual(entity, floor);
		}

		/// <summary>
		/// The flame attached to a burning Zealot. PoC values, deliberately
		/// oversized: the deeper the floor, the bigger the fire.
		/// </summary>
		public const string AuraEffectName = "F_buff_basic017_orange_fire";

		/// <summary>
		/// Attaches (or rescales) the burning-body effect to match the
		/// current floor. Detach happens in the buff handler's OnEnd.
		/// </summary>
		public static void UpdateAuraVisual(ICombatEntity entity, int floor)
		{
			// PoC scaling: floor 80 -> 2.0, floor 20 -> 5.0.
			var scale = 1f + (Max - floor) * 0.05f;

			Send.ZC_NORMAL.RemoveEffectByName(entity, AuraEffectName, true);
			Send.ZC_NORMAL.AttachEffect(entity, AuraEffectName, scale, EffectLocation.Bottom);
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
