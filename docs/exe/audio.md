# exe/audio — positional audio

The original really had 3D sound. It delay-loads **QMixer.dll** (QSound's mixer, shipped in the install, dated Jan 2000) and resolves 27 named entry points, including `SetSourcePosition`, `SetListenerPosition`, `SetListenerOrientation`, `SetListenerVelocity`, `SetSourceCone`, `SetDistanceMapping`, `SetSpeedOfSound`, `SetSpeakerPlacement`, `SetPanRate`, `SetFrequency` and `GetSourcePosition`. Every sound in the game goes through one entry point, `Sound_PlayEffect(handle, category, effect, x, y, z)`, and a 2D sound is played at `(0,0,0)` — **but that does not mean a 2D sound is merely a 3D source sitting at the origin, and reading it that way is a trap.** The engine has a separate, genuinely listener-*independent* path (`QSWaveMixSetPosition`) chosen per voice by a flag whose writer was never found; see "The listener, and what a pause does to it". Positions come from `.MD2` nodes that the id table marks as emitters with the flag word `0x211`. The lobby used none of it: everything there played at `(0,0,0)`.

## QMixer

The import-by-name table is at `00725990`–`00725c52` (u16 hint + name, terminated by the string `QMIXER.dll`).

The delay-load IAT *can* be found, but not by searching for a string or a VA: the tables hold **RVAs of the `IMAGE_IMPORT_BY_NAME` records**, so search for the dword `nameHintAddress - 0x400000`. `SetDistanceMapping`'s record is at `00725bc0`, so a byte search for `c0 5b 32 00` finds it in **two** places — the delay-load signature, because the INT and the delay IAT start out identical. The pair sits at `006fd294`–`006fd2fc` and `007243ac`–`00724414`, 27 entries each, zero-terminated at both ends. `006fd2xx` is the live IAT: it has READ xrefs, the other has none. Ghidra leaves that whole region undefined as functions, which is why xrefs look empty until the records are defined.

**Deliberately not resolved:** every `QSWaveMixSetReverb*`, plus `SetRoomSize`, `SetPolarPosition`, `SetSourceVelocity`, `SetListenerRolloff`, `SetSourceCone2`. Reverb came from **EAX** instead — `data\sound.sam` carries `SoundInfo.QSOUND 15` and `SoundInfo.EAX 10`, and `SETREVERB` is a ride-script opcode, with `SndReverb.map` as its settings file.

| Address / value | Original name | What it is | Evidence |
|---|---|---|---|
| `00725990`–`00725c52` | — | QMixer import-by-name table (u16 hint + name), terminated by `QMIXER.dll` | Read from the exe |
| `00725bc0` | `SetDistanceMapping` | That import's `IMAGE_IMPORT_BY_NAME` record; its RVA is the search key | Byte search `c0 5b 32 00` hits exactly the INT and the delay IAT |
| `006fd294`–`006fd2fc` | — | The **live** delay IAT, 27 slots, zero-terminated | Only this copy has READ xrefs |
| `007243ac`–`00724414` | — | The INT, identical twin of the delay IAT at load time | Delay-load layout |
| `006fd2a4` | — | `SetDistanceMapping`'s IAT slot; called exactly once | `get_xrefs_to` on the slot |
| `006d2963` | — | The one call site of that slot | xref from `006fd2a4` |
| `006d2930` | — | Thin wrapper around it: builds a 16-byte struct `{cbSize=0x10, a, b, c}` and copies the three values straight from its caller, so the real parameters live one level further up | Decompiled |
| `00711980` | — | The only place the wrapper's address appears — a vtable, so the caller is virtual-dispatched | Address search |
| `007655d8` | — | Ride-script keyword table; holds `SETREVERB` alongside `SPARK`, `SCREAMLEVEL`, `SETLIGHT`, `WALKON` | Read from the exe |

`SndReverb.map` is 820 bytes with first dword `2000`. **Record shape NOT decoded** — 48 and 68 both divide the remainder, 52 is wrong.

## The single entry point

    Sound_PlayEffect( handle, category, effectId, x, y, z )

The three ints become floats in a stack record `{vtable 00700b90, category, effect, x, y, z}`, virtual-called on the sound manager at `DAT_00802bcc` through vtable+8.

**A 2D sound is just one played at (0,0,0).** `UI_PlaySound` is literally `Sound_PlayEffect(0, uiCat, id, 0, 0, 0)`. Copy that shape — one call, optional position — rather than adding a parallel positional path.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0051bfc0` | `Sound_PlayEffect` | The single entry point for every sound, positional or not | Decompiled; all callers below reach audio through it |
| `00700b90` | — | vtable stored in the stack record it builds | Decompiled |
| `DAT_00802bcc` | — | The sound manager the record is dispatched on, through vtable+8 | Decompiled |
| `00485aa0` | `UI_PlaySound` | 2D wrapper: `Sound_PlayEffect(0, uiCat, id, 0, 0, 0)` | Decompiled |

## Where positional audio actually lived

In parks, in the ride-script engine. `FUN_005573d0` (its own error strings say `RSSE:`) resolves a location via `FUN_00556b90`, then calls `Sound_PlayEffect` with real x, y, z for object types 3–10, and feeds the *same* resolved location to `Particles_Spawn` for types 1–2.

`FUN_00556b90` resolves the location two ways:

- **Given a node id**: `FUN_0044b220` finds the node; position is the node matrix's translation row (`+0x30` / `+0x34` / `+0x38`). Optionally direction is `+0x20` / `+0x24` / `+0x28`, normalised and sign-flipped on a flag bit — that direction is the source cone.
- **Otherwise**: the thing's own world position.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_005573d0` | — | Ride-script sound/particle spawn; object types 3–10 → `Sound_PlayEffect`, types 1–2 → `Particles_Spawn`, same resolved location | Its error strings are prefixed `RSSE:` |
| `FUN_00556b90` | — | Resolves a script location: node id → node matrix, else the thing's world position | Decompiled |
| `FUN_0044b220` | — | Node lookup by id **and** capability flag (see below) | Decompiled |
| `+0x30` / `+0x34` / `+0x38` | — | Translation row of the node matrix = emitter position | Decompiled |
| `+0x20` / `+0x24` / `+0x28` | — | Direction row, normalised and sign-flipped on a flag bit = source cone | Decompiled |

## Node lookup is by id AND a capability flag

`FUN_0044b220` walks the `.MD2` id table for a record whose **id matches** *and* whose **flag word shares a bit** with a given mask. Not by id alone. The masks the exe passes are `0x200` sound, `0x100` particles, `0x400` costume.

The flag words in the shipped model data agree with those masks bit-for-bit — the masks were derived from the exe and the flag words from the models independently:

| Value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x200` | — | Mask passed for **sound** | Constant at the `FUN_0044b220` call in `FUN_00556b90` |
| `0x100` | — | Mask passed for **particles** | Same call site |
| `0x400` | — | Mask passed for **costume** | Same call site |
| `0x211` | — | id-table flag word marking a **sound node** | 65 uses in shipped models, 20 of them on explicitly sound-named nodes |
| `0x111` | — | id-table flag word marking a **particle emitter** | 136 uses, all on emitter-named nodes |
| `0x1031` | — | id-table flag word of the `1stperson` camera node | Shipped models |

OpenTPW already parses this table (`ModelFile.ReadNodeIds`, `Node.Id`, `Node.IdFlags`). An earlier note that "its flag words vary widely" is explained by the above: they are capability bits.

### Trap: two different meanings for `0x200`

`Node.Flags` (record `+0x00`) uses `0x200` for **"transform-only node"**. The mask tested by `FUN_0044b220` is `Node.IdFlags`, a **different word that reuses the value** and means **"sound node"**. Conflating the two sends you the wrong way.

Related node-record facts: the node **name** is at `+0x54`, the same word the mesh path always read — the 88-byte transform-only records carry it too, it was simply never read for them. `+0x50` is **NOT** a second pointer; its low u16 is the record's own index, so do not try to read it as one.

## Emitters in the shipped data

- In-park `levels/space/features/gates.wad` → `gates.MD2`: mesh 1 `Antennae01` (rotated by `gatesm3`), mesh 2 `hatch`, node 7 `ant_emitter` idFlags `0x111` id 3, node 9 **`sound node` idFlags `0x211` id 1**.
- The pattern recurs: `Sound emitter01` on fantasy's speaker wads, `sound node` on every theme's `features/gates.wad`, `sound` on ride carts, `sound nodetemp` on every `Bus.MD2`.

**Nothing in any data file binds a category+effect to an emitter node.** The node says "a sound goes here"; *which* sound was compiled into the exe. That last hop must be chosen, not recovered.

## The lobby was entirely non-positional

`Lobby_Start` plays global effects 2 and 3 at `(0,0,0)`; `FUN_005e1640` plays each park's music and bed (effects 1 and 2) at `(0,0,0)`. **So positional lobby audio in OpenTPW is a deliberate deviation — an improvement on the original, not a restoration. Always say so when lobby audio is reported on.**

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `005dcfe0` | `Lobby_Start` | Plays global effects 2 and 3, both at `(0,0,0)` | Decompiled |
| `FUN_005e1640` | — | Plays each park's music and bed (effects 1 and 2), both at `(0,0,0)` | Decompiled |

The lobby island `Spa_isle.MD2` has nodes 26 `ant_emitternew` and 27 `ant_emitter`, parented to mesh 9 `Antennae01` (the only mesh either space lobby model rotates) — but **no id record**, so in the lobby they are named positions only. It is the sole model in `lobby.wad` with an emitter node, verified across all eight lobby models: the other three islands carry only `Dummy0..09` (idFlags `0x111`) plus `Spline path`, jungle adds `l_tree1` / `l_tree2`, and all four lobby gates carry nothing but a root `Dummy01`. That is why OpenTPW looks the name up rather than naming Space in code.

## The listener, and what a pause does to it

**`FUN_0051c1d0` is the per-frame listener update.** This page used to record that as not found and the search as stopped; it was found on 2026-09-21 from the other end, by following what a pause writes.

It takes **nine dwords — three vectors**. The first is the listener **position**; the other two are orientation (forward and top). It hands them to `FUN_006b5ba0` → `FUN_006b8b60`, which compares the incoming position and both orientation vectors against the stored copies at `obj+0x28` and `obj+4`/`obj+0x10` and acts **only when something changed** — so the pause's write sticks without needing to be repeated.

| Address | What it is | Evidence |
|---|---|---|
| `FUN_0051c1d0` | The per-frame listener update: `(position, forward, top)` | Three call sites, all in `Game_StateMachine` |
| `0x0054e70e` | The **lobby's** call — nine literals: `(0,0,-50)`, `(0,0,1)`, `(0,1,0)` | Disassembled; `0xc2480000` = −50.0f |
| `0x0054f958` | A **park's**, camera-mode mask `0x16` set: the camera globals | Disassembled |
| `0x0054f9e4` | A **park's**, mask clear: the **midpoint** camera↔`0x00790ab8`, lerped by `0.5` at `0x00700f88` | Disassembled |
| `FUN_006b8b60` | Stores it, and only acts on a change | Decompiled |

### The pause substitution

    if ( DAT_00803ad2 != 0 )  position.second = _DAT_00700b28;

`0x00700b28` reads `00 40 1c 46` = `0x461C4000` = **10000.0f**. The original is **Y-up** — the lobby call site's own top vector is `(0,1,0)` — so the coordinate replaced is **height**: a pause lifts the listener **10,000 units straight up**, about eight times the whole map, since a park spans 0..1280 and coordinates reach QMixer unscaled.

`DAT_00803ad2` has **exactly one reader** (`0x0051c1e9`, inside `FUN_0051c1d0`) and one writer, the one-line setter `FUN_0051c1c0`:

| Call site | Passes | In |
|---|---|---|
| `0x004092e8` | 1 | `Game_Pause` |
| `0x00409339` | 0 | `Game_Resume` |
| `0x00409391` | 0 | `Game_TogglePause`, resume branch |
| `0x004093bf` | 1 | `Game_TogglePause`, pause branch |
| `0x00415254` | 0 | `FUN_00415140`, the **savegame loader's post-load rebuild** (`FUN_00414d40`, "Loaded savegame: %s") |

That last one is why the flag is better read as **"the world is not being heard right now"** than strictly as "paused": a load started from the paused menu clears it as it rebuilds the world.

### What this does NOT establish, and it is the important part

**How much a sound is turned down by that lift is undetermined.** The lift decides **which** sounds a pause takes, not **how much** — and only the *which* is recoverable from this executable: a listener cannot reach a sound that was never placed against it. Any sentence of the form "every placed sound attenuates to nothing" is **unsupported**; see the Unknowns below for exactly where the missing model lives.

There is also a genuine **2D path**, so "a 2D sound is just one played at (0,0,0)" is true of `Sound_PlayEffect`'s shape but misleading about the engine: wrapper slot `+0x74` (`0x006d2640`) takes a flag — non-zero calls `QSWaveMixSetSourcePosition` (listener-relative), zero calls `QSWaveMixSetPosition` (**listener-independent**). The choice is made per voice at one place, `FUN_006b7fa0`, from **bit `0x20` of `params+0xc`**, and **no writer of that bit was found**. So whether the original's music and UI were registered 2D — immune to the lift — or 3D at the origin is **not established in either direction**.

OpenTPW reproduces the *which* and chooses the *how much*: see `Audio.HoldPlaced`.

### The QMixer wrapper vtable

At **`0x00711920`** — not `0x00711980`, which appears nowhere in the image. The vptr is installed by `MOV dword ptr [ESI],0x711920` at `0x006d1e64` in `FUN_006d1e30`, whose only caller is `FUN_006c4190` at `0x006c423b`. Each slot's target ends in `CALL dword ptr [0x006fd2xx]`, an entry of the delay-load IAT above, which is how the mapping was recovered.

| Slot | Import |
|---|---|
| `+0x18` | `InitEx` / `Activate` |
| `+0x24` | `OpenWaveEx` / `PlayEx` / `ConfigureChannel` / `FreeWave` |
| `+0x28` | `StopChannel` |
| `+0x2c` | `PauseChannel` |
| `+0x30` | `Restart` / `Stop` |
| `+0x60` | **`SetDistanceMapping`** |
| `+0x64` | `SetSpeakerPlacement` |
| `+0x68` | `SetPanRate` |
| `+0x6c` | `SetSpeedOfSound` |
| `+0x74` | **`SetSourcePosition` / `SetPosition`** (3D or 2D, per the flag above) |
| `+0x78` | `GetSourcePosition` |
| `+0x7c` | `SetSourceCone` |
| `+0x80` | `SetVolume` |
| `+0x88`, `+0x98` | `SetListenerPosition` |
| `+0x94` | `SetFrequency` |

## Unknowns

- ~~Not found, and the search was stopped: where the original sets the listener each frame.~~ **FOUND, 2026-09-21 — it is `FUN_0051c1d0`.** See "The listener, and what a pause does to it" above, which also carries the three call sites and the pause substitution.
- **`FUN_0051c700` is NOT the distance-mapping feed**, which this page guessed it "possibly" was. It is the sound-detail ladder that interpolates the three `RadiusInfo[n].MINRADIUS` values (100.0 / 0.5 / 0 at SWITCH 25 / 50 / 75) from `data\sound.sam` and posts them to `0x006b5890` → `FUN_006b99c0` → `FUN_006b8180`, which latches `{on/off, radius}` and walks the voice array setting a per-voice LEVEL. That level comes from `FUN_006c4c80`, a **segment-versus-circle occlusion test that uses only the X and Z components** and ignores height entirely — so the pause's Y-lift cannot touch it either way. It never reaches `SetDistanceMapping`.
- `SndReverb.map`'s record shape is **not decoded**.
- The actual parameters passed to `SetDistanceMapping` are still **unknown**, but where they live is now known precisely. `QSWaveMixSetDistanceMapping` has **exactly one call site in the image**, `0x006c581b` in `FUN_006c5690`, reached as `if ( params->flags_at_0x14 & 0x200 ) vtbl[0x60]( out, channel, params + 0x38 )` — so the three values are the dwords at `params+0x38..+0x40`. **No writer of that `0x200` request bit exists anywhere in the image** (404 MOV-imm32 and 2 OR-imm32 candidates examined, none carrying it); the record is built by a virtual-dispatched builder that was not reached statically. So **QMixer's own DEFAULT mapping probably governs**, and that is in `QMixer.dll` (307,200 bytes, 27 Jan 2000, exporting `QSWaveMixGetDistanceMapping`), not in `testme.exe`. The Ghidra project holds only `testme.exe` and `TP.ICD`; importing the DLL is the one step that would settle it.
- **`params+0x14` is a request mask, not a flag pair.** `FUN_006c5690` tests at least nine bits of it, each gating one setter: `0x1` the position path (`FUN_006b7fa0`), `0x2` volume, `0x20` frequency, `0x40`, `0x100` source cone, `0x200` distance mapping, `0x400`, `0x20000`, `0x80000`.
- **Consequently, "a pause attenuates every placed sound to nothing" is NOT established** — in either direction. It is asserted by `docs/CLEANUP-PLAN.md` item 6 and was asserted by `GameClock.cs` and `weather.md`; none of them had evidence, and the curve is undetermined. OpenTPW holds placed voices to **silence** instead, which is a choice standing in for a curve nobody has measured — said at the site, in `Audio.HoldPlaced`.
