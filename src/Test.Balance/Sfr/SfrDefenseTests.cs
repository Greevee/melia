using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Melia.Zone;
using Xunit;
using Xunit.Abstractions;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// Diagnostic runs for SfrDefenseProbe. Not wired into SfrPricer yet - this
	/// is the fast loop for checking the measurement itself reads sensibly
	/// before any pricing formula is built on top of it.
	/// </summary>
	/// <remarks>
	/// Opt-in: each named skill costs two full encounter windows
	/// (SfrDials.EncounterWindowMs apiece) in wall-clock time.
	/// </remarks>
	[Collection(BalanceCollection.Name)]
	public class SfrDefenseTests
	{
		/// <summary>
		/// Environment variable that enables the run.
		/// </summary>
		public const string EnableVariable = "BALANCE_DEFENSE";

		/// <summary>
		/// Environment variable naming the skills to measure.
		/// </summary>
		public const string SkillsVariable = "BALANCE_DEFENSE_SKILLS";

		/// <summary>
		/// Report the run leaves behind.
		/// </summary>
		public const string ReportName = "sfr-defense.md";

		/// <summary>
		/// A spread of skills with an obvious defensive or crowd-control
		/// payload, to sanity-check the measurement finds one where it is
		/// known to exist.
		/// </summary>
		private static readonly string[] DefaultSkills =
		[
			"Peltasta_Langort",
			"Swordman_Bash",
			"Highlander_CrossCut",
		];

		private readonly ITestOutputHelper _output;
		private readonly List<string> _lines = [];

		public SfrDefenseTests(BalanceHost host, ITestOutputHelper output)
			=> _output = output;

		private static bool Enabled => Environment.GetEnvironmentVariable(EnableVariable) == "1";

		/// <summary>
		/// Runs the control/treatment pair for each named skill and reports
		/// what it bought in avoided damage.
		/// </summary>
		[Fact]
		public void DefensiveValue()
		{
			if (!Enabled)
			{
				_output.WriteLine($"Skipped. Set {EnableVariable}=1 to run.");
				return;
			}

			var named = Environment.GetEnvironmentVariable(SkillsVariable);
			var skills = string.IsNullOrWhiteSpace(named)
				? DefaultSkills
				: named.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			Write("# Defensive / crowd-control value");
			Write("");
			Write("Control is a mob swinging freely at an idle character for the window; treatment is the");
			Write("same window after one press of the named skill. The gap is what the skill bought.");
			Write("");

			var measuredAny = false;

			foreach (var skillName in skills)
			{
				if (!ZoneServer.Instance.Data.SkillDb.TryFind(skillName, out var data))
				{
					Write($"{skillName,-28}unknown skill");
					continue;
				}

				var prefix = SfrData.ClassOf(skillName);

				if (!JobCatalog.TryGet(prefix, out var job))
				{
					Write($"{skillName,-28}'{prefix}' is not in the job catalog");
					continue;
				}

				var level = SfrData.SkillMaxLevel(skillName);
				var charLevel = ScenarioMatrix.CharacterLevelsFor(job, [50]).FirstOrDefault(50);

				var result = SfrDefenseProbe.Measure(job, data.Id, level, charLevel);

				measuredAny |= result.Error == null;
				Write($"{skillName,-28}{result}");
			}

			_output.WriteLine("report saved to " + SaveReport());

			Assert.True(measuredAny, "no skill could be measured at all");
		}

		private void Write(string line = "")
		{
			_output.WriteLine(line);
			_lines.Add(line);
		}

		private string SaveReport()
		{
			var directory = Path.Combine(SfrData.Root, SweepReport.OutputDirectory);

			Directory.CreateDirectory(directory);

			var path = Path.Combine(directory, ReportName);

			File.WriteAllText(path, string.Join(Environment.NewLine, _lines), Encoding.UTF8);

			return Path.GetFullPath(path);
		}
	}
}
