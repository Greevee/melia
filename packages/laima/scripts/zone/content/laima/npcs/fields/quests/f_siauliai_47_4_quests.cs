//--- Melia Script ----------------------------------------------------------
// Baron Allerno - Quest NPCs
//--- Description -----------------------------------------------------------
// Quest NPCs and content for f_siauliai_47_4 map.
//---------------------------------------------------------------------------

using System;
using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Characters.Components;
using Melia.Zone.World.Actors.Monsters;
using Melia.Zone.World.Quests;
using Melia.Zone.World.Quests.Objectives;
using Melia.Zone.World.Quests.Prerequisites;
using Melia.Zone.World.Quests.Rewards;
using Yggdrasil.Util;
using static Melia.Zone.Scripting.Shortcuts;

public class FSiauliai474QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// =====================================================================
		// QUEST 1001: Mages in the Orchards
		// =====================================================================
		// Steward Juozapas - The Baron's orchards overrun by Spion Mages
		//---------------------------------------------------------------------
		AddNpc(20125, L("[Steward] Juozapas"), "f_siauliai_47_4", 2454, -772, 270, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_siauliai_47_4", 1001);

			dialog.SetTitle(L("Juozapas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A tall, thin man in a frayed but well-kept coat looks out over an orchard gone wild*{/}"));
				await dialog.Msg(L("Twenty years I served Baron Allerno. Twenty years keeping these pear orchards trimmed. Now they're a nest for Spion Mages - hedge-wizards who lost their minds to the Orange blight that's been creeping up from Gytis."));

				var response = await dialog.Select(L("The Gytis farmhands come north to forage and the Mages burn them where they stand. I've got no soldiers left to send - the Baron's guard was disbanded when he disappeared."),
					Option(L("I'll kill the Mages"), "help"),
					Option(L("Where is the Baron?"), "info"),
					Option(L("Not my estate to defend"), "leave")
				);

				switch (response)
				{
					case "help":
						await dialog.Msg(L("{#666666}*Something steady comes back into his voice*{/}"));

						character.Quests.Start(questId);
						await dialog.Msg(L("Watch their spellwork - mostly Earth stuff. Stonetraps, rootbinds. Keep moving, don't stand still."));
						await dialog.Msg(L("And if you find the Baron's old rod in their nests, bring it back. He carried one a lot like it."));
						break;

					case "info":
						await dialog.Msg(L("Marched out to the demon war and never came back. No body. No letter. Just... gone."));
						await dialog.Msg(L("I still light the lamps in the manor every dusk, just in case. Call it foolish if you want. I'd rather keep lighting them than stop and be wrong about it."));
						break;

					case "leave":
						await dialog.Msg(L("Fair enough. A stranger's kindness was already more than I'd hoped for."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killSpionMage", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("{#666666}*He listens to the quiet of the orchard for a long moment*{/}"));
					await dialog.Msg(L("You can hear the wind through the pear leaves again. I'd forgotten what that sounded like."));
					await dialog.Msg(L("Take this - an old threshing-rod from the manor armory. The Baron kept one for sentimental reasons. He'd want it used, not rotting on a wall."));

					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Middle orchards and the western pear-row. Fifteen Mages. Keep moving between their binds."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The Gytis farmhands have started foraging north again. They wave when they pass the manor gate."));
			}
		});

		// =====================================================================
		// QUEST 1002: Rent Ledger Dispatch
		// =====================================================================
		// Bookkeeper Urte - Soft-worded rent dispatch
		//---------------------------------------------------------------------
		AddNpc(20161, L("[Bookkeeper] Urte"), "f_siauliai_47_4", 207, -143, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_siauliai_47_4", 1002);

			dialog.SetTitle(L("Urte"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A bookkeeper at a leather-bound ledger marks a column with a small checkmark*{/}"));
				await dialog.Msg(L("The Baron's estate runs on goodwill and old contracts these days. The tenant at the northern farm hasn't paid rent in six months. I'd like to know why before the magistrates do."));

				var response = await dialog.Select(L("Tenant Rokas is a decent man. If he's fallen on hard times, I'll note that in the ledger. If he just forgot, a polite reminder will shake loose the silver. Take this dispatch to him - it's a gentle one, I promise."),
					Option(L("I'll deliver it"), "help"),
					Option(L("Why do you still collect rent?"), "info"),
					Option(L("Sounds like landlord's work"), "leave")
				);

				switch (response)
				{
					case "help":
						await dialog.Msg(L("{#666666}*She folds the dispatch and hands it over*{/}"));

						character.Quests.Start(questId);
						await dialog.Msg(L("Don't push him if he's embarrassed. Just note what he says."));
						await dialog.Msg(L("And if he asks after the Baron - tell him the lamps are still lit."));
						break;

					case "info":
						await dialog.Msg(L("Because when the Baron comes back, he'll ask. I'd like the ledger ready when he does."));
						await dialog.Msg(L("The tenants have kept faith in their own way. I keep mine in books."));
						break;

					case "leave":
						await dialog.Msg(L("Then I'll send it by magpie and hope the bird can read."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("deliverDispatch", out var deliverObj)) return;

				if (deliverObj.Done)
				{
					await dialog.Msg(L("{#666666}*She writes Rokas's reply into the margin*{/}"));
					await dialog.Msg(L("Barley blight, not forgetfulness. Good. I'll mark it as paid-in-kind with next season's harvest."));
					await dialog.Msg(L("Thank you. Take these - honest pay for an honest walk."));

					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Rokas is at the northern warp - Tenants' Farm side. Give him the dispatch and bring back his reply."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Rokas sent barley for next season, as promised. The ledger is balanced for another year."));
			}
		});

		// =====================================================================
		// Tenant Rokas - Recipient for Quest 1002
		//---------------------------------------------------------------------
		AddNpc(20151, L("[Tenant] Rokas"), "f_siauliai_47_4", 653, 1595, 180, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_siauliai_47_4", 1002);

			dialog.SetTitle(L("Rokas"));

			if (!character.Quests.IsActive(questId))
			{
				await dialog.Msg(L("{#666666}*A weather-worn tenant farmer beside a field of yellow-tinged barley*{/}"));
				await dialog.Msg(L("The blight's in the soil now. I don't know what to do."));
				return;
			}

			var delivered = character.Variables.Perm.GetInt("Laima.Quests.f_siauliai_47_4.Quest1002.Delivered", 0) >= 1;

			if (delivered)
			{
				await dialog.Msg(L("Tell Urte thank you. I'll send the barley - what's good of it - with next season's harvest."));
				return;
			}

			await dialog.Msg(L("{#666666}*He reads the dispatch through twice*{/}"));
			await dialog.Msg(L("Tell Urte... tell her it's barley blight. Half my crop is yellow on the stem. I haven't sent coin because I don't have any. Plain as that."));
			await dialog.Msg(L("{#666666}*He glances toward the manor in the distance*{/}"));
			await dialog.Msg(L("Tell her I'll send whatever I can salvage at the autumn harvest. And tell her I'm grateful the Baron's people still remember us."));
			await dialog.Msg(L("The lamps still lit at dusk? Tell her that means more to me than the rent ever did."));

			character.Variables.Perm.Set("Laima.Quests.f_siauliai_47_4.Quest1002.Delivered", 1);
			character.ServerMessage(L("{#FFD700}Dispatch delivered. Return to Bookkeeper Urte.{/}"));
		});

		// =====================================================================
		// QUEST 1003: Stormfeathers
		// =====================================================================
		// Tinker Dovas - Lightning feathers for insect-lure traps
		//---------------------------------------------------------------------
		AddNpc(20117, L("[Tinker] Dovas"), "f_siauliai_47_4", -2100, -150, 90, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_siauliai_47_4", 1003);

			dialog.SetTitle(L("Dovas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A tinker working a soldering iron over a small brass contraption*{/}"));
				await dialog.Msg(L("Hold this - no, gently! The trick's in the static charge. One Hamming feather plus a copper coil, and you've got an insect-lure that'll save a farmer an entire field."));

				var response = await dialog.Select(L("The Myrkiti folk are losing cabbages to hook-weevils. My traps work fine, but I'm out of stormfeathers. The Orange Hammings here in Allerno drop the best ones - their feathers hold a charge for a full week."),
					Option(L("I'll gather five stormfeathers"), "help"),
					Option(L("Why the Orange ones?"), "info"),
					Option(L("Sounds like your problem"), "leave")
				);

				switch (response)
				{
					case "help":
						await dialog.Msg(L("{#666666}*He grins and sets the solder aside*{/}"));

						character.Quests.Start(questId);
						await dialog.Msg(L("Wear gloves if you have them - the static bites."));
						await dialog.Msg(L("And don't go after the Hammings. They're prickly birds, but they molt on their own. We just want what they've already dropped."));
						break;

					case "info":
						await dialog.Msg(L("The orange ones roost in the pear orchards. Pear sap mixes with something in the soil - I think it's the Gytis corruption - and their feathers come out denser, more charged than normal."));
						await dialog.Msg(L("Bad for the birds, good for my traps. Not a trade I'd pick, but there it is."));
						break;

					case "leave":
						await dialog.Msg(L("Then duck out of the way while I solder. Sparks fly."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				var featherCount = character.Inventory.CountItem(667174);

				if (featherCount >= 5)
				{
					await dialog.Msg(L("{#666666}*He holds a feather near a copper disk and a faint spark jumps*{/}"));
					await dialog.Msg(L("Five clean feathers, all charged. I'll have fifteen traps soldered by week's end."));
					await dialog.Msg(L("Take these. Myrkiti's headman gave me a purse for whoever helped."));

					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Five nests, across the estate. Look for shed Hamming down at the bush-bases."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Myrkiti's cabbages are safe through autumn. I owe you a working trap - remind me when I'm not elbow-deep in solder."));
			}
		});

		// =====================================================================
		// HAMMING BUSH-NESTS
		// =====================================================================
		// For Quest 1003 - Stormfeathers
		// =====================================================================

		void AddStormfeatherNest(int nestNumber, int x, int z, int direction)
		{
			AddNpc(152018, L("Hamming Bush-Nest"), "f_siauliai_47_4", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_siauliai_47_4", 1003);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A thicket with orange down scattered at its base. A faint static hum rises from the nest*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_siauliai_47_4.Quest1003.Nest{nestNumber}";
				var gathered = character.Variables.Perm.GetBool(variableKey, false);

				if (gathered)
				{
					await dialog.Msg(L("{#666666}*The longest feathers are already taken. Only wisps of down remain*{/}"));
					return;
				}

				var spawnedKey = $"Laima.Quests.f_siauliai_47_4.Quest1003.Nest{nestNumber}.Spawned";
				var hasSpawned = character.Variables.Perm.GetBool(spawnedKey, false);
				if (!hasSpawned && RandomProvider.Get().Next(100) < 15)
				{
					character.Variables.Perm.Set(spawnedKey, true);

					if (SpawnTempMonsters(character, MonsterId.Popolion_Orange, 1, 70, TimeSpan.FromMinutes(1)))
					{
						character.ServerMessage(L("{#FF6666}A curious Popolion bounces toward the nest!{/}"));
					}
				}

				var result = await character.TimeActions.StartAsync(L("Plucking feather..."), "Cancel", "SITGROPE", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Inventory.Add(667174, 1, InventoryAddType.PickUp);
					character.Variables.Perm.Set(variableKey, true);

					var currentCount = character.Inventory.CountItem(667174);
					character.ServerMessage(LF("Stormfeathers gathered: {0}/5", currentCount));

					if (currentCount >= 5)
					{
						character.ServerMessage(L("{#FFD700}All feathers gathered! Return to Tinker Dovas.{/}"));
					}
				}
				else
				{
					character.ServerMessage(L("Gathering interrupted."));
				}
			});
		}

		AddStormfeatherNest(1, 1590, -283, 0);
		AddStormfeatherNest(2, -552, -1425, 90);
		AddStormfeatherNest(3, 734, -286, 180);
		AddStormfeatherNest(4, 1244, -809, 270);
		AddStormfeatherNest(5, 408, -443, 0);

		// =====================================================================
		// QUEST 1004: The Manor's Lost Rooms
		// =====================================================================
		// Housekeeper Lina - Cataloging the estate outbuildings
		//---------------------------------------------------------------------
		AddNpc(147419, L("[Housekeeper] Lina"), "f_siauliai_47_4", -491, -1456, 45, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_siauliai_47_4", 1004);

			dialog.SetTitle(L("Lina"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*An elder housekeeper with a brass ring of keys, each labeled with faded ink*{/}"));
				await dialog.Msg(L("The magistrate's office sent word. If the Baron isn't found by next season, the estate goes to the Crown's rolls as abandoned. I want a proper record before that."));

				var response = await dialog.Select(L("Four outbuildings still stand on the grounds - the greenhouse, the stables, the library ruin, and the summer pavilion. Each one has things the Baron loved. I want someone to walk them and note what's still there. Before strangers come."),
					Option(L("I'll catalog the outbuildings"), "help"),
					Option(L("Why not walk them yourself?"), "info"),
					Option(L("Just let the Crown take it"), "leave")
				);

				switch (response)
				{
					case "help":
						await dialog.Msg(L("{#666666}*She hands over the key-ring*{/}"));

						character.Quests.Start(questId);
						await dialog.Msg(L("Start wherever suits you. The greenhouse is furthest south, the pavilion nearest the Gytis warp."));
						await dialog.Msg(L("And if you find anything fragile, leave it. A list is enough. I'm not asking you to carry forty years of a man's life around in your pockets."));
						break;

					case "info":
						await dialog.Msg(L("My knees. And my heart, honestly. Every room is him - his books, his dried flowers, the brass telescope he never got around to fixing."));
						await dialog.Msg(L("I can catalog what someone tells me is there. I can't walk it myself yet."));
						break;

					case "leave":
						await dialog.Msg(L("No. Not yet. Forty years of lit lamps doesn't end with a shrug."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				var roomsVisited = character.Variables.Perm.GetInt("Laima.Quests.f_siauliai_47_4.Quest1004.RoomsVisited", 0);

				if (roomsVisited >= 4)
				{
					await dialog.Msg(L("{#666666}*She listens to your list, nodding at each item as if checking it against memory*{/}"));
					await dialog.Msg(L("The telescope's still on the library shelf. The orchid frames. The mandolin in the pavilion."));
					await dialog.Msg(L("{#666666}*She catches herself getting emotional and waves it off*{/}"));
					await dialog.Msg(L("Enough of that. Here - small thanks. You walked the goodbye I couldn't make myself walk alone."));

					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Four outbuildings. Greenhouse, stables, library, pavilion. Walk slow. Note what's there."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("I walked the pavilion myself yesterday. Easier, knowing what I'd find. Thank you."));
			}
		});

		// =====================================================================
		// ESTATE LANDMARKS
		// =====================================================================
		// For Quest 1004 - The Manor's Lost Rooms
		// =====================================================================

		void AddEstateLandmark(int landmarkNumber, string landmarkName, string observation, int x, int z, int direction)
		{
			AddNpc(147501, L(landmarkName), "f_siauliai_47_4", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_siauliai_47_4", 1004);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A weathered estate landmark, quiet under the open sky*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_siauliai_47_4.Quest1004.Room{landmarkNumber}";
				var cataloged = character.Variables.Perm.GetBool(variableKey, false);

				if (cataloged)
				{
					await dialog.Msg(L("{#666666}*You've already cataloged this outbuilding*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Cataloging..."), "Cancel", "SITGROPE", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var roomsVisited = character.Variables.Perm.GetInt("Laima.Quests.f_siauliai_47_4.Quest1004.RoomsVisited", 0);
					character.Variables.Perm.Set("Laima.Quests.f_siauliai_47_4.Quest1004.RoomsVisited", roomsVisited + 1);

					character.ServerMessage(L(observation));
					character.ServerMessage(LF("Outbuildings cataloged: {0}/4", roomsVisited + 1));

					if (roomsVisited + 1 >= 4)
					{
						character.ServerMessage(L("{#FFD700}All rooms cataloged! Return to Housekeeper Lina.{/}"));
					}
				}
				else
				{
					character.ServerMessage(L("Cataloging interrupted."));
				}
			});
		}

		AddEstateLandmark(1, "Greenhouse", "Orchid frames intact. Three glass panes missing. Smell of warm earth.", -350, -1550, 0);
		AddEstateLandmark(2, "Stable Ruins", "Stalls empty but swept. A folded saddle blanket, moth-chewed, tucked in a corner.", 1250, -830, 90);
		AddEstateLandmark(3, "Library Ruin", "Brass telescope still on the shelf. Bookcases burned; one wall of books survived.", 500, -10, 180);
		AddEstateLandmark(4, "Summer Pavilion", "Mandolin on a bench. Dried flowers in a vase, pressed and labeled in a careful hand.", 2420, -1330, 270);
	}
}

//-----------------------------------------------------------------------------
// QUEST DEFINITIONS
//-----------------------------------------------------------------------------

// Quest 1001 CLASS: Mages in the Orchards
//-----------------------------------------------------------------------------

public class MagesInTheOrchardsQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_siauliai_47_4", 1001);
		SetName("Mages in the Orchards");
		SetType(QuestType.Sub);
		SetDescription("Steward Juozapas needs the Spion Mages cleared from the Baron's orchards - hedge-wizards driven feral by the Orange blight creeping up from Gytis.");
		SetLocation("f_siauliai_47_4");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver("[Steward] Juozapas", "f_siauliai_47_4");

		AddObjective("killSpionMage", "Defeat Spion Mages",
			new KillObjective(15, new[] { MonsterId.Spion_Mage }));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));  // Lv3 EXP Card
		AddReward(new ItemReward(640003, 2)); // Normal HP Potion
		AddReward(new ItemReward(640006, 2)); // Normal SP Potion
		AddReward(new ItemReward(640009, 1));  // Stamina Potion
		AddReward(new ItemReward(142119, 1));  // Thresh Rod
	}
}

// Quest 1002 CLASS: Rent Ledger Dispatch
//-----------------------------------------------------------------------------

public class RentLedgerDispatchQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_siauliai_47_4", 1002);
		SetName("Rent Ledger Dispatch");
		SetType(QuestType.Sub);
		SetDescription("Bookkeeper Urte needs a soft-worded rent dispatch delivered to Tenant Rokas at the northern farm warp.");
		SetLocation("f_siauliai_47_4");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver("[Bookkeeper] Urte", "f_siauliai_47_4");

		AddObjective("deliverDispatch", "Deliver the dispatch to Tenant Rokas",
			new VariableCheckObjective("Laima.Quests.f_siauliai_47_4.Quest1002.Delivered", 1, true));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));  // Lv3 EXP Card
		AddReward(new ItemReward(640003, 2)); // Normal HP Potion
		AddReward(new ItemReward(640006, 2)); // Normal SP Potion
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_siauliai_47_4.Quest1002.Delivered");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_siauliai_47_4.Quest1002.Delivered");
	}
}

// Quest 1003 CLASS: Stormfeathers
//-----------------------------------------------------------------------------

public class StormfeathersQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_siauliai_47_4", 1003);
		SetName("Stormfeathers");
		SetType(QuestType.Sub);
		SetDescription("Tinker Dovas needs five stormfeathers from Orange Hamming bush-nests to build insect-lure traps for Myrkiti's farmers.");
		SetLocation("f_siauliai_47_4");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver("[Tinker] Dovas", "f_siauliai_47_4");

		AddObjective("collectFeathers", "Gather stormfeathers from Hamming bush-nests",
			new CollectItemObjective(667174, 5));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));  // Lv3 EXP Card
		AddReward(new ItemReward(640003, 2)); // Normal HP Potion
		AddReward(new ItemReward(640006, 2)); // Normal SP Potion
		AddReward(new ItemReward(640009, 1));  // Stamina Potion
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(667174,
			character.Inventory.CountItem(667174),
			InventoryItemRemoveMsg.Destroyed);

		for (int i = 1; i <= 5; i++)
		{
			character.Variables.Perm.Remove($"Laima.Quests.f_siauliai_47_4.Quest1003.Nest{i}");
			character.Variables.Perm.Remove($"Laima.Quests.f_siauliai_47_4.Quest1003.Nest{i}.Spawned");
		}
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(667174,
			character.Inventory.CountItem(667174),
			InventoryItemRemoveMsg.Destroyed);

		for (int i = 1; i <= 5; i++)
		{
			character.Variables.Perm.Remove($"Laima.Quests.f_siauliai_47_4.Quest1003.Nest{i}");
			character.Variables.Perm.Remove($"Laima.Quests.f_siauliai_47_4.Quest1003.Nest{i}.Spawned");
		}
	}
}

// Quest 1004 CLASS: The Manor's Lost Rooms
//-----------------------------------------------------------------------------

public class TheManorsLostRoomsQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_siauliai_47_4", 1004);
		SetName("The Manor's Lost Rooms");
		SetType(QuestType.Sub);
		SetDescription("Housekeeper Lina has asked you to catalog the four outbuildings of Baron Allerno's estate before the magistrate's office declares it abandoned.");
		SetLocation("f_siauliai_47_4");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver("[Housekeeper] Lina", "f_siauliai_47_4");

		AddObjective("visitRooms", "Catalog the estate outbuildings",
			new VariableCheckObjective("Laima.Quests.f_siauliai_47_4.Quest1004.RoomsVisited", 4, true));

		AddReward(new ExpReward(3100, 2200));
		AddReward(new SilverReward(3800));
		AddReward(new ItemReward(640082, 2));  // Lv3 EXP Card
		AddReward(new ItemReward(640003, 2)); // Normal HP Potion
		AddReward(new ItemReward(640006, 2)); // Normal SP Potion
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_siauliai_47_4.Quest1004.RoomsVisited");
		for (int i = 1; i <= 4; i++)
		{
			character.Variables.Perm.Remove($"Laima.Quests.f_siauliai_47_4.Quest1004.Room{i}");
		}
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_siauliai_47_4.Quest1004.RoomsVisited");
		for (int i = 1; i <= 4; i++)
		{
			character.Variables.Perm.Remove($"Laima.Quests.f_siauliai_47_4.Quest1004.Room{i}");
		}
	}
}
