using System;
using System.Reflection;
using Yggdrasil.Util;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Pins Yggdrasil's RandomProvider to a known seed so combat samples are
	/// reproducible. RandomProvider exposes no seed setter, but its per-thread
	/// instance field is not readonly and Get() returns it when already set,
	/// so assigning it is enough to control every RandomProvider.Get() caller
	/// on this thread.
	/// </summary>
	public static class DeterministicRandom
	{
		private const string ThreadInstanceField = "_random";
		private const string SharedInstanceField = "_rnd";

		private static readonly FieldInfo _threadInstance = typeof(RandomProvider)
			.GetField(ThreadInstanceField, BindingFlags.NonPublic | BindingFlags.Static);

		private static readonly FieldInfo _sharedInstance = typeof(RandomProvider)
			.GetField(SharedInstanceField, BindingFlags.NonPublic | BindingFlags.Static);

		/// <summary>
		/// Gets whether the shared instance used by the static
		/// RandomProvider.Next helpers could also be pinned. It is a static
		/// readonly field, which some runtimes refuse to set by reflection.
		/// </summary>
		public static bool SharedInstancePinned { get; private set; }

		/// <summary>
		/// Seeds the calling thread's RandomProvider instance. Must be called
		/// on the same thread that runs the measurement, and the measurement
		/// must not hop threads.
		/// </summary>
		/// <param name="seed"></param>
		public static void Seed(int seed)
		{
			if (_threadInstance == null)
			{
				throw new InvalidOperationException(
					$"RandomProvider.{ThreadInstanceField} not found - the Yggdrasil version changed " +
					"and the balance harness can no longer control the seed.");
			}

			_threadInstance.SetValue(null, new Random(seed));

			// Best effort: RandomProvider.Next/NextDouble read a separate
			// static readonly instance, which reflection may not be able
			// to set. Those call sites are outside the damage pipeline.
			SharedInstancePinned = false;

			if (_sharedInstance == null)
				return;

			try
			{
				_sharedInstance.SetValue(null, new Random(seed));
				SharedInstancePinned = true;
			}
			catch (Exception)
			{
				// Left unpinned; callers can check SharedInstancePinned.
			}
		}

		/// <summary>
		/// Clears the pinned instance so the thread returns to normal
		/// unseeded behaviour.
		/// </summary>
		public static void Reset()
		{
			_threadInstance?.SetValue(null, null);
		}
	}
}
