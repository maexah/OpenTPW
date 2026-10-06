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
| `0x0040153a` | | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00401d8b` | | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00402938` | Shape reader `FUN_00402720`: start of the row swap that turns a picture upside down | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkEntryCellTests.cs  |
| `0x004029ab` | Shape reader: end of the row swap | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkEntryCellTests.cs  |
| `0x00402d70` | | OpenTPW/Global/GameCalendar.cs OpenTPW/VM/RideScript.cs  |
| `0x00402d90` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x00402db0` | | OpenTPW/Global/GameClock.cs  |
| `0x00402e6b` | FUN_00402e60, the save's clock snapshot: +0x4c = the clock's reading, the first dword of KOLC | OpenTPW.Files/Formats/Save/ParkClock.cs  |
| `0x00402ea0` | | OpenTPW/Global/GameClock.cs  |
| `0x00402f10` | | OpenTPW/VM/RideScript.cs  |
| `0x00403030` | | OpenTPW/Global/GameClock.cs  |
| `0x00403050` | | OpenTPW/Global/GameClock.cs  |
| `0x004030d0` | | OpenTPW/VM/RideScript.cs  |
| `0x004031fd` | FUN_004031f0: +0x48 = +0x4c - FUN_00402f10(), so the clock reads the saved reading | OpenTPW.Files/Formats/Save/ParkClock.cs  |
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
| `0x0040c4b0` | Game table row 4's handler (key 0x72, F3): the thunk to 0x00481490, a bare JMP to the full-screen toggle FUN_004a29d0 | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x0040c4d0` | | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x0040c5c0` | | OpenTPW.Tests/ParkCamcorderKeyOnReleaseTests.cs OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x0040c5d0` | The system table's Ctrl+H handler, Popup Help, run on the key's release by the window procedure | OpenTPW/UI/HelpBar.cs  |
| `0x004134f5` | Item loader `FUN_00413410`: descriptor `+0x4ac` stored as a copy of `+0x4c`, `Info.WhichUIType` | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x00413ac4` | | OpenTPW/World/Park/ParkItemCatalogue.cs  |
| `0x00413f18` | | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW/World/Park/ParkItemCatalogue.cs  |
| `0x00413ffe` | | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00415193` | FUN_00415140, the post-load rebuild: FUN_00402e80 makes both clocks read their saved KOLC readings | OpenTPW.Files/Formats/Save/ParkClock.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x00415270` | the whole-game restore chain: seventeen modules in order, each checked against a four-character tag that follows it | OpenTPW.Files/Formats/Save/ParkScriptStates.cs  |
| `0x004162c2` | | OpenTPW.Files/Formats/Save/SaveReader.cs  |
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
| `0x0042a8bd` | FUN_0042a760, a right press: bit 4 of the camera's button word DAT_00790aac set | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042a8fb` | FUN_0042a760, a right release: bit 4 of DAT_00790aac cleared | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042af85` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x0042afba` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x0042b925` | First-person camera update: the right button's walk tests the button bit (EDI) and the walking flags (EDX), never RMB cancel | OpenTPW.Tests/ParkFirstPersonRightButtonWalkTests.cs  |
| `0x0042b935` | First-person camera update: a held right press (DAT_00790aac & 4) adds the Up arrow's 0.1 to the forward term | OpenTPW.Tests/ParkFirstPersonRightButtonWalkTests.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
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
| `0x0042c587` | The camcorder sweep, after every pass: FUN_0042a340 at the pass's cell; a ride found runs Ride it! from first person and ends the sweep | OpenTPW.Tests/ParkCamcorderWalkTests.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0042d130` | | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x004311f0` | FUN_00430ed0, a coaster car's update: FUN_00473c70 advances the car's model; a layout replayed inside the 31 ms loop reaches it | OpenTPW/VM/RideScript.cs  |
| `0x004380e7` | SAOC loader: node +0x3c bit 0, the circuit closed, from the saved header's flag bit 0 | OpenTPW.Files/Formats/Save/ParkCoasters.cs OpenTPW/World/Park/ParkRideChoice.cs  |
| `0x004382b3` | SAOC loader: node +0x3c bit 1, a gap open, from the saved header's flag bit 1 | OpenTPW.Files/Formats/Save/ParkCoasters.cs  |
| `0x0043837d` | SAOC loader: node +0x140, the clash count, restored from header +0x1c last | OpenTPW.Files/Formats/Save/ParkCoasters.cs OpenTPW/World/Park/ParkRideChoice.cs  |
| `0x0044a904` | FUN_0044a870: a lookup record gets a matrix only when its file flags carry 0x10 or 0x20 (TEST byte [rec],0x30) | OpenTPW/World/Ride/RideNodes.cs  |
| `0x0044abf2` | Pose walk FUN_0044ab30: a record whose file flags meet 0x40040 takes its position from a face of its parent mesh while that mesh carries 0x200000 | OpenTPW/World/Lobby/LobbyModel.cs OpenTPW/World/Ride/RideNodes.cs  |
| `0x0044ac00` | | OpenTPW/World/Lobby/MeshAnimator.cs  |
| `0x0044ac2a` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x0044ad11` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x0044b220` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x0044b226` | Node lookup FUN_0044b220: the mask tested against 0x3da1f83 | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x0044b22e` | Node lookup FUN_0044b220: a mask sharing no bit with 0x3da1f83 replaced by 0x3da1f82 | OpenTPW.Files/Formats/Model/ModelFile.cs OpenTPW.Tests/RideNodesTests.cs  |
| `0x0044b2e0` | | OpenTPW/World/Advisor/AdvisorModel.cs  |
| `0x0044ba1f` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x0044e4b7` | FUN_0044e410( 3 ), the off-screen sweep: FUN_00473c70 called with flags 8 (no rest pose, no hide list) | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00452bb0` | | OpenTPW/World/Park/ParkItemHeights.cs  |
| `0x00452c1d` | | OpenTPW/World/Park/ParkItemHeights.cs  |
| `0x0045aa5a` | | OpenTPW/Client/Players.cs  |
| `0x0045aa74` | | OpenTPW/Client/Game.cs OpenTPW/Client/Players.cs  |
| `0x0045aab4` | | OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x0045acfc` | | OpenTPW/Client/Game.cs  |
| `0x004623b3` | | OpenTPW.Tests/LobbyModelAnimationTests.cs OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x004623df` | | OpenTPW.Tests/LobbyModelAnimationTests.cs OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00462bd1` | Item loader FUN_004629d0: with flag 0x20000, the second model set "p%s" loaded into the record's +0xd0 | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x00462d4a` | Item loader FUN_004629d0: runtime bit 8 on every lookup record of an item loaded under 0x400000 (Info.DoHeadProcessing) | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00463060` | the build path: checks role 0 exists, triggers it, then starts role 13 at once, which holds that clip at frame nought. A newly built thing's script, run from word 0, plays it; a loaded one's resumes past it and its channels come back from the save | OpenTPW/World/Park/ParkRides.cs  |
| `0x004646a1` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004647a0` | the `RSYS` arm of the restore chain: overwrites every animation channel from the saved record and restores the per-node flag words with it, which is what stops a loaded park's things standing frozen | OpenTPW.Files/Formats/Save/ParkThingStates.cs  |
| `0x00464bcb` | FUN_004647a0, the RSYS restore: a saved channel's eleven dwords copied onto the channel, from | OpenTPW.Files/Formats/Save/ParkThingStates.cs OpenTPW.Tests/ParkScriptStateTests.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x00464bdb` | FUN_004647a0: the three saved stamps onto +0x10, +0x14 and +0x18, from | OpenTPW.Tests/AnimTimeControlTests.cs OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00464bec` | FUN_004647a0: the three stamps, to here | OpenTPW.Tests/AnimTimeControlTests.cs OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00464c17` | FUN_004647a0: the copy, to here (the queue +0x24..+0x30 last) | OpenTPW.Files/Formats/Save/ParkThingStates.cs OpenTPW.Tests/ParkScriptStateTests.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x0046717c` | Placement FUN_00467030: the sine table index, FISTP of the float turn times 651.89862; the cosine's is rounded apart at 0x00467194 | OpenTPW/World/Ride/RideNodes.cs  |
| `0x00467d00` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00467d60` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00468a64` | FUN_004689f0: FUN_00468950 looks for sign1 and sign2 in the item's model and its P model; all four found, the instance takes the item's boards | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x00468e11` | FUN_004689f0: the preview instance triggers role 5 (M), entry 0, looped, speed 1.0 | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x00468e2f` | FUN_004689f0: the preview's first angle, an x87 intrinsic's answer (FUN_0067b24a), operands not traced | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x0046a82c` | | OpenTPW/World/Park/ParkObjects.cs  |
| `0x0046a838` | | OpenTPW/World/Park/ParkObjects.cs  |
| `0x0046b600` | | OpenTPW.Common/Client/Window.cs  |
| `0x0046bb0b` | Window procedure `FUN_0046b600`: `GetKeyState` for Shift, Ctrl and Alt at each key, before `UI_PostKey` | OpenTPW.Tests/ParkCamcorderKeyOnReleaseTests.cs OpenTPW.Tests/ParkEscapeOnReleaseTests.cs OpenTPW.Tests/ParkFullScreenViewTests.cs OpenTPW/Global/Input.cs  |
| `0x0046c480` | Place-staff mode (type 5, vtable `0x006fea40`) MOVE: carries the candidate's sprite under the pointer and draws a red square over a cell the click would refuse | OpenTPW/World/Park/ParkStaffPool.cs  |
| `0x0046c730` | Place-staff mode OnInstall: carry cursor 9, and a sprite of the candidate's kind in their costume | OpenTPW/World/Park/ParkStaffPool.cs  |
| `0x0046c9b9` | Place-staff FUN_0046c8e0: the candidate's costume byte (+0x11 of the hand's record) pushed to the constructor, here the handyman's | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0046cdc0` | Place-worker mode (type 6) OnUninstall: when the hand still names a worker, puts them down on their own current cell with the drop's body | OpenTPW.Tests/ParkHandTests.cs OpenTPW.Tests/ParkLeaveTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004702ea` | FUN_004702d0, local x parent: each element the third product plus the second plus the first; the translation row adds the parent's last | OpenTPW/World/Ride/RideNodes.cs  |
| `0x00470e90` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004711d0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471860` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471c73` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW.Tests/AnimationEasingTests.cs  |
| `0x00471c83` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471d32` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00472bff` | FUN_00472bc0, a fresh start: +0x10, +0x14 and +0x18 stamped from the frame's clock snapshot DAT_007b496c | OpenTPW/VM/RideScript.cs  |
| `0x00472cf4` | FUN_00472cb0, the restore's clip bind: +0x20 = the saved span x 0.03 DIVIDED by the speed | OpenTPW.Tests/AnimTimeControlTests.cs OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00472f60` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00472fdd` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x0047308f` | FUN_00472f60's start: an entry past a loaded role's clips read from past the table, the role standing | OpenTPW/World/Ride/RideAnimations.cs  |
| `0x00473193` | FUN_00472f60, the hold (role 14): AnimTime set a whole clip past the start stamp | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004732a0` | The animation trigger: plays entry N of role R on a model channel, once or looped, queued behind a clip still part-way through unless the flags carry `0x2` (`park.md`); the lobby's gate and isle reach it through `0x005d83f0` with role 5, M (`lobby.md`, "Escape cancels the fly-in") | OpenTPW/World/Lobby/LobbyGate.cs  |
| `0x004732e5` | FUN_004732a0: the first of its seven take-over gates (to 0x00473344) | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00473315` | FUN_004732a0: 'finished' judged on +0x20 as the last per-frame advance left it, no refresh | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x0047337b` | | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/World/Ride/AnimTimeControl.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004733b1` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004733cc` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW.Tests/RideScriptModelTests.cs  |
| `0x004733d6` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004733db` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004733e7` | | OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004736e7` | Channel advance FUN_004735d0: a held channel re-pinned, AnimTime a whole clip past the start | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004738a3` | | OpenTPW.Tests/AnimTimeControlTests.cs OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x00473f32` | FUN_00473e30: model+4 gains 0x10 when no role past nought has clips (the stall-and-stop ending) | OpenTPW/World/Ride/RideAnimations.cs  |
| `0x00474070` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00474840` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00474bf0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00474cc0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x0047509d` | Sprite VM FUN_00475010: the end word 0x005da3c0 spotted rather than called; state +0x18 = 4, from | OpenTPW/World/Park/SpriteScript.cs  |
| `0x004750af` | FUN_00475010: +0x114 zeroed at the end word, to here | OpenTPW/World/Park/SpriteScript.cs  |
| `0x00475a71` | Sprite maker FUN_00475a10: the kind's bank count read for its one assert, that the kind has a bank at all | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00475c23` | FUN_00475b80 handed an address: the loop stack +0x1c reset to 0x14 and +0x70/+0x74/+0x78 zeroed, from | OpenTPW/World/Park/SpriteScript.cs  |
| `0x00475c3e` | FUN_00475b80: the reset, to here; picture, alpha and due time kept | OpenTPW/World/Park/SpriteScript.cs  |
| `0x004762b0` | | OpenTPW/World/Park/SpriteScript.cs  |
| `0x004763b0` | Sprite op LoopStart: pushes the pc past itself (FUN_00475230) and increments +0x78 | OpenTPW/World/Park/SpriteScript.cs  |
| `0x004763d0` | Sprite op LoopWhile, three operands (local, comparison, value): true jumps back and keeps the start | OpenTPW/World/Park/SpriteScript.cs  |
| `0x004764c2` | LoopWhile's comparison 8: CMP local,value / SETGE, signed | OpenTPW/World/Park/SpriteScript.cs  |
| `0x00476673` | LoopWhile: false pops the start and decrements +0x78 | OpenTPW/World/Park/SpriteScript.cs  |
| `0x00476678` | LoopWhile's comparison jump table, eight entries | OpenTPW/World/Park/SpriteScript.cs  |
| `0x004767e0` | Sprite op SubLocal, two operands: local minus a value | OpenTPW/World/Park/SpriteScript.cs  |
| `0x0047682f` | SubLocal: the integer SUB for the locals past the six floats | OpenTPW/World/Park/SpriteScript.cs  |
| `0x0047698c` | Sprite op Frame: a frame of -1 writes +0x114 = 0, hiding the sprite, and still yields | OpenTPW/World/Park/SpriteScript.cs  |
| `0x00476c50` | | OpenTPW/UI/UiMesh.cs  |
| `0x00476e80` | | OpenTPW/UI/UiMesh.cs  |
| `0x00476f10` | | OpenTPW/UI/UiMesh.cs  |
| `0x00477310` | | OpenTPW/UI/UiMesh.cs  |
| `0x0047ed80` | | OpenTPW/UI/UiWindow.cs  |
| `0x0047eda3` | UI_LoadModalTree: the full-screen control a modal window is loaded into, over the park's layer | OpenTPW.Tests/ParkHandTests.cs  |
| `0x0047eed0` | | OpenTPW/UI/Screens/MessageBox.cs  |
| `0x0047f020` | | OpenTPW/UI/Screens/MessageBox.cs OpenTPW/UI/UiWindow.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0047f251` | | OpenTPW/World/Level.cs  |
| `0x004813c0` | | OpenTPW/UI/WindowStack.cs  |
| `0x00481a2b` | Camcorder button `FUN_00481a10`, its first call: FUN_00485b40 closes the open park screen before the mode is built | OpenTPW.Tests/ParkScreenTests.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x00481ad0` | Camcorder button `FUN_00481a10`: installs the camcorder mode through the setter, letting go of the hand | OpenTPW.Tests/ParkHandTests.cs  |
| `0x00485780` | | OpenTPW/UI/ButtonGlint.cs OpenTPW/UI/UiControl.cs OpenTPW/UI/UiSounds.cs OpenTPW/UI/WindowStack.cs  |
| `0x00485a70` | The font in a slot of the current set: none from slot 13 up, by an unsigned compare (`lobby.md`, "Meshes and fonts") | OpenTPW.Tests/UiFontsTests.cs OpenTPW/UI/UiFonts.cs  |
| `0x00485cc8` | FUN_00485b70, a park screen opening: the layer's cursor set to 0, the plain arrow (FUN_004a2aa0) | OpenTPW/World/Level.cs  |
| `0x00485ccd` | FUN_00485b70, a park screen opening: the camera table switched off and its latches cleared (FUN_0040cb50); the cheat table's follows | OpenTPW.Tests/ParkScreenTests.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x00485ce6` | FUN_00485b70, a park screen opening: the game table switched off | OpenTPW.Tests/ParkScreenTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Level.cs  |
| `0x00485cf3` | FUN_00485b70, a park screen opening: the gadget's arm folded, FUN_004a25f0( 1 ) | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00485d20` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00486bce` | | OpenTPW/Global/GameClock.cs  |
| `0x004873b3` | Hover category: a type-12 track cell under a type-25 parent gets no category | OpenTPW/World/Level.cs  |
| `0x00488234` | Park_MouseMessageProc, the tick 0x1e with a quick click armed: FUN_0065968e, the real-time clock, less the press's stamp | OpenTPW.Tests/ParkHandTests.cs  |
| `0x0048823f` | Park_MouseMessageProc: more than 200 ms (0xc8) since the right press disarms the quick click | OpenTPW/World/Level.cs  |
| `0x00488290` | Park_MouseMessageProc hands its message to the camera's FUN_0042a760, as the viewfinder layer's handler does | OpenTPW.Tests/ParkFirstPersonRightButtonWalkTests.cs  |
| `0x004882ba` | Park mouse proc: a right press with RMB cancel on takes the mouse capture | OpenTPW/UI/WindowStack.cs  |
| `0x0048833a` | Park mouse proc: a right press with RMB cancel on arms the quick click (DAT_007c2500 = 1) | OpenTPW.Tests/ParkHandTests.cs OpenTPW/UI/WindowStack.cs  |
| `0x0048836f` | Park_MouseMessageProc, the right release: reads the armed flag and no clock | OpenTPW/World/Level.cs  |
| `0x0048842b` | Park mouse proc: a quick right click with RMB cancel on installs the idle mode over whatever mode is current | OpenTPW.Tests/ParkHandTests.cs OpenTPW/World/Level.cs  |
| `0x00488569` | Park_MouseMessageProc, the timer: the hover FUN_00486d90 only while no park screen is open | OpenTPW/World/Level.cs  |
| `0x004885a5` | Park_MouseMessageProc, a button's release: the camera table, the mode's move and its button-up slot, with no test of an open screen | OpenTPW/World/Level.cs  |
| `0x00488741` | Park_MouseMessageProc, a left or middle press: CMP [0x007c24c8], an open park screen leaves the case before the idle click and the mode's button-down slot | OpenTPW.Tests/ParkScreenTests.cs OpenTPW/UI/WindowStack.cs OpenTPW/World/Level.cs  |
| `0x0048884e` | Park_MouseMessageProc, the pointer's entry: the hover only while no park screen is open | OpenTPW/World/Level.cs  |
| `0x00488921` | `Park_MouseMessageProc` key-up case (`0x1000b`): the binding tables through `FUN_0040c990` - `scenes.md`, "The park Escape route" | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/WindowStack.cs  |
| `0x00488a00` | | OpenTPW.Tests/ParkCamcorderKeyOnReleaseTests.cs OpenTPW.Tests/ParkEscapeOnReleaseTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x00488aa1` | Layer 1 (first person) handler FUN_00488a00: its 0x10006 click case, right button and RMB cancel, leaves first person | OpenTPW.Tests/ParkFirstPersonRightClickTests.cs OpenTPW/UI/Park/ParkViewfinder.cs  |
| `0x00488aa8` | FUN_00488a00: the 0x10006 case's RMB cancel test | OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x00488bc6` | `FUN_00488ba0`, the park screens' key handler: a plain Escape let go closes the screen (message 4) | OpenTPW.Tests/ParkEscapeOnReleaseTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x00488bcb` | FUN_00488ba0, the key-up case: TEST EAX,0xff0000, so the Escape that closes a park screen carries no modifier | OpenTPW.Tests/ParkEscapeOnReleaseTests.cs  |
| `0x00488c13` | Park screen key handler FUN_00488ba0, a key up: runs the shortcuts table and no other | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/WindowStack.cs  |
| `0x00489ca0` | | OpenTPW/UI/WindowStack.cs  |
| `0x00489de1` | | OpenTPW/Client/Renderer.cs  |
| `0x0048b220` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x0048b2a0` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x0048b6a0` | | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x0048b977` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x0048bb36` | Park menu handler `FUN_0048b6a0`, key-up case: closes the menu on key `0x1b` whatever the modifiers, or on shortcuts row 0 | OpenTPW.Tests/ParkEscapeOnReleaseTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
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
| `0x0048ceca` | Object window base FUN_0048cea0: UI_LoadTree onto the park's layer 0 | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x0048d1a0` | Object windows' preview handler: a click 0x10006 of either button moves the camera to the window's thing and closes it | OpenTPW.Tests/ParkCameraToThingTests.cs OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x0048d1c0` | Preview handler's click arm: FUN_004867b0 on the thing shown, then the window's vtable +0x2c (close) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x0048f4a6` | | OpenTPW/UI/VirtualScreen.cs  |
| `0x0048f830` | | OpenTPW/UI/UiText.cs  |
| `0x0048ff8f` | The number painter FUN_0048fde0's handler: swprintf "%d" of the value, the ride window's Users last month | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x00491ab0` | | OpenTPW/UI/UiText.cs  |
| `0x00492180` | | OpenTPW/UI/HelpBar.cs  |
| `0x00492d80` | | OpenTPW/UI/Screens/GameMenu.cs OpenTPW/UI/UiSounds.cs  |
| `0x00492d8f` | MenuList_ChoiceCallback: a click 0x10006 of any button plays sound 0xc1 and chooses the row | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00492e80` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00492f60` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00493171` | MenuList_Show: closes the open park screen (FUN_00485b40) before the menu is shown | OpenTPW.Tests/ParkScreenTests.cs OpenTPW/UI/Screens/GameMenu.cs OpenTPW/UI/UiWindow.cs  |
| `0x00493270` | All-visitors handler, the 2000 ms timer 0x80083: each existing row rewritten in place by FUN_006644ea, no clear or scroll | OpenTPW.Tests/UiListTests.cs OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x00493483` | Visitors handler's 0x402 arm: the row's guest id, FUN_004867b0, then message 4 | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x004934c5` | Visitors handler: a right-clicked row (0x402) moves the camera to that guest (FUN_004867b0) | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x004934da` | Visitors handler: message 4 closes the screen after a right-clicked row | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x0049353e` | Visitors screen FUN_00493530: UI_LoadTree onto the park's layer 0 | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x0049383e` | All-visitors row adder FUN_00493800: Time In Park, the park time since the arrival stamp over 36,000,000,000 | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x00493850` | All-visitors row adder FUN_00493800: Rides Ridden is mNumRides +0x1c4 | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x0049385a` | FUN_00493800: the "?" column is the last thought +0x30 less one, 999 for none | OpenTPW/UI/Park/ParkVisitorsScreen.cs  |
| `0x00495554` | All-items handler's 0x402 arm: the row's thing id, FUN_004867b0, then message 4 | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00495584` | All-items handler: a right-clicked row (0x402) moves the camera to that thing (FUN_004867b0) | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00495599` | All-items handler: message 4 closes the screen after a right-clicked row | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00495abe` | All-items screen FUN_00495aa0: UI_LoadTree onto the park's layer 0 | OpenTPW/UI/Park/ParkItemsScreen.cs  |
| `0x00495feb` | All-staff handler's 0x402 arm: the row's member id, FUN_004867b0, then message 4 | OpenTPW/UI/Park/ParkStaffScreen.cs  |
| `0x0049602f` | All-staff handler: a right-clicked row (0x402) moves the camera to that member of staff (FUN_004867b0) | OpenTPW/UI/Park/ParkStaffScreen.cs  |
| `0x00496044` | All-staff handler: message 4 closes the screen after a right-clicked row | OpenTPW/UI/Park/ParkStaffScreen.cs  |
| `0x00496643` | All-staff screen FUN_00496620: UI_LoadTree onto the park's layer 0 | OpenTPW/UI/Park/ParkStaffScreen.cs  |
| `0x00498d33` | The entry-price screen's `b_door`: `FUN_00519ef0( down != 1, 0 )`, so down closes the park | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x00498db5` | Entry-price screen FUN_00498d80: UI_LoadTree onto the park's layer 0 | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x00498fc3` | The entry-price builder reads `mParkClosed` (`FUN_0051a280`) for the door switch | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x00498fd9` | The entry-price builder sets `b_door` down for a closed park (`Button_SetDown`) | OpenTPW/UI/Park/ParkEntryPriceScreen.cs  |
| `0x0049bf34` | Hire screen FUN_0049bdd0: UI_LoadTree onto the park's layer 0 | OpenTPW/UI/Park/ParkHireScreen.cs  |
| `0x004a0f05` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a0f8a` | FUN_004a0e30, the gadget's trend arrow: last month's cash in (+0x1fc94) against its total costs (+0x1f5a4), frame 1 when lower | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2057` | FUN_004a1d70: the aerial mast 0x2d's top set to its bottom less 8, so the mast is built eight high | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a23da` | FUN_004a1d70: the arm 0x21's right edge moved by its left less the handle's (-645), so the arm is built in | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2529` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a287d` | The full-screen view's cover handler 0x004a2840 hands its message to the camera's FUN_0042a760 | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x004a2905` | The full-screen view's control handler 0x004a2840, key up: the camera table, then F3 or a plain Escape off, else Ctrl+P the postcard | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x004a2918` | The full-screen view's handler, key up: FUN_0040c990 on the camera table alone | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x004a2938` | The full-screen view's handler 0x004a2840, a key up at the end of a park: a plain Escape opens the game menu | OpenTPW.Tests/ParkFullScreenViewTests.cs  |
| `0x004a2942` | The full-screen view's handler at the end of a park (world state 4): a plain Escape opens the game menu, the view stays on | OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x004a2990` | The full-screen view's handler 0x004a2840: game action 4 or a plain Escape turns the view off | OpenTPW.Tests/ParkFullScreenViewTests.cs  |
| `0x004a29e6` | FUN_004a29d0, off to on: refused while gui_CameraFlags & 0x16 (first person, a ride view) | OpenTPW.Tests/ParkFullScreenViewTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x004a2ac0` | FUN_004a2ac0( a ): message 6 with a to the park's layer 0 and 1 - a to layer 1; first person's entry passes 0 | OpenTPW.Tests/ParkHandTests.cs OpenTPW/UI/Park/ParkGadget.cs OpenTPW/World/Level.cs  |
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
| `0x004a6104` | Player slot callback 0x004a6000: SUB 0x10002 then SUB 4, the click 0x10006 of any button | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a61b0` | | OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a61d0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs OpenTPW/UI/Screens/MessageBox.cs  |
| `0x004a61e9` | Quit Game callback 0x004a61d0: the click 0x10006 of any button | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a6290` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a62b0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a6580` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a65a8` | `FrontEnd_ShowPlayerSlots` hides the lobby's root control | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a6a50` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x004a6a83` | `FrontEnd_ClosePlayerSlots` shows the lobby's root control again | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a6b80` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x004a6d00` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs OpenTPW/UI/WindowStack.cs  |
| `0x004a6e40` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x004ab023` | | OpenTPW/UI/Park/ParkBuyScreen.cs OpenTPW/World/Park/ParkResearch.cs  |
| `0x004ab063` | | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x004ab086` | | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x004ab386` | Buy screen fill FUN_004ab1b0: the footprint picture's cell, the surface's width and height each over 8 (SAR 3) | OpenTPW/UI/Park/ParkFootprintPicture.cs  |
| `0x004ab403` | Buy screen fill FUN_004ab1b0: the footprint squares' alpha byte, 0x80 | OpenTPW/UI/Park/ParkFootprintPicture.cs  |
| `0x004ab4c8` | Buy screen fill FUN_004ab1b0: a land row or a mystery ride clears the footprint surface and paints nothing | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x004ac42e` | Buy screen handler FUN_004ac270, the frame message 0x1e: a waiting row is shown by FUN_004ab1b0 and cleared | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x004ac438` | Buy screen handler FUN_004ac270, the frame message: FUN_0065968e less the row's stamp, shown past 500 ms | OpenTPW.Tests/ParkFootprintPictureTests.cs  |
| `0x004ac443` | Buy screen handler FUN_004ac270: the waiting row is shown only after more than 500 ms (CMP 0x1f4, JLE) | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x004aca16` | Buy screen handler FUN_004ac270, message 0x401: a row other than the waiting one is kept as waiting and stamped | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x004acca0` | Buy screen opener FUN_004acc70: with the screen already up it picks the tab again and returns | OpenTPW.Tests/ParkScreenTests.cs OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004acd62` | Buy screen FUN_004acc70: UI_LoadTree onto the park's layer 0, not modal | OpenTPW.Tests/ParkHandTests.cs OpenTPW/UI/Park/ParkBuyScreen.cs OpenTPW/UI/UiWindow.cs  |
| `0x004ad606` | The ride window sets its door down while `mCanLoad` is nought (`Button_SetDown`, from here) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ad622` | The ride window's door position (to here) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ad890` | The object windows' shared base, vtable `+0xc`: fills the stats table's labels | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ade7d` | FUN_004ade40, the ride window: Users last month sums the object's customers ring, +0x1a8, over its last min( filled, 30 ) finished days | OpenTPW/UI/Park/ParkObjectWindow.cs OpenTPW/World/Park/ParkObjectRings.cs  |
| `0x004ade83` | FUN_004ade40, Users last month: MOV EDI,0x1e, the thirty days asked for | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004adeb7` | FUN_004ade40, Users last month: the filled count compared with 30, FUN_00495d40 when fewer | OpenTPW.Tests/ParkSettleUpCountsTests.cs OpenTPW/World/Park/ParkObjectRings.cs  |
| `0x004adefd` | FUN_004ade40: each finished day added as an unsigned 32-bit figure (FILD qword), from | OpenTPW/World/Park/ParkObjectRings.cs  |
| `0x004adf20` | FUN_004ade40: the total __ftol'd, to here | OpenTPW/World/Park/ParkObjectRings.cs  |
| `0x004adf28` | FUN_004ade40: Users last month handed to its painter as a number through +0x1c | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004adf40` | FUN_004ade40, the object window: its Age is FUN_004dd670, printed signed | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004ae0da` | FUN_004ade40: Scrap value handed to its painter as a number through +0x1c | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004af440` | The object windows' shared base, vtable `+0x3c`: writes the three buffered values onto the ride | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004af63b` | Ride window handler FUN_004af600: message 0x10 with 0x80080, the 4000 ms timer's tick, refills the figures (FUN_004ade40) | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004af67b` | Ride window handler FUN_004af600, message 0x15: timer 0x80080 armed at 4000 ms | OpenTPW.Tests/ParkCameraToThingTests.cs OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004af871` | Ride window handler FUN_004af600, the door (0x3e38): the figures refilled, FUN_004ade40 | OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x004b8b70` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b8ca0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b8ea0` | Shows the island panel again: message 6 with 1 to its tree, then the mail badge rule `0x004bbbd0` | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004b8ee0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b9340` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b9840` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004cf3f6` | Arrival manager `FUN_004cf3e0`: `FUN_0041a990`'s elapsed count against the period `[0x00785314]`; `JBE` skips the call, so the count must be more than the period | OpenTPW.Tests/ParkPeopleTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf455` | Arrival manager: re-tests the offloading flag (`+0x10`) after a load is called, so the same call goes on to ask the vehicle | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf489` | Arrival manager FUN_004cf3e0: with a load held and no vehicle answering, the load's vehicle summoned by its size | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf4b6` | FUN_004cf3e0, its tail, every sweep: FUN_0051a9d0, then the vehicle's status asked and the trigger by who waits at the stop | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf4cb` | FUN_004cf3e0's tail, somebody at the stop: the vehicle's status asked; none, 0, or 2 with the load off summons or triggers | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf4de` | FUN_004cf3e0's tail, a leaver at the stop and the vehicle at 2: the load's still-to-drop [ESI+0xc] read; only nought sends it on | OpenTPW.Tests/ParkTickTests.cs  |
| `0x004cf526` | FUN_004cf3e0's tail: FUN_0051a2f0( 0 ), the summons at random, for a leaver with no vehicle current | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf533` | Arrival manager's tail, the arm with nobody at the stop (`FUN_0051a9d0` nought): lets the vehicle go at state 4 alone | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf56b` | Arrival manager, vehicle at 2: `JLE` on `mPeopleOnBus`; at nought or below the load is let go (`FUN_0041a960` re-marks `mTimeSig`, the flag cleared) | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf594` | Arrival manager: after dropping a guest, on to the tail at `0x004cf4b6`; the load is not let go on the drop's sweep | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf745` | Guest maker FUN_004cf720: FUN_004d8650 asked for stop B (argument 1) on both arms | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf75c` | FUN_004cf720: 0x100, two rows, off the packed cell while FUN_0051aad0 reports a vehicle other than the small crowd's | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004cf81a` | Bank constructor FUN_004cf7c0: mWithdrawalsEnabled +0x114 = 1; mLastBalance, mTurnEnteredRed, mProfitThisYear nought before it | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d01f3` | `FUN_004d01f0`, the bank's withdrawal: nothing at all while `mWithdrawalsEnabled` (`+0x114`) is nought | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d0205` | Withdrawal FUN_004d01f0: mBalance +0xc less the amount, from | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d020a` | FUN_004d01f0: JNS on the new balance; below nought with the old mLastBalance +0x11c not below, mTurnEnteredRed +0x120 = mGameTick, from | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d0222` | FUN_004d01f0: the red stamp, to here | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d0251` | FUN_004d01f0: mProfitThisYear +0x124 less the amount, to here | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d034e` | Bank message handler FUN_004d02d0, message 0xd (the year's change): mProfitThisYear +0x124 zeroed | OpenTPW.Tests/ParkBankTests.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004d0499` | The bank's month turn: a bought loan's repayment taken off the year's profit by an unsigned division | OpenTPW.Tests/ParkBankTests.cs  |
| `0x004d3e92` | | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW/World/Park/ParkResearch.cs  |
| `0x004d49a0` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x004d5e76` | `FUN_004d5de0`: the guard's decide at hire, on `mGameTick & 3` | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004d6545` | `FUN_004d6410`: a staff member's idle stamp tested against `mGameTick` | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004d655d` | `FUN_004d6410`: the guard's walk-or-stay, `mGameTick & 3` | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004d72f7` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004d7b29` | `FUN_004d7b20`: the arrival manager's one call, through its thunk, once a thing sweep | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004d7b30` | Sweep tail FUN_004d7b20: the staff pool's turn FUN_005084f0, after the arrival manager | OpenTPW.Tests/ParkSweepCapTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkStaffPool.cs  |
| `0x004d8a37` | Edge test FUN_004d8750: CMP ESI,1, mode 1's clause letting a step leave a path for a cell not path, queue or footprint | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004d8b8f` | FUN_004d8b40, a route's length: -1 when FUN_00511ef0 answers 0x70000000 | OpenTPW/World/Park/CellSearch.cs  |
| `0x004d8ba0` | FUN_004d8b40: | OpenTPW/World/Park/CellSearch.cs  |
| `0x004d8be7` | FUN_004d8b40: the sum, to here | OpenTPW/World/Park/CellSearch.cs  |
| `0x004db169` | Object constructor FUN_004db090: mTotalCosts +0x184 zeroed | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db389` | FUN_004db090: mQualityOfGoods +0x18c = 50 | OpenTPW.Tests/ParkBankTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db3ad` | Object constructor FUN_004db090: mPricePerUse +0x194 copied unclamped from UsageInfo.InitPricePerUse (+0xe4) | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkStartingSettingsTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db3b3` | FUN_004db090: mAmountOfSpecialIngredient +0x198 = 50 | OpenTPW.Tests/ParkBankTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db3f3` | Object constructor: the flags word built from the item's description (from here) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db420` | Object constructor: the queue-path bit `0x08` from descriptor `+0x40`, `Info.HasQueue` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db425` | Object constructor: `OR [ESI+0x32],0x8` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db517` | | OpenTPW/World/Park/ParkRides.cs  |
| `0x004db51c` | Object constructor FUN_004db090: the starting speed, capacity and duration from Upgrades[0], each only above nought, from | OpenTPW.Tests/ParkStartingSettingsTests.cs OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x004db534` | Object constructor FUN_004db090: FUN_0055a300 pushes the starting speed into the script's speed word +0xc0 | OpenTPW/World/Park/ParkBumperCars.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x004db560` | Object constructor FUN_004db090: the starting capacity's low byte through the setter FUN_004dd7f0 (+0x5d, script variable 2) | OpenTPW/World/Park/ParkRides.cs  |
| `0x004db64f` | Object constructor FUN_004db090: the starting duration's low byte, held to Min/MaxDuration, stored to +0x5c, to here | OpenTPW.Tests/ParkStartingSettingsTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db66a` | Object constructor FUN_004db090: +0x18 stamped with the park calendar, FUN_004f8690 | OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004db712` | Object constructor: closes an object carrying the queue-path bit (from here) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004db793` | Object constructor: the queue-path close (to here) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004dcf90` | | OpenTPW.Tests/ParkRidesTests.cs  |
| `0x004dd150` | The object destructor `FUN_004dd0a0` sends the type-10 message on the bus: every guest and member of staff bound to the thing answers it | OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004dd2c9` | The object destructor `FUN_004dd0a0` calls the script teardown `FUN_00559060` with mode 0, 4 or 7 | OpenTPW.Tests/ParkSellTests.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x004dd9b7` | Offer gate FUN_004dd920: a coaster (track type 3) asks FUN_00441970, after the room test | OpenTPW/World/Park/ParkRideChoice.cs  |
| `0x004dda87` | FUN_004dda40, the longest queue: the speed +0x58 zero-extended (FILD qword) over the tier's signed InitSpeed (FIDIV) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004dda95` | FUN_004dda40: R stored as a float at 0x007cdc54, read back only at 0x004ddb32 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ddac9` | FUN_004dda40: FLD Upgrades[l].QueueWaitTimeConstant, the float at descriptor +0x1b4 + 0x40 x l | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x004ddb3c` | FUN_004dda40: FCOM 4.0f then TEST AH,0x41: at or below 4, or not a number, takes the floor | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ddd4e` | `FUN_004ddd20` (leave a queue) empties `VAR_LETMEON` when it names the leaver (from here) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004ddd7d` | `FUN_004ddd20`: the `VAR_LETMEON` clear (to here) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004ddde9` | `FUN_004ddd20`: with no `mQPrev` the leaver's `mQNext` becomes `mFirstInQ` | OpenTPW.Tests/ParkQueueJoinTests.cs OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004ddfa9` | `FUN_004ddf50` (GetPositionInQueue) gives up at a guest no longer queueing | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004de233` | | OpenTPW/World/Park/ParkState.cs  |
| `0x004de266` | `FUN_004de1f0` calls `FUN_004d8c60`, a write into a coarse grid at the back cell's block | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/ParkState.cs  |
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
| `0x004e058a` | FUN_004e0560, a sideshow's excitement: 20 - trunc(chance x sqrt(clamp(cost - price, 0, 100)) x 0.1 x -0.8f), from | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e05d4` | FUN_004e0560, a sideshow's excitement: to here | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e05f8` | FUN_004e0560: track type 3, a coaster's excitement from its track | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e0689` | FUN_004e0560: the tier byte +0x50, unbounded, shifted to the descriptor's 0x40 stride | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e0691` | FUN_004e0560: Upgrades[l].InitSpeed, descriptor +0x1a8 + 0x40l, the speed ratio's divisor | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x004e06be` | FUN_004e0560: Upgrades[l].InitDuration, descriptor +0x1a0 + 0x40l, the duration ratio's divisor | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x004e06ce` | FUN_004e0560: the track arm on the object's handle +0x28 | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e06e9` | FUN_004e0560, the track arm: the four out-values zeroed before FUN_00545310, from | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e0706` | FUN_004e0560: the track term 3 x half the crossings + the longest + 2 x half the bends, from | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e0714` | FUN_004e0560: the track term held 0..40, to here | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e0756` | FUN_004e0560, the track arm: the base held 0..100, to here | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e07be` | FUN_004e0560: the level times the speed and duration ratios, each held 0.75..1.25 | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004e092c` | | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004e09b0` | | OpenTPW.Tests/ParkRideAdmitTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004e0d10` | | OpenTPW.Tests/ParkToiletDirtTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004e13fc` | `Invite`'s `mCanLoad` bail: `FUN_004e0450` and return, skipping the watchdog | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004e16c6` | The charge's economy feed `FUN_004e16b0`: the price deposited in the park's bank (`FUN_004d0190`) | OpenTPW.Tests/ParkRideExitTests.cs  |
| `0x004e1711` | Economy feed FUN_004e16b0, a shop: the park analyser's month total +0x20130 | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004e18a6` | FUN_004e16b0, a sideshow: the park analyser's month total +0x20380 | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004e2390` | FUN_004e2290, the scrap percentage: 100 only with mNumCustomers (+0x1a0) nought, under 30 days old | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004e23b2` | | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x004e26c6` | The win roll FUN_004e2670: r drawn from the park's generator FUN_00516330, won when the object's chance >= r % 100 | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004f7ea0` | Clock constructor FUN_004f7e80: mFunnyTimeStart seeded 2000-01-01 00:00 through SystemTimeToFileTime | OpenTPW/Global/GameCalendar.cs  |
| `0x004f7ea9` | | OpenTPW/Global/GameCalendar.cs  |
| `0x004f8321` | Calendar FUN_004f8260: the day of the month compared with mDayAtLastUpdate; a difference sends the day's change, message 0xb | OpenTPW.Tests/GameCalendarTests.cs OpenTPW/Global/GameCalendar.cs OpenTPW/World/Park/ParkState.cs  |
| `0x004f83b9` | Calendar FUN_004f8260: the month compared on its own; a change sends message 0xc | OpenTPW/Global/GameCalendar.cs  |
| `0x004f84d0` | FUN_004f8260: the year compared on its own; a change sends message 0xd | OpenTPW/Global/GameCalendar.cs  |
| `0x004f8792` | | OpenTPW/Global/GameCalendar.cs  |
| `0x004f87e7` | | OpenTPW.Tests/GameCalendarTests.cs OpenTPW/Global/GameCalendar.cs  |
| `0x004f8ccc` | Person serialiser FUN_004f8b10, write arm: mCount (+0x2c) written as one byte, the person record's byte 36 | OpenTPW.Files/Formats/Save/ParkWorld.cs  |
| `0x004f93a6` | Person base reader FUN_004f8b10: mSpriteID reduced modulo its kind's loaded banks (FUN_00541f60) | OpenTPW.Tests/ParkCostumeTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkSpriteBanks.cs  |
| `0x004f94e2` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f950e` | | OpenTPW/World/Park/Thoughts.cs  |
| `0x004f9534` | SetRandomDest: the call's one draw, r % 5 + 1, the linked walk's pass count, taken before the links count | OpenTPW.Tests/ParkNoLinksWanderTests.cs OpenTPW/World/Park/PeepBehaviour.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004f953b` | | OpenTPW/World/Park/LinkedWander.cs  |
| `0x004f95af` | | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004f95b9` | SetRandomDest `FUN_004f9490`: the count of the person's own cell's links (`FUN_00522810`); nought takes the no-links arm | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f95c0` | | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004f95c6` | | OpenTPW.Tests/ParkLinkedWanderTests.cs OpenTPW/World/Park/LinkedWander.cs  |
| `0x004f96fb` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004f98a1` | | OpenTPW/World/Park/LinkedWander.cs  |
| `0x004f98f5` | | OpenTPW/World/Park/LinkedWander.cs  |
| `0x004f9916` | | OpenTPW.Tests/ParkLinkedWanderTests.cs OpenTPW/World/Park/LinkedWander.cs  |
| `0x004f991c` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f9983` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f99bf` | | OpenTPW.Tests/ParkNoLinksWanderTests.cs  |
| `0x004f9a05` | SetRandomDest: start of the no-links arm (path on seven rays, then five random cells) | OpenTPW.Tests/ParkNoLinksWanderTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f9b98` | SetRandomDest's no-links arm: a probe's failed route moves on to the next probe | OpenTPW.Tests/ParkNoLinksWanderTests.cs  |
| `0x004f9d19` | SetRandomDest's no-links arm: the five random tries' count, an off-map draw included | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f9d5f` | SetRandomDest: end of the no-links arm | OpenTPW.Tests/ParkNoLinksWanderTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f9d8e` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x004f9deb` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/PeepBehaviour.cs OpenTPW/World/Park/Thoughts.cs  |
| `0x004f9e09` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f9e40` | SetRandomDest's no-links arm: the eight-way direction table of its probes | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004f9f89` | `FUN_004f9f00`: first half of `prev + (cur - prev) * t` | OpenTPW.Tests/ParkGuestPlacementTests.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x004f9fa9` | FUN_004f9f00, a person's sample: the across position truncated by __ftol before the scale | OpenTPW/World/Park/Balloon.cs  |
| `0x004f9fb6` | `FUN_004f9f00`: second half of the interpolation | OpenTPW.Tests/ParkGuestPlacementTests.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x004f9fd2` | FUN_004f9f00: the down position truncated by __ftol | OpenTPW/World/Park/Balloon.cs  |
| `0x004fa015` | `FUN_004f9f00` copies the octant straight off the thing: the heading is not blended | OpenTPW.Tests/ParkGuestPlacementTests.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x004fa0c7` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fa0e6` | | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x004fa184` | FUN_004fa030: a guest (model byte 1) holding a balloon (+0x210) has it placed each frame, from | OpenTPW/World/Park/Balloon.cs  |
| `0x004fa244` | FUN_004fa030: the bob's phase counts the placement (0x007cedd8 by 0.1 to 1.0), from | OpenTPW/World/Park/Balloon.cs  |
| `0x004fa28b` | FUN_004fa030: the bob's count, to here | OpenTPW/World/Park/Balloon.cs  |
| `0x004fa30b` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fa578` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fa5d0` | | OpenTPW.Tests/ParkStrandedTests.cs  |
| `0x004fa5fe` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fa62a` | `FUN_004fa5f0`: returns nought without routing on `mStrandedTime` (`+0x198`) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fa95d` | The place-a-peep routine re-stamps previous := current | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/PeepNavigator.cs  |
| `0x004fae10` | | OpenTPW.Files/Formats/Save/RecordStream.cs  |
| `0x004fb019` | Guest constructor `FUN_004faec0`: the kind drawn as the world generator mod `[0x007851d4]`, the `PeepTypes` row count | OpenTPW.Tests/ParkGuestTypeTests.cs  |
| `0x004fb18d` | Guest constructor FUN_004faec0: the child - the generator reseeded with the id, kind 0, one draw over the kid banks, from | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkSpriteBanks.cs  |
| `0x004fb19e` | Guest constructor FUN_004faec0: FUN_00516370 reseeds the park's generator with the guest's id word | OpenTPW.Tests/ParkPeopleTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb1bc` | FUN_004faec0: the child's sprite built (FUN_004d4140), to here | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb1c9` | Guest constructor FUN_004faec0: the hurry +0xc2 set to 25, the word at 0x0075c7f2 | OpenTPW.Tests/ParkTickTests.cs  |
| `0x004fb1d0` | Guest constructor: FUN_004fa990 on the cell the guest is made on decides the first state | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb1e4` | Guest constructor: on a park cell, a visitor number and state 6 | OpenTPW.Tests/ParkPeopleTests.cs  |
| `0x004fb1f5` | Guest constructor: off the park's cells, the walk to the crossing's bus-stop side begins (to 0x004fb24d) | OpenTPW.Tests/ParkPeopleTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb206` | Guest constructor: the low bit of the fourth draw after the reseed picks the roadside cell (FUN_004d8710) | OpenTPW.Tests/ParkPeopleTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb221` | Guest constructor: the low byte of the fifth draw is the place across the roadside cell | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb22b` | Guest constructor: the place down the roadside cell, a fixed 0xc8 of 256 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb24d` | Guest constructor: FUN_004fa5f0 routes the walk to the roadside point | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb259` | Guest constructor: no route to the roadside, state 6 instead of state 0 | OpenTPW.Tests/ParkPeopleTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb2fd` | Guest constructor FUN_004faec0: broadcasts message 0x1c, a guest made (the all-visitors list adds their row) | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb333` | FUN_004fb330, a guest deleted at the bus: the balloon's sprite deleted (FUN_00475550), no burst, from | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb33e` | Guest delete FUN_004fb330: FUN_00475550 frees the balloon's sprite ([guest + 0x210]) before the guest goes | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb346` | FUN_004fb330: the balloon deletion, to here | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb383` | The guest's type-10 answer `FUN_004fb360` chooses a guest by `mMajorDest` alone | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb38d` | `FUN_004fb360`'s rider arm: state `0x10` exactly | OpenTPW.Tests/ParkEvictionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb3cd` | `FUN_004fb360` makes a rider a new sprite when admission destroyed theirs; its position is still nought | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb3f5` | `FUN_004fb360` plays the kids' effect `0x80` at the rider's sprite | OpenTPW.Tests/ParkPutOffSoundTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fb4a1` | `FUN_004fb360` puts the guest into state 6, deciding | OpenTPW.Tests/ParkEvictionTests.cs  |
| `0x004fb4a6` | FUN_004fb360, a thing removed: +0x1de, the saved major, tested against it for every guest, from | OpenTPW.Tests/ParkSecondToiletTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb4b3` | FUN_004fb360: the saved major cleared, to here | OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb4ba` | FUN_004fb360, a thing removed: each mPreviousRides slot naming it emptied with the refusal beside it, from | OpenTPW.Tests/ParkVisitHistoryTests.cs OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fb4d9` | FUN_004fb360: the history clear, to here | OpenTPW/World/Park/Peep.cs  |
| `0x004fc66d` | Guest reader FUN_004fb530, read arm: mSavedMajorDest (+0x1de) read as a word by FUN_004d37e0, as mMajorDest is | OpenTPW.Files/Formats/Save/ParkWorld.cs  |
| `0x004fc82e` | | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fc83f` | | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fc842` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fc84e` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x004fc985` | | OpenTPW/World/Park/Thoughts.cs  |
| `0x004fc9eb` | | OpenTPW/World/Park/Thoughts.cs  |
| `0x004fca43` | | OpenTPW/World/Park/Thoughts.cs  |
| `0x004fcb21` | The guest's chooser `FUN_004fcb10` clears `mMajorDest` before it chooses, chosen or not | OpenTPW.Tests/ParkEvictionTests.cs OpenTPW.Tests/ParkSecondToiletTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fcbc4` | The chooser `FUN_004fcb10` aims at the back-of-queue cell's centre (`FUN_004fa530`) | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fcbcf` | | OpenTPW.Tests/ParkRideChooserTests.cs OpenTPW.Tests/ParkSecondToiletTests.cs OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fcbd8` | | OpenTPW.Tests/ParkRideChooserTests.cs  |
| `0x004fcc7d` | | OpenTPW.Tests/ParkRideChooserTests.cs OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fcd3f` | FUN_004fcc30, the ride score: the same kind as mPreviousRides[0] scores nought, from | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fcd79` | FUN_004fcc30: mPreviousRides[0] looked up in the thing table 0x7cfb90 | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fcd97` | FUN_004fcc30: the same-kind nought, to here | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fce04` | | OpenTPW.Tests/ParkRideChooserTests.cs OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fce3e` | FUN_004fcc30: the queue term only within a squared distance of 8 (JG) | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fce40` | FUN_004fcc30: the queue term over +0x40, the walked cells, nought read as 1 | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fcf06` | FUN_004fcc30: the excitement weight only for a non-zero low byte of ExcitementLevel | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd22a` | FUN_004fcc30: the weighted mean, an unsigned divide | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd24f` | FUN_004fcc30: new while FUN_004dd670 <= DecisionVariable1, unsigned (JA) | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd277` | FUN_004fcc30: rain, the weather thing's mCurrentDrops above nought | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fd2aa` | FUN_004fcc30: the golden-ticket or price factor, from | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd352` | FUN_004fcc30: the golden-ticket or price factor, to here | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd354` | FUN_004fcc30: the two histories divide the score, from | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd496` | FUN_004fcc30: the two histories, to here | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x004fd4ea` | | OpenTPW.Tests/ParkGuestTypeTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fd50a` | `FUN_004fd4e0`, the arrival refusal: reads `PeepTypes[kind].PreferredExcitement`, the byte at `0x7850e4` + 12 × kind | OpenTPW.Tests/ParkGuestTypeTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fd660` | FUN_004fd570: the window, x from the guest's x - 2 to + 1 (outer), from | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fd6ce` | FUN_004fd570: a window cell only if strictly nearer the major's back cell than the guest (JGE) | OpenTPW.Tests/ParkSecondToiletTests.cs  |
| `0x004fd73f` | FUN_004fd570: each candidate asked the offer gate FUN_004dd920 | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fd74b` | FUN_004fd570: then scored by FUN_004fcc30 | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fd750` | FUN_004fd570: the best, from nought, taken on a higher score or an equal one on an odd mGameTick, from | OpenTPW.Tests/ParkSecondToiletTests.cs OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fd76d` | FUN_004fd570: the tie, to here | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fd7a4` | FUN_004fd570: the window, to here | OpenTPW/World/Park/ParkRideChooser.cs  |
| `0x004fd888` | FUN_004fd570, the switch test: JLE refuses a candidate no shorter from the major's entry than the guest | OpenTPW.Tests/ParkSecondToiletTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fd934` | FUN_004fd570: +0x1de = the old major, whatever it held | OpenTPW.Tests/ParkSecondToiletTests.cs OpenTPW/World/Park/Peep.cs  |
| `0x004fd93b` | FUN_004fd570: FUN_004fa530 to the candidate's entry cell, its answer ignored | OpenTPW.Tests/ParkSecondToiletTests.cs  |
| `0x004fd98b` | Settle-up FUN_004fd970: the thing left pushed onto mPreviousRides, from | OpenTPW.Tests/ParkVisitHistoryTests.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/Peep.cs  |
| `0x004fd9a5` | FUN_004fd970: the push, to here | OpenTPW/World/Park/Peep.cs  |
| `0x004fd9ac` | Settle-up FUN_004fd970: the guest's mNumRides, mNumShops or mNumSideshows +1 by the descriptor's +0x4ac, from | OpenTPW.Tests/ParkSettleUpCountsTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fd9b1` | FUN_004fd970: +0x4ac read and compared 0, 1, 2; a feature bumps none | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fd9d2` | FUN_004fd970: the counters, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fd9e2` | FUN_004fd970: FUN_004e1690, mNumCustomers and today's customers +1, before the gate | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fd9e7` | FUN_004fd970: FatigueEffect (+0xe8) off mTiredness +0x1b8, held 0..100, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fda00` | FUN_004fd970: the fatigue step, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fda1b` | FUN_004fd970: 3 x (low byte of happiness - low byte of the join's snapshot +0x20c), from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fda47` | FUN_004fd970: the change since the join, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fda92` | FUN_004fd970: the park analyser's settle-up sample, change + 50, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fdb2c` | FUN_004fd970: the analyser's sample, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fdb33` | FUN_004fd970: FUN_004e19f0, today's served +1, every kind | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fdb38` | FUN_004fd970: a sideshow's winner thinks thought 5 and pushes event 0x18, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fdb7f` | FUN_004fd970: thought 5, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fdc25` | Settle-up FUN_004fd970, the lost arm: a sideshow's loser thinks thought 6 (FUN_0050be80) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fde6a` | Price opinion `FUN_004fde50`: a price of nought answers nought, and no sample is pushed | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fdf6d` | Price opinion: the first unsigned division by 100 (`MUL`, `SHR 5`), mood times the goods | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fdfe7` | Price opinion: the second unsigned division, after `RipOffOK` | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe005` | Price opinion: the third unsigned division, after happiness - the worth | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe15f` | Price opinion: price above worth, unsigned (`JA`) | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe167` | Price opinion: cash below price, unsigned (`JC`) | OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x004fe1be` | Charge FUN_004fe1a0: sample 0xd0 at the guest (FUN_004faa00), from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe1cb` | FUN_004fe1a0: the sound, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe204` | FUN_004fe1e0: the visit entered in the guest's event history, entry 8 (FUN_0050c100) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe259` | FUN_004fe1e0, a visit's effects: calls the excitement match FUN_004fdcc0, before the item's own effects | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe44e` | FUN_004fe1e0, the hunger dock: (r & 7) + the amount + the effect compared with 30, unsigned (the thirst dock's at 0x004fe4a0) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe60e` | FUN_004fe1e0, sugar: amount x 6 / 100 added to the word mAdjustorSpeed +0xc4, no clamp | OpenTPW/World/Park/Peep.cs  |
| `0x004fe615` | FUN_004fe1e0: the appearance switch on the descriptor's +0x15c, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe63d` | FUN_004fe1e0: any appearance but 0, 1 or 2 logs a balance-file error into the bare RET, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe642` | FUN_004fe1e0, a costume: the arm, from (to 0x004fe6b5) | OpenTPW.Tests/ParkCostumeTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe64a` | FUN_004fe1e0, a costume: mESPSprite +0x24 compared with exactly 2 | OpenTPW.Tests/ParkCostumeTests.cs  |
| `0x004fe6b5` | FUN_004fe1e0: the costume arm, to here | OpenTPW.Tests/ParkCostumeTests.cs  |
| `0x004fe6ba` | FUN_004fe1e0, a balloon: the arm, from (to 0x004fe78a) | OpenTPW.Tests/ParkBalloonTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe6fc` | FUN_004fe1e0, a balloon: the park's generator reseeded with the guest's id (FUN_00516370) | OpenTPW/World/Park/Balloon.cs  |
| `0x004fe737` | FUN_004fe1e0, a balloon: the life, the shop's quality byte x 255 / 100 held to 25..255, from | OpenTPW/World/Park/Balloon.cs  |
| `0x004fe76f` | FUN_004fe1e0, a balloon: the life, to here | OpenTPW/World/Park/Balloon.cs  |
| `0x004fe775` | FUN_004fe1e0, a balloon: event 0xc naming the shop (FUN_0050c100) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe787` | FUN_004fe1e0: the balloon and costume arms' shared event tail, FUN_0050c100 with the shop's id | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe78a` | FUN_004fe1e0: the balloon arm and the costume arm's shared event tail, to here | OpenTPW.Tests/ParkBalloonTests.cs OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe78f` | FUN_004fe1e0, the toilet arm: the object's flags & 1, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe795` | FUN_004fe1e0: the toilet need truncated for FUN_004e2440's byte, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe7a8` | FUN_004fe1e0: FUN_004e2440, the dirtying, with the need's byte | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe7b6` | FUN_004fe1e0, a toilet: the guest's toilet need +0x1ac zeroed | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe7dc` | FUN_004fe1e0: illness's truncated byte above 90, unsigned (CMP AL,0x5a / JBE) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe81f` | FUN_004fe1e0, a sideshow: mNumSideshowsWon +0x1d0 +1, before the winner's rise | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe8e8` | FUN_004fe1e0: the special ingredient's jump table, five entries (0 and above 4 to the switch's end) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x004fe96b` | Let-go FUN_004fe950: mBalloonScript +0x210 zeroed after the sprite is put on 0x0074f4c0 | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004fecb4` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fecb9` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fecd2` | | OpenTPW.Tests/ParkDecidingArmsTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fece2` | | OpenTPW.Tests/ParkDecidingArmsTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fed15` | | OpenTPW.Tests/ParkDecidingArmsTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fed26` | | OpenTPW.Tests/ParkDecidingArmsTests.cs  |
| `0x004fedb3` | | OpenTPW.Tests/ParkDecidingArmsTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fedd8` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fee51` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fee5b` | | OpenTPW.Tests/ParkLeavingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fee87` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fef13` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fef19` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004feff2` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004ff108` | | OpenTPW.Tests/ParkDecidingArmsTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff150` | | OpenTPW.Tests/ParkDecidingArmsTests.cs  |
| `0x004ff156` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff1c8` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004ff1d9` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x004ff24b` | | OpenTPW/World/Park/ParkAdmission.cs  |
| `0x004ff3ae` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff3d6` | | OpenTPW.Tests/ParkDecidingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff3f4` | | OpenTPW.Tests/ParkDecidingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff425` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff42e` | | OpenTPW.Tests/ParkDecidingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff437` | | OpenTPW.Tests/ParkGuestTypeTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff44e` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff457` | | OpenTPW.Tests/ParkGuestTypeTests.cs  |
| `0x004ff46f` | | OpenTPW.Tests/ParkDecidingTests.cs  |
| `0x004ff480` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff4a3` | | OpenTPW.Tests/ParkDecidingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff52c` | State 1's handler FUN_004ff520: FUN_0051a760 asked; no does nothing at all | OpenTPW.Tests/PeepBehaviourTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ff543` | FUN_004ff520: the first draw's low bit picks the ticket booth (FUN_004d8610) | OpenTPW.Tests/PeepBehaviourTests.cs  |
| `0x004ff55e` | FUN_004ff520: the second draw's low byte, the place across the booth's cell | OpenTPW.Tests/PeepBehaviourTests.cs  |
| `0x004ff56b` | FUN_004ff520: the third draw's low byte, the place down the booth's cell | OpenTPW.Tests/PeepBehaviourTests.cs  |
| `0x004ff5ab` | FUN_004ff520's return, where a no from FUN_0051a760 jumps | OpenTPW.Tests/PeepBehaviourTests.cs  |
| `0x004ffa44` | | OpenTPW.Tests/ParkLeavingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffa7d` | | OpenTPW.Tests/ParkLeavingTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffc3d` | State 10's arrival test: the guest's cell against `GetBackOfQueue` | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffc68` | | OpenTPW.Tests/ParkToiletDirtTests.cs  |
| `0x004ffc85` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffca1` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffcd7` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffce6` | Arrival FUN_004ffbc0: the excitement refusal pushes the thing onto mPreviousTemporaryRides (FUN_004fdc60) | OpenTPW.Tests/ParkGuestTypeTests.cs OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffcf6` | | OpenTPW.Tests/ParkGuestTypeTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffd74` | Arrival FUN_004ffbc0: the too-long refusal pushes the thing onto mPreviousTemporaryRides | OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffd92` | Arrival FUN_004ffbc0: happiness copied to +0x20c, the join's snapshot, before FUN_004ddb90 links the queue | OpenTPW.Tests/ParkSettleUpCountsTests.cs OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffdad` | Joining a queue re-takes the place (`FUN_00501160`) | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffdf4` | Arriving at a queue with no route to the place: put out | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW.Tests/ParkVisitHistoryTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe0a` | | OpenTPW.Tests/ParkLongestQueueTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe16` | State 10: no back of queue, or no route to it - state 6 with `MajorDest` kept | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe4a` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe7a` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe94` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffe9f` | State 10 FUN_004ffbc0, stuck: MajorDest cleared, the saved major left | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffeaf` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffec9` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffef2` | FUN_004ffbc0: the walking-turn count +0x2c, from | OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004ffefe` | FUN_004ffbc0: CMP AL,0xb / JBE, unsigned: the twelfth decides | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x004fff06` | FUN_004ffbc0: the count zeroed, then the minor decision FUN_004fd570 called, to here | OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050010a` | `InQueue` turn: the board arm's no route, "the player has removed the path", out | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050012b` | `InQueue` turn: the board arm's no route calls `FUN_004e0ac0`, the ride forgets its nominee | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005001d8` | `InQueue` turn: invited but not the nominee, the whole turn is nothing | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005001f0` | | OpenTPW.Tests/ParkToiletDirtTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500270` | `InQueue` turn: the lost place, "Problem with a queue", out | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x005002f2` | `FUN_004ffff0`: the mood, which a re-take falls through to | OpenTPW.Tests/ParkQueuePlaceTests.cs  |
| `0x00500308` | `InQueue` turn: the mood is read once `mGameTick - mTimeOfLastSpotAnim` exceeds 30 | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050031c` | `InQueue` turn: happiness above `0x50` plays spot animation 5 | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050031e` | `InQueue` turn: the branch for happiness above `0x50` | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500320` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500324` | | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500330` | `InQueue` turn: happiness from `0x14` asks about the toilet | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500334` | `InQueue` turn: happiness below `0xa` gives up unhappy | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500336` | `InQueue` turn: happiness 10..19 plays spot animation 4 | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500387` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005003a2` | `InQueue` turn: a toilet need above `0x50` leaves for a toilet | OpenTPW.Tests/ParkQueueTurnTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005003b6` | `InQueue` turn: a queuer for a toilet stays in its queue | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500415` | `InQueue` turn: boredom after `mTimeStartedIdling` + 100 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050049e` | The `InQueue` turn's shared way out (from here) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005004b3` | `FUN_004ffff0`'s put-out tail: `FUN_004ddd20`, then `FUN_005012f0` | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500523` | `InQueue` turn: a ride broken down (state 1) re-takes no place | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500532` | `FUN_004ffff0`'s re-take: `FUN_00501160`, and out if it fails | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050059d` | InQueue turn FUN_004ffff0, arm 5a: FUN_004dda40 against mQueuePos, unsigned; past it, thought 0x10, event 0x15, out | OpenTPW.Tests/ParkLongestQueueTests.cs  |
| `0x00500631` | `InQueue` turn: the track gate's item track type 1 | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500643` | `InQueue` turn: the track gate's `mIsTrackRideValid` nought | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00500715` | At the door: the price opinion `FUN_004fde50` | OpenTPW.Tests/ParkRideExitTests.cs OpenTPW/World/Park/ParkRideOperation.cs OpenTPW/World/Park/PeepBehaviour.cs OpenTPW/World/Park/PeepPriceOpinion.cs  |
| `0x0050076b` | FUN_005006b0, too expensive at the door: event 10 pushed onto the guest's history (FUN_0050c100) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500778` | At the door, too expensive: the first `MediumHappinessChange` | OpenTPW.Tests/PeepPriceOpinionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050077f` | FUN_005006b0, too expensive at the door: FUN_004e1670, mNumWalkAways and today's walk-aways +1 | OpenTPW.Tests/PeepPriceOpinionTests.cs  |
| `0x005007b4` | At the door, too expensive: put out (`FUN_005012f0`) | OpenTPW.Tests/PeepPriceOpinionTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500826` | At the door, refused: re-take the front of the queue | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500857` | At the door: "Couldn't rejoin FOQ even!", put out | OpenTPW.Tests/ParkQueuePlaceTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500913` | State 15 FUN_00500900, arrived: the saved major +0x1de read | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050092a` | FUN_00500900: the saved major cleared | OpenTPW/World/Park/Peep.cs  |
| `0x005009d7` | FUN_00500900: "Left minor destination, found old major one again!", SetState(10) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500a3b` | FUN_00500900, stuck: MajorDest cleared, the saved major left | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00500b47` | State 0x13's handler FUN_00500ad0: FUN_0051a760 asked, the other of its two callers | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050133d` | `FUN_005012f0` plays the kids' effect `0x80` only when the guest's id `& 7` is nought | OpenTPW.Tests/ParkPutOffSoundTests.cs OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x00501413` | `FUN_00501390`: place against cells times four, unsigned (from here) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050141c` | `FUN_00501390`: the unsigned place test (to here) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00501422` | `FUN_00501390`: a guest in state 14 is never put out | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0050148a` | `FUN_00501390`: thought `0xd` when the id divides by three | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005015e3` | ExitRide FUN_005014e0: the cell off the exit must be neither queue nor entrance before the settle-up | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x005015ef` | ExitRide FUN_005014e0: the route to it must succeed before the settle-up FUN_004fd970 | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x00501658` | Guest tick handler `FUN_00501650`: its first call, `FUN_004fa870`, stamps the previous position | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/PeepNavigator.cs  |
| `0x005018f8` | Needs turn FUN_00501650, its last test: states 16 and 17 and FUN_004fa990 gate a draw and the balloon's countdown, from | OpenTPW/World/Park/Peep.cs  |
| `0x00501913` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0050192d` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00501949` | FUN_00501650: the life at nought lets the balloon go (FUN_004fe950), to here | OpenTPW/World/Park/Peep.cs  |
| `0x00501951` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x005019da` | Guest needs turn FUN_00501650, its last call: FUN_004fdc90, a nought onto the refusals when mGameTick % 20 is nought | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/Peep.cs  |
| `0x00501d26` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00501d32` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00501d3b` | | OpenTPW.Tests/ParkQueueTurnTests.cs  |
| `0x00501fd3` | State setter FUN_00501db0 case 0xf: with life left and the thing left not a balloon shop, the balloon built again, from | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x0050208a` | FUN_00501db0 case 0xf: the rebuild, to here | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x0050212b` | Admission tests the object's flag bit `0x20`, which keeps the rider's sprite | OpenTPW.Files/Formats/Save/ParkWorld.cs  |
| `0x00502147` | Admission destroys the rider's sprite on an object without flag bit `0x20` | OpenTPW.Files/Formats/Save/ParkWorld.cs  |
| `0x00502156` | FUN_00501db0 case 0x10: the balloon's sprite deleted (FUN_00475550), its life kept, from | OpenTPW/World/Park/Peep.cs  |
| `0x00502169` | FUN_00501db0 case 0x10: +0x210 zeroed, to here | OpenTPW/World/Park/Peep.cs  |
| `0x005022ef` | FUN_00501db0 case 0x11: the balloon let go (FUN_004fe950) | OpenTPW/World/Park/Peep.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00502333` | FUN_00501db0, entering state 0x15: a guest whose +0x204 is set hands the park analyser their stay (FUN_004c7600) | OpenTPW/World/Park/Peep.cs  |
| `0x005026cb` | `FUN_00502600`: the researcher's decide at hire, on a world draw | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00502ba9` | `FUN_005029f0`: the researcher's walk-or-stay, a world draw `& 3` | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00502be4` | `FUN_005029f0`: the researcher staying takes state `0xf`, research | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00504d8f` | `FUN_00504c70`: a staff member put out of a sold rest area claims another and stays in state 0 | OpenTPW.Tests/ParkEvictionTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00505495` | `FUN_00505490` opens with `FUN_004fa870`, as the guest handler does | OpenTPW.Tests/ParkTickTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x005054b3` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x005054c0` | | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00505542` | Staff idle turn: a nought idle stamp ends the idle wait on the next sweep | OpenTPW/World/Park/Staff.cs  |
| `0x00505745` | `FUN_005056e0`: state 6's wait against `mGameTick`, unsigned | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506286` | `FUN_005061d0`: the normal end of a rest calls `FUN_00506d10`, which takes one off `VAR_STAFFIN` | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506b41` | FUN_00506a40, the tired branch: the rest byte, truncated, tested <= the rest level | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506b50` | FUN_00506a40: thought 0x14 (tired) shown through FUN_0050be80 before the rest area is looked for | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506cc2` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506ccd` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506cda` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00506cf4` | | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x0050701f` | | OpenTPW.Tests/ParkStaffBehaviourTests.cs OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x0050819d` | FUN_00508170, the minimums' round: ADD EAX,EDX, the kind's staff in the park added to its candidates before the compare with its minimum | OpenTPW.Tests/ParkStaffPoolRefreshTests.cs  |
| `0x0050b7f9` | Thing delete FUN_0050b780: broadcasts message 0x1b before the free (the all-visitors list removes a guest's row) | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0050c039` | | OpenTPW/World/Park/Thoughts.cs  |
| `0x0050cd80` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x0050ed79` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/PeepBehaviour.cs OpenTPW/World/Park/PeepWalk.cs  |
| `0x0050f870` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x0050fd19` | | OpenTPW/World/Park/PeepWalk.cs  |
| `0x005101d0` | FUN_00510190: max_force and max_speed each held at 0x28f (655) or more | OpenTPW/World/Park/Peep.cs  |
| `0x00511fc4` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512880` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x0051295c` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512a5e` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512b4c` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00515865` | | OpenTPW/Global/GameCalendar.cs OpenTPW/World/Level.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00516330` | The park's generator: the state at world +0x1da708 times 0x19660d plus 0x3c6ef35f, rolled right 13 and kept; answers its magnitude | OpenTPW.Tests/ParkPeopleTests.cs  |
| `0x0051635f` | World generator FUN_00516330: NEG leaves 0x80000000 unchanged, which RAND and FINDSCRIPTRAND then halve | OpenTPW.Tests/RideScriptClockTests.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkGenerator.cs  |
| `0x00516394` | Thing sweep `FUN_00516380`: `mGameTick` up by one | OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkState.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x00516695` | World tick FUN_00516380: FUN_004d7b20 after every thing's turn, where the calendar sends the day's change | OpenTPW/World/Level.cs  |
| `0x00516d13` | World save `FUN_00516c80`: installs the idle mode before anything is written, so leaving a park lets go of the hand | OpenTPW/World/Level.cs  |
| `0x00517bec` | World load: `mGameTick` read from the save | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x005181e7` | | OpenTPW/World/Park/ParkResearch.cs  |
| `0x0051861c` | Load factory FUN_005179c0: the join's snapshot +0x20c zeroed before the guest's record is read | OpenTPW/World/Park/Peep.cs  |
| `0x00519f76` | `FUN_00519ef0`'s open arm (first argument non-zero), only when the park is closed | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/UI/Park/ParkEntryPriceScreen.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a013` | The park's door, opening: the open guard `FUN_004df290` on each visitable object | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x0051a01e` | The park's door, opening: `FUN_004df390` opens it | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a09e` | `FUN_00519ef0`'s close arm runs only when the park is open | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a0e8` | The park's door, closing: the gate's `VAR_COMMAND` = 0 when nobody is in the park (from here) | OpenTPW/World/Park/ParkState.cs  |
| `0x0051a161` | The park's door, closing: the gate write (to here) | OpenTPW/World/Park/ParkState.cs  |
| `0x0051a1ae` | The park's door, closing: `FUN_004df300` on every visitable object | OpenTPW.Tests/ParkClosedRideTests.cs OpenTPW/World/Park/ParkPeople.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0051a314` | Summons FUN_0051a2f0: a vehicle already current is reused whatever size is asked | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0051a5ed` | FUN_0051a2f0, no vehicle current: the slot's existing thing has script variable 0, VAR_TRIGGER, set to 1; one just made has variable 1, VAR_STATUS | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0051a663` | FUN_0051a2f0, a vehicle current: its script variable 0, VAR_TRIGGER, set to 1 | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0051a774` | FUN_0051a760: CMP AX,[ESI+0x1da72c], the current vehicle against the small crowd's; any other answers 1 before a status is read | OpenTPW.Tests/ParkTickTests.cs  |
| `0x0051b920` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x0051bcb0` | | OpenTPW/Audio/Audio.cs OpenTPW/World/Level.cs  |
| `0x0051bd70` | | OpenTPW/Audio/Audio.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0051bfab` | Sound_ApplyGroupVolumes posting the voice service (message `0x700b6c`) | OpenTPW/World/Park/ParkScreams.cs  |
| `0x0051c2c0` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x0051c300` | | OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Park/ParkCarSounds.cs  |
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
| `0x00524a77` | Carry shell's button-up FUN_00524960: its one read of the down slot's flag DAT_008186d8, gating a sound | OpenTPW/World/Level.cs  |
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
| `0x00527541` | `FUN_004de1f0` after a queue run is laid (mode 3) | OpenTPW.Tests/ParkQueueRemeasureTests.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005275f2` | Mode-3 commit: the tool ends through `FUN_0052f200(0,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527655` | Apply dispatcher: end of the path/queue arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527f9a` | The demolisher builds the queue's list, `FUN_00530120`, before the gate | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527fa5` | The demolisher arms mode 3, `FUN_0052f200( 3, 0 )`, which posts advisor `0xcb` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527fb4` | The demolisher's gate: the object's cached queue length `+0x40` above nought | OpenTPW.Tests/ParkPathBuildingTests.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527fe8` | The drain's debit scaled by the per-age percentage, `FUN_004e2290` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00528007` | Demolisher FUN_00527ee0: the queue drain's debit, the bank's withdrawal FUN_004d01f0 | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00528179` | FUN_00527ee0: the track arm's withdrawal; any track type but nought, karts' and water's withdraws nought | OpenTPW.Tests/ParkBankTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052818d` | The demolisher puts back the tool it was called under, `FUN_0052f200( prevTool, 0 )`; tool 0 installs the idle mode | OpenTPW.Tests/ParkHandTests.cs OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052842b` | The demolisher's second footprint pass: `FUN_005367a0( 0, 0 )` on every cell of the shape but its `.` ones | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00528584` | Demolisher FUN_00527ee0: FUN_00545610 frees the object's track ride entry, before the object goes | OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00528f62` | Placer: start of the pairing of footprint bases 1, 0x40, 0x10, 4 with angles 0, 90, 180, 270 | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00528f8b` | Placer: the angle-0 arm's base write, `1` (the pairing's other three at `0x00528fb7`, `0x00528fe7`, `0x0052900f`) | OpenTPW/World/Park/ParkBuilding.cs  |
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
| `0x00529e4d` | Placer FUN_00529e10: a track handle for an item whose Bumper.BumperType (+0xa0) is non-zero | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00529edf` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00529f2b` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00529f6a` | Placer FUN_00529e10: the call to FUN_00545890 for a track ride's slot | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052ff9a` | `FUN_0052fe50` clears nothing when the mode is 3 and P is a path | OpenTPW.Tests/ParkPathBuildingTests.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0052ffec` | `FUN_004de1f0` from the backtrack `FUN_0052fe50` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005300a6` | `FUN_0052fe50`'s last call re-arms the mode before it answers nought | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053019a` | `FUN_00530120` switches on the entry cell's whole mask, a single cardinal bit only | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005301ea` | `FUN_00530120`: a faced queue or entrance cell of another owner is pushed alone | OpenTPW.Tests/ParkPathBuildingTests.cs  |
| `0x0053024a` | `FUN_00530120`'s walk starts on the entry cell itself | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005302ea` | `FUN_00530120`: an entrance cell never stops the walk | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005302f7` | `FUN_00530120`: the walk stops on a cell with one link of the eight | OpenTPW.Tests/ParkPathBuildingTests.cs  |
| `0x0053036d` | `FUN_00530120` at a path: steps back unless the cell before is an entrance | OpenTPW.Tests/ParkPathBuildingTests.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005303f1` | `FUN_00530120`: an entry cell with no link pushes the cell its angle names | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00530487` | `FUN_00530120`'s angle arm ends; the final push follows | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005330ca` | | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x0053473c` | Stamp: queue over path force-clears the path, `FUN_005367a0(0,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053475d` | Stamp: end of the force-clear | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005347af` | | OpenTPW/World/Park/ParkState.cs  |
| `0x00534858` | `FUN_004de1f0` from the stamp: path laid over a queue cell | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00534906` | `FUN_005348d0` path arm: a queue cell becomes path, type only | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x00534913` | Path arm: the type write | OpenTPW/World/Park/ParkPathNeighbours.cs OpenTPW/World/Park/ParkState.cs  |
| `0x0053522d` | `FUN_005348d0`: the queue arm, laid type 0 or 3 | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053525a` | Queue arm: first of the four entrance-bond probes | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535323` | Queue arm: last entrance-bond probe | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535597` | Queue arm: end | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005357c7` | Verdict `FUN_00535670`: the `0x40` outside-the-park test, first for every tool | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535811` | Verdict `FUN_00535670`: the parent redirect for track types 12 and 17, made inline after `FUN_004d0af0` fetches the cell's own track record | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005358e9` | Queue verdict: the cash total skips queue over path | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005358f1` | Queue verdict: end of that guard | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535d63` | Verdict: end of the path arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536140` | `FUN_00536100` chooses its axis: a tie runs along Y | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005367ed` | `FUN_005367a0` leaves a bare cell alone | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053694b` | | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536a07` | The clear's queue arm zeroes the overlap counter under force | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536a37` | `FUN_005367a0`'s forced queue arm refunds only while the owner's cell is typed | OpenTPW.Tests/ParkPathBuildingTests.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536abf` | The clear's queue arm zeroes the overlap counter before its reset | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00536bc9` | The clear's reset, which type 4 and the queue arm jump to and the path arm repeats: bare, unlinked, unflagged, unowned, tile 55 | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053c755` | `FUN_0053c3f0`: the marker wave's phase gains 0.1 a frame unless paused | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x0053c773` | `FUN_0053c3f0`: the phase store | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x0053c7b1` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x0053c7f6` | | OpenTPW.Tests/ParkStrandedTests.cs OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x00540900` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00540d90` | | OpenTPW.Files/Formats/Sprite/SpriteBankFile.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x005419a4` | Sprites_LoadFolder: the folder's files sorted by name with _stricmp (FUN_00541210) before any loads | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00542075` | World sprite draw FUN_00542010: the flags word +0xc4 read | OpenTPW/World/Park/SpriteScript.cs  |
| `0x0054207d` | FUN_00542010: the alpha byte +0xa0 read into the draw's colour | OpenTPW/World/Park/SpriteScript.cs  |
| `0x005422b2` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x005422fb` | FUN_00542010: the flags word's 0x200 bit tested | OpenTPW/World/Park/SpriteScript.cs  |
| `0x005423a0` | | OpenTPW.Files/Formats/Sprite/SpriteBankFile.cs OpenTPW/UI/ScreenParticles.cs  |
| `0x00543725` | KART loader FUN_00543560: a saved ride through FUN_00545890 with its handle, which names the slot | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00544061` | KART loader FUN_00543560: each saved section handed to FUN_0054b2f0 | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x005443d2` | Track rides' table: 64 entries of 0xd0 bytes allocated zeroed (GMEM_ZEROINIT) | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x0054536d` | FUN_00545310: the stale test, the handle against slot OR entry[0] << 8 | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00545375` | FUN_00545310: the walk of the entry's section list +0xbc, from | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x0054538d` | FUN_00545310: the pass counter set to 2, above the back edge, so nothing is reset between passes | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x005453e1` | FUN_00545310: the walk's back edge, to here | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x0054566e` | FUN_00545610: the same stale test before freeing | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x005458ea` | FUN_00545890, a placement: the search for an entry whose entry[0] is nought stops at 0x40 | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00545a0b` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00546225` | FUN_00545890 with every entry taken: the read through a null entry | OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00546409` | | OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x00546439` | | OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x0054658c` | | OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x005466c0` | | OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x0054760f` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x005478fc` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00548499` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054856f` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00548584` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054997b` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x005499ab` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054a01d` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054a0e0` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054a185` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054a250` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054a2bd` | | OpenTPW/World/Park/ParkCarSounds.cs  |
| `0x0054a2ee` | | OpenTPW/World/Park/ParkCarSounds.cs  |
| `0x0054a366` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054a3a2` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054b065` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x0054e682` | | OpenTPW/Global/GameClock.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0054e6a6` | | OpenTPW/Client/Game.cs  |
| `0x0054e6df` | | OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0054e72a` | Game_StateMachine: FUN_0044e510 advances the one model at DAT_0079fcb4, outside the 31 ms loop | OpenTPW/VM/RideScript.cs  |
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
| `0x0054f47f` | | OpenTPW/World/Park/ParkRides.cs  |
| `0x0054f493` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f49b` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4ad` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4bf` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4c4` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f56b` | | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x0054f5fb` | Game_StateMachine: CALL FUN_00475360, the sprites' step, before the thing gate and outside the three-sweep cap | OpenTPW.Tests/ParkSweepCapTests.cs  |
| `0x0054f668` | | OpenTPW/Global/GameCalendar.cs OpenTPW/World/Level.cs OpenTPW/World/Park/ParkWeather.cs  |
| `0x0054f680` | | OpenTPW.Tests/ParkSweepCapTests.cs OpenTPW/Global/GameCalendar.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x0054f683` | Re-stamps the peep beat's baseline `[0x00878a1c]` inside the every-eighth-tick gate | OpenTPW/World/Park/ParkPeople.cs  |
| `0x0054f7bb` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f82d` | Game_StateMachine: TEST [0x00877d34],0x1f, the every-32nd-step block that sets the music's level | OpenTPW.Tests/ParkScreamTests.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f84e` | Game_StateMachine: the crowd's level held to 89 (0x59) before it is handed to the music | OpenTPW.Tests/ParkScreamTests.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f860` | Game_StateMachine: the music's level made nought while the world's state +0x1da738 is 4 | OpenTPW.Tests/ParkScreamTests.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f870` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f875` | Game_StateMachine, the every-32nd-step block: the word at 0x007b05cc read for the crowd voice's count (FUN_004c8d30) | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f8c8` | Game_StateMachine, the every-32nd-step block: CALL FUN_0055ab50, its last call | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f9f9` | | OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0054fa08` | The park frame's one call of `FUN_00557ab0`, after the 31 ms catch-up loop: every script's walks stepped once a frame | OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x0054fa0d` | Per-frame block: placed objects' fraction, 1/31 against its own baseline | OpenTPW/Global/GameClock.cs OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x0054fa38` | Per-frame block: particles' fraction, 1/62 | OpenTPW/Global/GameClock.cs  |
| `0x0054fa5c` | Per-frame block: peeps' and staff's fraction, 1/248.000007, driving `FUN_00518f90` | OpenTPW/Global/GameClock.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x0054fc2a` | Game_StateMachine, once a pass of the park's loop, drawn or not: the pass's sweep count [0x00879064] zeroed | OpenTPW/World/Park/ParkPeople.cs  |
| `0x005502f6` | | OpenTPW/Client/Players.cs  |
| `0x00551241` | STARTSCREAM: `(a + b) / 2`, clamped, for parameter 6 | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00551265` | STARTSCREAM: sets parameter 6 on the new handle | OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x00551701` | `FUN_005516b0`: clears the script's critical flag as its turn begins | OpenTPW/VM/RideScript.cs  |
| `0x00551715` | | OpenTPW/VM/RideScript.cs  |
| `0x00551724` | | OpenTPW/VM/RideScript.cs  |
| `0x00551844` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00551da3` | RSSE COPY: operand 0 tested before operand 1 is fetched; a literal leaves the PC on operand 1 | OpenTPW.Tests/RideScriptStackTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00551df5` | | OpenTPW.Files/Formats/Script/RideScriptFile.cs  |
| `0x00551f5f` | `PUSH 0x1c`: the effect list's nodes are 28 bytes | OpenTPW/World/Ride/RideEffects.cs  |
| `0x00552376` | `KILLOBJ`: steps to the next node before it unlinks the match, with no break | OpenTPW/World/Ride/RideEffects.cs  |
| `0x00552950` | | OpenTPW/VM/RideScript.cs  |
| `0x00552952` | RSSE TRIGANIM: the script speed divisor pushed as the play rate | OpenTPW/VM/RideScript.cs  |
| `0x005529bc` | | OpenTPW/VM/RideScript.cs  |
| `0x00552ab0` | | OpenTPW.Tests/RideScriptModelTests.cs  |
| `0x00552b14` | RSSE WAITANIM first visit: +0xa4 cleared (and +0xa8 set to 0xffff after it) | OpenTPW.Tests/RideScriptAnimationTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00552b1a` | RSSE WAITANIM first visit: +0xa8, the looping key, set to 0xffff, model or not | OpenTPW.Tests/RideScriptAnimationTests.cs OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00552be4` | RSSE LOOPANIM: the script speed divisor pushed as the play rate | OpenTPW/World/Park/ParkRides.cs  |
| `0x00552c1a` | `TRIGWAITANIM` handler | OpenTPW/VM/RideScript.cs  |
| `0x00552cfc` | RSSE TRIGWAITANIM re-entry: channel 0 role read raw through FUN_00473fb0, no pose-flag test | OpenTPW/VM/RideScript.cs  |
| `0x00552d08` | RSSE TRIGWAITANIM re-entry: the accessor's returned flags thrown away; the mark read | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00552d11` | RSSE TRIGWAITANIM re-entry: the role plus one equal to the mark jumps to the shared clear at 0x005535f4 | OpenTPW/VM/RideScript.cs  |
| `0x00552fe5` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00553158` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x005531f9` | | OpenTPW/VM/RideScript.cs  |
| `0x00553435` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00553470` | | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x005535d6` | `TRIGWAITANIM`: the not-equal re-entry rewinds and writes `+0x98 = 0` | OpenTPW/VM/RideScript.cs  |
| `0x005535f4` | `TRIGWAITANIM`: the equal branch clears the mark `+0xbc` and falls through | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x0055374c` | RSSE GETANIM_CH with no model: the flag word zeroed, so +0x48 is stored as it stands | OpenTPW.Tests/RideScriptChannelTests.cs OpenTPW/VM/RideScript.cs  |
| `0x0055374e` | | OpenTPW/VM/RideScript.cs  |
| `0x005538eb` | RSSE WAIT4ANIM handler: +0xa4 nought leaves at once; passed, it is cleared and the turn goes on | OpenTPW.Tests/RideScriptClockTests.cs  |
| `0x005539a9` | RSSE JSR handler | OpenTPW/VM/RideScript.cs  |
| `0x005539d8` | RSSE JSR: the return address ORed with the label tag 0x20000000 | OpenTPW/VM/RideScript.cs  |
| `0x00553a07` | RSSE JSR: no stack or no room parks the PC (the jump follows) | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553a1c` | RSSE JSR: a non-label operand leaves through NOP | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553a24` | RSSE JSR: a label operand goes into the PC, after either arm | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553a32` | RSSE RETURN handler | OpenTPW/VM/RideScript.cs  |
| `0x00553a63` | RSSE RETURN: no stack or no frame parks the PC | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553a66` | RSSE RETURN: the popped word's tag test and strip | OpenTPW/VM/RideScript.cs  |
| `0x00553a74` | RSSE RETURN: a popped word without the label tag leaves through NOP | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553c1e` | RSSE PUSH handler | OpenTPW/VM/RideScript.cs  |
| `0x00553c89` | RSSE PUSH: the value written to +0x48 after a push | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553cac` | RSSE PUSH: the value written to +0x48 on a stack error | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553cba` | RSSE POP handler | OpenTPW/VM/RideScript.cs  |
| `0x00553d1d` | RSSE POP: an empty stack parks the PC, then the store tail writes 0 | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553d25` | RSSE HUSH handler | OpenTPW/VM/RideScript.cs  |
| `0x00553d95` | RSSE HUSH: the value written to +0x48 | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x00553daa` | RSSE HOP handler | OpenTPW/VM/RideScript.cs  |
| `0x00553dfa` | RSSE heap error: logs "RSSE: Heap Error" and changes nothing | OpenTPW.Tests/RideScriptStackTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00553e72` | RSSE ADD: a literal operand 0 leaves through NOP before +0x48 is written | OpenTPW.Tests/RideScriptStackTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00554052` | RSSE DIV: the IDIV, which faults on INT_MIN / -1 (MOD has its own at 0x00554120) | OpenTPW/VM/RideScript.cs  |
| `0x0055470b` | | OpenTPW/VM/RideScript.cs  |
| `0x0055473d` | | OpenTPW/VM/RideScript.cs  |
| `0x00554777` | | OpenTPW/VM/RideScript.cs  |
| `0x00554913` | | OpenTPW/VM/RideScript.cs  |
| `0x0055496e` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00554b11` | | OpenTPW/VM/RideScript.cs  |
| `0x00554c07` | | OpenTPW/World/Ride/RideState.cs  |
| `0x00554c3e` | | OpenTPW.Tests/RideScriptHeadTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00554c89` | | OpenTPW.Tests/RideScriptHeadTests.cs  |
| `0x00554ca9` | | OpenTPW.Tests/RideScriptHeadTests.cs  |
| `0x00554caf` | | OpenTPW/VM/RideScript.cs  |
| `0x00554ccb` | | OpenTPW/VM/RideScript.cs  |
| `0x00554cd3` | | OpenTPW/VM/RideScript.cs  |
| `0x00554cf3` | | OpenTPW/VM/RideScript.cs  |
| `0x00554d13` | | OpenTPW/VM/RideScript.cs  |
| `0x00554d26` | | OpenTPW.Tests/RideScriptHeadTests.cs OpenTPW/VM/RideScript.cs  |
| `0x0055512e` | `SPAWNCHILD` tests the loader's answer (`TEST EAX,EAX / JZ`) | OpenTPW/VM/RideScript.cs  |
| `0x005555ad` | `GETVARINCHILD`/`GETVARINPARENT`: shared tail that bounds the index from above only | OpenTPW/VM/RideScript.cs  |
| `0x005555e9` | RSSE BOUNCESETNODE: the raw operand word stored to +0x70, no tag test | OpenTPW/VM/RideScript.cs  |
| `0x00555939` | RSSE store tail: +0x48, then the variable only for a 0x40 operand (POP, HOP and others) | OpenTPW.Tests/RideScriptStackTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00555afe` | RSSE WALKON: after FUN_00556f40 returns, nothing writes +0x48 | OpenTPW.Tests/RideScriptStackTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00555ee6` | RSSE STARTSCREAM: the new handle, or a refusal's 0, stored over +0xd0 | OpenTPW.Tests/ParkScreamChainTests.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x00555f6b` | `SINGLESCREAM` picks its branch on the second operand (`CMP ESI,EDI / JGE`) | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00556009` | `SCREAMLEVEL` writes the volume call's return over the scream handle at `+0xd0` | OpenTPW/World/Park/ParkAudio.cs  |
| `0x005560d0` | RSSE FINDSCRIPTRAND: SHR 1, an unsigned halving of the generator's answer, as RAND's at 0x0055398f | OpenTPW.Tests/RideScriptRelativeTests.cs  |
| `0x0055641b` | | OpenTPW/VM/RideScript.cs  |
| `0x0055646d` | | OpenTPW/VM/RideScript.cs  |
| `0x005567a3` | RSSE dispatcher: a word without the opcode tag logs "Bad instruction" and parks | OpenTPW.Tests/RideScriptStackTests.cs  |
| `0x005567d8` | The VM dispatcher's jump table, 106 opcodes | OpenTPW.Files/Formats/Script/Opcode.cs  |
| `0x005569b8` | `SETOBJPARAM`'s type dispatch: jump table | OpenTPW/World/Ride/RideEffects.cs  |
| `0x005569c4` | `SETOBJPARAM`'s type dispatch: byte map | OpenTPW/World/Ride/RideEffects.cs  |
| `0x00556a5c` | `COAST`'s eight-entry sub-op table | OpenTPW/World/Ride/RideState.cs  |
| `0x00556fce` | WALKON FUN_00556f40: the leg from the distance between the walk and head nodes, x100, nought to 100 | OpenTPW/VM/RideScript.cs  |
| `0x00557276` | WALKOFF FUN_005571a0: a new leg from the distance between the off-from and off-to nodes, x100 | OpenTPW/VM/RideScript.cs  |
| `0x00557b3a` | FUN_00557ab0, a bounce rider's place: the node looked up by id in the walk space 0x800 (FUN_0044b220) | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00557e79` | FUN_00557d80, arriving on the ride (state 1 to 2): start restamped from the clock; due left alone | OpenTPW.Tests/RideScriptWalkTests.cs OpenTPW/VM/RideScript.cs  |
| `0x00558018` | FUN_00557d80, a walk off's end (state 3 to 4): the state written alone | OpenTPW.Tests/RideScriptWalkTests.cs OpenTPW/VM/RideScript.cs  |
| `0x005587f0` | | OpenTPW.Files/Formats/Script/RideScriptFile.cs  |
| `0x00558c45` | Script loader FUN_005587f0: the bounce node base +0x70 set to 1 | OpenTPW/VM/RideScript.cs  |
| `0x00558c4f` | Script loader FUN_005587f0: +0xa8, the looping key, starts at 0xffff | OpenTPW/VM/RideScript.cs  |
| `0x00558d2e` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d5b` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d68` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d8a` | | OpenTPW/World/Ride/RideNodes.cs  |
| `0x00558db7` | | OpenTPW/World/Ride/RideNodes.cs  |
| `0x00558db9` | | OpenTPW/VM/RideScript.cs  |
| `0x0055924b` | Script death: the sound-script arm, first of three identical arms | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x0055928c` | Script death: the child arm | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x005592cd` | Script death: the arm that tells the parent | OpenTPW/VM/RideScriptScheduler.cs  |
| `0x005597a0` | the `RSSE` arm of the restore chain: reads each script's whole 244-byte struct back from the file, program counter included, so a loaded park's scripts resume mid-flight | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW.Files/Formats/Save/ParkThingStates.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkRides.cs OpenTPW/World/Ride/RideEffects.cs  |
| `0x005598d7` | | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW.Tests/ParkScriptStateTests.cs OpenTPW/VM/RideScriptScheduler.cs  |
| `0x005599d3` | | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x00559af1` | FUN_005597a0: the stack size +0x54 from the saved stack block's length | OpenTPW/World/Park/ParkRides.cs  |
| `0x00559d3d` | | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW/VM/RideScript.cs  |
| `0x00559da7` | | OpenTPW.Files/Formats/Save/ParkScriptStates.cs  |
| `0x00564790` | | OpenTPW.Files/Formats/Sprite/SpritePackFile.cs  |
| `0x0056695c` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00579e00` | | OpenTPW/Render/Assets/Asset.cs  |
| `0x0057c620` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x00580320` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00582170` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x00587380` | | OpenTPW/Client/Game.cs OpenTPW/UI/LoadingScreen.cs  |
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
| `0x005a0a50` | The advisor metadata table's filler: a static initializer run before WinMain, filling 0x0076e300's 351 rows | OpenTPW/UI/Park/ParkLines.cs  |
| `0x005accf0` | | OpenTPW/Client/Players.cs  |
| `0x005aef50` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af530` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs  |
| `0x005af680` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x005af740` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af810` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af940` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afb00` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afc30` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW/Client/Players.cs OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005afc60` | | OpenTPW.Files/Formats/Save/PlayerFile.cs OpenTPW.Tests/ProfilePreservationTests.cs  |
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
| `0x005d6060` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005d60c0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x005d6110` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005d8bac` | | OpenTPW/World/Level.cs  |
| `0x005d96fc` | | OpenTPW/World/Sky.cs  |
| `0x005da3c0` | A bare RET: the game's log and assert sink, and the sprite scripts' end word | OpenTPW/World/Park/SpriteScript.cs  |
| `0x005dd034` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005dfd2d` | Lobby camera constructor: the leave state `+0x14` set to 0 | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e0470` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x005e052f` | Lobby camera state 2: writes the render camera's darkening target `+0x60` as the radius closes | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e06e4` | Lobby camera state 1's arrival: plays the gate's M1 once (`0x005d83f0( 0, 0 )` on `island+8`) | OpenTPW/World/Lobby/LobbyGate.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1100` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x005e11f7` | The lobby camera update's tail loop: an idle isle starts its M1 or M2 at random | OpenTPW.Tests/LobbyCountedGapsTests.cs OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005e13fb` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e184c` | The island lobby camera's update, every pass with a player picked: arms the advisor's 90-second repeat of response 394 or 395 | OpenTPW.Tests/LobbyCountedGapsTests.cs OpenTPW/World/LobbyCameraMode.cs  |
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
| `0x005f0bd1` | Map opener FUN_005f0b40: closes the open park screen (FUN_00485b40) before it hides the layer | OpenTPW/UI/Park/ParkMapScreen.cs OpenTPW/UI/UiWindow.cs  |
| `0x005f17ef` | The map's handler `FUN_005f1130`, key-up case: a plain Escape let go closes the map | OpenTPW.Tests/ParkEscapeOnReleaseTests.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkMapScreen.cs  |
| `0x005f2565` | Map overlay FUN_005f2380: a thing's satisfaction average, FUN_004e1e30 | OpenTPW/UI/Park/ParkMapScreen.cs  |
| `0x005f5fa0` | The sound clock: wall-time milliseconds | OpenTPW/World/Park/ParkAudio.cs  |
| `0x005f8ae0` | | OpenTPW/Client/GameDir.cs  |
| `0x006584df` | | OpenTPW/UI/WindowStack.cs  |
| `0x00658b5b` | UI: posts a release (0x10004) to the control that took the press | OpenTPW.Tests/ParkFirstPersonRightButtonWalkTests.cs OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x00658f97` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00659a58` | | OpenTPW/UI/UiMesh.cs  |
| `0x0065d3a3` | | OpenTPW/UI/UiControl.cs  |
| `0x0065da8d` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x0065f8b2` | Base control proc, the press: a control's flag 0x8 skips the double click's test | OpenTPW/UI/WindowStack.cs  |
| `0x0065f8c7` | Base control proc: a press within 500 ms of the button's stamp is a double click's second (0x10007) | OpenTPW.Tests/LeftClickTests.cs OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x0065f8d3` | Base control proc: the double click's compare, strictly less than 500 ms | OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x0065f969` | Base control proc: a release under 500 ms after its press posts the click 0x10006 | OpenTPW.Tests/LeftClickTests.cs OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x0065f977` | Base control proc: the click 0x10006 is posted with the press's point | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/UiControl.cs  |
| `0x0065f9af` | Base control proc: an unspoiled release stamps the button with its time | OpenTPW.Tests/LeftClickTests.cs OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x0065f9bd` | Base control proc: any other release clears the button's stamp | OpenTPW.Tests/LeftClickTests.cs OpenTPW.Tests/ParkFirstPersonRightClickTests.cs OpenTPW/UI/WindowStack.cs  |
| `0x0065fa33` | Base control proc: a move spoils a press that strayed more than 6 units | OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x0065fab7` | Base control proc: the stray compare across, more than 6 | OpenTPW.Tests/LeftClickTests.cs OpenTPW.Tests/ParkFirstPersonRightClickTests.cs OpenTPW/UI/UiControl.cs OpenTPW/UI/WindowStack.cs  |
| `0x0065fadb` | Base control proc: the stray compare down, more than 6 | OpenTPW.Tests/ParkFirstPersonRightClickTests.cs  |
| `0x0065fd0e` | | OpenTPW/UI/UiWindow.cs  |
| `0x0065fd58` | | OpenTPW/UI/UiControl.cs  |
| `0x0065fe75` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x006644d2` | FUN_00664495, after a list add or removal: with count <= visible the slider is disabled and the top row left | OpenTPW.Tests/UiListTests.cs OpenTPW/UI/UiList.cs  |
| `0x006656ba` | FUN_006656a0, a list's answer to the pointer's move: CMP [EDX+0x11c],0, no row selected while a button pressed on the list is down | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/UiList.cs  |
| `0x00665d22` | List proc FUN_00665c35, the move 0x10003: FUN_006656a0, then returns before the base proc, so no stray is judged | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/UiList.cs  |
| `0x00665dbd` | List proc FUN_00665c35: a click of any button but the left goes to FUN_0066563d (select the row under it, post 0x402) | OpenTPW/UI/UiList.cs  |
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
| `0x0066bb9b` | | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/UiControl.cs OpenTPW/UI/WindowStack.cs  |
| `0x0066c5a4` | A polygon region's contains test: the crossings count, in whole virtual units | OpenTPW/UI/UiControl.cs  |
| `0x0067a830` | __ftol: chop, FISTP qword, the low dword in EAX; the integer indefinite's low dword is nought | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x006804da` | The runtime's start-up: the FPU at 53-bit precision | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x006b0680` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b15f0` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b4aa0` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b88d7` | The other read of `+0xc`: a play replaces a live handle only for a higher priority | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006b8930` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x006bb9f9` | The effect record's `+0xc` copied into the voice's priority - `audio.md` | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006bbb80` | | OpenTPW/World/Park/ParkCarSounds.cs  |
| `0x006bbbe0` | | OpenTPW/World/Park/ParkCarSounds.cs  |
| `0x006bbfb1` | | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006bc2d0` | A variation's wait between a held voice's samples: `min + LCG % (max - min)` | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bcc71` | A held voice's fade step taking the no-channel exit, so its stop is a cut | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bd9b0` | A held voice's stop: its own children only | OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdae0` | A held voice's tick: extend the chain, prune it | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdb50` | A held voice's chain extension | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdb88` | Make a child once the clock passes the newest child's time | OpenTPW.Tests/ParkScreamChainTests.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bdc3e` | A pruned child deleted without its channel being stopped | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006bde00` | The music's voice class (vtable 0x0070a498), slot +0x00: queues the next sample as the current one runs out | OpenTPW/World/Park/ParkAudio.cs  |
| `0x006be55e` | FUN_006be450: no zone record holds the parameter, so the controller's value is made 0x7f and the voice parked (flag 0x40) | OpenTPW/World/Park/ParkAudio.cs  |
| `0x006c0314` | The SFX.map header word that says the weights are shares | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006c0510` | The SFX.map loader's walk: effects, variation headers, samples, zones | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006c0676` | Running-total variation weights turned into shares at load | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs  |
| `0x006c3a80` | A held voice's child: variation, sample, and the time of the next | OpenTPW/Audio/SoundCategory.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c3d80` | Whether a variation takes its wait from the voice's parameter (mask `+0x18` bit 4) | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c3e00` | A held voice's next variation: the first first, then by the zones and the parameter | OpenTPW.Files/Formats/Sound/SoundCategoryFile.cs OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c3e1c` | The held voice's parameter byte read for the variation pick | OpenTPW/World/Park/ParkAudio.cs  |
| `0x006c3e26` | | OpenTPW/World/Park/ParkCarSounds.cs  |
| `0x006c3f49` | No zone covers the parameter: the chain is parked | OpenTPW/World/Park/ParkScreams.cs  |
| `0x006c581b` | The one call site of QMixer's SetDistanceMapping, gated on a request bit that nothing writes | OpenTPW/Audio/Audio.cs OpenTPW/Audio/AudioListener.cs  |
| `0x006c9270` | | OpenTPW/Audio/AudioClip.cs  |
| `0x006e71b4` | | OpenTPW.Tests/PeepHeadingTests.cs OpenTPW/World/Park/PeepHeading.cs  |
| `0x006fe2bc` | | OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x006fe2c4` | | OpenTPW/World/Park/ParkBumperBoats.cs  |
| `0x006fe408` | | OpenTPW.Files/Formats/Map/ItemHeightMapFile.cs  |
| `0x006fe6bc` | | OpenTPW.Tests/RideAnimationsTests.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x006fe7d4` | Float 2/3: the share of the panel's width for half the footprint's diagonal (FUN_004689f0) | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x006fe7e4` | Float -pi/4: the preview's tip, the sine's index (FUN_00468e50) | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x006fe7e8` | Float pi/4: the preview's tip, the cosine's index (FUN_00468e50) | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x006fe804` | Float -0.0004: taken off the preview's angle each millisecond (FUN_00468e50) | OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x006febe4` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006febec` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006fec00` | The constant 0.01 in the lobby path sampler's `percent * 0.01 * segments` | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x006fec08` | | OpenTPW.Files/Formats/Model/AnimationFile.cs OpenTPW.Tests/RideAnimationsTests.cs OpenTPW/World/Ride/AnimTimeControl.cs OpenTPW/World/Ride/RideAnimations.cs  |
| `0x006fecb8` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00700550` | | OpenTPW/World/Park/ParkState.cs  |
| `0x00700558` | Float 4.0f, the longest queue's floor (FUN_004dda40) | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x007005b0` | Float -0.8f, the sideshow excitement's factor | OpenTPW.Tests/ParkRideScoreTests.cs OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007005b8` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007005c0` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007005d0` | | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x00700750` | Float 1/3000, the price factor's scale | OpenTPW.Tests/ParkRideScoreTests.cs OpenTPW/World/Park/ParkRideScore.cs  |
| `0x00700754` | Float -0.1f, the price factor's step | OpenTPW.Tests/ParkRideScoreTests.cs OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007007a4` | | OpenTPW.Tests/ParkBoardingTests.cs OpenTPW/World/Park/Peep.cs  |
| `0x00700848` | Double: the rest one turn of staff walking costs, before the grade multiplier | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x00700850` | Double: the same for mood | OpenTPW/World/Park/StaffBehaviour.cs  |
| `0x007009a0` | Float 2.0f: the most speed FUN_00510190 hands the mover | OpenTPW/World/Park/Peep.cs  |
| `0x007009a8` | Double 26214.4: a speed of one in the mover's max_force | OpenTPW/World/Park/Peep.cs  |
| `0x007009b0` | Double 13107.2: a speed of one in the mover's max_speed, a fifth of a cell a sweep | OpenTPW/World/Park/Peep.cs  |
| `0x00700eb8` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00700ec8` | | OpenTPW/World/Park/ParkBumperCars.cs  |
| `0x00700f20` | | OpenTPW/World/Park/ParkCarSounds.cs  |
| `0x00701f50` | | OpenTPW/World/Sky.cs  |
| `0x00701f54` | | OpenTPW/World/Sky.cs  |
| `0x00701f58` | | OpenTPW/World/Sky.cs  |
| `0x00701f5c` | | OpenTPW/World/Sky.cs  |
| `0x00701f64` | | OpenTPW/World/Sky.cs  |
| `0x00701f7c` | | OpenTPW/World/Lobby/LobbyScript.cs OpenTPW/World/Sky.cs  |
| `0x0070200c` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x007029cc` | | OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00702ae4` | | OpenTPW/World/Lobby/LobbyFlyer.cs  |
| `0x00702c78` | Lobby camera: +0.05. Near the target the look speed takes 0.05 of the cap off itself per frame, with no delta; the homing turn steps 0.05 x delta | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c7c` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c84` | Lobby camera: -0.05. Far from the target the look speed subtracts this times the cap per frame, which adds 0.05 of the cap, with no delta | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c8c` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00702c94` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00702ca4` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x0070a2e0` | vtable of the held-chain voice class (flags `0x0404`) | OpenTPW/World/Park/ParkScreams.cs  |
| `0x007396c8` | The `Info.Shape` alphabet, 19 rows of `{char, kind, bit}` | OpenTPW.Files/Formats/ItemDescriptionFile.cs OpenTPW.Tests/ParkEntryCellTests.cs  |
| `0x007397b0` | | OpenTPW.Files/Formats/ItemHoarding.cs  |
| `0x00741c8c` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00741d40` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00742178` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00744b30` | | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00744e3c` | The compiled item schema's `HasQueue` entry, after `IsChoosable`: descriptor `+0x40` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0074669c` | The compiled .sam schema's Upgrades entry: three tiers of 16 leaves, 0x40 apart | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00747930` | | OpenTPW/World/Park/ParkItemCatalogue.cs  |
| `0x0074c9c4` | Float 9.999: where the camcorder sweep parks or puts back an axis going positive, `cell * 10 + 9.999` | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0074c9c8` | Float 1.01: the camcorder sweep's tie-break | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0074c9d0` | Float 0.001: the camcorder sweep's nudge after an open crossing that left the cell unchanged | OpenTPW/World/Park/ParkCamcorderCameraMode.cs  |
| `0x0074ced0` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x0074cf58` | | OpenTPW.Tests/LobbyModelAnimationTests.cs  |
| `0x0074f480` | Sprite script, the held balloon (word 1650): frame 0, jump to itself | OpenTPW/World/Park/Balloon.cs  |
| `0x0074f490` | Sprite script words 1654..1664: the let-go loop, frame 1 and the alpha down by 20 while it is nought or more | OpenTPW/World/Park/SpriteScript.cs  |
| `0x0074f4c0` | Sprite script, the let-go balloon (word 1666): the alpha to 250, then into the loop at 0x0074f490 | OpenTPW.Tests/ParkBalloonTests.cs OpenTPW/World/Park/SpriteScript.cs  |
| `0x0074f558` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x0074f920` | | OpenTPW/UI/Screens/MessageBox.cs  |
| `0x0074fa98` | | OpenTPW/UI/Park/ParkGadget.cs OpenTPW/UI/Park/ParkViewfinder.cs  |
| `0x0074fb50` | Status code to colour, for the object windows and the all-items rows (`FUN_00485f60`'s codes) | OpenTPW/UI/Park/ParkClosedStatus.cs  |
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
| `0x00752a42` | The handle 0x23's 16-point outline in the gadget's stream (op 4, sub-op 4) | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752ac8` | The gadget body 0x1d's 23-point outline in the gadget's stream (op 4, sub-op 4) | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f10` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f24` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f30` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00753c68` | | OpenTPW.Tests/LeftClickTests.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x00753f50` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x007540c0` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x007540cc` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x00754cf8` | Layout stream of the buy screen | OpenTPW/UI/Park/ParkBuyScreen.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x00755150` | Layout stream of the ride object window | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/Park/ParkObjectWindow.cs  |
| `0x00757f60` | Stream: the island panel, its root's 23-point outline included | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x0075c7f0` | The three hurry words 0, 25, 50 a person's +0xc2 is written from (FUN_004ff730 and others) | OpenTPW.Tests/PeepBehaviourTests.cs OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x0075c7f2` | The hurry-speed word 25, in the table at 0x0075c7f0 (0, 25, 50) | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x0075c7f8` | Words 60, 80, 100, 120, 140: the base speeds (a guest's drawn % 5, a member of staff's by rest) | OpenTPW/World/Park/Peep.cs  |
| `0x0075c7fc` | Word 100: what FUN_004fa870 divides the three speed words' sum by | OpenTPW/World/Park/Peep.cs  |
| `0x0075c804` | | OpenTPW/World/Park/Thoughts.cs  |
| `0x0075c808` | Float 1.0: a held balloon's height before the dip and the bob | OpenTPW/World/Park/Balloon.cs  |
| `0x0075c80c` | Dword 20: the bob's rows | OpenTPW/World/Park/Balloon.cs  |
| `0x0075c810` | The bob's table: twenty rows of three floats; only the middle, 0 to 0.2 and back, is not nought | OpenTPW/World/Park/Balloon.cs  |
| `0x0075c904` | Float 1.5: the bob's scale | OpenTPW/World/Park/Balloon.cs  |
| `0x0075c90c` | Float 0.5: the gap's divisor in a held balloon's dip | OpenTPW/World/Park/Balloon.cs  |
| `0x0075c910` | Float 0.35: how far behind in the sweep a held balloon's trailing sample is | OpenTPW/World/Park/Balloon.cs  |
| `0x0075d0f8` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x0075d178` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x0075d798` | String "Customer returning a costume." | OpenTPW/World/Park/ParkRideOperation.cs  |
| `0x0075d914` | String "The bus is coming!  RUUUUUUUUUUN!!!!", FUN_004ff730's log line when the bus reports 3 | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x007622b0` | | OpenTPW/World/Park/CellLine.cs OpenTPW/World/Park/MapStep.cs  |
| `0x0076338c` | The queue pieces table, twelve bytes a record | OpenTPW/World/Park/ParkQueues.cs  |
| `0x00763b38` | The 20-entry marker texture table (`blue`, `red`, ... `m_link`, `m_end`) | OpenTPW/World/Park/ParkBuildMarkers.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00763f88` | Sprite kinds table: fourteen bare kind names | OpenTPW/World/Park/Balloon.cs OpenTPW/World/Park/ParkGuestSprites.cs OpenTPW/World/Park/Thoughts.cs  |
| `0x00764030` | The four kid banks `Sprites_LoadFolder` loads before its sweep | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00764090` | String "balloons", sprite kind 10's name in the table at 0x00763f88 | OpenTPW/World/Park/Balloon.cs  |
| `0x00764178` | The track ride templates: 14 of 0xd0 bytes, BumperType -1's first, each's first dword its BumperType | OpenTPW/World/Park/ParkBumperCars.cs OpenTPW/World/Park/ParkTrackRideTable.cs  |
| `0x00765280` | The opcode table `{name*, operandCount*}`, eight bytes a record | OpenTPW.Files/Formats/Script/Opcode.cs  |
| `0x00765c18` | String "RSSE: Heap Error" | OpenTPW/VM/RideScript.cs  |
| `0x00765c2c` | String "RSSE: Stack Error" | OpenTPW/VM/RideScript.cs  |
| `0x00768ab4` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00768fb8` | `AdvisorResponseTable` - see `advisor-park.md` | OpenTPW/Client/Diagnostics/DebugConsole.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/Park/ParkLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0076dc18` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0076e300` | | OpenTPW/UI/Park/ParkLines.cs  |
| `0x00774c18` | The lobby's full-screen root control that `FrontEnd_Init` loads (callback `0x005d58b0`) | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x00774ce0` | | OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00774da0` | | OpenTPW/UI/Park/ParkMapScreen.cs  |
| `0x0077c480` | UI click limit, 500 ms | OpenTPW/UI/WindowStack.cs  |
| `0x0077c488` | Whether the interface posts input: set as the window comes active, cleared as it goes (WM_ACTIVATEAPP) | OpenTPW/Client/Renderer.cs  |
| `0x00782f40` | | OpenTPW.Tests/ParkCarSoundsTests.cs  |
| `0x00782f44` | | OpenTPW.Tests/ParkCarSoundsTests.cs  |
| `0x00782fa0` | | OpenTPW.Tests/ParkCarSoundsTests.cs  |
| `0x00783540` | | OpenTPW.Tests/ParkCarSoundsTests.cs  |
| `0x00783544` | | OpenTPW.Tests/ParkCarSoundsTests.cs  |
| `0x007835a0` | | OpenTPW.Tests/ParkCarSoundsTests.cs  |
| `0x00785058` | `PeepInfo.SmallHappinessChange`, 5 in Lost Kingdom, read as a byte | OpenTPW.Tests/ParkMoodChangeTests.cs OpenTPW/World/Park/ParkAdmission.cs  |
| `0x0078505c` | `PeepInfo.MediumHappinessChange`, 15 in Lost Kingdom, read as a byte | OpenTPW.Tests/ParkMoodChangeTests.cs OpenTPW/World/Park/ParkAdmission.cs  |
| `0x00785060` | `PeepInfo.BigHappinessChange`, 25 in Lost Kingdom, read as a byte | OpenTPW.Tests/ParkMoodChangeTests.cs OpenTPW/World/Park/ParkAdmission.cs  |
| `0x00785064` | PeepInfo.PerfectRide, an int: happiness for a gap under 5 (FUN_004fdcc0) | OpenTPW/World/Park/ParkAdmission.cs  |
| `0x00785068` | PeepInfo.GoodRide, an int: happiness for a gap under 15 | OpenTPW/World/Park/ParkAdmission.cs  |
| `0x0078506c` | PeepInfo.OKRide, an int: happiness for a gap under 40 | OpenTPW/World/Park/ParkAdmission.cs  |
| `0x00785070` | PeepInfo.RideVomitDivisor, an int: the excitement over it, times the fullness | OpenTPW/World/Park/ParkAdmission.cs  |
| `0x007850e0` | | OpenTPW/World/Park/ParkAdmission.cs  |
| `0x007854f4` | | OpenTPW/Global/GameCalendar.cs  |
| `0x00785914` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00785970` | The game's global clock object; `GameClock_Pause`/`Resume` act on it and `Game_Pause` freezes it | OpenTPW/Global/GameCalendar.cs OpenTPW/Global/GameClock.cs OpenTPW/VM/RideScript.cs  |
| `0x00786b84` | | OpenTPW/Global/GameClock.cs  |
| `0x00786b90` | | OpenTPW/Client/SaveFolder.cs  |
| `0x00786ba4` | | OpenTPW/Global/GameClock.cs OpenTPW/UI/UiWindow.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/World/Level.cs  |
| `0x0078d8d8` | | OpenTPW.Files/Formats/Save/ConfigFile.cs OpenTPW/Client/GameOptions.cs  |
| `0x0078d90e` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x00790a88` | Camera scroll term, the first of three FUN_0042aab0 zeroes when it places the look-at | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x00790a90` | Camera scroll term, the last of the three FUN_0042aab0 zeroes | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x007afd08` | The preview record FUN_004689f0 fills: instance, root matrix, angle, panel, the fit's five floats, the clock | OpenTPW.Tests/ParkObjectPreviewTests.cs OpenTPW/UI/Park/ParkObjectPreview.cs  |
| `0x007b05cc` | The packed cell (a word) whose neighbourhood the crowd voice's level is counted in (read at 0x0054f875) | OpenTPW/World/Park/ParkAudio.cs  |
| `0x007c24c8` | The open park screen's control, nought when none is | OpenTPW/World/Level.cs  |
| `0x007cb2fc` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x007cc1e4` | The buy screen's waiting row, an item id as a word; nought is none | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x007cc1f0` | The millisecond reading (FUN_0065968e) taken when the buy screen's waiting row was set | OpenTPW/UI/Park/ParkBuyScreen.cs  |
| `0x007cc4b8` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x007cdb98` | | OpenTPW/World/Park/ParkState.cs  |
| `0x007cdba0` | The static initialisers of the eight ring-order directions | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x007ced88` | | OpenTPW/World/Park/PeepBehaviour.cs  |
| `0x007cedd4` | The bob's phase, a global only FUN_004fa030 writes, never reset | OpenTPW/World/Park/Balloon.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x007cedd8` | The bob's count toward its next step, a float | OpenTPW/World/Park/Balloon.cs OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x007cfb90` | The thing table: stride 20, a thing's pointer by its handle | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x00803a20` | Sound category slot: ambient | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a24` | Sound category slot: kids | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a28` | Sound category slot: rides | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a2c` | | OpenTPW/UI/UiSounds.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a30` | Sound category slot: staff | OpenTPW/World/Park/ParkAudio.cs  |
| `0x00803a3c` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0080ced8` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x0080cef8` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00818800` | The footprint grid FUN_0052c5b0 fills: 16 columns of 16 dwords, the width at +0x400 and the depth at +0x404 | OpenTPW/UI/Park/ParkFootprintPicture.cs  |
| `0x0081b740` | The pending list of run ends Backspace pops; count `DAT_00820a8c` | OpenTPW.Tests/ParkBuildModeTests.cs OpenTPW/World/Park/ParkBuildMode.cs  |
| `0x00877d34` | | OpenTPW/Global/GameCalendar.cs OpenTPW/Global/GameClock.cs OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkPeople.cs  |
| `0x00878128` | | OpenTPW/Global/GameClock.cs  |
| `0x008786bc` | The per-frame clock sample the three beat fractions share | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00878a1c` | The peep beat's baseline, re-stamped at `0x0054f683` | OpenTPW/World/Park/ParkPeople.cs  |
| `0x00879064` | The thing sweeps run in this pass of the park's loop, held to three (0x0054f680) | OpenTPW/World/Park/ParkPeople.cs  |
| `0x008791a0` | | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW.Tests/ParkScriptStateTests.cs  |
| `0x008bcbcc` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00f82884` | The lobby's front-end object pointer; the message box's pause test wants it gone | OpenTPW/Global/GameClock.cs OpenTPW/World/Level.cs  |
| `0x00faa598` | The base control proc's record for button 0: state word, press point, then the stamp at +8 (stride 0xc a button) | OpenTPW/UI/WindowStack.cs  |
| `0x00faa5a0` | The left button's click stamp: the press's time, the release's after a click, nought after any other release | OpenTPW/UI/WindowStack.cs  |
| `0x00faa5ac` | UI: the right button's time stamp (0x00faa5a0 + 1 * 0xc) | OpenTPW/UI/WindowStack.cs  |
| `0x00faa638` | The hold on every control's timer: set by FUN_00662411, read by FUN_006622d2, cleared by FUN_00662420 | OpenTPW/UI/UiTimer.cs  |
| `0x00fb1f20` | The sound engine's one random seed | OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Park/ParkScreams.cs  |
