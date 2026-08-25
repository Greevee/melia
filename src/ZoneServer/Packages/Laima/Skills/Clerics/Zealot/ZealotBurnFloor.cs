using System;
using Melia.Shared.Game.Const;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The burn floor: the share of maximum HP that Immolate burns the
	/// Zealot down to, and the single number the kit revolves around.
	/// Per the concept workbook (Zealot_Rework_Konzept.xlsx v1.0) the floor
	/// runs in fixed steps 80 -> 60 -> 40: Fanaticism lowers it one step
	/// and grants a Fanaticism stack; at the 40% minimum it instead costs
	/// health directly and grants two stacks. Temper the Flame raises the
	/// floor one step (ending the burn mode when used at the top step).
	/// </summary>
	// Values shown in the tooltips come from the captionRatio fields of the
	// Zealot skills in skills_overrides.txt — keep the two in sync:
	// Immolate captionRatio2 = Ignition;
	// Fanaticism captionRatio1 = Step, captionRatio2 = Min;
	// Temper the Flame captionRatio2 = Step.
	public static class ZealotBurnFloor
	{
		private const string FloorVar = "Zealot.BurnFloor";
		private const string StacksVar = "Zealot.FanaticismStacks";

		/// <summary>
		/// The floor Immolate sets when the burn mode is first activated,
		/// and the highest step: Temper the Flame used here ends the mode.
		/// </summary>
		public const int Ignition = 80;

		/// <summary>
		/// Lowest reachable floor, after two Fanaticism uses. Deliberately
		/// no lower: below ~40% health the client blinks its low-HP warning
		/// permanently.
		/// </summary>
		public const int Min = 40;

		/// <summary>
		/// Step size for lowering (Fanaticism) and raising (Temper).
		/// </summary>
		public const int Step = 20;

		/// <summary>
		/// PLACEHOLDER (concept: "Stack-Cap noch offen") — generous cap,
		/// since Fanaticism at the minimum floor keeps granting stacks.
		/// </summary>
		public const int MaxFanaticismStacks = 10;

		/// <summary>
		/// Returns the entity's current floor; only meaningful while the
		/// burn mode (Immolation aura) is active.
		/// </summary>
		public static int Get(ICombatEntity entity)
		{
			var value = (int)entity.GetTempVar(FloorVar);

			// Unset variables read as zero, which would mean "burn everything"
			// rather than "untouched".
			if (value <= 0)
				return Ignition;

			return value;
		}

		/// <summary>
		/// Sets the floor outright, used when Immolate is lit.
		/// </summary>
		public static void Set(ICombatEntity entity, int value)
		{
			value = Math.Clamp(value, Min, Ignition);
			entity.SetTempVar(FloorVar, value);

			ShowOnAura(entity, value);
		}

		/// <summary>
		/// Moves the floor by the given amount and returns the new value,
		/// clamped to the step range. Ending the mode at the top step is the
		/// caller's decision (Temper the Flame), not a side effect here.
		/// </summary>
		public static int Shift(ICombatEntity entity, int delta)
		{
			var value = Math.Clamp(Get(entity) + delta, Min, Ignition);
			entity.SetTempVar(FloorVar, value);

			ShowOnAura(entity, value);

			return value;
		}

		/// <summary>
		/// Returns the entity's current Fanaticism stacks.
		/// </summary>
		public static int GetStacks(ICombatEntity entity)
			=> Math.Clamp((int)entity.GetTempVar(StacksVar), 0, MaxFanaticismStacks);

		/// <summary>
		/// The buff carrying the Fanaticism stack display, so the stacks
		/// show in the resource bar with icon and counter, Frenzy-style
		/// (Fanaticism_Zealot12_Buff, whose display is known to work).
		/// Display only — the authoritative count lives in the temp var.
		/// </summary>
		public const BuffId StackBuff = BuffId.Fanaticism_Zealot12_Buff;

		/// <summary>
		/// Adds Fanaticism stacks, up to the cap.
		/// </summary>
		public static void AddStacks(ICombatEntity entity, int amount)
		{
			var stacks = Math.Min(MaxFanaticismStacks, GetStacks(entity) + amount);
			entity.SetTempVar(StacksVar, stacks);
			ShowStacks(entity, stacks);
		}

		/// <summary>
		/// Removes all Fanaticism stacks and returns how many there were,
		/// so the Immolate burst can scale by what it consumed.
		/// </summary>
		public static int ConsumeStacks(ICombatEntity entity)
		{
			var stacks = GetStacks(entity);
			entity.SetTempVar(StacksVar, 0);
			ShowStacks(entity, 0);
			return stacks;
		}

		/// <summary>
		/// Mirrors the stack count onto the display buff: started with the
		/// first stack, counter updated on change, removed at zero.
		/// </summary>
		private static void ShowStacks(ICombatEntity entity, int stacks)
		{
			if (stacks <= 0)
			{
				entity.StopBuff(StackBuff);
				return;
			}

			if (!entity.TryGetBuff(StackBuff, out var buff))
			{
				entity.StartBuff(StackBuff, 1, 0f, TimeSpan.Zero, entity, SkillId.Zealot_Fanaticism);
				entity.TryGetBuff(StackBuff, out buff);
			}

			if (buff == null)
				return;

			buff.OverbuffCounter = stacks;
			buff.NotifyUpdate();
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
		/// The flame on the burning Zealot. One-shot, pulsed once per aura
		/// tick.
		/// </summary>
		/// <remarks>
		/// Effect names MUST exist in the packet string db
		/// (system/db/packetstrings.txt) — AddStringId throws for unknown
		/// names. Delivery uses PlayEffectNode, the only channel that truly
		/// parents an effect to the model: AttachEffect is a no-op on this
		/// client build, AddEffect draws nothing, and plain PlayEffect
		/// renders at the spawn position.
		/// </remarks>
		public const string AuraEffectName = "I_sphere009_fire";

		/// <summary>
		/// The skeleton node the flame is attached to. Dummy_body is the
		/// torso: it follows the character without the wild spinning that
		/// hand bones add while running.
		/// </summary>
		public const string AuraNodeName = "Dummy_body";

		/// <summary>
		/// Plays one pulse of the burning-body fire on the entity, sized by
		/// the ACTUAL health: 0.5 at 80% HP, growing linearly to 2.0 at 20%
		/// HP — so paying the blood price below the floor keeps feeding the
		/// flame, matching the missing-HP damage bonus.
		/// </summary>
		public static void PulseAuraVisual(ICombatEntity entity)
		{
			var hpPercent = 100f - GetMissingHpPercent(entity);
			var scale = Math.Clamp(0.5f + (Ignition - hpPercent) * 0.025f, 0.5f, 2.0f);
			entity.PlayEffectNode(AuraEffectName, scale, AuraNodeName);
		}

		/// <summary>
		/// Returns the share of maximum HP the entity is currently missing,
		/// as a percentage — the class damage bonus while burning.
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
