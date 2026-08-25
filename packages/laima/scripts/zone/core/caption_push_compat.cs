//--- Melia Script ----------------------------------------------------------
// Caption push compatibility for pre-500 protocol clients
//--- Description -----------------------------------------------------------
// CZ_GAME_READY only sends the caption ratio/override push and the job
// circles to protocol >= 500 clients. The Laima client (390044) is older,
// so without this every #{CaptionRatio}#/#{CaptionTime}# token backed by
// the generic LAIMA caption functions renders as 0.
//
// Re-sends the push once the client has fully loaded (CZ_LOAD_COMPLETE),
// when the addons are guaranteed to be up to receive the addon messages.
// Fires again on map changes, which is harmless: the client tables replace
// per skill id. Newer clients already got the push in CZ_GAME_READY and
// are skipped.
//---------------------------------------------------------------------------

using Melia.Shared.Scripting;
using Melia.Shared.Versioning;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Network;
using Melia.Zone.Scripting;

public class CaptionPushCompatScript : GeneralScript
{
	[On("PlayerLoadComplete")]
	protected void OnPlayerLoadComplete(object sender, PlayerEventArgs e)
	{
		if (Versions.Protocol >= 500)
			return;

		var character = e.Character;

		Send.ZC_NORMAL.CaptionRatios(character);
		Send.ZC_NORMAL.CaptionOverrides(character);
		Send.ZC_NORMAL.JobCircles(character);
	}
}
