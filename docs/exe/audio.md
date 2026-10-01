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
| `00711980` | — | The only place the wrapper's address appears: slot `+0x60` of the QMixer wrapper vtable at `0x00711920` (see below), so the caller is virtual-dispatched | Address search |
| `007655d8` | — | Ride-script keyword table; holds `SETREVERB` alongside `SPARK`, `SCREAMLEVEL`, `SETLIGHT`, `WALKON` | Read from the exe |

`SndReverb.map` is 820 bytes with first dword `2000`. **Record shape NOT decoded** — 48 and 68 both divide the remainder, 52 is wrong.

## The single entry point

    Sound_PlayEffect( handle, category, effectId, x, y, z )

The three ints become floats in a stack record `{vtable 00700b90, category, effect, x, y, z}`, virtual-called through `DAT_00802bcc`'s vtable+8. `DAT_00802bcc` is not the manager: it is an 8-byte forwarder (vptr `0x0070a288`, the manager at `+4`) whose `+8` (`0x006b5b40`) calls the manager's `+0x10`, `0x006b87d0`, which is the real play. See "How the engine plays an effect" below.

**`Sound_PlayEffect` has no 2D form: a caller that wants a flat sound passes (0,0,0).** `UI_PlaySound` is literally `Sound_PlayEffect(0, uiCat, id, 0, 0, 0)`. Whether the engine then plays such a voice listener-independent is not established (see "The listener, and what a pause does to it"). OpenTPW copies the shape: one call, optional position (`Audio.Play`, null for flat).

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0051bfc0` | `Sound_PlayEffect` | The single entry point for every sound, positional or not | Decompiled; all callers below reach audio through it |
| `00700b90` | — | vtable stored in the stack record it builds | Decompiled |
| `DAT_00802bcc` | — | An 8-byte forwarder to the sound manager, written through a pointer (`0x0051b6a6 PUSH 0x802bcc`, `0x006b5de6`), which is why it shows only reads | Disassembly |
| `00485aa0` | `UI_PlaySound` | 2D wrapper: `Sound_PlayEffect(0, uiCat, id, 0, 0, 0)` | Decompiled |

## How the engine plays an effect: priority, not a repeat delay

Decoded 2026-09-23 for `docs/QUEUE.md` Q9. Five decoders each had their claims checked by two refuters (workflow `wf_99a15f42-84d`). `0x006bc2d0`, `0x006c3e00` and `0x006c0676` were then re-read by hand, and the layout was re-measured over all 31 shipped `cat_*SFX.map`. The file layout itself is in the FileFormats docs clone, `formats/sound-categories.md`, on branch `docs/sound-formats` (not yet on master).

**The engine keeps no time per effect.** No part of the path from `Sound_PlayEffect` down to a voice reads a clock or writes one into an effect record or a category:
- the play entry `0x006b87d0` does not;
- neither does the device gate `0x006b88e9` → `0x006b9bd0`, which is `MOV EAX,1; RET 8` on both device vtables;
- neither does anything else before the voice is made.

A second caller of the same category and effect is always let through and gets a new voice with a new handle. **So a stop cannot change what another caller hears, except by freeing capacity** (see the pool below).

**The effect record's fourth int is a priority.** OpenTPW's `SoundCategoryFile` reads it as `RepeatDelay`: 2700 for every kids scream, 10000 for music, 6000 for speech. It has two readers:
- **Voice creation.** It is copied into the voice (`0x006bb9f9 MOV DX,[EAX+0xc]`, `0x006bb9fd MOV [ECX+0x22],DX`) as the high word of the sort key at `voice+0x20`. The low word is closeness to the listener (`0x006bba6c`).
- **The play entry.** When the caller passes in a live handle, the new effect replaces that handle's voice only if its priority is strictly higher (`0x006b88d3`..`0x006b88da`). STARTSCREAM and SINGLESCREAM pass handle 0 (`0x00551165`), so this never applies to them.

Every use of that int as a delay in OpenTPW is OpenTPW's own: `SoundCategory.Play`'s throttle, and every replay it times (the lobby's beds and one-shots, the park's music and weather, and SINGLESCREAM). These are filed as Q43.

**The voice class comes from the record's flags word**, the `u16` at `+0x10` (`0x006b6774`..`0x006b67f2`):

| Flags word | Voice class | Shipped users |
|---|---|---|
| no bit `0x4` (for example 0, `0x200`, `0x8`) | One-shot: plays one sample and dies. Freed at start + length + 250 ms (`0x006bc01b`), or when its channel ends. | 1,067 of the 1,267 records, SINGLESCREAM's 75-90 and 105-109 among them; 30 more carry bit `0x1` and go through a deferred queue first (`0x006b66d0`) |
| `0x0404` | Held chain, final vtable `0x0070a2e0`. The constructor stores `0x0070a390`, then overwrites it at `0x006bdde5`. | 18 records: kids 71-74, staff 188, and the ambient beds with `0x120404`/`0x170404` |
| bits `0x4` and `0x2` (`0x0406`, `0x0606`, ...) | A class that passes the variation's wait to the mixer channel (`0x006bdf00` → `0x006c5220`). Not decoded further. | 135 records, music 2, kids 91 and global ambient 33 among them |

**A held voice is a chain.** The parent of a `0x0404` voice never plays a sample.
- **Its tick** (vtable `+0x14`, `0x006bdae0`) only extends and prunes a chain of one-shot children (`0x006bdb50`, `0x006bdc10`).
- **When a child is made.** Each pass, if the clock has passed the newest child's time (`0x006bdb88 CMP EBP,[ESI+0x1c]; JBE`), a new child is made. It is a 0x50-byte voice of class `0x0070a9e0` (`0x006bdbba` → `0x006b72f0` → `0x006c3ded`).
- **What a child gets.** `0x006c3a80` gives it:
  - a variation (`0x006c3e00`);
  - a fresh weighted sample (`0x006bc680`);
  - its time: now plus a wait drawn from that variation's header (`0x006c3abe`..`0x006c3ac5`).
- **How the wait is drawn.** The wait is the header's `u16` pair at `+0x10`/`+0x12` (`0x006bc2d0`):
  - nought when both are nought;
  - otherwise the pair is swapped if the second is the smaller, then `min + LCG % (max - min)`, which is min when they are equal.
  - Bit 4 of the header's `+0x18` takes the wait from the voice's parameter instead (`0x006c3d80`). No scream variation sets it.
  - It is counted from the child's start, and the sample's length does not come into it.
  - Shipped for the four screams: 71 1000-3000 ms, 72 500-2000, 73 100-1000, 74 0-500.
- **Which variation it plays.** The first child takes the effect's first variation, whatever the parameter (`0x006c3e26`..`0x006c3e33`). Every later child draws, weighted by each target's `+0x1e`, among the current variation's zone records whose `lo..hi` holds the voice's parameter byte (`0x006c3e1c`, `0x006c3e89`..`0x006c3e97`). No match parks the chain until a new parameter arrives (`0x006c3f49`).
- **Where the parameter comes from.** For the screams it is parameter 6. STARTSCREAM sets it to `clamp((operand + speed) / 2, 0, 100)` straight after the play (`0x00551261`..`0x00551265`), and SCREAMLEVEL resets it. The zones send 0-25 to variation 1, 26-50 to 2, 51-75 to 3 and 76-100 to 4, so Lost Kingdom's Belly Bounce, at `(20 + 50) / 2 = 35`, screams variation 1 once and variation 2 ever after.
- **Pruning.** A child is pruned when the clock reaches its time (`0x006bdc23`). The prune deletes it without stopping its channel (`0x006b71f0` → `0x006bb9b0`), so its sample most likely plays out over the next one's start. Whether the device reclaims that channel early is **not traced**.
- **The clock.** It is wall time in milliseconds (`0x005f5fa0`: QueryPerformanceCounter, or timeGetTime), read once per service pass (`0x006b62c1`). It does not pause with the game.
- **The service.** The service runs whenever `Sound_ApplyGroupVolumes` posts message `0x700b6c` (`0x0051bfab`). How often that is has **not been measured**. A child made in one pass starts on the next (`0x006bdb79`).

**A stop takes down one chain.**
- `Sound_Stop` and `Sound_StopFading` find the voice by its exact handle (`0x006b6830`, generation-checked at `0x006b6876`).
- A held parent's stop (`0x006bd9b0`) hard-stops each child still in its chain, down to `QSWaveMixStopChannel` (`0x006d24d7`). It deletes the children, clears the chain and flags itself finished (`0x006bcade`).
- With fading on, StopFading still ends in the same hard stop. The parent has no channel, so its first fade step takes the no-channel exit (`0x006bcc71`), and the next service stops it (`0x006b62fe` → `0x006b6320`).
- Nothing on any stop or free path writes an effect record, a category or a manager gate. STOPSCREAM zeroes the script's `+0xd0` itself (`0x00555f0a`).

**Where two callers of one effect do meet: the pool.**
- Each service keeps only the top N voices by that `(priority << 16) | closeness` key and ticks only those (`0x006b6358`, `0x006b637b`). N is 12 by default (`0x006b83b0`) and is reset from the options, up to 30 (`0x006b6140`).
- The hardware channels are taken by the same key (`0x006bad28`).
- A held chain that falls out of the top N makes no children, and a stop elsewhere can let it back in. Not built here (Q43).

**Two rides screaming the same band, therefore:**
- two parents, two handles, each chain starting at once and going on its own random clock;
- one ride's STOPSCREAM cuts that ride's newest child and changes nothing about the other.

OpenTPW's `ParkScreams` builds exactly this: `ParkAudio.StopScream` releases nothing, and `SoundCategory.PickFrom` picks a child's sample with no gate.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x0070a288` | — | vtable of the 8-byte forwarder at `DAT_00802bcc`: `+8` play → manager `+0x10` | Disassembly (`0x006b5dd6`, `0x006b5b4f`) |
| `0x0070a2a0` | — | The sound manager's vtable: `+8` `0x006b8720` registers a category (→ `0x006bf330`), `+0x10` `0x006b87d0` plays, `+0x18` `0x006b89b0` stops a handle | Read from the image |
| `0x0051b530` | — | The game's side of registering a category: a `cat_*` name to a handle through the manager's vtable `+4` (`DAT_00802bcc`); 0 when the sound system is down (`DAT_00802bc8`/`DAT_00802bd4`) or `FUN_0041e8e0` does not answer 1. Called by `Sound_RegisterGlobalCategories`, `FUN_0051ec50`, `FUN_0051e890` and `FUN_0051e8f0` | Decompiled |
| `0x006bf330` | — | Registers a category: looks the name up and loads `<name>BANK.map` and `<name>SFX.map` into a 0x34-byte category. It does not play | Disassembly (`0x006bf3c2`, `0x006bf520`) |
| `0x006b87d0` | — | Play. It reads no clock | Disassembly |
| `0x006bb9f9` | — | The effect record's `+0xc` copied into the voice's priority | Disassembly |
| `0x006b88d7` | — | The only other read of `+0xc`: replace a live handle only for a higher priority | Disassembly |
| `0x006bdae0` | — | A held voice's tick: extend the chain, prune it | Disassembly |
| `0x006bdb88` | — | Make a child once the clock passes the newest child's time | Disassembly |
| `0x006c3a80` | — | A child's variation, sample and time | Disassembly |
| `0x006bc2d0` | — | A variation's wait: `min + LCG % (max - min)` | Disassembly, re-read by hand |
| `0x006c3e00` | — | A child's variation: the first variation first, then by the zones and the parameter | Disassembly, re-read by hand |
| `0x006bd9b0` | — | A held voice's stop: its own children only | Disassembly |
| `0x005f5fa0` | — | The sound clock: wall-time milliseconds | Disassembly |
| `0x00fb1f20` | — | The one random seed every draw above advances | Disassembly |
| `0x006b7e76` | — | Faults (a read through NULL) when no audio device exists: with Wine's pulse and ALSA drivers both disabled, mmdevapi finds no driver and the game crashes right after its DirectDraw set-up | Proton log, `EXCEPTION_ACCESS_VIOLATION` at this address |

## Where positional audio actually lived

In parks, in the ride-script engine. `FUN_005573d0` (its own error strings say `RSSE:`) resolves a location via `FUN_00556b90`, then calls `Sound_PlayEffect` with real x, y, z for object types 3–10, and feeds the *same* resolved location to `Particles_Spawn` for types 1–2.

`FUN_00556b90` resolves the location two ways:

- **Given a node id**: `FUN_0044b220` finds the lookup record; position is the translation row (`+0x30` / `+0x34` / `+0x38`) of the world matrix the pose walk last stored for that record, (0, 0, 0) for one never stored; a miss returns 0 having written nothing (`ride-operation.md`, "How long a leg lasts, and where its ends are"). Optionally direction is `+0x20` / `+0x24` / `+0x28`, normalised and sign-flipped on a flag bit — that direction is the source cone.
- **Otherwise**: the centre of the thing's cell rectangle ×10 in x and z, at the model node's base height (`0x00556d52`): it takes `FUN_00466b70`'s box and drops its heights. `(0, 0, 0)` with no model or no `.hmp` (`park.md`, "The effect subsystem").

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_005573d0` | — | Ride-script sound/particle spawn; object types 3–10 → `Sound_PlayEffect`, types 1–2 → `Particles_Spawn`, same resolved location | Its error strings are prefixed `RSSE:` |
| `FUN_00556b90` | — | Resolves a script location: node id → the record's stored world matrix, else the centre of the thing's cell rectangle ×10 in x and z at the node's base height, or `(0, 0, 0)` with no model or no `.hmp` | Decompiled |
| `FUN_0044b220` | — | Node lookup by id **and** capability flag (see below) | Decompiled |
| `+0x30` / `+0x34` / `+0x38` | — | Translation row of that stored matrix = emitter position | Decompiled |
| `+0x20` / `+0x24` / `+0x28` | — | Direction row, normalised and sign-flipped on a flag bit = source cone | Decompiled |

## What an EventMap's slots feed

A track ride's or coaster's own sounds do not come from its script's `ADDOBJ` or `EVENT`: the ride engine reads them
from the script's `SPAWNSOUND` child, its `EventMap.rse` (`park.md`, "The script-to-script family", has the two
layouts and the jungle values). `FUN_0055a3e0( script, slot )` answers the child's variable at that index, and
`FUN_0051eeb0( script, flags, category, slot, x, y, z )` plays it as an effect when it is not 0. **Every caller passes
`[0x00803a3c]`, the park's `cat_rides`.**

| Slot | Read by | Ride | What it does |
|---|---|---|---|
| 0 | `FUN_004392a0` (`0x0043932f`) | coaster, per train (list `DAT_00790fe0`) | plays it into the train's `+100`, after stopping the old voices; skipped while `DAT_00785a2c` is set |
| 1 | `FUN_004392a0` (`0x00439363`) | the same | plays it into the train's `+0x68` |
| 5-8 | `FUN_004392a0`, `FUN_004396e0` | the same | stored in the train at `+0x54`..`+0x60` (use not read) |
| 0 | `Bumper_Retarget` `FUN_0054a040` (`0x0054a366`, `0x0054a682`, `0x0054a909`) | bumper, go-kart, water arms | plays it into the car's held voice `+0x20` |
| 1 | `FUN_0054ae50` (`0x0054b01d`) | go-karts only (types -4, -7, -9, -10), as a car is taken off | plays it; every other type fades its voice |
| 3 | `Bumper_PlayCarSound` `FUN_00547170` (`0x00547311`) | bumper, go-kart, water | case 2, the toot (logs "TOOT") |
| 4 | the same (`0x00547425`) | the same | case 4 |
| 0 | `FUN_0055a720` (`0x0055a944`) | tour ride, as a car is added | plays it at the car's position / 300 into `car+0xe0` |
| 10 | `Bumper_StepCar` (×3) and `FUN_0055abf0` (×17) | track rides; the flying cars | a **parameter id**, not an effect: `FUN_0051bc40( voice, slot 10, level )`, levels 60/30/80/15 for the flying cars and computed for the track cars |

So slot 10 is `VAR_PAR0` of the 11-variable layout and slots 5-8 are `VAR_PAR0`-`VAR_PAR3` of the 10-variable one. **No
caller reads slot 2**, so the coasters' `VAR_EVT2` (69, not an effect in jungle's `cat_rides`) is never played. Not read:
what sets the car's case to 2 or 4, the gates on the go-kart and water arms of `Bumper_Retarget`, and what
`DAT_00785a2c` is. `FUN_0055a3e0` answers 0 for a script with no sound script or a slot past its variable count.

### The bumper arm's engine (read for Q202, measured in the original)

- **The start** (`Bumper_Retarget`, `0x0054a250`..`0x0054a3a2`): only when the car's `+0x20` is 0, its flags hold both
  `0x4000` and `0x400000` (active and the lead), its ride's `+0x50` is 2 (running) and it lacks `0x20` (unloading). The
  thing is found by walking the things for one of type 3 whose `+0x28` is the ride's handle; its script's slot 0 is
  played (`FUN_0051eeb0( script, 0, cat_rides, 0, 0, 0, 0 )`) and the answer put at the car by `FUN_0051c270( voice,
  x × f, 0, z × f )`, `f` the float at `0x00700f20` (0.0033036, so 302.7 record units to a world unit, where the boats
  are drawn at 307.2), height 0. Both answers go into `+0x20`.
- **Each step** (`Bumper_StepCar`'s bumper arm, `0x00548499`..`0x00548584`, every type of it, not only the Hot Pot):
  while `+0x20` is not 0, `FUN_0051c270` moves it to the car, and `FUN_0051bc40( voice, slot 10, speed / 3 )` sets the
  parameter slot 10 names (16 for the jungle `bumper`) to `+0x4c / 3`. Each answers the handle, or 0 once the voice
  has gone (`FUN_006b6620`), and the answer goes back into `+0x20`, so a voice that has ended empties it and the next
  retarget starts another. No ride state gates the step.
- **The go's end** (`Bumper_CarTick`'s unloading arm for types -1, -3, -6, -11, -14, `0x0054788e`, by the byte map at
  `0x00547c30`): after the unload, `Sound_StopFading( +0x20 )` with 60, every unloading tick, **without clearing
  `+0x20`**; the handle goes when the voice has. **Taking a car off** (`FUN_0054ae50`, `0x0054b065`) fades it the same
  way. The cars stay when a go ends; only `BUMP 10`, a sale or a capacity cut takes them off.
- **Measured in the original** (Q202, `q202/voicelog.py`, the reference park patched to research the Hot Pot, bought
  and queued; two goes logged every track tick): the lead boat's `+0x20` was non-zero from the go's first tick, the
  same handle for all 557 ticks of it; the ride went back to loading at tick 29575 and `+0x20` read 0 from 29583, 8
  ticks later; the next go's first tick held a new handle. No other boat held one.

The effect is jungle 194, `Engine.mp2` (447 ms by the map), flagged `0x6` (below): a voice with no timer, so its own tick plays
the sample again each time its channel ends and the handle lives for the whole go.

## A voice's two controllers

**Which class an effect's flags word makes** (`0x006b6774`..`0x006b67f2`, the play entry): bit `0x4` clear, a
one-shot; `0x4` with `0x10`, `FUN_006be680`; `0x4` with `0x2`, `FUN_006be610` with `0x400`, `FUN_006be330` with
`0x100`, else `FUN_006be090` (vtable `0x0070a338`, 194's class); `0x4` alone, `FUN_006bdd30` with `0x400` (the held
chain), else `FUN_006bdc60`. **The voice's own flags** (`FUN_006bbe90`, `+0x30`): effect `0x8` gives `6`, effect `0x4`
gives `2`, `0x200` gives `8`, `0x20` gives `0x200`, `+0x11` bit `0x40` gives `0x20000`. **A voice with `2` never
times out** (`FUN_006bcb60`); one without it is freed at start + length + 250 ms. When its channel ends
(`0x006bbe40`) it clears `1` and, unless it holds `0x40` or `0x4800`, is marked finished (`0x10`) and freed by the
next service (`0x006b62d5`). Effect `0x8`'s voice bit `4` asks the mixer for a loop (`0x006be016`, request `0x40`).
Which of these keeps 194's voice alive across its channel's end was **measured, not read**: the handle lived 557
ticks over a 447 ms sample.

**The controllers** (`[voice+0x4c]`, eight bytes, `FUN_006b7150`): four keys then four values. Key 0 is the effect
record's `+0x12`; keys 1 and 2 are the variation header's bytes `+0x16` and `+0x1a` (`FUN_006bbf00`); key 3 is 0.
`FUN_0051bc40( voice, id, level )` goes to the voice's vtable `+0x28` (`0x006bbb80`): every slot whose key is `id`'s
low byte takes `level`'s low byte, and a match past slot 0, unless the voice holds `0x4000`, applies them
(`0x006bbbe0`). Slot 0 is the held chains' zone parameter (above).

**What they drive** (`0x006bc040`, asked with bit 1 for the volume and bit 2 for the pitch): the header's `u16` at
`+0x18` with that bit takes slot 1's value; else `+0x1c` with that bit takes slot 2's; else none. With a value `v`,
the volume is `v × (hi − lo) / 100 + lo` over the bytes `+0x0c`/`+0x0d` (`FUN_006bc090`) and the pitch the same over
the signed bytes `+0x0e`/`+0x0f` (`FUN_006bc170`), the pair swapped first if out of order; with none, a draw from `lo`
up to, not including, `hi` (`0x00fb1f20`'s generator). Bit 4 of the same masks is the held chain's wait (above).
Measured over all 1,595 shipped variations: the masks use only bits 1, 2 and 4.

**The pitch** goes to the mixer as a frequency (`FUN_006d2820`, request `0x20`): the channel's own rate times a
table entry, `0x00782f40 + 4p` above nought and `0x00783540 + 4|p|` below, and those tables hold 2^(k/96) from k = 1,
so a pitch `p` plays at 2^((p + 1) / 96) of the sample's rate above nought and 2^((p − 1) / 96) below, then
`QSWaveMixSetFrequency`. **For 194**: key 16 at `+0x16`, mask `+0x18` = 2, pitch -24 to 36, volume 68 to 68: the speed
/ 3 the step sets moves the pitch from -24 (0.835 of the sample's rate) at rest to +25 (1.207) at a boat's top speed of
247, at volume 68.

**OpenTPW** builds this for the bumper arm (`ParkBumperCars`, `ParkCarSounds`): the lead's engine looped at 68 / 100,
moved and pitched each tick, faded with 60 taken as milliseconds. The volume's further scale by the groups' levels
(`FUN_006bb860`) and QMixer's own volume scale are not read. The coasters', tour ride's and go-karts' slots are not
built.

## Node lookup is by id AND a capability flag

`FUN_0044b220` walks the `.MD2` id table for a record whose **id matches** *and* whose **flag word shares a bit** with a given mask, and answers the first such record. Not by id alone. The masks include `0x200` sound, `0x100` particles and `0x400` costume; walk nodes take `0x800` and heads `0x80`. A mask sharing no bit with `0x3da1f83` is swapped for `0x3da1f82` first (`0x0044b226`..`0x0044b22e`); none of the masks above is.

The flag words in the shipped model data agree with those masks bit-for-bit — the masks were derived from the exe and the flag words from the models independently:

| Value | Original name | What it is | Evidence |
|---|---|---|---|
| `0x200` | — | Mask passed for **sound** | Pushed by `FUN_005573d0` as `FUN_00556b90`'s fifth argument, which it hands to `FUN_0044b220` (`0x00556bc0`) |
| `0x100` | — | Mask passed for **particles** | Pushed the same way by `FUN_005573d0`'s particle arms |
| `0x400` | — | Mask passed for **costume** | Pushed by `FUN_00429ee0` and `FUN_00429f60`, which show and hide a node on an advisor model |
| `0x211` | — | id-table flag word marking a **sound node** | 65 uses in shipped models, 20 of them on explicitly sound-named nodes |
| `0x111` | — | id-table flag word marking a **particle emitter** | 454 uses in shipped models: most on effect-named nodes (`smoke`, `steam`, `particle emitter01`), but 108 on `Dummy` nodes, 64 on `Head` nodes and 12 on the park advisors' `Rotate1`-`3` |
| `0x1031` | — | id-table flag word of the `1stperson` camera node | Shipped models |

OpenTPW parses this table (`ModelFile.ReadNodeIds`, `Node.Id`, `Node.IdFlags`) and looks a node up in it as the engine does (`ModelFile.FindNode`, the walk family's through `RideNodes`). Its flag words are the capability bits above.

### Trap: two different meanings for `0x200`

`Node.Flags` (record `+0x00`) uses `0x200` for **"transform-only node"**. The mask tested by `FUN_0044b220` is `Node.IdFlags`, a **different word that reuses the value** and means **"sound node"**. Conflating the two sends you the wrong way.

Related node-record facts: the node **name** is at `+0x54` in every record, the 88-byte transform-only ones included. `+0x50` is **NOT** a second pointer; its low u16 is the record's own index, so do not try to read it as one.

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

**`FUN_0051c1d0` is the per-frame listener update.** It was found by following what a pause writes.

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

## How a voice is decoded

The MPEG voice class's constructor `0x006c82c0` sets its vtable `0x0070ae30`. Its read, slot `+0x1c` (`0x006c83c0`),
calls slot `+0x5c` (`0x006c8630`, at `0x006c8429`), which goes to the decoder's `0x006d1ab0`, a byte copy. Frames
are decoded in `0x006d17b0`: Layer II by `0x006cdf50`, Layer I by `0x006ce5a0`. Both use one of two synthesis
routines, picked by `DAT_00fb2704`, and both write 16-bit samples.

**Each voice is clamped to 16 bits as it is decoded, never wrapped.** The x87 routine `0x006c9270` makes its three
word stores (`0x006c9415`, `0x006c947f`, `0x006c953e`) after `FISTP` and an explicit clamp to `0x7fff` and
`0xffff8000`. Every one of the MMX routine `0x006ca580`'s 96 word stores stores a `PACKSSDW`, which saturates. 336
shipped entries peak past full scale in a floating-point decode (FileFormats `sounds.md`, "Decoding"), so the clamp
takes effect on those entries: ffmpeg's 16-bit decode clips them the same way. How QMixer adds 16-bit voices
together is not in the executable.

OpenTPW does not clamp a voice: `AudioClip` keeps NLayer's floats, up to 1.40, and only the final mix is clamped
(`Audio.Mix`), so a clip past full scale played below full volume is louder at its peaks than the original's.

## Unknowns

- **`FUN_0051c700` is NOT the distance-mapping feed.** It is the sound-detail ladder that interpolates the three `RadiusInfo[n].MINRADIUS` values (100.0 / 0.5 / 0 at SWITCH 25 / 50 / 75) from `data\sound.sam` and posts them to `0x006b5890` → `FUN_006b99c0` → `FUN_006b8180`, which latches `{on/off, radius}` and walks the voice array setting a per-voice LEVEL. That level comes from `FUN_006c4c80`, a **segment-versus-circle occlusion test that uses only the X and Z components** and ignores height entirely — so the pause's Y-lift cannot touch it either way. It never reaches `SetDistanceMapping`.
- `SndReverb.map`'s record shape is **not decoded**.
- The actual parameters passed to `SetDistanceMapping` are still **unknown**, but where they live is now known precisely. `QSWaveMixSetDistanceMapping` has **exactly one call site in the image**, `0x006c581b` in `FUN_006c5690`, reached as `if ( params->flags_at_0x14 & 0x200 ) vtbl[0x60]( out, channel, params + 0x38 )` — so the three values are the dwords at `params+0x38..+0x40`. **No writer of that `0x200` request bit exists anywhere in the image** (404 MOV-imm32 and 2 OR-imm32 candidates examined, none carrying it); the record is built by a virtual-dispatched builder that was not reached statically. So **QMixer's own DEFAULT mapping probably governs**, and that is in `QMixer.dll` (307,200 bytes, 27 Jan 2000, exporting `QSWaveMixGetDistanceMapping`), not in `testme.exe`. The Ghidra project holds only `testme.exe` and `TP.ICD`; importing the DLL is the one step that would settle it.
- **`params+0x14` is a request mask, not a flag pair.** `FUN_006c5690` tests at least nine bits of it, each gating one setter: `0x1` the position path (`FUN_006b7fa0`), `0x2` volume, `0x20` frequency, `0x40`, `0x100` source cone, `0x200` distance mapping, `0x400`, `0x20000`, `0x80000`.
- **Consequently, "a pause attenuates every placed sound to nothing" is NOT established**, in either direction, and nothing measured supports it. OpenTPW holds placed voices to **silence** instead. That is a choice standing in for a curve nobody has measured, and it is said at the site, in `Audio.HoldPlaced`.
