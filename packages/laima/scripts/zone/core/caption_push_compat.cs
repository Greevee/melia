//--- Melia Script ----------------------------------------------------------
// Caption push re-send after load complete
//--- Description -----------------------------------------------------------
// CZ_GAME_READY sends the caption ratio/override push and the job circles
// right after ZC_START_GAME, but on the Laima client (390044, protocol
// 1000) the UI addons that register the LAIMA_* addon messages are not
// initialized yet at that point, so the push is dropped and every
// #{CaptionRatio}#/#{CaptionTime}# token backed by the generic LAIMA
// caption functions renders as 0.
//
// Re-sends the push once the client has fully loaded (CZ_LOAD_COMPLETE),
// when the addons are up. Fires again on map changes, which is harmless:
// the client tables replace per skill id.
//---------------------------------------------------------------------------

using Melia.Shared.Scripting;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Network;
using Melia.Zone.Scripting;

public class CaptionPushCompatScript : GeneralScript
{
	[On("PlayerLoadComplete")]
	protected void OnPlayerLoadComplete(object sender, PlayerEventArgs e)
	{
		var character = e.Character;

		Send.ZC_NORMAL.CaptionRatios(character);
		Send.ZC_NORMAL.CaptionOverrides(character);
		Send.ZC_NORMAL.JobCircles(character);
	}
}
