using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.World.Actors.Characters;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// Handler for the attack-speed window Fanaticism opens on every use
	/// (riding on the unused BeadyEyed_Buff, shown as "Fanatic Rush").
	/// Attack speed and nothing else. It used to mint Fanaticism stacks per
	/// attack as well, which was the class's whole economy; that economy is
	/// gone — Pyre reads health lost instead — so what is left is the plain
	/// frenzy the skill's name promised in the first place.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.BeadyEyed_Buff)]
	public class Zeal_Rush_BuffOverride : BuffHandler
	{
		/// <summary>
		/// Attack speed while the window lasts. PLACEHOLDER.
		/// Shown in Fanaticism's tooltip via captionRatio1 — keep the two in
		/// sync.
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
