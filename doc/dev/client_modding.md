# Client-side modding from the server

Last verified: 2026-08-25

How far a server can change the player's experience without the player
installing anything. Everything here was verified against client version
390044 with an unmodified client installation.

The short answer: **behaviour, numbers, and text are yours; media files and
the structure of the skill tree are not.**

## Authoritative source files

- `src/ZoneServer/ZoneServer.cs` — `LoadIesMods()`, the IES modification list
- `src/Shared/IES/IesModList.cs` — IES mod data structures
- `src/ZoneServer/Network/Send.cs` — `ZC_IES_MODIFY_LIST`
- `src/ZoneServer/Network/Send.Normal.cs` — `PlayTextEffect`, `PlayEffect`
- `src/ZoneServer/Scripting/ClientScript.cs` — Lua delivery, `ScriptMaxLength`
- `system/scripts/zone/core/client/api/001_override.lua` — `Melia.Override`
- Golden example (frame manipulation): `system/scripts/zone/core/client/original_gems/`

## The four channels

| Channel | Reaches | Needs |
|---|---|---|
| Object properties | tooltip numbers, skill values | nothing, automatic |
| Skill/buff handlers | all behaviour, damage, effects | C# handler |
| IES mods (`ZC_IES_MODIFY_LIST`) | client database fields | the IES row id |
| Lua client scripts | UI, text, icons, any client function | the function name |

### 1. Object properties — numbers, for free

Values from the skill database are calculated server-side and sent as object
properties; the client only renders them. Changing a value in
`packages/laima/db/skills_overrides.txt` changes the client's tooltip with no
further work.

Proof: Chant (41904) has `basicSp: 210` in `system/db/skills.txt` and
`basicSp: 105` in the laima override. The client shows 105.

Tunable per skill: `basicSp`, `basicCast`, `factor`, `factorByLevel`,
`atkAdd`, `atkAddByLevel`, `cooldownTime`, `overheatCount`, `overheatDelay`,
`overheatGroup`, `splashType/Range/Height/Angle/Rate`, `maxRange`,
`hitCount`, `multiHitCount`, `enableCastMove`, `castInterruptible`.

Properties actually shipped to the client live in
`src/ZoneServer/Skills/SkillProperties.cs` (`SpendSP`, `SplRange`,
`SklHitCount`, `SkillSR`, and others).

**Trap:** tooltip numbers come from the **database**, not from the handler.
A `SkillModifier` set in code (see below) changes combat but never appears in
the tooltip. Holy Smash shows "Skill Factor: 158% x 2" — that is
`factor 59 + factorByLevel 99` from the db, not what the handler computed.
Keep both in sync or the tooltip lies.

### 2. Handlers — behaviour is entirely yours

The handler decides what a skill does. The client plays the animation it has
and renders whatever the server reports, so an attack skill may heal, a buff
skill may damage, and nothing has to match the skill's name or original
design.

`SkillModifier` (`src/ZoneServer/Skills/Combat/SkillModifier.cs`) exposes
`BonusPAtk`, `BonusMAtk`, `BonusDamage`, `DamageMultiplier`,
`FinalDamageMultiplier`, `SkillFactorBonus`, `DefenseBonus`,
`DefensePenetrationRate`, `HitRateMultiplier`, `BlockPenetrationMultiplier`,
`CritRateMultiplier`, `CritDamageMultiplier`, `MinCritChance`,
`MaxCritChance`, `CritChanceMultiplier`, `BonusCritChance`,
`BonusDodgeChance`.

```csharp
var modifier = SkillModifier.Default;
modifier.MinCritChance = 100;                 // always critical
modifier.SkillFactorBonus = 15f * skill.Level;
modifier.DefensePenetrationRate = 0.5f;

var result = SCR_SkillHit(caster, target, skill, modifier);
target.TakeDamage(result.Damage, caster);
```

Details and interface choice: `doc/dev/skill_handlers.md`.

### 3. IES mods — patching the client database

`IesModList.Add(namespace, classId, propertyName, value)` overwrites a
property of a row the client already has. Sent on login via
`ZC_IES_MODIFY_LIST`. Values may be int or string.

```csharp
this.IesMods.Add("Job", 1005, "Rank", 2);            // make a job selectable
this.IesMods.Add("SkillTree", 10502, "MaxLevel", 5); // make skills learnable
this.IesMods.Add("SharedConst", 104, "Value", 15);   // max base job level
this.IesMods.Add("Item", 648001, "MarketCategory", "Misc_Usual");
```

The Centurion block in `LoadIesMods()` is the reference case: a job the client
disabled in 2015, re-enabled purely from the server.

**Two limits.** IES mods can only *change existing rows*, never add one —
there is no insert, so no new skill slots in a class. And you need the IES row
id (`10502` above), which is **not** in Melia's data; `skilltree.txt` has no id
column. Those ids have to be read out of the client IPF archives.

`LoadIesMods()` is currently hardcoded in `ZoneServer.cs`, so package-local
IES mods are not possible without touching that upstream file.

### 4. Lua client scripts — the UI

The client is Lua-scripted and every global function can be hijacked:

```lua
Melia.Override("FUNCTION_NAME", function(original, ...)
    local result = original(...)
    -- inspect or modify
    return result
end)
```

Place scripts in `packages/laima/scripts/zone/core/client/<name>/`:

- `main.cs` — a `ClientScript` with `LoadAllScripts()` in `Load()` and
  `SendAllScripts(character)` in `Ready()`
- `001.lua`, `002.lua`, and so on — loaded in file name order

No entry in `scripts.txt` is needed; `core/**/*` is a wildcard.

**Hard limit:** `ClientScript.ScriptMaxLength = 2048` characters **per file**.
Split across numbered files; they share one global namespace.

**The client runs Lua 5.1.** `table.unpack` does not exist, only the global
`unpack`.

## Asking the client about itself

The client will tell you its own API. Since `Melia.Override` works on `_G`, a
script can enumerate it and report through `ui.SysMsg(...)`, which accepts
free strings and appears in the player's system chat.

```lua
for name, value in pairs(_G) do
    if type(value) == "function" and string.find(string.upper(name), "TOOLTIP") then
        ui.SysMsg(name)
    end
end
```

To learn a function's arguments, dump their types once per session. Frames
answer `GetName()`; wrap it in `pcall` because other userdata does not:

```lua
local n = select("#", ...)
for i = 1, n do
    local v = select(i, ...)
    if type(v) == "userdata" then
        local ok, nm = pcall(function() return v:GetName() end)
        ui.SysMsg(i .. ") userdata " .. (ok and tostring(nm) or "?"))
    else
        ui.SysMsg(i .. ") " .. type(v) .. " " .. tostring(v))
    end
end
```

This is how the tooltip signatures below were found — no IPF extraction
needed. Note that the client IPF files are locked while the client runs.

## Skill tooltips

Client version 390044 offers 30 functions with "TOOLTIP" in the name. The
relevant ones for skills:

| Function | argc | Notes |
|---|---|---|
| `SET_SKILL_TOOLTIP_CAPTION` | 3 | args **2 and 3** are the description text |
| `SKILL_LV_DESC_TOOLTIP` | 7 | arg **5** is the per-level effect text |
| `SET_SKILL_TOOLTIP_ICON_AND_NAME` | 3 | arg **1** is the frame, `GetName()` is `skill_desc` |
| `UPDATE_SKILL_TOOLTIP` | — | main entry point, returns nil |
| `GET_SKILL_TOOLTIP_VALUES` | — | value block |
| `SET_SKILL_TOOLTIP_BY_TYPE`, `..._BY_TYPE_LEVEL` | — | not yet examined |

Also present: `ABILITY_DESC_TOOLTIP`, `UPDATE_ABILITY_TOOLTIP`,
`SCR_ABIL_ADD_SKILLFACTOR_TOOLTIP`, `SCR_REINFORCEABILITY_TOOLTIP`,
`DRAW_EXPAND_SKILL_TOOLTIP`, `SET_TOOLTIP_SKILLSCROLL`,
`UPDATE_SUMMON_SKILL_TOOLTIP`, `SKILLGEM_CONVERT_SCROLL_CHANGE_TOOLTIP`.

**These functions do not return the text — they receive it.** `CAPTION` and
`UPDATE_SKILL_TOOLTIP` return nil, `SKILL_LV_DESC_TOOLTIP` returns a number
(a layout y position). So patch the *arguments*, then call the original:

```lua
local UNPACK = table.unpack or unpack   -- Lua 5.1

Melia.Override("SKILL_LV_DESC_TOOLTIP", function(original, ...)
    local args = {...}
    local n = select("#", ...)          -- preserve trailing nils

    if type(args[5]) == "string" and args[5] ~= "" then
        args[5] = args[5] .. "{nl}{#33DD55}Rewritten by the server{/}"
    end

    return original(UNPACK(args, 1, n))
end)
```

Always type-check before patching and pass everything else through untouched.

Preserve the argument count with `select("#", ...)` rather than `#args`:
trailing nils are meaningful to the client and `#` on a table with holes is
undefined in Lua.

### Identifying which skill is being drawn

The tooltip functions do **not** hand you a usable skill reference — argument
2 of `SET_SKILL_TOOLTIP_ICON_AND_NAME` answers neither `GetClassName()` nor
`GetClassID()`. What they do carry is the skill's description text, so match
on a distinctive phrase from it:

```lua
if string.find(args[2], "Sets enemies on fire", 1, true) then
    -- this tooltip belongs to Immolation
end
```

Use `string.find(s, pattern, 1, true)` — the `true` disables pattern matching,
so text containing `%`, `-` or `(` does not blow up the match.

The client draws icon and name first, then the caption, then the per-level
block. So `SET_SKILL_TOOLTIP_CAPTION` can identify the skill and record it in
a global, which `SKILL_LV_DESC_TOOLTIP` then reads to replace the effect text
of the same skill.

### What crashes the client

**Touching UI frames can kill the client outright**, and `pcall` does not save
you: it catches Lua errors, but a fault in the native UI layer takes the whole
process down ("The client has been shutdown due to an error").

Confirmed to crash, on client 390044:

- `icon:SetImage("gem_mon_weaver")` — a valid image name, but from the item
  context; skill icons appear to live in a different atlas.
- Probing native objects with dynamic keys (`obj[key]` / `obj[key](obj)`) to
  discover which getters exist.
- `GET_CHILD(...)` plus `SetText`/`GetText` on the resulting child.

Confirmed safe, used repeatedly without incident:

- Reading and rewriting the **string arguments** passed into the tooltip
  functions, then calling the original.
- `frame:GetChildCount()` / `frame:GetChildByIndex(i)` with `GetName()` for a
  one-off structure dump.

So: **patch arguments, do not manipulate frames.** The practical consequence
is that the description and the per-level block are freely rewritable, while
the tooltip *headline* and the *icon* are not reachable this way — they are
only set through the frame. Putting the new name as the first line of the
description is a workable substitute:

```lua
desc = "{#FFCC33}Pain Suppression{/}{nl}Brace against your own fire..."
```

For the headline and icon, IES mods are the crash-free alternative to
investigate: they are data, not code. Whether the `Skill` namespace exposes
`Name` and `Icon` columns to `ZC_IES_MODIFY_LIST` is untested.

If a script does crash the client, recovery is cheap: delete the script folder
and restart the ZoneServer. Client scripts are pushed fresh on every login and
are never cached on disk.

### Text markup

The client rich text control understands:

- `{nl}` — line break
- `{#RRGGBB}` ... `{/}` — colour
- `{img NAME w h}` — inline image
- `{s16}` ... `{/}` — font size

The client does *not* apply style formatting to floating text effects (see the
remark in `PlayTextEffect`).

### Icons

Icons can be repointed to any image the client already ships, via the frame:

```lua
local picture = GET_CHILD(frame, "itempic", "ui::CPicture")
picture:SetImage(otherImageName)
```

See `original_gems/001.lua`, which overrides `GET_ITEM_ICON_IMAGE`,
`INV_ICON_SETINFO` and the gem tooltip this way. New image files cannot be
added — only existing ones reused.

## Floating text and effects

```csharp
Send.ZC_NORMAL.PlayTextEffect(actor, caster, "SHOW_CUSTOM_TEXT", 0, "any text");
Send.ZC_NORMAL.PlayEffect(actor, "F_cleric_heal_active_ground_new");
```

`SHOW_CUSTOM_TEXT` is a Melia invention: it is rewritten to `SHOW_BUFF_TEXT`
with a `CUSTOM:` prefix, which a Lua override
(`core/client/custom_text_effect/`) intercepts and prints verbatim. Effect
names may be borrowed from any other skill.

## What cannot be done from the server

- **Add rows to client data** — no new skill in a class tree, no new item
  type. IES mods change, never insert.
- **New media** — images, animations, sounds, models. Only what the client
  already ships can be reused or repointed.
- **Reorder or extend the visible skill tree structure** without the IES row
  ids from the client archives.

Everything else — values, behaviour, buffs, pads, text, colours, icon
assignment, unlock levels, max levels — is reachable from the server.

## Traps worth knowing

**`activationType` in the skill db can be wrong.** Chant (41904) is declared
`ActiveSkill` but the client treats it as a passive ("Constantly applied after
learning the skill"). When server data and the client tooltip disagree, the
tooltip is the more reliable source. Implement such skills as
`IPassiveSkillHandler`; those run for every learned skill on
`CZ_LOAD_COMPLETE` (`PacketHandler.cs`), so on every map load.

**`useType` can be wrong too, and it decides the handler interface.**
Zealot's Invulnerable (41701) is listed as `MeleeGround`, but the client sends
`CZ_SKILL_SELF` for it, so it needs `ISelfSkillHandler` — a different `Handle`
signature, taking a `Direction` instead of a target position. Do not derive
the interface from the database alone. The server tells you the right one as
soon as the skill is used:

```
TryGetHandler: The skill handler for 'X' is not of type 'ISelfSkillHandler'.
CZ_SKILL_SELF: No handler for skill 'X' found.
```

Same for `CZ_SKILL_GROUND` (`IGroundSkillHandler`) and `CZ_SKILL_TARGET`
("no *force* skill handler" → `IForceSkillHandler`). Writing a handler against
the db value and reading the first warning is faster than guessing.

**A class may gate itself behind a passive.** All Crusader skills stayed
greyed out in the quickbar — not because of weapons, rank, or job count, but
because Chant grants `[Goddess' Retribution]`
(`BuffId.GoddessPunishment_Buff = 2260`) and, per its own description,
"Crusader skill activated". Without that buff the client refuses the whole
class and sends no packet at all. **When every skill of a class is dead, look
for a passive that unlocks it before debugging anything else.**

**Silence is a diagnosis.** If clicking a skill produces no log line at all,
the client blocked it and the cause is client-side. When a packet does arrive,
`PacketHandler` logs `No handler for skill 'X' found`, which also names the
required interface ("no *force* skill handler" means `IForceSkillHandler`).

**Data files replace, they do not merge, when the db is not indexed.**
`SkillTreeDb` is a plain `DatabaseJson`, so `packages/laima/db/skilltree.txt`
**replaces** `system/db/skilltree.txt` entirely (719 entries instead of 864).
Indexed databases such as `ServerDb` merge by key, but replace the whole entry
for a matching key. See `LoadDb` in `src/Shared/Server.cs`.

**Restart after data changes.** `db/` and `conf/` are read at startup only.
The load line confirms overrides: `done loading N entries from x.txt (with
overrides from ...)`.
