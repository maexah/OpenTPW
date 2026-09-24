# Park weather

Weather in the original is not a subsystem: it is a **thing of model 15** in the park's thing list, ticked like any other thing, and everything it does follows from a single `mQuality` value that the park's clock re-rolls on a seven-day cycle. The clock itself is an accelerated wall clock ("funny time") whose counter advances every eighth 31 ms tick, so a day is about 5.714 s and a weather change lands every ~40 real seconds. The `.sam` tuning schema — key names included — is compiled into the executable as descriptor records, and the runtime addresses those keys bind to were recovered by simulating the binder. The rain and thunder samples are identified both from the exe (effect ids in a global `cat_ambient` category) and independently from the shipped sound maps.

## Weather is a thing (model 15)

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_0050b360` | — | Thing-update dispatcher; switches on the thing's type byte at `+2`. Case `0x0f` calls `FUN_00512880`. | Decompile of the switch |
| `FUN_00512880` | — | The weather thing's update: schedules the next change, rolls the new quality, publishes the forecast | Case `0x0f` target |
| `FUN_00511f70` | — | Weather constructor, **104 bytes (0x68)**; writes the type byte via `FUN_0050afe0(this,1,0xf)` | Decompile |
| `FUN_0050afe0` | — | Thing type-byte writer | Call in the constructor |
| `0x00518db4` | — | Loader jump table; entry `0x0e` dispatches model 15 to `0x00518b62` | Table read |
| `0x00518b62` | — | Model 15 load entry | Jump-table entry `0x0e` |
| `FUN_0050b550` | — | Thing broadcast-message dispatcher; case `0x0f` → `FUN_005127a0` | Decompile |
| `FUN_005127a0` | — | Weather message handler: message 1 = "apply now" → `FUN_00512f00`; message 2 = "forecast in N days" → fills `+0x20..+0x2c` | Decompile |

`ParkWorld`'s header field **`mWeather` is that thing's HANDLE**, sitting among `mMechanicHQ` / `mResearchLab` / `mStaffHQ`, all manager handles — it is **not** a quality value. The saved record is **99 bytes** = 4 (id) + 4 (model 15) + 91 body, matching the "thing id 7, model 15, 99 bytes" record in jungle's save.

### Field map

Read from the named save stream in `FUN_00512010`: the original announces every field name to a logging call the release build compiles away — the same trick that recovered the `ParkWorld` header.

| Offset | Original name | What it is | Evidence |
|---|---|---|---|
| `+0x0c` | `mQuality` | 1..100; everything else derives from it | Named save stream |
| `+0x20..+0x2c` | — | Forecast record, filled by message 2 | Named save stream |
| `+0x30` | `mCurrentDrops` | Live particle count, ramped toward the target | Named save stream |
| `+0x34` | `mTargetDrops` | Target particle count; drives `FUN_0051e830` | Named save stream |
| `+0x38` | `mLightning` | Flag, set when quality is in the lightning band | Named save stream |
| `+0x3c` | `mLightningCountdown` | — | Named save stream |
| `+0x40` | `mLightningNumThisPeriod` | — | Named save stream |
| `+0x44` | `mThunderCountdown` | Weather ticks left before the thunder sample plays | Named save stream |
| `+0x48` | `mLastLightningLocation` | vec3; the **top** of the last bolt, y = 300 | Named save stream |
| `+0x54` | `mRainSoundHandle` | Per-thing rain voice; string at `0x007622e4`; serialized at `0x00512387` and `0x005126de` | Named save stream, string table |
| `+0x58` | `mPType` | Saved and loaded, **never read** — **purpose unknown** | Named save stream |
| `+0x5c` | `mWindDir` | Degrees, wrapped `[0,360)` | Named save stream |
| `+0x60` | `mWindForce` | `(100 - q) * 0.01`, clamped `[0,1]` | Named save stream |
| `+0x64` | `mOverridden` | Saved and loaded, **never read by anything traced**. Whether it is the original's forced-weather latch is **not established** | Named save stream |

### Quality is the only input

`FUN_00512f00` is the only converter from quality to weather:

```
snow      if QualityForSnowLow      <= q <= QualityForSnowHigh      -> particle mode 2, NO sound
rain      if QualityForRainLow      <= q <= QualityForRainHigh      -> particle mode 1 + rain loop
lightning if QualityForLightningLow <= q <= QualityForLightningHigh -> sets +0x38

intensity  = (q - low) * 100 / (high - low)
particles  = ((100 - intensity) * MaxRaindrops) / 100, capped at MaxRaindrops
mWindForce = (100 - q) * 0.01 clamped [0,1]
mWindDir  += (rand % 90) - 45, wrapped [0,360)   then FUN_0057d920(dir, force)
fall speed = 0.8 for snow, 2.5 for rain (FUN_0057dc40)
```

**Quality is INVERTED into rain: low quality = bad weather.** Jungle ships quality 78 against a rain band of 0..40, so it starts dry — but it does not stay dry, because the quality is re-rolled every cycle.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00512f00` | — | Quality → particle mode, wind, sound; the sole converter | Decompile |
| `FUN_00516330` | — | Coin flip; decides the winner when a quality qualifies as both snow and rain | Call in `FUN_00512f00` |
| `FUN_0057d920` | — | Wind setter `(angleDegrees, force)`; stored normalised with **y fixed at -1 before normalisation** | Decompile |
| `FUN_0057dc40` | — | Fall-speed setter (velocity in units/sec) | Decompile |
| `FUN_00512e10` | — | Rolls a new quality for the next cycle | Call in `FUN_00512880` |

## The calendar: "funny time", and why it must be reproduced as a counter

There is no day counter. The calendar is an **accelerated wall clock read back through `FileTimeToSystemTime`**, and a day advances as an **edge** — when the derived calendar day differs from `mDayAtLastUpdate` — not by accumulation.

```
funny_seconds = counter * mFunnySecsPerRealSec / 4     (FUN_004f8770 / FUN_004f8260)
counter       = *(0x0080239c) + 0x1da70c,  +1 per FUN_00516380 call
FUN_00516380 is called every 8th 31 ms tick = 248 ms = ~4/s, hence the /4
```

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_004f8260` | — | The day advance; sole writer of `mDayAtLastUpdate` / `mMonthAtLastUpdate` | Decompile |
| `FUN_004f8770` | — | Counter → funny seconds | Decompile |
| `0x0080239c` | — | Base of the tick counter (`+ 0x1da70c`) | Decompile |
| `FUN_00516380` | — | Increments the counter; one call per eight ticks | Call site below |
| `0x0054f668` | — | `TEST byte [0x00877d34],0x7` — the every-eighth-tick gate | Disassembly |
| `0x0054f7bb` | — | The gated `CALL FUN_00516380` | Disassembly |
| `0x00877d34` | — | The 31 ms tick counter itself | Gate above |
| `0x004f7ea9` | — | `MOV [ESI+0x1c],0x3a98` — `mFunnySecsPerRealSec` defaults to **15000** | Disassembly of `FUN_004f7e80` |
| `FUN_004f7e80` | — | Game-clock constructor | Decompile |
| `FUN_004f7f30` | — | Clock save block: `+0x00 mFunnyTimeStart`, `+0x08 mSessionStart`, `+0x10 mMonthAtLastUpdate`, `+0x14 mDayAtLastUpdate`, `+0x1c mFunnySecsPerRealSec`. **`+0x18` (year) is deliberately not saved** | Named save stream |
| `0x007ced58` | — | Published calendar: `+58` second, `+5c` minute, `+60` hour, `+64` day, `+68` month (0-based), `+6c` year-1900, `+70` day-of-week — written every world tick | Decompile |
| `0x0054f680` | — | `CMP EAX,0x3` — **at most 3 world-clock advances per rendered frame**, so a stuttering machine loses time and never gains it | Disassembly |
| `0x785970` | — | The millisecond clock, used by the VM's `SETTIMER`/`GETTIMER`/`GETTIME` — a different clock from this one | Decompile |

**The nominal day is 86400/15000 = 5.76 s. The real figure is ~5.714 s, and the drift is STRUCTURAL, not a rounding error.** The counter is fed one unit per **248 ms** while every conversion divides by **4** as if the unit were 250 ms, so funny time runs **~0.806% fast** and nothing anywhere compensates. **A reimplementation that wants the same dates must advance a counter every 8 ticks and compute `counter * rate / 4` — it must not integrate wall-clock seconds.**

At that rate: a month ≈ 2.9 min, a year ≈ 35 min, and **a weather change (7 days) ≈ 40 real seconds**.

- **Day length is per-park DATA, not a constant** — it lives in the clock's save block — but nothing in the shipped game ever changes it. There are exactly two writers (the 15000 default and the save loader), no script opcode, no `.sam` key, and the clock pointer never escapes into a variable. Jungle's `Easymode.TPWI`, inflated and read, holds `98 3a 00 00` = 15000 at decompressed offset `0x1a57`, the last field of the clock block that starts at `0x1a3f`.
- The VM's `YEAR` / `MONTH` / `DAY` / `HOUR` / `MIN` / `SEC` (opcodes 97–102) read the **real system clock**, not this one.

### Seasons, and December's out-of-range read

**SEASON = wMonth / 3, on the RAW 1-based month** — a signed divide at `0x004f87e7`, with no clamp.

**December is an out-of-range table read, and it is a real bug in the original.** `wMonth` 12 gives index 4 against a 4-entry table. The address it reaches is `0x007854c4 + 4*0xc` = **`0x007854f4`** — **not** `0x007854f8`. So December reads Avg from the Seasons *element-count* slot, Tolerance from `DaysOfWarning` (4) and Chance from `DaysBetweenChanges` (7).

> The often-quoted reading **"Avg = 4, Tolerance = 7, Chance = 20"** is **REFUTED**: it is off by one dword and wrong on all three values.

## The `.sam` schema, with its key names, is compiled into the exe

The key names are present in the binary as raw bytes inside descriptor records. A string search reports them absent — the same failure mode that once hid `'camcorder'` (Jython's `getValue()` does not reliably yield a string); reading the raw bytes shows the names in place.

**Descriptor records, stride `0x3c`:** `+0x00` type, `+0x04` name[0x20], `+0x24` min, `+0x28` max, `+0x34` array count.
**Types:** `2` = table start, `3` = table end (carries the table's name and element count), `1` = section name (appears AFTER its fields), `0` = separator, `5`/`6` = scalar (`6` is bounded).

| Address | Type | Original name | Bounds |
|---|---|---|---|
| `0x00741c50` | 2 | *(table start)* | — |
| `0x00741c8c` | 6 | `AvgWeatherQuality` | 0..100 |
| `0x00741cc8` | 6 | `NormalTolerance` | 0..100 |
| `0x00741d04` | 6 | `ChanceForExceptionalWeather` | 0..100 |
| `0x00741d40` | 3 | `Seasons` | count 4 |
| `0x00741db8` | 5 | `DaysOfWarning` | — |
| `0x00741df4` | 6 | `DaysBetweenChanges` | 1..1000 |
| `0x00741e30` | 6 | `SpeedOfChange` | 1..100 |
| `0x00741e6c` | 1 | `Weather` *(section)* | — |
| `0x00741ee4` | 6 | `QualityForSnowLow` | -1..100 |
| *(following records)* | 6 | `QualityForSnowHigh`, `QualityForRainLow/High`, `QualityForLightningLow/High`, `LightningCountdownSpeed` 1..1000, `LightningCountdownFrom` 1..10000, `LightningTime`, `ThunderStartsThisFarAway` 0..15, `ThunderRandomiser` 1..4 | as noted |
| `0x00742178` | 6 | `MaxRaindrops` | 0..1000 |
| `0x007421b4` | 1 | `WeatherEffects` *(section)* | — |

Those names reassemble into the shipped keys exactly: `Seasons[k].AvgWeatherQuality`, `Weather.DaysOfWarning`, `WeatherEffects.MaxRaindrops`.

**`-1` is a legal sentinel, not a hack.** `QualityForSnow*`'s declared bound is `-1..100`, so the shipped `-1/-1` is the format's own way of saying "never".

### Runtime addresses

Descriptors carry no target address; the binder `FUN_00401030` computes them positionally. Simulating that allocator reproduced **18/18** of the addresses the weather code reads (the rival reading scored 3/18).

| Address | Original name | Shipped value | Note |
|---|---|---|---|
| `0x007854c0` | `GuardConstsPerGrade` element count | — | Precedes the Seasons table |
| `0x007854c4 + 12k` | `Seasons[k].AvgWeatherQuality` | 75 / 80 / 90 / 50 | |
| `0x007854c8 + 12k` | `Seasons[k].NormalTolerance` | 25 / 20 / 10 / 15 | |
| `0x007854cc + 12k` | `Seasons[k].ChanceForExceptionalWeather` | 10 / 10 / 5 / 10 | |
| `0x007854f4` | `Seasons` element count | 4 | The "unexplained hole": it is the table's countptr, and what December reads as Avg |
| `0x007854f8` | `Weather.DaysOfWarning` | 4 | |
| `0x007854fc` | `Weather.DaysBetweenChanges` | 7 | |
| `0x00785500` | `Weather.SpeedOfChange` | 20 | |
| `0x00785504` / `0x00785508` | `QualityForSnowLow` / `High` | -1 / -1 | Global layer |
| `0x0078550c` / `0x00785510` | `QualityForRainLow` / `High` | 0 / 40 | Global layer |
| `0x00785514` / `0x00785518` | `QualityForLightningLow` / `High` | 0 / 15 | Global layer |
| `0x0078551c` | `LightningCountdownSpeed` | 10 | |
| `0x00785520` | `LightningCountdownFrom` | 500 | |
| `0x00785524` | `LightningTime` | 2000 | ms; the park bolt's lifetime |
| `0x00785528` | `ThunderStartsThisFarAway` | 10 | Weather ticks |
| `0x0078552c` | `ThunderRandomiser` | 2 | |
| `0x00785530` | `MaxRaindrops` | 600 | The only weather key no theme declares |

### The forecast is real

`FUN_00512880` schedules the next change at `mLastChanged + (DaysBetweenChanges - DaysOfWarning)` = 3 days, rolls the new quality with `FUN_00512e10`, and publishes it as a forecast; the thing applies it **`DaysOfWarning` (4) days later**, so the full cycle is 7 days. **Whether the warning is ever player-visible** (an advisor line, a forecast panel) is **not established**.

## Weather is per-theme

Every theme restates **26 of the 27** weather keys in its own `levels/<theme>/Standard.sam`, and five genuinely differ. `MaxRaindrops` is the one key no theme declares, so it alone comes from the global layer.

> The inherited claim that "the weather keys are identical in all four themes — weather is not per-theme" is **REFUTED**.

| Key | global | jungle | hallow | space | fantasy |
|---|---|---|---|---|---|
| `QualityForSnowLow/High` | -1/-1 | -1/-1 | **0/25** | -1/-1 | -1/-1 |
| `QualityForRainLow/High` | 0/40 | 0/40 | **15/45** | **-1/-1** | 0/40 |
| `QualityForLightningHigh` | 15 | 15 | **25** | 15 | 15 |

- **Hallow is the stormy one**: it snows, it rains harder and over a wider band, and its lightning reaches 25 instead of 15. Corroborated independently from a different file entirely — the lobby island scripts say the same thing, and of the four parks only hallow asks for `RAINY(1)` and `LIGHTNING(63)`.
- **Space never rains**: its rain band is `-1/-1`, disabled exactly as snow is elsewhere.
- **Everything else is shared**: all four seasons, all three pacing keys and every storm-timing number are identical in all four themes.

**The coin flip in `FUN_00512f00` is not dead code — it exists for hallow.** Hallow's snow band 0..25 and rain band 15..45 **overlap at 15..25**, so a quality in that range qualifies as both, which is precisely the case the original resolves by tossing a coin (`FUN_00516330`).

## Snow, and where it can happen

There is a snow branch in `FUN_00512f00` (particle mode 2), a dedicated renderer `FUN_0057f490` using a 128-entry cubic spline table for per-flake flutter, and `snow.tga` ships in both `data/generic/weather/` and the low-detail `data/generic/sweather/`.

> The claim that **"the only entry point can never be taken"** is **REFUTED**. It is true of jungle, space and fantasy. **Hallow's snow band is 0..25**, and quality is clamped to 1..100, so a quality of 1 to 25 lands squarely in it. Hallow is the game's winter.

## The renderers — one shared object, lobby and park alike

| Address / offset | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_008bcbac` | — | A **single global particle/bolt object used by BOTH scenes**; they differ only in the values pushed into it | Decompile |
| `FUN_0057ff60` | — | Draws it, once per frame | Decompile |
| `FUN_0057d300` | — | Engine init: **1024 drops**, random points in the cube [-4,+4] on each axis, stride 12 bytes at object `+8`..`+0x3008` | Decompile |
| `FUN_0057d910` | — | One-instruction setter writing the live drop count to `+0x3318`; it does **not clamp** | Decompile |
| `DAT_0087afe8 + 0x30` | — | The camera vector the drop cube wraps around | Decompile |
| `DAT_00790ab0` | — | Parallax selector: factor **0.1**, or 0.3 when `& 0x16` | Decompile |
| `FUN_0057d850` | — | Art registration: `raindrop.tga` at `+0x3350`, `snow.tga` at `+0x3348`, `lightning.tga` at `+0x334c`, each with flag bits `0x80200` | Decompile |
| `FUN_00423b50` | — | Sets the shared render object's type word | Decompile |
| `FUN_005508c0` / `FUN_00550950` | — | Register **1 = raindrop.tga, 2 = snow.tga** | Decompile |
| `FUN_0057f490` | — | Snow renderer, 128-entry cubic spline table for per-flake flutter | Decompile |

- The array holds exactly 1024 drops and `FUN_0057d910` does not clamp. Shipped `MaxRaindrops` 600 is safe; **a park file with more would overrun into the bolt array**.
- Every frame each drop is drawn at (particle − wrapped camera origin) and re-wrapped into the same cube: `if c > 4 then c -= 8 else if c < -4 then c += 8`. With the parallax factor of 0.1, **the rain drifts at a tenth of the camera's speed**.
- Fall speed is a **velocity in units/sec**: offsets advance by `direction * speed * dt`, with `dt` capped at 500 ms. The lobby never calls either setter, so **lobby rain falls at the default 5.0** while a park uses 2.5 (rain) or 0.8 (snow).
- A drop is a quad from `p1` to `p1 + windDirection * 0.1`; **culled if z <= 0**; brightness `(6.0 - z) * 12.0` clamped at 0, so drops fade out by z = 6.

### The bolt

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `FUN_00512c50` | — | Returns the bolt's endpoints: **GROUND first, SKY second** | Decompile |
| `FUN_00580320` | — | Draws a bolt; **argument 7 is a LIFETIME IN MILLISECONDS**, stored at record `+0x24` and decremented by the frame's elapsed ms | Decompile |

- Ground point: y = 0, x/z random across the whole map then clamped to `[1, cells*10]`. Sky point: y = **300**, leaning `rand*100 - 50` in x and z. **`FUN_00512c50`'s third argument is pushed and never read.**
- Lifetime: park 2000 = `LightningTime`; lobby 500 = half a second. **The lobby's OTHER 500 is a different argument — the sky point's y.** The lobby bolt is 500 units tall *and* lasts 500 ms.
- Argument 8 is a **seed/branching flag**: non-zero stores an rdtsc-derived odd value making the jagged path stable across frames, enabling up to 4 levels of branching and a flicker colour; zero reseeds the path every frame, draws pure white and forbids branching.
- Bolt records are `0x30` bytes, **max 16**; extra bolts in a frame are silently dropped while the function still returns 1.

### What the park camera does to the bolt

**Most of a park's bolt is above the screen, and it is the camera, not a fault.** The park orbit camera is zoom 70..130, pitch 45..65 degrees; measured at cam = (475, 117, 94) — ninety-four units up, pitched ~57 down, 90-degree vertical FOV. The top of frame is therefore about **12 degrees BELOW horizontal**, which at a bolt's ~215-unit distance is world **z ≈ 48**, against a bolt spanning **z 0..300**. Nothing above the camera's own 94 can ever be in shot while the pitch is down, so **about five sixths of every bolt is off the top of the frame**.

**A bolt arrives as a DIAGONAL, not a column, and so does the rain.** A world-vertical line seen from a downward pitch projects as a ray radiating from the nadir, so a strike off to one side enters from a top corner. Rain read as a "radial starburst" in a thumbnail is this projection, not a defect: magnified, it is ordinary falling rain — thin white slanted streaks varying in length with distance.

## The sounds

The exe says thunder is **effect `0x20`** and the rain loop **effect `0x21`** in the category at `DAT_00803a20`, which `Sound_RegisterGlobalCategories` (`0x0051eae0`) creates by name as **`cat_ambient`** — a **global** category, not one of the four a park registers in state 9. The exe cannot say which samples those are; the shipped data can:

```
data/global/sound/cat_ambientSFX.map, bank data/global/sound/AmbientHD.sdt
  effect 32 (0x20)  ONE list of [Thunder2.mp2, Thunder3.mp2, Thunder4.mp2]
                    cumulative weights 21845 / 43690 / 65535 = even thirds
  effect 33 (0x21)  RAIN.mp2 in four lists (four variations of the same file)
```

Two independent methods agree exactly, and a third confirms it live: the runtime log reads `Sound category global/sound/ambient: 1 bank(s), 18 effect(s) [... 33x4, 32x3 ...]` — four samples on the rain effect, three on thunder.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `DAT_00803a20` | `cat_ambient` | The global ambient sound category holding effects `0x20` and `0x21` | Decompile |
| `0x0051eae0` | `Sound_RegisterGlobalCategories` | Creates `cat_ambient` by name | Decompile |
| `FUN_0051e830` | — | The **rain level**, exact analogue of the music level; drives the global rain voice from `+0x34` | Decompile |
| `FUN_0051e790` | — | The music level | Decompile |
| `FUN_0051bc40` | — | `(handle, selector, level)`; both level setters go through it | Decompile |
| `FUN_005131f0` | — | `(this, mode)`: **0** = start rain (particle mode 1 + effect `0x21` into thing `+0x54`), **1** = start snow (mode 2, fade the rain loop), **2** = stop everything | Decompile |
| `DAT_00803aac` | — | The **global** rain voice handle started by `FUN_0051e830` | Decompile |
| `DAT_00802bcc` | — | Runtime pointer behind a new voice's default level; **cannot be resolved statically** | Decompile |
| `0x00513070` | — | `PUSH 0x0` — the rain branch of `FUN_00512f00` **starts** `+0x54` | Disassembly |
| `0x00512ffd` | — | `PUSH 0x1` — the snow branch **stops** `+0x54` | Disassembly |

- **Thunder is POSITIONAL**: `Sound_PlayEffect(0, cat_ambient, 0x20, x, y, z)`, where the position is `mLastLightningLocation` — the **TOP** of the bolt, y = 300. The trailing "float" in the decompile is three `__ftol` arguments, **not** a volume.
- **Thunder is delayed after the flash** by `ThunderStartsThisFarAway + rand % ThunderRandomiser` weather ticks — light before sound, deliberately.
- **The selector passed to `FUN_0051bc40` is not a loop selector.** The handle identifies the voice; the selector indexes one of four per-voice control slots whose key byte comes from the sample record, which is why music / kids / rain use 4 / 7 / 8.
- **The lobby's thunder and a park's are the SAME recordings.** `SfxHD.sdt` also holds Thunder2/3/4, two of the three at byte-identical sizes, so a `ThunderVolume` measured in the lobby carries across on evidence rather than by assumption.

### Two rain voices: what is fact, and what is inference

**The original starts TWO rain voices in rain and ONE in snow — this is fact.** `FUN_005131f0` starts effect `0x21` into `thing+0x54`, and `FUN_0051e830` starts the same effect into the global `DAT_00803aac`. Two handles, two voices, never copied between each other, both live at once, and **no per-effect dedupe exists** (the driver's only guard is keyed on the handle you pass in, and both sites pass 0). Only the global one is ever volume-controlled.

> The conclusion "so build one voice — the modulated one" is **REFUTED**, while every fact under it stands:
> - `thing+0x54` is `mRainSoundHandle` (string `0x007622e4`) and is **SERIALIZED** in the save block (`FUN_00512010`, at `0x00512387` and `0x005126de`). It is not an incidental extra voice.
> - `FUN_00423b50` / `FUN_005508c0` / `FUN_00550950` establish **1 = raindrop.tga, 2 = snow.tga**, so `FUN_005131f0` case 0 is RAIN and case 1 is SNOW.
> - In `FUN_00512f00` the rain branch **starts** `+0x54` (`0x00513070`) and the snow branch **stops** it (`0x00512ffd`) — but **BOTH write `+0x34` identically**, and `+0x34` is what drives `FUN_0051e830`. So the global loop plays in both weathers, and **`mRainSoundHandle` is the only thing that makes rain sound different from snow**.

**NOT ESTABLISHED: whether the duplicate is AUDIBLE.** A new voice's default level lives behind the runtime pointer `DAT_00802bcc` and cannot be resolved statically. Every play-then-control pattern in this module sets a level immediately after playing (ops 4, 7, 8), which is *consistent with* the default not being the intended level but proves nothing.

**Keep the distinction: "the original starts both" is fact; "you can hear both" is inference, and the deciding measurement has not been made.** Since snow is unreachable in shipped data outside hallow, the rain/snow distinction this preserves rarely manifests — so a one-voice build is defensible as a documented deviation, and a two-voice build is defensible as faithful. The question is to be settled by **measuring in OpenTPW**, not by argument.

## `MaxRaindrops` is a cap that can never be reached

The heaviest rain the game can make is **580 drops, not 600**. A quality clamps to 1, so `intensity = (1-0)*100/40 = 2` and the target is `(100-2)*600/100 = 588`; the ramp steps by `SpeedOfChange` and **stops within one step of its target**, so it settles at 580 and never lands on 588.

## Behaviour measured in the running game

| Measurement | Result | Predicted |
|---|---|---|
| Weather tick rate | 3.99/s | 4.03/s |
| Ramp to a new target | 7.3 s, 29 steps | 7.2 s at `SpeedOfChange` 20 per weather tick |
| Calendar advance | 4 days in 23 s | 5.714 s a day |
| A park's weather turning on its own | quality 50 → 87 inside a minute | ~40 s a change |
| A forced quality of 0 | clamped to 1 | clamp 1..100 |

**The 3.99/s is the load-bearing measurement.** It says the weather is paced by the every-eighth-tick gate — the calendar's counter — and not by frames or by the game tick. On the game tick it would run **eight times too fast**.

Because a park's weather turns on its own every ~40 s, **any forced quality must either be re-forced or made to hold**: a forced 0 measured twenty seconds later comes back as a delivered 32 (exactly 120 drops and no lightning) simply because the schedule rolled underneath it.

Sound categories register no `Asset`s and so cost no load steps; jungle's load-step count is unchanged by them.

## Pause, and why the calendar cannot roll while paused

Pause is nothing but a frozen clock, confirmed four separate ways. While paused, no ticks run, `0x00877d34` stops, `FUN_00516380` is never reached, funny time stops, and **the calendar cannot roll over**. `GameClock_Resume` folds the paused span into the epoch at `+0x2c`, so time continues from where it froze rather than jumping.

| Address | Original name | What it is | Evidence |
|---|---|---|---|
| `0x00402d70` | — | Thunk to `FUN_004031e0` → `FUN_00402f10` → `FUN_004030d0` | Disassembly |
| `FUN_004030d0` | — | Returns a frozen capture while `+0x30` is set | Decompile |
| `0x004092c3` / `0x0054f47a` | — | `MOV ECX,0x785970` — `Game_Pause` freezes the very object the park loop reads | Disassembly |
| `0x00786ba4` | `Game+0x3c` (Ghidra: `g_ParkRunning`) | **The PAUSE-PERMISSION GATE** | Disassembly |
| `0x00786b84` | `Game+0x1c` | **The PAUSED FLAG**, written by `Game_Pause` at `0x004092ad` | Disassembly |
| `0x004092a3` / `0x00409303` / `0x00409353` | — | `CMP dword ptr [ESI+0x3c],0x1` in Pause / Resume / TogglePause, each JNZ-ing to a bare epilogue. **Equality with 1**, not merely non-zero | Disassembly |
| `0x0054e682` / `0x0054ea4c` | `Game_StateMachine` | Sets the gate's base per scene: **0 on the lobby load**, **1 on an ordinary park load** | Disassembly |
| `0x00407bd9` | — | The Game constructor writes the gate to 1 **from boot**; register-relative, so it **never appears in Ghidra's address-xref list** | Disassembly |
| `0x0054e96d` / `0x0054ea3f` | — | The `+0x3f0` test: an **online-requested park loads with the gate 0** | Disassembly |
| `0x00486b90` / `0x00486bce` | — | Reads the **paused flag** to suppress a particle spawn | Disassembly |
| `0x0040936d` | `Game_TogglePause` | Reads its own paused state | Disassembly |
| `0x0040bf72` | — | **Undisassembled** command-table entry: `PUSH 0x1; MOV ECX,0x786b68; CALL Game_TogglePause` — the 80th load of the game object, invisible to every reference list | Byte read |
| `0x786b68` | — | The game object, as loaded by that command-table entry | Disassembly |
| `0x0054f4d4` | — | `[0x007a1a14] & 8` — window deactivation skips the entire simulation body on alt-tab, **after** `0x00877d34` has been incremented, so the tick counter advances while nothing simulates | Disassembly |
| `0x007a1a14` | — | The window-activation word | Test above |

**The two globals are one hex digit apart and easy to conflate: `0x00786ba4` is the pause-permission gate, `0x00786b84` is the paused flag.** Keep both.

> **"1 means a park is running" is flatly FALSE.** It is 1 from boot (`0x00407bd9`); an online-requested park loads with it 0 (`0x0054e96d`); and it is **never cleared when a park ends** — the clear lives in case 1, the *lobby load*, not case `0xb`.

> **"Neither loop tests a paused flag" is TRUE OF THE LOOPS and must not be generalised.** No tick loop anywhere reads it, but **"nothing tests it" is REFUTED**: `FUN_00486b90` and `Game_TogglePause` both read it.

The gate is **both** a pause-permission gate and a re-entrancy latch — one field at two timescales. `Game_StateMachine` sets the base per scene, and each pausing screen **borrows** it by zeroing it *after* `Game_Pause` returns, restoring 1 before `Game_Resume`. The accurate one-liner: **"pausing is permitted right now and nobody holds it."**

### Why the lobby does not pause — three independent barriers

`GameMenu_Open` guards in three steps:

1. `0x0048c830` — a menu already exists (`0x007c2534`) → return.
2. `0x0048c83a` — **the SCENE ARGUMENT: non-zero builds the lobby menu (`0x0048c600`) and returns without ever touching the pause.** The lobby passes 1 (`PUSH 0x1` at `0x005e4207` in its Escape handler); the four other callers pass 0.
3. Only the zero/park path reaches `0x0048c868` and tests the gate.

Then: the gate is 0 all through the lobby anyway, and `Game_Pause` refuses regardless.

- **The options screen IS reachable from the lobby** (lobby menu id `0xb` → `CALL 0x004a3a30`), and there the gate test genuinely is what prevents the pause — **but it is not a refusal**: the screen still opens, the branch only decides whether to pause, and `0x007cb2ec` remembers which happened.
- `MessageBox_Open` alone also tests `0x00f82884`, which is a **POINTER** (the lobby front-end object), not a boolean — non-null while the lobby exists.

### `Game_Pause` is not unconditional

`thunk_FUN_0048a6e0` runs only when **arg1 == 1**, and `Advisor_PauseVoice` + `FUN_0051c1c0(1)` are the **ELSE branch of arg2**. Every screen-driven site passes **(0,0)**, so for them the thunk never fires and the advisor path always does. **A pause also moves the 3D listener**: `DAT_00803ad2` is set, and `FUN_0051c1d0` then replaces the listener's second coordinate with **10000.0**. That second coordinate is **height** — the original is Y-up, which its own lobby listener call site settles by passing a `(0,1,0)` top vector. Full decode in `audio.md`, "The listener, and what a pause does to it".

**Two cautions.** The window procedure's `WM_ACTIVATEAPP` branch passes **`(1,1)`** at `0x0046b74c`, so not every call site passes (0,0), and on **alt-tab** the *voice-pausing* path runs instead of the listener one. And **how far that 10,000-unit lift actually turns a sound down is undetermined**: QMixer's distance model is not in the executable. Do not read this row as "every placed sound attenuates to nothing".

> Ghidra's xref index hides live code: `0x0040bf72` above is undisassembled, as were `FUN_0048b6a0` and the camcorder thunks. **An audit resting on Ghidra's xrefs silently undercounts.**

## Open questions

- `mPType` (`+0x58`): saved, loaded, never read. **Purpose unknown.**
- `mOverridden` (`+0x64`): saved, loaded, never read by anything traced. Whether it is the original's forced-weather latch is **not established**.
- Whether the forecast warning is ever player-visible is **not established**.
- Whether the second (un-modulated) rain voice is **audible** is **not established**; the deciding measurement has not been made.
- `FUN_00512c50`'s third argument is pushed and never read.
- Rain in a park uses the same `Rain` constants as the lobby (half-extents 44 × 44 × 34, widened past 4:3 by the view's
  spread; look-ahead 26; fall 62), tuned for islands ~70 units wide where a park is 1280. They read correctly; whether the density is right is a judgement call, not arithmetic.
