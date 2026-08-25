using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the short attack-speed rush Fanaticism grants on every
	/// use (riding on the unused BeadyEyed_Buff, shown as "Zeal").
	/// PLACEHOLDER: flat bonus, sized around Frenzy's range (150 + 10 per
	/// stack there). Design idea on file: an ability later toggles whether
	/// Fanaticism grants stacks or this rush instead.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.BeadyEyed_Buff)]
	public class Zeal_Rush_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Attack speed while the rush lasts. PLACEHOLDER.
		/// </summary>
		private const float AspdBonus = 200f;

		public override void OnActivate(Buff buff, ActivationType activationType)
		{
			if (buff.Target is Character)
				UpdatePropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM, AspdBonus);
		}

		public override void OnEnd(Buff buff)
		{
			RemovePropertyModifier(buff, buff.Target, PropertyName.NormalASPD_BM);
		}
	}
}
