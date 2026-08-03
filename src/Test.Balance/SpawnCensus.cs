using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Zone;
using Melia.Zone.World.Spawning;

namespace Melia.Test.Balance
{
	/// <summary>
	/// A monster that actually spawns somewhere a player can reach, together
	/// with where it spawns and how many of it stand there.
	/// </summary>
	public class CensusMob
	{
		public MonsterData Data { get; init; }

		/// <summary>
		/// Available maps this monster spawns on.
		/// </summary>
		public string[] Maps { get; init; }

		/// <summary>
		/// Summed max population across every spawner for this monster on
		/// those maps, which is how common it is in practice.
		/// </summary>
		public int Population { get; init; }

		public override string ToString()
			=> $"{this.Data.ClassName} lv{this.Data.Level} {this.Data.Rank} x{this.Population} on {this.Maps.Length} map(s)";
	}

	/// <summary>
	/// One spawn point, reduced to the numbers the density measurement needs.
	/// </summary>
	internal class CensusPoint
	{
		public string Map { get; init; }
		public float X { get; init; }
		public float Z { get; init; }
		public int Level { get; init; }

		/// <summary>
		/// Expected monsters standing here, which is the spawner's max
		/// population divided over its spawn points.
		/// </summary>
		public float Weight { get; init; }
	}

	/// <summary>
	/// Reads the live spawner registry and restricts it to the maps in
	/// available_maps.md, so every scenario fights something reachable.
	/// </summary>
	/// <remarks>
	/// This is the runtime counterpart to sim.py's spawn scanner: it reads
	/// the same spawners the server does rather than re-parsing the scripts,
	/// so it cannot drift from what actually spawns.
	/// </remarks>
	public static class SpawnCensus
	{
		/// <summary>
		/// Ranks a scenario may fight. Material, NPC and the rest are props,
		/// not content.
		/// </summary>
		private static readonly MonsterRank[] CombatRanks =
			[MonsterRank.Normal, MonsterRank.Special, MonsterRank.Elite, MonsterRank.Boss];

		/// <summary>
		/// Ranks the density measurement counts. Elites and bosses stand
		/// alone, so including them would inflate what an AoE skill reaches.
		/// </summary>
		private static readonly MonsterRank[] DensityRanks = [MonsterRank.Normal, MonsterRank.Special];

		private static readonly object _buildLock = new();
		private static readonly Dictionary<(float, int, int), float> _densityCache = new();
		private static CensusMob[] _mobs;
		private static CensusPoint[] _points;

		/// <summary>
		/// Every monster that spawns on an available map.
		/// </summary>
		public static CensusMob[] Mobs
		{
			get { Build(); return _mobs; }
		}

		/// <summary>
		/// Returns the monsters of the given rank at the given level that
		/// spawn on an available map.
		/// </summary>
		/// <param name="level"></param>
		/// <param name="rank"></param>
		public static CensusMob[] Find(int level, MonsterRank rank)
			=> Mobs.Where(m => m.Data.Level == level && m.Data.Rank == rank).ToArray();

		/// <summary>
		/// Returns the median-HP monster of the given rank at or near the
		/// given level, so a scenario is not skewed by an outlier.
		/// </summary>
		/// <remarks>
		/// Levels are not densely populated on available maps, so the search
		/// widens outwards until it finds one. The chosen level is reported
		/// via actualLevel because it changes what the level gap really is.
		/// </remarks>
		/// <param name="level"></param>
		/// <param name="rank"></param>
		/// <param name="tolerance"></param>
		/// <param name="actualLevel"></param>
		public static MonsterData FindReferenceMob(int level, MonsterRank rank, int tolerance, out int actualLevel)
		{
			for (var offset = 0; offset <= tolerance; ++offset)
			{
				var candidateLevels = offset == 0
					? new[] { level }
					: new[] { level - offset, level + offset };

				foreach (var candidateLevel in candidateLevels)
				{
					var candidates = Find(candidateLevel, rank)
						.OrderBy(m => m.Data.Hp)
						.ToArray();

					if (candidates.Length == 0)
						continue;

					actualLevel = candidateLevel;

					return candidates[candidates.Length / 2].Data;
				}
			}

			throw new InvalidOperationException(
				$"No rank:\"{rank}\" monster within {tolerance} levels of {level} spawns on an available map.");
		}

		/// <summary>
		/// Returns the median-HP monster of the given rank at or near the
		/// given level.
		/// </summary>
		/// <param name="level"></param>
		/// <param name="rank"></param>
		/// <param name="tolerance"></param>
		public static MonsterData FindReferenceMob(int level, MonsterRank rank = MonsterRank.Normal, int tolerance = 5)
			=> FindReferenceMob(level, rank, tolerance, out _);

		/// <summary>
		/// Returns the p75 number of monsters standing within the given
		/// radius of a monster, for spawns in the given level band.
		/// </summary>
		/// <remarks>
		/// This is the EffTargets_low term from the plan: what a skill's
		/// geometry reaches with no gathering. Passing a band keeps the
		/// answer honest, since density does not rise with level and a
		/// whole-game average would hide that.
		/// </remarks>
		/// <param name="radius"></param>
		/// <param name="minLevel"></param>
		/// <param name="maxLevel"></param>
		public static float MeasureDensity(float radius, int minLevel = 1, int maxLevel = 999)
		{
			Build();

			// The scan is quadratic in points per map and the scenario matrix
			// asks for the same bands thousands of times, so it is memoised.
			lock (_densityCache)
			{
				if (_densityCache.TryGetValue((radius, minLevel, maxLevel), out var cached))
					return cached;
			}

			var result = ScanDensity(radius, minLevel, maxLevel);

			lock (_densityCache)
				_densityCache[(radius, minLevel, maxLevel)] = result;

			return result;
		}

		/// <summary>
		/// Walks the point cloud and returns the p75 count within radius.
		/// </summary>
		/// <param name="radius"></param>
		/// <param name="minLevel"></param>
		/// <param name="maxLevel"></param>
		private static float ScanDensity(float radius, int minLevel, int maxLevel)
		{
			var byMap = _points
				.Where(p => p.Level >= minLevel && p.Level <= maxLevel)
				.GroupBy(p => p.Map);

			var counts = new List<float>();

			foreach (var map in byMap)
			{
				var points = map.ToArray();

				foreach (var point in points)
				{
					var count = 0f;

					foreach (var other in points)
					{
						var dx = other.X - point.X;
						var dz = other.Z - point.Z;

						if (dx * dx + dz * dz <= radius * radius)
							count += other.Weight;
					}

					counts.Add(count);
				}
			}

			if (counts.Count == 0)
				return 0;

			counts.Sort();

			return counts[(int)Math.Min(counts.Count - 1, counts.Count * 0.75f)];
		}

		/// <summary>
		/// Returns the radius at which p75 density first reaches the given
		/// number of monsters, which is how far apart that many monsters
		/// really stand.
		/// </summary>
		/// <remarks>
		/// Scenario S2 places its monsters at this spacing rather than at an
		/// invented one, so "the ungathered case" means what the spawn data
		/// says it means.
		/// </remarks>
		/// <param name="count"></param>
		/// <param name="minLevel"></param>
		/// <param name="maxLevel"></param>
		/// <param name="maxRadius"></param>
		public static float RadiusForDensity(float count, int minLevel = 1, int maxLevel = 999, float maxRadius = 2000f)
		{
			for (var radius = 20f; radius <= maxRadius; radius += 20f)
			{
				if (MeasureDensity(radius, minLevel, maxLevel) >= count)
					return radius;
			}

			return maxRadius;
		}

		/// <summary>
		/// Returns the number of monsters a skill with the given splash
		/// geometry reaches passively, rounded to the nearest whole target
		/// with a floor of one.
		/// </summary>
		/// <param name="splashRange"></param>
		/// <param name="minLevel"></param>
		/// <param name="maxLevel"></param>
		public static int EffectiveTargets(float splashRange, int minLevel = 1, int maxLevel = 999)
		{
			var radius = Math.Max(1f, splashRange * 2);

			return Math.Max(1, (int)Math.Round(MeasureDensity(radius, minLevel, maxLevel)));
		}

		/// <summary>
		/// Walks the spawner registry once and caches both the eligible
		/// monster list and the spawn point cloud the density measurement
		/// runs over.
		/// </summary>
		private static void Build()
		{
			lock (_buildLock)
			{
				if (_mobs != null)
					return;

				var all = ZoneServer.Instance.World.GetSpawners();

				if (all.Length == 0)
					throw new InvalidOperationException("No spawners are registered - content scripts did not load.");

				var populations = new Dictionary<int, int>();
				var mapsByMonster = new Dictionary<int, HashSet<string>>();
				var points = new List<CensusPoint>();

				foreach (var spawner in all.OfType<MonsterSpawner>())
				{
					var data = spawner.MonsterData;

					if (data == null || !CombatRanks.Contains(data.Rank))
						continue;

					var areas = ResolveAreas(spawner);
					var maps = ResolveAvailableMaps(spawner, areas);
					if (maps.Length == 0)
						continue;

					Record(populations, mapsByMonster, data, spawner.MaxAmount, maps);
					AddPoints(spawner, data, areas, points);
				}

				// Field bosses and event monsters use a different spawner
				// that names one map directly and has no spawn areas, so
				// they contribute to the pool but not to density.
				foreach (var spawner in all.OfType<EventMonsterSpawner>())
				{
					var data = spawner.MonsterData;

					if (data == null || !CombatRanks.Contains(data.Rank))
						continue;

					if (!ZoneServer.Instance.World.TryGetMap(spawner.MapId, out var map))
						continue;

					if (!AvailableMaps.Contains(map.ClassName))
						continue;

					Record(populations, mapsByMonster, data, spawner.MaxAmount, [map.ClassName]);
				}

				_mobs = populations
					.Select(pair => new CensusMob
					{
						Data = ZoneServer.Instance.Data.MonsterDb.Entries[pair.Key],
						Maps = mapsByMonster[pair.Key].OrderBy(m => m).ToArray(),
						Population = pair.Value,
					})
					.OrderBy(m => m.Data.Level)
					.ToArray();

				_points = points.ToArray();
			}
		}

		/// <summary>
		/// Folds one spawner's contribution into the running totals.
		/// </summary>
		/// <param name="populations"></param>
		/// <param name="mapsByMonster"></param>
		/// <param name="data"></param>
		/// <param name="amount"></param>
		/// <param name="maps"></param>
		private static void Record(Dictionary<int, int> populations, Dictionary<int, HashSet<string>> mapsByMonster, MonsterData data, int amount, string[] maps)
		{
			populations.TryGetValue(data.Id, out var population);
			populations[data.Id] = population + amount;

			if (!mapsByMonster.TryGetValue(data.Id, out var monsterMaps))
				mapsByMonster[data.Id] = monsterMaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var map in maps)
				monsterMaps.Add(map);
		}

		/// <summary>
		/// Returns the spawner's spawn areas that sit on an available map.
		/// </summary>
		/// <remarks>
		/// Read from the spawn area collection rather than from
		/// MonsterSpawner.Maps, because that set is only filled in as
		/// monsters actually spawn - which never happens headless, since
		/// RunHeadless skips StartWorld.
		/// </remarks>
		/// <param name="spawner"></param>
		private static SpawnArea[] ResolveAreas(MonsterSpawner spawner)
		{
			if (spawner.SpawnPointsIdent == null)
				return [];

			if (!ZoneServer.Instance.World.TryGetSpawnAreas(spawner.SpawnPointsIdent, out var areas))
				return [];

			return areas.GetAll()
				.Where(a => a.Map != null && AvailableMaps.Contains(a.Map.ClassName))
				.ToArray();
		}

		/// <summary>
		/// Returns the available maps the spawner spawns on, dropping any
		/// map players cannot reach.
		/// </summary>
		/// <param name="spawner"></param>
		/// <param name="areas"></param>
		private static string[] ResolveAvailableMaps(MonsterSpawner spawner, SpawnArea[] areas)
		{
			var maps = new HashSet<string>(areas.Select(a => a.Map.ClassName), StringComparer.OrdinalIgnoreCase);

			// A spawner that already spawned somewhere reports it directly,
			// which covers any spawner defined without a spawn area.
			foreach (var mapId in spawner.Maps)
			{
				if (!ZoneServer.Instance.World.TryGetMap(mapId, out var map))
					continue;

				if (AvailableMaps.Contains(map.ClassName))
					maps.Add(map.ClassName);
			}

			return maps.ToArray();
		}

		/// <summary>
		/// Adds the spawner's spawn points to the cloud, weighting each by
		/// the share of the population that stands there.
		/// </summary>
		/// <param name="spawner"></param>
		/// <param name="data"></param>
		/// <param name="areas"></param>
		/// <param name="points"></param>
		private static void AddPoints(MonsterSpawner spawner, MonsterData data, SpawnArea[] areas, List<CensusPoint> points)
		{
			if (areas.Length == 0 || !DensityRanks.Contains(data.Rank))
				return;

			var weight = spawner.MaxAmount / (float)areas.Length;

			foreach (var area in areas)
			{
				var center = area.Area.Center;

				points.Add(new CensusPoint
				{
					Map = area.Map.ClassName,
					X = center.X,
					Z = center.Y,
					Level = data.Level,
					Weight = weight,
				});
			}
		}
	}
}
