using System;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone;
using Xunit;
using Xunit.Abstractions;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Proves the headless harness boots and that the real combat pipeline
	/// agrees with sim.py's model of it. These are the gate for building out
	/// the full scenario matrix.
	/// </summary>
	[Collection(BalanceCollection.Name)]
	public class BalanceSmokeTests
	{
		private readonly ITestOutputHelper _output;

		public BalanceSmokeTests(BalanceHost host, ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void ServerBootsWithDataAndScripts()
		{
			Assert.NotNull(ZoneServer.Instance.Data.MonsterDb);
			Assert.NotEmpty(ZoneServer.Instance.Data.MonsterDb.Entries);
			Assert.NotNull(ZoneServer.Instance.Data.SkillDb);
			Assert.True(ZoneServer.Instance.World.Count > 0, "No maps were loaded.");
		}

		[Fact]
		public void ArenaMapIsAvailable()
		{
			var arena = SyntheticActors.GetArena();

			Assert.NotNull(arena);
			Assert.NotNull(arena.Ground);
		}

		[Fact]
		public void SyntheticCharacterHasExpectedAttack()
		{
			var character = SyntheticActors.CreateCharacter(JobId.Swordsman, 1);

			try
			{
				var attack = character.Properties.GetFloat(PropertyName.MAXPATK);

				_output.WriteLine($"Lv1 Swordsman, no gear: MAXPATK {attack:F1}");

				// Phase 2b: 5 (base) + weapon 0 + stat. A naked Lv1
				// character should be a single-digit-to-low-teens number,
				// not the pre-rebalance ~125.
				Assert.InRange(attack, 1f, 40f);
			}
			finally
			{
				SyntheticActors.Cleanup(character);
			}
		}

		[Fact]
		public void BasicAttackDamageMatchesSim()
		{
			// sim.py reference R1: Lv1 character, Lv1 Normal sword, vs a Lv1
			// Normal mob -> ~19 damage at ~100% hit. This runs naked, so it
			// only checks the shape: a small positive number, few dodges.
			var character = SyntheticActors.CreateCharacter(JobId.Swordsman, 1);
			var mobData = SyntheticActors.FindReferenceMob(1);
			var mob = SyntheticActors.CreateMob(mobData.Id);

			try
			{
				var skill = SyntheticActors.GiveSkill(character, SkillId.Normal_Attack, 1);
				var sample = HitSampler.Sample(character, mob, skill);

				_output.WriteLine($"Lv1 vs {mobData.ClassName} (lv{mobData.Level}, DEF {mobData.PhysicalDefense}): {sample}");

				Assert.True(sample.EffectiveMean > 0, "Basic attack dealt no damage at all.");
				Assert.True(sample.DodgeRate < 0.20f, $"Dodge rate {sample.DodgeRate:P0} is too high for a same-level fight.");
			}
			finally
			{
				SyntheticActors.Cleanup(character, mob);
			}
		}

		[Fact]
		public void SamplingIsReproducible()
		{
			// Guards the seed reaching the damage pipeline: if a roll stops
			// reading GameRandom, this fails loudly rather than silently going
			// back to unseeded sampling.
			var character = SyntheticActors.CreateCharacter(JobId.Swordsman, 30);
			var mobData = SyntheticActors.FindReferenceMob(40);
			var mob = SyntheticActors.CreateMob(mobData.Id);

			try
			{
				var skill = SyntheticActors.GiveSkill(character, SkillId.Normal_Attack, 1);

				var first = HitSampler.Sample(character, mob, skill, samples: 500);
				var second = HitSampler.Sample(character, mob, skill, samples: 500);
				var different = HitSampler.Sample(character, mob, skill, samples: 500, seed: 1);

				_output.WriteLine($"seed A run 1: {first}");
				_output.WriteLine($"seed A run 2: {second}");
				_output.WriteLine($"seed B      : {different}");

				Assert.Equal(first.EffectiveMean, second.EffectiveMean);
				Assert.Equal(first.DodgeRate, second.DodgeRate);

				// A different seed should move at least one of the rolls,
				// otherwise the seed is not reaching the pipeline at all.
				Assert.True(first.EffectiveMean != different.EffectiveMean || first.DodgeRate != different.DodgeRate,
					"Changing the seed changed nothing - the seed is not reaching SCR_SkillHit.");
			}
			finally
			{
				SyntheticActors.Cleanup(character, mob);
			}
		}

		[Fact]
		public void LevelGapProducesRisingMissRate()
		{
			// Phase 2d: the wall is expressed as misses and must grow with
			// the level gap at every character level.
			var character = SyntheticActors.CreateCharacter(JobId.Swordsman, 30);
			var skill = SyntheticActors.GiveSkill(character, SkillId.Normal_Attack, 1);

			var previousDodge = -1f;

			foreach (var gap in new[] { 0, 10, 20, 30 })
			{
				var mobData = SyntheticActors.FindReferenceMob(30 + gap);
				var mob = SyntheticActors.CreateMob(mobData.Id);

				try
				{
					var sample = HitSampler.Sample(character, mob, skill);

					_output.WriteLine($"char Lv30 vs mob Lv{30 + gap}: {sample}");

					Assert.True(sample.DodgeRate >= previousDodge,
						$"Dodge rate fell from {previousDodge:P0} to {sample.DodgeRate:P0} as the gap widened.");

					previousDodge = sample.DodgeRate;
				}
				finally
				{
					SyntheticActors.Cleanup(null, mob);
				}
			}

			SyntheticActors.Cleanup(character);
		}
	}
}
