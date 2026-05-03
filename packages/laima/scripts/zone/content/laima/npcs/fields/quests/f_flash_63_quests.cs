//--- Melia Script ----------------------------------------------------------
// Downtown Quest NPCs
//--- Description -----------------------------------------------------------
// Petrification-cursed quests for the Downtown ruins.
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

public class FFlash63QuestNpcsScript : GeneralScript
{
	protected override void Load()
	{
		// Quest 1: Lemur Howl
		//-------------------------------------------------------------------------
		AddNpc(20100, L("[District Warden] Grelle"), "f_flash_63", -46, 1211, 180, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_63", 1001);

			dialog.SetTitle(L("Grelle"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Welcome to Downtown. Used to be the civic heart. Now it's mostly Lemurs and an awful lot of noise."));
				await dialog.Msg(L("The howl alone drives off any ward-crew that tries to work here. Worse than that - prolonged exposure stiffens the joints. Slow petrification, by sound."));
				await dialog.Msg(L("Thin twenty-two and the volume drops enough for my crew to get a shift in. That's the goal."));

				var response = await dialog.Select(L("Will you kill the Lemurs for me?"),
					Option(L("I'll handle the Lemurs"), "help"),
					Option(L("Stiffens by sound?"), "info"),
					Option(L("Try another district"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Twenty-two. Move fast - they pack in, and a packed pack is the worst part."));
						await dialog.Msg(L("Cotton in the ears. You'll thank me."));
						break;

					case "info":
						await dialog.Msg(L("My senior ward-hand worked a month without ear cotton. He doesn't bend his left knee anymore. We learned."));
						await dialog.Msg(L("Cotton, or short shifts, or both. I prefer both."));
						break;

					case "leave":
						await dialog.Msg(L("If Downtown doesn't come back, the rest of the city has nothing to come back to. So we work."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killLemurs", out var killObj)) return;

				if (killObj.Done)
				{
					await dialog.Msg(L("Volume's down. My crew's working their first full shift in six months."));
					await dialog.Msg(L("Pay's yours. Two cotton plugs on the house."));

					character.Quests.Complete(questId);
				}
				else
				{
					await dialog.Msg(L("Still howling. Keep at it."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Three ward-lines reset. First ward-work in Downtown since the magistrate turned to stone."));
			}
		});

		// Quest 2: Civic Records
		//-------------------------------------------------------------------------
		AddNpc(20114, L("[Civic Scribe] Agatha"), "f_flash_63", 303, -980, 89, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_63", 1003);

			dialog.SetTitle(L("Agatha"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Grelle send you? She does that. We share work when she has a spare pair of hands."));
				await dialog.Msg(L("Downtown's civic records are in the vaults - deeds, marriage rolls, debt ledgers. Hammer-Goblins curl up on them for warmth and chew the corners."));
				await dialog.Msg(L("Without those records, two hundred families lose their property claims. Get me four volumes, intact, and Downtown still legally exists."));

				var response = await dialog.Select(L("Will you bring me the volumes?"),
					Option(L("I'll bring the volumes"), "help"),
					Option(L("Why do they lie on the books?"), "info"),
					Option(L("Maybe later"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Fifteen Hammer-Goblins should clear the main vault. Volumes are leather-bound and clasped - look in the niches."));
						await dialog.Msg(L("Bring them unopened. Chain-of-custody matters for legal validity."));
						break;

					case "info":
						await dialog.Msg(L("The pages give off a slight warmth. Something about the curse-script the records are sealed with. The goblins aren't malicious - they just like a warm bed."));
						await dialog.Msg(L("Inconvenient, all the same."));
						break;

					case "leave":
						await dialog.Msg(L("If Downtown legally folds, the families who owned property here lose everything. I'd rather not let that happen."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killHammerGoblins", out var killObj)) return;
				if (!quest.TryGetProgress("recoverRecords", out var recObj)) return;

				if (killObj.Done && recObj.Done)
				{
					await dialog.Msg(L("Four volumes, intact. That's two hundred families with their legal identity restored."));
					await dialog.Msg(L("Pay's yours. Your name goes in the margin of the restoration order."));

					character.Inventory.Remove(650785, character.Inventory.CountItem(650785), InventoryItemRemoveMsg.Given);

					character.Quests.Complete(questId);
				}
				else
				{
					var status = "";
					if (!killObj.Done)
						status += L("More Hammer-Goblins still in the vaults. ");
					if (!recObj.Done)
						status += L("More record-volumes still missing. ");

					await dialog.Msg(LF("Keep at it. {0}", status));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("One family sent me a hand-stitched blanket as thanks. Their great-grandmother's trade. I almost cried."));
			}
		});

		// Quest 3: Ritual Brand Pages
		//-------------------------------------------------------------------------
		AddNpc(20102, L("[Curse-Scholar] Hedvig"), "f_flash_63", 104, -912, 90, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_63", 1004);

			dialog.SetTitle(L("Hedvig"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("There's a ritual operation working out of the old bath-house. Wand-Goblins, but they're being directed - someone literate is feeding them sigil pages."));
				await dialog.Msg(L("The pages are a sigil-chain. Complete and chanted, it fires petrification across a city block at once. Population zero on whatever's caught inside."));
				await dialog.Msg(L("Twelve goblins, five pages. Pages are the priority - the chain doesn't fire if I have any of them."));

				var response = await dialog.Select(L("Will you bring me the pages?"),
					Option(L("I'll bring the pages"), "help"),
					Option(L("Block-petrification, really?"), "info"),
					Option(L("Evacuate Downtown"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Pages are in oiled scroll-cases on every goblin's belt. Don't open them - the sigils are still active and they bite into eyes that read them."));
						await dialog.Msg(L("Bring them sealed. I'll handle the rest."));
						break;

					case "info":
						await dialog.Msg(L("Yes. There's a reason the original mage who started this curse died horribly. The work bends back on whoever runs it. The cabal doesn't care - they think they're holy."));
						await dialog.Msg(L("Same Saltisdaughter outfit Pavel's burning plates over in Roxona. They're networked across districts. Each district pulls a different limb."));
						break;

					case "leave":
						await dialog.Msg(L("To where? Half the adjacent districts are already half-cursed. Downtown is the fallback for everyone else. We can't move it."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killWandGoblins", out var killObj)) return;
				if (!quest.TryGetProgress("gatherPages", out var pageObj)) return;

				if (killObj.Done && pageObj.Done)
				{
					await dialog.Msg(L("Five sealed cases. The chain's broken - they don't have the full sequence anymore."));
					await dialog.Msg(L("Pay's yours. I'll run the counter-ritual tonight. Smoke goes blue when it takes."));

					character.Inventory.Remove(650825, character.Inventory.CountItem(650825), InventoryItemRemoveMsg.Given);

					character.Quests.Complete(questId);
				}
				else
				{
					var status = "";
					if (!killObj.Done)
						status += L("More Wand-Goblins still chanting. ");
					if (!pageObj.Done)
						status += L("More pages still missing. ");

					await dialog.Msg(LF("Keep at it. {0}", status));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Smoke went blue. Counter-ritual took. The block-petrifier threat is off the table for now."));
			}
		});

		// Quest 4: The Stonefrosted Alpha
		//-------------------------------------------------------------------------
		AddNpc(20103, L("[Bounty Hunter] Nikolai"), "f_flash_63", 952, -844, 0, async dialog =>
		{
			var character = dialog.Player;
			var questId = new QuestId("f_flash_63", 1005);
			var alphaSpawnedKey = "Laima.Quests.f_flash_63.Quest1005.AlphaSpawned";

			dialog.SetTitle(L("Nikolai"));

			if (!character.Quests.Has(questId))
			{
				await dialog.Msg(L("Got a contract you'll like. Or hate. Depends on the day."));
				await dialog.Msg(L("There's an Alpha Lemur out east that caught a bad variant of the curse - a cold-strain that crusted his fur with rime that doesn't melt. Hits like a hammer, leads with the cold."));
				await dialog.Msg(L("Lesser Lemurs defer to him. Drop ten, he comes out to set them straight. Bounty's on the rime-pelt - it's the only material that makes true cold-wards."));

				var response = await dialog.Select(L("So? Want the contract?"),
					Option(L("I'll take the contract"), "help"),
					Option(L("What's a cold-ward?"), "info"),
					Option(L("Pass"), "leave")
				);

				switch (response)
				{
					case "help":
						character.Quests.Start(questId);
						await dialog.Msg(L("Ten. He hits with a rime-slam that staggers most people once. Don't get hit twice."));
						await dialog.Msg(L("Stay mobile. He's heavy."));
						break;

					case "info":
						await dialog.Msg(L("Ward against the cold-curse strain. The variant that traps you conscious inside the stone."));
						await dialog.Msg(L("Bad way to go. Cold-wards prevent it. Limited supply because his rime is the only source."));
						break;

					case "leave":
						await dialog.Msg(L("Bounty climbs every week. I'll be here."));
						break;
				}
			}
			else if (character.Quests.IsActive(questId))
			{
				if (!character.Quests.TryGetById(questId, out var quest)) return;
				if (!quest.TryGetProgress("killPack", out var packObj)) return;
				if (!quest.TryGetProgress("killAlpha", out var alphaObj)) return;

				if (packObj.Done && alphaObj.Done)
				{
					await dialog.Msg(L("Rime-pelt intact. Cold-wards for a year off that one piece."));
					await dialog.Msg(L("Bounty paid, plus my cut. Good work."));

					character.Variables.Perm.Remove(alphaSpawnedKey);

					character.Quests.Complete(questId);
				}
				else if (packObj.Done && !alphaObj.Done)
				{
					var hasSpawned = character.Variables.Perm.GetBool(alphaSpawnedKey, false);
					if (!hasSpawned)
					{
						character.Variables.Perm.Set(alphaSpawnedKey, true);

						if (SpawnTempMonsters(character, MonsterId.Lemur, 1, 120, TimeSpan.FromMinutes(5)))
						{
							await dialog.Msg(L("Pack's thinned. That howl - three octaves down. He's coming."));
							await dialog.Msg(L("{#FF9966}Move - the rime glints right before he slams.{/}"));
							character.ServerMessage(L("{#FF9966}The Stonefrosted Alpha charges out, rime-fur steaming!{/}"));
						}
					}
					else
					{
						await dialog.Msg(L("He's loose. Don't lose him - he heals up if he goes back behind cover."));
					}
				}
				else
				{
					await dialog.Msg(L("Pack's still tight. He won't show."));
				}
			}
			else if (character.Quests.HasCompleted(questId))
			{
				await dialog.Msg(L("Pelt shipped to the cold-ward forge. A dozen wards going out next week. Saved a few lungs already."));
			}
		});
	}
}

//-----------------------------------------------------------------------------
// QUEST DEFINITIONS
//-----------------------------------------------------------------------------

public class LemurHowlQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_63", 1001);
		SetName(L("Lemur Howl"));
		SetType(QuestType.Sub);
		SetDescription(L("Grelle's ward-crew can't work through the Lemur howl. Thin the pack so a full shift can run."));
		SetLocation("f_flash_63");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[District Warden] Grelle"), "f_flash_63");

		AddObjective("killLemurs", L("Kill howl-cursed Lemurs"),
			new KillObjective(22, new[] { MonsterId.Lemur }));

		AddReward(new ExpReward(11900, 8100));
		AddReward(new SilverReward(15000));
		AddReward(new ItemReward(640086, 1));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
	}
}

public class CivicRecordsQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_63", 1003);
		SetName(L("Civic Records"));
		SetType(QuestType.Sub);
		SetDescription(L("Two hundred families' property claims rest on Downtown's civic record-volumes. Clear the Hammer-Goblins on the vault niches and recover four volumes intact."));
		SetLocation("f_flash_63");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Civic Scribe] Agatha"), "f_flash_63");

		AddObjective("killHammerGoblins", L("Kill Hammer-Goblins"),
			new KillObjective(15, new[] { MonsterId.Goblin2_Hammer }));

		AddObjective("recoverRecords", L("Recover civic record-volumes"),
			new CollectItemObjective(650785, 4));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));

		AddDrop(650785, 0.40f, MonsterId.Goblin2_Hammer);
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650785, character.Inventory.CountItem(650785), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650785, character.Inventory.CountItem(650785), InventoryItemRemoveMsg.Destroyed);
	}
}

public class RitualBrandPagesQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_63", 1004);
		SetName(L("Ritual Brand Pages"));
		SetType(QuestType.Sub);
		SetDescription(L("Hedvig has traced a Saltisdaughter ritual to the old bath-house. Kill the Wand-Goblins running it and recover five sigil-chain pages before they fire the chant."));
		SetLocation("f_flash_63");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Curse-Scholar] Hedvig"), "f_flash_63");

		AddObjective("killWandGoblins", L("Kill ritual Wand-Goblins"),
			new KillObjective(12, new[] { MonsterId.Goblin2_Wand3 }));

		AddObjective("gatherPages", L("Recover sigil-chain pages"),
			new CollectItemObjective(650825, 5));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));

		AddDrop(650825, 0.50f, MonsterId.Goblin2_Wand3);
	}

	public override void OnComplete(Character character, Quest quest)
	{
		character.Inventory.Remove(650825, character.Inventory.CountItem(650825), InventoryItemRemoveMsg.Destroyed);
	}

	public override void OnCancel(Character character, Quest quest)
	{
		character.Inventory.Remove(650825, character.Inventory.CountItem(650825), InventoryItemRemoveMsg.Destroyed);
	}
}

public class TheStonefrostedAlphaQuest : QuestScript
{
	protected override void Load()
	{
		SetId("f_flash_63", 1005);
		SetName(L("The Stonefrosted Alpha"));
		SetType(QuestType.Sub);
		SetDescription(L("Nikolai has a contract on a cold-cursed Alpha Lemur whose rime-pelt is the only source of true cold-wards. Thin the pack to draw him out."));
		SetLocation("f_flash_63");
		SetAutoTracked(true);

		SetReceive(QuestReceiveType.Manual);
		SetCancelable(true);
		SetUnlock(QuestUnlockType.AllAtOnce);
		AddQuestGiver(L("[Bounty Hunter] Nikolai"), "f_flash_63");

		AddObjective("killPack", L("Thin the Lemur pack"),
			new KillObjective(10, new[] { MonsterId.Lemur }));

		AddObjective("killAlpha", L("Defeat the Stonefrosted Alpha"),
			new KillObjective(1, new[] { MonsterId.Lemur }));

		AddReward(new ExpReward(23800, 16200));
		AddReward(new SilverReward(17000));
		AddReward(new ItemReward(640086, 2));
		AddReward(new ItemReward(640004, 3));
		AddReward(new ItemReward(640007, 3));
		AddReward(new ItemReward(640013, 1));
	}
}
