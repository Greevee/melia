using Melia.Shared.Game.Const;

namespace Melia.Test.Balance
{
	/// <summary>
	/// Which basic attack a class actually swings, given the weapon its
	/// reference build carries.
	/// </summary>
	/// <remarks>
	/// There is no single basic attack. Character.JobSkills grants a different
	/// one per base job and the weapon decides which of them fires, and they
	/// are not equivalent: Normal_Attack is classType Melee and reads PATK,
	/// while Magic_Attack is classType Magic and reads MATK. Measuring every
	/// class with Normal_Attack therefore priced casters against a swing they
	/// never make and read zero, which is what forced the Swordsman reference
	/// yardstick that this replaces.
	/// </remarks>
	public static class BasicAttacks
	{
		/// <summary>
		/// Returns the basic attack the weapon fires, falling back to the
		/// base job's default when the build carries no weapon.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="weapon"></param>
		public static SkillId For(JobEntry job, EquipType weapon)
		{
			switch (weapon)
			{
				case EquipType.Bow:
				case EquipType.THBow:
					return SkillId.Bow_Attack;

				case EquipType.Staff:
				case EquipType.Wand:
					return SkillId.Magic_Attack;

				case EquipType.THStaff:
					return SkillId.Magic_Attack_TH;

				case EquipType.Mace:
					return SkillId.Hammer_Attack;

				case EquipType.THMace:
					return SkillId.Hammer_Attack_TH;

				case EquipType.Musket:
					return SkillId.Musket_Attack;

				case EquipType.Cannon:
					return SkillId.Cannon_Normal_Attack;

				case EquipType.Pistol:
					return SkillId.Pistol_Attack;

				case EquipType.THSword:
				case EquipType.THSpear:
					return SkillId.Normal_Attack_TH;

				case EquipType.Sword:
				case EquipType.Dagger:
				case EquipType.Spear:
				case EquipType.Rapier:
					return SkillId.Normal_Attack;
			}

			return Default(job);
		}

		/// <summary>
		/// Returns the base job's own basic attack, used when the reference
		/// build could not find a weapon.
		/// </summary>
		/// <param name="job"></param>
		public static SkillId Default(JobEntry job)
		{
			switch (job.BaseJob)
			{
				case JobClass.Wizard: return SkillId.Magic_Attack;
				case JobClass.Archer: return SkillId.Bow_Attack;
				case JobClass.Cleric: return SkillId.Hammer_Attack;
			}

			return SkillId.Normal_Attack;
		}
	}
}
