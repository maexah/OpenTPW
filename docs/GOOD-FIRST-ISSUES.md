# Good first issues (drafts)

The text of the GitHub issues the maintainer makes from the queue items tagged **Good first** in `docs/QUEUE.md`.
This page is drafts: nothing here is on GitHub until the maintainer creates the issue (a push-like act), and the
issue's number then goes on the queue item as `Claimed:` when somebody takes it (`docs/WORKFLOW.md`, "Claims"). Delete
a draft here when its issue exists. Label each issue `good first issue`. Every one below needs the .NET 10 SDK and
nothing else: **no game, no Ghidra, no Proton** ([`CONTRIBUTING.md`](../CONTRIBUTING.md), "Quick start"). Line numbers
drift; find the member by name.

How to take one: comment "I'll take this", branch off `main` as `<you>/<what-it-changes>`, and open a pull request
that names the Q-number, with the record `CONTRIBUTING.md` asks for ("Showing how you checked").

---

## Q159: `SdtArchive.GetFile` matches a truncated name the wrong way round

**Where to look.** `source/OpenTPW.Files/Formats/Archive/SDTArchive.cs`, `GetFile( string name )` (the comment above
it explains the fixed 16-byte name field).

**What is wrong.** It tests `x.Name.StartsWith( name )`, the stored name against the one asked for. A `.sdt` name field
is 16 bytes and a longer name is cut to fit. Asking for `"TP SCREECH 11.mp2"`, stored as `"TP SCREECH 11.m"`, never
matches; and `"TP SCREECH 1"` would wrongly find `"TP SCREECH 11.m"`. No caller opens an entry by name yet, so nothing
visible changes today.

**What is expected.** Match an equal name or, for a stored name that fills its 16-byte field, the request's prefix of
that length. A test that builds a small archive in memory with a cut name, an exact name and a near-miss, and shows
the old behaviour fails it (put the bug back and watch the test go red).

**Tools.** `dotnet test`. `ShortReadTests.SdtArchiveTakesEveryByteFromAOneByteStream` builds a small archive in
memory and opens it with `new SdtArchive( Stream )`: copy that. A real `.sdt` is not needed.

## Q160: two park tests do not test what their names say

**Where to look.** `source/OpenTPW.Tests/ParkFootprintOccupancyTests.cs`, `LeavingACellTheThingIsNotOnKeepsWhoeverStandsBehindIt`;
and `source/OpenTPW.Tests/ParkRideJoinTests.cs`, `NoQueueGrowsPastTheRoomItHas`.

**What is wrong.** The first passes whatever `LeaveCell` does: the ride is already unlinked by the first call, the
second leaves `Occupant` at nought, and `ParkPicking.ThingOn` falls back to the cell's parent and answers the ride anyway.
The second takes a queue's room as `QueueSizeInCells` times four, which is nought for the Drinks Shop and the three
toilets, whose room the gate measures in walked cells (`ParkRideChoice.QueueCellsFor`).

**What is expected.** The first asserts the cell's `Occupant`. If that then fails, say so in the pull request and
leave `LeaveCell` as it is (the maintainer decides from the disassembly). The second uses the walked count. For each,
put the bug back and show the test failing.

**Tools.** `dotnet test`. Without the game these park tests may skip; if they do, say so, and the maintainer runs them
with the game.

## Q284: the console's `particles <slot>` reads a word as slot 0

**Where to look.** `source/OpenTPW/Client/Diagnostics/DebugConsole.cs`, `case "particles":`, the `Argument( 1 )` that
becomes `cloudSlot`.

**What is wrong.** `Argument` falls back to 0 for a word that is not a number, so `particles abc` prints slot 0's
cloud without saying that `abc` was not understood.

**What is expected.** A word that is not a number answers `particles: <word> is not a slot number`, as an
out-of-range slot already answers its own line. A test if the console can be driven from one; otherwise say how you
checked.

**Tools.** `dotnet build`; `OPENTPW_DEBUG_CONSOLE=1` and the game only if you want to see it run (not required).

## Q288 (one bullet at a time): names and log lines that say what the code does not

**Where to look.** `docs/QUEUE.md`, item Q288: about a dozen bullets, each a rename or a reworded message with its file
named. Take one and say which in your claim.

**Example, the first bullet.** `ModelFile.Mesh.VertCnt` in `source/OpenTPW.Files/Formats/Model/ModelFile.cs` is
assigned the mesh's *material* count, and nothing reads it. Rename it `MaterialCount`.

**What is expected.** The rename or rewording only, with no change in behaviour: the build has 0 errors and the same
test counts as before. Where a name comes from a file format, the FileFormats page the bullet names tells you the
right word.

**Tools.** `dotnet build` and `dotnet test`.
