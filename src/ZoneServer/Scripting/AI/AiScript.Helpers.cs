using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.World;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Monsters;
using Melia.Shared.Util;

namespace Melia.Zone.Scripting.AI
{
	public abstract partial class AiScript
	{
		private readonly Dictionary<string, DateTime> _actionTimers = new();
		private int _switchRandomRoll;
		private int _switchRandomBand;

		/// <summary>
		/// Waits for a random amount of time between the two bounds.
		/// </summary>
		/// <remarks>
		/// The base implementation paces itself on the wall clock and draws
		/// its duration from a generator of its own, so an AI's timing is
		/// neither on the game's clock nor on its random source. These read
		/// both, which is the same wall clock and the same rolls on a running
		/// server, and is what lets a fight replay identically off one seed.
		/// </remarks>
		/// <param name="min"></param>
		/// <param name="max"></param>
		protected override IEnumerable Wait(int min, int max = 0)
		{
			var duration = max > min ? GameRandom.Get().Next(min, max + 1) : min;

			return this.Wait(TimeSpan.FromMilliseconds(duration));
		}

		/// <summary>
		/// Waits for the given amount of time.
		/// </summary>
		/// <param name="timeSpan"></param>
		protected override IEnumerable Wait(TimeSpan timeSpan)
		{
			var end = GameClock.Now + timeSpan;

			this.IsWaiting = true;

			while (GameClock.Now < end)
				yield return true;

			this.IsWaiting = false;
		}

		/// <summary>
		/// Executes the routine until it returns or the given time has passed.
		/// </summary>
		/// <param name="timeout"></param>
		/// <param name="routine"></param>
		protected new IEnumerable Timeout(int timeout, IEnumerable routine)
			=> this.Timeout(TimeSpan.FromMilliseconds(timeout), routine);

		/// <summary>
		/// Executes the routine until it returns or the given time has passed.
		/// </summary>
		/// <param name="timeout"></param>
		/// <param name="routine"></param>
		protected new IEnumerable Timeout(TimeSpan timeout, IEnumerable routine)
		{
			var end = GameClock.Now + timeout;

			foreach (var step in routine)
			{
				if (GameClock.Now >= end)
					yield break;

				yield return step;
			}
		}

		/// <summary>
		/// Returns true if the given percentage chance was met.
		/// </summary>
		/// <param name="percent"></param>
		protected new bool Chance(float percent)
			=> GameRandom.Get().NextDouble() < percent / 100f;

		/// <summary>
		/// Returns a random number between 0 and max-1.
		/// </summary>
		/// <param name="max"></param>
		protected new int Random(int max)
			=> GameRandom.Get().Next(max);

		/// <summary>
		/// Returns a random number between min and max-1.
		/// </summary>
		/// <param name="min"></param>
		/// <param name="max"></param>
		protected new int Random(int min, int max)
			=> GameRandom.Get().Next(min, max);

		/// <summary>
		/// Returns a random element from the given list.
		/// </summary>
		/// <param name="values"></param>
		protected new TValue RandomValue<TValue>(params TValue[] values)
			=> values[GameRandom.Get().Next(values.Length)];

		/// <summary>
		/// Rolls the number the following Case calls are matched against.
		/// </summary>
		/// <param name="max"></param>
		protected new void SwitchRandom(int max = 100)
		{
			_switchRandomRoll = GameRandom.Get().Next(max);
			_switchRandomBand = 0;
		}

		/// <summary>
		/// Returns true if the roll SwitchRandom made falls into this case's
		/// share of the range.
		/// </summary>
		/// <param name="value"></param>
		protected new bool Case(int value)
		{
			_switchRandomBand += value;

			return _switchRandomRoll < _switchRandomBand;
		}

		/// <summary>
		/// Checks if a named action is ready to be performed, based on a cooldown.
		/// If it is ready, the timer is reset.
		/// Useful for implementing cooldown-based action gating.
		/// </summary>
		/// <param name="actionName">A unique name for the action's timer.</param>
		/// <param name="cooldown">The duration to wait before this action can be performed again.</param>
		/// <returns>True if the action can be performed, false otherwise.</returns>
		protected bool IsActionReady(string actionName, TimeSpan cooldown)
		{
			var now = GameClock.Now;
			if (_actionTimers.TryGetValue(actionName, out var readyTime))
			{
				if (now >= readyTime)
				{
					_actionTimers[actionName] = now + cooldown;
					return true;
				}
				return false;
			}
			else
			{
				// First time, it's always ready.
				_actionTimers[actionName] = now + cooldown;
				return true;
			}
		}

		/// <summary>
		/// Gets the nearest hostile entity within a given range.
		/// </summary>
		/// <param name="range">The search range.</param>
		/// <returns>The nearest ICombatEntity, or null if none are found.</returns>
		protected ICombatEntity GetNearestEnemy(float range)
		{
			_nearbyEnemiesBuffer.Clear();
			this.Entity.Map.GetAttackableEnemiesInPosition(
				this.Entity, this.Entity.Position, range, _nearbyEnemiesBuffer);

			ICombatEntity nearest = null;
			var nearestDistSq = float.MaxValue;
			var pos = this.Entity.Position;

			for (var i = 0; i < _nearbyEnemiesBuffer.Count; i++)
			{
				var e = _nearbyEnemiesBuffer[i];
				if (this.EntityGone(e))
					continue;

				var dx = e.Position.X - pos.X;
				var dz = e.Position.Z - pos.Z;
				var distSq = dx * dx + dz * dz;

				if (distSq < nearestDistSq)
				{
					nearestDistSq = distSq;
					nearest = e;
				}
			}

			return nearest;
		}

		/// <summary>
		/// Gets the farthest hostile entity within a given range.
		/// </summary>
		/// <param name="range">The search range.</param>
		/// <returns>The farthest ICombatEntity, or null if none are found.</returns>
		protected ICombatEntity GetFarthestEnemy(float range)
		{
			_nearbyEnemiesBuffer.Clear();
			this.Entity.Map.GetAttackableEnemiesInPosition(
				this.Entity, this.Entity.Position, range, _nearbyEnemiesBuffer);

			ICombatEntity farthest = null;
			var farthestDistSq = -1f;
			var pos = this.Entity.Position;

			for (var i = 0; i < _nearbyEnemiesBuffer.Count; i++)
			{
				var e = _nearbyEnemiesBuffer[i];
				if (this.EntityGone(e))
					continue;

				var dx = e.Position.X - pos.X;
				var dz = e.Position.Z - pos.Z;
				var distSq = dx * dx + dz * dz;

				if (distSq > farthestDistSq)
				{
					farthestDistSq = distSq;
					farthest = e;
				}
			}

			return farthest;
		}

		/// <summary>
		/// Checks if the entity's HP is below a certain percentage.
		/// </summary>
		/// <param name="percent">The percentage threshold (e.g., 0.5 for 50%).</param>
		/// <returns>True if HP is at or below the threshold.</returns>
		protected bool IsHpBelow(float percent)
		{
			if (this.Entity.MaxHp == 0) return false;
			return (this.Entity.Hp / (float)this.Entity.MaxHp) <= percent;
		}

		/// <summary>
		/// Checks if the entity is near its original spawn position.
		/// </summary>
		/// <param name="range">The distance to check against.</param>
		/// <returns>True if the entity is within the specified range of its spawn point.</returns>
		protected bool IsNearSpawnPosition(float range)
		{
			if (this.Entity is not IMonster monster || monster.SpawnPosition == Position.Zero)
			{
				return true; // Not a monster or no spawn position, so it's not "far"
			}
			return monster.Position.InRange2D(monster.SpawnPosition, range);
		}
	}
}
