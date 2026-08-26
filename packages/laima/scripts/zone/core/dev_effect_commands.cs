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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Melia.Shared.Game.Const;
using Melia.Shared.World;
using Melia.Zone.Scripting;
using Yggdrasil.Util.Commands;
using Melia.Zone.World.Actors;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Monsters;
using static Melia.Zone.Scripting.Shortcuts;

public class DevEffectCommandsScript : GeneralScript
{
	protected override void Load()
	{
		AddChatCommand("playeffect", "<effect_name> [scale]", "Plays an actor-bound effect on you (moves with the character).", 0, 99, this.HandlePlayEffect);
		AddChatCommand("playeffectnode", "<effect_name> <node> [value]", "Attaches an effect to a skeleton node (e.g. Dummy_R_HAND).", 0, 99, this.HandlePlayEffectNode);
		AddChatCommand("dummy", "[count] [mhp]", "Spawns training dummies around you (huge HP, no attack, no defense).", 0, 99, this.HandleDummy);
		AddChatCommand("cleardummies", "", "Removes all training dummies you spawned on this map.", 0, 99, this.HandleClearDummies);
	}

	/// <summary>
	/// Wood carving training dummy (fire variant); a prop monster with no
	/// AI of its own.
	/// </summary>
	private const int DummyMonsterId = 57253;

	private static readonly List<Mob> SpawnedDummies = new();

	private CommandResult HandleDummy(Character sender, Character target, string message, string commandName, Arguments args)
	{
		var count = 3;
		var mhp = 9999999f;

		if (args.Count > 0 && int.TryParse(args.Get(0), out var c))
			count = Math.Clamp(c, 1, 10);
		if (args.Count > 1 && float.TryParse(args.Get(1), NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
			mhp = Math.Max(1, h);

		// Zero attack and defense: they can't fight back, and damage tests
		// read clean, undefended numbers.
		var overrides = Properties("MHP", mhp, "MINPATK", 0, "MAXPATK", 0, "MINMATK", 0, "MAXMATK", 0, "DEF", 0, "MDEF", 0);

		for (var i = 0; i < count; i++)
		{
			var pos = target.Position.GetRelative(new Direction(i * (360.0 / count)), 50f);

			var dummy = new Mob(DummyMonsterId, RelationType.Enemy);
			dummy.Position = pos;
			dummy.ApplyOverrides(overrides);

			target.Map.AddMonster(dummy);
			SpawnedDummies.Add(dummy);
		}

		sender.ServerMessage($"Spawned {count} training dummies ({mhp:0} HP).");
		return CommandResult.Okay;
	}

	private CommandResult HandleClearDummies(Character sender, Character target, string message, string commandName, Arguments args)
	{
		var removed = 0;

		foreach (var dummy in SpawnedDummies.ToList())
		{
			if (dummy.Map != target.Map)
				continue;

			dummy.Map.RemoveMonster(dummy);
			SpawnedDummies.Remove(dummy);
			removed++;
		}

		sender.ServerMessage($"Removed {removed} training dummies.");
		return CommandResult.Okay;
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
