using System;
using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the Zealot burn mode carried by Immolate.
	/// Per the concept (Zealot_Rework_Konzept.xlsx v1.0) the aura burns the
	/// caster's health down to the current burn floor and keeps it there,
	/// and slowly builds Fervor — faster the deeper the floor sits. The
	/// damage side of the kit lives in the Immolate burst (recasting the
	/// skill), whose power and area scale with the floor; the fast Fervor
	/// source is Brand the Heretic.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Immolation_Self_Buff)]
	public class Immolation_Self_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Share of maximum HP burned per second while above the floor.
		/// PLACEHOLDER (concept: "Tickrate" is tuning-only). Flat, so
		/// reaching a floor takes a predictable amount of time.
		/// </summary>
		private const float BurnPerSecond = 0.05f;

		/// <summary>
		/// Never burns the caster to death; enemies should do that.
		/// </summary>
		private const float HpFloor = 1f;

		/// <summary>
		/// The db entry updates every 500 ms, so every second tick is one
		/// second of game time.
		/// </summary>
		private const int TicksPerSecond = 2;

		private const string TickVar = "Immolation.Tick";
		private const string FervorTickVar = "Immolation.FervorTick";

		public override void OnEnd(Buff buff)
		{
			// Ending the mode resets the risk dial entirely.
			if (buff.Target is ICombatEntity target)
				ZealotBurnFloor.ConsumeStacks(target);
		}

		public override void WhileActive(Buff buff)
		{
			var target = buff.Target;
			if (target.IsDead)
				return;

			if (!this.IsFullSecond(buff))
				return;

			ZealotBurnFloor.PulseAuraVisual(target, ZealotBurnFloor.Get(target));

			this.BurnTowardsFloor(target);
			this.BuildFervor(buff, target);
		}

		/// <summary>
		/// Slowly builds Fervor while burning, faster the deeper the floor
		/// sits: one stack every 5/4/3/2 seconds at floors 70/50/30/10.
		/// PLACEHOLDER pacing (addition to the concept workbook, requested
		/// after v1.0) — Brand the Heretic stays the fast Fervor source.
		/// </summary>
		private void BuildFervor(Buff buff, ICombatEntity target)
		{
			var floor = ZealotBurnFloor.Get(target);
			var intervalSeconds = 2 + (floor - ZealotBurnFloor.Min) / ZealotBurnFloor.Step;

			var tick = buff.Vars.GetInt(FervorTickVar) + 1;

			if (tick < intervalSeconds)
			{
				buff.Vars.SetInt(FervorTickVar, tick);
				return;
			}

			buff.Vars.SetInt(FervorTickVar, 0);
			ZealotFervor.AddStacks(target, 1, buff.SkillId);
		}

		/// <summary>
		/// Counts update ticks and returns true once per second.
		/// </summary>
		private bool IsFullSecond(Buff buff)
		{
			var tick = buff.Vars.GetInt(TickVar) + 1;

			if (tick < TicksPerSecond)
			{
				buff.Vars.SetInt(TickVar, tick);
				return false;
			}

			buff.Vars.SetInt(TickVar, 0);
			return true;
		}

		/// <summary>
		/// Burns health until the caster sits at their burn floor, then stops.
		/// Healing above the floor simply gets burned off again.
		/// </summary>
		private void BurnTowardsFloor(ICombatEntity target)
		{
			if (target is not Character character)
				return;

			var maxHp = target.Properties.GetFloat(PropertyName.MHP);
			if (maxHp <= 0)
				return;

			var floorHp = maxHp * (ZealotBurnFloor.Get(target) / 100f);
			var hp = target.Hp;

			// At or below the floor there is nothing to burn — this is where
			// the aura idles.
			if (hp <= floorHp || hp <= HpFloor)
				return;

			var burn = Math.Min(maxHp * BurnPerSecond, hp - Math.Max(floorHp, HpFloor));
			if (burn <= 0)
				return;

			// ModifyHp would send ZC_ADD_HP, which the client answers with
			// its damage-taken flash - the whole character blinking brighter
			// once per second. The safe variant plus a plain status update
			// moves the HP bar without triggering that feedback.
			character.ModifyHpSafe(-burn, out _, out var priority);
			Send.ZC_UPDATE_ALL_STATUS(character, priority);
		}
	}
}
