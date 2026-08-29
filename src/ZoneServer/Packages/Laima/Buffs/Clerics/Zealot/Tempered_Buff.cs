using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.Scripting.ScriptableEvents;
using Melia.Zone.Skills;
using Melia.Zone.Skills.Combat;
using Melia.Zone.Skills.Handlers.Clerics.Zealot;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The window Temper the Flame opens (riding on the now unused
	/// Fanaticism_Buff).
	/// While it holds, a share of every blow does not land all at once —
	/// the fire takes it and works it off over the next few seconds, and
	/// every point of it feeds the Pyre. Nothing is prevented and nothing
	/// is reduced: the same total arrives, just as a burn rather than a
	/// spike, which is the one shape a Zealot's own healing can answer.
	/// Getting hit is how the Pyre loads while this is up, which is why the
	/// window is worth walking into a fight for rather than out of one.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Fanaticism_Buff)]
	public class Tempered_BuffOverride : BuffHandler
	{
		/// <summary>
		/// The share of an incoming blow that is deferred into fire.
		/// Deliberately modest — this is a delay, not a mitigation, and the
		/// defensive value of the skill is meant to sit in its heal.
		/// PLACEHOLDER.
		/// Shown in the tooltip via captionRatio2 in skills_overrides.txt —
		/// keep the two in sync.
		/// </summary>
		public const float DeferredShare = 0.20f;

		[CombatCalcModifier(CombatCalcPhase.AfterCalc, BuffId.Fanaticism_Buff)]
		public void OnDefenseAfterCalc(ICombatEntity attacker, ICombatEntity target, Skill skill, SkillModifier modifier, SkillHitResult skillHitResult)
		{
			if (!target.IsBuffActive(BuffId.Fanaticism_Buff))
				return;

			if (skillHitResult.Damage <= 0)
				return;

			// Nothing to work the fire off with if the flame is out; without
			// the aura ticking, the deferred pool would simply sit there.
			if (!target.IsBuffActive(BuffId.Immolation_Self_Buff))
				return;

			var deferred = skillHitResult.Damage * DeferredShare;

			skillHitResult.Damage -= deferred;
			ZealotBurnFloor.AddDeferred(target, deferred);
		}
	}
}
