# Executable addresses cited in source

Seed generated mechanically from code comments. One row per address; fill in the 'What it is' column from the citing comment, then make the comment point here.

**This is an index of what our source cites, not the decode.** The per-subsystem pages beside it hold
the traced detail — the field tables, the state machines, the evidence and the confidence markers —
and between them they carry several hundred addresses that are cited in no code comment at all. If an
address here has an empty 'What it is', try the subsystem page first: `park.md`, `park-engine.md`,
`ride-operation.md`, `hud.md`, `weather.md`, `advisor-park.md`, `ui.md`, `lobby.md`, `boot.md`,
`scenes.md`, `audio.md`, `render-states.md`, `saves.md`. See `../README.md` for what each covers.

| Address | What it is | Cited in |
|---|---|---|
| `0x00402938` | Shape reader `FUN_00402720`: start of the row swap that turns a picture upside down | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x004029ab` | Shape reader: end of the row swap | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00402d70` | | OpenTPW/VM/RideScript.cs OpenTPW/Global/GameCalendar.cs  |
| `0x00402d90` | | OpenTPW/World/Advisor/Advisor.cs OpenTPW/Global/GameClock.cs  |
| `0x00402db0` | | OpenTPW/Global/GameClock.cs  |
| `0x00402ea0` | | OpenTPW/Global/GameClock.cs  |
| `0x00402f10` | | OpenTPW/VM/RideScript.cs  |
| `0x00403030` | | OpenTPW/Global/GameClock.cs  |
| `0x00403050` | | OpenTPW/Global/GameClock.cs  |
| `0x004030d0` | | OpenTPW/VM/RideScript.cs  |
| `0x004033a0` | | OpenTPW/VM/RideScript.cs  |
| `0x00407f95` | | OpenTPW/World/Level.cs  |
| `0x004092a0` | | OpenTPW/UI/UiWindow.cs OpenTPW/World/Level.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x004092a3` | | OpenTPW/Global/GameClock.cs  |
| `0x004092ad` | | OpenTPW/Global/GameClock.cs  |
| `0x004092c3` | | OpenTPW/Global/GameCalendar.cs  |
| `0x00409303` | | OpenTPW/Global/GameClock.cs  |
| `0x00409353` | | OpenTPW/Global/GameClock.cs  |
| `0x0040c4d0` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x00415270` | the whole-game restore chain: seventeen modules in order, each checked against a four-character tag that follows it | OpenTPW.Files/Formats/Save/ParkScriptStates.cs  |
| `0x00419710` | | OpenTPW/UI/UiFonts.cs  |
| `0x00423690` | | OpenTPW/World/Level.cs OpenTPW/Client/GameOptions.cs  |
| `0x004237f0` | | OpenTPW/UI/Screens/OptionsScreen.cs OpenTPW/Client/SaveFolder.cs OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x00423ad0` | | OpenTPW/UI/Screens/OptionsScreen.cs OpenTPW/Client/GameOptions.cs  |
| `0x00423b00` | | OpenTPW/UI/Screens/OptionsScreen.cs OpenTPW/Client/GameOptions.cs  |
| `0x00423bc0` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00423dd0` | | OpenTPW/Client/GameOptions.cs  |
| `0x00423e90` | | OpenTPW/Client/SaveFolder.cs OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x004242c0` | | OpenTPW.Files/Formats/Save/RecordStream.cs OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x00424820` | | OpenTPW/Client/SaveFolder.cs OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x00424930` | | OpenTPW/Client/SaveFolder.cs OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x00429ba0` | | OpenTPW/World/Advisor/AdvisorModel.cs  |
| `0x00429d60` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0042a190` | | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x0042d130` | | OpenTPW/World/Park/ParkOrbitCameraMode.cs  |
| `0x0044b220` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x0044b2e0` | | OpenTPW/World/Advisor/AdvisorModel.cs  |
| `0x0045aa5a` | | OpenTPW/Client/Players.cs  |
| `0x0045aa74` | | OpenTPW/Client/Players.cs OpenTPW/Client/Game.cs  |
| `0x0045aab4` | | OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x0045acfc` | | OpenTPW/Client/Game.cs  |
| `0x004623b3` | | OpenTPW/World/Lobby/LobbyModel.cs OpenTPW.Tests/LobbyModelAnimationTests.cs  |
| `0x004623df` | | OpenTPW/World/Lobby/LobbyModel.cs OpenTPW.Tests/LobbyModelAnimationTests.cs  |
| `0x00463060` | the build path: checks role 0 exists, triggers it, then queues role 13 to freeze the model on its last frame. Why a newly built thing plays its construction clip and a loaded one does not | OpenTPW/World/Park/ParkRides.cs  |
| `0x004646a1` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004647a0` | the `RSYS` arm of the restore chain: overwrites every animation channel from the saved record and restores the per-node flag words with it, which is what stops a loaded park's things standing frozen | OpenTPW.Files/Formats/Save/ParkThingStates.cs  |
| `0x00467d00` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x00467d60` | | OpenTPW/World/Lobby/LobbyModel.cs  |
| `0x0046b600` | | OpenTPW.Common/Client/Window.cs  |
| `0x00470e90` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004711d0` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471860` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471c73` | | OpenTPW.Tests/AnimationEasingTests.cs OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471c83` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00471d32` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00472f60` | | OpenTPW/World/Lobby/LobbyScript.cs OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x00472fdd` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x004732c2` | | OpenTPW/World/Ride/AnimTimeControl.cs  |
| `0x0047337b` | | OpenTPW/World/Ride/AnimTimeControl.cs OpenTPW.Tests/RideScriptModelTests.cs  |
| `0x004733b1` | | OpenTPW/World/Ride/RideAnimations.cs OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004733cc` | | OpenTPW.Tests/RideScriptModelTests.cs OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004733d6` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004733db` | | OpenTPW/World/Ride/RideAnimations.cs OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x004733e7` | | OpenTPW/World/Ride/RideAnimations.cs  |
| `0x004738a3` | | OpenTPW/World/Ride/AnimTimeControl.cs OpenTPW.Tests/AnimTimeControlTests.cs  |
| `0x00474070` | | OpenTPW/World/Lobby/LobbyScript.cs OpenTPW.Files/Formats/Model/AnimationFile.cs  |
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
| `0x0047f020` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/UiWindow.cs OpenTPW/UI/Screens/MessageBox.cs  |
| `0x0047f251` | | OpenTPW/World/Level.cs  |
| `0x004813c0` | | OpenTPW/UI/WindowStack.cs  |
| `0x00485780` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/ButtonGlint.cs OpenTPW/UI/UiSounds.cs  |
| `0x00485a70` | | OpenTPW/UI/UiFonts.cs  |
| `0x00485d20` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00486bce` | | OpenTPW/Global/GameClock.cs  |
| `0x0048842b` | Park mouse proc: a quick right click installs the idle mode (RMB cancel) | OpenTPW/World/Level.cs  |
| `0x00488434` | Park mouse proc: and ends the build tool, `FUN_0052f200(0,1)` | OpenTPW/World/Level.cs  |
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
| `0x0048c830` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/UI/UiWindow.cs  |
| `0x0048c83a` | | OpenTPW/UI/UiWindow.cs OpenTPW/World/Level.cs OpenTPW/Global/GameClock.cs  |
| `0x0048c868` | | OpenTPW/World/Level.cs OpenTPW/Global/GameClock.cs  |
| `0x0048cd10` | | OpenTPW/Client/GameOptions.cs  |
| `0x0048d8f8` | |  |
| `0x0048f4a6` | | OpenTPW/UI/VirtualScreen.cs  |
| `0x0048f830` | | OpenTPW/UI/UiText.cs  |
| `0x00491ab0` | | OpenTPW/UI/UiText.cs  |
| `0x00492180` | | OpenTPW/UI/HelpBar.cs  |
| `0x00492d80` | | OpenTPW/UI/UiSounds.cs OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00492e80` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x00492f60` | | OpenTPW/UI/Screens/GameMenu.cs  |
| `0x004a0f05` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2387` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2529` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x004a2bf0` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a2e90` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a3480` | | OpenTPW/UI/UiControl.cs OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a3960` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a3a30` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/UiWindow.cs OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a3a4f` | | OpenTPW/World/Level.cs  |
| `0x004a43b0` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a4490` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x004a6000` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs OpenTPW/Client/Locale/UIStrings.cs  |
| `0x004a61b0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs OpenTPW/Client/Players.cs  |
| `0x004a61d0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs OpenTPW/UI/Screens/MessageBox.cs  |
| `0x004a6290` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x004a62b0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x004a6580` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x004a6a50` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x004a6b80` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x004a6d00` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x004a6e40` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x004b8b70` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b8ca0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b8ee0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004b9340` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x004b9840` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x004d49a0` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x004db517` | | OpenTPW/World/Park/ParkRides.cs  |
| `0x004dcf90` | | OpenTPW.Tests/ParkRidesTests.cs  |
| `0x004f7ea9` | | OpenTPW/Global/GameCalendar.cs  |
| `0x004f8321` | | OpenTPW/Global/GameCalendar.cs OpenTPW.Tests/GameCalendarTests.cs  |
| `0x004f8792` | | OpenTPW/Global/GameCalendar.cs  |
| `0x004f87e7` | | OpenTPW/Global/GameCalendar.cs OpenTPW.Tests/GameCalendarTests.cs  |
| `0x004fae10` | | OpenTPW.Files/Formats/Save/RecordStream.cs  |
| `0x0050cd80` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x0050f870` | | OpenTPW/World/Park/FixedVector.cs  |
| `0x00511fc4` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512880` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x0051295c` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512a5e` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00512b4c` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00515865` | | OpenTPW/World/Level.cs OpenTPW/Global/GameCalendar.cs  |
| `0x0051b920` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x0051bcb0` | | OpenTPW/World/Level.cs OpenTPW/Audio/Audio.cs  |
| `0x0051bd70` | | OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/Audio/Audio.cs  |
| `0x0051c2c0` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x0051c300` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x0051e8f0` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x0051ea50` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x0051eae0` | | OpenTPW/UI/UiSounds.cs OpenTPW/World/Park/ParkAudio.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0051ef30` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x0051f320` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051f370` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x0051faa0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051fd20` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051fd80` | | OpenTPW/World/Level.cs  |
| `0x0051fe30` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051feb0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x0051ff10` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520130` | | OpenTPW/World/Lobby/LobbyScript.cs OpenTPW/World/Particles/ParticleSystem.cs OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x00520470` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520560` | | OpenTPW/World/Particles/ParticleSystem.cs OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x00520d60` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520e00` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00520f10` | | OpenTPW/World/Particles/ParticleSystem.cs OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x005214a0` | | OpenTPW/World/Particles/ParticleSystem.cs OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x00521d60` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00521e60` | | OpenTPW/World/Particles/ParticleSystem.cs OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x00522360` | | OpenTPW/World/Particles/ParticleSystem.cs OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x005224f0` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00524a63` | Build commit: a red preview (`DAT_00816d48`) lays nothing, sound `0xaf` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00524acd` | Build commit: end of the red-preview refusal | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00524db7` | Place commit: karts and the water ride lay their first track cells | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00524e49` | Place commit: end of the track-cell seeding | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00525264` | Place commit: `FUN_0052a050` anchors the queue tool on the cell before the entrance | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052526e` | Place commit: dereferences that cell with no null test | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0052529e` | Place commit: `FUN_0052f580(3,0)`, mode 3 keeping the anchor | OpenTPW/World/Park/ParkBuilding.cs OpenTPW/World/Park/ParkBuildMode.cs  |
| `0x005253c4` | Place commit: a thing with no queue goes idle, `FUN_0052f580(0,0)` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005260f5` | Mode `0x14`: `FUN_00530120` anchors on the queue's end | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00526120` | Mode `0x14`: then `FUN_0052f580(3,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00527222` | Mode-3 commit: start of the queue run arm | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005275f2` | Mode-3 commit: the tool ends through `FUN_0052f200(0,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005292b2` | Placer sweep: the exit takes its turned bit as its direction | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005293c3` | Placer sweep: the entrance takes its turned bit as its direction | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529744` | Placer: returns null when the entrance faces off the map | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529757` | Placer: end of that test | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005297e7` | Placer: start of the queued arm (entrance pair and the queue cell before it) | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529808` | Placer: op `0x87` on the cell before the entrance, then the queue stamp | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529885` | Placer: last op on that queue cell | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529890` | Placer: the queue rewalked, `FUN_004de1f0` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005298d5` | Placer: a thing with no queue gets a path before its entrance | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529951` | Placer: start of the four-cardinal relink round an entrance path | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299aa` | Placer: end of that relink | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299cb` | Placer: the exit half, gated on `FUN_0052fab0` | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299e2` | Placer: returns null when the exit faces off the map | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x005299f5` | Placer: end of that test | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529abf` | Placer: start of the relink round the exit path | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x00529b18` | Placer: end of the exit half | OpenTPW/World/Park/ParkBuilding.cs  |
| `0x0053473c` | Stamp: queue over path force-clears the path, `FUN_005367a0(0,0)` | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053475d` | Stamp: end of the force-clear | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00534906` | `FUN_005348d0` path arm: a queue cell becomes path, type only | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x00534913` | Path arm: the type write | OpenTPW/World/Park/ParkPathNeighbours.cs  |
| `0x0053522d` | `FUN_005348d0`: the queue arm, laid type 0 or 3 | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053525a` | Queue arm: first of the four entrance-bond probes | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535323` | Queue arm: last entrance-bond probe | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00535597` | Queue arm: end | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005358e9` | Queue verdict: the cash total skips queue over path | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x005358f1` | Queue verdict: end of that guard | OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x0053c755` | `FUN_0053c3f0`: the marker wave's phase gains 0.1 a frame unless paused | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x0053c773` | `FUN_0053c3f0`: the phase store | OpenTPW/World/Park/ParkBuildMarkers.cs  |
| `0x00540d90` | | OpenTPW.Files/Formats/Sprite/SpriteBankFile.cs  |
| `0x005423a0` | | OpenTPW/UI/ScreenParticles.cs OpenTPW.Files/Formats/Sprite/SpriteBankFile.cs  |
| `0x0054e682` | | OpenTPW/World/Level.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/Global/GameClock.cs  |
| `0x0054e6a6` | | OpenTPW/Client/Game.cs  |
| `0x0054e6df` | | OpenTPW/World/Level.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0054e768` | | OpenTPW/Global/GameClock.cs  |
| `0x0054e770` | | OpenTPW/Global/GameClock.cs  |
| `0x0054e780` | | OpenTPW/Global/GameClock.cs  |
| `0x0054ea4c` | | OpenTPW/World/Level.cs OpenTPW/Global/GameClock.cs  |
| `0x0054ec92` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054ec9a` | | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054ec9f` | | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054ed3f` | | OpenTPW/World/Level.cs  |
| `0x0054ed7c` | | OpenTPW/World/Level.cs OpenTPW/Global/GameClock.cs  |
| `0x0054f455` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f45f` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f47a` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f493` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f49b` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4bf` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f4c4` | | OpenTPW/Global/GameClock.cs  |
| `0x0054f56b` | | OpenTPW/VM/RideScriptScheduler.cs OpenTPW/World/Park/ParkRides.cs  |
| `0x0054f668` | | OpenTPW/World/Level.cs OpenTPW/World/Park/ParkWeather.cs OpenTPW/Global/GameCalendar.cs  |
| `0x0054f680` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f7bb` | | OpenTPW/Global/GameCalendar.cs  |
| `0x0054f870` | | OpenTPW/World/Park/ParkAudio.cs  |
| `0x0054f9f9` | | OpenTPW/World/Level.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005502f6` | | OpenTPW/Client/Players.cs  |
| `0x00551df5` | | OpenTPW.Files/Formats/Script/RideScriptFile.cs  |
| `0x00552950` | | OpenTPW/VM/RideScript.cs  |
| `0x005529bc` | | OpenTPW/VM/RideScript.cs  |
| `0x00552ab0` | | OpenTPW.Tests/RideScriptModelTests.cs  |
| `0x00552fe5` | | OpenTPW/VM/RideScript.cs OpenTPW.Tests/RideScriptChannelTests.cs  |
| `0x00553158` | | OpenTPW/VM/RideScript.cs OpenTPW.Tests/RideScriptChannelTests.cs  |
| `0x005531f9` | | OpenTPW/VM/RideScript.cs  |
| `0x00553435` | | OpenTPW/VM/RideScript.cs OpenTPW.Tests/RideScriptChannelTests.cs  |
| `0x00553470` | | OpenTPW/VM/RideScript.cs OpenTPW.Tests/RideScriptChannelTests.cs  |
| `0x0055374e` | | OpenTPW/VM/RideScript.cs  |
| `0x00554c07` | | OpenTPW/World/Ride/RideState.cs  |
| `0x0055641b` | | OpenTPW/VM/RideScript.cs  |
| `0x0055646d` | | OpenTPW/VM/RideScript.cs  |
| `0x005587f0` | | OpenTPW.Files/Formats/Script/RideScriptFile.cs  |
| `0x00558d2e` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d5b` | | OpenTPW/VM/RideScript.cs  |
| `0x00558d68` | | OpenTPW/VM/RideScript.cs  |
| `0x005597a0` | the `RSSE` arm of the restore chain: reads each script's whole 244-byte struct back from the file, program counter included, so a loaded park's scripts resume mid-flight | OpenTPW.Files/Formats/Save/ParkScriptStates.cs OpenTPW/VM/RideScript.cs OpenTPW/World/Park/ParkRides.cs  |
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
| `0x00587db0` | | OpenTPW/Render/Assets/Asset.cs OpenTPW.Files/Formats/Sprite/SpritePackFile.cs  |
| `0x00588660` | | OpenTPW.Files/Formats/Sprite/SpritePackFile.cs  |
| `0x00591430` | | OpenTPW/UI/ScreenParticles.cs  |
| `0x00598960` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x00598b20` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00598bf0` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x00598ca0` | | OpenTPW.Files/Formats/Model/ModelFile.cs  |
| `0x00599050` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs OpenTPW/Client/GameOptions.cs  |
| `0x005994e0` | | OpenTPW/UI/Park/ParkFrontEnd.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005996d0` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00599880` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x005accf0` | | OpenTPW/Client/Players.cs  |
| `0x005aef50` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af530` | | OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af680` | | OpenTPW/Client/Players.cs OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af740` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af810` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005af940` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afb00` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afc30` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/Client/Players.cs OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afc60` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005afd70` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005aff50` | | OpenTPW.Files/Formats/Save/RecordStream.cs OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005b09c0` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005b0cc0` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005b0d70` | | OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005b11a0` | | OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005b11c0` | | OpenTPW/World/Lobby/LobbyIsland.cs  |
| `0x005b5cc0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005c5250` | | OpenTPW/Client/SaveFolder.cs  |
| `0x005c7590` | | OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs  |
| `0x005c7bb0` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs  |
| `0x005c7de0` | | OpenTPW/Client/SaveFolder.cs  |
| `0x005c7e90` | | OpenTPW/Client/Players.cs  |
| `0x005c7f40` | | OpenTPW/Client/Players.cs OpenTPW/Client/SaveFolder.cs OpenTPW.Files/Formats/Save/PlayerFile.cs  |
| `0x005c8190` | | OpenTPW/Client/SaveFolder.cs  |
| `0x005c83b0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/Client/Players.cs OpenTPW/Client/GameOptions.cs  |
| `0x005c8650` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/Client/Players.cs OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x005c8a10` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/Client/Players.cs  |
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
| `0x005e0470` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x005e1100` | | OpenTPW/World/Lobby/LobbyWeather.cs OpenTPW/World/Weather/Lightning.cs  |
| `0x005e13fb` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1a44` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x005e1bd0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x005e1cc0` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x005e1e30` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x005e1ee0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1f40` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e1fa0` | | OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/World/LobbyCameraMode.cs  |
| `0x005e3210` | | OpenTPW/World/Lobby/LobbyAudio.cs  |
| `0x005e4140` | | OpenTPW/UI/FrontEnd/FrontEnd.cs  |
| `0x005e41c0` | | OpenTPW/UI/WindowStack.cs OpenTPW/UI/FrontEnd/FrontEnd.cs OpenTPW/UI/Park/ParkFrontEnd.cs  |
| `0x005e4207` | | OpenTPW/UI/UiWindow.cs OpenTPW/World/Level.cs OpenTPW/Global/GameClock.cs  |
| `0x005e791b` | | OpenTPW.Files/Formats/Sign/SignFile.cs  |
| `0x005ec3d2` | | OpenTPW.Files/Formats/Sign/SignFile.cs  |
| `0x005ecd09` | | OpenTPW/World/Lobby/SignTexture.cs OpenTPW/World/Park/ParkObjects.cs OpenTPW.Files/Formats/Sign/SignFile.cs  |
| `0x005ecd18` | | OpenTPW/World/Park/ParkObjects.cs OpenTPW.Files/Formats/Sign/SignFile.cs  |
| `0x005ed5a0` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005ed770` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005ed920` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005edac0` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x005f0ad0` | | OpenTPW/UI/Park/ParkMapScreen.cs  |
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
| `0x00668820` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x006698e6` | | OpenTPW/UI/WindowStack.cs  |
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
| `0x0067a830` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006b0680` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b15f0` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b4aa0` | | OpenTPW/UI/BitmapFont.cs  |
| `0x006b8930` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x006e71b4` | | OpenTPW/World/Park/PeepHeading.cs OpenTPW.Tests/PeepHeadingTests.cs  |
| `0x006fe6bc` | | OpenTPW/World/Ride/RideAnimations.cs OpenTPW.Tests/RideAnimationsTests.cs  |
| `0x006febe4` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006febec` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x006fec08` | | OpenTPW/World/Ride/RideAnimations.cs OpenTPW/World/Ride/AnimTimeControl.cs OpenTPW.Tests/RideAnimationsTests.cs  |
| `0x006fecb8` | | OpenTPW.Files/Formats/Model/AnimationFile.cs  |
| `0x007005b8` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007005c0` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007007a4` | | OpenTPW/World/Park/Peep.cs OpenTPW.Tests/ParkBoardingTests.cs  |
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
| `0x00702c7c` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x00702c8c` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00702c94` | | OpenTPW/World/Weather/Lightning.cs  |
| `0x00702ca4` | | OpenTPW/World/LobbyCameraMode.cs  |
| `0x007396c8` | The `Info.Shape` alphabet, 19 rows of `{char, kind, bit}` | OpenTPW.Files/Formats/ItemDescriptionFile.cs  |
| `0x00741c8c` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00741d40` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x00742178` | | OpenTPW/World/Park/ParkWeather.cs  |
| `0x0074cf58` | | OpenTPW.Tests/LobbyModelAnimationTests.cs  |
| `0x0074f920` | | OpenTPW/UI/Screens/MessageBox.cs  |
| `0x0074fa98` | | OpenTPW/UI/Park/ParkGadget.cs OpenTPW/UI/Park/ParkViewfinder.cs  |
| `0x007501a0` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00751720` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752588` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x00752940` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f10` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f24` | | OpenTPW/UI/Park/ParkGadget.cs  |
| `0x00752f30` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x00753c68` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x00753f50` | | OpenTPW/UI/FrontEnd/Screens/NewPlayerDialog.cs  |
| `0x007540c0` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x007540cc` | | OpenTPW/UI/FrontEnd/Screens/PlayerSlots.cs  |
| `0x00757f60` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x0075d0f8` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x0075d178` | | OpenTPW/World/Park/ParkRideScore.cs  |
| `0x007622b0` | | OpenTPW/World/Park/CellLine.cs OpenTPW/World/Park/MapStep.cs  |
| `0x00763b38` | The 20-entry marker texture table (`blue`, `red`, ... `m_link`, `m_end`) | OpenTPW/World/Park/ParkBuildMarkers.cs OpenTPW/World/Park/ParkPathBuilding.cs  |
| `0x00768ab4` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
| `0x00768fb8` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/UI/Park/ParkLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0076dc18` | | OpenTPW/UI/FrontEnd/FrontEndLines.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0076e300` | | OpenTPW/UI/Park/ParkLines.cs  |
| `0x00774ce0` | | OpenTPW/World/Lobby/LobbyScript.cs  |
| `0x00774da0` | | OpenTPW/UI/Park/ParkMapScreen.cs  |
| `0x007854f4` | | OpenTPW/Global/GameCalendar.cs  |
| `0x00785914` | | OpenTPW/World/Advisor/Advisor.cs  |
| `0x00786b84` | | OpenTPW/Global/GameClock.cs  |
| `0x00786b90` | | OpenTPW/Client/SaveFolder.cs  |
| `0x00786ba4` | | OpenTPW/UI/UiWindow.cs OpenTPW/World/Level.cs OpenTPW/World/Advisor/Advisor.cs  |
| `0x0078d8d8` | | OpenTPW/Client/GameOptions.cs OpenTPW.Files/Formats/Save/ConfigFile.cs  |
| `0x0078d90e` | | OpenTPW/UI/ButtonGlint.cs  |
| `0x007cb2fc` | | OpenTPW/UI/Screens/OptionsScreen.cs  |
| `0x007cc4b8` | | OpenTPW/UI/FrontEnd/Screens/IslandPanel.cs  |
| `0x00803a2c` | | OpenTPW/UI/UiSounds.cs  |
| `0x0080ced8` | | OpenTPW.Files/Formats/Particle/ParticleLibraryFile.cs  |
| `0x0080cef8` | | OpenTPW/World/Particles/ParticleSystem.cs  |
| `0x00877d34` | | OpenTPW/World/Park/ParkPeople.cs OpenTPW/Global/GameCalendar.cs OpenTPW/Global/GameClock.cs  |
| `0x00878128` | | OpenTPW/Global/GameClock.cs  |
| `0x008bcbcc` | | OpenTPW/World/Park/ParkGuestSprites.cs  |
