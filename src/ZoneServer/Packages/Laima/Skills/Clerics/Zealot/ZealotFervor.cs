using System;
using Melia.Shared.Game.Const;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Fervor, the Zealot class resource.
	/// Immolation generates one stack per second while it burns; the other
	/// Zealot skills spend them. Kept in one place so every skill agrees on
	/// how stacks are counted and consumed.
	/// </summary>
	/// <remarks>
	/// Rides on the unused BrambleRage buff ("Lunacy"), which is already
	/// permanent and, via buffs_overrides, capped at 20 stacks, showing a
	/// stack count on the buff icon.
	/// </remarks>
	public static class ZealotFervor
	{
		/// <summary>
		/// The buff carrying the stacks.
		/// </summary>
		public const BuffId FervorBuff = BuffId.BrambleRage;

		/// <summary>
		/// Matches overBuff in the buff db entry. Twenty seconds of burning
		/// fills the bar.
		/// </summary>
		public const int MaxStacks = 20;

		/// <summary>
		/// Returns how many stacks the entity currently holds, 0 if none.
		/// </summary>
		public static int GetStacks(ICombatEntity entity)
		{
			if (!entity.TryGetBuff(FervorBuff, out var buff))
				return 0;

			// An active buff is worth at least one stack, regardless of where
			// the overbuff counter starts.
			return Math.Max(1, Math.Min(MaxStacks, buff.OverbuffCounter));
		}

		/// <summary>
		/// Adds a single stack, starting the buff if it is not running yet.
		/// </summary>
		/// <remarks>
		/// The stack count only reaches the client through a buff packet —
		/// raising OverbuffCounter alone changes nothing on screen. Frenzy,
		/// the reference implementation for stacking buffs, uses
		/// Buff.NotifyUpdate() for this, so we do the same.
		/// </remarks>
		public static void AddStack(ICombatEntity entity, SkillId skillId)
		{
			if (entity.TryGetBuff(FervorBuff, out var buff))
			{
				buff.IncreaseOverbuff();
				buff.NotifyUpdate();
				return;
			}

			// The db entry has duration 0, so the buff persists until spent.
			entity.StartBuff(FervorBuff, 1, 0f, TimeSpan.Zero, entity, skillId);

			// Start the counter at one rather than zero, so the very first
			// second of Immolation already reads as a stack.
			if (entity.TryGetBuff(FervorBuff, out var started))
			{
				started.OverbuffCounter = 1;
				started.NotifyUpdate();
			}
		}

		/// <summary>
		/// Spends every stack and returns how many there were, so a skill can
		/// scale itself by what it just consumed.
		/// </summary>
		public static int ConsumeAll(ICombatEntity entity)
		{
			var stacks = GetStacks(entity);

			if (stacks > 0)
				entity.StopBuff(FervorBuff);

			return stacks;
		}
	}
}
