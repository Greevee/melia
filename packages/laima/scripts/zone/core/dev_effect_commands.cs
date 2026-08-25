//--- Melia Script ----------------------------------------------------------
// Dev effect commands
//--- Description -----------------------------------------------------------
// Auditioning tool for actor-bound effects: >testeffect and >addeffect
// cover the position channel and the (broken) attach channel, but nothing
// exposed PlayEffect, the channel that binds an effect to the actor handle
// so it moves with the character. Names must exist in the packet string db.
//
// Careful with looping effects: PlayEffect has no duration, so a looping
// effect plays until relog. One-shot effects end on their own.
//---------------------------------------------------------------------------

using System.Globalization;
using Melia.Zone.Scripting;
using Yggdrasil.Util.Commands;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using static Melia.Zone.Scripting.Shortcuts;

public class DevEffectCommandsScript : GeneralScript
{
	protected override void Load()
	{
		AddChatCommand("playeffect", "<effect_name> [scale]", "Plays an actor-bound effect on you (moves with the character).", 0, 99, this.HandlePlayEffect);
		AddChatCommand("playeffectnode", "<effect_name> <node> [value]", "Attaches an effect to a skeleton node (e.g. Dummy_R_HAND).", 0, 99, this.HandlePlayEffectNode);
	}

	private CommandResult HandlePlayEffectNode(Character sender, Character target, string message, string commandName, Arguments args)
	{
		if (args.Count < 2)
			return CommandResult.InvalidArgument;

		var effectName = args.Get(0);
		var node = args.Get(1);
		var value = 1f;
		if (args.Count > 2)
			float.TryParse(args.Get(2), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

		try
		{
			target.PlayEffectNode(effectName, value, node);
			sender.ServerMessage($"PlayEffectNode '{effectName}' at '{node}' (value {value}).");
		}
		catch
		{
			sender.ServerMessage($"'{effectName}' is not in the packet string db.");
		}

		return CommandResult.Okay;
	}

	private CommandResult HandlePlayEffect(Character sender, Character target, string message, string commandName, Arguments args)
	{
		if (args.Count < 1)
			return CommandResult.InvalidArgument;

		var effectName = args.Get(0);
		var scale = 1f;
		if (args.Count > 1)
			float.TryParse(args.Get(1), NumberStyles.Float, CultureInfo.InvariantCulture, out scale);

		try
		{
			target.PlayEffect(effectName, scale);
			sender.ServerMessage($"PlayEffect '{effectName}' (scale {scale}).");
		}
		catch
		{
			sender.ServerMessage($"'{effectName}' is not in the packet string db.");
		}

		return CommandResult.Okay;
	}
}
