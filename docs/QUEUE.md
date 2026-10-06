# Work queue

Begun 2026-09-22 against `main` at `9b0ebab`; items from Q35 on were filed against later tips. File and doc line
numbers in Q13-Q34 are from `9b0ebab` and have drifted (`park-engine.md` by up to ~1,100 lines): find the member or
the heading, never the line.

**How to use this file.** One item per session. Take the first unticked item and do only that one.
Tick it in the commit that lands it, with the number that proves it; the pre-commit hook then moves it to the end of
its section in `history/queue-done.md`, which holds every finished item. A page citing a Q-number that is not here
means that finished entry. If an item turns out to be two,
split it into two lines here and stop after the first. Alexah may reorder; nobody else does.

**Every item, no exceptions.**

- Start from the repo root. Read `CLAUDE.md`, `docs/STATUS.md`, then this file.
- Branch from `main`, named for the item, next free number: `alexah/N-<what-it-changes>`.
- "Confirm" means the running game: a screenshot **and** the census or log line that goes with it,
  both. Predict the number before reading it. A test is added for regression only. It is never the proof.
- Put the bug back and re-run the new test. If it stays green, the test is hollow. Fix the test.
- Build and test the commit alone in a throwaway worktree.
- Facts found go in `docs/exe/`, and bytes in a shipped file in the FileFormats clone (rule 14). The commit
  message says why. The code comment says what the code does now. No "used to say", no dates, no `>>>`
  banners in code.
- Update `docs/STATUS.md` in the same commit. It stays under 120 lines.
- When confirmed, fast-forward merge into `main`. Commit locally. **Pushing `main` still needs Alexah's
  yes** (rule 1). Say what is ready and stop. Do not start the next item.
- End with four lines: what is ready, what was confirmed on screen, what was not, what the next item is.

**Where the detail is.** `docs/REVIEW-2026-09-21.md` section 5 for Q1 and Q3 to Q7.
`docs/REVIEW-2026-09-22.md` for Q2 and Q8 to Q12. Q70-Q75 come from the 2026-09-12 review, whose three
artifacts are listed in `docs/history/README.md`. Q208-Q217 come from the 2026-10-04 review of the commits of
2026-10-03 and 2026-10-04; an id such as `u6-hoardings-1` names a finding in its findings file (`CLAUDE.local.md`
says where). Q219-Q221 come from the 2026-10-04 effort audit of the commits of 2026-09-29 to 2026-10-01; an id such
as `r-f1` names a verdict in its results file (`CLAUDE.local.md` says where). Q222 comes from Q107's comparison with
the original.

---

## A. Bugs first

- [ ] **Q130d. The hire list stands in name order in the original. Decode first.** From Q130c. On four tabs the
  original lists its candidates by name (Chris Battson above Rajan Tande, who holds the earlier slot); OpenTPW's
  `ParkHireScreen` lists them in the pool's order. Decode what orders the list (`FUN_00481550` adds a row;
  `UiList.Insert` already models the sorted insert `FUN_0066403b`) and whether a heading's click re-sorts it. Confirm
  beside the original's screen, whose reference install reads the `american` name tables. Q130c's review: the
  list's builder is `FUN_0049b5b0`, which adds rows in slot order with the slot as the row's key; the pool here
  keeps no slots (`ParkStaffPool.Candidates`), which matters if the order turns out to be the slots'.
- [ ] **Q132. Guests and rides take their turns on the game clock over eight, where the original hands them
  `mGameTick`. Decode first.** Found by Q68b. `ParkPeople.OnUpdate` hands `Peep.Tick`, `PeepBehaviour.Step` and the
  rides' turns `GameClock.Ticks / 8`, which runs from the program's start and is not reset on entering a park
  (`PeepBehaviour.Step`'s `tick` note); the original's handlers read `mGameTick`, which `ParkState.GameTick` now
  carries from the save's 755. The needs share `(id & 3) == (tick & 3)`, the behaviours' time stamps and the chooser's
  tie on `mGameTick & 1` (`ParkRideChooser.Beats`) turn on it. Decode which of them read `mGameTick`, then pass the
  park's clock, as Q82b did for the staff. Confirm: a saved guest's stamp read against 755, in the `peeps` census,
  predicted first.
  Q82 found the guests' needs gate reads `mGameTick & 3` (`FUN_00501650`, `0x00501669`), as `ParkPeople.OnUpdate`'s
  thing-tick note says.
  From Q126: a dropped sweep makes the handed tick jump (n, n+1, n+2, then n+8 after a 64-tick frame), so the
  guests of one needs slot miss a turn the original's `mGameTick` gives them on its next sweep, and stamps taken
  from it jump five sweeps.
- [ ] **Q133. The mechanic, the handyman and the entertainer stand once their saved walk ends, where the original's
  walk about.** Found by Q82 (`ride-operation.md`, "Leaving idle, or a walk: the choice by kind"). With no work the
  mechanic's `FUN_004da5b0` and the handyman's `FUN_004d7100` take a random walk every time (`0x004da6fa`,
  `0x004d712d`), and SetState(0) only when none is found; the entertainer's `FUN_004d46d0`, after a draw mod 3 and no
  guest within `ActivationDistance`, takes the guard's `mGameTick & 3`. `StaffBehaviour.Decide` stands all three, and
  none of their searches is counted (`CLAUDE.md` rule 4): a broken ride, litter, a loo, guests to perform to.
  From Q100: the handyman's loo search and his states `0xa` and `0xb` are decoded (`ride-operation.md`, "A toilet's
  dirt"); building them belongs with this decide.
  Build the no-work walk and count each search where the original makes it; every kind's decide calls
  `FUN_00506a40` first (`0x004da5b8`, `0x004d7108`, `0x004d46d5`), as `StaffActivity.Idle` says. `Decide`'s summary
  and the class remarks already say the original's three walk about (`e0462c9`). Confirm: all five staff walking in
  a timed run, the `staff` census and `unimplemented`, photographed.
- [ ] **Q134. The researcher researches, where ours stands. Alexah's call first.** Found by Q82. On a nought from its
  draw, or no destination, the researcher takes state `0xf` (animation 10, `+0x214` = mGameTick) for
  `ResearcherConstsPerGrade.WorkDuration` + 1 sweeps (31 at grade 2), then walks or researches again; it never idles of
  its own accord, and every 20 sweeps it adds `ResearchAbility` to the lab (`0x00502984`). OpenTPW has no state `0xf`,
  so the fourth decide stands. Research waits on a research system this game lacks (`docs/PLAYER-GAPS.md`), but the
  state and its timer need no lab: ask whether to build that half now, and count the points meanwhile.
  - **Note (fork review, 2026-09-30), data for the build:** the global `Standard.sam` holds Research Effort by category (100, 15, 30,
    15, 10) and `ResearchTech`; `Easy_Standard.sam` overrides categories 3 and 4 (25 and 0). `Upgrades[n].CostOfResearch`
    is research points; `DurationOfUpgrade`, only in the category files, is the mechanic's upgrade time. In Instant
    Action the research screen shows UITEXT `0x1d4`, "research is automatic". Why the screen has six sliders for five
    categories is not decoded: read how the lab spends points by category first. Review items economy-1, economy-v3.
- [ ] **Q135. The staff's sounds are neither played nor counted.** Found by Q82. Every idle and walking turn draws
  the world random and on one in sixteen plays a cat_staff effect at the member's position (`FUN_004faa00`): idle
  `0xa1`, `0xa3`, `0xa5`, `0xa7`, `0xa9` and walking `0xa0`, `0xa2`, `0xa4`, `0xa6`, `0xa8` (handyman, mechanic,
  entertainer, guard, researcher), `0x8a` a researching turn; and with no draw `0x87` a performance's end, the guard's
  `0x88` (`Oi.mp2`) as a chase starts and `0x89` on a catch, both waiting on the chase, itself unbuilt. Count them first
  (`CLAUDE.md` rule 4); then decode each effect's samples and build. What a sample says is known only by listening.
  Confirm: `voices` and `unimplemented` over a timed run.
- [ ] **Q136. Six small differences in the staff's decide.** Found by Q82 (`ride-operation.md`, "Drawn on the way").
  (a) Tired is `(u8)trunc( rest ) <= RestLevel`, signed and inclusive (`0x00506b41`); `StaffBehaviour.Decide` tests
  the float `< RestLevel` and misses [1, 2). (b) The patrol roll `FUN_00506f30` takes only a path cell (`mType` 1,
  `FUN_00536310`) before it routes; **built in Q206**, with queue/approach wandering containment. (c) Not
  tired, `FUN_00506a40` sets the speed word `+0xc0` from the rest byte (60 to 140, `[0x0075c7f8]`), one of
  `FUN_004fa870`'s three terms. From Q177d: how the terms reach the walk is decoded (`ride-operation.md`, "Where a
  WALKING peep is drawn") and every person's four speed words are read (`ParkWorld.PaceState`); guests are eased
  (`Peep.Pace`), staff keep the saved speed and a hire a rested member's 1.4. The build is `Pace` for staff, with this
  base by rest.
  (d) Tired with no rest area found or reached, `FUN_00506a40` answers 0 and the kind's own choice follows (the guard's
  at `0x004d6554`, the researcher's at `0x00502b9f`); `Decide` stands them instead, so a tired member with no reachable
  Staff Room never walks again. (e) At the end of a rest the original runs the kind's decide in the same sweep
  (`FUN_005061d0`, `0x00506298`); `Rest` sets Idle at stamp 0 and decides a sweep later, which after Q82b reads the
  guard's `mGameTick & 3` a sweep late. (f) Found by Q82b: at hire the guard and the researcher decide at once, after
  `FUN_00506a40` (the guard's `0x004d5e76` on `mGameTick & 3`, the researcher's `0x005026cb` on a draw); `Hire` sets
  Idle at stamp 0, so they decide a sweep later. Confirm each in the `staff` census.
- [ ] **Q137. A guest going home under the `facing` overlay crashes the park.** Found by Q82's first run: an
  `IndexOutOfRangeException` in `ParkGuestSprites.Collapse` in the frame guest 37 went home. `Remove` rebuilds the
  vertex array at two quads a person, and the draw after it collapses every quad up to the last frame's `_uploaded`,
  which with the overlay's dash was two a person of the crowd before: past the new end. Without the overlay it waits
  for more than half the drawn crowd, staff included, to go before one draw, one going to none being the single case.
  Confirm: `facing 1`, a guest sent home with `depart`, the park still drawing, photographed.
- [ ] **Q138. The staff strike is neither built nor counted. Decode first.** Found by Q82's review
  (`ride-operation.md`, "Drawn on the way", the strike). `mStaffHQ`'s month handler `FUN_00508e70` runs every month
  the park is open: for each kind with staff it clears a set flag `[HQ + 0x28 + kind × 12]` or calls `FUN_00508f70`,
  which returns until the date passes 24 months; past that `FUN_00509360` raises the level, and levels 1 to 4 set the
  flag and post the warnings. Every decide opens with `FUN_00506a40`'s strike arm (the flag and the gate's
  `VAR_STATUS`), a walk to the strike area in state 4; state 5's `FUN_00506300` ends it. OpenTPW has none of it,
  counts none of it, and reads nothing of the save's model-9 record (`mForceStrike`, `mStrikeLevel[i]`). Decode the
  reach first: the epoch of `FUN_004f8800`'s 24-month gate. Count the monthly consideration where `FUN_00508f70` is
  reached (`CLAUDE.md` rule 4). The flag is `mStaffHQ`'s own, and the arm's one script read is the gate's status,
  which `ParkRides.GateStatus` answers, as `StaffBehaviour`'s class remarks say (`e0462c9`).
- [ ] **Q139. Six more reached paths only log, and the boot's one sound is unheard.** Found by Q69's sweep, the same
  shape as its seven (`CLAUDE.md` rule 4): the lobby menu's Go Online (`FrontEnd`, only logs); the park menu's Load Game,
  Save Game and Publish Park (`ParkFrontEnd.NotYet`, only logs); and R, research's shortcut, bound and consumed by
  nothing (`InputButton.Research`; the original's shortcuts row 11 runs `0x0040c5b0`, a thunk to `FUN_004aa480`, as
  read by the sweep). `Boot_Init` also plays `cat_ui` effect `0xd2` after sound starts (`docs/exe/boot.md`, step 4),
  which nobody has listened to: listen first, then count it or build it. Count each where it is reached. Confirm: the
  `unimplemented` census after choosing each.
  The sixth is the gadget's postcard (the 2026-09-26 staleness audit): `ParkGadget.SendPostcard` (`b_postcard`,
  `FUN_004a9380`, which pauses the game and writes a picture out; the game ships `Postcard.wad`, `postcard.jpg` and an
  HTML template) only logs and closes the arm.
  From Q114b: Ctrl+P under the full-screen view reaches the same `FUN_004a9380` and is counted
  (`FULL_SCREEN_VIEW_POSTCARD`).
- [ ] **Q140. The camcorder walks onto entrances the original shuts. Decode first.** Found by Q69's sweep. The
  original's edge test `FUN_004d8750` in mode 2, stepping into a type-9 cell, reads the entrance's owner chain
  (`0x004d8883`-`0x004d8b28`, as read by the sweep, not checked): it shuts the step unless the first catalogue object's
  ride has a view (`FUN_0042a440`: a coaster handle, a script camera, or a model node flagged `0x1000`) and its
  `UsageInfo.CannotRide` is nought, and opens it if both hold, whatever the neighbour bits say. `CellEdge.For` declines
  this (`queueAhead` always answers nothing there), so our viewer walks onto the Staff Room's, the toilets', the Drinks
  Shop's, the Jungle Spray's and the bin's entrances. Decode `FUN_0042a440` and the arm, then build the gate. Whether the
  original's viewer reaches the Belly Bounce's entrance by walking rests on `FUN_0042a440` for its model.
- [ ] **Q141. Golden tickets are never awarded. Decode first, and Alexah's call on when.** Found by Q69. What reaches
  the advisor's glints in the original is a golden-ticket award: only gesture rows 1 and 13 carry the glint flags, and
  only its lines use them (`docs/exe/scenes.md`, "Gesture table"). Those lines play on the park's own advisor, model slot
  1 from the level's `advisor.wad` with clips 16 to 20, which OpenTPW does not load. The sweep also read that a row whose
  first clip is not 14 skips the lead-in (`FUN_00598bf0`, not checked). No site reaches any of it, so nothing is counted.
  - **Note (fork review, 2026-09-30):** the automatic awards run only in game type 0 (`FUN_004d4a00`'s gate covers all eleven
    tickets), so in Lost Kingdom's Easymode, type 2 (Q178's note), the original never awards a ticket by play; this
    waits for a Full Simulation park (Q197). The cheat table can still award one in any type (rows
    `0x0040c6c0`..`0x0040c7e0` call `0x005af810`/`0x005af940`), behind the cheats byte the `CHTS` module loads, which
    Easymode ships set. The checker's decode is Q194's. Review items economy-8, gap3-8.
  - **From Q177c:** the buy list and its click read the same list of ticket-bought items as the purchase, and neither is
    built nor counted: a row whose item has a `GoldenTicketCost` not yet bought with tickets is named "??? Mystery
    Ride! ???" with its negated ticket cost for a price (`0x004ab02e`), and a click on it is refused when the player
    holds too few tickets (`FUN_004d4b90`, `0x004ac5a9`). The purchase's own arm is counted
    (`PURCHASE_GOLDEN_TICKET_ARM`). Lost Kingdom's `tourride` (1) and `volcano` (3) carry a ticket cost.
- [ ] **Q142. The staff pool reads a string table again for every name.** Found by Q70's game run. `ParkStaffPool`
  opens and parses a name table for each candidate it rolls (`RollName`) and STAFF_TYPES for each kind's name
  (`NameOfKind`): 27 of a park load's 28 table reads are six files read over and over, and the staff screen and the
  `candidates` census read STAFF_TYPES once a row. Read each once, as `Localization` does. Confirm: `park jungle` logs
  seven `String table:` lines (THEMENAMES too), two of them `35 strings in 904 bytes` (ENTERTAINER_NAMES and GUARD_NAMES alike), and the
  `candidates` census and the hire screen show the same names as before.
- [ ] **Q143. `BFSTReader` takes a string's length from one byte, and four shipped strings are longer.** Found by
  Q70's review. A record is `01`, a length of at least two bytes, little-endian, in a three-byte field, then the
  characters (FileFormats `strings.md`, on `docs/format-corrections`);
  `ReadFile` reads the length's first byte and skips the other two. Of the 4,730 records in the 42 shipped files, four
  are over 255, all in UITEXT.str: row 400 (`UIStrings.Change`, 262 characters in English, which reads "CHANGE"; 256 in
  american, which reads empty) and row 417 (`SoftwareCopyright`, 615 and 614, cut to 103 and 102). Nothing reads either
  row yet, so no screen shows the cut. Read all three bytes; the reader's copy of the layout, which says "3 bytes" and
  "Unknown", becomes a pointer to `strings.md`. A test on rows 400 and 417.
- [ ] **Q144. Three hollow ride-script tests.** Found by the 2026-09-26 staleness audit, whose comment fixes now say
  what each asserts. `RideScriptWalkTests.AWalkIsCollectedOnlyOnceItHasFinished`: its second script ends on its first
  turn, so the stepper never runs and neither `WALKGET` is checked. `MorePeopleThanLanesCannotAllBeWalkedOn` asserts a
  floor, not the ceiling its name claims. `RideScriptRunTests.WhatIsNotImplementedIsCountedRatherThanGuessed` asserts a
  count is not below nought, which cannot fail. Make each assert its claim, and put the bug back to prove it (`CLAUDE.md`
  rule 6). No game run.
- [ ] **Q145. A fee set on the entry-price screen never reaches the gate.** Found by the 2026-09-26 staleness audit.
  Guests judge and pay `ParkAdmission.Fee` (`PeepBehaviour.Judge`: `OpinionAt`, then `State.Take( admission.Fee )`),
  which `ParkPeople` captures once from the save's economy thing. The screen's plus and minus move
  `ParkState.AdmissionFee`, which only the screen and the `money` census read, so the price shown and the price
  charged part the moment the player changes it. The remark over `ENTRY_PRICE_REJUDGE_WAITING_GUESTS` ("The gate reads
  the fee through ParkState") and `SetAdmissionFee`'s remark become true when the judgement and the charge both read
  `ParkState.AdmissionFee`. Confirm: the fee raised by five on the screen, `money` showing it, then the next guest
  through the turnstile, `money`'s takings up by the new fee, predicted first; a screenshot.
- [ ] **Q146. Every guest who arrives is counted twice as a visitor.** Found by the 2026-09-26 staleness audit.
  `ParkPeople.Admit` calls `ParkState.Admit` when the vehicle drops a guest and throws the answer away, and the
  `Entering` case calls it again when they come through the gate and keeps it as `Peep.VisitorNumber`. The original
  moves `mNumberOfVisitorsToDate` in one place, a guest finishing `Entering` (`FUN_0051aaf0`), as `ParkState`'s
  `VisitorsToDate` and `PeepBehaviour.VisitorsToDate` both say. Take out the arrival's call. Confirm: `arrive` twice,
  both guests through the gate, the visitors screen's numbers for them one apart, predicted first; a screenshot.
- [ ] **Q147. `MP2File.Duration` reads every sample as MPEG-2, and five shipped samples are MPEG-1.** Found by the
  2026-09-26 staleness audit. The getter never reads the header's version bits (`(SoundData[1] >> 3) & 3`) and always
  takes the MPEG-2 bitrate tables. By the audit's census of all 47 `.sdt`, five entries are MPEG-1 Layer I (header
  `ffff22c0`, bitrate index 2, 64 kbit/s read as 48): `keyexplode` in global `SfxHD.sdt`, and `tp_riderepaired`,
  `tp_sideshowwin_`, `mortar_2` and `mortar_3` in global `RideHD.sdt`. Their lengths come out 4/3 too long,
  `SoundCategoryFile.ReadSamples` turns away every map record that names one (its class summary says so), and the
  effects after it in its category are shifted along: by the audit's reading global lobby sfx 4 (the park entry) and
  global rides 203, 211 and 212 play nothing, and rides 202 the wrong list. Read the version and take MPEG-1's tables
  for it; the two `SoundCategoryFile` remarks that rest on the lengths ("every sample in every bank", "26 to 83ms")
  change with it, and the version split goes to FileFormats `sounds.md` (rule 14). Q43's structural walk is the other
  half, for the variations with no samples. Confirm: a test over every shipped bank that each entry's length agrees
  with its decode, `ReadSamples` and `ReadVariations` agreeing in both global categories, and the park-entry effect
  heard by capture.
- [ ] **Q148. The music and the happiness gauge count guests the original leaves out.** Found by the 2026-09-26
  staleness audit. The original's crowd count behind the music (`FUN_004c7fa0`) and its happiness average both take a
  guest only where `FUN_004fa990` passes, and that is decoded: it packs the guest's cell from bytes `+5` and `+7`
  (`0x004fa994`) and passes when the cell's kind is 0, 1, 3, 9 or 10 (`FUN_00536390`, read first-hand, and
  `FUN_00536310` to `FUN_00536350`, `hud.md`, "The cell codes"; `park-engine.md`, the PICK UP button), so a guest on a
  ride's footprint (kind 4) or outside the park is left out. `ParkAudio.CrowdLevel` and `ParkPeople.AverageHappiness`
  count every guest, and `CrowdLevel`'s remark reads the test as a dword at `thing + 8` whose meaning is not
  established, which the disassembly does not bear out. Build the filter once for both, and for `HeadingForExit`'s
  change of mind when it is built. Confirm: the Belly Bounce full, the music level and the gauge read before and
  after, predicted first; a screenshot of the gauge.
- [ ] **Q149. The calendar keeps its own game tick, from nought, and makes up the advances the original loses.** Found
  by the 2026-09-26 staleness audit. The original's calendar counter is `mGameTick` (`+0x1da70c`, `weather.md`, "The
  calendar"), which a loaded save sets to its own (755 in `Easymode.TPWI`, as `GameCalendar.Rebase` says);
  `GameCalendar.Counter` starts at nought beside `ParkState.GameTick`, which carries the 755, so the date and the
  weather's days run about 33 days behind the original's. And `GameCalendar.Update` adds at most three advances a
  frame but carries the rest to later frames (`wanted - Counter`), where `0x0054f680` drops them for good, as
  `LongestCatchUp`'s own remark says. Check that the published calendar (`0x007ced58`) adds the counter to
  `mFunnyTimeStart` alone, then drive the calendar from `ParkState.GameTick`, so Q126's cap holds the date too.
  Confirm: the gadget's date on entering Lost Kingdom, predicted from 755 advances; a screenshot.
  Q165c gave the ride score and the object window's Age the original's calendar (`ParkState.CalendarNow`, from the
  save's `mGameTick`); the gadget's date and the weather's days still count from nought.
  From Q177b: the objects' day rings roll on `GameCalendar.DayRolled` too, so their days turn about 18 world ticks
  later than the original's after Lost Kingdom's load (its first changes at ticks 761, 784, 807), and a load never
  rolls on its first tick where the file's `mDayAtLastUpdate` differs from the loaded date.
  From Q198: the month's change comes to the bank and the staff on `GameCalendar.MonthRolled`, so here on 2/1/2000,
  177 s in, where the original's first after the load is at tick 1383, 3.1 (measured). A load reads the month and the
  day but not the year, which keeps the entry's seed, 2000 (`weather.md`), so a park saved in another year gets `0xd`
  on its first sweep and zeroes `mProfitThisYear` (decoded, not measured).
  From Q126: the thing sweep is capped at three a frame now and `ParkState.GameTick` with it, so after a 2 s stall
  `GameCalendar` (which carries its advances over) runs five ahead of `ParkState.CalendarNow`, and the weather's
  tick and the day's, month's and year's work with it; the original drops all of them with the sweep
  (`FUN_004d7b20`, `FUN_00512880`). `LongestCatchUp`'s remark ("loses time and can never gain it") is not what
  `Update` does.
- [ ] **Q150. Scripts and the thing sweep take a frame's ticks in two loops, where the original takes both per tick.**
  Found by the 2026-09-26 staleness audit. The original's park loop runs the scripts (`0x0054f56b`) and the thing
  sweep (`0x0054f7bb`) inside one loop over the frame's ticks. `ParkRides` and `ParkPeople` each loop over
  `GameClock.TicksDue` on their own, rides first (`ParticleSystem` is a third), so on a frame with two ticks due every
  script takes both before any guest takes one. `GameClock.TicksDue`'s remark says this must become one loop once
  guests react to what rides did in the same tick, and they do now: a shut ride turns its queue away (`c54e848`). Run
  the park's systems from one loop, one tick at a time, in the original's order, with Q126. Confirm: a long frame
  forced, the log's ride and guest lines interleaved tick by tick.
- [ ] **Q151. A park's sky draws the lobby's horizon band. Decode first.** Found by the 2026-09-26 staleness audit.
  `FUN_005863c0` gives the band's four rings, per half, 0.99, `c08` + 0.5, `c0c` + 0.5, `v` + 0.5 and then 0.49,
  `c08`, `c0c`, `v`, with `c08`, `c0c`, `v` 0.34, 0.17, 0.01 (`0x00701f88`) by default and 0.19, 0.01, 0.49
  (`0x00701f94`) while `DAT_008bcbc8 & 0x2000000` is set (read first-hand). `Sky.UpperV` and `LowerV` hold the second
  set, 0.99, 0.69, 0.51, 0.99 over 0.49, 0.19, 0.01, 0.49, which is the lobby's, and `Sky.Band` draws it for every
  sky; `_tintRamp`'s remark, "the whole difference between the two skies", is true of the code only. Settle which
  scene sets the flag (it also gates a mirrored copy of the dome), write the draw to `docs/exe/`, then give a park the
  default set, 0.99, 0.84, 0.67, 0.51 over 0.49, 0.34, 0.17, 0.01. Confirm: a camcorder frame of Lost Kingdom's
  horizon before and after.
- [ ] **Q152. Every lightning bolt lasts 0.42 s.** Found by the 2026-09-26 staleness audit. `Lightning.Duration` is a
  constant, and neither `LobbyWeather` nor `ParkWeather.PlaceBolt` passes a lifetime to `Strike`. The original's bolt
  draw takes one (`FUN_00580320`, argument 7, in milliseconds): 500 in the lobby and `LightningTime` in a park, 2000
  in the balance file, which nothing here reads (`weather.md`, "The bolt"). Pass each scene's own. Confirm: `bolt` in
  the lobby and in Lost Kingdom, frames grabbed every 100 ms, the bolt up for 0.5 s and 2 s.
- [ ] **Q153. Editing a queue parts from the original's walk twice.** Found by the 2026-09-26 staleness audit.
  `ParkPathBuilding.EditQueue` and `QueueEnds` both cite `FUN_00530120`, and they differ. With no link on the entry
  cell, `EditQueue` steps along the entry cell's raw `Direction` byte, where the original takes the side the thing's
  angle names (`0x005303f1`), as `QueueEnds` does with `AngleSide`; and its no-queue arm cuts nothing, where the
  original takes the path's bit when the entry cell faces a path directly (`ride-operation.md`, "The sale's drain",
  the list). Neither is said at the site. Build `EditQueue` on `QueueEnds`' walk. Confirm: a Belly Bounce bought and
  turned, its window's queue button pressed before any queue is laid, `tool` and `cell` on the anchor, predicted
  first; a screenshot.
- [ ] **Q154. The queue retile does not ask the track layer.** Found by the 2026-09-26 staleness audit.
  `ParkPathBuilding.PathLinks`, the original's `FUN_00535dd0` counting a queue cell's mutual path links, leaves out
  its fourth test, `FUN_0053ad20` of the track record beside the cell, and counts `QUEUE_TILE_TRACK_FLAGS_GATE` on
  every link instead. The save's track layer is read now (`ParkWorld.MapCell`'s `Track` fields), and
  `CellEdge.TrackCloses` answers `FUN_0053ad20` for the walk. Whether the retile's test is exactly `TrackCloses`,
  which also applies `TrackCounts`' five types (`FUN_005363f0`), is not established: read the call first, then ask it
  and write it to `park-engine.md`. Confirm: the Belly Bounce's queue relaid, `cell` on each queue cell and the
  counter's absence in `unimplemented`, the tiles predicted first.
- [ ] **Q155. Every script's speed word is 50, where a placed thing's is its own. Decode first.** Found by the
  2026-09-26 staleness audit. The object constructor pushes the item's operating speed into its script (`FUN_0055a300`
  at `0x004db534`), and the ride window's speed slider writes it (`FUN_004dd6e0`); `docs/exe/park.md`, "The clock, the
  speed word, and WAIT". `RideScript` keeps no speed word: `WAIT`'s divisor is counted as `RIDE_SPEED_SCALES_WAITS`,
  and `ParkAudio.ScriptSpeed` is a constant 50 in `ScreamVolume` and `ScreamGridIndex`, where the speed is half the
  answer. `ParkScreamTests` calls the Belly Bounce's 35 "the real park's number", which holds only if its speed is 50.
  Decode what a loaded object's script holds (the constructor's push from descriptor `+0x1a8`, and whether the save's
  `mOperatingSpeed` reaches the script after it), measure the Belly Bounce's, then keep the word per script and read
  it in `WAIT` and in both scream readings. Confirm: `rides` showing the Belly Bounce's word, and the scream's level
  by capture.
  From Q166: every trigger hands the divisor (`[ESP+0x14]`, 0.5 + 0.01 × speed, `0x00551cdc`) to the channel as its
  play rate - `TRIGANIM` `0x00552952`, `WAITANIM` `0x00552a9f`, `LOOPANIM` `0x00552be4`, `TRIGWAITANIM` `0x00552d2f`,
  `TRIGANIM_CH` `0x00553190`, `WAITANIM_CH` `0x00553325`, `LOOPANIM_CH` `0x00553465`, `TRIGWAITANIM_CH` `0x00553624` -
  where `StartAnimation` passes 1.0 (said at the site); the deadlines of `TRIGANIM` `0x005529ac`, `WAITANIM`
  `0x00552acd`, `TRIGWAITANIM` `0x00552d94`, `TRIGANIM_CH` `0x005531ee` and `TRIGWAITANIM_CH` `0x00553682` divide by
  it, and none of these divisions is counted (`RIDE_SPEED_SCALES_WAITS` fires only from the ride window). The unbuilt
  `TRIGANIMSPEED` passes its rate / 1000 × the divisor (`0x00552f6e`), and its deadline, the length × 1000 / its rate,
  does not divide by it. The length a trigger answers does not depend on it (`FUN_00472f60`,
  `0x0047323e`..`0x00473258`).
  From Q174: `+0xe4` is the play rate in thousandths. After every turn of a script with a model the scheduler writes
  `(0.5 + 0.01 × speed) × +0xe4 / 1000` into every channel's queued speed `+0x30` (`0x00551789`), which a promotion
  and a loop's replay start at; OpenTPW's promotion keeps the speed the clip was queued at. `TRIGANIMSPEED` leaves its rate
  there (jungle `Gates` 4000). The Easymode Belly Bounce was saved looping at 1.1, and OpenTPW's second trigger at its
  word 43 (`park.md`, "Where OpenTPW's animation state parts from the engine's", difference 4) replaces it at 1.0. With a divisor other than 1, a clip under 300 ms or no model makes
  `WAITANIM`'s qword sum wrap `+0xa0` (`park.md`, "`WAITANIM` is NOT `TRIGANIM` with a wait").
- [ ] **Q156. The staff screen's two happiness meters fill upward. Decode first.** Found by the 2026-09-26 staleness
  audit. `UiMeter` fills every meter from the bottom, a choice its remarks justify by the gadget's gauge housing being
  taller than wide (59 by 224); the staff screen's two `happygrad.wct` meters are 376 by 45 (`ParkStaffScreen`,
  `0x32d` and `0x32c`). What paints a meter is the two sub-objects `FUN_0066d885` hands the value to (`+0x98`,
  `+0x9c`, their vtable `+0xc`), untraced. Trace their geometry, write it to `hud.md`, and fill each meter the
  original's way. Confirm: the staff screen at a known happiness, photographed.
- [ ] **Q157. Guests' and rides' reached arms that count nothing.** Found by the 2026-09-26 staleness audit. Each is
  reached in Lost Kingdom, unbuilt, and calls no `Unimplemented.Report` (`CLAUDE.md` rule 4). In `PeepBehaviour`:
  `Entering` lacks `FUN_004ffb20`'s guard (park shut or gate not open, back to the gate), reachable since the
  entry-price door shuts a running park; `GoingToRide`'s stuck arm (`BigHappinessChange`, event 3) and its park-shut
  arm, both built by Q102; `AtGate` asks nothing of `FUN_0051a760`, the arrival vehicle's gate; `HeadingForExit` has no
  change of mind (`FUN_00500a50`), which a saved guest can take; states 19 and 21 stand silently, 19 on every
  departure and 21 after `WalkingOutside`, which Q128 builds; `Decide` did nothing when the chooser finds nothing,
  built by Q107; and `Judge` reads `ParkExcitement`, nought until Q26 decodes `FUN_004c8240`. `CellReroute` never
  runs `FUN_005108a0`'s diagonal pass after a scan that splices nothing, which is how most routes end.
  `CellEdge.Blocked`'s mode-2 entrance arm always answers nothing, whenever the camcorder walks at an entrance, which
  Q140 decodes. `Peep.Tick` leaves out the cell's `RegionFX` term (`FUN_00501650`) on every needs turn.
  `ParkRideOperation` makes no breakdown request and sets no worn flag (`ride-operation.md`, "The first half of the
  turn"), `Invite` reads no `RunsContinuously` (`+0x33` bit 0), and nothing wrote `mPreviousRides` (`+0x1e0`) - Q165c
  built that. `ParkRideChoice.CanBeOffered`'s coaster arm (`FUN_00441970`)
  goes uncounted where `MayOpen` counts `OPEN_GUARD_COASTER_TRACK_RECORD`. Count each where the original tests it, and
  put each build without an item in the queue. Confirm: `unimplemented` over a timed run, each name present, predicted
  first.
  - **Note (fork review, 2026-09-30):** `EntertainerConstsPerGrade[g].HappinessEffectOnCell` (4, 8, 12, 16, 20; `Standard.sam`)
    has no reader in our decode; find it before building the entertainer's half of the region term. Review item peeps-v3.
  From Q126: `FUN_0055a470` (`0x0054f828`, every eighth step, a dropped sweep's too) finds the ride the camera is
  on (`FUN_0042a4e0`, the list at `[0x008791b0]`) and hands its `+0xd8`, or the default `[0x0078578c]`, to
  `FUN_0051c8d0`, a sound setting not decoded. Nothing here has it, counted or not.
- [ ] **Q158. Five park-screen readouts are stand-ins, and nothing counts them.** Found by the 2026-09-26 staleness
  audit (`CLAUDE.md` rule 4). `ParkObjectWindow`'s Age row prints a bare number: its wording, `FUN_006acd60` with
  `0x1b1`, is not decoded. Its scrap row prints the build price, where `FUN_004e2400` scales it by the age bucket's
  percentage, `SCRAP_VALUE_DEPRECIATION`, counted only when something is sold. `ParkEntryPriceScreen`'s heading
  `0x4f3af` is empty, and its remark says "left empty and counted"; the original titles it with the name of the thing
  at world `+0x1da732`, `mParkGates` (`ParkWorld`), and its help row 191 reads "Park name - left-click to edit", so
  the title is the park's name and its edit is Q28's. `ParkGadget.ShowDate` prints the machine's short date in place
  of a format not read back. `ParkBuyScreen`'s description panel `0x1ea` draws its frame and no preview. Count each
  where it is drawn. Confirm: `unimplemented` after opening a ride's window, the entry-price screen and the buy
  screen.
  From Q188: the buy screen's panel is the object window's preview (`ParkObjectPreview`, which wants a placed thing
  today: give it an item); in the original it is (437,162) 360 by 363. The footprint picture at its lower left is
  built (Q233b) and is drawn in front of the model; the name row `0x1ec` above it is not built.
- [ ] **Q159. `SdtArchive.GetFile` matches a truncated name the wrong way round.** Found by the 2026-09-26 staleness
  audit. It tests `x.Name.StartsWith( name )`, the stored name against the one asked for. A `.sdt` name field is 16
  bytes and a longer name is cut to fit, so asking for "TP SCREECH 11.mp2", stored as "TP SCREECH 11.m", never
  matches, which is what its summary says `StartsWith` is there for; and "TP SCREECH 1" would find "TP SCREECH 11.m".
  Jungle's `LobbySfxHD.sdt` and `AmbientHD.sdt` store eight such names. No caller opens a `.sdt` entry by name yet
  (the archive handler `Game` registers reaches it through `OpenFile`). Match an equal name, or, for a stored name
  that fills its field, the request's prefix of that length. A test on the eight. No game run.
- [ ] **Q160. Two park tests do not test what their names say.** Found by the 2026-09-26 staleness audit.
  `ParkFootprintOccupancyTests.LeavingACellTheThingIsNotOnKeepsWhoeverStandsBehindIt` passes whatever `LeaveCell`
  does: `LeaveCell( 58, 15, Ride )` already unlinks the ride from (58,16)'s list, `LeaveCell( 58, 16, Guest )` then
  leaves its `Occupant` nought, and `ParkPicking.ThingOn` falls back to the cell's parent and answers the ride anyway.
  Assert the cell's `Occupant`; if it then fails, check `FUN_004d9280` before making `LeaveCell` ignore a thing not on
  the cell asked about. `ParkRideJoinTests.NoQueueGrowsPastTheRoomItHas` takes the room as the save's
  `QueueSizeInCells` times four, nought on the Drinks Shop and the three toilets, where the gate measures their walked
  cells (`ParkRideChoice.QueueCellsFor`); a run that queues a guest at a toilet fails it falsely. Use the walked
  count. Put the bug back for each (`CLAUDE.md` rule 6). No game run.

- [ ] **Q210. Hoardings: the original's terrain rectangle and its base-vertex gate.** Found by the 2026-10-04 review
  (`u6-hoardings-1`, `-2`, `-4`; `docs/exe/ride-hoardings.md`). `ParkRideHoarding.Ground` samples `x0 + gx` where the
  original samples `x0 + trunc( gx * (w - 1) / w + 0.1 )` inside the model's inclusive rectangle (`+0xc0`, `+0xc4`,
  `FUN_00467030` `0x004670c1`, `FUN_00454190`); counted `HOARDING_TERRAIN_RECTANGLE`. `ParkObjects` gates on
  `Nodes.Count > 1` and reads `Meshes[0]` where the original counts base vertices (`FUN_00469a80`, `0x0046a838`) and
  reads the mesh with the most (`0x0046a82c`). Port both, and write the test's expected value from the executable's
  formula, not from the code. Confirm: a ride bought on sloping ground, closed, photographed with `hoardings`; then
  the same panels beside the original's under Proton (their height, which rises first, where the sign sits).
- [ ] **Q211. Tests that run the callers, not the helpers.** Found by the 2026-10-04 review: fifteen bugs put back
  left all 1,764 tests green. Nothing runs `ParkBuilding.Buy` (taking `BindOperation` out of `Build`); `ParkObjects`'
  hoarding gate, its saved-slot `Restore`, `ParkRideHoarding.Fill`'s sliding V, the panels' `OrderBy`, the `+ 0.1`
  truncation; `Advisor.PlayParkResponse` (its `Busy` guard, the end clip's length); `SayOnlyOnce`'s `history.Said`;
  `Level`'s `DrawStatus`; `SetupParticles` before `SetupParkEntities`; `FreshPark`'s goods and chance on the two fixed
  objects; the prankery id's `& 0xffff`; the cash's `Math.Max( 0, ... )`; `Players.Load`'s read-only stand-in. And
  `ArchiveDamageTests.EveryInstalledWadEntryStillDecodes` prints its hash and count without asserting either. Write
  each test, put its bug back, see it fail (`CLAUDE.md` rule 6). No game run.
- [ ] **Q212. The ride window's other statuses. Decode first.** Found by the 2026-10-04 review (`u7-ride-door-6`,
  `-7`, `-9`, `-10`). `FUN_00485f60` answers `0xb`..`0x14` (80/100/255) for a ride with a mechanic called (`+0x64`),
  2 (UITEXT 366) for state 1 with nobody assigned and `0x15` (UITEXT 385, "REPAIRS IN PROGRESS") with one, 24 for an
  exit not connected; `ParkClosedStatus` answers 1, 22 and 23 only, shows grey CLOSED for a mechanic call, and
  `ParkObjectWindow` shows BROKEN DOWN from `VAR_BROKEN` with no decode behind it. `OBJECT_WINDOW_EXIT_CONNECTION_STATUS`
  and `QUEUE_VERDICT_CORNER_RULE` are reported every frame a window is open (9,631 and 24,273 in one run): report on
  a change. Confirm: each status photographed beside the original's.
- [ ] **Q213. The status box's lettering is smaller than the original's.** Seen 2026-10-04 with the two side by side
  (Q208): the original's "LINE NOT CONNECTED" fills the striped box in two lines of large type, OpenTPW's is small.
  Decode the font and size `FUN_004ade40` draws with. Confirm: the box photographed beside the original's.
- [ ] **Q214. A fresh Full Simulation park's four gaps.** Found by the 2026-10-04 review (`u1-startup-1` to `-4`).
  `ParkStaffPool.Roll` gives every candidate costume 0 where `FUN_00507600` stores `FUN_00541f70( kind )` at record
  `+9`, so every hired entertainer and mechanic wears bank 0 (`guests`: `bank 0+0` on all five kinds). The land
  boundary (track types 25 and 12, from `Hoardings.sam`) is written into the cells and neither drawn nor counted.
  The catalogue's emitters are registered and the per-item slot table (item `+0x4dc`, `0x0041455a`) is thrown away.
  `FreshParkBoundary.Apply` throws where the original stops at its 64th entry (`0x00532517`), and a test pins the
  throw. Confirm: several entertainers hired in a fresh park, more than one costume on screen; the boundary beside
  the original's fresh park.
- [ ] **Q215. The park advisor's tick: three paths neither built nor counted, and the original never watched.** Found
  by the 2026-10-04 review (`u5-advisor-door-2`, `-3`, `-9`). `FUN_0059a550` polls a metadata row each tick
  (`FUN_0059c680( 1 )`); `FUN_0059b620` posts a message after `Advisor_SayResponse` (`FUN_0040f740`, `FUN_0040fb10`,
  response groups 150 and 151 for the door); `Advisor_SayResponse` answers 0 for a sample of no length; event 10 keeps
  the cooldown stamp. Count each, then build. Confirm: press the door in the original under Proton and hear whether
  he speaks (Q90 predicted silence from the scores and never ran it), then the same in OpenTPW.
- [ ] **Q216. The save-preservation policy. Alexah's call first.** Found by the 2026-10-04 review (`u9-foundation-2`,
  `-5`). OpenTPW will not rewrite a gms.dat or Config.tcf it did not read whole; the original overwrites. A Config.tcf
  of version 0, of another version or with a tail blocks saving the options for good, and nothing on screen says a
  save was refused (rule 11: say it in the game's own style). Decide whether to keep it. Either way, measure the
  stricter `ExpandedMemoryStream.ReadBytes` over all 47 `.sdt`, the 42 `.str` and the saves: a short read it used to
  pad now throws.
- [ ] **Q217. Three leftovers among the park's people.** Found by the 2026-10-04 review (`u3-staff-paths-1`,
  `u4-gate-1`, `u2-arrivals-4`). Staff are kept on paths by a fence the original does not have; its own recovery is
  the no-links arm (`0x004f95c0`, Q112): build that, then ask Alexah whether the fence stays. `ParkPeople` holds two
  versions of `FUN_004fa990`: `InsideGateCensus` reads the position's cell as the original does (bytes `+5`, `+7`),
  `OnACountingCell` reads the occupancy link first; make them one. `ParkCatchUpOrderTests`' row (16, 1) asserts the
  open Q150 defect as the expected result and will go red when Q150 is fixed: say so in the test.
- [ ] **Q218. Five gaps left by the Q208 and Q209 corrections.** Found 2026-10-04 by a second read of those four
  commits, each checked against the code. (1) `RideHoardingTests.TerrainEndpointsFollowTheOriginalNormalizedQuarterTurns`
  still says by its name that it follows the original, while `docs/exe/ride-hoardings.md` says it checks OpenTPW's
  larger rectangle: rename it now, and Q210 writes the real one. (2) `ride-hoardings.md` says `Meshes[0]` is the mesh
  with the most base vertices in every shipped outline; the corpus test reads `Meshes[0]` and never counts them.
  Measure it over all 129 outlines and assert it, or take the sentence out. (3) `ProfilePreservationTests` has no
  profile that reads at `Load` and fails at `Select`: the unreadable one is unreadable before `Load`, the cached one
  is deleted. Write that case (readable, then mode 0, then restored) and put the `CanWrite` revoke back out to see it
  fail. (4) `ParkBoughtClosedTests.ALoadedDisconnectedRideKeepsItsSavedDoor` builds a new ride and rebinds it; no save
  is read. Load one, or name the test for what it does. (5) `docs/exe/ride-operation.md` says the remeasure tail
  `FUN_004de1f0` leaves at once when `+0x68` is non-zero; `ParkRideOperation.ReopenAfterRemeasure` clears
  `AssignedStaff` whatever was decided and cites `0x004de48c`. Read the tail in Ghidra and make the two agree. Also
  two sentences: `docs/exe/saves.md`'s "None of this is the original's behaviour" follows a sentence that says the
  first save is the original's, and `STATUS.md`'s "cached profiles stay read-only" is true of an unreadable file
  only. Put the bug back for each test (`CLAUDE.md` rule 6). No game run.

- [ ] **Q219. Light the models from the normals the file stores. Decode first.** Found by the 2026-10-04 effort
  audit (`r-f1`, `r-c2`). The original's model lighter `FUN_00574660` takes one normal per vertex-order entry from the
  table at mesh `+0x64` (stride 12, `+0x5e` entries); OpenTPW lights every placed model from smoothed normals it
  computes, and `ModelFile.ReadFaceNormals` reads only as many entries as the faces index. Count the table's entries
  across all 2,129 `.md2`, compare the stored normals with the computed ones, then read and use the stored ones. The
  FileFormats models page gets the count. Confirm: one ride and one shop photographed beside the original's under
  Proton, the same sun.
- [ ] **Q220. Tests for the wiring the effort audit found untested.** Found by the 2026-10-04 effort audit: seventeen
  bugs put back left all 1,766 tests green. `Level.Update`'s day roll, month turn (its order: training, the bank,
  wages) and year turn; `ParkRides.Resume`'s `MoveKeptReadings` and `BindNew`'s `RideNodes`; the walk leg's x/z form
  (a leg a hair from a whole unit); the Hot Pot's `ParkBumperBoats.Put` and its drawn heading, the speed word's
  placement wiring, the seating friction 7/10 and the broken ride's 1000, the car's turn, the engine fade's length,
  `TowardsCamera`; `ParkGuestSprites.DrawBalloons` (the bob's rate and write-back); the camcorder's
  `UseFirstPersonPictures` on enter and leave; `ParkObjectWindow`'s 30 days and 4 s; the buy list's features tab.
  Write each test, put its bug back, see it fail (`CLAUDE.md` rule 6). No game run.
- [ ] **Q221. Eight leftovers from the effort audit, each dead by content today.** Found by the 2026-10-04 effort
  audit, each read in the listing. (1) `ParkScriptStates` reads the tick and next handle with `header > dword * 4`; it
  should be `>= (dword + 1) * 4`. (2) `ParkBuyScreen.Listed` applies two of the original's four filters: add the
  upgrade (`+0x284`) and model-failed (`+0x508`) tests or count them (`FUN_004aaf70`). (3) `ParkAudio.ScreamEffectFor`
  silences a negative band; `FUN_00551130` plays `0x48` for one. (4) `RideScript.NextRandom` answers 0 for every
  bound + 1 at or below nought; only -1 divides by nought (`0x00553994`). (5) `ParkItemHeights.Over` floors the
  cell's middle where `0x00452bb0` truncates corner offsets: measure it on raised ground. (6) The original rolls a
  rider's head sprite to its node (`FUN_0044b510`, `0x0044ba04`); `DrawHead` does not: say it at the site or build
  it. (7) `Peep.Pace` works at 24 bits and `ShopCostOfGoods` at 53 under one control word: settle which, the pinned
  18 may be 19. (8) Two docs lines not yet re-measured: `park.md`'s selector table rows 8 and 9 leave out the
  `0x4000` and type gates of `FUN_00544c80` and `FUN_00544e50`; FileFormats `sound-categories.md` says the masks use
  bits 1, 2 and 4 beside 645 speech variations holding 100.
- [ ] **Q222. The original's guests come up empty-handed, and ours do not. Measure first.** Found by Q107's
  comparison with the original (`ride-operation.md`, "Q107"). The same stock Lost Kingdom park left alone for 240 s:
  the original's chooser named nothing 112 times and a thing 72 times (Jungle Spray 27, Drinks Shop 18, Belly Bounce
  16, toilets 11), with at least fifteen arrivals; OpenTPW's named nothing never and a thing 16 times (Belly Bounce 14,
  Jungle Spray 1, Drinks Shop 1), with one arrival (Q26). In the original seven of the empty hands were at (52,29),
  where the Jungle Spray turned guests away fourteen times; here `why` scores the Belly Bounce 10 or more for a fresh guest anywhere inside
  the park. So a score, an offer gate or the clock the chooser reads (Q132) differs, and Q104's and Q105's scores were
  never compared with the original. Read one guest's scores in the original at a known cell (a break in
  `FUN_004fcc30`, or its inputs from memory) beside `why` for the same guest and cell, find the term that differs,
  then file its build. Instruments: `q107/orig/watch.py` and `watch1.log`, `q107/base`.

- [ ] **Q224. A happy guest does not jump on their deciding turn.** Found by Q111 (`ride-operation.md`, "The state-6
  turn, in order", (a)). More than 100 sweeps past `+0x208` and happiness above 80: spot animation 5 and the turn ends
  (`0x004fecd2`..`0x004fecea`). Every part is built (`PeepBehaviour.PlaySpotAnimation`, `SpotHappy`); build it where
  `DECIDE_HAPPY_SPOT_ANIMATION` is counted. The original's stock park made one in 964 sweeps (guest 33 at 83).
  Confirm: a guest at happiness 90 deciding, set 5 for eleven sweeps and no sooner than 101 sweeps again; a screenshot
  of the jump, beside the original's.
- [ ] **Q225. A guest full of litter neither walks to a bin nor drops it. Decode first.** Found by Q111, arm (c).
  Litter 90 or more: the nearest `HoldsLitter` thing within three cells that routes, `MajorDest` and state 9, whose
  turn `FUN_004fff20` walks there, sets the bin's script variable 0 and zeroes the litter; none, litter of a drawn
  type on the cell (`FUN_004d93b0`) and the level zeroed. Decode `FUN_004d93b0` and what a cell's litter is drawn as,
  and check `FUN_004fff20` whole; then build both where `DECIDE_LITTER_BIN_ERRAND` and `DECIDE_LITTER_DROPPED` are
  counted. Confirm: a guest at litter 90 beside the bin at (44,29) walks to it, `peeps` showing litter 0; one far
  from it drops, `cell` showing the litter; a screenshot of each.
- [ ] **Q226. Nobody is sick.** Found by Q111, arm (b). Illness exactly 100 on a third of deciding turns: spot
  animation 7, litter type 7 on the cell, event `0x12`, sound `0xcc`, illness nought, and the turn ends. After Q225,
  which builds a cell's litter. Build it where `DECIDE_VOMIT` is counted. Confirm: `need vomit 100`, a guest sick
  within a few turns, `peeps` showing illness 0; a screenshot.
- [ ] **Q227. Nobody stops to watch the entertainer.** Found by Q111, arm (e). An entertainer on the nine cells
  around a deciding guest, and the nearest one performing (staff state `0xe`): event `0xe`, the guest turned to face
  them, and the turn ends. The original's stock park did it eight times in 964 sweeps. After Q133, which builds the
  performance; counted meanwhile as `DECIDE_ENTERTAINER_BESIDE`, whoever the entertainer is doing. The fireworks half
  (`DECIDE_WATCH_FIREWORKS`) is dead by content in Lost Kingdom. Confirm: a guest beside the performing entertainer
  facing them; `facing 1` and a screenshot.
- [ ] **Q228. A prankster plays no pranks.** Found by Q111, arm (f). Below happiness 15, on a draw under the
  prankery: a stink bomb (litter type 8, sound `0xcf`) on `PeepInfo.StinkbombLikelihood` of a hundred, litter, or
  the balloon of another guest on the cell let go; event `0xf`, a type-`0xe` bus message and happiness up one. After
  Q225. Decode what the bus message's receivers do with it before building. Counted as
  `DECIDE_PRANK_STINK_BOMB`, `DECIDE_PRANK_LITTER` and `DECIDE_PRANK_BALLOON`. Confirm: `need prankery 102` beside a
  guest holding a balloon, the balloon let go; a screenshot.
- [ ] **Q229. A member of staff put down sets off on the next sweep, where the original's stands eleven. Decode
  first.** Found by Q112's measurement of the original (`ride-operation.md`, "OpenTPW takes the arm"). Its guard, put
  down from the staff window's PICK UP, reads state 7 to 0 on the click, 0 to 1 with a destination a sweep later, 0
  again inside that same tick, and walks eleven or twelve sweeps on (ticks 1206, 1207, 1207, 1218; 1732, 1733, 1733,
  1745). `ParkPeople.DropStaff` sets Idle from Carried, which stamps 0, so ours decides on the next sweep and keeps
  walking. What writes the second 0 is not read: the hand mode's uninstall puts a worker it still names down again
  (`0x0046cdc0`, `FUN_00505ea0`), and state 0 from a walk stamps the clock, which would fit. Decode the drop's order,
  then build it. In the second run the walk also stopped on grass at (43,27) eight sweeps in and the no-links arm ran
  again from there: read why. Confirm: `staff` after `putstaff`, `idleSince` and the sweep the walk starts, predicted
  first; a screenshot.
- [ ] **Q230. The gadget's arm jumps out and in, where the original's slides. Decode first.** Found by Q113. The
  arm's handler `LAB_004a14f0` (installed at `0x004a2387`) is a jump table over messages `0xa` to `0x100` working a
  state in the control's own `+0x134`; `FUN_004a25f0` reads states 3 and 4 as "still moving", and `FUN_004a2590` is
  the one way a panel gets onto the arm. Neither how far a step moves nor how long the slide takes is traced. The two
  ends are known: in, the arm's right edge is 645 left of the stream's (`hud.md`, "The arm and the aerial as built").
  Here `ParkGadget.PutArm` sets it out or in at once, counted as `GADGET_ARM_SLIDE`; in, the arm and its end draw
  nothing and the retract button is hidden, where the original narrows the arm and switches the button off. Decode
  the states, the step and its clock, and how a mesh is drawn on a narrowed control; measure the slide's length in the
  original's frames; then build it on `Time.Delta` (rule 10). Confirm: frames every 50 ms of the arm going out, beside
  the original's.
- [ ] **Q231. Under an open park screen: F1, the wheel and the release. Decode first.** Found by Q115
  (`park-engine.md`, "A park screen is open"). The screens' key handler answers a plain F1 let go by calling
  `FUN_005194d0` on the world and `FUN_0059ab50` on what it answers (`0x00488bfb`), not traced; nothing here reads F1 in
  a park. Where the wheel's message goes with the pointer over a screen's body is not decoded: here the wheel zooms
  the camera wherever the pointer is, unless a list or a slider takes it. And an armed tool's click beside a screen is
  taken on the press here, where the original skips the press and commits on the release (`Level.KeptFromThePark`),
  which is `RunBuildMode`'s standing difference. Decode the two, count each where it is reached, then build. Confirm:
  F1 and the wheel over the buy screen beside the original's, read from memory.
- [ ] **Q232. A list opens with no row selected, where the original's opens with one highlighted. Decode first.**
  Found by Q117 (`hud.md`, "A right click on a list"). The original's add selects the first row as a list fills
  (`FUN_0066525c( 0 )` in `FUN_0066403b`), and its visitors list opened with a row highlighted every time: the first
  on one opening, the second and the tenth on two others, so something else moves it (the all-items builders call
  `FUN_00664d2a` and `FUN_00665739` with a thing; the two-second rewrite and the sorted insert are the other
  candidates). `UiList.FirstRow` counts the add's selection and builds none (`LIST_FIRST_ROW_SELECTED`), so a right
  click that misses every row before one is chosen goes nowhere here (`LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED`),
  and `Remove` drops the selection on the `0x100` branch though all five list trees have the bit clear
  (`LIST_RESELECT_PAST_THE_WINDOW`). Decode which row each of the five lists opens on and what the buy and hire
  lists do, then build it. Confirm: the visitors list opened beside the original's, the highlighted row predicted
  first; a screenshot.
  From Q233b: the buy list now selects the row under a moving pointer (`UiList.SelectsUnderPointer`, the flag `0x80`);
  the hire list carries the flag too and does not set it, and the three list screens' trees carry it (`0x291`). With
  no first row selected, the buy screen's panel stays empty until the pointer has been over a row, where the
  original's shows the top row half a second after the list fills.

- [ ] **Q234. One staff test fails now and then in the whole suite.** Found by Q123's gate (and once unnamed at
  Q233's). `ParkStaffBehaviourTests.TheParksSweepHandsTheStaffItsOwnClock` failed 1 of 7 whole-suite runs with the
  game, alone in a worktree, and 0 of 40 runs of its class alone, so something another class leaves behind reaches
  it, or a draw it rests on is not seeded. Again at Q123b's gate, 2 of 9 runs by then: "Assert.IsTrue failed. staff 30
  ended only 2 walks in 400 sweeps" (line 500). Find what it reads that is shared (the world's random, `GameClock`, `Time`), and pin it. No game run.

## B. Docs and comments

- [ ] **Q13. Move `docs/CLEANUP-PLAN.md` into `docs/history/`.** Every item in it is closed. It is still untracked in
  `docs/`, so it exists on this machine only. Commit it under `docs/history/` and list it in `docs/history/README.md`.
  (The STATUS diet landed in `c445844`; `QUEUE.md` and both reviews were committed in `c2ddaf6`.) No game run.
- [ ] **Q95. Two descriptor offsets the compiled `.sam` schema names otherwise.** Found by Q50c's schema simulation
  (`FUN_00401030` over the table at `0x00744b30`), not yet checked against each page's own evidence: `hud.md` calls
  item `+0xC4` `Research.Group`, which the schema puts at `+0x178`, making `+0xC4` `UsageInfo.GoldenTicketCost`; and
  `park-engine.md` divides a capacity by `+0x1a0`, which the schema makes `Upgrades[0].InitDuration` (`+0x198` is
  `InitCapacity`). Settle each against the code that reads it, and correct the page that is wrong. No game run.
  Q165 settled both from the scorer (`ride-operation.md`, "What a thing is worth to a guest"): `+0xC4` is
  `GoldenTicketCost` and `+0x1a0` `InitDuration`; `hud.md`'s `+0xC4` text is still to correct.
- [ ] **Q14. Comment sweep of the cleanup commits.** Replace history-voice comments with what the code does now,
  found by member because line numbers go stale. Left: `AudioListener.AttenuationTo`'s summary nests a `<para>`
  inside a `<para>`, and `ParkCamcorderCameraMode.DebugWalk` steps a per-frame `WalkSpeed / 60f`; make it use
  `Time.Delta` (rule 10). The history-voice sites this item named are done: most went with `e0462c9`, `Walk`'s two
  and `Slide`'s doubled line with `0640abc`, `ParkFixedItems`' remark with `36639d3`, and the IslandPanel and
  LobbyGate sites and the DebugConsole tab with Q41 (`325f102`). No game run.
- [ ] **Q81. The FileFormats docs still disagree with themselves and with the game.** From the clone's 2026-09-18
  audit, as the 2026-09-24 staleness audit found it (`docs/history/fileformats-docs-ledger.md`). Most of it is fixed,
  on disjoint branches, which is the other half: `rsse.md`, `vm/instructions.md`, `sounds.md`, `strings.md` and `saves.md` (whose
  World block is written three times) exist in divergent versions, and the `.md2` format has three pages
  (`models.md`, `md2.md`, `model.md`). `vm/info.md`'s per-script variable ids are fixed on `docs/rsse-instruction-set` (and
  `docs/save-module-chain`) only. Still wrong: `saves.md`'s `mFlags` `0x8` bit, its "version 85" (hex), and "the sixth
  person model is the visitor"; and `wad.md`'s name length, which includes the NUL. Open on both sides: what `0x14E`
  means (`SpriteBankFile`'s note). The Bumper enum is not a page error: the page's 1-17 is probably right, and it is
  `ScriptDefs.cs`, a labelled leftover left as it is, that looks corrupt. Settle each page's one
  version on the branch that owns it (`docs/README.md`, "The FileFormats docs clone"), then fix. No game run; the
  site builds.
- [ ] **Q75. The 2026-09-12 review's Phase F, what is left.** Opportunistic, never alone. Done: `LangVersion` 14.0
  (`4f6da17`), `Terrain.cs` (`2f38ce3`), `ReadInt16` (`f31862e`). Left: `SixLabors.ImageSharp` 3.1.6 in
  `OpenTPW.ModKit.csproj`; `content/textures/test.png`; `.editorconfig`'s `end_of_line = crlf` with no
  `.gitattributes`; and, to LABEL as dead by CODE rather than delete (rule 3), `Public/ModelFile.cs` and
  `Public/MapFile.cs` (both unreferenced), `Render/Primitives/Cube.cs` and `ReadUIntN`. `Singleton.cs` is used by
  `UI/Cursor.cs`, though nothing reads its `Instance`. Left to Alexah's call, not to be started: `.gitignore` for `.claude/`,
  `.vscode/` and `.mcp.json`, ModKit as a whole, and the nullable warnings except in passing.
  Also left (the 2026-09-26 staleness audit): the `Zio` 0.17.0 package in `OpenTPW.Common.csproj`, which no source
  file uses and which carries a low advisory (`DECISIONS.md`, "Package advisories"), and the `PreferredBackend`
  setting in `App.config` and `Settings.settings`, which nothing reads (`Settings.Default` is read for `GamePath` and
  `GameWindowSize` only). Label both as dead by CODE at their sites; remove either only on Alexah's word.
- [ ] **Q161. Stale text in code strings, test names and one census key.** Found by the 2026-09-26 staleness audit,
  whose edits were to comments only. `Extensions.GetColor`'s `[Obsolete( "Use Color.Parse" )]` names a method that
  does not exist, and nothing calls `GetColor` (dead by CODE: label it, rule 3). `RideEffects.Trigger` reports
  `effect type {type} (TRIGGEREVENT)`; the instruction is `EVENT` (`Opcode`), beside `(ADDOBJ)`. The console's `load`
  replies "they come one a tick"; `StepArrivals` admits one a thing sweep. Assert messages: `AnimationEasingTests`
  ("an older note here quoted"), `ParkRideJoinTests` ("only 13 and 14 can be"; six pass the filter),
  `ParkRestAreaTests` ("the original's own other arm", against Q136 (d)), `ParkStateTests` ("the handyman" of thing
  26, the mechanic), `ParkTickTests` ("neither of which this tree had"), `ParkPathBuildingTests` ("the loader's
  runtime flag is a different thing", which `park-engine.md` calls not established), `PeepWalkTests` ("at 31ms a
  tick"; a step is a thing sweep), `RideAnimationsTests` ("this branch"), `PeepNeedsTests` ("nothing else makes a
  guest hurry"; `HurriesToTheGate`), `RideScriptModelTests` ("where the advance sat"), `PeepNavigatorTests` ("the
  longest route in the shipped park", true of the save only) and `SoundCategoryTests` ("happened to work here"). Test
  names: `AnimationUvTests.ATwoKeyTrackReadsAsItAlwaysDid`,
  `BalanceFieldTests.AKeyThatNamesOneFieldIsReadExactlyAsBefore`,
  `GameCalendarTests.TheDaysKeepTheOriginalsSlightlyFastPace` (it asserts only a count of 231),
  `ParkEvictionTests.EveryQueuerIsPutOutOfTheQueueAndLosesBothChanges` (its summary: the drain has already put out all
  from the fifth place back) and `ParkWeatherTests.TheWeatherTurnsEverySevenDaysAndIsDecidedThreeDaysBefore` (four
  days, `weather.md`). `ParkPathTests`' summary says "SEVEN tests fail" without the `ReferenceEquals` guard, measured
  when sixteen classes built an edge test; twenty do now: take the guard out again in a worktree and write the count
  taken, or drop it. No game run.
- [ ] **Q204. Load the area-only notes only in their area.** Claude Code loads a `.claude/rules/*.md` file whose
  `paths:` front matter matches an open file, and no other time. Move the sections of `CLAUDE.local.md` (and any of
  `CLAUDE.md`) that serve one area only - the capture recipe, the original under Proton, the 2.0 patch's evidence - into
  such files, keyed to the source and harness paths they serve, so a session that never opens them never loads them.
  Measure both files' size before and after, and confirm in a fresh session that a matching file brings its rule in
  and a non-matching one does not. Borrowed from Rootstock (github.com/Mazhron/rootstock-os); nothing else of it is
  taken. Alexah's yes 2026-10-01, for after the weekly reset. No game run.

## C. Alexah's list: cause known, one session each

- [ ] **Q15. Green line between path cells and on ride signs.** Path textures get the default sampler,
  `AnisotropicWrap` = Wrap (`ParkPaths.Build`, `Material.cs:106`). Sign halves get `AnisotropicRepeat`
  = Mirror (`SignTexture.cs:71-72`, `Material.cs:107`). The original clamps at every site
  (`render-states.md`, "Texture addressing is CLAMP"). With wrap plus mipmaps, a tile's edge samples the far edge. Use clamp
  where the original does. Confirm: screenshot two paths in a column, a crossroads and a ride sign,
  before and after.
  Every model texture is unclamped too (the 2026-09-26 staleness audit): `LobbyModel` asks `TextureFlags.Repeat`,
  which `Material` samples as Mirror, as `ParkPaths` and `ParkGround` now do, and an unflagged texture gets the same
  sampler (`Texture.SamplerFor`). Check that a model's UVs stay inside 0 to 1 before clamping it, since six
  register-pushed sites are not covered (`render-states.md`); the sea keeps its wrap.
- [ ] **Q16. Screams are heard everywhere.** Sounds are placed and panned, but a park never sets
  `Audio.ReferenceDistance` (`Audio.HoldPlaced` says so), so nothing attenuates. The lobby sets it
  (`LobbyAudio.MoveTo`) and clears it on the way out. The distance law lives in `QMixer.dll`
  (`audio.md`), so the reference distance is a declared choice; say so at the site. Confirm: capture the
  mix with the camera on the ride and far from it; show the level difference; screenshot both camera
  positions.
- [ ] **Q17. Camcorder mode can strafe.** `ParkCamcorderCameraMode.Walk` passes
  `Input.Right` into `Step`. `park-engine.md`, "Walking on the ground is swept against the cell edges", decodes the sweep by two velocities but not the
  keys. Remove the strafe. Confirm: press the strafe key, the stand position in the `camcorder` census
  does not move sideways, screenshot.
- [ ] **Q18. The advisor draws in front of the dimmed screen.** `Level.Render` draws the HUD, then
  the overlay pass with the advisor. Pausing does not hide him either; only the options screen's quiet stop takes him off (`ui.md`, "Pausing him"). Draw the
  dimmer over him, or hide him while such a window is open. Confirm: screenshot with the buy screen open.
- [ ] **Q19. VSync and a frame limiter.** `Renderer.CreateGraphicsDevice` hard-codes VSync on. `Display.cs` already
  carries each mode's refresh rate. Add an Options row: VSync mode, and a frame limit from 30 up to
  Unlimited, default the monitor's refresh rate on first launch. Confirm: screenshot the options row;
  log the measured frame time at two settings.
- [ ] **Q20a. A world particle pass.** Nothing draws a particle effect in the world (`ParticleSystem`'s summary, "Not
  built: drawing effects in the world"; `ScreenParticles` draws only `OnScreen` templates). Build it once, for Q20b and
  Q38. Confirm: one known effect drawn in the park, screenshot.
- [ ] **Q20b. No bubbles on the drinks shop, no chimney smoke on the staff room.** The scripts ask for the effects and
  `RideEffects` records the request; nothing consumes the records. Join them to `ParticleSystem.Spawn` through Q20a's
  pass (`ParLib.cs` names `Bubbles = 58`, `Smoke = 2`, `SmallSmoke = 13`; `park.md` decodes the node and the per-tick
  push). Confirm: screenshot the drinks shop with bubbles and the staff room with a staff member inside and smoke
  rising; log lines for both spawns.
  - **Note (fork review, 2026-09-30):** part of what Lost Kingdom's effects need comes from the save, not from new script
    requests. The `PART` module (written by `FUN_0051f680`, read by `FUN_0051f7a0` at `0x00415437`) restores the live
    emitters, and `Particles_KillAllOnScreen` (`0x005200b0`) then kills every screen-drawn one, so after loading
    `Easymode.TPWI` two world emitters live on: WaterFall at slot 0 and Bubbles at slot 20. A saved script's `ADDOBJ`
    handle points at them through the `RSSE` module's `OBJ ` records, which `ParkScriptStates` skips today: seed the
    emitters only together with those records, or `Coconut.RSE`'s `KILLOBJ 1` can never stop the bubbles. The theme's
    `.emt` files go into the first empty template slots, 101 and 102 in jungle, never past 104. Layout: Q193. Review
    items fmt-assets-v1, gap3-6, gap5-1..gap5-4.
- [ ] **Q21. Entering a park: hide the front end for the fly-in.** `IslandPanel.EnterPark` closes only the
  island panel, and the rest of the front end stays up while the camera flies in. Hide it for the flight, and
  say what Escape's cancel (`FrontEnd.MenuKey`, `LobbyCameraMode.CancelLeave`) brings back - today only the
  island panel. The gate half landed with Q41: the flight plays M1 as the homing ends and M2 on a cancel
  (`docs/exe/lobby.md`, "Escape cancels the fly-in"), so do not re-pace it. Confirm: screenshot burst from the
  click to the loading screen.

## D. Alexah's list: decode first, then build (two sessions each)

The decode session writes the finding to `docs/exe/` and stops. The build is the next session.

- [ ] **Q22. Riders sit still on the Belly Bounce.** Seat positions are read once from the model's rest
  pose (the `LobbyModel` constructor) and never from the animated pose. A riding peep's sprite is `None`
  (`Peep.AnimationFor`). Decode: which frame a rider shows, and how the original re-resolves the seat
  node each frame (`ride-operation.md`, "Where a rider is drawn", and `FUN_005580a0` under "The WALK family").
  Then build both. Q175 re-read the lookup by id and flag (`audio.md`, "Node lookup is by id AND a capability flag")
  and decoded where a node's position comes from (the posed matrix, refreshed on screen; `ride-operation.md`, "How
  long a leg lasts, and where its ends are"), and `FUN_005580a0`'s interpolation; which frame a rider shows is still
  open. The engine finds a bounce rider's node by id in `0x800` ("Where a rider is drawn"); `ModelFile.FindNode` is
  that lookup and `RideNodes` the stored positions at rest (Q175b), not the animated pose a rider needs.
- [ ] **Q23. Camera rotation snaps by 45 degrees.** `ParkOrbitCameraMode.Update`, the two rotate keys. The 90-degree
  option exists (`GameOptions.NinetyDegreeRotation`) and is read by nothing. Decode the original's step
  and its easing (`park-engine.md`, "The park camera", has the saved and required rotation, not the rate). Build what
  the decode says, driven by `Time.SmoothingFactor`. Alexah: match the original, do not invent.
- [ ] **Q24. Nothing highlights a thing under the mouse.** The idle pointer already follows the hover category over
  ground and path (`Level.IdleOverPath`). Picking is decoded (`park-engine.md`, "Picking is a real ray cast, not a
  grid lookup"): the hit point comes from a ray against the terrain and then object meshes, and the hovered THING is
  taken from the cell, not by hitting its model (`ParkPicking.ThingUnderCursor`). Decode what the hover updater
  `FUN_00486d90` highlights when the hovered thing is an object or a person, then build it.
- [ ] **Q25. The camcorder button should give a crosshair and place the camera where you click.**
  `ParkGadget.EnterCamcorder` enters the mode at once. The original installs a mouse-interaction mode
  (`FUN_00481a10`, `park-engine.md`, "Camcorder mode — the first-person view"). Its click handler was decoded by Q48
  (`park-engine.md`, "Entering and leaving first person"): cursor `0x13`, a left click on a cell of type 0, 1, 3, 9 or
  30 stands the viewer at the picked point, and leaving puts the saved point of interest and yaw back, where ours keeps
  the walk. It is also Q48's hole (3). Left to decode: the click's own cell tests and its thing branch. Then build.
  From Q121: measured in the original, one forward term (the Up arrow, or a held right button) walks about 21.8 units a
  second at zoom word 110, both together twice that; `WalkSpeed` is a chosen 40. The velocity is not decoded to a speed
  (`hud.md`, "Four ways out of camcorder mode"): decode it with this, then set the speed. The original's walk keys
  are the zoom's, Up and Down (bits `0x20` and `0x40` of the button word, measured); here W and S walk and the arrows
  do not.
- [ ] **Q26. Ferry, seaplane and bus are always there.** `ParkFixedItems.Items` stands all three
  permanently. `ParkPeople.StepArrivals` sizes every load at `Arrival.MinPeople` (1), and `VehicleFor` gives one
  person the bus, so only the bus is ever called. The original creates the vehicle on demand (`FUN_0051a2f0`,
  `park.md`, "Arrivals: who comes, on what, and how often"); the
  headcount score (`FUN_004c8240`) is not decoded. Q68 found the rest of the headcount: `NewParkBonus` is added to
  the score on every call and the sum scaled by 1.2 or 0.8, so even a score of nought brings 3 or 4 to Lost Kingdom.
  Decode the score, then build create-on-demand and the bus / ferry / plane ordering; the pause between loads is
  built (Q68b).
  The same score is the park's worth every guest judges the gate's fee against (`PeepBehaviour.ParkExcitement`, nought
  until it is built; the 2026-09-26 staleness audit): build both readers.
- [ ] **Q27. Pushing the mouse at the screen edge does not scroll.** The "push scroll" option exists and
  is read by nothing. `ParkOrbitCameraMode.Update` scrolls from keys only. Decode the camera
  binding table at `0x00748158` (`park-engine.md`, "The park camera"), then build.
- [ ] **Q28. Renaming parks and rides.** The save carries a name per park (`saves.md`, the `gms.dat` theme record's park name). No rename
  control exists; signs read fixed names (`ParkFixedItems.BuildSign`, `ParkObjects.BuildSign`,
  `LobbyIsland.DisplayNames`). Decode where the original edits the name and what it writes, then build the
  control and make the three sign painters read the save's name.
- [ ] **Q29. Parity with the original's hardware renderer.** `render-states.md`, "Texture addressing is CLAMP", counts the
  MIN/MAG/MIPFILTER and LOD-bias sites but does not decode their values; no shade-mode row exists; ZFUNC
  is LESSEQUAL there and Less here. The renderer lights per pixel with 16x anisotropic filtering and
  full mipmaps (`test.shader`'s fragment `main`, `Material.CreateSampler`). Decode the values, then match them. Confirm:
  side-by-side screenshots of the same view.
- [ ] **Q30. Waving flags at the bus stop.** Nothing found in code or docs. Decode what the original
  draws at `BusStopA/B` (`park.md:54, 98`), then build.
- [ ] **Q37. The placement terrain rule.** `ParkBuilding.Refusal` counts `PLACEMENT_TERRAIN_RULE` and lets a
  thing stand on types 2, 7 and 30 and on land flagged `0x40`, outside the park. The original allows a
  footprint only over in-park bare ground or path (`FUN_00535600`, `FUN_00535670` refusing `0x40` at
  `0x005357c7`); whether every placement route goes through that verdict (its op-`0x104` branch) is
  still open. When it is built, `Unstamp`'s two declared deviations go: keeping `0x40` where the original
  clears the whole flag word, and giving terrain back its save's record rather than clearing it.
  - **Note (fork review, 2026-09-30):** the `.hmp` adds nothing here: its mark plane equals `Info.Shape` in all 274 pictured items
    (Q191).
- [ ] **Q38. What building and selling look and sound like.** Counted, not built: the death particle
  (`DESTROY_PARTICLE_EFFECT`, `Info.DestroyParticleEffect`, 75-78, a world effect spread over the
  footprint) and the demolish sound (`DEMOLISH_SOUND`, `0x96`-`0x99` by size). Not yet counted: the
  build side - `Info.CreateParticleEffect` and the `+0xd4` sound zone the script loader makes when an
  item has one (`park.md`, "What selling a thing does to its script"). Needs a world particle pass,
  which nothing here has; the screen pass draws only `OnScreen` templates.
- [ ] **Q40. The place-staff mode's cell rule and its carry preview.** Found by Q6's decode (`park-engine.md`,
  "Putting a candidate down: the type-5 mode"), so this is a build. The original takes a worker only on a
  cell of `mType` 0, 1, 3 or 9, not flagged `0x40`, whose track record after the 12/17 parent redirect is
  not track type 11, 13, 16, 18 or 25; here any cell on the map takes one (`STAFF_PLACEMENT_CELL_RULE`).
  While a candidate is carried the original shows cursor 9 (`c_carry.ani`), hangs a sprite of their kind
  in their costume under the pointer and puts a red square on a cell the click would refuse; here the
  pointer is plain (`STAFF_CARRY_PREVIEW`). Confirm: carry a candidate over a cell the rule refuses, click,
  still in the hand and the red square photographed; then a path cell, hired.
- [ ] **Q43. The number read as a repeat delay is a priority, and the original throttles nothing.** Found by
  Q9's decode (`docs/exe/audio.md`, "How the engine plays an effect: priority, not a repeat delay").
  `SoundCategoryFile.Effect.RepeatDelay` is the effect record's `+0x0c`, which the original reads only as a voice
  priority, and nothing on its play path reads a clock. So `SoundCategory.Play`'s per-effect throttle has no
  counterpart, and neither have the replays it times: the lobby's beds and one-shots, the park's music and
  weather, and SINGLESCREAM. The original's repeating voices come from the flags word instead. There are two kinds:
  - `0x0404` chains, built by Q9 for the screams only (staff 188 and the ambient beds are not);
  - the `0x4|0x2` class, which hands a variation's wait to the mixer (music 2, kids 91, ambient 33). It is undecoded.

  Also owed:
  - the top-N voice pool, keyed by priority and closeness (N is 12, at most 30);
  - weighted variation picks (4 of 63 effects are uneven, and `SoundCategory.Pick` is even);
  - whether parameter 6 is also a level (OpenTPW hears it as the scream's gain);
  - replacing `ReadSamples`' scan with the structural walk.

  Decode the `0x4|0x2` class first. Confirm: capture the lobby's mix and a park's mix, before and after.
  Music 2 is of that class (`0x0606`), and `ParkAudio` plays the park loop's `FUN_0051bc40( voice, 4, level )` as the
  music's volume, so an empty park is silent; if parameter 4 picks the variation by the crowd instead, that silence is
  ours (`scenes.md`, "Park parameters"; the 2026-09-26 staleness audit). Settle it with the class.
- [ ] **Q51. Rest-area occupancy is unbuilt at both ends.** Found by Q36's decode. Arriving to rest
  (`FUN_00505fe0`) adds one to the rest area's script variable 0 (`VAR_STAFFIN` in every staff room) and sends
  message 15 to the resting-staff list (UI control `0x1e7b`); leaving (`FUN_00506d10`, from the normal end of a rest
  and from a sale) takes the one back. Neither half is built, and all three sites count `REST_AREA_OCCUPANCY`. Decode
  what the staff-room script does with `VAR_STAFFIN`, and whether a resting member of staff is hidden (entering state
  3 frees the sprite). Confirm: a guard resting, `rides` showing the room's variable.
- [ ] **Q55. A member of staff going idle queues no stand.** Found by Q36's review. The original's state-0 setter
  queues animation 3 every time (`FUN_005054d0` case 0, through `FUN_004fa460`: `PUSH 3`, `FUN_004217f0`);
  `Staff.AnimationFor` answers nothing for `Idle`, so a member who stops keeps the cycle they had - a guard put out of
  a sold rest area walks on the spot. Check every state-0 entry the original makes against the kinds' own sprite
  scripts before adding it. Confirm: a guard walking, then idle, `guests` showing the set change, photographed.
- [ ] **Q52. A rider on a thing without flag `0x20` is hidden in the original.** Found by Q36's decode. Admission
  tests the object's flag bit `0x20` (`0x0050212b`) and, without it, destroys the rider's sprite (`0x00502147`);
  OpenTPW never hides a rider. Every visitable thing in Lost Kingdom's save carries the bit, so nothing there shows
  it; a thing bought this session carries none of the bits whose keys are not read (`BOUGHT_OBJECT_FLAG_BITS`).
  `0x20` is `RideHandlesSprite`, descriptor `+0x104` (`0x004db414`; `park-engine.md`, "Still open"): read it, then
  build both. Confirm: a guest riding a bought Belly Bounce, `guests`, photographed.
- [ ] **Q54. The `.sam` reader against the original's parser.** Found by Q36's decode (`park-engine.md`, "How a key
  finds its global"). In the original the first bad line ends the file - an unknown key, a bounded value out of
  range, or a negative in a non-negative field - and an array's count is the highest index written plus one, so
  Lost Kingdom's guest type is `rand % 8`. Compare `ParkBalance` with both. Confirm: a test per behaviour against the
  shipped files, and the guest types a loaded park draws from.
- [ ] **Q58. A purchase faces whatever the hand last held. Decode first.** Found by Q39's decode. The original keeps
  one rotation, `DAT_0081d7a4`, and `FUN_0052f200` writes it only for tool 0 (`0x0052f3b6`), so an item bought over a
  carried move faces the moved thing's way; a candidate's or worker's pickup leaves it too. Here a purchase always
  starts at nought, said at `ParkBuilding.CarryingAngle`. Decode what a successful put-down leaves in it (the light
  setter `FUN_0052f580( 0, 0 )`) before building one rotation. Confirm: move a ride turned 90, buy over it, put the
  purchase down, `objects` showing its angle.
- [ ] **Q60. A mechanic put down should look for work at once. Decode first.** Found by Q39's decode. The type-6
  put-down (`FUN_00505ea0`) sets every kind idle but the mechanic, whose claims are cleared (`FUN_004dad20`) and whose
  job search runs there and then (`FUN_004da5b0`); here a mechanic goes idle like the rest, counted as
  `MECHANIC_PUT_DOWN_JOB_SEARCH`. Decode whether idle here reaches the same search a tick later, and what differs.
  Confirm: pick up a mechanic, put them down, `staff` on the next few ticks.
- [ ] **Q61. The park-entry fly-in darkens the screen, and nothing here draws it.** Found by Q41's decode
  (`docs/exe/lobby.md`, "Escape cancels the fly-in"). State 2 sets the render camera's `+0x60` to
  `clamp( (1 - (r - 8) / (SPINRADIUS - 8)) x 65536, 0, 65535 )` (`0x005e052f`), states 0 and 1 set it to 0, and
  `0x005d8cb0` eases `+0x5c` toward it by an eighth of the gap per lobby pass and draws a black rectangle over the
  view with alpha `min( +0x5c >> 8, 255 )` (`0x008bd4e4`, drawn at `0x00576e54`). So the flight fades to black as it
  closes on the gate, and a cancel lifts it. Counted as `LOBBY_FLY_IN_FADE`. The ease is per pass, not per second:
  convert it through `Time.SmoothingFactor` and say so (rule 10). Confirm: `step` into the flight, screenshots at
  three radii with mean brightness falling, then Escape and the brightness back within a second.
- [ ] **Q62. What View the online world does offline. Decode first.** Found by Q41's decode. `IslandPanel_Callback`'s
  case for button `0x1e0e9` (`0x004b8c18`-`0x004b8c2f`) calls the island camera's `+0x20`, its deactivate
  (`0x005e1bd0`, which Ghidra names `IslandLobby_ViewOnlineWorld`), and then the online-world child's `+0x1c`, with no
  online test read yet; `IslandPanel.ViewOnlineWorld` does nothing, saying the original does nothing offline. Decode
  whether a test sits in front, and what the online child shows with no connection. Confirm: the button pressed,
  `windows` and a screenshot.
- [ ] **Q63. Does the engine's idle default play a lobby gate by itself? Decode first.** Found by Q41's decode. The
  lobby loop's `FUN_0044e410( 3 )` calls `FUN_00473c70( model, 0, 8 )` on every model with `[+0x14] != 0` and
  `!(+4 & 0x3000)`, and that replays role M entry 0 whenever channel 0 has finished, unless `(model+4 & 0x8004) != 0`.
  If the lobby's gate instances pass, the original's gates open by themselves - at the start, and again after a
  cancel's M2 - which would change what `LobbyGate` idles on. Decode the lobby instances' `+4` and `+0x14` (they are
  built through `0x005d8870` with flags `0xc0`). Confirm: a test on the flags, and the gate's `state` over 30 s idle.
- [ ] **Q64. Escape over the player slots opens the game menu.** Found by Q42's decode (`docs/exe/lobby.md`, "The
  lobby's keys act on the release"). `FrontEnd_ShowPlayerSlots` hides the lobby's root control (`0x004a65a8`) and gives
  the slots the focus (`0x004a65d6`), and their callback `0x004a5fc0` drops every key, so with the slots up no key acts
  at all: Escape opens no menu, and the slots' own Quit is the way out. A message box closed over the slots moves the
  focus to the hidden root (`0x004862a0`), where keys still reach nothing. Here `FrontEnd.MenuKey` opens the lobby's
  menu over the slots, said at the site. Confirm: nobody playing, Escape let go, `windows` still `PlayerSlots`; a
  screenshot.
- [ ] **Q65. The system table's keys: Ctrl+H on the release, and F8.** Found by Q42's decode (`lobby.md`, "The
  lobby's keys act on the release", its paragraph "The system table's keys, in either scene"; `park-engine.md`, "The original fires its shortcuts on key RELEASE"). The
  window procedure matches the system table `[0x0078718c]` itself on every key, in both scenes, and runs its handlers
  on the release: `P` (pause, `0x0040bf70`), Ctrl+H (Popup Help, `0x0040c5d0`), F8 (a `Scr%05ld.tga` screenshot,
  `0x0040c470` and `0x00550460`) and Ctrl+Shift+Alt+F8 (`0x0040c480`, a flag read only at `0x0054f455` and
  `0x0054fb93`). The player slots switch the tables off (`0x0040cfa0`) and their close back on (`0x0040cf60`). Here
  Ctrl+H toggles on its press (`HelpBar.Update`, said at the site), F8 is not built, and nothing switches the keys off
  under the slots. Decode what the F8 chord's flag does, and where a screenshot may be written without touching the
  game's folder, before building either. Confirm: Ctrl+H held, the help bar unchanged until it is let go; a screenshot.
  Until F8 and the chord are built, count each where it is let go (`CLAUDE.md` rule 4): nothing binds either, so the
  press reaches nothing and nothing says so (the 2026-09-26 staleness audit).
- [ ] **Q66. A disabled button takes the pointer.** Found by Q42's review. The original's hit test (`0x0065db25`) skips a
  control flagged `0x2` - a disabled button, `0x0065da8d` - with everything under it, so the pointer passes over it to
  whatever is behind: no hover, no help row, no glint. Here `UiButton.TakesMouse` is true whatever `Enabled` is, said at
  the site; the island panel's greyed arrows for an Instant Action player are the case in the lobby, and a press there
  still ends at the panel's outline, so nothing but the hover differs. Before changing the hit test, check every park
  window whose root takes no pointer: a press on a greyed button there would fall through to the world. Confirm: an
  Instant Action player's pointer on a grey arrow, no help row; a screenshot.

## E. Large

- [ ] **Q31. The other eight object windows.** `Level.OpenObjectWindow` opens only a ride's (`UiType 0`). Shops,
  sideshows and the rest stop at `SHOP_WINDOW`, `SIDESHOW_WINDOW` and `FEATURE_WINDOW`, and a staff member at
  `STAFF_WINDOW` (`Level.ClickWorldAt`); a clicked visitor reaches nothing counted. `park-engine.md`, "The
  per-object management screen is nine screens", lists the nine. One window per session, shop first.
  From Q177: the shop, sideshow and toilet windows' figures are decoded (`ride-operation.md`, "The settle-up's
  bookkeeping"): customer satisfaction, profit last month, customers as "C of M", winners last month, the cost of
  goods from the window's pending sliders (applied on close, on stepping to the next or by apply to all), and a
  toilet's users last month. `UIStrings` 37 to 39 are one row off: UITEXT 37 is "Scrap value", 38 "Local happiness",
  39 "Quality of goods", 40 "Sale price"; nothing uses the three members yet.
- [ ] **Q32. Graphics tiers.** Only `Level.SetupParticles` and `Level.NumKids` read the detail files (`low.sam`,
  `med.sam`, `high.sam`), and only `GameOptions.PARTICLEDENSITY` and `NUMKIDS` (the kid and staff bank caps) from them; nothing reads their `GraphicalOptions.*` keys (texture quality and
  filtering, sky, shadows, fog, mipmaps, view distance). The detail-file loader is `0x00423bc0` (`OptionsScreen`'s
  restart note). Decode what it does with each key - the lobby plan's item 11 names three: the `stexture` set,
  `SKYQUALITY` and the particle low-detail byte - then build low / medium / high.
- [ ] **Q33. UI scale.** The UI has one fixed virtual size (`VirtualScreen`, 2048 by 1536). Add Auto / small / medium
  / large in the dead Video Card row (`OptionsScreen.cs:71-72`).
- [ ] **Q34b. Pictures for the README.** Animated pictures need a capture tool; none exists in the repo.
- [ ] **Q72. Values from data, not constants (the 2026-09-12 review's Phase B).** The lobby's four island names
  from `THEMENAMES.str` through the reader the gate already uses (`ParkFixedItems.ParkDisplayName`) in place of
  `LobbyIsland.DisplayNames`; the `ISLAND()` directory and model names in place of `themeName[0..3]` (`LobbyIsland`,
  `LobbyGate`); and `DUCKINGLEVEL` from `sound.sam` for the advisor's 0.38 duck - but that mapping to `0x00785914` is
  inferred, not proven (`Advisor`'s note), so decode the global's writer first. For the names and the `ISLAND()`
  fields the data equals the constant, so "nothing looks different" is the regression check (`docs/exe/lobby.md`,
  "Park names and the locale tables"). Confirm: a lobby screenshot before and after, identical.
- [ ] **Q73. Cache pipelines and assets by key (the rest of the review's Phase D).** One pipeline per material
  (`Material`) where one per (shader, flags, output) would do, and texture and shader caches that scan `Asset.All`
  linearly (`Texture.Cache`, `Shader.Cache`). The shared blank texture landed (`bb897f8`); measure the load before
  and after, as that step did, and do not re-take its measurement.
  - **Note (fork review, 2026-09-30):** add `Renderer.cs`'s per-frame dirty-shader scan of `Asset.All` to the list; time it with the
    frame profiler before changing it. Review item ui-render-platform-2.
- [ ] **Q74. Lift the lobby out of `Level`, or record it as dropped (the review's Phase E). Alexah's call.** Park
  entry shipped without it: `Level` builds the lobby (`SetupEntities`, islands hard-coded, an unread `Global`) beside
  the park (`SetupParkEntities`). Ask Alexah before starting.
- [ ] **Q167. Read Alexah's Full Simulation saves.** Given 2026-09-28: a real player's saves, written by the
  original, in `~/repos/game/saves-fullsim/save/` (read-only; `CLAUDE.local.md` has the path). Jungle has
  `autosave.TPWS`, `New Save.TPWS` and `restart.INTS`; fantasy and hallow have theirs, space is empty. Made with
  `global.sam` edited (starting cash all 9s, one key per park); that changes the data folder, not the save's layout.
  The game crashed during the hallow park and would not load it again, so a hallow file may be damaged. Jungle first:
  load each in OpenTPW, count what reads and what does not, and put every layout fact in the FileFormats docs.
  Q165c read the jungle `autosave.TPWS` and `New Save.TPWS` through `ParkWorld` (`q165cprobe`, read-only): no problem
  reported, and the guests' two histories check out (FileFormats `saves.md`, 470).
  - **Note (Q174c's probe):** the `RSYS` channel module does not walk to its end in the jungle `New Save.TPWS` and
    `autosave.TPWS` (206 channels read, the cursor off the module's end), so `ParkThingStates` refuses it and none of
    their channels is restored; the fantasy and hallow saves and the shipped park walk closed (`q174cprobe/out.txt`).
  - **Note (Q181b):** a park built from the jungle `New Save.TPWS` throws in `ParkGuestSprites` (`People.ToDictionary` by
    `SpriteSlot`: two people share slot 0), after the scripts are bound; the game exits (`q181b/run1/run.log`).
  - **Note (fork review, 2026-09-30):** `restart.INTS` is the original's own fresh park. `FUN_00550b30` writes the new world's
    state once, on the first state-0xf frame, and Restart Park (`FUN_005ac5f0`) loads it back. Each of the three holds
    twelve things (the ten managers, then the unplaced gates and lights, catalogue objects x601 and x603), no people
    and no placed objects, `mGameTick` 0, the park closed, fee 20 and the shipped `Standard.sam`'s loans (APR 18-23);
    only the cash shows the edited data folder. It is Q197's reference. Review items gap3-2..gap3-11.

## F. Then

Back to `docs/PLAYER-GAPS.md`: gap 5 (happiness gauge), gap 4 (advisor in a park), gap 7 (saving a
park, decode first). Also litter and the day ending, whose deferral reasons expired
(`docs/REVIEW-2026-09-21.md` section 6).

## G. The lobby plan's open items

From the lobby plan Alexah agreed on 2026-09-12, "Finishing the Lobby" (its artifacts are listed in
`docs/history/README.md`). Alexah asked that its report be kept in use: when an item here lands or the order changes,
update it with the Artifact tool's `url` (https://claude.ai/code/artifact/48cf6fc2-0874-404c-b7bf-a05fe4b16470), never
a second copy. Items 1-4,
6-8 and the gate half of 5 landed; item 11 is Q32. Cut from the lobby that day and still cut: the intro movies and
splash, the three online UI trees, cones, reverb and Doppler, the message box's third button, and the original's full
state machine.

- [ ] **Q76. The isle's clips loop; the original picks one at random when idle (plan item 5).** The camera update's
  tail loop (`0x005e11f7`) starts the isle's M1 or M2 at random whenever it is idle (`docs/exe/lobby.md`, "Not
  sound"); here the isle's clips loop. Counted as `LOBBY_ISLE_RANDOM_CLIP`, once an isle (Q69); building it takes the counter
  out. The gate half landed (`3eed966`, `9b0ebab`, Q41); whether the gate also idles
  by itself is Q63. Confirm: `sound`/`state` over a minute of lobby, and a capture of the isle between clips.
- [ ] **Q77. The advisor's 90-second idle nag in the lobby (plan item 10).** Responses 394/395 (`0x18a`/`0x18b`,
  armed at `0x005e184c`, `docs/exe/ui.md`); sample 470 is never heard today. The arm and the queue's timer are read
  (`ui.md`, "The lobby's idle repeat"), and the arm is counted as `LOBBY_ADVISOR_IDLE_REPEAT` (Q69); when the first nag
  comes wants one sitting with the original first. Confirm: the line heard after 90 idle seconds, by capture.
- [ ] **Q78. The lobby's sea from `lobby/Terrain/base.md2` (plan item 9). Decode first.** The lobby has a sea: a
  `Water` entity (`Level`) that `Sky` seals the horizon against. The item is re-sourcing it from `base.md2` through the
  park terrain loader - but `Sky`'s own note says the original's lobby has no sea below its islands, which contradicts
  the premise. Settle whether the original draws `base.md2`'s sea before building anything.
- [ ] **Q79. Robustness (plan item 12, the rest).** Atomic save writes exist (`BaseFileSystem.WriteAllBytes`) and
  input is dropped while unfocused (Q42). Open: top-level error containment (`Program.Main`/`Game.Run` catch nothing),
  and easing the render rate and music while the window is unfocused.
- [ ] **Q80. Two tests the lobby audit asked for (plan item 13, the rest).** A save round trip against a real
  game-written file (it wants a sitting with the original to make one), and `LobbyScript` over the four shipped island
  scripts. Sound categories are covered by `SoundCategoryTests`.
