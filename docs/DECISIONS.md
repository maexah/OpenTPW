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
`Audio`'s header (`846dd0f`). The stale `bin/Debug/net8.0/` output left behind is noted in `CLAUDE.local.md`. The
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

The test project's `Microsoft.NET.Test.Sdk` 16.11.0 is the only source of the high-severity `Newtonsoft.Json`
9.0.1 advisory, and it ships in nothing but the tests. Alexah was asked on 2026-09-13 and chose to leave it for
now; do not raise it again. Two more advisories stand and were not put to Alexah: `Zio` 0.17.0 (low, every project)
and `SixLabors.ImageSharp` 3.1.6 (moderate and high, in `OpenTPW.ModKit` only, which the game no longer builds in).
