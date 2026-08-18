using System;
using System.Threading;
using Yggdrasil.Util;

namespace Melia.Shared.Util
{
	/// <summary>
	/// The random source everything that rolls should read, so a run can
	/// substitute a generator it seeded for the ambient one.
	/// </summary>
	/// <remarks>
	/// The server reads Yggdrasil's provider and nothing changes for it: with
	/// no generator installed <see cref="Get"/> forwards straight to
	/// <see cref="RandomProvider.Get"/>.
	///
	/// A harness that needs a press to replay identically installs one through
	/// <see cref="Use"/>. The current generator is <see cref="AsyncLocal{T}"/>,
	/// so it follows an async flow across awaits and continuations without any
	/// handler having to pass it along, and two flows running at once never see
	/// each other's. RandomProvider's own instance is [ThreadStatic] instead,
	/// which is the one thing a seeded measurement cannot use: a handler pacing
	/// itself with await resumes on whichever pool thread is free, and that
	/// thread's slot was never seeded.
	/// </remarks>
	public static class GameRandom
	{
		private static readonly AsyncLocal<Random> _current = new();

		/// <summary>
		/// The generator installed for the current async flow, or null when
		/// rolls come from the ambient provider.
		/// </summary>
		public static Random Current => _current.Value;

		/// <summary>
		/// Installs a generator for the current async flow and everything it
		/// starts. Null returns the flow to the ambient provider.
		/// </summary>
		/// <param name="random"></param>
		public static void Use(Random random)
			=> _current.Value = random;

		/// <summary>
		/// The generator to roll against, installed or ambient.
		/// </summary>
		public static Random Get()
			=> _current.Value ?? RandomProvider.Get();
	}
}
