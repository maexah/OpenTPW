# Executable addresses cited in source

Generated mechanically from every `0x` address our source cites, in comments or code, between `0x00400000` and
`0x00ffffff`: one row per address, with **every** file that cites it. Values in that range that are not addresses
(flags, masks, control ids, a fog colour) are left out. Addresses our source names only as a Ghidra label
(`FUN_...`, `DAT_...`) or as bare listing hex (`0054f668`) are not indexed - grep the source for those, and write an
address as `0x0054f668` for it to be indexed; the few rows cited only as labels predate the rule and are kept. The 'Cited in' paths are relative to `source/`. Fill in the 'What it is' column from the citing
comment, then make the comment point here.

**This is an index of what our source cites, not the decode.** The per-subsystem pages beside it hold
the traced detail — the field tables, the state machines, the evidence and the confidence markers —
and between them they carry over a thousand addresses that no source file cites at all. If an
address here has an empty 'What it is', try the subsystem page first: `park.md`, `park-engine.md`,
`ride-operation.md`, `hud.md`, `weather.md`, `advisor-park.md`, `ui.md`, `lobby.md`, `boot.md`,
`scenes.md`, `audio.md`, `render-states.md`, `saves.md`. See `../README.md` for what each covers.

| Address | What it is | Cited in |
|---|---|---|
| `0x00402938` | Shape reader `FUN_00402720`: start of the row swap that turns a picture upside down | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkEntryCellTests.cs  |
| `0x004029ab` | Shape reader: end of the row swap | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkEntryCellTests.cs  |
| `0x00402d70` | | OpenTPW/Global/GameCalendar.cs OpenTPW/VM/RideScript.cs  |
| `0x00402d90` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x00402db0` | | OpenTPW/Global/GameClock.cs  |
| `0x00402ea0` | | OpenTPW/Global/GameClock.cs  |
| `0x00402f10` | | OpenTPW/VM/RideScript.cs  |
| `0x00403030` | | OpenTPW/Global/GameClock.cs  |
| `0x00403050` | | OpenTPW/Global/GameClock.cs  |
| `0x004030d0` | | OpenTPW/VM/RideScript.cs  |
| `0x004033a0` | | OpenTPW/VM/RideScript.cs  |
| `0x00407f95` | | OpenTPW/World/Level.cs  |
| `0x00409180` | The park teardown on leaving (called at `0x0054ff91`): frees the world and zeroes `0x007cf83c` at `0x004091b8` | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x004091aa` | The park teardown on leaving: destructs the world, whose first member is the staff candidate pool (`FUN_00507840`) | OpenTPW/World/Park/ParkStaffPool.cs  |
| `0x004091b8` | The park teardown on leaving: zeroes the world pointer `0x007cf83c`, after every thing is destroyed | OpenTPW/World/Level.cs  |
| `0x004092a0` | | OpenTPW/Audio/Audio.cs OpenTPW/UI/UiWindow.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x004092a3` | | OpenTPW/Global/GameClock.cs  |
| `0x004092ad` | | OpenTPW/Global/GameClock.cs  |
| `0x004092c3` | | OpenTPW/Global/GameCalendar.cs  |
| `0x00409303` | | OpenTPW/Global/GameClock.cs  |
| `0x00409353` | | OpenTPW/Global/GameClock.cs  |
| `0x0040bda0` | Backspace handler, game-table row 5 and the coaster table's `backtrack` (undefined bytes in Ghidra) | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0040bdde` | Backspace handler: start of the idle branch - one press of the clear on the hovered path | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0040be9c` | Backspace handler: end of the idle branch | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0040c35f` | Escape handler `0x0040c180`: installs the idle mode through the setter over any mode but 0 or 1, which runs the outgoing mode's uninstall | OpenTPW.Tests/ParkHandTests.cs  |
| `0x0040c368` | Escape handler `0x0040c180`: after the idle install, `FUN_0052f200(0,1)` zeroes the tool and the rotation, and the handler answers 1 so the menu does not open | OpenTPW.Tests/ParkHandTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x0040c4d0` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/WindowStack.cs  |
| `0x0040c5d0` | The system table's Ctrl+H handler, Popup Help, run on the key's release by the window procedure | OpenTPW/UI/HelpBar.cs  |
| `0x004134f5` | Item loader `FUN_00413410`: descriptor `+0x4ac` stored as a copy of `+0x4c`, `Info.WhichUIType` | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x00415270` | the whole-game restore chain: seventeen modules in order, each checked against a four-character tag that follows it | OpenTPW.Files/Formats/Save/ParkScriptStates.cs  |
| `0x00419710` | | OpenTPW/UI/UiFonts.cs  |
| `0x00423690` | | OpenTPW/Client/GameOptions.cs OpenTPW/World/Level.cs  |
| `0x004237f0` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW/Client/SaveFolder.cs OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00423ad0` | | OpenTPW/Client/GameOptions.cs OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00423b00` | | OpenTPW/Client/GameOptions.cs OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00423bc0` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00423dd0` | | OpenTPW/Client/GameOptions.cs  |
| `0x00423e90` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/SaveFolder.cs  |
| `0x004242c0` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW.Files/Formats/Save/RecordStream.cs  |
| `0x00424820` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW/Client/SaveFolder.cs  |
| `0x00424930` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW/Client/SaveFolder.cs  |
| `0x00429ba0` | | OpenTPW/World/Advisor/AdvisorModel.cs  |
| `0x00429d60` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0042a190` | | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x0042bdd8` | `FUN_0042b1c0`: the first-person sweep begins, the camcorder's step taken cell by cell | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042bdf6` | | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042bef5` | The sweep: X's reach, from `modf` of `position * 0.1f` | OpenTPW.Tests/ParkCamcorderWalkTests.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042bff8` | The sweep's tie-break: an exact tie divides the X step by 1.01 so that Y is asked | OpenTPW.Tests/ParkCamcorderWalkTests.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042c093` | `FUN_0042b1c0`: the X-axis call of `FUN_004d8750`, pushing a literal 2 | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042c0b0` | The sweep: X refused going negative is parked at `cell * 10` | OpenTPW.Tests/ParkCamcorderWalkTests.cs  |
| `0x0042c14d` | The sweep: the nudge after an open crossing that left the cell unchanged | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042c197` | The sweep: the axis not asked goes the same fraction, and is put back if its cell changed | OpenTPW.Tests/ParkCamcorderWalkTests.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042c290` | `FUN_0042b1c0`: the Y-axis call of `FUN_004d8750`, pushing a literal 2 | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042c460` | The sweep: the whole step, with either axis put back if its cell changed | OpenTPW.Tests/ParkCamcorderWalkTests.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042d130` | | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x0044b220` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x0044b2e0` | | OpenTPW/World/Advisor/AdvisorModel.cs  |
| `0x0045aa5a` | | OpenTPW/Client/Players.cs  |
| `0x0045aa74` | | OpenTPW/Client/Game.cs OpenTPW/Client/Players.cs  |
| `0x0045aab4` | | OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x0045acfc` | | OpenTPW/Client/Game.cs  |
| `0x004623b3` | | OpenTPW.Tests/LobbyModelAnimationTests.cs OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x004623df` | | OpenTPW.Tests/LobbyModelAnimationTests.cs OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00463060` | the build path: checks role 0 exists, triggers it, then queues role 13 to freeze the model on its last frame. Why a newly built thing plays its construction clip and a loaded one does not | OpenTPW/World/Park/ParkRides.cs  |
| `0x004646a1` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004647a0` | the `RSYS` arm of the restore chain: overwrites every animation channel from the saved record and restores the per-node flag words with it, which is what stops a loaded park's things standing frozen | OpenTPW.Files/Formats/Save/ParkThingStates.cs  |
| `0x00467d00` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00467d60` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x0046b600` | | OpenTPW.Common/Client/Window.cs  |
| `0x0046c480` | Place-staff mode (type 5, vtable `0x006fea40`) MOVE: carries the candidate's sprite under the pointer and draws a red square over a cell the click would refuse | OpenTPW/World/Park/ParkStaffPool.cs  |
| `0x0046c730` | Place-staff mode OnInstall: carry cursor 9, and a sprite of the candidate's kind in their costume | OpenTPW/World/Park/ParkStaffPool.cs  |
| `0x0046cdc0` | Place-worker mode (type 6) OnUninstall: when the hand still names a worker, puts them down on their own current cell with the drop's body | OpenTPW.Tests/ParkHandTests.cs OpenTPW.Tests/ParkLeaveTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x00470e90` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004711d0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471860` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471c73` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW.Tests/AnimationEasingTests.cs  |
| `0x00471c83` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471d32` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00472f60` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00472fdd` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004732a0` | The animation trigger: plays entry N of role R on a model channel, once or looped, queued behind a clip still part-way through unless the flags carry `0x2` (`park.md`); the lobby's gate and isle reach it through `0x005d83f0` with role 5, M (`lobby.md`, "Escape cancels the fly-in") | OpenTPW/World/Lobby/LobbyGate.cs  |
| `0x004732c2` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x0047337b` | | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004733b1` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004733cc` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW.Tests/RideScriptModelTests.cs  |
| `0x004733d6` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004733db` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004733e7` | | OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004738a3` | | OpenTPW.Tests/AnimTimeControlTests.cs OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00474070` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00474840` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00474bf0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00474cc0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004762b0` | | OpenTPW/World/Park/SpriteScript.cs  |
| `0x00476c50` | | OpenTPW/UI/UiMesh.cs  |
| `0x00476e80` | | OpenTPW/UI/UiMesh.cs  |
| `0x00476f10` | | OpenTPW/UI/UiMesh.cs  |
| `0x00477310` | | OpenTPW/UI/UiMesh.cs  |
| `0x0047ed80` | | OpenTPW/UI/UiWindow.cs  |
| `0x0047eed0` | | OpenTPW/UI/Screens/MessageBox.cs  |
| `0x0047f020` | | OpenTPW/UI/Screens/MessageBox.cs OpenTPW/UI/UiWindow.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0047f251` | | OpenTPW/World/Level.cs  |
| `0x004813c0` | | OpenTPW/UI/WindowStack.cs  |
| `0x00481ad0` | Camcorder button `FUN_00481a10`: installs the camcorder mode through the setter, letting go of the hand | OpenTPW.Tests/ParkHandTests.cs  |
| `0x00485780` | | OpenTPW/UI/ButtonGlint.cs OpenTPW/UI/UiControl.cs OpenTPW/UI/UiSounds.cs OpenTPW/UI/WindowStack.cs  |
| `0x00485a70` | | OpenTPW/UI/UiFonts.cs  |
| `0x00485d20` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00486bce` | | OpenTPW/Global/GameClock.cs  |
| `0x004873b3` | Hover category: a type-12 track cell under a type-25 parent gets no category | OpenTPW/World/Level.cs  |
| `0x0048842b` | Park mouse proc: a quick right click with RMB cancel on installs the idle mode over whatever mode is current | OpenTPW.Tests/ParkHandTests.cs OpenTPW/World/Level.cs  |
| `0x00488a00` | | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x00489ca0` | | OpenTPW/UI/WindowStack.cs  |
| `0x00489de1` | | OpenTPW/Client/Renderer.cs  |
| `0x0048b220` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x0048b2a0` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x0048b6a0` | | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x0048b977` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x0048bc30` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x0048bc50` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x0048bd40` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x0048bf28` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x0048c150` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Screens/GameMenu.cs  |
| `0x0048c600` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Screens/GameMenu.cs  |
| `0x0048c830` | | OpenTPW/Global/GameClock.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Screens/GameMenu.cs OpenTPW/UI/UiWindow.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0048c83a` | | OpenTPW/Global/GameClock.cs OpenTPW/UI/UiWindow.cs OpenTPW/World/Level.cs  |
| `0x0048c868` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Level.cs  |
| `0x0048cd10` | | OpenTPW/Client/GameOptions.cs  |
| `0x0048f4a6` | | OpenTPW/UI/VirtualScreen.cs  |
| `0x0048f830` | | OpenTPW/UI/UiText.cs  |
| `0x00491ab0` | | OpenTPW/UI/UiText.cs  |
| `0x00492180` | | OpenTPW/UI/HelpBar.cs  |
| `0x00492d80` | | OpenTPW/UI/Screens/GameMenu.cs OpenTPW/UI/UiSounds.cs  |
| `0x00492e80` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00492f60` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00498d33` | The entry-price screen's `b_door`: `FUN_00519ef0( down != 1, 0 )`, so down closes the park | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x00498fc3` | The entry-price builder reads `mParkClosed` (`FUN_0051a280`) for the door switch | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x00498fd9` | The entry-price builder sets `b_door` down for a closed park (`Button_SetDown`) | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x004a0f05` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2387` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2529` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2bf0` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a2e90` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a3480` | | OpenTPW/UI/Screens/OptionsScreen.cs OpenTPW/UI/UiControl.cs  |
| `0x004a3960` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a3a30` | | OpenTPW.Common/Client/Window.cs OpenTPW/UI/Screens/OptionsScreen.cs OpenTPW/UI/UiWindow.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x004a3a4f` | | OpenTPW/World/Level.cs  |
| `0x004a3ae0` | `OptionsScreen_Open` hides the lobby's root control (message 6) | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a43b0` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a4490` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a6000` | | OpenTPW/Client/Locale/UIStrings.cs OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a61b0` | | OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a61d0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs OpenTPW/UI/Screens/MessageBox.cs  |
| `0x004a6290` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a62b0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a6580` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a65a8` | `FrontEnd_ShowPlayerSlots` hides the lobby's root control | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a6a50` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x004a6a83` | `FrontEnd_ClosePlayerSlots` shows the lobby's root control again | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a6b80` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x004a6d00` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs OpenTPW/UI/WindowStack.cs  |
| `0x004a6e40` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x004ad5c6` | The ride window greys its door for a closed ride the open guard refuses (from here) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ad5e2` | The ride window's door greying (to here) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ad606` | The ride window sets its door down while `mCanLoad` is nought (`Button_SetDown`, from here) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ad622` | The ride window's door position (to here) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ad890` | The object windows' shared base, vtable `+0xc`: fills the stats table's labels | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004af440` | The object windows' shared base, vtable `+0x3c`: writes the three buffered values onto the ride | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004b8b70` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b8ca0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b8ea0` | Shows the island panel again: message 6 with 1 to its tree, then the mail badge rule `0x004bbbd0` | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004b8ee0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b9340` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b9840` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004d49a0` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x004d6545` | `FUN_004d6410`: a staff member's idle stamp tested against `mGameTick` | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004db3f3` | Object constructor: the flags word built from the item's description (from here) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db420` | Object constructor: the queue-path bit `0x08` from descriptor `+0x40`, `Info.HasQueue` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db425` | Object constructor: `OR [ESI+0x32],0x8` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db517` | | OpenTPW/World/Park/ParkRides.cs  |
| `0x004db712` | Object constructor: closes an object carrying the queue-path bit (from here) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db793` | Object constructor: the queue-path close (to here) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004dcf90` | | OpenTPW.Tests/ParkRidesTests.cs  |
| `0x004dd150` | The object destructor `FUN_004dd0a0` sends the type-10 message on the bus: every guest and member of staff bound to the thing answers it | OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004dd2c9` | The object destructor `FUN_004dd0a0` calls the script teardown `FUN_00559060` with mode 0, 4 or 7 | OpenTPW.Tests/ParkSellTests.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x004ddd4e` | `FUN_004ddd20` (leave a queue) empties `VAR_LETMEON` when it names the leaver (from here) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004ddd7d` | `FUN_004ddd20`: the `VAR_LETMEON` clear (to here) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004ddde9` | `FUN_004ddd20`: with no `mQPrev` the leaver's `mQNext` becomes `mFirstInQ` | OpenTPW.Tests/ParkQueueJoinTests.cs OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004ddfa9` | `FUN_004ddf50` (GetPositionInQueue) gives up at a guest no longer queueing | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004de2b9` | `FUN_004de1f0`'s queue walk skips the object's nominee | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004de2bd` | `FUN_004de1f0`'s queue walk reads each `mQNext` before the call | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004de2f7` | `FUN_004de1f0`'s tail: the reopen asks `mCanLoad` nought first | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004de3df` | `FUN_004de1f0`'s tail: `mIsTrackRideValid` for track types 1 to 3 | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004de48c` | `FUN_004de1f0`'s tail: `mAssignedStaffMember` (`+0x5e`) zeroed on every call | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004de510` | `FUN_004de4a0`'s entrance arm: the angle names a side (from here) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004de53f` | `FUN_004de4a0`'s entrance arm: the angle's side (to here) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004de8d8` | `FUN_004de840`: the jitter across a queue, `rand % 28 + 114` (`ADD BL,0x72`) | OpenTPW/World/Park/ParkQueuePlace.cs  |
| `0x004dea15` | `FUN_004de840`: the back cell's side table `01 04 10 40`, a fourth place turning to the side opposite the first path | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/ParkQueuePlace.cs  |
| `0x004dea4e` | `FUN_004de840`: `GetNeighbouringCell`'s answer read as a cell unchecked (NULL off the map) | OpenTPW/World/Park/ParkQueuePlace.cs  |
| `0x004deb26` | `FUN_004de840`'s second switch: `ADD AL,0x80` for direction `0x04`, so 191 gives 63 | OpenTPW/World/Park/ParkQueuePlace.cs  |
| `0x004decd1` | `FUN_004dec30` reads the ENTRY cell's `mDirection` | OpenTPW/World/Park/ParkQueuePlace.cs  |
| `0x004df3ea` | `FUN_004df390` logs "Opening non-openable ride!" five times when its guard refuses, and opens anyway | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004e0554` | `FUN_004e0450` puts the queue's head out (`FUN_005012f0`) | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004e13fc` | `Invite`'s `mCanLoad` bail: `FUN_004e0450` and return, skipping the watchdog | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004e16c6` | The charge's economy feed `FUN_004e16b0`: the price deposited in the park's bank (`FUN_004d0190`) | OpenTPW.Tests/ParkRideExitTests.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004f7ea9` | | OpenTPW/Global/GameCalendar.cs  |
| `0x004f8321` | | OpenTPW.Tests/GameCalendarTests.cs OpenTPW/Global/GameCalendar.cs  |
| `0x004f8792` | | OpenTPW/Global/GameCalendar.cs  |
| `0x004f87e7` | | OpenTPW.Tests/GameCalendarTests.cs OpenTPW/Global/GameCalendar.cs  |
| `0x004f9f89` | `FUN_004f9f00`: first half of `prev + (cur - prev) * t` | OpenTPW.Tests/ParkGuestPlacementTests.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x004f9fb6` | `FUN_004f9f00`: second half of the interpolation | OpenTPW.Tests/ParkGuestPlacementTests.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x004fa015` | `FUN_004f9f00` copies the octant straight off the thing: the heading is not blended | OpenTPW.Tests/ParkGuestPlacementTests.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x004fa62a` | `FUN_004fa5f0`: returns nought without routing on the retry stamp at `+0x198` | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fa95d` | The place-a-peep routine re-stamps previous := current | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/PeepNavigator.cs  |
| `0x004fae10` | | OpenTPW.Files/Formats/Save/RecordStream.cs  |
| `0x004fb075` | Guest constructor `FUN_004faec0`: happiness set to 50.0 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb383` | The guest's type-10 answer `FUN_004fb360` chooses a guest by `mMajorDest` alone | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb38d` | `FUN_004fb360`'s rider arm: state `0x10` exactly | OpenTPW.Tests/ParkEvictionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb3cd` | `FUN_004fb360` makes a rider a new sprite when admission destroyed theirs; its position is still nought | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb3f5` | `FUN_004fb360` plays the kids' effect `0x80` at the rider's sprite | OpenTPW.Tests/ParkPutOffSoundTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb4a1` | `FUN_004fb360` puts the guest into state 6, deciding | OpenTPW.Tests/ParkEvictionTests.cs  |
| `0x004fcb21` | The guest's chooser `FUN_004fcb10` clears `mMajorDest` before it chooses, chosen or not | OpenTPW.Tests/ParkEvictionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fcbc4` | The chooser `FUN_004fcb10` aims at the back-of-queue cell's centre (`FUN_004fa530`) | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fcc49` | `FUN_004fcc30` asks `GetBackOfQueue` of the object: the score's distance and nearby effects are read there | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fde6a` | Price opinion `FUN_004fde50`: a price of nought answers nought, and no sample is pushed | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fdf6d` | Price opinion: the first unsigned division by 100 (`MUL`, `SHR 5`), mood times the goods | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fdfe7` | Price opinion: the second unsigned division, after `RipOffOK` | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe005` | Price opinion: the third unsigned division, after happiness - the worth | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe15f` | Price opinion: price above worth, unsigned (`JA`) | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe167` | Price opinion: cash below price, unsigned (`JC`) | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe453` | `FUN_004fe1e0` docks `SmallHappinessChange` behind a gate on descriptor `+0x148` | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe4a5` | `FUN_004fe1e0` docks `SmallHappinessChange` behind a gate on descriptor `+0x144` | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe4cf` | `FUN_004fe1e0`: happiness gains the object's byte `+0x198` times the happiness effect over a hundred (to `0x004fe525`) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe525` | `FUN_004fe1e0`: the end of that gain | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004ffc3d` | State 10's arrival test: the guest's cell against `GetBackOfQueue` | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffdad` | Joining a queue re-takes the place (`FUN_00501160`) | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffdf4` | Arriving at a queue with no route to the place: put out | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe16` | State 10: no back of queue, or no route to it - state 6 with `MajorDest` kept | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050010a` | `InQueue` turn: the board arm's no route, "the player has removed the path", out | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005001d8` | `InQueue` turn: invited but not the nominee, the whole turn is nothing | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500270` | `InQueue` turn: the lost place, "Problem with a queue", out | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x005002f2` | `FUN_004ffff0`: the mood, which a re-take falls through to | OpenTPW.Tests/ParkQueuePlaceTests.cs  |
| `0x00500308` | `InQueue` turn: the mood is read once `mGameTick - mTimeOfLastSpotAnim` exceeds 30 | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050031c` | `InQueue` turn: happiness above `0x50` plays spot animation 5 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050031e` | `InQueue` turn: the branch for happiness above `0x50` | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500330` | `InQueue` turn: happiness from `0x14` asks about the toilet | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500334` | `InQueue` turn: happiness below `0xa` gives up unhappy | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500336` | `InQueue` turn: happiness 10..19 plays spot animation 4 | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x005003a2` | `InQueue` turn: a toilet need above `0x50` leaves for a toilet | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005003b6` | `InQueue` turn: a queuer for a toilet stays in its queue | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500415` | `InQueue` turn: boredom after `mTimeStartedIdling` + 100 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050049e` | The `InQueue` turn's shared way out (from here) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005004b3` | `FUN_004ffff0`'s put-out tail: `FUN_004ddd20`, then `FUN_005012f0` | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500523` | `InQueue` turn: a ride broken down (state 1) re-takes no place | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500532` | `FUN_004ffff0`'s re-take: `FUN_00501160`, and out if it fails | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500631` | `InQueue` turn: the track gate's item track type 1 | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500643` | `InQueue` turn: the track gate's `mIsTrackRideValid` nought | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500715` | At the door: the price opinion `FUN_004fde50` | OpenTPW.Tests/ParkRideExitTests.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/PeepBehaviour.cs OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x00500778` | At the door, too expensive: the first `MediumHappinessChange` | OpenTPW.Tests/PeepPriceOpinionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005007b4` | At the door, too expensive: put out (`FUN_005012f0`) | OpenTPW.Tests/PeepPriceOpinionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500826` | At the door, refused: re-take the front of the queue | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500857` | At the door: "Couldn't rejoin FOQ even!", put out | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050133d` | `FUN_005012f0` plays the kids' effect `0x80` only when the guest's id `& 7` is nought | OpenTPW.Tests/ParkPutOffSoundTests.cs OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x00501413` | `FUN_00501390`: place against cells times four, unsigned (from here) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050141c` | `FUN_00501390`: the unsigned place test (to here) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00501422` | `FUN_00501390`: a guest in state 14 is never put out | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050148a` | `FUN_00501390`: thought `0xd` when the id divides by three | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00501658` | Guest tick handler `FUN_00501650`: its first call, `FUN_004fa870`, stamps the previous position | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/PeepNavigator.cs  |
| `0x0050212b` | Admission tests the object's flag bit `0x20`, which keeps the rider's sprite | OpenTPW.Files/Formats/Save/ParkWorld.cs  |
| `0x00502147` | Admission destroys the rider's sprite on an object without flag bit `0x20` | OpenTPW.Files/Formats/Save/ParkWorld.cs  |
| `0x00504d8f` | `FUN_00504c70`: a staff member put out of a sold rest area claims another and stays in state 0 | OpenTPW.Tests/ParkEvictionTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00505495` | `FUN_00505490` opens with `FUN_004fa870`, as the guest handler does | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x00506286` | `FUN_005061d0`: the normal end of a rest calls `FUN_00506d10`, which takes one off `VAR_STAFFIN` | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x0050cd80` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x0050f870` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x00511fc4` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512880` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x0051295c` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512a5e` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512b4c` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00515865` | | OpenTPW/Global/GameCalendar.cs OpenTPW/World/Level.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00516394` | Thing sweep `FUN_00516380`: `mGameTick` up by one | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00516d13` | World save `FUN_00516c80`: installs the idle mode before anything is written, so leaving a park lets go of the hand | OpenTPW/World/Level.cs  |
| `0x00517bec` | World load: `mGameTick` read from the save | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00519f40` | `FUN_00519ef0`'s second-argument path writes the gate's `VAR_COMMAND` = 2 | OpenTPW/World/Park/ParkRides.cs  |
| `0x00519f76` | `FUN_00519ef0`'s open arm (first argument non-zero), only when the park is closed | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/UI/Park/ParkEntryPriceScreen.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a013` | The park's door, opening: the open guard `FUN_004df290` on each visitable object | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x0051a01e` | The park's door, opening: `FUN_004df390` opens it | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a09e` | `FUN_00519ef0`'s close arm runs only when the park is open | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a0e8` | The park's door, closing: the gate's `VAR_COMMAND` = 0 when nobody is in the park (from here) | OpenTPW/World/Park/ParkRides.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a161` | The park's door, closing: the gate write (to here) | OpenTPW/World/Park/ParkRides.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a1ae` | The park's door, closing: `FUN_004df300` on every visitable object | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a66b` | End of `FUN_0051a2f0`: sets variable 0 (`VAR_TRIGGER`) to 1 on the standing vehicle | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0051b920` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x0051bcb0` | | OpenTPW/Audio/Audio.cs OpenTPW/World/Level.cs  |
| `0x0051bd70` | | OpenTPW/Audio/Audio.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0051bfab` | Sound_ApplyGroupVolumes posting the voice service (message `0x700b6c`) | OpenTPW/World/Park/ParkScreams.cs  |
| `0x0051c2c0` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x0051c300` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x0051e8f0` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x0051ea50` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x0051eae0` | | OpenTPW/UI/UiSounds.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0051ef30` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x0051f320` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051f370` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x0051faa0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051fd20` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051fd80` | | OpenTPW/World/Level.cs  |
| `0x0051fe30` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051feb0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051ff10` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520130` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs OpenTPW/World/Lobby/LobbyScript.cs OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520470` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520560` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520d60` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520e00` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520f10` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x005214a0` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00521d60` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00521e60` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00522360` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x005224f0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00524a63` | Build commit: a red preview (`DAT_00816d48`) lays nothing, sound `0xaf` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00524aae` | Place commit: a click on a red cell plays sound `0xaf` and leaves the hand as it is | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00524acd` | Build commit: end of the red-preview refusal | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00524db7` | Place commit: karts and the water ride lay their first track cells | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00524e49` | Place commit: end of the track-cell seeding | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00525264` | Place commit: `FUN_0052a050` anchors the queue tool on the cell before the entrance | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052529e` | Place commit: `FUN_0052f580(3,0)`, mode 3 keeping the anchor | OpenTPW/World/Park/ParkBuildMode.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005253c4` | Place commit: a thing with no queue goes idle, `FUN_0052f580(0,0)` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005260f5` | Mode `0x14`: `FUN_00530120` anchors on the queue's end | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00526118` | `FUN_004de1f0` from the queue-edit arm (`0x14`) | OpenTPW.Tests/ParkQueueRemeasureTests.cs  |
| `0x00526120` | Mode `0x14`: then `FUN_0052f580(3,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005271b7` | Apply dispatcher `FUN_00524960`: start of the path/queue arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527222` | Mode-3 commit: start of the queue run arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527541` | `FUN_004de1f0` after a queue run is laid (mode 3) | OpenTPW.Tests/ParkQueueRemeasureTests.cs  |
| `0x005275f2` | Mode-3 commit: the tool ends through `FUN_0052f200(0,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527655` | Apply dispatcher: end of the path/queue arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0052818d` | The demolisher puts back the tool it was called under, `FUN_0052f200( prevTool, 0 )`; tool 0 installs the idle mode | OpenTPW.Tests/ParkHandTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052842b` | The demolisher's second footprint pass: `FUN_005367a0( 0, 0 )` on every cell of the shape but its `.` ones | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00528f62` | Placer: start of the pairing of footprint bases 1, 0x40, 0x10, 4 with angles 0, 90, 180, 270 | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00528f8b` | Placer: end of that pairing | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005292b2` | Placer sweep: the exit takes its turned bit as its direction | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005293c3` | Placer sweep: the entrance takes its turned bit as its direction | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529744` | Placer: returns null when the entrance faces off the map | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529757` | Placer: end of that test | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005297e7` | Placer: start of the queued arm (entrance pair and the queue cell before it) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529808` | Placer: op `0x87` on the cell before the entrance, then the queue stamp | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529885` | Placer: last op on that queue cell | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529890` | Placer: the queue rewalked, `FUN_004de1f0` | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005298d5` | Placer: a thing with no queue gets a path before its entrance | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529951` | Placer: start of the four-cardinal relink round an entrance path | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299aa` | Placer: end of that relink | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299cb` | Placer: the exit half, gated on `FUN_0052fab0` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299e2` | Placer: returns null when the exit faces off the map | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299f5` | Placer: end of that test | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529abf` | Placer: start of the relink round the exit path | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529b18` | Placer: end of the exit half | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052ffec` | `FUN_004de1f0` from the backtrack `FUN_0052fe50` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053473c` | Stamp: queue over path force-clears the path, `FUN_005367a0(0,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053475d` | Stamp: end of the force-clear | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00534858` | `FUN_004de1f0` from the stamp: path laid over a queue cell | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00534906` | `FUN_005348d0` path arm: a queue cell becomes path, type only | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x00534913` | Path arm: the type write | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x0053522d` | `FUN_005348d0`: the queue arm, laid type 0 or 3 | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053525a` | Queue arm: first of the four entrance-bond probes | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535323` | Queue arm: last entrance-bond probe | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535597` | Queue arm: end | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005357c7` | Verdict `FUN_00535670`: the `0x40` outside-the-park test, first for every tool | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535811` | Verdict `FUN_00535670`: the parent redirect for track types 12 and 17, made inline after `FUN_004d0af0` fetches the cell's own track record | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005358e9` | Queue verdict: the cash total skips queue over path | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005358f1` | Queue verdict: end of that guard | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535d63` | Verdict: end of the path arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536a07` | The clear's queue arm zeroes the overlap counter under force | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536abf` | The clear's queue arm zeroes the overlap counter before its reset | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536bc9` | The clear's reset, which type 4 and the queue arm jump to and the path arm repeats: bare, unlinked, unflagged, unowned, tile 55 | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053c755` | `FUN_0053c3f0`: the marker wave's phase gains 0.1 a frame unless paused | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x0053c773` | `FUN_0053c3f0`: the phase store | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x00540d90` | | OpenTPW.Files/Formats/Sprite/SpriteBankFile.cs  |
| `0x005423a0` | | OpenTPW.Files/Formats/Sprite/SpriteBankFile.cs OpenTPW/UI/ScreenParticles.cs  |
| `0x0054e682` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0054e6a6` | | OpenTPW/Client/Game.cs  |
| `0x0054e6df` | | OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0054e768` | | OpenTPW/Global/GameClock.cs  |
| `0x0054e770` | | OpenTPW/Global/GameClock.cs  |
| `0x0054e780` | | OpenTPW/Global/GameClock.cs  |
| `0x0054ea4c` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Level.cs  |
| `0x0054ec92` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054ec9a` | | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054ec9f` | | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054ed3f` | | OpenTPW/World/Level.cs  |
| `0x0054ed7c` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Level.cs  |
| `0x0054f455` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f45f` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f47a` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f493` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f49b` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4bf` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4c4` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f56b` | | OpenTPW/VM/RideScriptScheduler.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x0054f668` | | OpenTPW/Global/GameCalendar.cs OpenTPW/World/Level.cs OpenTPW/World/Park/ParkWeather.cs  |
| `0x0054f680` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f683` | Re-stamps the peep beat's baseline `[0x00878a1c]` inside the every-eighth-tick gate | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0054f7bb` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f870` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f9f9` | | OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0054fa0d` | Per-frame block: placed objects' fraction, 1/31 against its own baseline | OpenTPW/Global/GameClock.cs  |
| `0x0054fa38` | Per-frame block: particles' fraction, 1/62 | OpenTPW/Global/GameClock.cs  |
| `0x0054fa5c` | Per-frame block: peeps' and staff's fraction, 1/248.000007, driving `FUN_00518f90` | OpenTPW/Global/GameClock.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x005502f6` | | OpenTPW/Client/Players.cs  |
| `0x00551241` | STARTSCREAM: `(a + b) / 2`, clamped, for parameter 6 | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00551265` | STARTSCREAM: sets parameter 6 on the new handle | OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x00551701` | `FUN_005516b0`: clears the script's critical flag as its turn begins | OpenTPW/VM/RideScript.cs  |
| `0x00551715` | | OpenTPW/VM/RideScript.cs  |
| `0x00551724` | | OpenTPW/VM/RideScript.cs  |
| `0x00551df5` | | OpenTPW.Files/Formats/Script/RideScriptFile.cs  |
| `0x00551f5f` | `PUSH 0x1c`: the effect list's nodes are 28 bytes | OpenTPW/World/Ride/RideEffects.cs  |
| `0x00552376` | `KILLOBJ`: steps to the next node before it unlinks the match, with no break | OpenTPW/World/Ride/RideEffects.cs  |
| `0x00552950` | | OpenTPW/VM/RideScript.cs  |
| `0x005529bc` | | OpenTPW/VM/RideScript.cs  |
| `0x00552ab0` | | OpenTPW.Tests/RideScriptModelTests.cs  |
| `0x00552c1a` | `TRIGWAITANIM` handler | OpenTPW/VM/RideScript.cs  |
| `0x00552fe5` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00553158` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x005531f9` | | OpenTPW/VM/RideScript.cs  |
| `0x00553435` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00553470` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x005535d6` | `TRIGWAITANIM`: the not-equal re-entry rewinds and writes `+0x98 = 0` | OpenTPW/VM/RideScript.cs  |
| `0x005535f4` | `TRIGWAITANIM`: the equal branch clears the mark `+0xbc` and falls through | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x0055374e` | | OpenTPW/VM/RideScript.cs  |
| `0x00554c07` | | OpenTPW/World/Ride/RideState.cs  |
| `0x0055512e` | `SPAWNCHILD` tests the loader's answer (`TEST EAX,EAX / JZ`) | OpenTPW/VM/RideScript.cs  |
| `0x005555ad` | `GETVARINCHILD`/`GETVARINPARENT`: shared tail that bounds the index from above only | OpenTPW/VM/RideScript.cs  |
| `0x00555f6b` | `SINGLESCREAM` picks its branch on the second operand (`CMP ESI,EDI / JGE`) | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00556009` | `SCREAMLEVEL` writes the volume call's return over the scream handle at `+0xd0` | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0055641b` | | OpenTPW/VM/RideScript.cs  |
| `0x0055646d` | | OpenTPW/VM/RideScript.cs  |
| `0x005567d8` | The VM dispatcher's jump table, 106 opcodes | OpenTPW.Files/Formats/Script/Opcode.cs  |
| `0x005569b8` | `SETOBJPARAM`'s type dispatch: jump table | OpenTPW/World/Ride/RideEffects.cs  |
| `0x005569c4` | `SETOBJPARAM`'s type dispatch: byte map | OpenTPW/World/Ride/RideEffects.cs  |
| `0x00556a5c` | `COAST`'s eight-entry sub-op table | OpenTPW/World/Ride/RideState.cs  |
| `0x005587f0` | | OpenTPW.Files/Formats/Script/RideScriptFile.cs  |
| `0x00558d2e` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d5b` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d68` | | OpenTPW/VM/RideScript.cs  |
| `0x0055924b` | Script death: the sound-script arm, first of three identical arms | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x0055928c` | Script death: the child arm | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x005592cd` | Script death: the arm that tells the parent | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x005597a0` | the `RSSE` arm of the restore chain: reads each script's whole 244-byte struct back from the file, program counter included, so a loaded park's scripts resume mid-flight | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW.Files/Formats/Save/ParkThingStates.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkRides.cs OpenTPW/World/Ride/RideEffects.cs  |
| `0x0056695c` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00579e00` | | OpenTPW/Render/Assets/Asset.cs  |
| `0x0057c620` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x00580320` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00582170` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x00587380` | | OpenTPW/UI/LoadingScreen.cs  |
| `0x00587479` | | OpenTPW/UI/LoadingScreen.cs  |
| `0x005879c0` | | OpenTPW/UI/LoadingScreen.cs  |
| `0x00587a70` | | OpenTPW/UI/LoadingScreen.cs  |
| `0x00587c80` | | OpenTPW/Render/Assets/Asset.cs  |
| `0x00587db0` | | OpenTPW.Files/Formats/Sprite/SpritePackFile.cs OpenTPW/Render/Assets/Asset.cs  |
| `0x00588660` | | OpenTPW.Files/Formats/Sprite/SpritePackFile.cs  |
| `0x00591430` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x00598960` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x00598b20` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00598bf0` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x00598ca0` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x00599050` | | OpenTPW/Client/GameOptions.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005994e0` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005996d0` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00599880` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Advisor/ClipSequence.cs  |
| `0x005accf0` | | OpenTPW/Client/Players.cs  |
| `0x005aef50` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af530` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs  |
| `0x005af680` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x005af740` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af810` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af940` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afb00` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afc30` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005afc60` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afd70` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005aff50` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW.Files/Formats/Save/RecordStream.cs  |
| `0x005b09c0` | A park's record: found or made, and answered only while its global.sam loads (`0x005b11c0`) | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005b0cc0` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005b0d70` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005b11a0` | | OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005b11c0` | Loads a park's `data\levels\%s\global.sam`; 0 when it will not load | OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005b5cc0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005c5250` | | OpenTPW/Client/SaveFolder.cs  |
| `0x005c7590` | | OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs  |
| `0x005c7bb0` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x005c7de0` | | OpenTPW/Client/SaveFolder.cs  |
| `0x005c7e90` | | OpenTPW/Client/Players.cs  |
| `0x005c7f40` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs  |
| `0x005c8190` | | OpenTPW/Client/SaveFolder.cs  |
| `0x005c83b0` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/GameOptions.cs OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x005c8650` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005c8a10` | | OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005d58b0` | The lobby's root control's callback: hands every message on to `0x005d5dd0`, and so to `IslandLobby_OnKey` | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/WindowStack.cs  |
| `0x005d5970` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005d5db0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005d5f80` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x005d6060` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005d6070` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x005d60c0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x005d6110` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005d8bac` | | OpenTPW/World/Level.cs  |
| `0x005d96fc` | | OpenTPW/World/Sky.cs  |
| `0x005dd034` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005dfd2d` | Lobby camera constructor: the leave state `+0x14` set to 0 | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e0470` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x005e052f` | Lobby camera state 2: writes the render camera's darkening target `+0x60` as the radius closes | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e06e4` | Lobby camera state 1's arrival: plays the gate's M1 once (`0x005d83f0( 0, 0 )` on `island+8`) | OpenTPW/World/Lobby/LobbyGate.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1100` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x005e13fb` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1890` | The island camera's `+0x18`, Escape: cancels a leave (state 1 or 2 to 0) and shows the panel; answers 1 only when it cancelled | OpenTPW.Tests/LobbyEscapeTests.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e18ab` | The cancel's gate clip: M2 once on `island+8`, from state 2 only | OpenTPW/World/Lobby/LobbyGate.cs  |
| `0x005e1a44` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x005e1bd0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x005e1cc0` | `IslandLobby_EnterPark`, the island camera's `+0x40`: leaving, no island, Instant Action straight in, then the keys held against the cost | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1ce0` | `IslandLobby_EnterPark`'s first test: the camera state `+0x14` is nought | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1da3` | `IslandLobby_EnterPark` returns for a park with no record - its global.sam would not load | OpenTPW.Tests/LobbyKeysOnReleaseTests.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005e1e30` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/Lobby/LobbyAudio.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1e50` | The fly-in's arrival (`+0x48`): takes the park from the current island, sets the scene's choice to 2 | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1ee0` | Island arrow handler, **next** (`+0x38`): refuses while the camera is leaving (`+0x14`), then in Instant Action; `lobby.md` "The island keys wait for the fly-in" | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1ee3` | Next-island handler's first test: `[this+0x14]` non-zero returns | OpenTPW.Tests/LobbyIslandKeysTests.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1f40` | Island arrow handler, **previous** (`+0x3c`): the same two refusals | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1fa0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e2310` | The island camera's `+0x14`: on a key's release Enter is Enter this park, `0x2700`/`0x2500` the next/previous island; a left press is Enter this park | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005e234f` | `0x005e2310`'s press arm: button 0 calls Enter this park (`+0x40`) | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005e3210` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x005e4140` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005e41c0` | `IslandLobby_OnKey`: on Escape's release asks every active child's `+0x18`, and opens the game menu only if none answered | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/WindowStack.cs  |
| `0x005e4207` | | OpenTPW/Global/GameClock.cs OpenTPW/UI/UiWindow.cs OpenTPW/World/Level.cs  |
| `0x005e791b` | | OpenTPW.Files/Formats/Sign/SignFile.cs  |
| `0x005ec3d2` | | OpenTPW.Files/Formats/Sign/SignFile.cs  |
| `0x005ecd09` | | OpenTPW.Files/Formats/Sign/SignFile.cs OpenTPW/World/Lobby/SignTexture.cs OpenTPW/World/Park/ParkObjects.cs  |
| `0x005ecd18` | | OpenTPW.Files/Formats/Sign/SignFile.cs OpenTPW/World/Park/ParkObjects.cs  |
| `0x005ed5a0` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005ed770` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005ed920` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005edac0` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005f0ad0` | | OpenTPW/UI/Park/ParkMapScreen.cs  |
| `0x005f5fa0` | The sound clock: wall-time milliseconds | OpenTPW/World/Park/ParkAudio.cs  |
| `0x005f8ae0` | | OpenTPW/Client/GameDir.cs  |
| `0x006584df` | | OpenTPW/UI/WindowStack.cs  |
| `0x00658f97` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00659a58` | | OpenTPW/UI/UiMesh.cs  |
| `0x0065d3a3` | | OpenTPW/UI/UiControl.cs  |
| `0x0065da8d` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x0065fd0e` | | OpenTPW/UI/UiWindow.cs  |
| `0x0065fd58` | | OpenTPW/UI/UiControl.cs  |
| `0x0065fe75` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x006662b7` | | OpenTPW/UI/UiControl.cs  |
| `0x0066656c` | | OpenTPW/UI/UiControl.cs  |
| `0x006677ae` | | OpenTPW/UI/UiControl.cs  |
| `0x0066781a` | | OpenTPW/UI/UiControl.cs  |
| `0x00667833` | | OpenTPW/UI/UiControl.cs  |
| `0x00667fee` | The edit box's key-up handler: Enter (`0x0d`) sends `0x802`, Escape `0x804`, Tab `0x803` | OpenTPW/UI/WindowStack.cs  |
| `0x00668820` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x006698e6` | The UI queue pop: hands each key message to one control only - an accelerator's target, else the focus if visible, else the last control any press reached, if visible | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/WindowStack.cs  |
| `0x0066a1b0` | | OpenTPW/UI/UiControl.cs  |
| `0x0066a1e8` | | OpenTPW/UI/UiControl.cs  |
| `0x0066a295` | | OpenTPW/UI/UiControl.cs  |
| `0x0066acf9` | | OpenTPW/UI/UiControl.cs  |
| `0x0066af19` | | OpenTPW/UI/UiControl.cs  |
| `0x0066b088` | | OpenTPW/UI/UiControl.cs  |
| `0x0066b47d` | | OpenTPW/UI/UiControl.cs  |
| `0x0066b8aa` | | OpenTPW/UI/UiControl.cs  |
| `0x0066ba22` | | OpenTPW/UI/UiControl.cs  |
| `0x0066bb9b` | | OpenTPW/UI/UiControl.cs  |
| `0x0066c5a4` | A polygon region's contains test: the crossings count, in whole virtual units | OpenTPW/UI/UiControl.cs  |
| `0x0067a830` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006b0680` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b15f0` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b4aa0` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b88d7` | The other read of `+0xc`: a play replaces a live handle only for a higher priority | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006b8930` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x006bb9f9` | The effect record's `+0xc` copied into the voice's priority - `audio.md` | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006bc2d0` | A variation's wait between a held voice's samples: `min + LCG % (max - min)` | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bcc71` | A held voice's fade step taking the no-channel exit, so its stop is a cut | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bd9b0` | A held voice's stop: its own children only | OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdae0` | A held voice's tick: extend the chain, prune it | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdb50` | A held voice's chain extension | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdb88` | Make a child once the clock passes the newest child's time | OpenTPW.Tests/ParkScreamChainTests.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdc3e` | A pruned child deleted without its channel being stopped | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c0314` | The SFX.map header word that says the weights are shares | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006c0510` | The SFX.map loader's walk: effects, variation headers, samples, zones | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006c0676` | Running-total variation weights turned into shares at load | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006c3a80` | A held voice's child: variation, sample, and the time of the next | OpenTPW/Audio/SoundCategory.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c3d80` | Whether a variation takes its wait from the voice's parameter (mask `+0x18` bit 4) | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c3e00` | A held voice's next variation: the first first, then by the zones and the parameter | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c3e1c` | The held voice's parameter byte read for the variation pick | OpenTPW/World/Park/ParkAudio.cs  |
| `0x006c3f49` | No zone covers the parameter: the chain is parked | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c581b` | The one call site of QMixer's SetDistanceMapping, gated on a request bit that nothing writes | OpenTPW/Audio/Audio.cs OpenTPW/Audio/AudioListener.cs  |
| `0x006e71b4` | | OpenTPW.Tests/PeepHeadingTests.cs OpenTPW/World/Park/PeepHeading.cs  |
| `0x006fe6bc` | | OpenTPW.Tests/RideAnimationsTests.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x006febe4` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006febec` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006fec00` | The constant 0.01 in the lobby path sampler's `percent * 0.01 * segments` | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x006fec08` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW.Tests/RideAnimationsTests.cs OpenTPW/World/Ride/AnimTimeControl.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x006fecb8` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x007005b8` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007005c0` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007007a4` | | OpenTPW.Tests/ParkBoardingTests.cs OpenTPW/World/Park/Peep.cs  |
| `0x00700848` | Double: the rest one turn of staff walking costs, before the grade multiplier | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00700850` | Double: the same for mood | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00701720` | | OpenTPW/Render/Assets/Texture.cs  |
| `0x00701f50` | | OpenTPW/World/Sky.cs  |
| `0x00701f54` | | OpenTPW/World/Sky.cs  |
| `0x00701f58` | | OpenTPW/World/Sky.cs  |
| `0x00701f5c` | | OpenTPW/World/Sky.cs  |
| `0x00701f64` | | OpenTPW/World/Sky.cs  |
| `0x00701f7c` | | OpenTPW/World/Lobby/LobbyScript.cs OpenTPW/World/Sky.cs  |
| `0x0070200c` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x007029cc` | | OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00702ae4` | | OpenTPW/World/Lobby/LobbyFlyer.cs  |
| `0x00702c78` | Lobby camera: adds 0.05 of the look-speed cap per frame, with no delta | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c7c` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c84` | Lobby camera: subtracts 0.05 of the cap per frame | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c8c` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00702c94` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00702ca4` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x0070a2e0` | vtable of the held-chain voice class (flags `0x0404`) | OpenTPW/World/Park/ParkScreams.cs  |
| `0x007396c8` | The `Info.Shape` alphabet, 19 rows of `{char, kind, bit}` | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkEntryCellTests.cs  |
| `0x00741c8c` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00741d40` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00742178` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00744e3c` | The compiled item schema's `HasQueue` entry, after `IsChoosable`: descriptor `+0x40` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0074c9c4` | Float 9.999: where the camcorder sweep parks or puts back an axis going positive, `cell * 10 + 9.999` | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0074c9c8` | Float 1.01: the camcorder sweep's tie-break | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0074c9d0` | Float 0.001: the camcorder sweep's nudge after an open crossing that left the cell unchanged | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0074cf58` | | OpenTPW.Tests/LobbyModelAnimationTests.cs  |
| `0x0074f920` | | OpenTPW/UI/Screens/MessageBox.cs  |
| `0x0074fa98` | | OpenTPW/UI/Park/ParkGadget.cs OpenTPW/UI/Park/ParkViewfinder.cs  |
| `0x0074fb50` | Status code to colour, for the object windows and the all-items rows (`FUN_00485f60`'s codes) | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x007501a0` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x007506c8` | Layout stream of the allpeeps (visitors) screen | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x007508e0` | Layout stream of the allitems screen's frame | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00750ab8` | allitems: the five-column list tree (rides, shops) | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00750ba0` | allitems: the six-column list tree (sideshows) | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00750ca0` | allitems: the two-column list tree (miscellaneous items) | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00750e10` | Layout stream of the allstaff screen | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkStaffScreen.cs  |
| `0x00751720` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00751798` | Layout stream of the entryprice screen | OpenTPW/UI/Park/ParkEntryPriceScreen.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x00751fa8` | Layout stream of the hire screen | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkHireScreen.cs  |
| `0x00752588` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x00752940` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f10` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f24` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f30` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00753c68` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x00753f50` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x007540c0` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x007540cc` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x00754cf8` | Layout stream of the buy screen | OpenTPW/UI/Park/ParkBuyScreen.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x00755150` | Layout stream of the ride object window | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x00757f60` | Stream: the island panel, its root's 23-point outline included | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x0075d0f8` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x0075d178` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007622b0` | | OpenTPW/World/Park/CellLine.cs OpenTPW/World/Park/MapStep.cs  |
| `0x0076338c` | The queue pieces table, twelve bytes a record | OpenTPW/World/Park/ParkQueues.cs  |
| `0x00763b38` | The 20-entry marker texture table (`blue`, `red`, ... `m_link`, `m_end`) | OpenTPW/World/Park/ParkBuildMarkers.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00763f88` | Sprite kinds table: fourteen bare kind names | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00764030` | The four kid banks `Sprites_LoadFolder` loads before its sweep | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00765280` | The opcode table `{name*, operandCount*}`, eight bytes a record | OpenTPW.Files/Formats/Script/Opcode.cs  |
| `0x00768ab4` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00768fb8` | `AdvisorResponseTable` - see `advisor-park.md` | OpenTPW/Client/Diagnostics/DebugConsole.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/Park/ParkLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0076dc18` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0076e300` | | OpenTPW/UI/Park/ParkLines.cs  |
| `0x00774c18` | The lobby's full-screen root control that `FrontEnd_Init` loads (callback `0x005d58b0`) | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x00774ce0` | | OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00774da0` | | OpenTPW/UI/Park/ParkMapScreen.cs  |
| `0x0077c488` | Whether the interface posts input: set as the window comes active, cleared as it goes (WM_ACTIVATEAPP) | OpenTPW/Client/Renderer.cs  |
| `0x00785058` | `PeepInfo.SmallHappinessChange`, 5 in Lost Kingdom, read as a byte | OpenTPW.Tests/ParkMoodChangeTests.cs OpenTPW/World/Park/ParkAdmission.cs  |
| `0x0078505c` | `PeepInfo.MediumHappinessChange`, 15 in Lost Kingdom, read as a byte | OpenTPW.Tests/ParkMoodChangeTests.cs OpenTPW/World/Park/ParkAdmission.cs  |
| `0x00785060` | `PeepInfo.BigHappinessChange`, 25 in Lost Kingdom, read as a byte | OpenTPW.Tests/ParkMoodChangeTests.cs OpenTPW/World/Park/ParkAdmission.cs  |
| `0x007854f4` | | OpenTPW/Global/GameCalendar.cs  |
| `0x00785914` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00785970` | The game's global clock object; `GameClock_Pause`/`Resume` act on it and `Game_Pause` freezes it | OpenTPW/Global/GameCalendar.cs OpenTPW/Global/GameClock.cs OpenTPW/VM/RideScript.cs  |
| `0x00786b84` | | OpenTPW/Global/GameClock.cs  |
| `0x00786b90` | | OpenTPW/Client/SaveFolder.cs  |
| `0x00786ba4` | | OpenTPW/Global/GameClock.cs OpenTPW/UI/UiWindow.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0078d8d8` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW/Client/GameOptions.cs  |
| `0x0078d90e` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x007cb2fc` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x007cc4b8` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x007cdba0` | The static initialisers of the eight ring-order directions | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x00803a20` | Sound category slot: ambient | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a24` | Sound category slot: kids | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a28` | Sound category slot: rides | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a2c` | | OpenTPW/UI/UiSounds.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a30` | Sound category slot: staff | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0080ced8` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x0080cef8` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0081b740` | The pending list of run ends Backspace pops; count `DAT_00820a8c` | OpenTPW.Tests/ParkBuildModeTests.cs OpenTPW/World/Park/ParkBuildMode.cs  |
| `0x00877d34` | | OpenTPW/Global/GameCalendar.cs OpenTPW/Global/GameClock.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x00878128` | | OpenTPW/Global/GameClock.cs  |
| `0x008786bc` | The per-frame clock sample the three beat fractions share | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00878a1c` | The peep beat's baseline, re-stamped at `0x0054f683` | OpenTPW/World/Park/ParkPeople.cs  |
| `0x008bcbcc` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00f82884` | The lobby's front-end object pointer; the message box's pause test wants it gone | OpenTPW/Global/GameClock.cs OpenTPW/World/Level.cs  |
| `0x00fb1f20` | The sound engine's one random seed | OpenTPW/World/Park/ParkScreams.cs  |
