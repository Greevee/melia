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
		/// The rolling record of health the Zealot has lost, one bucket per
		/// second, ten buckets deep. Everything counts: the self-burn, blows
		/// from enemies, and the fire Temper deferred. It is all health the
		/// fire took, and Pyre pays out on the lot.
		/// One record instead of the old pair of pools — they were always
		/// measuring the same thing from two different ends.
		/// </summary>
		private const string HurtVar = "Zealot.Hurt";
		private const string HurtSlotVar = "Zealot.HurtSlot";

		/// <summary>
		/// How far Pyre looks back, how much lost health buys one lash, and
		/// the most lashes one Pyre can throw. PLACEHOLDER values.
		/// Shown in Pyre's tooltip via captionRatio1/2 and captionTime —
		/// keep them in sync.
		/// </summary>
		public const int HurtWindowSeconds = 10;
		public const float HurtPerLash = 0.10f;
		public const int MaxLashes = 12;

		/// <summary>
		/// The buff carrying the readout, counting lashes ready rather than
		/// raw health — that is the number the player acts on.
		/// </summary>
		public const BuffId PyreBuff = BuffId.ImmolationMeltArmor_Buff;

		/// <summary>
		/// Records health the Zealot just lost, however it was lost.
		/// </summary>
		public static void RecordHurt(ICombatEntity entity, float amount)
		{
			if (amount <= 0)
				return;

			var slot = GetHurtSlot(entity);
			entity.SetTempVar(HurtVar + slot, entity.GetTempVar(HurtVar + slot) + amount);

			ShowPyre(entity, GetPyreLashes(entity));
		}

		/// <summary>
		/// Moves the window on by a second, dropping whatever fell out of the
		/// back of it. Driven by the aura's tick.
		/// </summary>
		public static void RotateHurtWindow(ICombatEntity entity)
		{
			var slot = (GetHurtSlot(entity) + 1) % HurtWindowSeconds;

			entity.SetTempVar(HurtSlotVar, slot);
			entity.SetTempVar(HurtVar + slot, 0f);

			ShowPyre(entity, GetPyreLashes(entity));
		}

		/// <summary>
		/// Health lost across the whole window.
		/// </summary>
		public static float GetHurt(ICombatEntity entity)
		{
			var total = 0f;

			for (var i = 0; i < HurtWindowSeconds; ++i)
				total += Math.Max(0f, entity.GetTempVar(HurtVar + i));

			return total;
		}

		/// <summary>
		/// How many lashes the last ten seconds are worth.
		/// </summary>
		public static int GetPyreLashes(ICombatEntity entity)
		{
			var maxHp = entity.Properties.GetFloat(PropertyName.MHP);
			if (maxHp <= 0)
				return 0;

			var perLash = maxHp * HurtPerLash;
			if (perLash <= 0)
				return 0;

			return Math.Clamp((int)(GetHurt(entity) / perLash), 0, MaxLashes);
		}

		/// <summary>
		/// Empties the window. Firing Pyre does not do this — the window is a
		/// record of the last ten seconds and it keeps rolling regardless —
		/// but putting the flame out does.
		/// </summary>
		public static void ClearHurt(ICombatEntity entity)
		{
			for (var i = 0; i < HurtWindowSeconds; ++i)
				entity.SetTempVar(HurtVar + i, 0f);

			ShowPyre(entity, 0);
		}

		private static int GetHurtSlot(ICombatEntity entity)
			=> Math.Clamp((int)entity.GetTempVar(HurtSlotVar), 0, HurtWindowSeconds - 1);

		/// <summary>
		/// Mirrors the lashes ready onto the readout buff, so the build-up is
		/// visible instead of being a hidden number.
		/// </summary>
		private static void ShowPyre(ICombatEntity entity, int lashes)
		{
			if (lashes <= 0)
			{
				entity.StopBuff(PyreBuff);
				return;
			}

			if (!entity.TryGetBuff(PyreBuff, out var buff))
			{
				entity.StartBuff(PyreBuff, 1, 0f, TimeSpan.Zero, entity, SkillId.Zealot_EmphasisTrust);
				entity.TryGetBuff(PyreBuff, out buff);
			}

			if (buff == null || buff.OverbuffCounter == lashes)
				return;

			buff.OverbuffCounter = lashes;
			buff.NotifyUpdate();
		}



		/// <summary>
		/// The damage bonus the entity's current stage is worth.
		/// </summary>
		public static float GetStageBonus(ICombatEntity entity)
			=> StageDamageBonus[GetStage(entity) - 1];

		/// <summary>
		/// The bonus of a given stage, as a percentage — for tooltips.
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

	}
}
