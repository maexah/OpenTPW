# Decisions

Choices that could have gone another way, one paragraph each, dated. Each says what was chosen and why, and
where the facts behind it now live. The code comments named here are the current copy of those facts.

## Leaving Veldrid (2026-09-12)

Veldrid is abandoned upstream (its maintainer stopped publishing in February 2023; `veldrid-spirv`'s last commit
was 2022-06-03), and Rosetta 2 is going away, so Alexah asked for the alternatives before the project went deeper.
The immediate Apple Silicon blocker turned out to be a stale pin, `Veldrid.SPIRV` 1.0.14, not the abandonment:
1.0.15 ships a universal `libveldrid-spirv.dylib`. Four routes were priced - bump the pin and stay, NeoVeldrid,
the ppy/veldrid fork, and SDL3's GPU API - and **Alexah chose NeoVeldrid** ("NeoVeldrid is my choice"), because
.NET 8's support ends 2026-11-10 and the move to .NET 10 was due anyway. One idea from the investigation is still
open: compiling the few shaders in `content/shaders/` offline would take the SPIR-V native out of the shipped game
on every platform at once (`ShaderCompiler` cross-compiles them at every launch, even on Vulkan, only for the
reflection names `Material` needs). The report, "One Version Behind", is
https://claude.ai/code/artifact/995b85f9-1b72-4549-b1e7-4b4b2b5912fd (Alexah's; its raw evidence is on Alexah's
machine, the path in `CLAUDE.local.md`).

## NeoVeldrid 1.2.1 on .NET 10 (2026-09-12 to 2026-09-13)

The migration is `4f6da17`..`846dd0f` and its audit `e2aaab1`..`3485ec3`, on `alexah/35-neoveldrid`. Every
project builds on `net10.0` against NeoVeldrid 1.2.1 and Silk.NET, and the game no longer references the ModKit
editor (`b2aca43`). The facts it rests on live in the code: how Silk.NET finds its natives, and the host-supplied
`NATIVE_DLL_SEARCH_DIRECTORIES` the game prepends to it, in `NativeLibraries`' summary (`e2aaab1`); the shader
uniform-block naming in `ShaderCompiler`; and why the audio device is opened on the SDL the window came from in
`Audio`'s header (`846dd0f`). The
migration plan, "Leaving Veldrid" (Alexah's, drawn 2026-09-12), is
https://claude.ai/code/artifact/74a59520-a16a-4703-84df-0ad69f15a58c: read it for its API verification table and the
audio-trap proof; its eight-commit ladder is superseded by the four the migration landed as.

## The post-migration audit (2026-09-13)

Seven items, all settled. The native search directories come from the host (above). On Wayland the game ships its
bundled SDL, and `OPENTPW_SYSTEM_SDL=1` stands it aside for the machine's own, for SDL alone (`cfa7e5f`). Startup
was profiled from 8.08 s to 6.73 s, and to 5.61 s once one blank texture was handed out rather than two thousand
(`bb897f8`); `TryGetCachedTexture` was deliberately left as it was. Two method traps it found are
`docs/VERIFYING.md` rules 122 and 123.

## Package advisories (2026-09-13)

The test project's `Microsoft.NET.Test.Sdk` 16.11.0 was the only source of the high-severity `Newtonsoft.Json`
9.0.1 advisory, shipped in nothing but the tests. Alexah chose on 2026-09-13 to leave it for then, and on 2026-10-29
asked for every build warning to be fixed at its cause: 17.14.1 takes a patched `Newtonsoft.Json`. `SixLabors.ImageSharp` 3.1.6 (moderate and high) went with `OpenTPW.ModKit`, the upstream
editor nothing built in, which Alexah had removed on 2026-10-29. `Zio` 0.17.0 (low) is gone: Q753 dropped it from
`OpenTPW.Common.csproj`, nothing referenced it and it shipped in every weekly zip.

## A press goes to what the pointer is over (2026-09-25)

The original works its hover out only on a move and when its interface changes (`FUN_006589f9`), so a press made
just after a window opens, before the pointer moves, can go to the old hover and miss the last button that window
built (`docs/exe/lobby.md`, the Unsettled list under "The lobby's keys act on the release"). OpenTPW hit-tests every
frame, so a press always goes to what the pointer is
over. Copying the original would mean decoding each window's build order to reproduce what is almost certainly a
glitch, so **Alexah chose to keep ours as a fix** ("Option 1", after the pros and cons were put to them, Q69). The
deviation is said at `WindowStack.OnUpdate`.

## F2 stays beside F3 (2026-10-05)

The original's full-screen view is F3 (`docs/exe/park-engine.md`, "The full-screen view: F3"), and F2 is bound to
nothing in it. OpenTPW's F2 hides the whole interface, pointer included, in the lobby and in a park, and it is how a
clean frame is taken. When F3 was built (Q114b) the choice was put to Alexah with what F2 was measured to do: keep it
as it is, keep it in the lobby only, or remove it. **Alexah chose to keep F2 as it is.** It is said at
`InputButton.HideUI`.

## A hired costume is brought within the banks loaded (2026-10-06)

The original makes a hired person's sprite on the candidate's costume as saved, and draws from one flat table of
loaded sprite banks in which a bank past its kind's count is the next kind's (`docs/exe/park-engine.md`, "What a hire
makes of the candidate's costume"). On low detail, where one mechanic bank is loaded, a mechanic hired from Lost
Kingdom's saved pool with the second costume is therefore drawn as a guard, in the park and in the hire screen's
preview, until a save and load reduces the costume. Measured in the original under Proton: thing 43, sprite kind 6
bank 1, the guard's figure. The choice was put to Alexah with that picture: draw the guard as the original does, or
bring the costume within the banks loaded at the hire, as the original's own load of a saved person does. **Alexah
chose the mechanic.** It is said at `ParkPeople.StaffPicture`.

## The reference executable is 2.0 (2026-09-27)

Alexah asked for the reverse-engineering base to move from `testme.exe`, thought to be 1.0, to a decrypted 2.0 exe
(`tp-2_0.exe`), the game's last release. The move turned out to be unnecessary: **`testme.exe` already is 2.0 and stays
the reference**, and Alexah chose to record that rather than migrate ("Record it", 2026-09-27). It draws "v 2.0" in the
lobby and "Ver 2.0" on ALT-V, the two things the 2.0 patch's readme promises (`docs/exe/boot.md`, "Which build this
is"). The official patch, `TPPatchTwoEUROAMER20000324a.exe` (InstallShield around RTPatch 5.00 deltas), was applied
with its own engine to a copy of the disc install `~/Games/TPWorld` was copied from: it replaced only the
SafeDisc-wrapped exe and its loader, `clokspl.exe` and `readme.txt`, and added `UNINST.dll` and two American chat word
lists the disc already holds in other folders. Every data file it touches, jungle's `coaster1.wad` and `minecart.wad`
included, was already the 2.0 version, a content check that one-byte negative controls proved. The disc's `TP.ICD` has the same PE link time as the patch's (2000-03-24 15:14:32
UTC), and their encrypted sections are consistent with one plaintext under two keys, so the disc is a later pressing
with 2.0 built in. `tp-2_0.exe` is the same program, 493 of its 3,734,528 bytes different, and it is the worse copy: a
no-CD crack at `0x005aa5ad` (the disc check, `boot.md`), `USP10.dll` renamed `USP11.dll` in its imports, two section
sizes enlarged over padding, and entries 5 to 7 of a movie block decoder's jump table (`FUN_00672e60`, at
`0x00fbbac0`; the decoder of codec type 3's `pQGT`/`MUVf` frames, which no shipped movie carries) holding garbage
where `testme.exe` holds code addresses. Hashes (sha256): `testme.exe`
`cf0ffd955077eca146d75ee46c45b8a0786fb757a8f7d204b1aed8ec5a1ee4cb`, the file Ghidra's `/testme.exe` records as its
source; `tp-2_0.exe` `d0399acfc1bcbdcbe5836814b19e386486bfa06f057646ae7821bc25e9927ac9`. The Gold disc image holds 35
bonus attraction wads and no exe. The evidence (the extracted patch, the apply logs and controls, three adversarial
reviews) is on Alexah's machine, the path in `CLAUDE.local.md`.

## Rules for everyone in `AGENTS.md`, the harness in the repo (2026-10-27)

Alexah asked for the repo to be set up so others can contribute, with their own agents or without. Chosen, one
question at a time: the rules every contributor works by moved to `AGENTS.md`, which any coding agent reads, and
`CLAUDE.md` imports it, so one copy serves Claude Code and the rest. The rules keep their numbers, because other
pages cite them; the maintainer's own working rules (15, 17, 19, 20, and rule 1's push survey) went to their
uncommitted `CLAUDE.local.md` rather than to a maintainer section, so nothing committed binds a contributor to
another person's desk. Outside work reaches `main` by a pull request the maintainer reviews and fast-forwards, still
with no CI. The shared harness (`lib/`, `original/`, and the C# readers) moved from the maintainer's cache into
`tools/harness/`, every folder it names now read from an environment variable (`paths.py`); the per-item evidence
folders stayed out, and so did anything made from the game's files.

## Textures loaded by path stay for the life of the process (2026-10-30)

A texture made from a path is kept in `Asset.All` and shared by every later request for the path (`Texture.TryAdoptCached`), and nothing releases it when a level ends. Q832 measured the
trade on the stock Lost Kingdom park: after a park the lobby holds 337 more textures and 13.5 MB more on the card (the park's 157 `levels/jungle` textures, its interface's 163 and 14
`generic/*`), and a park entered again loads in 3.1 s against 5.0 s the first time (the terrain phase 780 -> 66 ms). Letting go at a level's end would give the 13.5 MB back in the lobby and
cost about 1.75 s on each park entered again (a probe that refused cache hits across a level's end, which released nothing: 4.83 s). The lobby's own 234 textures stay alive in the park (the 135 lobby/terrain
ones 6.9 MB with mips by their file headers, up to 8.6 MB with the interface's and the advisor's): that is video memory, which the desk's VmHWM (485 MB, CPU memory) does not count, so the peak the desk measured says nothing
for it, and what a release at the lobby's end would save on a smaller computer is not measured (a prediction of 6.9 to 8.6 MB of the park's 108.8 MB texture account is filed to be tried). Eleven statics hold textures that would have to be cleared with them (`UiMesh.Loaded` and the other interface caches). So they stay. Whoever reopens this should
begin with the re-measure of a release at the lobby's end (a probe build that deletes the path textures at `Level.Unload`; `roundtrip.py` with `TRIPS=1 OFFSCREEN=1`, park1's VmHWM, `mem gpu` and `mem vulkan` against the unchanged binary's 108.8 and 204.5 MB), and with Q833, which finds what else a round trip leaves. The figures and runs are Q832's
entry in `docs/history/queue-done.md`.
