using System;
using Melia.Zone;
using Xunit;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Boots a headless ZoneServer once for the whole test run, so data,
	/// scripts and skill handlers are the real ones.
	/// </summary>
	public class BalanceHost
	{
		private static readonly object _bootLock = new();
		private static bool _booted;

		/// <summary>
		/// Gets the booted server instance.
		/// </summary>
		public ZoneServer Server => ZoneServer.Instance;

		public BalanceHost()
		{
			lock (_bootLock)
			{
				if (_booted)
					return;

				// NavigateToRoot walks up to the repo root, so the harness
				// finds system/ and packages/ from the test bin folder.
				ZoneServer.Instance.RunHeadless();
				_booted = true;
			}
		}
	}

	/// <summary>
	/// Collection definition that shares a single booted server across
	/// every balance test class.
	/// </summary>
	[CollectionDefinition(Name)]
	public class BalanceCollection : ICollectionFixture<BalanceHost>
	{
		public const string Name = "Balance";
	}
}
