//--- Melia Script ----------------------------------------------------------
// Northern Parias Forest Quest NPCs
//--- Description -----------------------------------------------------------
// Quests for Northern Parias Forest (f_maple_24_3).
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

public class FMaple243QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// Quest 1: Fragolin Thinning
		//-------------------------------------------------------------------------
		AddNpc(20060, L("[Forest-Ward] Rasa"), "f_maple_24_3", -1500, -1000, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1001);

			dialog.SetTitle(L("Rasa"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Fragolins are swarming the north trail. Kill forty-five and the pilgrim road will be open again."));

				var response = await dialog.Select(L("Will you open the road for the pilgrims?"),
					Option(L("I'll kill"), "help"),
					Option(L("Pilgrims?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Forty-five. Watch out around the berry-patches."));
						break;

					case "info":
						await dialog.Msg(L("The road runs to the Parias shrine, but the swarm is keeping pilgrims out."));
						break;

					case "leave":
						await dialog.Msg(L("Road stays closed, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killFragolins", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("Pilgrims are already passing through."));
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep killing."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Road's open. The shrine has visitors again."));
			}
		});

		// Quest 2: Cloverin Pollen
		//-------------------------------------------------------------------------
		AddNpc(20114, L("[Herbalist] Vaiva"), "f_maple_24_3", -600, 600, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1002);

			dialog.SetTitle(L("Vaiva"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Cloverin pollen is what holds the forest wards together. Kill thirty and bring me eight pollen sacs."));

				var response = await dialog.Select(L("Will you bring me the pollen sacs?"),
					Option(L("I'll bring"), "help"),
					Option(L("Wards?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Keep the sacs whole. They spoil if you tear them."));
						break;

					case "info":
						await dialog.Msg(L("The wards keep the rootcrystal corruption off the glade. Pollen is what feeds them."));
						break;

					case "leave":
						await dialog.Msg(L("The wards will get weaker, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killCloverins", out var killObj)) return;
				if (!quest.TryGetProgress("gatherPollen", out var cObj)) return;

				if (killObj.Done && cObj.Done)
				{
					await dialog.Msg(L("Eight sacs. The wards will be back to full strength."));
					character.Inventory.Remove(650248, character.Inventory.CountItem(650248), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep hunting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The wards are humming steady again."));
			}
		});

		// Quest 3: Blueberrin Essence
		//-------------------------------------------------------------------------
		AddNpc(20117, L("[Alchemist] Eimis"), "f_maple_24_3", 400, -900, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1003);

			dialog.SetTitle(L("Eimis"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Blueberrin essence will cure the pilgrim fever. Kill fifteen and bring me five vials of essence."));

				var response = await dialog.Select(L("Will you bring me the vials?"),
					Option(L("I'll bring"), "help"),
					Option(L("Fever?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Don't shake the vials. They go bad."));
						break;

					case "info":
						await dialog.Msg(L("Pilgrims catch the fever when they leave the road. The essence breaks it in a single night."));
						break;

					case "leave":
						await dialog.Msg(L("More pilgrims will catch the fever, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killBlueberrins", out var killObj)) return;
				if (!quest.TryGetProgress("gatherEssence", out var pObj)) return;

				if (killObj.Done && pObj.Done)
				{
					await dialog.Msg(L("Five vials. I can mix the cure now."));
					character.Inventory.Remove(650249, character.Inventory.CountItem(650249), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep hunting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Six pilgrims already cured of the fever."));
			}
		});

		// Quest 4: Rootcrystal Killing
		//-------------------------------------------------------------------------
		AddNpc(20114, L("[Crystal-Warder] Audra"), "f_maple_24_3", -900, -400, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1004);

			dialog.SetTitle(L("Audra"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A crystal-warder, frowning at a chime-stone she keeps striking*{/}"));
				await dialog.Msg(L("Parias has a true note. The grove hums it, the trees carry it. The pilgrims pray to it without even knowing. When the note's right, the whole forest sings in tune."));
				await dialog.Msg(L("The Rootcrystals are pulling on the bedrock and dragging the note flat. Pilgrims feel something's off before they see anything wrong - they turn back at the path-stone and can't even say why."));

				var response = await dialog.Select(L("Break 45 Parias Rootcrystals to stop the pull, then re-tune the four chime-stones I've hung from the path-trees. The chimes will anchor the true note again. Will you do it?"),
					Option(L("I'll break the crystals and tune the chimes"), "help"),
					Option(L("How does a forest hum a note?"), "info"),
					Option(L("Sounds like cantor work"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*She gives you a worn bone-mallet*{/}"));
						await dialog.Msg(L("Break the forty-five crystals first. Strike low and step back - the shards carry the off-note. Don't pocket any of them."));
						await dialog.Msg(L("Then the four chime-stones. They hang from the path-trees at about shoulder height. Tap each one once and listen for the true note. If it rings flat, tap it a second time - but never a third."));
						break;

					case "info":
						await dialog.Msg(L("Every place has a note - a meadow, a mountain, a city. Most places it's too quiet to hear over the noise of daily life. Parias is different. The grove was consecrated for it centuries back, so the note is louder."));
						await dialog.Msg(L("The crystals interrupt the note. The chime-stones are how we teach the grove its own pitch again after the interruption is over."));
						break;

					case "leave":
						await dialog.Msg(L("It is cantor work, partly. But the cantors are at Salvia's seminary, three days' walk away. The grove will go completely flat by then. I'll find someone closer if I can."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("breakCrystals", out var killObj)) return;
				if (!quest.TryGetProgress("tuneChimes", out var tObj)) return;

				if (killObj.Done && tObj.Done)
				{
					await dialog.Msg(L("{#666666}*She listens for a long moment, then smiles*{/}"));
					await dialog.Msg(L("The glade reads clean. The chimes are ringing true all the way to the inner path. Parias is in tune for the first time in two months."));
					await dialog.Msg(L("Here's your pay, plus a sealed phial of grove-water from the heart-spring. Drink it slow - you can taste the note in it."));
					character.Quests.Complete(questId);
				}
				else if (!killObj.Done)
				{
					await dialog.Msg(L("The crystals are still pulling. If you tap a chime-stone now, you'd just teach the grove the wrong note. Break the crystals first."));
				}
				else
				{
					await dialog.Msg(L("The crystals are quiet. Now go tune the four chime-stones. One tap with the bone-mallet, then listen for the true note. Take your time - the grove is listening too."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Parias hums clean from the path-stone all the way through. A pilgrim came through last week and stopped halfway down the inner path - said she heard her grandmother's lullaby in the leaves. That's the note doing its work."));
			}
		});

		// Chime-stone tuning points for Quest 1004
		//-------------------------------------------------------------------------
		void AddChimeStone(int chimeNumber, int x, int z, int direction)
		{
			AddNpc(47190, L("Chime-Stone"), "f_maple_24_3", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_maple_24_3", 1004);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A pale chime-stone hangs from a path-tree branch*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_maple_24_3.Quest1004.Chime{chimeNumber}";
				if (character.Variables.Perm.GetBool(variableKey, false))
				{
					await dialog.Msg(L("{#666666}*Already tuned. The stone rings clean*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Tuning chime..."), "Cancel", "PRAY", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var count = character.Variables.Perm.GetInt("Laima.Quests.f_maple_24_3.Quest1004.ChimesTuned", 0) + 1;
					character.Variables.Perm.Set("Laima.Quests.f_maple_24_3.Quest1004.ChimesTuned", count);
					character.ServerMessage(LF("Chime-stones tuned: {0}/4", count));

					if (count >= 4)
						character.ServerMessage(L("{#FFD700}All chime-stones tuned! Return to Crystal-Warder Audra.{/}"));
				}
				else
				{
					character.ServerMessage(L("Tuning interrupted."));
				}
			});
		}

		AddChimeStone(1, -800, -300, 0);
		AddChimeStone(2, -1100, -500, 90);
		AddChimeStone(3, -700, -100, 180);
		AddChimeStone(4, -1000, -600, 270);

		// Quest 5: The Fragolin Mother
		//-------------------------------------------------------------------------
		AddNpc(47245, L("[Bounty Hunter] Tadas"), "f_maple_24_3", -1700, -1100, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1005);
			var alphaSpawnedKey = "Laima.Quests.f_maple_24_3.Quest1005.AlphaSpawned";

			dialog.SetTitle(L("Tadas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A bounty hunter cleaning dried musk off his vambrace*{/}"));
				await dialog.Msg(L("There's a Fragolin Mother nesting under the north ridge. She's old, scarred, and smarter than three of her daughters put together. Most hunters who go after her come back missing an arm."));
				await dialog.Msg(L("Three brood-burrows along the ridge feed her hatch cycle. Smoke the burrows and her instinct flips - instead of hiding deeper in the nest, she'll come up to fight. That's when we get her."));

				var response = await dialog.Select(L("Smoke the three brood-burrows along the north ridge, then kill 10 Fragolins to bait her out. She fights when her hatch is threatened. Will you do it?"),
					Option(L("I'll take the Mother"), "help"),
					Option(L("Why smoke and not destroy the burrows?"), "info"),
					Option(L("Find a Fragolin specialist"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*He gives you a pouch of dry tinder and a striker*{/}"));
						await dialog.Msg(L("Burn the three burrows first. I left smudge-torches at each entrance. Light the torch, jam it deep in the burrow-mouth, then walk away. The smoke does the rest."));
						await dialog.Msg(L("Then kill ten of the brood. She'll come out around the eighth one. She's heavy and slow to start, but her swing's got the reach of a polearm. Stay inside the swing-arc, not outside it."));
						break;

					case "info":
						await dialog.Msg(L("If you destroy the burrows, the next Fragolin Mother claims the territory inside a season. But smoked burrows stay tainted to Fragolins - they'll avoid the ridge for years."));
						await dialog.Msg(L("It's an old hunter trick. Cheaper than digging, and it lasts longer than just blocking them. The smoke also flushes the Mother out, because she reads it as a wildfire threatening the hatch. She comes out fighting instead of running."));
						break;

					case "leave":
						await dialog.Msg(L("Swarm grows, the ridge gets unlivable, and the path to the inner grove closes by autumn. Let me know if you change your mind."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("smokeBurrows", out var sObj)) return;
				if (!quest.TryGetProgress("killPack", out var pObj)) return;
				if (!quest.TryGetProgress("killAlpha", out var aObj)) return;

				if (sObj.Done && pObj.Done && aObj.Done)
				{
					await dialog.Msg(L("{#666666}*He counts the coin slowly into a pouch*{/}"));
					await dialog.Msg(L("Burrows are smoked, Mother's dead, swarm's broken. The ridge will stay tainted to Fragolins for years - that's the smoke doing its work."));
					await dialog.Msg(L("Full bounty plus a hide-stipend. The Mother's hide makes good vambrace-leather, and you earned a share. The next pilgrim caravan to the inner grove will roll through clean. Drink to that."));
					character.Variables.Perm.Remove(alphaSpawnedKey);
					character.Quests.Complete(questId);
				}
				else if (pObj.Done && !aObj.Done)
				{
					var hasSpawned = character.Variables.Perm.GetBool(alphaSpawnedKey, false);
					if (!hasSpawned)
					{
						character.Variables.Perm.Set(alphaSpawnedKey, true);
						if (SpawnTempMonsters(character, MonsterId.Fragolin, 1, 150, TimeSpan.FromMinutes(5)))
						{
							await dialog.Msg(L("Here she comes!"));
							character.ServerMessage(L("{#FF9966}The Fragolin Mother emerges from the ridge!{/}"));
						}
					}
					else
					{
						await dialog.Msg(L("Go find her."));
					}
				}
				else if (!sObj.Done)
				{
					await dialog.Msg(L("Three torches, three burrow-mouths. Light, jam deep, walk away. The smoke does what a blade can't."));
				}
				else
				{
					await dialog.Msg(L("Burrows are smoking. Now ten of the brood. She'll come out around the eighth one. Mind her swing - it's got the reach of a polearm."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Ridge stays quiet, pilgrims sleep easy. Three new caravans came through this season - none of them needed a bounty contract. That's the best kind of thanks: nobody had to ask for it."));
			}
		});

		// Brood-burrow smoke points for Quest 1005
		//-------------------------------------------------------------------------
		void AddBroodBurrow(int burrowNumber, int x, int z, int direction)
		{
			AddNpc(47190, L("Fragolin Brood-Burrow"), "f_maple_24_3", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_maple_24_3", 1005);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A burrow under the ridge with a smoke-torch propped at the entrance*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_maple_24_3.Quest1005.Burrow{burrowNumber}";
				if (character.Variables.Perm.GetBool(variableKey, false))
				{
					await dialog.Msg(L("{#666666}*Already smoked. The air still smells sharp*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Smoking burrow..."), "Cancel", "SITGROPE", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var count = character.Variables.Perm.GetInt("Laima.Quests.f_maple_24_3.Quest1005.BurrowsSmoked", 0) + 1;
					character.Variables.Perm.Set("Laima.Quests.f_maple_24_3.Quest1005.BurrowsSmoked", count);
					character.ServerMessage(LF("Brood-burrows smoked: {0}/3", count));

					if (count >= 3)
						character.ServerMessage(L("{#FFD700}All burrows smoked! Now bait out the Mother.{/}"));
				}
				else
				{
					character.ServerMessage(L("Smoking interrupted."));
				}
			});
		}

		AddBroodBurrow(1, -1500, -900, 0);
		AddBroodBurrow(2, -1900, -1300, 90);
		AddBroodBurrow(3, -1600, -1200, 180);

		// Quest 6: Parias Forest Sweep
		//-------------------------------------------------------------------------
		AddNpc(155146, L("[Ranger-Captain] Mindaugas"), "f_maple_24_3", 200, 1000, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1006);

			dialog.SetTitle(L("Mindaugas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A ranger-captain with a trail-map weighed down by river-pebbles*{/}"));
				await dialog.Msg(L("Parias forest goes three days' walk in any direction. We can't patrol all of it. So we sweep the trail-corridors where pilgrims actually walk, and log the work on the Ranger Cairn at the trail-head."));
				await dialog.Msg(L("Three species cause us trouble in the corridors: Fragolins, Cloverins, and Blueberrins. Each ranger does a round on their cycle, then logs the kill count. The next one reads the cairn before they head out."));

				var response = await dialog.Select(L("Kill 12 Fragolins, 12 Cloverins, and 12 Blueberrins, then mark the tally on the Ranger Cairn at the trail-head. Standard ranger pay. Take the job?"),
					Option(L("I'll take the sweep"), "help"),
					Option(L("Why not just patrol everywhere?"), "info"),
					Option(L("Find a forest-hand"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*He gives you a charcoal stub and a folded forest-pass*{/}"));
						await dialog.Msg(L("Thirty-six kills, no padding the count. The next ranger checks the bone-piles on Sunday's perimeter walk."));
						await dialog.Msg(L("The cairn is at the trail-head, east face. Mark a chevron - one stroke down, one stroke up. Don't write your name. The cairn doesn't care about names, only the sweep."));
						break;

					case "info":
						await dialog.Msg(L("Forty rangers couldn't patrol three days' walk in every direction. Twelve can patrol the corridors and respond to off-trail sightings. We picked twelve because that's what the budget allows."));
						await dialog.Msg(L("So we sweep the corridors hard and the off-trail areas only sometimes. Pilgrims walk the corridors. The pilgrims are who we protect."));
						break;

					case "leave":
						await dialog.Msg(L("Then the next pilgrim caravan walks an unswept corridor, and we don't get to decide who comes back. I'd rather not roll the dice on that."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killFragolins", out var fObj)) return;
				if (!quest.TryGetProgress("killCloverins", out var cObj)) return;
				if (!quest.TryGetProgress("killBlueberrins", out var bObj)) return;
				if (!quest.TryGetProgress("logCairn", out var lObj)) return;

				if (fObj.Done && cObj.Done && bObj.Done && lObj.Done)
				{
					await dialog.Msg(L("{#666666}*He countersigns your forest-pass and counts coin into a cloth*{/}"));
					await dialog.Msg(L("Your cairn chevron is clean. Sweep's done, numbers add up, and the next ranger walks the corridor at first light."));
					await dialog.Msg(L("Standard pay, plus a bonus for the off-trail sightings you reported. The forest-corridor system works - you're part of the reason why."));
					character.Quests.Complete(questId);
				}
				else if (fObj.Done && cObj.Done && bObj.Done)
				{
					await dialog.Msg(L("Sweep's done. Now go mark the cairn - east face, chevron, nothing fancy. Press the chalk in hard - the rain washes off a soft mark."));
				}
				else
				{
					await dialog.Msg(L("Twelve of each. They cycle through the corridor in sequence - Fragolins lead, Cloverins follow, Blueberrins clean up after. Sweep them in order if you can."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Rangers walk the trail-corridor every dawn now. Three pilgrim caravans through this fortnight, no losses, no off-trail sightings. The cairn keeps its records and we keep the corridor."));
			}
		});

		// Ranger Cairn for Quest 1006
		//-------------------------------------------------------------------------
		AddNpc(47190, L("Ranger Cairn"), "f_maple_24_3", 250, 1050, 90, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_maple_24_3", 1006);

			if (!character.Quests.IsActive(questId))
			{
				await dialog.Msg(L("{#666666}*A trailhead cairn marked with old ranger tallies*{/}"));
				return;
			}

			var loggedKey = "Laima.Quests.f_maple_24_3.Quest1006.CairnLogged";
			if (character.Variables.Perm.GetBool(loggedKey, false))
			{
				await dialog.Msg(L("{#666666}*Your tally is already marked here*{/}"));
				return;
			}

			if (!character.Quests.TryGetById(questId, out var quest)) return;
			if (!quest.TryGetProgress("killFragolins", out var fObj)) return;
			if (!quest.TryGetProgress("killCloverins", out var cObj)) return;
			if (!quest.TryGetProgress("killBlueberrins", out var bObj)) return;

			if (!(fObj.Done && cObj.Done && bObj.Done))
			{
				await dialog.Msg(L("{#666666}*The cairn is ready, but you haven't finished the sweep*{/}"));
				return;
			}

			var result = await character.TimeActions.StartAsync(L("Logging cairn..."), "Cancel", "PRAY", TimeSpan.FromSeconds(3));

			if (result == TimeActionResult.Completed)
			{
				character.Variables.Perm.Set(loggedKey, true);
				character.ServerMessage(L("{#FFD700}Cairn logged. Return to Ranger-Captain Mindaugas.{/}"));
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

public class FMaple243Quest1001 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_3", 1001);
		SetName(L("Fragolin Thinning"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Fragolins swarming the Parias pilgrim road."));
		SetLocation("f_maple_24_3");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Forest-Ward] Rasa"), "f_maple_24_3");

		AddObjective("killFragolins", L("Kill Fragolins"),
			new KillObjective(45, new[] { MonsterId.Fragolin }));

		AddReward(new ExpReward(500, 340));
		AddReward(new SilverReward(1200));
		AddReward(new ItemReward(640081, 1));
		AddReward(new ItemReward(640002, 2));
		AddReward(new ItemReward(640005, 2));
	}
}

public class FMaple243Quest1002 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_3", 1002);
		SetName(L("Cloverin Pollen"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Cloverins and bring pollen sacs for the forest wards."));
		SetLocation("f_maple_24_3");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Herbalist] Vaiva"), "f_maple_24_3");

		AddObjective("killCloverins", L("Kill Cloverins"),
			new KillObjective(30, new[] { MonsterId.Cloverin }));

		AddObjective("gatherPollen", L("Gather pollen sacs"),
			new CollectItemObjective(650248, 8));

		AddReward(new ExpReward(1200, 800));
		AddReward(new SilverReward(1600));
		AddReward(new ItemReward(640081, 2));
		AddReward(new ItemReward(640002, 2));
		AddReward(new ItemReward(640005, 2));
		AddReward(new ItemReward(640008, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650248, character.Inventory.CountItem(650248), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650248, character.Inventory.CountItem(650248), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple243Quest1003 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_3", 1003);
		SetName(L("Blueberrin Essence"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Blueberrins and bring essence-vials for the pilgrim fever cure."));
		SetLocation("f_maple_24_3");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Alchemist] Eimis"), "f_maple_24_3");

		AddObjective("killBlueberrins", L("Kill Blueberrins"),
			new KillObjective(15, new[] { MonsterId.Blueberrin }));

		AddObjective("gatherEssence", L("Gather essence-vials"),
			new CollectItemObjective(650249, 5));

		AddReward(new ExpReward(1200, 800));
		AddReward(new SilverReward(1600));
		AddReward(new ItemReward(640081, 2));
		AddReward(new ItemReward(640002, 2));
		AddReward(new ItemReward(640005, 2));
		AddReward(new ItemReward(640008, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650249, character.Inventory.CountItem(650249), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650249, character.Inventory.CountItem(650249), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FMaple243Quest1004 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_3", 1004);
		SetName(L("Rootcrystal Killing"));
		SetType(QuestType.Sub);
		SetDescription(L("Break Parias Rootcrystals pulling the forest off-tone."));
		SetLocation("f_maple_24_3");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Crystal-Warder] Audra"), "f_maple_24_3");

		AddObjective("breakCrystals", L("Break Parias Rootcrystals"),
			new KillObjective(45, new[] { MonsterId.Rootcrystal_01 }));

		AddObjective("tuneChimes", L("Tune the four chime-stones"),
			new VariableCheckObjective("Laima.Quests.f_maple_24_3.Quest1004.ChimesTuned", 4, true));

		AddReward(new ExpReward(500, 340));
		AddReward(new SilverReward(1200));
		AddReward(new ItemReward(640081, 1));
		AddReward(new ItemReward(640002, 2));
		AddReward(new ItemReward(640005, 2));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_3.Quest1004.ChimesTuned");
		for (int i = 1; i <= 4; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_3.Quest1004.Chime{i}");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_3.Quest1004.ChimesTuned");
		for (int i = 1; i <= 4; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_3.Quest1004.Chime{i}");
	}
}

public class FMaple243Quest1005 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_3", 1005);
		SetName(L("The Fragolin Mother"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Fragolins to draw out the Fragolin Mother nesting under the ridge."));
		SetLocation("f_maple_24_3");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Bounty Hunter] Tadas"), "f_maple_24_3");

		AddObjective("smokeBurrows", L("Smoke the three brood-burrows"),
			new VariableCheckObjective("Laima.Quests.f_maple_24_3.Quest1005.BurrowsSmoked", 3, true));

		AddObjective("killPack", L("Kill Fragolins"),
			new KillObjective(10, new[] { MonsterId.Fragolin }));

		AddObjective("killAlpha", L("Defeat the Fragolin Mother"),
			new KillObjective(1, new[] { MonsterId.Fragolin }));

		AddReward(new ExpReward(1600, 1100));
		AddReward(new SilverReward(2000));
		AddReward(new ItemReward(640081, 3));
		AddReward(new ItemReward(640002, 3));
		AddReward(new ItemReward(640005, 3));
		AddReward(new ItemReward(640008, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_3.Quest1005.BurrowsSmoked");
		for (int i = 1; i <= 3; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_3.Quest1005.Burrow{i}");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_3.Quest1005.BurrowsSmoked");
		for (int i = 1; i <= 3; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_maple_24_3.Quest1005.Burrow{i}");
	}
}

public class FMaple243Quest1006 : QuestScript
{
	protected override void Load()
	{
		SetId("f_maple_24_3", 1006);
		SetName(L("Parias Forest Sweep"));
		SetType(QuestType.Sub);
		SetDescription(L("Standard sweep of Fragolins, Cloverins, and Blueberrins."));
		SetLocation("f_maple_24_3");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Ranger-Captain] Mindaugas"), "f_maple_24_3");

		AddObjective("killFragolins", L("Kill Fragolins"),
			new KillObjective(12, new[] { MonsterId.Fragolin }));

		AddObjective("killCloverins", L("Kill Cloverins"),
			new KillObjective(12, new[] { MonsterId.Cloverin }));

		AddObjective("killBlueberrins", L("Kill Blueberrins"),
			new KillObjective(12, new[] { MonsterId.Blueberrin }));

		AddObjective("logCairn", L("Log the sweep on the Ranger Cairn"),
			new VariableCheckObjective("Laima.Quests.f_maple_24_3.Quest1006.CairnLogged", 1, true));

		AddReward(new ExpReward(1600, 1100));
		AddReward(new SilverReward(2000));
		AddReward(new ItemReward(640081, 3));
		AddReward(new ItemReward(640002, 3));
		AddReward(new ItemReward(640005, 3));
		AddReward(new ItemReward(640008, 1));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_3.Quest1006.CairnLogged");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_maple_24_3.Quest1006.CairnLogged");
	}
}
