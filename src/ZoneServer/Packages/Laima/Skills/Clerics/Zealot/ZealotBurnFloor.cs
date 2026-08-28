using System;
using Melia.Shared.Game.Const;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The burn floor: the share of maximum HP that Immolate burns the
	/// Zealot down to, and the single number the kit revolves around.
	/// The floor runs in three stages — 75%, 50%, 25% — and the stage, not
	/// current health, is what the damage bonus reads: committing deeper is
	/// rewarded and staying alive up there is a support problem, not a
	/// reason to lose the reward. Fanaticism steps down, Temper puts the
	/// fire out.
	/// </summary>
	// Values shown in the tooltips come from the captionRatio fields of the
	// Zealot skills in skills_overrides.txt — keep the two in sync:
	// Immolate captionRatio2 = Ignition, captionRatio1 = burn share;
	// Fanaticism captionRatio1 = Step, captionRatio2 = Min;
	// Temper the Flame captionRatio2 = Ignition.
	public static class ZealotBurnFloor
	{
		private const string FloorVar = "Zealot.BurnFloor";
		private const string StacksVar = "Zealot.FanaticismStacks";

		/// <summary>
		/// The floor Immolate sets when the burn mode is first activated,
		/// and the top of the ladder.
		/// </summary>
		public const int Ignition = 75;

		/// <summary>
		/// Lowest floor Fanaticism can settle on — the third and deepest
		/// stage.
		/// </summary>
		public const int Min = 25;

		/// <summary>
		/// Step size for lowering (Fanaticism) and raising (Temper).
		/// </summary>
		public const int Step = 25;

		/// <summary>
		/// The floor Fanaticism can dip to temporarily once it is already at
		/// the minimum, held only for the length of the Fanaticism window
		/// and then released back to Min. Not Min - Step: with three stages
		/// that would be zero, and the dip is meant to hurt, not to kill.
		/// </summary>
		/// <remarks>
		/// Only <see cref="Set"/> reaches this far down; <see cref="Shift"/>
		/// still stops at Min, so the ordinary step-down cannot land here by
		/// accident.
		/// </remarks>
		public const int TempMin = 10;

		/// <summary>
		/// PLACEHOLDER (concept: "Stack-Cap noch offen") — generous cap,
		/// since Fanaticism at the minimum floor keeps granting stacks.
		/// </summary>
		public const int MaxFanaticismStacks = 20;

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
		/// Sets the floor outright, used when Immolate is lit and for the
		/// temporary dip below Min that the Fanaticism window holds open.
		/// </summary>
		public static void Set(ICombatEntity entity, int value)
		{
			value = Math.Clamp(value, TempMin, Ignition);
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
			=> ConsumeStacks(entity, MaxFanaticismStacks);

		/// <summary>
		/// Spends at most the given number of stacks and returns how many
		/// were actually spent, leaving the rest on the bar — a spender with
		/// a cap cannot empty a full bar on its own.
		/// </summary>
		public static int ConsumeStacks(ICombatEntity entity, int max)
		{
			var spent = Math.Min(GetStacks(entity), Math.Max(0, max));
			var remaining = GetStacks(entity) - spent;

			entity.SetTempVar(StacksVar, remaining);
			ShowStacks(entity, remaining);

			return spent;
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
		/// Overall size of the body flame. The flame is the only indicator of
		/// the burn floor now that the ladder is just two steps, so it is
		/// sized up to be readable at a glance.
		/// </summary>
		private const float FlameSizeFactor = 1.25f;

		/// <summary>
		/// Plays one pulse of the burning-body fire on the entity, sized by
		/// the ACTUAL health: smallest at the ignition floor, growing
		/// linearly as health drops — so dipping below the floor keeps
		/// feeding the flame, matching the missing-HP damage bonus.
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="scaleFactor">
		/// Multiplies the health-derived size, so a state that makes the
		/// Zealot burn hotter can say so visually. Zeal passes 2.
		/// </param>
		public static void PulseAuraVisual(ICombatEntity entity, float scaleFactor = 1f)
		{
			// Sized by how deep the Zealot has committed, not by current
			// health: with a healer keeping them up, stage three has to look
			// like stage three.
			var scale = StageFlameScale[GetStage(entity) - 1];
			entity.PlayEffectNode(AuraEffectName, scale * FlameSizeFactor * scaleFactor, AuraNodeName);
		}

		/// <summary>
		/// How many steps down the Zealot has committed: 1 at the ignition
		/// floor, one more per step below it. Reads the floor, not current
		/// health, so healing never takes the reward away.
		/// </summary>
		public static int GetStage(ICombatEntity entity)
		{
			var stage = (Ignition - Get(entity)) / Step + 1;

			return Math.Clamp(stage, 1, StageCount);
		}

		/// <summary>
		/// Number of stages the floor can reach: 75%, 50%, 25%.
		/// </summary>
		public const int StageCount = 3;

		/// <summary>
		/// Damage bonus per stage, applied to everything the burning Zealot
		/// does. Sized so that Zeal, which doubles it, turns the deepest
		/// stage into +100%. PLACEHOLDER values; mirrored into Immolate's
		/// captionRatio2 for the tooltip.
		/// </summary>
		private static readonly float[] StageDamageBonus = { 0.10f, 0.25f, 0.50f };

		/// <summary>
		/// Flame size per stage — the visual tell for which stage is live.
		/// </summary>
		private static readonly float[] StageFlameScale = { 0.7f, 1.2f, 2.0f };

		/// <summary>
		/// Seconds between two passively generated stacks, per stage. The
		/// deepest stage funds exactly one channel (Zeal or Blind Faith
		/// drain one per second), which is the whole point: burning deep
		/// pays for staying in a state, and the Fanaticism window is what
		/// banks a surplus on top. PLACEHOLDER values.
		/// </summary>
		private static readonly int[] StageStackInterval = { 3, 2, 1 };

		/// <summary>
		/// Seconds the entity's current stage needs per passive stack.
		/// </summary>
		public static int GetStackInterval(ICombatEntity entity)
			=> StageStackInterval[GetStage(entity) - 1];

		/// <summary>
		/// The damage bonus the entity's current stage is worth.
		/// </summary>
		public static float GetStageBonus(ICombatEntity entity)
			=> StageDamageBonus[GetStage(entity) - 1];

		/// <summary>
		/// The bonus of a given stage, as a percentage — for tooltips and
		/// for the ember Temper freezes.
		/// </summary>
		public static float GetStageBonusPercent(int stage)
			=> StageDamageBonus[Math.Clamp(stage, 1, StageCount) - 1] * 100f;

		/// <summary>
		/// The sparks thrown off an enemy struck by Zeal, so a fire hit
		/// reads as a fire hit without a screen-wide overlay.
		/// </summary>
		/// <remarks>
		/// Same constraint as AuraEffectName: the name MUST exist in
		/// system/db/packetstrings.txt or AddStringId throws.
		/// </remarks>
		public const string FireHitEffectName = "F_spark011_orange";

		/// <summary>
		/// Plays the fire sparks on an enemy Zeal just burned.
		/// </summary>
		public static void PulseFireHit(ICombatEntity enemy)
			=> enemy.PlayEffectNode(FireHitEffectName, 1f, AuraNodeName);

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
