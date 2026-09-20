# What a player notices

The work queue, **ordered by when a player meets it**, not by size or by how interesting it is to build.
Drawn up 2026-09-20 from `docs/STATUS.md`, the 42-thing Lost Kingdom census
(`docs/history/current-task-archive.md`, grep `THE PARK IS FULLY DECODED`) and the live plan.

**Alexah sets which item is the goal. One item per session** (`CLAUDE.md`, "How a session runs" 4).
Tick an item here in the same commit that lands it, and move its detail to the page that owns it.

Scope is **Lost Kingdom only**. Anything that changes nothing in `data/levels/jungle` is not on this list,
however large it looks — that trap has been hit twice.

| # | What a player sees | Depends on |
|---|---|---|
| ~~1~~ | ~~The gates never open when you enter a park~~ **DONE 2026-09-20** | — |
| 2 | Four of the six gadget buttons do nothing | — (to be honest); everything (to work) |
| 3 | Nobody new ever arrives | — |
| 4 | The advisor is silent unless you open a screen | — |
| 5 | The happiness gauge reads wrong | — |
| 6 | Nobody goes home and no day ever ends | **3** |
| 7 | A park cannot be saved | — |

---

## 1. The gates never open when you enter a park — DONE, 2026-09-20

- [x] The gate now idles **shut** and plays its opening clip **once** as the player commits to the park;
      the park is asked for the moment the doors finish. `LobbyGate.Open`, called from
      `IslandPanel.EnterPark`.
- **The original does not do this**, measured rather than assumed: `IslandLobby_LeaveForPark`
  (`0x005e1e30`) is three calls — the leaving flag, `IslandPanel_KeyPuffAndEnterSound`, and a UI
  message 6 that closes the panel — and the state-3 teardown behind it only tears down. So this is
  `CLAUDE.md` rule 11, marked as a deviation at the call site. The queue's old "needs a held scene
  transition" note was a previous session's inference and is withdrawn.
- **Not every gate swings.** Fantasy's is a worm with **no rotation tracks at all**, so it gets no
  `MeshRotator`; a length taken from the rotator alone left it inert. A gate is played for as long as
  it *moves* — the rotation movement's end where it has one, the clip's own span otherwise. Full table
  in `docs/exe/lobby.md`.
- **Confirmed in the running game**, not from code: jungle held the load for 114–120 stepped frames
  against a predicted 114 (57 ÷ 30 fps), reproduced three times; fantasy 198–204 against a predicted
  200, on the other branch. On screen the doors go shut → part-open → open, with the difference
  localised to a band dead centre on the doors (columns 101–135 of 256, peak 33.6 against a 5.84 mean).
- **Two traps worth keeping**, both of which cost runs: the debug console's `pause` sets `Time.Paused`
  and `Time.Delta` then reads **nought**, so anything driven by it freezes and never finishes — use
  `step <n>`, each one frame of ¹⁄₆₀ s. And the lobby camera orbits, so `settle` alone lands on
  whichever side it had reached: the gates face their island's **−Y** side, which is `orbit π`.

## 2. Four of the six gadget buttons do nothing

- [ ] **Seen:** Buy is the first thing anyone clicks in a theme park game, and it is inert. So are Info,
      Money and Research.
- **Lives:** `ParkGadget.cs:311/329/344/347`, all four `NotYet(...)`. Camcorder and Map are the two that work.
- **The real dispatch is decoded** in `docs/exe/hud.md`: `FUN_004a0940( n )` opens *the screen that
  category was last left on*, from three globals seeded 1 / 3 / 10. **The HUD is six category pickers,
  not 17 buttons.**
- **Two jobs, and they are very different sizes.** Making the buttons honest is small. Making them *work*
  means buying, building, hiring, finances and research, none of which exist in any form — the single
  largest missing system in the project. Do not start the second one by accident.
- **Gate:** the `unimplemented` census in the debug console.

## 3. Nobody new ever arrives

- [ ] **Seen:** the park is permanently the save's 13 guests and 5 staff. Once they have ridden the one
      ride, nothing changes again, ever.
- **Lives:** `ParkPeople.PeepsIn` is the *only* place a `Peep` is constructed, and it builds the list from
  `park.People` — the save — and never adds. `PeepState.AtTheBusStop` (21) and `ParkAdmission.BusStopA/B`
  already exist as anchor points with nothing feeding them.
- **Census:** ids 29 and 31-41 are the 13 guests, on the bus road at x 47-48, y 9-15, already walking in.
- **Not blocked:** the gate-admission path it would feed is built and measured — guests pay at the gate.
- **Gate:** `park jungle`, then the `guests` census over time. **Predict the count before reading it.**
- **NOT confirmed in a run** — this rests on code reading alone.

## 4. The advisor is silent unless you open a screen

- [ ] **Seen:** the advisor is a constant presence in the original and says nothing here while you play.
- **Lives:** plan item N2; `docs/exe/advisor-park.md`.
- **The "needs simulation" blocker was refuted:** his acceptance gate consults no world state, and the HUD
  it waited on exists. This is buildable today.
- **Do not transcribe anything first:** every global speech line is already transcribed in
  `/home/alex/ghidra/notes/global-speech-transcripts.tsv`. Grep it.
- **Gate:** audio capture, cross-correlated against the game's own mix — `verifying-audio-by-capture.md`.
- **NOT confirmed in a run** — this rests on code reading alone.

## 5. The happiness gauge reads wrong

- [ ] **Seen:** the gauge does not track how the park is actually doing.
- **Lives:** the `meter.wct` mapping. `ParkGadget.ShowHappiness` and `UiMeter` draw it;
  `ParkPeople.AverageHappiness` supplies the number and is *not* the fault.
- **Largely exonerated already:** `UiMeter.Max` is 100 and it is handed 50. The mapping from the skin to
  the drawn height is what is left.
- **This is the last fault Alexah found by playing that is still open.** It was found by playing;
  confirm the fix the same way.

## 6. Nobody goes home and no day ever ends

- [ ] **Seen:** guests ride, wander, and then do it again forever. The day never closes.
- **Depends on 3.** Built alone, departures drain the park to empty and leave it that way — there is
  nothing to refill it.
- **Lives:** plan item N3 — "the last of the twenty-two the park can reach".
- **Gate:** the `peeps` census, with `pause` and `step <n>` to make a short-lived state observable.

## 7. A park cannot be saved

- [ ] **Seen:** Load, Save and Publish do nothing, which a player meets at the moment they try to stop.
- **Lives:** `ParkFrontEnd.cs:172/173/178`, all three `NotYet(...)`.
- **The real cost is not the button.** **No `.TPWS` has ever been read** — the reader must not be assumed
  to generalise from the one file the game ships. `docs/exe/saves.md` also records an unreconciled
  divergence between the traced preamble byte counts and what the shipped file measures.

---

## Just behind these

- **The Drinks Shop can never serve anyone.** It declares no queue cells, so it is never offered and the
  dismiss path cannot reach it. The sideshow is the only place spending pays off today.
- **35 of 106 opcodes are unimplemented** — but only three are reached by shipped content in this park,
  so it is mostly invisible here. Take the count fresh; three README lines and `RideScriptFile.cs:99`
  still quote older ones.

## Deliberately not on this list

Each was deferred for a measured reason, not for want of interest: the handyman's litter arm (**no litter
source in this park**), the `BUMP` / `COAST` / `HOP` families (**their rides are not placed in Lost
Kingdom**), the HUD's four screens, and the other three themes.
