using System;

namespace Melia.Test.Balance
{
	/// <summary>
	/// The opt-in gates that keep the balance suites from running together.
	/// </summary>
	/// <remarks>
	/// Both suites boot a headless ZoneServer and measure over encounter
	/// windows, so an unfiltered run costs the sum of the two for a result
	/// nobody asked for. One variable per suite, and neither is set by default.
	/// </remarks>
	public static class BalanceSuites
	{
		public const string SfrVariable = "BALANCE_SFR";
		public const string BuffVariable = "BALANCE_BUFF";

		/// <summary>
		/// Returns whether the SFR suite was asked for.
		/// </summary>
		public static bool SfrEnabled => Environment.GetEnvironmentVariable(SfrVariable) == "1";

		/// <summary>
		/// Returns whether the buff suite was asked for.
		/// </summary>
		public static bool BuffEnabled => Environment.GetEnvironmentVariable(BuffVariable) == "1";

		/// <summary>
		/// Returns the line a skipped suite reports instead of running.
		/// </summary>
		/// <param name="variable"></param>
		public static string SkipMessage(string variable)
			=> $"Skipped. Set {variable}=1 to run this suite.";
	}
}
