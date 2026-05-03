//--- Melia Script ----------------------------------------------------------
// Southern Parias Forest Quest NPCs
//--- Description -----------------------------------------------------------
// Quests for Southern Parias Forest (f_maple_24_2).
//---------------------------------------------------------------------------

using System;
using Melia.Shared.Game.Const;
using Melia.Zone.Scripting;
using Melia.Zone.World.Quests;
using Melia.Zone.World.Actors.Characters;
using Melia.Zone.World.Actors.Characters.Components;
using Melia.Zone.World.Quests.Objectives;
using Melia.Zone.World.Quests.Prerequisites;
using Melia.Zone.World.Quests.Rewards;
using Yggdrasil.Util;
using static Melia.Zone.Scripting.Shortcuts;
using Melia.Zone.World.Actors;

public class FMaple242QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// Quest 1: Zeuni Thinning
		//-------------------------------------------------------------------------
		AddNpc(20060, L("[Ranger] Aurimas"), "f_maple_24_2", -1100, -550, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_2", 1001);

			dialog.SetTitle(L("Aurimas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Zeuni Kucarries are crowding the southern ridge. Kill forty-five and the trail will open back up."));

				var response = await dialog.Select(L("Will you open the trail for us?"),
					Option(L("I'll thin"), "help"),
					Option(L("Trail?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Forty-five. Watch out for the burrows."));
						break;

					case "info":
						await dialog.Msg(L("The southern trail goes to Parias proper, but it's been blocked since the pack moved in."));
						break;

					case "leave":
						await dialog.Msg(L("Then the trail stays shut."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killZeuni", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("Trail's walkable now."));
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep at it."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The scouts moved through yesterday."));
			}
		});

		// Quest 2: Numani Pelts
		//-------------------------------------------------------------------------
		AddNpc(20114, L("[Tanner] Daiva"), "f_maple_24_2", -400, -500, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_2", 1002);

			dialog.SetTitle(L("Daiva"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Numani pelts get nice and thick against the cold. Kill thirty, bring me eight clean pelts."));

				var response = await dialog.Select(L("Will you bring me the pelts?"),
					Option(L("I'll bring"), "help"),
					Option(L("Clean?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Cut along the flank, not the spine, or you'll ruin them."));
						break;

					case "info":
						await dialog.Msg(L("No tears, no burrs. I mean clean."));
						break;

					case "leave":
						await dialog.Msg(L("Folks'll have thin coats this winter, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killNumani", out var killObj)) return;
				if (!quest.TryGetProgress("gatherPelts", out var pObj)) return;

				if (killObj.Done && pObj.Done)
				{
					await dialog.Msg(L("Eight pelts. I'll have the coats done by end of the week."));
					character.Inventory.Remove(650244, character.Inventory.CountItem(650244), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep hunting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Village will stay warm this winter."));
			}
		});

		// Quest 3: Zabbi Fangs
		//-------------------------------------------------------------------------
		AddNpc(20116, L("[Alchemist] Margarita"), "f_maple_24_2", 900, 700, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_2", 1003);

			dialog.SetTitle(L("Margarita"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("I grind Zabbi fangs into fever-salve. Kill fifteen and bring me five matched pairs."));

				var response = await dialog.Select(L("Will you bring me the fangs?"),
					Option(L("I'll bring"), "help"),
					Option(L("Salve?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Paired means both fangs from the same jaw."));
						break;

					case "info":
						await dialog.Msg(L("Crushed fang breaks marsh-fever. Two farmsteads are down with it already."));
						break;

					case "leave":
						await dialog.Msg(L("More folks'll catch the fever, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killZabbi", out var killObj)) return;
				if (!quest.TryGetProgress("gatherFangs", out var fObj)) return;

				if (killObj.Done && fObj.Done)
				{
					await dialog.Msg(L("Five pairs. I'll have the salve ready by nightfall."));
					character.Inventory.Remove(650246, character.Inventory.CountItem(650246), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep hunting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Both farmsteads are up and walking again."));
			}
		});

		// Quest 4: Crystal Resonance
		//-------------------------------------------------------------------------
		AddNpc(20117, L("[Surveyor] Linas"), "f_maple_24_2", -250, 450, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_2", 1004);

			dialog.SetTitle(L("Linas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("The Rootcrystals are humming wrong on the southern grid. Break twelve and bring me eight resonant slivers."));

				var response = await dialog.Select(L("Will you bring me the slivers?"),
					Option(L("I'll break"), "help"),
					Option(L("Wrong?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Wrap them up separately. They sing to each other if you don't."));
						break;

					case "info":
						await dialog.Msg(L("The grid tone dropped a semitone last month. Something's going on underneath."));
						break;

					case "leave":
						await dialog.Msg(L("Then the grid keeps drifting."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("breakCrystals", out var killObj)) return;
				if (!quest.TryGetProgress("gatherSlivers", out var sObj)) return;

				if (killObj.Done && sObj.Done)
				{
					await dialog.Msg(L("Eight slivers. Now I can map the shift properly."));
					character.Inventory.Remove(650247, character.Inventory.CountItem(650247), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep breaking them."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Turns out it's a bore, not a drift. I sent for a digging team."));
			}
		});

		// Quest 5: The Pack Elder
		//-------------------------------------------------------------------------
		AddNpc(47245, L("[Bounty Hunter] Mantas"), "f_maple_24_2", 1300, -50, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_2", 1005);
			var elderSpawnedKey = "Laima.Quests.f_maple_24_2.Quest1005.ElderSpawned";

			dialog.SetTitle(L("Mantas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("There's a Zabbi Pack-Elder leading the southern Kucarries. Kill ten of them and he'll come out of the den."));

				var response = await dialog.Select(L("Want the contract?"),
					Option(L("I'll face him"), "help"),
					Option(L("Pack-Elder?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Ten of them. He'll come."));
						break;

					case "info":
						await dialog.Msg(L("He's the one calling the moves. Take him out and the pack scatters."));
						break;

					case "leave":
						await dialog.Msg(L("Pack stays put, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killPack", out var pObj)) return;
				if (!quest.TryGetProgress("killElder", out var eObj)) return;

				if (pObj.Done && eObj.Done)
				{
					await dialog.Msg(L("Pack will scatter by tonight."));
					character.Variables.Perm.Remove(elderSpawnedKey);
					character.Quests.Complete(questId);
				}
				else if (pObj.Done && !eObj.Done)
				{
					var hasSpawned = character.Variables.Perm.GetBool(elderSpawnedKey, false);
					if (!hasSpawned)
					{
						character.Variables.Perm.Set(elderSpawnedKey, true);
						if (SpawnTempMonsters(character, MonsterId.Kucarry_Zabbi, 1, 150, TimeSpan.FromMinutes(5)))
						{
							await dialog.Msg(L("Here he comes!"));
							character.ServerMessage(L("{#FF9966}The Pack-Elder bursts from the den!{/}"));
						}
					}
					else
					{
						await dialog.Msg(L("Go find him."));
					}
				}
				else
				{
					await dialog.Msg(L("Get the ten first."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The den's empty. Scouts confirmed it."));
			}
		});

		// Quest 6: Southern Parias Sweep
		//-------------------------------------------------------------------------
		AddNpc(155146, L("[Militia-Captain] Vaclovas"), "f_maple_24_2", 400, 300, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_2", 1006);

			dialog.SetTitle(L("Vaclovas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Southern sweep. Twelve Zeuni, twelve Numani, twelve Zabbi. Standard work."));

				var response = await dialog.Select(L("Will you sweep the ridge?"),
					Option(L("I'll do it"), "help"),
					Option(L("Pay?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Thirty-six total. Get to it."));
						break;

					case "info":
						await dialog.Msg(L("Fair pay. Standard rate."));
						break;

					case "leave":
						await dialog.Msg(L("Forest stays wild, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killZeuni", out var zObj)) return;
				if (!quest.TryGetProgress("killNumani", out var nObj)) return;
				if (!quest.TryGetProgress("killZabbi", out var bObj)) return;

				if (zObj.Done && nObj.Done && bObj.Done)
				{
					await dialog.Msg(L("All done. Good work."));
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep going."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The militia's patrolling the ridge now."));
			}
		});
	}
}

//-----------------------------------------------------------------------------
// QUEST DEFINITIONS
//-----------------------------------------------------------------------------

public class FMaple242Quest1001 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_2", 1001);
		SetName(L("Zeuni Thinning"));
		SetType(QuestType.Sub);
		SetDescription(L("Thin Zeuni Kucarries blocking the southern trail."));
		SetLocation("f_maple_24_2");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Ranger] Aurimas"), "f_maple_24_2");

		AddObjective("killZeuni", L("Kill Zeuni Kucarries"),
			new KillObjective(45, new[] { MonsterId.Kucarry_Zeuni }));

		AddReward(new ExpReward(1000, 700));
		AddReward(new SilverReward(2200));
		AddReward(new ItemReward(640081, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
	}
}

public class FMaple242Quest1002 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_2", 1002);
		SetName(L("Numani Pelts"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Numani Kucarries and bring clean pelts for winter coats."));
		SetLocation("f_maple_24_2");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Tanner] Daiva"), "f_maple_24_2");

		AddObjective("killNumani", L("Kill Numani Kucarries"),
			new KillObjective(30, new[] { MonsterId.Kucarry_Numani }));

		AddObjective("gatherPelts", L("Gather clean pelts"),
			new CollectItemObjective(650244, 8));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650244, character.Inventory.CountItem(650244), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650244, character.Inventory.CountItem(650244), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple242Quest1003 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_2", 1003);
		SetName(L("Zabbi Fangs"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Zabbi Kucarries and bring paired fangs for fever-salve."));
		SetLocation("f_maple_24_2");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Alchemist] Margarita"), "f_maple_24_2");

		AddObjective("killZabbi", L("Kill Zabbi Kucarries"),
			new KillObjective(15, new[] { MonsterId.Kucarry_Zabbi }));

		AddObjective("gatherFangs", L("Gather paired fangs"),
			new CollectItemObjective(650246, 5));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650246, character.Inventory.CountItem(650246), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650246, character.Inventory.CountItem(650246), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple242Quest1004 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_2", 1004);
		SetName(L("Crystal Resonance"));
		SetType(QuestType.Sub);
		SetDescription(L("Break Rootcrystals to gather resonant slivers for the surveyor."));
		SetLocation("f_maple_24_2");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Surveyor] Linas"), "f_maple_24_2");

		AddObjective("breakCrystals", L("Break Rootcrystals"),
			new KillObjective(12, new[] { MonsterId.Rootcrystal_01 }));

		AddObjective("gatherSlivers", L("Gather resonant slivers"),
			new CollectItemObjective(650247, 8));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650247, character.Inventory.CountItem(650247), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650247, character.Inventory.CountItem(650247), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple242Quest1005 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_2", 1005);
		SetName(L("The Pack Elder"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Zabbi Kucarries to draw out the Pack-Elder leading the southern pack."));
		SetLocation("f_maple_24_2");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Bounty Hunter] Mantas"), "f_maple_24_2");

		AddObjective("killPack", L("Kill Zabbi Kucarries"),
			new KillObjective(10, new[] { MonsterId.Kucarry_Zabbi }));

		AddObjective("killElder", L("Defeat the Pack-Elder"),
			new KillObjective(1, new[] { MonsterId.Kucarry_Zabbi }));

		AddReward(new ExpReward(3100, 2200));
		AddReward(new SilverReward(3800));
		AddReward(new ItemReward(640082, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}
}

public class FMaple242Quest1006 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_2", 1006);
		SetName(L("Southern Parias Sweep"));
		SetType(QuestType.Sub);
		SetDescription(L("Standard sweep of Zeuni, Numani, and Zabbi Kucarries."));
		SetLocation("f_maple_24_2");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Militia-Captain] Vaclovas"), "f_maple_24_2");

		AddObjective("killZeuni", L("Kill Zeuni Kucarries"),
			new KillObjective(12, new[] { MonsterId.Kucarry_Zeuni }));

		AddObjective("killNumani", L("Kill Numani Kucarries"),
			new KillObjective(12, new[] { MonsterId.Kucarry_Numani }));

		AddObjective("killZabbi", L("Kill Zabbi Kucarries"),
			new KillObjective(12, new[] { MonsterId.Kucarry_Zabbi }));

		AddReward(new ExpReward(3100, 2200));
		AddReward(new SilverReward(3800));
		AddReward(new ItemReward(640082, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}
}
