using Melia.Shared.Game.Const;
using Melia.Shared.L10N;
using Melia.Shared.Packages;
using Melia.Shared.World;
using Melia.Zone.Network;
using Melia.Zone.Skills.Handlers.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Skills.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Stub for the Zealot skill Fanatic Illusion.
	/// The concept (Zealot_Rework_Konzept.xlsx) still lists it as WiP with
	/// no agreed design, but the skill stays visible and learnable in the
	/// tree. Casting refuses cleanly — without a handler the client would
	/// sit locked in its skill state for ~2 seconds.
	/// </summary>
	[Package("laima")]
	[SkillHandler(SkillId.Zealot_FanaticIllusion)]
	public class Zealot_FanaticIllusionOverride : IGroundSkillHandler
	{
		public void Handle(Skill skill, ICombatEntity caster, Position originPos, Position farPos, ICombatEntity target)
		{
			caster.ServerMessage(Localization.Get("Fanatic Illusion is still being designed."));
			Send.ZC_SKILL_DISABLE(caster);
		}
	}
}
