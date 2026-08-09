using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Melia.Shared.Util
{
	/// <summary>
	/// The time source everything that paces itself should read, so a run can
	/// substitute a clock it controls for the wall clock.
	/// </summary>
	/// <remarks>
	/// The server reads the wall clock and nothing changes for it: with no
	/// clock installed every member here forwards straight to
	/// <see cref="DateTime.UtcNow"/> and <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.
	///
	/// A harness that needs a press to replay identically installs one through
	/// <see cref="Use"/>. The current clock is <see cref="AsyncLocal{T}"/>, so
	/// it follows an async flow across awaits and continuations without any
	/// handler having to pass it along, and two flows running at once never see
	/// each other's.
	/// </remarks>
	public static class GameClock
	{
		private static readonly AsyncLocal<VirtualClock> _current = new();

		/// <summary>
		/// The clock installed for the current async flow, or null when time is
		/// the wall clock.
		/// </summary>
		public static VirtualClock Current => _current.Value;

		/// <summary>
		/// Installs a clock for the current async flow and everything it starts.
		/// </summary>
		/// <param name="clock"></param>
		public static void Use(VirtualClock clock)
			=> _current.Value = clock;

		/// <summary>
		/// The current time, virtual or real.
		/// </summary>
		public static DateTime Now
			=> _current.Value?.Now ?? DateTime.UtcNow;

		/// <summary>
		/// The current local time, virtual or real.
		/// </summary>
		/// <remarks>
		/// A separate member only so the wall-clock case keeps the exact
		/// semantics each call site already had - a stored DateTime.Now
		/// compared against a DateTime.UtcNow is wrong by the machine's
		/// offset, and swapping one for the other silently would be a
		/// production bug. Under a virtual clock both return the same virtual
		/// instant, which is what makes the comparison hold.
		/// </remarks>
		public static DateTime LocalNow
			=> _current.Value?.Now ?? DateTime.Now;

		/// <summary>
		/// Returns a task that completes once the given time has passed on
		/// whichever clock is current.
		/// </summary>
		/// <param name="time"></param>
		/// <param name="cancellationToken"></param>
		public static Task Delay(TimeSpan time, CancellationToken cancellationToken = default)
		{
			var clock = _current.Value;

			if (clock == null)
				return Task.Delay(time, cancellationToken);

			return clock.Delay(time, cancellationToken);
		}

		/// <summary>
		/// Returns a task that completes once the given milliseconds have
		/// passed on whichever clock is current.
		/// </summary>
		/// <param name="milliseconds"></param>
		/// <param name="cancellationToken"></param>
		public static Task Delay(int milliseconds, CancellationToken cancellationToken = default)
			=> Delay(TimeSpan.FromMilliseconds(milliseconds), cancellationToken);
	}

	/// <summary>
	/// A clock that only moves when it is told to, so work paced against it
	/// happens at the same point every run.
	/// </summary>
	/// <remarks>
	/// Waiters are completed inline on the thread calling <see cref="Advance"/>,
	/// which is what makes a flow deterministic rather than merely virtual: the
	/// continuation due at a given moment runs to its next await before Advance
	/// returns, so a whole press executes on one thread in one order regardless
	/// of how loaded the machine is. Completing them on the thread pool instead
	/// would reintroduce exactly the scheduling race the clock exists to remove.
	/// </remarks>
	public sealed class VirtualClock
	{
		private readonly object _syncLock = new();
		private readonly List<Waiter> _waiters = [];
		private DateTime _now;

		/// <summary>
		/// The clock's current time.
		/// </summary>
		public DateTime Now
		{
			get
			{
				lock (_syncLock)
					return _now;
			}
		}

		/// <summary>
		/// How much time the clock has been advanced by in total.
		/// </summary>
		public TimeSpan Elapsed { get; private set; }

		/// <summary>
		/// Waiters parked on the clock right now.
		/// </summary>
		public int Pending
		{
			get
			{
				lock (_syncLock)
					return _waiters.Count;
			}
		}

		/// <summary>
		/// Creates a clock starting at the given time.
		/// </summary>
		/// <param name="start"></param>
		public VirtualClock(DateTime? start = null)
			=> _now = start ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		/// <summary>
		/// Returns a task that completes once the clock has advanced past the
		/// given span.
		/// </summary>
		/// <param name="time"></param>
		/// <param name="cancellationToken"></param>
		public Task Delay(TimeSpan time, CancellationToken cancellationToken)
		{
			if (time <= TimeSpan.Zero)
				return Task.CompletedTask;

			if (cancellationToken.IsCancellationRequested)
				return Task.FromCanceled(cancellationToken);

			var waiter = new Waiter(_now + time, cancellationToken);

			lock (_syncLock)
				_waiters.Add(waiter);

			return waiter.Task;
		}

		/// <summary>
		/// Moves the clock forward and runs everything that comes due, to its
		/// next await, before returning.
		/// </summary>
		/// <remarks>
		/// Loops rather than releasing once, because a continuation woken here
		/// commonly parks again on a shorter delay that is also already due -
		/// a handler pacing a volley at 100 ms intervals inside a 250 ms step.
		/// Draining until nothing more is due is what keeps the step size from
		/// changing what a press delivers.
		/// </remarks>
		/// <param name="time"></param>
		public void Advance(TimeSpan time)
		{
			lock (_syncLock)
				_now += time;

			this.Elapsed += time;

			this.Release();
		}

		/// <summary>
		/// Completes every waiter that is due, repeatedly, until none is.
		/// </summary>
		private void Release()
		{
			// Bounded, because a continuation that parks on a zero-length delay
			// and wakes itself would otherwise spin here forever, and a
			// measurement hanging is worse than one coming out short.
			for (var pass = 0; pass < MaxReleasePasses; ++pass)
			{
				var due = new List<Waiter>();

				lock (_syncLock)
				{
					for (var i = _waiters.Count - 1; i >= 0; --i)
					{
						if (_waiters[i].Due > _now)
							continue;

						due.Add(_waiters[i]);
						_waiters.RemoveAt(i);
					}
				}

				if (due.Count == 0)
					return;

				// Ordered by when they came due, so two continuations parked on
				// different delays resolve in the order the delays say rather
				// than in list order.
				due.Sort((a, b) => a.Due.CompareTo(b.Due));

				foreach (var waiter in due)
					waiter.Complete();
			}
		}

		/// <summary>
		/// How many times Advance re-checks for newly due waiters before giving
		/// up on the step settling.
		/// </summary>
		private const int MaxReleasePasses = 1000;

		/// <summary>
		/// One parked continuation.
		/// </summary>
		private sealed class Waiter
		{
			private readonly TaskCompletionSource<bool> _source;
			private readonly CancellationToken _cancellationToken;

			/// <summary>
			/// The clock time this waiter comes due at.
			/// </summary>
			public DateTime Due { get; }

			/// <summary>
			/// The task the caller awaits.
			/// </summary>
			public Task Task => _source.Task;

			/// <summary>
			/// Creates a waiter due at the given time.
			/// </summary>
			/// <remarks>
			/// Deliberately without RunContinuationsAsynchronously: the
			/// continuation has to run on the advancing thread for the flow to
			/// be single-threaded and therefore reproducible.
			/// </remarks>
			/// <param name="due"></param>
			/// <param name="cancellationToken"></param>
			public Waiter(DateTime due, CancellationToken cancellationToken)
			{
				this.Due = due;

				_source = new TaskCompletionSource<bool>();
				_cancellationToken = cancellationToken;
			}

			/// <summary>
			/// Releases whatever is waiting on this, cancelled or not.
			/// </summary>
			public void Complete()
			{
				if (_cancellationToken.IsCancellationRequested)
					_source.TrySetCanceled(_cancellationToken);
				else
					_source.TrySetResult(true);
			}
		}
	}
}
