//--- Melia Script ----------------------------------------------------------
// Pilgrim Road West Quest NPCs
//--- Description -----------------------------------------------------------
// Quests for the west section of Pilgrim Road.
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

public class FPilgrimroad414QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// Quest 1: Purple Repusbunny Kill
		//-------------------------------------------------------------------------
		AddNpc(20060, L("[Tollwarden] Mindaugas"), "f_pilgrimroad_41_4", -400, 100, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1001);

			dialog.SetTitle(L("Mindaugas"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Purple Repusbunnies are swarming the west road. Forty-five kills and pilgrims can walk again."));

				var response = await dialog.Select(L("Will you clear the road for the pilgrims?"),
					Option(L("I'll kill"), "help"),
					Option(L("Pilgrims?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Forty-five of them. Watch out for the burrows."));
						break;

					case "info":
						await dialog.Msg(L("The caravan's been camped behind the waystones for three days. The road needs clearing."));
						break;

					case "leave":
						await dialog.Msg(L("Road stays blocked, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killBunnies", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("The caravan's moving."));
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep killing."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("First pilgrims passed through at dawn."));
			}
		});

		// Quest 2: Bowbunny Fletchings
		//-------------------------------------------------------------------------
		AddNpc(20059, L("[Caravan-Guard] Jurga"), "f_pilgrimroad_41_4", 600, 600, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1002);

			dialog.SetTitle(L("Jurga"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Bow Repusbunnies pick pilgrims off from the ridge. Kill thirty and bring me eight fletchings so we can mark the range."));

				var response = await dialog.Select(L("Will you bring me the fletchings?"),
					Option(L("I'll bring"), "help"),
					Option(L("Mark?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Fletchings, not whole arrows. The arrows snap."));
						break;

					case "info":
						await dialog.Msg(L("The fletchings tell us their draw weight. From the draw we figure the range, and from the range we know where to station shields."));
						break;

					case "leave":
						await dialog.Msg(L("Pilgrims will keep getting shot, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killBowBunnies", out var killObj)) return;
				if (!quest.TryGetProgress("gatherFletchings", out var fObj)) return;

				if (killObj.Done && fObj.Done)
				{
					await dialog.Msg(L("Eight fletchings. We've got the range mapped now."));
					character.Inventory.Remove(650254, character.Inventory.CountItem(650254), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep hunting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The shield line holds. Ridge is quieter now."));
			}
		});

		// Quest 3: Mage-Stub Bark
		//-------------------------------------------------------------------------
		AddNpc(153142, L("[Hedge-Witch] Vaiva"), "f_pilgrimroad_41_4", -500, 500, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1003);

			dialog.SetTitle(L("Vaiva"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Tree-Mage Stubs carry rootspell in their bark. Kill fifteen and bring me five strips."));

				var response = await dialog.Select(L("Will you bring me the strips?"),
					Option(L("I'll bring"), "help"),
					Option(L("Rootspell?"), "info"),
					Option(L("Skip"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Strip them clean - bark with moss on it won't read."));
						break;

					case "info":
						await dialog.Msg(L("It's older than the road itself. Hedge-charms can still pull power from it when nothing else answers."));
						break;

					case "leave":
						await dialog.Msg(L("Hedge goes silent, then."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killStubs", out var killObj)) return;
				if (!quest.TryGetProgress("gatherBark", out var bObj)) return;

				if (killObj.Done && bObj.Done)
				{
					await dialog.Msg(L("Five strips. The hedge wards will hold for the month."));
					character.Inventory.Remove(650256, character.Inventory.CountItem(650256), InventoryItemRemoveMsg.Given);
					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Keep hunting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Wards are burning green. That's a good sign."));
			}
		});

		// Quest 4: Waystone Rootcrystals
		//-------------------------------------------------------------------------
		AddNpc(20117, L("[Waystone-Keeper] Darius"), "f_pilgrimroad_41_4", -1100, -500, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1004);

			dialog.SetTitle(L("Darius"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*An old waystone-keeper next to a silent rune-stone*{/}"));
				await dialog.Msg(L("My grandmother kept these waystones. So did her mother. Eight generations, and none of them ever saw the ley-lines bend the way they're bending now."));
				await dialog.Msg(L("Rootcrystals are pushing up between the markers. Pilgrims walking by eye end up a league off-route, sometimes two. Last week a wool-cart drove three days west when it should have gone north - the driver swore the road kept turning under his wheels."));

				var response = await dialog.Select(L("Break 12 Rootcrystals, then re-true the four ley-marker waystones along the road. Lay your hand flat on each marker - the ley reads through the palm. Will you do it?"),
					Option(L("I'll break the crystals and re-true the markers"), "help"),
					Option(L("How does a waystone bend?"), "info"),
					Option(L("That sounds like priest-work"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*He blesses your palm with iron-water from a clay cup*{/}"));
						await dialog.Msg(L("Break the crystals first - 12 of them between the markers. Strike clean, and don't let the shards touch the rune-lines or the ley sours."));
						await dialog.Msg(L("Then the four marker-stones. Lay your right hand flat on each carved face and count to ten. The runes will hum if the line is true. If they buzz, the marker's still tangled - step back to the last one and try again."));
						break;

					case "info":
						await dialog.Msg(L("A ley-line is a path the world remembers. The waystones don't make the line - they just hold it where pilgrims can read it by eye."));
						await dialog.Msg(L("The crystals push the bedrock, and the bedrock carries the line. If the bedrock moves, the line drifts, and the pilgrims drift with it. Some don't come back."));
						break;

					case "leave":
						await dialog.Msg(L("It is priest work in a sense. But the priests are at Salvia, and the wool-cart is still out there somewhere. I'll wait here for someone less particular."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("breakCrystals", out var killObj)) return;
				if (!quest.TryGetProgress("retrueMarkers", out var rObj)) return;

				if (killObj.Done && rObj.Done)
				{
					await dialog.Msg(L("{#666666}*He checks his copper pendulum - it stops dead-true on the road's axis*{/}"));
					await dialog.Msg(L("Ley reads straight from here to the next caravan-stop. The markers are humming clean and the runes are warm to the palm again."));
					await dialog.Msg(L("Take this. Old waystone-keeper's coin, minted back in my grandmother's day. Spends the same as new."));
					character.Quests.Complete(questId);
				}
				else if (!killObj.Done)
				{
					await dialog.Msg(L("The crystals are still pushing on the bedrock. No use re-truing markers while the line keeps bending under your feet."));
				}
				else
				{
					await dialog.Msg(L("Crystals are down and the ley is settling. Now go re-true the four markers. Right palm flat, count to ten, listen for the hum."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Three caravans through this week, none drifted. The wool-cart found its way back too - driver swears he heard the markers humming as he passed each one. He's not wrong."));
			}
		});

		// Ley-marker waystones for Quest 1004
		//-------------------------------------------------------------------------
		void AddLeyMarker(int markerNumber, int x, int z, int direction)
		{
			AddNpc(47190, L("Ley-Marker Waystone"), "f_pilgrimroad_41_4", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_pilgrimroad_41_4", 1004);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A weathered ley-marker waystone, ley-runes faintly glowing*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_pilgrimroad_41_4.Quest1004.Marker{markerNumber}";
				if (character.Variables.Perm.GetBool(variableKey, false))
				{
					await dialog.Msg(L("{#666666}*Already re-trued; the runes hum steady*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Re-truing waystone..."), "Cancel", "PRAY", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var count = character.Variables.Perm.GetInt("Laima.Quests.f_pilgrimroad_41_4.Quest1004.MarkersTrued", 0) + 1;
					character.Variables.Perm.Set("Laima.Quests.f_pilgrimroad_41_4.Quest1004.MarkersTrued", count);
					character.ServerMessage(LF("Marker-stones re-trued: {0}/4", count));

					if (count >= 4)
						character.ServerMessage(L("{#FFD700}All marker-stones re-trued! Return to Waystone-Keeper Darius.{/}"));
				}
				else
				{
					character.ServerMessage(L("Re-truing interrupted."));
				}
			});
		}

		AddLeyMarker(1, -900, -300, 0);
		AddLeyMarker(2, -300, 200, 90);
		AddLeyMarker(3, 700, 700, 180);
		AddLeyMarker(4, 1500, -700, 270);

		// Quest 5: The Warren-King
		//-------------------------------------------------------------------------
		AddNpc(147473, L("[Bounty Hunter] Saule"), "f_pilgrimroad_41_4", 1800, -900, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1005);
			var alphaSpawnedKey = "Laima.Quests.f_pilgrimroad_41_4.Quest1005.AlphaSpawned";

			dialog.SetTitle(L("Saule"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A bounty hunter sharpening a curved blade*{/}"));
				await dialog.Msg(L("There's a Warren-King in the west tunnels. Twice my size, scarred from forty fights, and smart enough to bolt before any single hunter can pin him."));
				await dialog.Msg(L("That's the trick - he never fights. He runs. I've staked out three burrow-holes that are his escape routes. Plug them, and he'll have to stand."));

				var response = await dialog.Select(L("Plug the three staked burrow-holes, then kill 10 Repusbunnies to draw him up. He won't show his face for less than ten of his own pack dead. Are you in?"),
					Option(L("I'm in"), "help"),
					Option(L("Why doesn't he just fight?"), "info"),
					Option(L("That's a hunter's job, not mine"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*She taps three spots on a charcoal-sketched map*{/}"));
						await dialog.Msg(L("North slope, south crook, east scree. Each hole has a pine wedge I cut to size - jam it in deep and pack it with dirt and stones."));
						await dialog.Msg(L("Then ten kills. He'll come up shooting - he uses a Repusbunny bow, full draw. Take cover behind the warren-mounds."));
						break;

					case "info":
						await dialog.Msg(L("Survival, mostly. Bunnies that fight die quick. The ones that bolt and start a new warren keep breeding. Warren-Kings are the ones smart enough to figure that out. Hard to hunt, but easy to wait out if we had the time."));
						await dialog.Msg(L("We don't. He's been killing caravan-guards on the west road every fortnight for a season. The widows have stopped asking when their husbands are coming home."));
						break;

					case "leave":
						await dialog.Msg(L("The warren grows, more widows, and the caravan-guards stop signing up. I'll be here if you change your mind."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("plugBurrows", out var bObj)) return;
				if (!quest.TryGetProgress("killPack", out var pObj)) return;
				if (!quest.TryGetProgress("killAlpha", out var aObj)) return;

				if (bObj.Done && pObj.Done && aObj.Done)
				{
					await dialog.Msg(L("{#666666}*She tosses you a leather purse*{/}"));
					await dialog.Msg(L("Burrows plugged, king's down, and the warren will collapse on itself by week's end. The caravan-guards are drinking at my expense tonight, and I'm going to tell them why."));
					character.Variables.Perm.Remove(alphaSpawnedKey);
					character.Quests.Complete(questId);
				}
				else if (pObj.Done && !aObj.Done)
				{
					var hasSpawned = character.Variables.Perm.GetBool(alphaSpawnedKey, false);
					if (!hasSpawned)
					{
						character.Variables.Perm.Set(alphaSpawnedKey, true);
						if (SpawnTempMonsters(character, MonsterId.Repusbunny_Purple, 1, 150, TimeSpan.FromMinutes(5)))
						{
							await dialog.Msg(L("Here he comes!"));
							character.ServerMessage(L("{#FF9966}The Warren-King emerges from the west warren!{/}"));
						}
					}
					else
					{
						await dialog.Msg(L("Go find him."));
					}
				}
				else if (!bObj.Done)
				{
					await dialog.Msg(L("Three burrow-holes still open. North slope, south crook, east scree. Plug them or he runs the moment you draw a blade."));
				}
				else
				{
					await dialog.Msg(L("Burrows are sealed. Now kill ten of his pack - he won't surface for a single one less."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The warren collapsed in on itself. The widows came around asking whose blade did it - I told them mine, but they didn't believe me. They were right not to."));
			}
		});

		// Burrow-hole plug points for Quest 1005
		//-------------------------------------------------------------------------
		void AddBurrowHole(int burrowNumber, int x, int z, int direction)
		{
			AddNpc(47190, L("Warren Burrow-Hole"), "f_pilgrimroad_41_4", x, z, direction, async dialog =>
			{
				var character = dialog.Player;
				var questId = new QuestId("f_pilgrimroad_41_4", 1005);

				if (!character.Quests.IsActive(questId))
				{
					await dialog.Msg(L("{#666666}*A burrow-hole on the warren ridge, freshly dug*{/}"));
					return;
				}

				var variableKey = $"Laima.Quests.f_pilgrimroad_41_4.Quest1005.Burrow{burrowNumber}";
				if (character.Variables.Perm.GetBool(variableKey, false))
				{
					await dialog.Msg(L("{#666666}*Already plugged with stone and turf*{/}"));
					return;
				}

				var result = await character.TimeActions.StartAsync(L("Plugging burrow..."), "Cancel", "SITGROPE", TimeSpan.FromSeconds(3));

				if (result == TimeActionResult.Completed)
				{
					character.Variables.Perm.Set(variableKey, true);
					var count = character.Variables.Perm.GetInt("Laima.Quests.f_pilgrimroad_41_4.Quest1005.BurrowsPlugged", 0) + 1;
					character.Variables.Perm.Set("Laima.Quests.f_pilgrimroad_41_4.Quest1005.BurrowsPlugged", count);
					character.ServerMessage(LF("Burrows plugged: {0}/3", count));

					if (count >= 3)
						character.ServerMessage(L("{#FFD700}All burrows plugged! Now bait out the Warren-King.{/}"));
				}
				else
				{
					character.ServerMessage(L("Plugging interrupted."));
				}
			});
		}

		AddBurrowHole(1, 1500, -800, 0);
		AddBurrowHole(2, 1900, -1100, 90);
		AddBurrowHole(3, 1600, -1200, 180);

		// Quest 6: West Road Sweep
		//-------------------------------------------------------------------------
		AddNpc(155146, L("[Road-Marshal] Aldona"), "f_pilgrimroad_41_4", 1700, 1000, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1006);

			dialog.SetTitle(L("Aldona"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("{#666666}*A road-marshal pinning a fresh ledger-page to her board*{/}"));
				await dialog.Msg(L("The west stretch is the worst on the whole pilgrim road. Three monster types, none of them small, all of them territorial. We have to sweep it weekly or it doesn't stay swept."));
				await dialog.Msg(L("The Marshal's office in Salvia doesn't pay on a hunter's word. They pay on tally-stones. One stone per kill, dropped on the Toll-Stone Cairn at the bend, counted by the cantor on Sundays."));

				var response = await dialog.Select(L("Kill 12 Repusbunnies, 12 Bow Repusbunnies, and 12 Tree-Mage Stubs, then drop one tally-stone on the Toll-Stone Cairn for the marshal-rolls. Take the contract?"),
					Option(L("I'll take the contract"), "help"),
					Option(L("Why tally-stones, not coin-receipts?"), "info"),
					Option(L("Find a closer hunter"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("{#666666}*She gives you a smooth river-stone*{/}"));
						await dialog.Msg(L("Thirty-six kills. Don't pad the count - the cantor weighs the cairn-stones against my ledger and the math has to add up."));
						await dialog.Msg(L("The cairn's at the toll bend, where the road kinks south. Drop the stone, let it lie. I'll countersign after."));
						break;

					case "info":
						await dialog.Msg(L("Coin-receipts can be forged or lost in the post. A river-stone in a sealed cairn can't. It's an old Pelke custom from the demon war - simple and honest."));
						await dialog.Msg(L("Pay scales with the count, so don't lie about the kills. If I undercount you, complain to me. If you overcount, the cantor finds out by Sunday and the marshal's office bans you for a year."));
						break;

					case "leave":
						await dialog.Msg(L("Then the west stretch stays thick and the next pilgrim-cart pays for it. There's always a closer hunter, but rarely a willing one."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killBunnies", out var bObj)) return;
				if (!quest.TryGetProgress("killBowBunnies", out var wObj)) return;
				if (!quest.TryGetProgress("killStubs", out var sObj)) return;
				if (!quest.TryGetProgress("dropTally", out var tObj)) return;

				if (bObj.Done && wObj.Done && sObj.Done && tObj.Done)
				{
					await dialog.Msg(L("{#666666}*She marks your ledger-row in tar-ink and counts coin into a cloth*{/}"));
					await dialog.Msg(L("Sweep's done, cairn-stone dropped, math adds up. Honest marshal's coin."));
					await dialog.Msg(L("Stop by next fortnight if you want the contract again. The road never stops needing it."));
					character.Quests.Complete(questId);
				}
				else if (bObj.Done && wObj.Done && sObj.Done)
				{
					await dialog.Msg(L("Sweep's complete. Now drop your tally-stone on the cairn at the toll bend - I can't pay until the cantor counts it."));
				}
				else
				{
					await dialog.Msg(L("Twelve of each. Keep the river-stone in your pocket. Keep at it."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("The marshal's patrols cover the west stretch hourly now, wages paid by your levy. The cantor still weighs your stone every Sunday - says it's heavier than it should be. That's his way of giving a compliment."));
			}
		});

		// Toll-Stone Cairn for Quest 1006 tally
		//-------------------------------------------------------------------------
		AddNpc(47190, L("Toll-Stone Cairn"), "f_pilgrimroad_41_4", 1700, 1100, 90, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_pilgrimroad_41_4", 1006);

			if (!character.Quests.IsActive(questId))
			{
				await dialog.Msg(L("{#666666}*A weather-worn cairn beside the toll bend, ringed with old tally-stones*{/}"));
				return;
			}

			var droppedKey = "Laima.Quests.f_pilgrimroad_41_4.Quest1006.TallyDropped";
			if (character.Variables.Perm.GetBool(droppedKey, false))
			{
				await dialog.Msg(L("{#666666}*Your tally-stone is already on the cairn*{/}"));
				return;
			}

			if (!character.Quests.TryGetById(questId, out var quest)) return;
			if (!quest.TryGetProgress("killBunnies", out var bObj)) return;
			if (!quest.TryGetProgress("killBowBunnies", out var wObj)) return;
			if (!quest.TryGetProgress("killStubs", out var sObj)) return;

			if (!(bObj.Done && wObj.Done && sObj.Done))
			{
				await dialog.Msg(L("{#666666}*The cairn is ready to receive a tally-stone, but you haven't finished the sweep*{/}"));
				return;
			}

			var result = await character.TimeActions.StartAsync(L("Dropping tally-stone..."), "Cancel", "PRAY", TimeSpan.FromSeconds(3));

			if (result == TimeActionResult.Completed)
			{
				character.Variables.Perm.Set(droppedKey, true);
				character.ServerMessage(L("{#FFD700}Tally-stone laid on the cairn. Return to Road-Marshal Aldona.{/}"));
			}
			else
			{
				character.ServerMessage(L("Drop interrupted."));
			}
		});
	}
}

//-----------------------------------------------------------------------------
// QUEST DEFINITIONS
//-----------------------------------------------------------------------------

public class FPilgrimroad414Quest1001 : QuestScript
{
	protected override void Load()
	{
		SetId("f_pilgrimroad_41_4", 1001);
		SetName(L("Purple Repusbunny Kill"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Purple Repusbunnies blocking the west pilgrim road."));
		SetLocation("f_pilgrimroad_41_4");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Tollwarden] Mindaugas"), "f_pilgrimroad_41_4");

		AddObjective("killBunnies", L("Kill Purple Repusbunnies"),
			new KillObjective(45, new[] { MonsterId.Repusbunny_Purple }));

		AddReward(new ExpReward(11900, 8100));
		AddReward(new SilverReward(15000));
		AddReward(new ItemReward(640086, 1));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
	}
}

public class FPilgrimroad414Quest1002 : QuestScript
{
	protected override void Load()
	{
		SetId("f_pilgrimroad_41_4", 1002);
		SetName(L("Bowbunny Fletchings"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Bow Repusbunnies and bring fletchings to map the ridge shots."));
		SetLocation("f_pilgrimroad_41_4");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Caravan-Guard] Jurga"), "f_pilgrimroad_41_4");

		AddObjective("killBowBunnies", L("Kill Bow Repusbunnies"),
			new KillObjective(30, new[] { MonsterId.Repusbunny_Bow_Purple }));

		AddObjective("gatherFletchings", L("Gather fletchings"),
			new CollectItemObjective(650254, 8));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 3));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650254, character.Inventory.CountItem(650254), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650254, character.Inventory.CountItem(650254), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FPilgrimroad414Quest1003 : QuestScript
{
	protected override void Load()
	{
		SetId("f_pilgrimroad_41_4", 1003);
		SetName(L("Mage-Stub Bark"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Tree-Mage Stubs and bring rootspell bark for hedge wards."));
		SetLocation("f_pilgrimroad_41_4");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Hedge-Witch] Vaiva"), "f_pilgrimroad_41_4");

		AddObjective("killStubs", L("Kill Tree-Mage Stubs"),
			new KillObjective(15, new[] { MonsterId.Stub_Tree_Mage }));

		AddObjective("gatherBark", L("Gather bark strips"),
			new CollectItemObjective(650256, 5));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 3));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650256, character.Inventory.CountItem(650256), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650256, character.Inventory.CountItem(650256), InventoryItemRemoveMsg.Destroyed);
	}
}

public class FPilgrimroad414Quest1004 : QuestScript
{
	protected override void Load()
	{
		SetId("f_pilgrimroad_41_4", 1004);
		SetName(L("Waystone Tangles"));
		SetType(QuestType.Sub);
		SetDescription(L("Break Rootcrystals bending the waystone ley off the west road."));
		SetLocation("f_pilgrimroad_41_4");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Waystone-Keeper] Darius"), "f_pilgrimroad_41_4");

		AddObjective("breakCrystals", L("Break Rootcrystals"),
			new KillObjective(12, new[] { MonsterId.Rootcrystal_05 }));

		AddObjective("retrueMarkers", L("Re-true the four ley-marker waystones"),
			new VariableCheckObjective("Laima.Quests.f_pilgrimroad_41_4.Quest1004.MarkersTrued", 4, true));

		AddReward(new ExpReward(11900, 8100));
		AddReward(new SilverReward(15000));
		AddReward(new ItemReward(640086, 1));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 3));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_pilgrimroad_41_4.Quest1004.MarkersTrued");
		for (int i = 1; i <= 4; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_pilgrimroad_41_4.Quest1004.Marker{i}");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_pilgrimroad_41_4.Quest1004.MarkersTrued");
		for (int i = 1; i <= 4; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_pilgrimroad_41_4.Quest1004.Marker{i}");
	}
}

public class FPilgrimroad414Quest1005 : QuestScript
{
	protected override void Load()
	{
		SetId("f_pilgrimroad_41_4", 1005);
		SetName(L("The Warren-King"));
		SetType(QuestType.Sub);
		SetDescription(L("Kill Purple Repusbunnies to draw out the Warren-King from the west warren."));
		SetLocation("f_pilgrimroad_41_4");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Bounty Hunter] Saule"), "f_pilgrimroad_41_4");

		AddObjective("plugBurrows", L("Plug the three Warren burrow-holes"),
			new VariableCheckObjective("Laima.Quests.f_pilgrimroad_41_4.Quest1005.BurrowsPlugged", 3, true));

		AddObjective("killPack", L("Kill Purple Repusbunnies"),
			new KillObjective(10, new[] { MonsterId.Repusbunny_Purple }));

		AddObjective("killAlpha", L("Defeat the Warren-King"),
			new KillObjective(1, new[] { MonsterId.Repusbunny_Purple }));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 3));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_pilgrimroad_41_4.Quest1005.BurrowsPlugged");
		for (int i = 1; i <= 3; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_pilgrimroad_41_4.Quest1005.Burrow{i}");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_pilgrimroad_41_4.Quest1005.BurrowsPlugged");
		for (int i = 1; i <= 3; i++)
			character.Variables.Perm.Remove($"Laima.Quests.f_pilgrimroad_41_4.Quest1005.Burrow{i}");
	}
}

public class FPilgrimroad414Quest1006 : QuestScript
{
	protected override void Load()
	{
		SetId("f_pilgrimroad_41_4", 1006);
		SetName(L("West Road Sweep"));
		SetType(QuestType.Sub);
		SetDescription(L("Standard sweep of Purple Repusbunnies, Bow Repusbunnies, and Tree-Mage Stubs."));
		SetLocation("f_pilgrimroad_41_4");
		SetAutoTracked(true);
		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Road-Marshal] Aldona"), "f_pilgrimroad_41_4");

		AddObjective("killBunnies", L("Kill Purple Repusbunnies"),
			new KillObjective(12, new[] { MonsterId.Repusbunny_Purple }));

		AddObjective("killBowBunnies", L("Kill Bow Repusbunnies"),
			new KillObjective(12, new[] { MonsterId.Repusbunny_Bow_Purple }));

		AddObjective("killStubs", L("Kill Tree-Mage Stubs"),
			new KillObjective(12, new[] { MonsterId.Stub_Tree_Mage }));

		AddObjective("dropTally", L("Lay a tally-stone on the Toll-Stone Cairn"),
			new VariableCheckObjective("Laima.Quests.f_pilgrimroad_41_4.Quest1006.TallyDropped", 1, true));

		AddReward(new ExpReward(26400, 18000));
		AddReward(new SilverReward(18800));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 3));
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_pilgrimroad_41_4.Quest1006.TallyDropped");
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Variables.Perm.Remove("Laima.Quests.f_pilgrimroad_41_4.Quest1006.TallyDropped");
	}
}
