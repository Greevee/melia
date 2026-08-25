//--- Melia Script ----------------------------------------------------------
// Caption push delivery for the Laima client
//--- Description -----------------------------------------------------------
// The caption ratio push normally travels as the LAIMA_CAPTION_RATIOS addon
// message, whose client-side receiver is registered by the changejobbutton
// addon. That addon has autoopen=false behind an opencheck, so on servers
// where the check stays false it never initializes, nothing registers the
// message, and every #{CaptionRatio}#/#{CaptionTime}# token backed by the
// generic LAIMA caption functions renders as 0.
//
// This script therefore also delivers the data through ZC_EXEC_CLIENT_SCP,
// calling LAIMA_SET_CAPTION_RATIOS (defined in the always-loaded
// shared.ipf/script/calc_property_skill.lua) directly - no addon required.
// Runs on load complete and on every map change; harmless to repeat, the
// client table replaces per skill id. The stock addon-message push from
// CZ_GAME_READY stays untouched for clients where the addon does listen.
//---------------------------------------------------------------------------

using System.Globalization;
using System.Text;
using Melia.Shared.Scripting;
using Melia.Zone;
using Melia.Zone.Events.Arguments;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;
using Yggdrasil.Logging;

public class CaptionPushCompatScript : GeneralScript
{
	// Keeps the full exec script safely below the client script size limit.
	private const int ChunkLength = 1500;

	[On("PlayerLoadComplete")]
	protected void OnPlayerLoadComplete(object sender, PlayerEventArgs e)
	{
		var character = e.Character;

		var sb = new StringBuilder();
		var chunks = 0;

		foreach (var data in ZoneServer.Instance.Data.SkillDb.Entries.Values)
		{
			if (data.CaptionRatio1 == 0 && data.CaptionRatio1ByLevel == 0
				&& data.CaptionRatio2 == 0 && data.CaptionRatio2ByLevel == 0
				&& data.CaptionRatio3 == 0 && data.CaptionRatio3ByLevel == 0
				&& data.CaptionTime == 0 && data.CaptionTimeByLevel == 0)
				continue;

			if (sb.Length > 0)
				sb.Append(' ');

			sb.Append((int)data.Id).Append(':')
				.Append(F(data.CaptionRatio1)).Append(':').Append(F(data.CaptionRatio1ByLevel)).Append(':').Append(F(data.CaptionRatio1Max)).Append(':')
				.Append(F(data.CaptionRatio2)).Append(':').Append(F(data.CaptionRatio2ByLevel)).Append(':').Append(F(data.CaptionRatio2Max)).Append(':')
				.Append(F(data.CaptionRatio3)).Append(':').Append(F(data.CaptionRatio3ByLevel)).Append(':').Append(F(data.CaptionRatio3Max)).Append(':')
				.Append(F(data.CaptionTime)).Append(':').Append(F(data.CaptionTimeByLevel));

			if (sb.Length >= ChunkLength)
			{
				this.SendChunk(character, sb.ToString());
				sb.Clear();
				chunks++;
			}
		}

		if (sb.Length > 0)
		{
			this.SendChunk(character, sb.ToString());
			chunks++;
		}

		Log.Debug("CaptionPushCompat: sent {0} caption chunk(s) to {1}.", chunks, character.Name);
	}

	private void SendChunk(Character character, string chunk)
	{
		// Only digits, ':', '.', '-' and spaces - safe inside a Lua string.
		character.ExecuteClientScript("LAIMA_SET_CAPTION_RATIOS('" + chunk + "')");
	}

	private static string F(float value)
		=> value.ToString("0.###", CultureInfo.InvariantCulture);
}
