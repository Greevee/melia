//--- Melia Script ----------------------------------------------------------
// Inner Enceinte District Quest NPCs
//--- Description -----------------------------------------------------------
// Petrification-cursed quests for the Inner Enceinte ruins.
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

public class FFlash64QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// Quest 1: Lemuria Swarm
		//-------------------------------------------------------------------------
		AddNpc(20127, L("[Enceinte Warden] Oswin"), "f_flash_64", -579, 1498, 90, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_64", 1001);

			dialog.SetTitle(L("Oswin"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Welcome to the Enceinte. The wall behind me is the last ring before the cursed heart of the city. Inside that ring, nothing flesh survives more than an hour."));
				await dialog.Msg(L("The Lemurias swarming the wall are the immediate problem. Smaller than the true Lemurs in Downtown, faster, and meaner - they bite, they don't roar."));
				await dialog.Msg(L("Thin twenty-two and the inner-wall ward-crew gets a working shift in. The wall holds the heart contained. The wall is the whole job."));

				var response = await dialog.Select(L("Will you kill the Lemurias for us?"),
					Option(L("I'll handle the swarm"), "help"),
					Option(L("What's in the heart?"), "info"),
					Option(L("Maybe later"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Twenty-two. Don't stop swinging - they swarm and a swarm of bites adds up faster than one big hit."));
						await dialog.Msg(L("Charm at the neck. The bites grey-streak."));
						break;

					case "info":
						await dialog.Msg(L("Full curse. Anyone who walks in walks out as stone, conscious if they're unlucky. Nothing moves in there but rumor and Gargoyle."));
						await dialog.Msg(L("That's why the wall matters. Thirty years of patrols, and we're still here because of it."));
						break;

					case "leave":
						await dialog.Msg(L("If the wall fails, the curse leaks west. I'm not moving."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killLemurias", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("Quiet up there. Crew worked a full shift. Three sections repaired."));
					await dialog.Msg(L("Pay's yours. Thanks."));

					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Still too thick. Keep cutting."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Inner wall's solid all the way around. First time in a decade."));
			}
		});

		// Quest 2: The Bunny Nests
		//-------------------------------------------------------------------------
		AddNpc(20117, L("[Warren-Inspector] Tobi"), "f_flash_64", 556, 341, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_64", 1003);

			dialog.SetTitle(L("Tobi"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Repusbunnies look harmless. They burrow under the enceinte floor and nest on the statues - the stone keeps body-warmth a long time, and they like that."));
				await dialog.Msg(L("Each nest has a token tucked in it. Something the statue was holding when the curse took them - a ring, a hairpin, a folded letter. Last trace of a life."));
				await dialog.Msg(L("Thin the Rubabos that guard the burrows and bring me four tokens. Their families pay for the closure."));

				var response = await dialog.Select(L("Will you bring me the tokens?"),
					Option(L("I'll bring the tokens"), "help"),
					Option(L("Why pay for trinkets?"), "info"),
					Option(L("Leave them buried"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Fifteen Rubabos for the main warrens. Tokens are wrapped in nesting fur - look for the padded clumps."));
						await dialog.Msg(L("Handle gentle. Sentimental cargo, not just archive."));
						break;

					case "info":
						await dialog.Msg(L("Try paying for closure. Half the time the family didn't even know which statue used to be theirs. A ring matches a record, and they finally have a place to leave flowers."));
						await dialog.Msg(L("Whole little business built on grief. Honest work, just sad."));
						break;

					case "leave":
						await dialog.Msg(L("Nests spread. Every week, another row of statues gets nested on and the tokens get chewed. I'd rather not wait."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killRubabos", out var killObj)) return;
				if (!quest.TryGetProgress("recoverTokens", out var tokenObj)) return;

				if (killObj.Done && tokenObj.Done)
				{
					await dialog.Msg(L("Four tokens. Match them to the registry tonight - four families with closure by week's end."));
					await dialog.Msg(L("Pay's yours. I'll send word about what they turn out to be. Always curious work."));

					character.Inventory.Remove(650455, character.Inventory.CountItem(650455), InventoryItemRemoveMsg.Given);

					character.Quests.Complete(questId);
				}
				else
				{
					var status = "";
					if (!killObj.Done)
						status += L("More Rubabos still guarding burrows. ");
					if (!tokenObj.Done)
						status += L("More tokens still in the nests. ");

					await dialog.Msg(LF("Keep at it. {0}", status));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("One was a wedding ring. Widow came by yesterday, wept for an hour, took it home. That's the part of the job that pays in something else."));
			}
		});

		// Quest 3: The Saltisdaughter Archers
		//-------------------------------------------------------------------------
		AddNpc(20142, L("[Curse-Warden] Alek"), "f_flash_64", 242, -1188, 89, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_64", 1004);

			dialog.SetTitle(L("Alek"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Saltisdaughter cabal sent archers up to the Enceinte last month. Same outfit Pavel's burning plates over in Roxona, same one Inspector Thane's caught with branded livestock in Ruklys."));
				await dialog.Msg(L("These ones are different. Their arrowheads are sigil-stamped - every shaft they fire that hits stone reinforces the curse-grid a little more."));
				await dialog.Msg(L("Twelve archers, five unfired arrows. Thane's case needs the third district to close. We're it."));

				var response = await dialog.Select(L("Will you bring me the arrows?"),
					Option(L("I'll bring the arrows"), "help"),
					Option(L("This closes the case?"), "info"),
					Option(L("Let them shoot"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Look in the quivers - the unfired ones. The fired arrowheads are dangerous to handle. Wrap whatever you take."));
						await dialog.Msg(L("Don't break the chain-of-custody. Thane's been clear about that."));
						break;

					case "info":
						await dialog.Msg(L("Roxona has the brand-plates. Ruklys has the Moyabu brands. We get the arrows. Three districts, one cabal, one case - and Fedimian dissolves them permanently."));
						await dialog.Msg(L("Wait too long, the grid extends into the heart, and the curse stops being something we can hold in. So no, we can't wait."));
						break;

					case "leave":
						await dialog.Msg(L("Every arrow they loose is another node on the grid. Reconsider before they finish a circuit."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killArchers", out var killObj)) return;
				if (!quest.TryGetProgress("gatherArrows", out var arrowObj)) return;

				if (killObj.Done && arrowObj.Done)
				{
					await dialog.Msg(L("Five sealed arrows. Thane's evidence chain is complete - all three districts represented."));
					await dialog.Msg(L("Pay's yours. Your name goes on the manifest. Cabal's done."));

					character.Inventory.Remove(650760, character.Inventory.CountItem(650760), InventoryItemRemoveMsg.Given);

					character.Quests.Complete(questId);
				}
				else
				{
					var status = "";
					if (!killObj.Done)
						status += L("More Saltisdaughter Archers still loosing. ");
					if (!arrowObj.Done)
						status += L("More arrows still in quivers. ");

					await dialog.Msg(LF("Keep at it. {0}", status));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Twelve arrests across three districts. Cabal dissolved on paper, and the grid's gone dark on the wall. That's a clean win - rare around here."));
			}
		});

		// Quest 4: The Stone-Mother
		//-------------------------------------------------------------------------
		AddNpc(147473, L("[Bounty Hunter] Lira"), "f_flash_64", -150, 281, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_64", 1005);
			var motherSpawnedKey = "Laima.Quests.f_flash_64.Quest1005.MotherSpawned";

			dialog.SetTitle(L("Lira"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Got a contract worth your time. There's a Lemuria matriarch ruling the swarm - half-stone through neck and shoulders, twice the size of the others, and faster than she has any right to be."));
				await dialog.Msg(L("Drop ten of her daughters and she comes out to enforce order. Bounty's huge - her stone-mantle carves into throat-wards, the only counter to the breath-stealer curse."));

				var response = await dialog.Select(L("So? Want the contract?"),
					Option(L("I'll take the contract"), "help"),
					Option(L("Throat-wards?"), "info"),
					Option(L("Pass"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Ten daughters first. She lunges from the stone shoulder - stay off her right or she'll fold you in half."));
						await dialog.Msg(L("Good hunting."));
						break;

					case "info":
						await dialog.Msg(L("Ward against stone-lung. The breath-stealer strain - you keep breathing for a few minutes after the lungs turn. Conscious the whole time."));
						await dialog.Msg(L("Mantle from a Lemuria matriarch is the only material that works. Limited supply, by definition."));
						break;

					case "leave":
						await dialog.Msg(L("Bounty climbs. I'll be here."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killPack", out var packObj)) return;
				if (!quest.TryGetProgress("killMother", out var motherObj)) return;

				if (packObj.Done && motherObj.Done)
				{
					await dialog.Msg(L("Mantle intact. Season's worth of throat-wards off that one piece."));
					await dialog.Msg(L("Bounty paid, plus my cut. You just saved a few dozen lungs."));

					character.Variables.Perm.Remove(motherSpawnedKey);

					character.Quests.Complete(questId);
				}
				else if (packObj.Done && !motherObj.Done)
				{
					var hasSpawned = character.Variables.Perm.GetBool(motherSpawnedKey, false);
					if (!hasSpawned)
					{
						character.Variables.Perm.Set(motherSpawnedKey, true);

						if (SpawnTempMonsters(character, MonsterId.Lemuria, 1, 120, TimeSpan.FromMinutes(5)))
						{
							await dialog.Msg(L("That howl - lower than the daughters, you can hear the stone in it. She's coming."));
							await dialog.Msg(L("{#FF9966}Move - and don't let her slip back into the swarm.{/}"));
							character.ServerMessage(L("{#FF9966}The Stone-Mother emerges, mantle gleaming!{/}"));
						}
					}
					else
					{
						await dialog.Msg(L("She's loose. Don't lose her."));
					}
				}
				else
				{
					await dialog.Msg(L("Swarm's still tight. She won't show."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Mantle went to the throat-ward forge. Lungs being saved as we speak."));
			}
		});

		// Quest 5: The Enceinte Wall Walk
		//-------------------------------------------------------------------------
		AddNpc(20018, L("[Wall Captain] Ember"), "f_flash_64", -116, 2084, 45, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_64", 1006);

			dialog.SetTitle(L("Ember"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("I run the wall patrols. Right now I'm not running anything - the walk is impassable on both flanks."));
				await dialog.Msg(L("Lemurias scale up from inside the heart. Repusbunnies tunnel under from outside. Either alone is a nuisance. Both at once and my patrols come back short."));
				await dialog.Msg(L("Twelve and twelve. Once you clear them, the walk is patrol-ready by dawn."));

				var response = await dialog.Select(L("Will you clear both sides of the walk?"),
					Option(L("I'll clear both sides"), "help"),
					Option(L("Why not just close the walk?"), "info"),
					Option(L("Maybe later"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Twelve and twelve. Watch your footing - the bunnies tunnel right under the path."));
						await dialog.Msg(L("If Oswin offers you charm-cotton, take it. The Lemuria bites grey-streak."));
						break;

					case "info":
						await dialog.Msg(L("If we close the walk, no one's checking the wall. Wall isn't checked, sections fail. Sections fail, the heart leaks west and we're all part of it."));
						await dialog.Msg(L("So the walk stays open. Always."));
						break;

					case "leave":
						await dialog.Msg(L("Walk doesn't wait. Neither does the curse."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killLemurias", out var lemObj)) return;
				if (!quest.TryGetProgress("killBunnies", out var bunObj)) return;

				if (lemObj.Done && bunObj.Done)
				{
					await dialog.Msg(L("Both clear. Patrol walks the full perimeter at dawn."));
					await dialog.Msg(L("Pay's yours. The wall holds another season because of you."));

					character.Quests.Complete(questId);
				}
				else
				{
					var status = "";
					if (!lemObj.Done)
						status += L("More Lemurias still scaling. ");
					if (!bunObj.Done)
						status += L("More Repusbunnies still tunneling. ");

					await dialog.Msg(LF("Keep pushing. {0}", status));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Patrol's walking clean every shift. Tightest the Enceinte's been since I took the post."));
			}
		});
	}
}

//-----------------------------------------------------------------------------
// QUEST DEFINITIONS
//-----------------------------------------------------------------------------

public class LemuriaSwarmQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_64", 1001);
		SetName(L("Lemuria Swarm"));
		SetType(QuestType.Sub);
		SetDescription(L("Oswin can't get his ward-crew on the inner wall while Lemurias swarm it. Thin the swarm so a full shift can run."));
		SetLocation("f_flash_64");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Enceinte Warden] Oswin"), "f_flash_64");

		AddObjective("killLemurias", L("Kill swarming Lemurias"),
			new KillObjective(22, new[] { MonsterId.Lemuria }));

		AddReward(new ExpReward(11900, 8100));
		AddReward(new SilverReward(15000));
		AddReward(new ItemReward(640086, 1));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
	}
}

public class TheBunnyNestsQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_64", 1003);
		SetName(L("The Bunny Nests"));
		SetType(QuestType.Sub);
		SetDescription(L("Tobi's recovery office matches lost tokens to grieving families. Clear the Rubabos guarding the burrows and recover four citizen tokens."));
		SetLocation("f_flash_64");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Warren-Inspector] Tobi"), "f_flash_64");

		AddObjective("killRubabos", L("Kill Rubabos guarding the burrows"),
			new KillObjective(15, new[] { MonsterId.Rubabos }));

		AddObjective("recoverTokens", L("Recover citizen tokens"),
			new CollectItemObjective(650455, 4));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));

		AddDrop(650455, 0.40f, MonsterId.Rubabos);
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650455, character.Inventory.CountItem(650455), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650455, character.Inventory.CountItem(650455), InventoryItemRemoveMsg.Destroyed);
	}
}

public class TheSaltisdaughterArchersQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_64", 1004);
		SetName(L("The Saltisdaughter Archers"));
		SetType(QuestType.Sub);
		SetDescription(L("Alek needs the third district's evidence to close Inspector Thane's cross-district case. Kill the Saltisdaughter Archers and recover five sealed brand-arrows."));
		SetLocation("f_flash_64");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Curse-Warden] Alek"), "f_flash_64");

		AddObjective("killArchers", L("Kill Saltisdaughter Archers"),
			new KillObjective(12, new[] { MonsterId.Saltisdaughter_Bow }));

		AddObjective("gatherArrows", L("Recover sealed brand-arrows"),
			new CollectItemObjective(650760, 5));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));

		AddDrop(650760, 0.50f, MonsterId.Saltisdaughter_Bow);
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650760, character.Inventory.CountItem(650760), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650760, character.Inventory.CountItem(650760), InventoryItemRemoveMsg.Destroyed);
	}
}

public class TheStoneMotherQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_64", 1005);
		SetName(L("The Stone-Mother"));
		SetType(QuestType.Sub);
		SetDescription(L("Lira has a contract on the Lemuria matriarch ruling the swarm. Thin the daughters to draw her out, then bring down the Stone-Mother for her throat-ward mantle."));
		SetLocation("f_flash_64");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Bounty Hunter] Lira"), "f_flash_64");

		AddObjective("killPack", L("Thin the Lemuria pack"),
			new KillObjective(10, new[] { MonsterId.Lemuria }));

		AddObjective("killMother", L("Defeat the Stone-Mother"),
			new KillObjective(1, new[] { MonsterId.Lemuria }));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));
	}
}

public class TheEnceinteWallWalkQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_64", 1006);
		SetName(L("The Enceinte Wall Walk"));
		SetType(QuestType.Sub);
		SetDescription(L("Captain Ember's wall patrols are stalled until both flanks of the walk are clear. Kill the Lemurias scaling from inside and the Repusbunnies tunneling from outside."));
		SetLocation("f_flash_64");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Wall Captain] Ember"), "f_flash_64");

		AddObjective("killLemurias", L("Kill Lemurias scaling from inside"),
			new KillObjective(12, new[] { MonsterId.Lemuria }));

		AddObjective("killBunnies", L("Kill Repusbunnies tunneling from outside"),
			new KillObjective(12, new[] { MonsterId.Repusbunny }));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));
	}
}
