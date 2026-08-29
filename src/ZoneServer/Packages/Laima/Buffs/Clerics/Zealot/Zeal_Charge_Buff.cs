using Melia.Shared.Game.Const;
using Melia.Shared.Packages;
using Melia.Zone.Buffs.Base;
using Melia.Zone.World.Actors;

namespace Melia.Zone.Buffs.Handlers.Clerics.Zealot
{
	/// <summary>
	/// The charges Zeal builds towards the next Immolate (riding on the now
	/// unused Fanaticism_Zealot12_Buff, which is the display carrier the
	/// resource bar reads).
	/// It carries no behaviour of its own — Zeal stacks it, Immolate spends
	/// it. The handler exists so the stack count has one place to live and
	/// one place to be capped.
	/// </summary>
	[Package("laima")]
	[BuffHandler(BuffId.Fanaticism_Zealot12_Buff)]
	public class Zeal_Charge_BuffOverride : BuffHandler
	{
		/// <summary>
		/// The most charges Zeal can bank, matching its overheat count: three
		/// presses, three charges, then Immolate. PLACEHOLDER.
		/// Shown in the tooltips via captionRatio2 — keep them in sync.
		/// </summary>
		public const int MaxCharges = 3;

		/// <summary>
		/// What one charge adds to the next Immolate. PLACEHOLDER.
		/// Shown in the tooltips via captionRatio1 — keep them in sync.
		/// </summary>
		public const float DamagePerCharge = 0.20f;

		/// <summary>
		/// Adds a charge, up to the cap, and shows it.
		/// </summary>
		public static void Add(ICombatEntity entity, SkillId skillId)
		{
			if (!entity.TryGetBuff(BuffId.Fanaticism_Zealot12_Buff, out var buff))
			{
				entity.StartBuff(BuffId.Fanaticism_Zealot12_Buff, 1, 0f, System.TimeSpan.Zero, entity, skillId);
				return;
			}

			if (buff.OverbuffCounter >= MaxCharges)
				return;

			buff.OverbuffCounter++;
			buff.NotifyUpdate();
		}

		/// <summary>
		/// Takes every charge and returns how many there were.
		/// </summary>
		public static int Consume(ICombatEntity entity)
		{
			if (!entity.TryGetBuff(BuffId.Fanaticism_Zealot12_Buff, out var buff))
				return 0;

			var charges = buff.OverbuffCounter;
			entity.StopBuff(BuffId.Fanaticism_Zealot12_Buff);

			return charges;
		}
	}
}
