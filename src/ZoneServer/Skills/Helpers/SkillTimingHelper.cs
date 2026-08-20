using System;
using System.Collections.Generic;
using Melia.Shared.Game.Const;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Helpers
{
	/// <summary>
	/// Adjusts the skill timings sent to the client for the delay the
	/// packets themselves take to arrive.
	/// </summary>
	public static class SkillTimingHelper
	{
		private static readonly HashSet<SkillId> FullShootTimeSkills = new()
		{
			SkillId.Archer_Jump,

			// Repeating skills pace themselves on the shoot time, so
			// cutting the client's copy desyncs it from the server.
			SkillId.Archer_TwinArrows,
			SkillId.Archer_ObliqueShot,
			SkillId.Scout_ObliqueFire,
			SkillId.Ranger_Barrage,
			SkillId.Ranger_SpiralArrow,
			SkillId.QuarrelShooter_StoneShot,
			SkillId.Fletcher_FletcherArrowShot,
			SkillId.Corsair_ImpaleDagger,
			SkillId.Corsair_PistolShot,
			SkillId.Dievdirbys_Carve,
			SkillId.Bokor_Effigy,
		};

		/// <summary>
		/// Returns the shoot time to send to the client, which locks the
		/// caster in place from the moment the packet arrives rather than
		/// the moment it was sent, unless the skill needs it whole.
		/// </summary>
		/// <param name="entity"></param>
		/// <param name="skill"></param>
		/// <param name="shootTime"></param>
		public static float GetClientShootTime(ICombatEntity entity, Skill skill, float shootTime)
		{
			if (entity is not Character character || skill.IsNormalAttack || FullShootTimeSkills.Contains(skill.Id))
				return shootTime;

			var latency = (float)character.Connection.ClientLatency.TotalMilliseconds;

			return Math.Max(0, shootTime - latency);
		}
	}
}
