using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Melia.Shared.Game.Const;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Rebuilds a sweep's results from the CSVs it wrote, so the report can be
	/// regenerated and retuned without paying for another twenty-minute run.
	/// </summary>
	public static class SweepArchive
	{
		/// <summary>
		/// Reads the skill matrix back.
		/// </summary>
		public static SkillProfile[] ReadProfiles()
		{
			return SweepReport.Read("skill-matrix")
				.Where(r => string.IsNullOrEmpty(Text(r, "error")))
				.Select(r => new SkillProfile
				{
					JobPrefix = Text(r, "class"),
					SkillClassName = Text(r, "skill"),
					Role = Enum.TryParse<SkillRole>(Text(r, "role"), out var role) ? role : SkillRole.Direct,
					ScenarioId = Text(r, "scenario"),
					CharacterLevel = Int(r, "charLevel"),
					SkillLevel = Int(r, "skillLevel"),
					MobLevel = Int(r, "mobLevel"),
					TargetsReached = Int(r, "targets"),
					DamagePerCast = Float(r, "damagePerCast"),
					BasicAttackPerCast = Float(r, "basicAttackPerCast"),
					BasicCastsPerSecond = Float(r, "basicCastsPerSecond"),
					BasicTargetsReached = Int(r, "basicTargets"),
					HitsPerCast = Int(r, "hitsPerCast"),
					PrimaryStat = Text(r, "primaryStat"),
					ReferenceDamagePerCast = Float(r, "referenceDamagePerCast"),
					TimesBasic = Float(r, "timesBasic"),
					TimesReference = Float(r, "timesReference"),
					CastsPerSecond = Float(r, "castsPerSecond"),
					SpPerSecond = Float(r, "spPerSecond"),
					CastsToKill = Float(r, "castsToKill"),
					DodgeRate = Float(r, "dodgeRate"),
					SpSustainable = Bool(r, "spSustainable"),
					Zero = Enum.TryParse<ZeroReason>(Text(r, "zeroReason"), out var zero) ? zero : ZeroReason.None,
				})
				.ToArray();
		}

		/// <summary>
		/// Reads the encounter sweep back.
		/// </summary>
		public static EncounterResult[] ReadEncounters()
		{
			return SweepReport.Read("encounters")
				.Select(r => new EncounterResult
				{
					JobPrefix = Text(r, "class"),
					SkillClassName = Text(r, "skill"),
					ScenarioId = Text(r, "scenario"),
					CharacterLevel = Int(r, "charLevel"),
					SkillLevel = Int(r, "skillLevel"),
					EnemyCount = Int(r, "enemies"),
					Casts = Int(r, "casts"),
					Seconds = Float(r, "seconds"),
					TotalDamage = Float(r, "totalDamage"),
					PadsCreated = Int(r, "pads"),
					SummonsCreated = Int(r, "summons"),
					EnemyDied = Bool(r, "enemyDied"),
					Error = string.IsNullOrEmpty(Text(r, "error")) ? null : Text(r, "error"),
				})
				.ToArray();
		}

		/// <summary>
		/// Reads the buff sweep back.
		/// </summary>
		public static BuffEffect[] ReadBuffs()
		{
			return SweepReport.Read("buffs")
				.Select(r => new BuffEffect
				{
					Id = Enum.TryParse<BuffId>(Text(r, "buff"), out var id) ? id : default,
					Owner = Text(r, "owner"),
					Slot = Enum.TryParse<BuffSlot>(Text(r, "slot"), out var slot) ? slot : default,
					Level = Int(r, "level"),
					CasterInt = Int(r, "casterInt"),
					Baseline = Float(r, "baseline"),
					Buffed = Float(r, "buffed"),
					PropertyDeltas = Text(r, "propertyDeltas"),
					Error = string.IsNullOrEmpty(Text(r, "error")) ? null : Text(r, "error"),
				})
				.ToArray();
		}

		private static string Text(Dictionary<string, string> row, string key)
			=> row.TryGetValue(key, out var value) ? value : "";

		private static int Int(Dictionary<string, string> row, string key)
			=> (int)Float(row, key);

		private static float Float(Dictionary<string, string> row, string key)
			=> float.TryParse(Text(row, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

		private static bool Bool(Dictionary<string, string> row, string key)
			=> bool.TryParse(Text(row, key), out var value) && value;
	}
}
