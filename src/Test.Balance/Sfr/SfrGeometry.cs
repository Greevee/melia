using System;
using System.Collections.Generic;
using System.Linq;

namespace Melia.Test.Balance.Sfr
{
	/// <summary>
	/// How many monsters a skill's geometry reaches in each scenario, and how
	/// that compares to what an averaged basic-attack swing reaches.
	/// </summary>
	/// <remarks>
	/// Mirrors SkillProfiler.ResolveTargets against the ScenarioMatrix
	/// placements, without booting a server: the pricer's whole point is that
	/// it can price a skill the sweep has never measured.
	/// </remarks>
	public static class SfrGeometry
	{
		/// <summary>
		/// Distance the melee placements sit at, mirroring ScenarioMatrix.
		/// </summary>
		public const float MeleeDistance = 30f;

		/// <summary>
		/// Spacing three monsters at measured passive density stand apart.
		/// </summary>
		/// <remarks>
		/// RadiusForDensity(3) halved, fitted against the measured targets.
		/// SpawnCensus computes it from the live spawner registry, which needs a
		/// booted world, so the measurement is carried here as a constant.
		/// </remarks>
		public const float DensitySpacingAtThree = 47f;

		/// <summary>
		/// Monsters the density constant was measured for.
		/// </summary>
		private const float DensityReferenceCount = 3f;

		/// <summary>
		/// How far a chasing monster closes per second of cast.
		/// </summary>
		public const float ChaseRunSpeed = 48f;

		/// <summary>
		/// SDR assumed for a monster the scenario does not give one, which is
		/// the medium-size value every placement but the penetration ranks uses.
		/// </summary>
		private const float DefaultSdr = 2f;

		/// <summary>
		/// The scenarios the pricer resolves geometry for, which is every one
		/// the weights name.
		/// </summary>
		public static IEnumerable<ScenarioSpec> PricedScenarios
			=> ScenarioMatrix.All.Where(s => SfrDials.ScenarioWeights.ContainsKey(s.Id));

		/// <summary>
		/// Returns the monsters and the aim point for a scenario.
		/// </summary>
		/// <remarks>
		/// Delegates to ScenarioMatrix so the pricer and the damage sweep place
		/// their monsters identically, passing the measured density constant in
		/// place of the SpawnCensus reading the pricer cannot take.
		/// </remarks>
		/// <param name="spec"></param>
		/// <param name="castSeconds"></param>
		/// <param name="aimDistance"></param>
		public static ScenarioMob[] Placement(ScenarioSpec spec, float castSeconds, out float aimDistance)
			=> ScenarioMatrix.Layout(spec, spec.MobCount, castSeconds * 1000f, ChaseRunSpeed,
				DensitySpacing(spec.MobCount), out aimDistance);

		/// <summary>
		/// Returns how far apart that many monsters stand at passive density.
		/// </summary>
		/// <remarks>
		/// Monsters per unit area is what the census measures, so the radius
		/// holding a given count grows as its square root.
		/// </remarks>
		/// <param name="count"></param>
		public static float DensitySpacing(int count)
			=> DensitySpacingAtThree * MathF.Sqrt(Math.Max(1, count) / DensityReferenceCount);

		/// <summary>
		/// Returns the monsters a skill's geometry reaches for one placement.
		/// </summary>
		/// <remarks>
		/// The caster sits at the origin facing +x, and the offsets are monster
		/// positions relative to it.
		/// </remarks>
		/// <param name="entry"></param>
		/// <param name="offsets"></param>
		/// <param name="aimDistance"></param>
		public static int SplashTargets(SkillEntryData entry, ScenarioMob[] offsets, float aimDistance)
		{
			var maxRange = entry.Num("maxRange");
			var splashRange = entry.Num("splashRange");
			var splashHeight = entry.Num("splashHeight");
			var splashAngle = entry.Num("splashAngle");
			var wave = entry.Num("waveLength");
			var type = entry.Text("splashType") ?? "Square";

			var reach = Math.Max(maxRange, Math.Max(splashHeight * 2, splashRange * 2));
			if (reach > 0 && aimDistance > reach)
				return 0;

			// A projectile takes the one target the client picked; its splash
			// fields describe nothing. Unless the handler builds its own area,
			// in which case that area is the real reach.
			if (entry.Text("useType") == "Force" && !entry.HandlerArea)
				return offsets.Any(o => Distance(o, (aimDistance, 0f)) <= MeleeDistance) ? 1 : 0;

			var length = splashHeight > 0 ? splashHeight * 2 : wave * 2;
			var width = splashRange * 2;

			if (type == "Fan" && splashAngle <= 0)
				type = "Square";

			if (type == "Circle")
				length = aimDistance;

			if (width <= 0 || (length <= 0 && type != "Circle") || (type != "Fan" && type != "Square" && type != "Circle"))
				return offsets.Length > 0 ? 1 : 0;

			var inside = new List<float>();

			foreach (var mob in offsets)
			{
				var (x, z) = (mob.X, mob.Z);
				var hit = false;

				switch (type)
				{
					case "Circle":
						hit = Distance((x, z), (aimDistance, 0f)) <= width;
						break;

					case "Square":
						hit = x >= 0 && x <= length && Math.Abs(z) <= width;
						break;

					case "Fan":
					{
						var distance = MathF.Sqrt(x * x + z * z);

						hit = distance > 0 && distance <= length
							&& Math.Abs(MathF.Atan2(z, x) * 180f / MathF.PI) <= splashAngle / 2f;

						break;
					}
				}

				if (hit)
					inside.Add(mob.Sdr > 0 ? mob.Sdr : DefaultSdr);
			}

			var count = entry.UseSdr
				? LimitBySdr(inside, entry.Num("splashRate"))
				: inside.Count;

			if (entry.TargetCap > 0)
				count = Math.Min(count, entry.TargetCap.Value);

			return count;
		}

		/// <summary>
		/// Returns the targets that survive the splash rate, mirroring
		/// Extensions.LimitBySDR.
		/// </summary>
		/// <remarks>
		/// Always yields one, then spends SR on each target's SDR. The targets
		/// are taken highest SDR first, exactly as LimitBySDR orders them, so a
		/// tanky monster soaks the splash before it reaches anything behind it.
		/// </remarks>
		/// <param name="sdrs"></param>
		/// <param name="splashRate"></param>
		public static int LimitBySdr(IEnumerable<float> sdrs, float splashRate)
		{
			var sr = Math.Max(1f, splashRate + SfrDials.CharacterSr);
			var hit = 0;

			foreach (var sdr in sdrs.OrderByDescending(v => v))
			{
				hit++;
				sr -= sdr;

				if (sr <= 0)
					break;
			}

			return hit;
		}

		/// <summary>
		/// Returns the targets an averaged basic-attack swing reaches for the
		/// same placement.
		/// </summary>
		/// <remarks>
		/// Averaged over the five base-job basic attacks rather than the
		/// caster's own, for the same reason the swing rate is: pricing against
		/// its own weapon asks whether a skill beats autoattacking with a mace,
		/// which lets weapon speed set a class's whole ceiling.
		/// </remarks>
		/// <param name="offsets"></param>
		/// <param name="aimDistance"></param>
		public static float GenericBasicReach(ScenarioMob[] offsets, float aimDistance)
		{
			var counts = new List<int>();

			foreach (var name in SfrData.GenericBasicAttacks)
			{
				if (SfrData.Skills.TryGetValue(name, out var entry))
					counts.Add(SplashTargets(entry, offsets, aimDistance));
			}

			return counts.Count > 0 ? (float)counts.Average() : 1f;
		}

		/// <summary>
		/// Puts the first monster straight ahead and spreads the rest on a
		/// circle of the given spacing around it.
		/// </summary>
		/// <remarks>
		/// The anchor matters: a placement whose monsters all sit on a ring
		/// leaves nothing directly in front, so a tight cone measures zero
		/// targets even against a crowd.
		/// </remarks>
		/// <param name="distance"></param>
		/// <param name="spacing"></param>
		/// <param name="count"></param>
		/// <summary>
		/// Returns the distance between two points on the ground plane.
		/// </summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		private static float Distance((float X, float Z) a, (float X, float Z) b)
			=> MathF.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));

		/// <summary>
		/// Returns the distance from a monster to a point.
		/// </summary>
		/// <param name="mob"></param>
		/// <param name="point"></param>
		private static float Distance(ScenarioMob mob, (float X, float Z) point)
			=> Distance((mob.X, mob.Z), point);
	}
}
