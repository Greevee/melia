using System;
using System.Collections.Generic;
using System.Linq;
using Melia.Shared.Data.Database;
using Melia.Shared.Game.Const;
using Melia.Zone;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Items;

namespace Melia.Test.Balance
{
	/// <summary>
	/// What a reference character is wearing, so a measurement can be
	/// reported against the gear that produced it.
	/// </summary>
	public class GearSet
	{
		public ItemGrade Grade { get; init; }
		public int ItemLevel { get; init; }
		public Dictionary<EquipSlot, Item> Items { get; } = new();

		/// <summary>
		/// The weapon, or null for a deliberately naked run.
		/// </summary>
		public Item Weapon => this.Items.TryGetValue(EquipSlot.RightHand, out var item) ? item : null;

		public override string ToString()
		{
			var weapon = this.Weapon == null ? "none" : $"{this.Weapon.Data.ClassName} lv{this.Weapon.UseLevel}";

			return $"{this.Grade} lv{this.ItemLevel} set, weapon {weapon}, {this.Items.Count} piece(s)";
		}
	}

	/// <summary>
	/// Builds and equips the level-appropriate reference gear a scenario is
	/// measured with, so absolute damage can be checked against R1/R2 rather
	/// than only its shape.
	/// </summary>
	public static class ReferenceGear
	{
		/// <summary>
		/// Item levels stop at 75 by design; past that, progression runs
		/// through grade and reinforce.
		/// </summary>
		public const int MaxItemLevel = 75;

		/// <summary>
		/// Armor slots the reference set fills. Accessories are left empty
		/// so their rolled properties do not muddy the base curve.
		/// </summary>
		private static readonly (EquipSlot Slot, EquipType Type)[] ArmorSlots =
		[
			(EquipSlot.Top, EquipType.Shirt),
			(EquipSlot.Pants, EquipType.Pants),
			(EquipSlot.Gloves, EquipType.Gloves),
			(EquipSlot.Shoes, EquipType.Boots),
		];

		/// <summary>
		/// Equips the class's reference set at the given character level and
		/// returns what was equipped.
		/// </summary>
		/// <param name="character"></param>
		/// <param name="job"></param>
		/// <param name="grade"></param>
		/// <param name="armorMaterial"></param>
		public static GearSet Equip(Character character, JobEntry job, ItemGrade grade = ItemGrade.Normal, ArmorMaterialType armorMaterial = ArmorMaterialType.Leather)
		{
			var itemLevel = Math.Min(MaxItemLevel, (int)character.Properties.GetFloat(PropertyName.Lv));
			var set = new GearSet { Grade = grade, ItemLevel = itemLevel };

			var weaponData = FindWeapon(job, grade, itemLevel);
			if (weaponData != null)
				Add(set, EquipSlot.RightHand, weaponData);

			if (job.UsesShield)
			{
				var shieldData = FindItem(EquipType.Shield, grade, itemLevel, null);
				if (shieldData != null)
					Add(set, EquipSlot.LeftHand, shieldData);
			}

			foreach (var (slot, type) in ArmorSlots)
			{
				var armorData = FindItem(type, grade, itemLevel, armorMaterial);
				if (armorData != null)
					Add(set, slot, armorData);
			}

			foreach (var pair in set.Items)
				character.Inventory.SetEquipSilent(pair.Key, pair.Value);

			character.Properties.InvalidateAll();
			character.Properties.SetFloat(PropertyName.HP, character.Properties.GetFloat(PropertyName.MHP));
			character.Properties.SetFloat(PropertyName.SP, character.Properties.GetFloat(PropertyName.MSP));

			return set;
		}

		/// <summary>
		/// Returns the class's weapon at the given level, walking its
		/// preference list and falling back to any weapon it can hold, so a
		/// gap in the item pool degrades the measurement instead of failing
		/// it silently.
		/// </summary>
		/// <param name="job"></param>
		/// <param name="grade"></param>
		/// <param name="itemLevel"></param>
		public static ItemData FindWeapon(JobEntry job, ItemGrade grade, int itemLevel)
		{
			foreach (var type in job.Weapons)
			{
				var data = FindItem(type, grade, itemLevel, null);

				if (data != null)
					return data;
			}

			foreach (var type in job.Weapons)
			{
				var data = FindItem(type, ItemGrade.Normal, itemLevel, null);

				if (data != null)
					return data;
			}

			return null;
		}

		/// <summary>
		/// Returns the highest-level item of the given type at or below the
		/// requested level, or null if the pool has none.
		/// </summary>
		/// <param name="type"></param>
		/// <param name="grade"></param>
		/// <param name="itemLevel"></param>
		/// <param name="material"></param>
		public static ItemData FindItem(EquipType type, ItemGrade grade, int itemLevel, ArmorMaterialType? material)
		{
			var candidates = ZoneServer.Instance.Data.ItemDb.Entries.Values
				.Where(i => i.EquipType1 == type)
				.Where(i => !string.IsNullOrEmpty(i.EquipSlot))
				.Where(i => i.Grade == grade)
				.Where(i => i.MinLevel > 0 && i.MinLevel <= itemLevel)
				.Where(i => material == null || i.Material == material.Value)
				.Where(i => !IsExcluded(i))
				.OrderByDescending(i => i.MinLevel)
				.ThenBy(i => i.Id)
				.ToArray();

			return candidates.FirstOrDefault();
		}

		/// <summary>
		/// Returns true for equipment a player would not fight in. Pet gear
		/// shares the weapon equip types, and at lv75 it was outranking every
		/// real sword.
		/// </summary>
		/// <param name="data"></param>
		private static bool IsExcluded(ItemData data)
		{
			if (data.ClassName == null)
				return false;

			return data.ClassName.StartsWith("PET", StringComparison.OrdinalIgnoreCase);
		}

		private static void Add(GearSet set, EquipSlot slot, ItemData data)
			=> set.Items[slot] = new Item(data.Id);
	}
}
