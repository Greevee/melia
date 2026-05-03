//--- Melia Script ----------------------------------------------------------
// Central Parias Forest Quest NPCs
//--- Description -----------------------------------------------------------
// Quests for Central Parias Forest.
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

public class FMaple241QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// Quest 1: Rudas Bloom
		//-------------------------------------------------------------------------
		AddNpc(20060, L("[Florist] Morta"), "f_maple_24_1", 0, 500, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1001);

			dialog.SetTitle(L("Morta"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("The Rudas Elavines are blooming again. Their petals sell well at every flower stall in Klaipeda."));
				await dialog.Msg(L("If you kill twenty-five of them, that should be enough petals for the whole season."));

				var response = await dialog.Select(L("Will you bring me the petals?"),
					Option(L("I'll harvest"), "help"),
					Option(L("Just petals?"), "info"),
					Option(L("Buy roses"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Twenty-five. They only shed petals when they're stressed."));
						break;

					case "info":
						await dialog.Msg(L("The petals don't wilt. Cut flowers die in a day, but Rudas petals keep for a whole month."));
						break;

					case "leave":
						await dialog.Msg(L("Suit yourself. Roses die fast, though."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killRudas", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("My stall will be bright for weeks now."));
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep harvesting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Sold out in three days."));
			}
		});

		// Quest 2: Atti Pollen
		//-------------------------------------------------------------------------
		AddNpc(20117, L("[Beekeeper] Kovas"), "f_maple_24_1", -1100, -600, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1002);

			dialog.SetTitle(L("Kovas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Attis pick up rare pollen on their legs. There are blooms my bees can't reach, but the Attis walk right through them."));
				await dialog.Msg(L("Kill fifteen Attis and bring five pollen-clusters."));

				var response = await dialog.Select(L("Will you bring me the pollen?"),
					Option(L("I'll kill and scrape"), "help"),
					Option(L("Why Attis?"), "info"),
					Option(L("Use honey"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Scrape the pollen off their leg joints. That's where it builds up."));
						break;

					case "info":
						await dialog.Msg(L("They go deep into the grove. My bees stay around the edges."));
						break;

					case "leave":
						await dialog.Msg(L("Honey's only half of what I do, y'know."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killAttis", out var killObj)) return;
				if (!quest.TryGetProgress("gatherPollen", out var pObj)) return;

				if (killObj.Done && pObj.Done)
				{
					await dialog.Msg(L("Five clusters! The hives are going to triple in size."));
					character.Inventory.Remove(650041, character.Inventory.CountItem(650041), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep at it."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Hives are doing great. The queens are laying twice as much."));
			}
		});

		// Quest 3: Delione Thinning
		//-------------------------------------------------------------------------
		AddNpc(20118, L("[Groundskeeper] Dovydas"), "f_maple_24_1", -900, 400, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1003);

			dialog.SetTitle(L("Dovydas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*An old groundskeeper, his rake clogged with Delione petals*{/}"));
				await dialog.Msg(L("Forty years I've been tending these inner paths. I can handle the Deliones on my own, but they're going after my saplings now."));
				await dialog.Msg(L("I planted four sapling-rings last autumn, and the Deliones keep uprooting them faster than they can grow. If they get to them before the saplings make it through their second year, the inner path won't have any trees at all."));

				var response = await dialog.Select(L("Kill 35 Deliones to clear the inner paths, then tend my four sapling-rings. Will you help an old man out?"),
					Option(L("I'll handle the Deliones and the saplings"), "help"),
					Option(L("Why thirty-five?"), "info"),
					Option(L("Find someone else"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*He gives you a waxed-linen apron and a bone trowel*{/}"));
						await dialog.Msg(L("Thirty-five Deliones, no cheating. They nest in clusters of seven, so kill the biggest one first and the rest will scatter."));
						await dialog.Msg(L("The sapling-rings are marked with reed stakes along the inner path. Brush the dirt back over the roots and press it down firm with the trowel. Some folks sing to them while they work - the trees seem to like it."));
						break;

					case "info":
						await dialog.Msg(L("Thirty-five is one for every year I've worked here. Old groundskeeper's habit - I count the work in years."));
						await dialog.Msg(L("The visitors won't notice the math. They'll just see an open path in spring instead of a choked one. That's good enough for me."));
						break;

					case "leave":
						await dialog.Msg(L("Then the inner path goes bare, the visitors stop coming, and Parias won't be Parias anymore. I'm too old to do this alone."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killDeliones", out var killObj)) return;
				if (!quest.TryGetProgress("tendSaplings", out var sObj)) return;

				if (killObj.Done && sObj.Done)
				{
					await dialog.Msg(L("{#666666}*He inspects each sapling-ring in turn*{/}"));
					await dialog.Msg(L("The paths are walkable, the saplings have firm roots. They should hold through the autumn rains."));
					await dialog.Msg(L("Take this. It's not much, but Parias doesn't have a rich treasury. Come back in five years and you'll see the trees you saved."));
					character.Quests.Complete(questId);
				}
				else if (!killObj.Done)
				{
					await dialog.Msg(L("Get the thirty-five Deliones first. No point tending the saplings while the Deliones are still pulling them up."));
				}
				else
				{
					await dialog.Msg(L("Paths are clear. Now go tend the four sapling-rings. Brush the dirt back over the roots, press it down firm. Take your time."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Visitors are back on the inner paths every weekend. I checked this morning - all four saplings have grown a finger's worth since you tended them. Forty-one years of this work and I still measure growth in fingers."));
			}
		});

		// Sapling-ring tend points for Quest 1003
		//-------------------------------------------------------------------------
		void AddSaplingRing(int ringNumber, int x, int z, int direction)
		{
			AddNpc(47190, L("Sapling-Ring"), "f_maple_24_1", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_maple_24_1", 1003);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A reed-staked ring of young saplings, half uprooted*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_maple_24_1.Quest1003.Ring{ringNumber}";
				if (character.Variables.Perm.GetBool(variableKey, false))
				{
					await dialog.Msg(L("{#666666}*Already tended. The saplings stand straight here*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Tending sapling-ring..."), "Cancel", "SITGROPE", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var count = character.Variables.Perm.GetInt("Laima.Quests.f_maple_24_1.Quest1003.RingsTended", 0) + 1;
					character.Variables.Perm.Set("Laima.Quests.f_maple_24_1.Quest1003.RingsTended", count);
					character.ServerMessage(LF("Sapling-rings tended: {0}/4", count));

					if (count >= 4)
						character.ServerMessage(L("{#FFD700}All sapling-rings tended! Return to Groundskeeper Dovydas.{/}"));
				}
				else
				{
					character.ServerMessage(L("Tending interrupted."));
				}
			});
		}

		AddSaplingRing(1, -800, 300, 0);
		AddSaplingRing(2, -1100, 500, 90);
		AddSaplingRing(3, -700, 600, 180);
		AddSaplingRing(4, -1000, 250, 270);

		// Quest 4: Cloverin Clovers
		//-------------------------------------------------------------------------
		AddNpc(20114, L("[Alchemist] Rasa"), "f_maple_24_1", 50, 800, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1004);

			dialog.SetTitle(L("Rasa"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Some Cloverins have a four-leaf clover in their leaf pattern. I need eight of those for my luck potion."));
				await dialog.Msg(L("Kill twenty Cloverins for the lucky clovers."));

				var response = await dialog.Select(L("Will you bring me the lucky clovers?"),
					Option(L("I'll harvest"), "help"),
					Option(L("Does luck work?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Eight is plenty. Don't try for nine, that's just superstition."));
						break;

					case "info":
						await dialog.Msg(L("It works if you believe in it. Like prayer, really."));
						break;

					case "leave":
						await dialog.Msg(L("Your loss."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killCloverins", out var killObj)) return;
				if (!quest.TryGetProgress("gatherClovers", out var cObj)) return;

				if (killObj.Done && cObj.Done)
				{
					await dialog.Msg(L("Eight clovers! I'll brew the potion tonight."));
					character.Inventory.Remove(650051, character.Inventory.CountItem(650051), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep going."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Sold the potion to a gambler. He came back the next day with triple his money."));
			}
		});

		// Quest 5: Elavine Matriarch
		//-------------------------------------------------------------------------
		AddNpc(153142, L("[Druid] Vaiva"), "f_maple_24_1", 1300, -100, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1005);
			var matriarchSpawnedKey = "Laima.Quests.f_maple_24_1.Quest1005.MatriarchSpawned";

			dialog.SetTitle(L("Vaiva"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A druid examining a brittle, grey-veined leaf*{/}"));
				await dialog.Msg(L("There's a Matriarch growing at the heart of Parias. Not a regular Elavine - this one's older, and hungrier. Her rot is spreading through the soil and killing every leaf within half a league."));
				await dialog.Msg(L("Three rot-blooms are feeding her, set up at the north, south, and east points of the grove. Burn them and she won't be able to keep growing. She'll come out furious, and that's when we kill her. Not before."));

				var response = await dialog.Select(L("Burn the three rot-blooms around the heart-grove, then kill 10 of her Rudas Elavine daughters to bait her out. She'll fight once her brood gets thin enough. Will you do it?"),
					Option(L("I'll burn the blooms and draw her"), "help"),
					Option(L("Why kill the daughters?"), "info"),
					Option(L("Leave the grove to its rot"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*She gives you a small clay torch-pot with a pitch-soaked wick*{/}"));
						await dialog.Msg(L("Burn the three rot-blooms first. They're at the north, south, and east of the heart-grove. Touch the torch to the stem, step back, and watch it shrivel. Don't breathe the smoke."));
						await dialog.Msg(L("Then kill ten of her daughters. She'll only come out once she feels her brood thinning. When she shows up, fight her on the stone ring - the rot can't cross stone."));
						break;

					case "info":
						await dialog.Msg(L("The daughters are part of her - she feels each one die. After ten, her instinct to defend what's left overrides her growth, and she comes out."));
						await dialog.Msg(L("It's cruel work, I know. But the grove is in worse shape. The rot's already taken the eastern half. If we don't kill her now, the rest goes by autumn."));
						break;

					case "leave":
						await dialog.Msg(L("Then Parias dies, the pilgrims stop coming, and the cantors will be mourning a forest instead of a saint. I've seen it happen before. I'd rather not see it again."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("burnBlooms", out var bObj)) return;
				if (!quest.TryGetProgress("killElavines", out var eObj)) return;
				if (!quest.TryGetProgress("killMatriarch", out var mObj)) return;

				if (bObj.Done && eObj.Done && mObj.Done)
				{
					await dialog.Msg(L("{#666666}*She closes her eyes for a long moment*{/}"));
					await dialog.Msg(L("Blooms are burned, Matriarch's dead. The grove will scar - the eastern half is gone - but the western half can breathe again."));
					await dialog.Msg(L("Take this. A druid's purse, and a sapling cutting from the heart-grove. Plant it somewhere it can hear running water, and it'll remember Parias forever."));
					character.Variables.Perm.Remove(matriarchSpawnedKey);
					character.Quests.Complete(questId);
				}
				else if (eObj.Done && !mObj.Done)
				{
					var hasSpawned = character.Variables.Perm.GetBool(matriarchSpawnedKey, false);
					if (!hasSpawned)
					{
						character.Variables.Perm.Set(matriarchSpawnedKey, true);
						if (SpawnTempMonsters(character, MonsterId.Rudas_Elavine, 1, 150, TimeSpan.FromMinutes(5)))
						{
							await dialog.Msg(L("She's coming out of the heart-grove!"));
							character.ServerMessage(L("{#FF9966}The Elavine Matriarch erupts in bloom!{/}"));
						}
					}
					else
					{
						await dialog.Msg(L("Find her before she escapes."));
					}
				}
				else if (!bObj.Done)
				{
					await dialog.Msg(L("Burn the three rot-blooms first. She can't keep growing without them."));
				}
				else
				{
					await dialog.Msg(L("Blooms are ash. Now kill ten of the daughters. She'll come out once she feels them dying."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The grove's healing. New shoots came up in the western half last week - small, but green. The eastern half is still bare. We won't pretend it'll come back. Sometimes you save what you can."));
			}
		});

		// Rot-bloom burn points for Quest 1005
		//-------------------------------------------------------------------------
		void AddRotBloom(int bloomNumber, int x, int z, int direction)
		{
			AddNpc(47190, L("Rot-Bloom"), "f_maple_24_1", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_maple_24_1", 1005);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A grey rot-bloom dripping sap into the soil*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_maple_24_1.Quest1005.Bloom{bloomNumber}";
				if (character.Variables.Perm.GetBool(variableKey, false))
				{
					await dialog.Msg(L("{#666666}*Already burned. The soil is still smoking*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Burning bloom..."), "Cancel", "SITGROPE", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var count = character.Variables.Perm.GetInt("Laima.Quests.f_maple_24_1.Quest1005.BloomsBurned", 0) + 1;
					character.Variables.Perm.Set("Laima.Quests.f_maple_24_1.Quest1005.BloomsBurned", count);
					character.ServerMessage(LF("Rot-blooms burned: {0}/3", count));

					if (count >= 3)
						character.ServerMessage(L("{#FFD700}All rot-blooms burned! Now bait out the Matriarch.{/}"));
				}
				else
				{
					character.ServerMessage(L("Burning interrupted."));
				}
			});
		}

		AddRotBloom(1, 1200, -100, 0);
		AddRotBloom(2, 1400, 100, 90);
		AddRotBloom(3, 1100, 200, 180);

		// Quest 6: Parias Sweep
		//-------------------------------------------------------------------------
		AddNpc(155146, L("[Ranger] Justinas"), "f_maple_24_1", -300, 900, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1006);

			dialog.SetTitle(L("Justinas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A ranger testing his bowstring*{/}"));
				await dialog.Msg(L("There are three species we sweep every week here - Rudas Elavines, Deliones, and Cloverins. Each one's manageable on its own, but together they crowd the paths and end up crowding the visitors too."));
				await dialog.Msg(L("The druid keeps a cairn at the grove-edge where we record each sweep. The rangers rotate through Parias on a schedule. The cairn tallies tell them whether to expect a clear path or to bring extra arrows."));

				var response = await dialog.Select(L("Kill 12 Rudas Elavines, 12 Deliones, and 12 Cloverins, then mark a tally line on the Druid's Cairn at the grove-edge. Will you take the job?"),
					Option(L("I'll take the sweep"), "help"),
					Option(L("Why a cairn instead of a logbook?"), "info"),
					Option(L("Find another ranger"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*He gives you a stub of white chalk wrapped in oilcloth*{/}"));
						await dialog.Msg(L("Thirty-six kills, no padding the count. The rangers will check the corpses on Sunday's patrol."));
						await dialog.Msg(L("The cairn is at the grove-edge, on the west face. Mark one horizontal line under the last one - one line per sweep, nothing fancy. The druid reads them like a calendar."));
						break;

					case "info":
						await dialog.Msg(L("Logbooks travel with the ranger. The cairn stays where the next ranger needs it. The druid set up the custom back in her grandmother's day - it's outlasted three logbook systems already."));
						await dialog.Msg(L("The chalk lines go back forty years on the west face of the cairn. Some weeks have no line. That's how we know which weeks the sweep failed."));
						break;

					case "leave":
						await dialog.Msg(L("The next ranger'll ask you the same thing. The paths don't sweep themselves."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killRudas", out var rObj)) return;
				if (!quest.TryGetProgress("killDeliones", out var dObj)) return;
				if (!quest.TryGetProgress("killCloverins", out var cObj)) return;
				if (!quest.TryGetProgress("logCairn", out var lObj)) return;

				if (rObj.Done && dObj.Done && cObj.Done && lObj.Done)
				{
					await dialog.Msg(L("{#666666}*He glances at the cairn, your chalk line clean under the last one*{/}"));
					await dialog.Msg(L("Tally logged, numbers add up. The druid will read the cairn at sundown, and the next ranger reads it at dawn."));
					await dialog.Msg(L("Full pay, plus a flask of grove-tea. The druid sends it along for anyone who chalks a clean line."));
					character.Quests.Complete(questId);
				}
				else if (rObj.Done && dObj.Done && cObj.Done)
				{
					await dialog.Msg(L("Sweep's done. Now go mark the cairn - west face, single horizontal line, nothing fancy. The druid notices fancy."));
				}
				else
				{
					await dialog.Msg(L("Twelve of each. They crowd together. If you get pinned by Rudas and Cloverins at the same time, back onto the path-stones - they won't follow you off the path."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Parias is walking clean. Your chalk line on the cairn has weathered three rains and you can still read it - the druid says that's how you know someone pressed it in like they meant it."));
			}
		});

		// Druid's Cairn for Quest 1006 tally
		//-------------------------------------------------------------------------
		AddNpc(47190, L("Druid's Cairn"), "f_maple_24_1", -200, 950, 90, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_1", 1006);

			if (!character.Quests.IsActive(questId))
			{
				await dialog.Msg(L("{#666666}*A moss-grown cairn at the grove edge, marked with old ranger tallies*{/}"));
				return;
			}

			var loggedKey = "Laima.Quests.f_maple_24_1.Quest1006.CairnLogged";
			if (character.Variables.Perm.GetBool(loggedKey, false))
			{
				await dialog.Msg(L("{#666666}*Your tally is already marked here*{/}"));
				return;
			}

			if (!character.Quests.TryGetById(questId, out var quest)) return;
			if (!quest.TryGetProgress("killRudas", out var rObj)) return;
			if (!quest.TryGetProgress("killDeliones", out var dObj)) return;
			if (!quest.TryGetProgress("killCloverins", out var cObj)) return;

			if (!(rObj.Done && dObj.Done && cObj.Done))
			{
				await dialog.Msg(L("{#666666}*The cairn is ready, but you haven't finished the sweep*{/}"));
				return;
			}

			var result = await character.TimeActions.StartAsync(L("Logging cairn..."), "Cancel", "PRAY", TimeSpan.FromSeconds(3));

			if (result == TimeActionResult.Completed)
			{
				character.Variables.Perm.Set(loggedKey, true);
				character.ServerMessage(L("{#FFD700}Cairn logged. Return to Ranger Justinas.{/}"));
			}
			else
			{
				character.ServerMessage(L("Logging interrupted."));
			}
		});
	}
}

//-----------------------------------------------------------------------------
// QUEST DEFINITIONS
//-----------------------------------------------------------------------------

public class FMaple241Quest1001 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_1", 1001);
		SetName(L("Rudas Bloom"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Rudas Elavines for preserving petals."));
		SetLocation("f_maple_24_1");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Florist] Morta"), "f_maple_24_1");

		AddObjective("killRudas", L("Kill Rudas Elavines"),
			new KillObjective(25, new[] { MonsterId.Rudas_Elavine }));

		AddReward(new ExpReward(1000, 700));
		AddReward(new SilverReward(2200));
		AddReward(new ItemReward(640081, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
	}
}

public class FMaple241Quest1002 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_1", 1002);
		SetName(L("Atti Pollen"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Attis and scrape inner-grove pollen for the beekeeper."));
		SetLocation("f_maple_24_1");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Beekeeper] Kovas"), "f_maple_24_1");

		AddObjective("killAttis", L("Kill Attis"),
			new KillObjective(15, new[] { MonsterId.Atti }));

		AddObjective("gatherPollen", L("Gather pollen-clusters"),
			new CollectItemObjective(650041, 5));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650041, character.Inventory.CountItem(650041), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650041, character.Inventory.CountItem(650041), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple241Quest1003 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_1", 1003);
		SetName(L("Delione Thinning"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Deliones choking the inner Parias paths."));
		SetLocation("f_maple_24_1");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Groundskeeper] Dovydas"), "f_maple_24_1");

		AddObjective("killDeliones", L("Kill Deliones"),
			new KillObjective(35, new[] { MonsterId.Delione }));

		AddObjective("tendSaplings", L("Tend the four sapling-rings"),
			new VariableCheckObjective("Laima.Quests.f_maple_24_1.Quest1003.RingsTended", 4, true));

		AddReward(new ExpReward(1000, 700));
		AddReward(new SilverReward(2200));
		AddReward(new ItemReward(640081, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_1.Quest1003.RingsTended");
		for (int i = 1; i <= 4; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_1.Quest1003.Ring{i}");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_1.Quest1003.RingsTended");
		for (int i = 1; i <= 4; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_1.Quest1003.Ring{i}");
	}
}

public class FMaple241Quest1004 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_1", 1004);
		SetName(L("Lucky Clovers"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Cloverins and gather lucky four-lobed clovers for luck potions."));
		SetLocation("f_maple_24_1");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Alchemist] Rasa"), "f_maple_24_1");

		AddObjective("killCloverins", L("Kill Cloverins"),
			new KillObjective(20, new[] { MonsterId.Cloverin }));

		AddObjective("gatherClovers", L("Gather lucky clovers"),
			new CollectItemObjective(650051, 8));

		AddReward(new ExpReward(1550, 1090));
		AddReward(new SilverReward(2900));
		AddReward(new ItemReward(640082, 1));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650051, character.Inventory.CountItem(650051), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650051, character.Inventory.CountItem(650051), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple241Quest1005 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_1", 1005);
		SetName(L("The Elavine Matriarch"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Rudas Elavines to draw out the Matriarch rotting the heart-grove."));
		SetLocation("f_maple_24_1");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Druid] Vaiva"), "f_maple_24_1");

		AddObjective("burnBlooms", L("Burn the three rot-blooms"),
			new VariableCheckObjective("Laima.Quests.f_maple_24_1.Quest1005.BloomsBurned", 3, true));

		AddObjective("killElavines", L("Kill Rudas Elavines"),
			new KillObjective(10, new[] { MonsterId.Rudas_Elavine }));

		AddObjective("killMatriarch", L("Defeat the Matriarch"),
			new KillObjective(1, new[] { MonsterId.Rudas_Elavine }));

		AddReward(new ExpReward(3100, 2200));
		AddReward(new SilverReward(3800));
		AddReward(new ItemReward(640082, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_1.Quest1005.BloomsBurned");
		for (int i = 1; i <= 3; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_1.Quest1005.Bloom{i}");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_1.Quest1005.BloomsBurned");
		for (int i = 1; i <= 3; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_1.Quest1005.Bloom{i}");
	}
}

public class FMaple241Quest1006 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_1", 1006);
		SetName(L("Parias Sweep"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill the Parias triad: Rudas, Deliones, Cloverins."));
		SetLocation("f_maple_24_1");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Ranger] Justinas"), "f_maple_24_1");

		AddObjective("killRudas", L("Kill Rudas Elavines"),
			new KillObjective(12, new[] { MonsterId.Rudas_Elavine }));

		AddObjective("killDeliones", L("Kill Deliones"),
			new KillObjective(12, new[] { MonsterId.Delione }));

		AddObjective("killCloverins", L("Kill Cloverins"),
			new KillObjective(12, new[] { MonsterId.Cloverin }));

		AddObjective("logCairn", L("Log the sweep on the Druid's Cairn"),
			new VariableCheckObjective("Laima.Quests.f_maple_24_1.Quest1006.CairnLogged", 1, true));

		AddReward(new ExpReward(3100, 2200));
		AddReward(new SilverReward(3800));
		AddReward(new ItemReward(640082, 2));
		AddReward(new ItemReward(640003, 2));
		AddReward(new ItemReward(640006, 2));
		AddReward(new ItemReward(640009, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_1.Quest1006.CairnLogged");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_1.Quest1006.CairnLogged");
	}
}
