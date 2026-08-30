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
		/// The floor Immolate sets when the burn mode is first activated:
		/// stage zero, where nothing burns yet. The fire only starts eating
		/// once Fanaticism stokes it down a step.
		/// </summary>
		public const int Ignition = 100;

		/// <summary>
		/// Lowest floor Fanaticism can settle on — the third and deepest
		/// stage.
		/// </summary>
		public const int Min = 0;

		/// <summary>
		/// Step size for lowering (Fanaticism) and raising (Temper).
		/// </summary>
		public const int Step = 25;


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
		/// Sets the floor outright, used when the fire is lit.
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
			var scale = StageFlameScale[GetStage(entity)];
			entity.PlayEffectNode(AuraEffectName, scale * FlameSizeFactor * scaleFactor, AuraNodeName);
		}

		/// <summary>
		/// How many steps down the Zealot has committed: 1 at the ignition
		/// floor, one more per step below it. Reads the floor, not current
		/// health, so healing never takes the reward away.
		/// </summary>
		public static int GetStage(ICombatEntity entity)
		{
			var stage = (Ignition - Get(entity)) / Step;

			return Math.Clamp(stage, 0, MaxStage);
		}

		/// <summary>
		/// Number of stages the floor can reach: 75%, 50%, 25%.
		/// </summary>
		public const int MaxStage = 4;

		/// <summary>
		/// Damage bonus per stage, applied to everything the burning Zealot
		/// does. Sized so that Zeal, which doubles it, turns the deepest
		/// stage into +100%. PLACEHOLDER values; mirrored into Immolate's
		/// captionRatio2 for the tooltip.
		/// </summary>
		private static readonly float[] StageDamageBonus = { 0f, 0.125f, 0.25f, 0.375f, 0.50f };

		/// <summary>
		/// Flame size per stage — the visual tell for which stage is live.
		/// </summary>
		private static readonly float[] StageFlameScale = { 0.5f, 0.8f, 1.2f, 1.6f, 2.0f };

		/// <summary>
		/// Blows that Temper the Flame turned into fire and that have not
		/// burned off yet. Pooled rather than kept as one debuff per hit:
		/// eight hits inside a window would otherwise be eight overlapping
		/// timers, unreadable in the bar and pointless to track separately.
		/// One number, worked off a share at a time.
		/// </summary>
		private const string DeferredVar = "Zealot.Deferred";

		/// <summary>
		/// Roughly how long a deferred blow takes to burn off, and the point
		/// below which the rest is simply taken at once so the pool always
		/// terminates instead of halving forever. PLACEHOLDER.
		/// Shown in Temper's tooltip via captionRatio3 — keep them in sync.
		/// </summary>
		public const float DeferredBurnSeconds = 4f;

		/// <summary>
		/// The share of a blow Temper the Flame turns into fire while its
		/// window holds. A delay, not a mitigation: the same total arrives,
		/// as a burn rather than a spike. PLACEHOLDER.
		/// Shown in Temper's tooltip via captionRatio2 — keep them in sync.
		/// </summary>
		public const float TemperedDeferredShare = 0.20f;
		private const float DeferredFloorShare = 0.005f;

		/// <summary>
		/// Adds a blow the fire took instead of the body.
		/// </summary>
		public static void AddDeferred(ICombatEntity entity, float amount)
		{
			if (amount <= 0)
				return;

			entity.SetTempVar(DeferredVar, GetDeferred(entity) + amount);
		}

		/// <summary>
		/// The fire still waiting to be paid for.
		/// </summary>
		public static float GetDeferred(ICombatEntity entity)
			=> Math.Max(0f, entity.GetTempVar(DeferredVar));

		/// <summary>
		/// Takes one second's worth off the pool and returns it. The
		/// remainder is taken whole once it drops below a fraction of a
		/// life, so the burn ends rather than trailing off forever.
		/// </summary>
		public static float DrainDeferred(ICombatEntity entity, float maxHp)
		{
			var pool = GetDeferred(entity);
			if (pool <= 0)
				return 0f;

			var share = pool / DeferredBurnSeconds;

			if (pool - share <= maxHp * DeferredFloorShare)
				share = pool;

			entity.SetTempVar(DeferredVar, pool - share);

			return share;
		}

		/// <summary>
		/// Putting the flame out puts the deferred fire out with it.
		/// </summary>
		public static void ClearDeferred(ICombatEntity entity)
			=> entity.SetTempVar(DeferredVar, 0f);



		/// <summary>
		/// The damage bonus the entity's current stage is worth.
		/// </summary>
		public static float GetStageBonus(ICombatEntity entity)
			=> StageDamageBonus[GetStage(entity)];

		/// <summary>
		/// The bonus of a given stage, as a percentage — for tooltips.
		/// </summary>
		public static float GetStageBonusPercent(int stage)
			=> StageDamageBonus[Math.Clamp(stage, 0, MaxStage)] * 100f;

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

	}
}
