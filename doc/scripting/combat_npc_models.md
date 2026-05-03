# Combat NPC Models

`AddCombatNpc(monsterClassId, ...)` uses the monster id as the *visual* model
for a guard. Most NPC entries in `monster_npc.xml` only ship an idle pose, so
walk/attack frames silently fall back to T-pose (or the actor never animates
at all). This document lists every NPC-rank entry whose animation set on disk
contains BOTH an attack file (`*_atk*.xsm`) AND a walk/run file
(`*_wlk*.xsm` / `*_run*.xsm`) under `animation.ipf/{npc,monster}/`.

These are the IDs that are safe to pass to `AddCombatNpc`.

## How this list is generated

```
python claude_tools/scripts/find_combat_npcs.py
```

The script parses `E:\Melia\ClientIPF\ies.ipf\monster_npc.xml`, keeps every
`<Class>` with `CT_MonRank="NPC"`, and verifies the `SET_ANI` folder under
`animation.ipf` actually contains both attack and walk animations. Re-run it
whenever the client extract is refreshed.

## Recommended models (soldier / guard / military)

These are the entries you will reach for 99% of the time when placing combat
NPCs (guards, patrols, royal escorts, paladins, schaffenstar, etc.).

| ID     | ClassName                     | Anim set                   | Description (KR)              |
|--------|-------------------------------|----------------------------|-------------------------------|
| 20060  | orsha_soldier_m               | orsha_soldier_m            | Orsha Soldier (male)          |
| 147390 | npc_paladin_follower1_1       | npc_paladin_follower1      | Paladin Follower A1           |
| 147391 | npc_paladin_follower3_1       | npc_paladin_follower3      | Paladin Follower C1           |
| 147399 | npc_paladin_follower1_2       | npc_paladin_follower1      | Paladin Follower A2           |
| 147400 | npc_paladin_follower1_3       | npc_paladin_follower1      | Paladin Follower A3           |
| 147401 | npc_paladin_follower3_2       | npc_paladin_follower3      | Paladin Follower C2           |
| 147402 | npc_paladin_follower3_3       | npc_paladin_follower3      | Paladin Follower C3           |
| 147403 | npc_paladin_follower2_1       | npc_paladin_follower2      | Paladin Follower B1           |
| 147404 | npc_paladin_follower2_2       | npc_paladin_follower2      | Paladin Follower B2           |
| 147405 | npc_paladin_follower2_3       | npc_paladin_follower2      | Paladin Follower B3           |
| 147406 | npc_paladin_follower2_4       | npc_paladin_follower2      | Paladin Follower B4           |
| 147410 | npc_soldier_female_01         | npc_soldier_female         | Female Guard                  |
| 147415 | npc_soldier_female_02         | npc_soldier_female         | Female Guard variant 2        |
| 147416 | npc_soldier_female_03         | npc_soldier_female         | Female Guard variant 3        |
| 147499 | npc_investigation01           | npc_paladin_follower2      | Investigation Captain         |
| 150220 | npc_kingdom_schaffenmem1      | npc_schaffenstar_member    | Resistance Member 1           |
| 150221 | npc_kingdom_schaffenmem2      | npc_schaffenstar_member    | Resistance Member 2           |
| 150222 | npc_kingdom_schaffenmem3      | npc_schaffenstar_member    | Resistance Member 3           |
| 150223 | npc_kingdom_schaffencap       | npc_schaffenstar_executive | Resistance Captain            |
| 150224 | npc_kingdom_paladin_follower1 | npc_paladin_follower2      | Kingdom Paladin Follower 1    |
| 150225 | npc_kingdom_paladin_follower2 | npc_paladin_follower1      | Kingdom Paladin Follower 2    |
| 150286 | npc_Heoshaa                   | npc_heoshaa                | Escort Captain Heoshaa        |
| 150287 | npc_Ramin                    | npc_ramin                  | General Ramin                 |
| 150289 | npc_RoyalGuard               | npc_royalguard             | Royal Guard                   |
| 150290 | ep14_1_kingdomsodier          | npc_soldier_1              | Kingdom Soldier               |
| 150298 | EP14_1_FCASTLE2_MQ_4_FIELDSOLDIER | npc_soldier_1          | Kingdom Field Soldier         |
| 150299 | EP14_1_FCASTLE2_MQ_5_FIELDSOLDIER | npc_soldier_1          | Kingdom Field Soldier         |
| 150300 | EP14_1_FCASTLE2_MQ_6_FIELDSOLDIER | npc_soldier_1          | Kingdom Field Soldier         |
| 153121 | npc_orsha_soldier_bugle_m     | orsha_soldier_m            | Orsha Bugle Soldier — ⚠️ attacks with a trumpet, do NOT use as a guard |
| 155142 | npc_white_hunter              | npc_w_hunter               | White Hunter                  |
| 156101 | npc_bolletta                  | npc_w_hunter               | Fletcher Bolletta             |

## Class master / sub-master models

Job-master NPC models — full anim sets, useful when you want a higher-tier
or named combatant rather than a generic soldier.

| ID     | ClassName               | Anim set         | Description (KR)            |
|--------|-------------------------|------------------|-----------------------------|
| 147326 | npc_CEN_master          | npc_cen_master   | Centurion Master            |
| 155066 | npc_BAR_sub_master      | npc_bar_master   | Barbarian Sub-Master        |
| 155067 | npc_ROD_sub_master      | npc_rod_master   | Rodelero Sub-Master         |
| 155073 | npc_LIN_sub_master      | npc_lin_master   | Linker Sub-Master           |
| 155081 | npc_WUG_sub_master      | npc_wug_master   | Mugosa Sub-Master           |
| 155082 | npc_SCT_sub_master      | npc_sct_master   | Scout Sub-Master            |
| 155094 | npc_sapper              | npc_sapper       | Revealer Mihail (Sapper)    |
| 155095 | npc_sadhu               | npc_sadhu        | Revealer Yane (Sadhu)       |
| 155096 | npc_highlander_2        | npc_highlander_2 | Revealer Connor (Highlander)|

## Schaffenstar (faction-themed humans)

| ID     | ClassName                    | Anim set                   |
|--------|------------------------------|----------------------------|
| 150023 | uniqraid_startower_npc_ramunas | npc_schaffenstar_executive |
| 150024 | uniqraid_startower_npc_kron    | npc_schaffenstar_executive |
| 150178 | npc_schaffenstar_adaux       | npc_schaffenstar_executive |
| 150179 | npc_schaffenstar_member2_4   | npc_schaffenstar_member    |
| 150180 | npc_schaffenstar_member2_5   | npc_schaffenstar_member    |
| 155163 | npc_schaffenstar_member1_1   | npc_schaffenstar_member    |
| 155164 | npc_schaffenstar_member1_2   | npc_schaffenstar_member    |
| 155165 | npc_schaffenstar_member1_3   | npc_schaffenstar_member    |
| 155166 | npc_schaffenstar_member2_1   | npc_schaffenstar_member    |
| 156114 | npc_schaffenstar_executive1_1| npc_schaffenstar_executive |
| 156115 | npc_schaffenstar_executive1_2| npc_schaffenstar_executive |
| 156116 | npc_schaffenstar_executive1_3| npc_schaffenstar_executive |
| 156117 | npc_schaffenstar_executive2_1| npc_schaffenstar_executive |
| 156118 | npc_schaffenstar_ramunas     | npc_schaffenstar_executive |
| 156119 | npc_schaffenstar_kron        | npc_schaffenstar_executive |
| 156120 | npc_schaffenstar_bayl        | npc_schaffenstar_executive |
| 156122–156128 | npc_schaffenstar_member1_4..10 | npc_schaffenstar_member |
| 156129 | npc_schaffenstar_member2_2   | npc_schaffenstar_member    |
| 156130 | npc_schaffenstar_member2_3   | npc_schaffenstar_member    |
| 156131–156134 | npc_schaffenstar_executive1_4..7 | npc_schaffenstar_executive |
| 156135 | npc_schaffenstar_henika      | npc_schaffenstar_executive |

## Pilgrims / monks / civilians (humanoid, light combat anims)

| ID     | ClassName            | Anim set         |
|--------|----------------------|------------------|
| 154050 | npc_Agatas           | npc_pilgrim_m_1  |
| 155034 | npc_pilgrim_m_1      | npc_pilgrim_m_1  |
| 155035 | npc_pilgrim_m_2      | npc_pilgrim_m_1  |
| 155036 | npc_pilgrim_m_3      | npc_pilgrim_m_1  |
| 155037 | npc_pilgrim_m_4      | npc_pilgrim_m_1  |
| 155038 | npc_pilgrim_m_5      | npc_pilgrim_m_1  |
| 155039 | npc_pilgrim_m_6      | npc_pilgrim_m_1  |
| 160201 | npc_pilgrim_ep16_1_track | npc_pilgrim_m_1 |
| 155042 | npc_friar_01         | npc_tila_monk    |
| 155043 | npc_friar_02         | npc_tila_monk    |
| 155044 | npc_friar_03         | npc_tila_monk    |
| 155045 | npc_friar_04         | npc_tila_monk    |
| 155046 | npc_friar_05         | npc_tila_monk    |
| 156000 | npc_gintas           | npc_tila_monk    |
| 156001 | npc_margiris         | npc_tila_monk    |
| 158001 | npc_friar_02_blacksmith | npc_tila_monk |
| 154010 | npc_vakarine_goddess | npc_vakarine_goddess |
| 154053 | npc_Galius           | npc_galius_follower |
| 154054 | npc_Galius_follower_1| npc_galius_follower |
| 154055 | npc_Galius_follower_2| npc_galius_follower |
| 154056 | npc_Galius_follower_3| npc_galius_follower |

## "Boss-look" NPCs (animated boss models packaged as NPC entries)

These are valid for `AddCombatNpc` because they reuse the boss anim set, but
they look like raid bosses — only use when that's actually what you want.

| ID     | ClassName              | Anim set              | Description (KR)        |
|--------|------------------------|-----------------------|-------------------------|
| 47284  | npc_paulius_2          | boss_insane_marnoks   | Fallen Paulius          |
| 147371 | npc_gesti              | boss_zesty            | Zesty                   |
| 147383 | npc_giltine            | npc_giltine           | Giltine                 |
| 150239 | npc_MasterRangda_Barong2 | barong              | Mielas                  |
| 153210 | npc_Spector_gh_red     | spector_gh_boss       | Red Apparition          |
| 153211 | npc_Sec_Spector_Gh     | spector_gh_boss       | Green Apparition        |
| 153212 | npc_Hallowventor       | hallowventor          | Hallowventor            |
| 154003 | npc_Blud               | blud                  | Demon Lord Blud         |
| 156164 | npc_Tantaliser         | boss_tantaliser       | Bound Tantaliser        |
| 160227 | npc_neringa_of_evil    | boss_darkneringa      | Dark Neringa            |
| 157007 | npc_ebonypawn          | npc_paladin_follower2 | Ebonypawn               |

## Avatars / event / decorative (mostly NOT useful for guards)

Listed for completeness — these reuse animated mob bodies as NPC entries
(`kupole`, `piggy`, transformation models, etc.). They animate, but don't
read as soldiers/guards.

| ID range / family | Anim set | Notes |
|---|---|---|
| 151171–151176, 154011–154016, 154113, 154125, 153144 | kupole | Kupole helpers (pixie) |
| 151178 | piggy | Gold pig |
| 152021 | confinedion | Sleeping Scorpio |
| 153209 | velwriggler | Blue Velwriggler |
| 154093 | tower_of_firepuppet | Fire puppet |
| 147462 | mushcaria | Mushcaria |
| 156009 | popolion | Popolion (transform) |
| 156010 | ferret_folk | Ferret (transform) |
| 156011 | tiny | Tiny (transform) |
| 156012 | npanto_baby | Npanto (transform) |
| 156013 | honeybean | Honeybean (transform) |
| 156014 | onion | Onion / Kepa (transform) |
| 156015 | jukopus | Jukopus (transform) |
| 150252 | orsha_soldier_m | TOS Hero Avatar Summon 1 |
| 150253 | npc_paladin_follower2 | TOS Hero Avatar Summon 2 |
| 150254 | npc_schaffenstar_member | TOS Hero Avatar Summon 3 |
| 160023, 160088, 160111, 160137, 160152, 160155, 160157, 160178, 160179, 160180, 160202, 160206, 160234, 160235, 161001 | kupole | Event / raid-strat / shop NPCs |
| 160187 | lapindion | Moon Rabbit |

## Verified-bad list (do NOT use)

Even where `monster_npc.xml` looks plausible, these have been observed to
display only an idle pose in-game:

- `soldier_axe` (pure idle)
- `orsha_soldier_f` (anim folder is empty in client)
- Most `monster_*` soldier classes — they're enemy mobs, not guards.

## Adding a new model to the verified list

1. Run `python claude_tools/scripts/find_combat_npcs.py` and confirm the id
   appears in the output.
2. Spawn it on a test map with `AddCombatNpc(id, "Test", map, x, z, 0, level: 1)`
   and aggro a low-level monster onto it. Confirm walk and attack frames
   actually play.
3. Add it to the table above with a short description.
