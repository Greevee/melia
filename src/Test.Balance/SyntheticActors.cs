using System;
using System.Collections.Generic;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone;
using Melia.Zone.Database;
using Melia.Zone.Network;
using Melia.Zone.Skills;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Characters.Components;
using Melia.Zone.World.Actors.CombatEntities.Components;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Maps;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Allocated stat points for a synthetic character. These map to the
	/// *_STAT properties, which are what stat point spending actually sets.
	/// </summary>
	public class StatSpread
	{
		public int Str { get; set; }
		public int Con { get; set; }
		public int Int { get; set; }
		public int Spr { get; set; }
		public int Dex { get; set; }

		/// <summary>
		/// Returns a spread with every point in one stat, which is the
		/// reference build sim.py calibrates against.
		/// </summary>
		/// <param name="stat"></param>
		/// <param name="points"></param>
		public static StatSpread AllIn(string stat, int points)
		{
			var spread = new StatSpread();

			switch (stat.ToUpperInvariant())
			{
				case "STR": spread.Str = points; break;
				case "CON": spread.Con = points; break;
				case "INT": spread.Int = points; break;
				case "SPR": spread.Spr = points; break;
				case "DEX": spread.Dex = points; break;
				default: throw new ArgumentException($"Unknown stat '{stat}'.", nameof(stat));
			}

			return spread;
		}
	}

	/// <summary>
	/// Builds characters and monsters in memory for scenario measurement,
	/// with no database or client involved.
	/// </summary>
	public static class SyntheticActors
	{
		/// <summary>
		/// Map every synthetic actor is placed on, chosen because it is a
		/// plain field with no scripted mechanics of its own.
		/// </summary>
		public const string ArenaMapName = "f_siauliai_west";

		/// <summary>
		/// Map ticks to allow a queued monster to register. The map adds at
		/// most five per tick, so this covers any scenario's group.
		/// </summary>
		private const int MaxSettleTicks = 20;

		private static int _teamNameCounter;
		private static Position? _arenaCenter;

		/// <summary>
		/// Returns the map synthetic actors fight on.
		/// </summary>
		public static Map GetArena()
		{
			if (!ZoneServer.Instance.World.TryGetMap(ArenaMapName, out var map))
				throw new InvalidOperationException($"Arena map '{ArenaMapName}' is not loaded.");

			return map;
		}

		/// <summary>
		/// Creates a character at the given job and level, places it on the
		/// arena and returns it.
		/// </summary>
		/// <param name="jobId"></param>
		/// <param name="level"></param>
		/// <param name="stats"></param>
		/// <param name="position"></param>
		public static Character CreateCharacter(JobId jobId, int level, StatSpread stats = null, Position position = default)
		{
			stats ??= StatSpread.AllIn("STR", level);

			var teamName = $"Synth{++_teamNameCounter}";

			var character = new Character
			{
				Name = teamName,
				TeamName = teamName,
				JobId = jobId,
			};

			// DummyConnection absorbs every Send.ZC_*; the account is real
			// but empty so Username and PermissionLevel resolve.
			character.Connection = new DummyConnection
			{
				SelectedCharacter = character,
				Account = new Account { Name = teamName },
			};

			character.Jobs.AddSilent(new Job(character, jobId));

			var properties = character.Properties;
			properties.SetFloat(PropertyName.Lv, level);
			properties.SetFloat(PropertyName.STR_STAT, stats.Str);
			properties.SetFloat(PropertyName.CON_STAT, stats.Con);
			properties.SetFloat(PropertyName.INT_STAT, stats.Int);
			properties.SetFloat(PropertyName.MNA_STAT, stats.Spr);
			properties.SetFloat(PropertyName.DEX_STAT, stats.Dex);
			properties.InvalidateAll();

			character.Position = ResolvePosition(position);
			GetArena().AddCharacter(character);

			properties.SetFloat(PropertyName.HP, properties.GetFloat(PropertyName.MHP));
			properties.SetFloat(PropertyName.SP, properties.GetFloat(PropertyName.MSP));

			return character;
		}

		/// <summary>
		/// Gives the character a skill at the given level.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="skillId"></param>
		/// <param name="level"></param>
		public static Skill GiveSkill(Character character, SkillId skillId, int level)
		{
			var skill = new Skill(character, skillId, level);
			character.Skills.Add(skill);

			return skill;
		}

		/// <summary>
		/// Creates a hostile monster of the given class name and places it
		/// on the arena.
		/// </summary>
		/// <param name="className"></param>
		/// <param name="position"></param>
		public static Mob CreateMob(string className, Position position = default)
		{
			if (!ZoneServer.Instance.Data.MonsterDb.TryFind(className, out var data))
				throw new ArgumentException($"Unknown monster '{className}'.", nameof(className));

			return CreateMob(data.Id, position);
		}

		/// <summary>
		/// Creates a hostile monster of the given id and places it on the
		/// arena.
		/// </summary>
		/// <param name="monsterId"></param>
		/// <param name="position"></param>
		public static Mob CreateMob(int monsterId, Position position = default)
		{
			var mob = new Mob(monsterId, RelationType.Enemy);

			mob.Position = ResolvePosition(position);
			mob.SpawnPosition = mob.Position;
			mob.Components.Add(new MovementComponent(mob));

			var map = GetArena();
			map.AddMonster(mob);
			Settle(map, mob);

			return mob;
		}

		/// <summary>
		/// Runs the map until the monster is actually registered on it.
		/// </summary>
		/// <remarks>
		/// Map.AddMonster only queues the monster; the queue drains in
		/// UpdateEntities, which RunHeadless never starts. Without this the
		/// monster is invisible to every Map.GetAttackable* lookup, so splash
		/// geometry finds nothing even though direct damage sampling works.
		/// </remarks>
		/// <param name="map"></param>
		/// <param name="mob"></param>
		private static void Settle(Map map, Mob mob)
		{
			for (var i = 0; i < MaxSettleTicks && mob.Map != map; ++i)
				map.Update(TimeSpan.Zero);

			if (mob.Map != map)
				throw new InvalidOperationException($"'{mob.Data.ClassName}' never registered on '{ArenaMapName}' - the add queue is not draining.");
		}

		/// <summary>
		/// Returns the median-HP monster of the given rank at the given
		/// level, restricted to monsters that spawn on a map players can
		/// reach, so results are neither skewed by an outlier nor measured
		/// against content nobody fights.
		/// </summary>
		/// <param name="level"></param>
		/// <param name="rank"></param>
		public static MonsterData FindReferenceMob(int level, MonsterRank rank = MonsterRank.Normal)
			=> SpawnCensus.FindReferenceMob(level, rank);

		/// <summary>
		/// Radius the arena needs to be walkable out to, so a scenario can
		/// place monsters anywhere inside it without the ground moving them.
		/// </summary>
		public const float ArenaClearance = 300f;

		/// <summary>
		/// Random ground positions to try before settling for the roomiest
		/// one found.
		/// </summary>
		private const int ArenaSearchTries = 2000;

		/// <summary>
		/// Returns a fixed point of walkable ground that every scenario is
		/// built around, so offsets between actors are meaningful.
		/// </summary>
		/// <remarks>
		/// The point must have open ground all around it. A merely walkable
		/// point is not enough: scenario offsets that land on a wall get
		/// snapped to the nearest valid ground, which silently moves a
		/// monster out of the cone the skill was aimed down.
		/// </remarks>
		public static Position GetArenaCenter()
		{
			if (_arenaCenter.HasValue)
				return _arenaCenter.Value;

			var ground = GetArena().Ground;
			var probes = GetClearanceProbes();

			var best = default(Position);
			var bestClear = -1;

			for (var i = 0; i < ArenaSearchTries; ++i)
			{
				if (!ground.TryGetRandomPosition(out var candidate))
					continue;

				var clear = 0;

				foreach (var probe in probes)
				{
					if (ground.IsValidPosition(new Position(candidate.X + probe.X, candidate.Y, candidate.Z + probe.Z)))
						++clear;
				}

				if (clear > bestClear)
				{
					best = candidate;
					bestClear = clear;
				}

				if (clear == probes.Length)
					break;
			}

			if (bestClear < 0)
				throw new InvalidOperationException($"Could not find walkable ground on '{ArenaMapName}'.");

			_arenaCenter = best;

			return best;
		}

		/// <summary>
		/// Returns the offsets the arena centre is tested for clearance at:
		/// rings at the distances scenarios actually place monsters.
		/// </summary>
		private static Position[] GetClearanceProbes()
		{
			var probes = new List<Position>();

			foreach (var radius in new[] { 40f, 120f, 200f, ArenaClearance })
			{
				for (var step = 0; step < 16; ++step)
				{
					var angle = step / 16f * MathF.PI * 2f;

					probes.Add(new Position(MathF.Cos(angle) * radius, 0, MathF.Sin(angle) * radius));
				}
			}

			return probes.ToArray();
		}

		/// <summary>
		/// Snaps a position onto valid ground, since skills and movement
		/// both depend on the actor standing somewhere walkable. A default
		/// position is treated as "the arena center".
		/// </summary>
		/// <remarks>
		/// Only the height is adjusted while the ground is walkable.
		/// Searching for the nearest valid position unconditionally moved
		/// actors tens of units sideways, which silently pushed them out of
		/// the cone a scenario had aimed at them.
		/// </remarks>
		/// <param name="position"></param>
		private static Position ResolvePosition(Position position)
		{
			if (position == default)
				return GetArenaCenter();

			var center = GetArenaCenter();
			var absolute = new Position(center.X + position.X, center.Y + position.Y, center.Z + position.Z);
			var ground = GetArena().Ground;

			if (ground.IsValidPosition(absolute) && ground.TryGetHeightAt(absolute, out var height))
				return new Position(absolute.X, height, absolute.Z);

			if (ground.TryGetNearestValidPosition(absolute, out var validPos))
				return validPos;

			return absolute;
		}

		/// <summary>
		/// Removes actors from the arena so scenarios do not leak state into
		/// each other.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="mobs"></param>
		public static void Cleanup(Character character, params Mob[] mobs)
		{
			var map = GetArena();

			foreach (var mob in mobs)
			{
				if (mob != null)
					map.RemoveMonster(mob);
			}

			if (character != null)
				map.RemoveCharacter(character);
		}
	}
}
