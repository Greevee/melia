using System;
using System.Reflection;
using Melia.Shared.Util;
using Yggdrasil.Util;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Pins the random source a measurement rolls against to a known seed, so
	/// combat samples are reproducible.
	/// </summary>
	/// <remarks>
	/// The seed is installed on GameRandom, whose instance is AsyncLocal and
	/// therefore follows the measurement across every await, continuation and
	/// Task.Run it makes - the hop that a thread-static seed silently loses.
	/// Yggdrasil's own thread-static instance is seeded alongside it as a
	/// backstop for any roll that still reaches RandomProvider directly, but it
	/// only covers the calling thread and is not what the model relies on.
	///
	/// The process-wide instance behind the static RandomProvider.Next helpers
	/// is deliberately left alone: it is shared by every thread, so seeding it
	/// from one measurement is a write the other measurements running at that
	/// moment can see, which is a source of drift rather than a guard against
	/// one. Those call sites read GameRandom now.
	/// </remarks>
	public static class DeterministicRandom
	{
		private const string ThreadInstanceField = "_random";

		private static readonly FieldInfo _threadInstance = typeof(RandomProvider)
			.GetField(ThreadInstanceField, BindingFlags.NonPublic | BindingFlags.Static);

		/// <summary>
		/// Seeds the random source for the calling async flow, and everything
		/// that flow goes on to start.
		/// </summary>
		/// <param name="seed"></param>
		public static void Seed(int seed)
		{
			GameRandom.Use(new Random(seed));

			_threadInstance?.SetValue(null, new Random(seed));
		}

		/// <summary>
		/// Reseeds the flow's generator from the window's seed and its position
		/// in the window, so two matched windows draw the same numbers at the
		/// same moment however many rolls each has made by then.
		/// </summary>
		/// <remarks>
		/// Common random numbers, applied per tick rather than once per pair. A
		/// control and its treatment start from one seed, and the press is what
		/// separates them - but a press that rolls anything advances the shared
		/// stream, so from that moment on the mob's swing timing, the damage
		/// roll and every other draw differ between the two halves for reasons
		/// that have nothing to do with what the press bought. Realigning each
		/// tick bounds that divergence to the tick it happens in.
		///
		/// The seed is hashed rather than added: adjacent seeds hand the legacy
		/// generator near-identical opening draws, which would correlate one
		/// tick's rolls with the next's. The hash is Mix rather than
		/// HashCode.Combine, whose seed is randomized once per process - every
		/// tick of every window drew from a different stream in each run of the
		/// harness, which is exactly the run-to-run drift this exists to stop.
		///
		/// Only GameRandom is moved here, not the thread-static backstop - this
		/// runs on every tick of every window, and the reflection write is the
		/// one part of Seed that is not cheap.
		/// </remarks>
		/// <param name="seed"></param>
		/// <param name="elapsedMs"></param>
		public static void Realign(int seed, int elapsedMs)
		{
			if (!RealignEnabled)
				return;

			GameRandom.Use(new Random(Mix(seed, elapsedMs)));
		}

		/// <summary>
		/// Folds two numbers into one, identically in every process.
		/// </summary>
		/// <param name="seed"></param>
		/// <param name="elapsedMs"></param>
		private static int Mix(int seed, int elapsedMs)
		{
			unchecked
			{
				var value = ((ulong)(uint)seed << 32) | (uint)elapsedMs;

				value ^= value >> 33;
				value *= 0xFF51AFD7ED558CCDUL;
				value ^= value >> 33;
				value *= 0xC4CEB9FE1A85EC53UL;
				value ^= value >> 33;

				return (int)value;
			}
		}

		/// <summary>
		/// Environment variable that turns the per-tick realignment off.
		/// </summary>
		public const string NoRealignVariable = "BALANCE_NO_REALIGN";

		/// <summary>
		/// Whether Realign does anything.
		/// </summary>
		/// <remarks>
		/// A switch rather than a constant because the realignment has a known
		/// limit and no measurement yet says which side of it is better. An
		/// AsyncLocal write reaches the flow that makes it and every flow that
		/// flow goes on to start - but a continuation resuming from an await
		/// restores the context it captured, so a handler that parked two ticks
		/// ago keeps drawing from the generator that was current then, not the
		/// one installed since. The tick loop and a handler's own pacing
		/// therefore draw from different generators, which is reproducible but
		/// is not the single shared stream the pairing assumes. Set the variable
		/// to 1 to fall back to one seeded generator per window and compare.
		/// </remarks>
		public static bool RealignEnabled { get; }
			= Environment.GetEnvironmentVariable(NoRealignVariable) != "1";

		/// <summary>
		/// Clears the pinned generator so rolls return to the ambient provider.
		/// </summary>
		public static void Reset()
		{
			GameRandom.Use(null);

			_threadInstance?.SetValue(null, null);
		}
	}
}
