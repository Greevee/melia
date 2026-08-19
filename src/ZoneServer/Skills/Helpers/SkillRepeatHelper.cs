using System;
using System.Threading.Tasks;
using Melia.Shared.Game.Const;
using Melia.Shared.Util;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Skills.Helpers
{
	/// <summary>
	/// Repeats client requested attacks at the skill's intended interval, so
	/// that the attack rate doesn't drop with the client's request rate.
	/// </summary>
	public static class SkillRepeatHelper
	{
		private const string RepeatUntilVar = "Melia.RepeatUntil";
		private const string RepeatRunningVar = "Melia.RepeatRunning";
		private const string LastRequestVar = "Melia.RepeatLastRequest";
		private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(50);
		private static readonly TimeSpan ChainGapMargin = TimeSpan.FromMilliseconds(250);
		private static readonly TimeSpan MinRoundTripAllowance = TimeSpan.FromMilliseconds(400);
		private static readonly TimeSpan MaxRoundTripAllowance = TimeSpan.FromMilliseconds(2000);
		private const double AllowanceTolerance = 1.3;

		/// <summary>
		/// Executes the attack and keeps repeating it at the skill's interval
		/// for as long as the client keeps requesting the skill.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="target"></param>
		/// <param name="attack"></param>
		/// <param name="probe"></param>
		public static void Request(Skill skill, ICombatEntity caster, ICombatEntity target, Func<ICombatEntity, bool> attack, Action probe = null)
		{
			var now = GameClock.Now;
			var interval = GetInterval(skill);
			var running = skill.Vars.GetBool(RepeatRunningVar, false);

			var chained = false;
			var requestGap = TimeSpan.Zero;

			if (skill.Vars.TryGet<DateTime>(LastRequestVar, out var lastRequest))
			{
				requestGap = now - lastRequest;
				chained = requestGap <= GetMaxChainGap(caster, interval);
			}

			skill.Vars.Set(LastRequestVar, now);

			// Only the request that starts a chain fires directly, the loop
			// paces every attack after it so the two can't bunch up.
			if (!running && !attack(target))
				return;

			// A single tap is only ever one attack, repeating starts once a
			// second request shows the button is being held.
			if (!chained)
			{
				skill.Vars.Set(RepeatUntilVar, now);

				// Acknowledging again releases the client's request gate, so
				// it asks for the next attack a round trip sooner and a held
				// button is recognized that much earlier.
				probe?.Invoke();
				return;
			}

			var holdWindow = requestGap > interval ? requestGap : interval;
			skill.Vars.Set(RepeatUntilVar, now + holdWindow);

			if (running)
				return;

			skill.Vars.SetBool(RepeatRunningVar, true);
			skill.Run(Repeat(skill, caster, target, attack));
		}

		/// <summary>
		/// Returns how far apart two requests may be and still count as the
		/// client holding the button down.
		/// </summary>
		/// <param name="caster"></param>
		/// <param name="interval"></param>
		private static TimeSpan GetMaxChainGap(ICombatEntity caster, TimeSpan interval)
		{
			var allowance = MinRoundTripAllowance;

			if (caster is Character character)
			{
				// The gap spans the ack going out and the next request
				// coming back, so it costs the one way delay twice.
				var measured = character.Connection.ClientLatency + character.Connection.ClientLatency + ChainGapMargin;
				if (measured > allowance)
					allowance = measured;
			}

			allowance *= AllowanceTolerance;

			if (allowance > MaxRoundTripAllowance)
				allowance = MaxRoundTripAllowance;

			return interval + allowance;
		}

		/// <summary>
		/// Returns the interval the skill is meant to be used at.
		/// </summary>
		/// <param name="skill"></param>
		private static TimeSpan GetInterval(Skill skill)
		{
			var shootTime = skill.Properties.GetFloatSafe(PropertyName.ShootTime);
			var interval = TimeSpan.FromMilliseconds(shootTime);

			return interval < MinInterval ? MinInterval : interval;
		}

		/// <summary>
		/// Repeats the attack until the client stops requesting the skill or
		/// the target can no longer be attacked.
		/// </summary>
		/// <param name="skill"></param>
		/// <param name="caster"></param>
		/// <param name="target"></param>
		/// <param name="attack"></param>
		private static async Task Repeat(Skill skill, ICombatEntity caster, ICombatEntity target, Func<ICombatEntity, bool> attack)
		{
			try
			{
				while (true)
				{
					await skill.Wait(GetInterval(skill));

					if (!skill.Vars.TryGet<DateTime>(RepeatUntilVar, out var repeatUntil) || GameClock.Now >= repeatUntil)
						break;

					if (caster.IsDead || target.IsDead || !caster.CanDamage(target))
						break;

					if (!caster.InSkillUseRange(skill, target))
						break;

					if (!attack(target))
						break;
				}
			}
			finally
			{
				skill.Vars.SetBool(RepeatRunningVar, false);
			}
		}
	}
}
