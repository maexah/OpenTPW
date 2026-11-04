using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The docs' path checker (<c>tools/check_docs_paths.py</c>, Q800, Q877): every path, type and link that a page names
/// must exist, and one that does not is printed with its page and line. It is run over a scratch repository that holds
/// a copy of the script and a few files, so the pages' real names can change without these tests moving.
/// </summary>
[TestClass]
public class CheckDocsPathsTests
{
	private const string NoFile = "no such file or folder";
	private const string NoType = "no such type (or member)";
	private const string NoLinkFile = "link to a file that is not in the repository";
	private const string NoLinkHeading = "link to a heading that is not there";

	/// <summary>The fixture's files besides the pages; <c>Bad.cs</c> holds a byte that is not UTF-8, ahead of a class.</summary>
	private static readonly (string Name, byte[] Bytes)[] Files =
	[
		("source/OpenTPW/Client/GameDir.cs", "namespace OpenTPW;\npublic class GameDir { public void Find() { } }\npublic struct ParkState { public int CellAt( int x, int y ) => 0; public void Reset() { } }\npublic enum Mode { Wet, Dry }\npublic interface IThing { void Poke(); }\npublic record Sample( int Value );\n"u8.ToArray()),
		("source/OpenTPW/Foo.cs", "class Foo { void Go() { Run( Widget ); } }\n"u8.ToArray()),
		("source/OpenTPW/Bad.cs", [.. new byte[] { 0xFF, 0xFE, 0x0A }, .. "class AfterBad { }\n"u8.ToArray()]),
		("source/OpenTPW/App.config", "<configuration><add key=\"OnlyInConfig\" /></configuration>\n"u8.ToArray()),
		("source/OpenTPW/OpenTPW.csproj", "<Project><Description>OnlyInProj</Description></Project>\n"u8.ToArray()),
		("source/OpenTPW/notes.txt", "OnlyInNotes\n"u8.ToArray()),
		("source/OpenTPW.Files/Formats.cs", "class Formats { }\n"u8.ToArray()),
		("source/OpenTPW.sln", "x"u8.ToArray()),
		("tools/run.sh", "x"u8.ToArray()),
		("tools/Outside.cs", "class Outside { }\n"u8.ToArray()),
		("docs/other.md", "x"u8.ToArray()),
		("docs/guide.md", "# Where things are\n## `Foo` Bar!\n### A \u2014 b (Q&A)\n#### Snake case\n###### Six\n####### Seven\n#NoSpace\n##### Re-use it\n## Trailing \n## snake_name\n## Same\n## Same\n## Same\n```sh\n# InTick\n```\n~~~\n```\n# InTilde\n~~~\n````md\n```\n# InFour\n```\n````\n## After fences\n## Closed ##\n## C# ##\n## Not# closed\n## Sharp # mid\nText # Fake\n"u8.ToArray()),
		(".gitignore", "ignored/\n"u8.ToArray()),
		("ignored/hidden.cs", "class Hidden { }\n"u8.ToArray()),
	];

	/// <summary>The checker's exit code and printed lines over the fixture and these pages (written over the fixture's), run from the scratch repository; the end of the test where there is no Python or git.</summary>
	private static (int Exit, string[] Lines) Check( string[] arguments, bool stage, params (string Name, string[] Lines)[] pages )
	{
		var root = RepoFiles.Root();
		var folder = Path.Join( Path.GetTempPath(), "opentpw-docspaths-" + Guid.NewGuid().ToString( "N" ) );

		if ( RepoFiles.Python( ["--version"] ) is not { Exit: 0 } )
			Assert.Inconclusive( "skipped: no Python here" );

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "tools" ) );
			File.Copy( Path.Join( root, "tools", "check_docs_paths.py" ), Path.Join( folder, "tools", "check_docs_paths.py" ) );

			if ( RepoFiles.Run( "git", new[] { "init", "-q" }, null, folder ) is not { Exit: 0 } )
				Assert.Inconclusive( "skipped: no git here" );

			foreach ( var (name, bytes) in Files )
			{
				Directory.CreateDirectory( Path.GetDirectoryName( Path.Join( folder, name ) )! );
				File.WriteAllBytes( Path.Join( folder, name ), bytes );
			}

			foreach ( var (name, lines) in pages )
				File.WriteAllText( Path.Join( folder, name ), string.Join( "\n", lines ) + "\n" );

			if ( stage )
				Assert.AreEqual( 0, RepoFiles.Run( "git", new[] { "add", "-A" }, null, folder )!.Value.Exit );

			var result = RepoFiles.Python( new[] { "tools/check_docs_paths.py" }.Concat( arguments ).ToArray(), null, folder );
			var output = result!.Value.Output.Split( '\n', StringSplitOptions.RemoveEmptyEntries ).Select( line => line.TrimEnd( '\r' ) ).ToArray();

			return (result.Value.Exit, output);
		}
		finally
		{
			RepoFiles.DeleteTree( folder );
		}
	}

	private static (int Exit, string[] Lines) Check( string[] arguments, params (string Name, string[] Lines)[] pages )
		=> Check( arguments, false, pages );

	private static string Problem( string page, int line, string name, string why )
		=> $"  {page}:{line}: `{name}`: {why}";

	private static string Header( int pages, int notFound )
		=> $"check_docs_paths: {pages} page(s), {notFound} name(s) not found";

	/// <remarks><b>Mutation:</b> <c>path_exists</c> matching a tail without the slash (<c>Dir.cs</c> for <c>GameDir.cs</c>, <c>penTPW/Client</c> for <c>OpenTPW/Client</c>), a prefix without the folder's end (<c>Clien</c>), or ignoring the trailing slash; <c>tracked()</c> without <c>--exclude-standard</c> finds the ignored file.</remarks>
	[TestMethod]
	public void APathMustMatchWholeOrAsTheTailOfAFileOrFolder()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"`tools/run.sh`",
			"`tools/gone.sh`",
			"`Client/GameDir.cs`",
			"`Dir.cs`",
			"`source/OpenTPW/Client/`",
			"`source/OpenTPW/Clien`",
			"`OpenTPW/Client`",
			"`penTPW/Client`",
			"`docs/`",
			"`docs/nothere/`",
			"`ignored/hidden.cs`",
			"`tools/Outside.cs`",
			"`.github/gone.yml`",
			"`Gone.md` `Gone.Two.cs`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 9 ),
			Problem( "README.md", 2, "tools/gone.sh", NoFile ),
			Problem( "README.md", 4, "Dir.cs", NoFile ),
			Problem( "README.md", 6, "source/OpenTPW/Clien", NoFile ),
			Problem( "README.md", 8, "penTPW/Client", NoFile ),
			Problem( "README.md", 10, "docs/nothere/", NoFile ),
			Problem( "README.md", 11, "ignored/hidden.cs", NoFile ),
			Problem( "README.md", 13, ".github/gone.yml", NoFile ),
			Problem( "README.md", 14, "Gone.md", NoFile ),
			Problem( "README.md", 14, "Gone.Two.cs", NoFile ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> <c>--cached</c> dropped from <c>tracked()</c> fails here (a staged file is not untracked); <c>--others</c> dropped fails every test but this one.</remarks>
	[TestMethod]
	public void AStagedFileCountsAsMuchAsAnUntrackedOne()
	{
		var (exit, lines) = Check( ["README.md"], true, ("README.md", ["`tools/run.sh`", "`Client/GameDir.cs`", "`ignored/hidden.cs`"]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[] { Header( 1, 1 ), Problem( "README.md", 3, "ignored/hidden.cs", NoFile ) }, lines );
	}

	/// <remarks><b>Mutation:</b> any one of the skips in <c>check()</c> or <c>NOT_OURS</c>, <c>GAME_FOLDERS</c> and <c>NOT_TRACKED_ON_PURPOSE</c> removed makes one of these a problem.</remarks>
	[TestMethod]
	public void NamesThatAreNotOursOrNotPathsAreNotChecked()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"`OpenTPW` `OpenTPW.dll` `console.log` `Proton` `Vulkan` `Xvfb` `MSTest` `Escape`",
			"`.gitattributes` `.md` `.cs` `.json`",
			"`data/x.cs` `Save/x.md` `save/x.md` `Data/x.cs` `content/ReferenceScreenshots/` `content/ReferenceScreenshots/a.md` `maexah/OpenTPW`",
			"`<kind>/gone.cs` `tools/*.gone` `tools/$X.cs` `a=b/gone.cs`",
			"`/abs/gone.cs` `~/gone.cs` `%APPDATA%/gone.cs` `-x/gone.cs` `http://x/gone.cs` `C:/gone.cs`",
			"`OpenTPW.Files` `PARK` `TPWI` `ParkState.cellat` `lower` `A`",
		]) );

		Assert.AreEqual( 0, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[] { Header( 1, 0 ) }, lines );
	}

	/// <remarks><b>Mutation:</b> the <c>OpenTPW.*</c> project-folder skip removed; or its folder test dropped so that any <c>OpenTPW.Word</c> passes.</remarks>
	[TestMethod]
	public void AProjectFolderNameIsCheckedAgainstTheSourceFolder()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md", ["`OpenTPW.Files`", "`OpenTPW.Nope`"]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[] { Header( 1, 1 ), Problem( "README.md", 2, "OpenTPW.Nope", NoType ) }, lines );
	}

	/// <remarks><b>Mutation:</b> <c>type_exists</c> without the word boundary (<c>ParkStat</c>, <c>ParkState.Cell</c>), without the declaration test of the head (<c>Ghost.CellAt</c>), with a keyword missing from the declaration list (one row of each kind: class, struct, enum, interface, record), or matching a declaration's prefix (<c>Game.Find</c> for <c>GameDir</c>); the single-name path dropping <c>\b</c> or the lower-case test; the call form not cut at its bracket, or its spaces sending it to the word check before it is cut (`ParkState.Gone( 1 )`); the source-file suffixes of <c>Index</c> losing <c>.config</c> or <c>.csproj</c>, or gaining <c>.txt</c>, or no longer limited to <c>source/</c>.</remarks>
	[TestMethod]
	[DataRow( "ParkState", true )]
	[DataRow( "ParkStat", false )]
	[DataRow( "Widget", true )]
	[DataRow( "Gone", false )]
	[DataRow( "AfterBad", true )]
	[DataRow( "Outside", false )]
	[DataRow( "Hidden", false )]
	[DataRow( "GameDir.Find", true )]
	[DataRow( "ParkState.CellAt", true )]
	[DataRow( "Mode.Wet", true )]
	[DataRow( "IThing.Poke", true )]
	[DataRow( "Sample.Value", true )]
	[DataRow( "ParkState.Cell", false )]
	[DataRow( "ParkState.Gone", false )]
	[DataRow( "Ghost.CellAt", false )]
	[DataRow( "Game.Find", false )]
	[DataRow( "ParkState.CellAt(1,2)", true )]
	[DataRow( "ParkState.Gone(1,2)", false )]
	[DataRow( "Ghost.CellAt(1)", false )]
	[DataRow( "ParkState.CellAt( 1, 2 )", true )]
	[DataRow( "ParkState.CellAt()", true )]
	[DataRow( "ParkState.Gone( 1 )", false )]
	[DataRow( "Ghost.CellAt( 1 )", false )]
	[DataRow( "OnlyInConfig", true )]
	[DataRow( "OnlyInProj", true )]
	[DataRow( "OnlyInNotes", false )]
	public void ATypeMustBeDeclaredAndItsMemberUsedInTheSource( string name, bool found )
	{
		var (exit, lines) = Check( ["README.md"], ("README.md", [$"`{name}`"]) );

		var shown = name.Contains( '(' ) ? name[..name.IndexOf( '(' )].TrimEnd() : name;
		Assert.AreEqual( found ? 0 : 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( found ? new[] { Header( 1, 0 ) } : new[] { Header( 1, 1 ), Problem( "README.md", 1, shown, NoType ) }, lines );
	}

	/// <remarks><b>Mutation:</b> the <c>" " in name</c> branch dropped (a command's missing path passes); a command's words not split on quotes, brackets or <c>=</c>.</remarks>
	[TestMethod]
	public void ABackticksWithSpacesAreACommandWhoseWordsAreChecked()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"`python3 tools/run.sh` and `cd tools` and `the ParkState type`",
			"`python3 tools/gone.py` then `git add tools/gone.cs`",
			"`run (tools/gone2.sh)` `x --out=tools/gone3.sh`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 4 ),
			Problem( "README.md", 2, "tools/gone.py", NoFile ),
			Problem( "README.md", 2, "tools/gone.cs", NoFile ),
			Problem( "README.md", 3, "tools/gone2.sh", NoFile ),
			Problem( "README.md", 3, "tools/gone3.sh", NoFile ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> the fence never closing (<c>in_fence</c> not toggled back), an indented fence not seen, a word's trailing colon kept, a fenced line parsed for backticks or types; a skip of the word list (<c>./</c>, <c>../</c>, <c>$</c>, <c>{</c>, <c>*</c>, <c>//</c>, <c>~</c>, a drive, a web address, a game folder) removed.</remarks>
	[TestMethod]
	public void AFencedLineHasItsPathLikeWordsCheckedAndThePageAfterTheFenceIsParsedAgain()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"text",
			"```sh",
			"dotnet build source/OpenTPW.sln",
			"python3 tools/run.sh --out=tools/gone.sh",
			"cp \"tools/gone2.py\" x",
			"echo tools/gone3.sh: done",
			"ls tools/nothere/",
			"Ghost.Nope `Ghost.Nope` ParkStat `tools/gone8.sh`",
			"cp 'tools/gone9.py' a,tools/gone10.sh;tools/gone11.sh f(tools/gone12.sh)",
			"./tools/gone.sh ../x.cs $HOME/x.cs ~/x.cs /x.cs a//b.cs C:/x.cs data/x.wad {a}/x.cs <k>/x.cs *.cs http://h/x.cs tools/file.bin Gone.cs build.sh %APPDATA%/x.cs -x/y.cs http-docs/x.cs tools/*.cs data/x.cs save/x.md Save/x.md Data/x.cs content/ReferenceScreenshots/ maexah/",
			"```",
			"`tools/gone4.sh` `ParkStat`",
			"   ```",
			"tools/gone5.sh",
			"   ```",
			"`tools/gone6.sh`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 13 ),
			Problem( "README.md", 4, "tools/gone.sh", NoFile ),
			Problem( "README.md", 5, "tools/gone2.py", NoFile ),
			Problem( "README.md", 6, "tools/gone3.sh", NoFile ),
			Problem( "README.md", 7, "tools/nothere/", NoFile ),
			Problem( "README.md", 8, "tools/gone8.sh", NoFile ),
			Problem( "README.md", 9, "tools/gone9.py", NoFile ),
			Problem( "README.md", 9, "tools/gone10.sh", NoFile ),
			Problem( "README.md", 9, "tools/gone11.sh", NoFile ),
			Problem( "README.md", 9, "tools/gone12.sh", NoFile ),
			Problem( "README.md", 12, "tools/gone4.sh", NoFile ),
			Problem( "README.md", 12, "ParkStat", NoType ),
			Problem( "README.md", 14, "tools/gone5.sh", NoFile ),
			Problem( "README.md", 16, "tools/gone6.sh", NoFile ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> the link target's file not required to exist, a folder link refused, an anchor checked on a file that is not markdown, an anchor not checked, a web or mail link checked, only the first link of a line read, the link pass skipped for a line that also has backticks.</remarks>
	[TestMethod]
	public void ALinkMustReachAFileOrFolderOfTheRepositoryAndAHeadingOfIt()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"# Top of README",
			"[ok](docs/guide.md) [bad](docs/missing.md)",
			"[ok](docs/guide.md#where-things-are)",
			"[bad](docs/guide.md#nope)",
			"[ok](docs/) [ok](tools/run.sh#L5)",
			"[web](https://x/y.md#z) [mail](mailto:a@b.c) [http](http://x/gone.md)",
			"[self](#top-of-readme) [bad](#nowhere)",
			"[x](docs/missing3.md) and `tools/gone7.sh`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 5 ),
			Problem( "README.md", 2, "docs/missing.md", NoLinkFile ),
			Problem( "README.md", 4, "docs/guide.md#nope", NoLinkHeading ),
			Problem( "README.md", 7, "#nowhere", NoLinkHeading ),
			Problem( "README.md", 8, "docs/missing3.md", NoLinkFile ),
			Problem( "README.md", 8, "tools/gone7.sh", NoFile ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> <c>slug()</c> collapsing spaces (<c>a-b-qa</c>), keeping backticks, punctuation or <c>&amp;</c>, not lower-casing, dropping <c>_</c>; the heading pattern taking seven hashes or no space after them or missing the sixth level; a repeated heading not numbered (<c>-1</c>, <c>-2</c>) or numbered from 0; a heading inside a backtick, tilde or longer fence counted; a closing run of hashes (<c>## Closed ##</c>) kept, or a run of hashes inside the heading cut (<c>Sharp # mid</c>); a heading found after text on its line.</remarks>
	[TestMethod]
	[DataRow( "where-things-are", true )]
	[DataRow( "foo-bar", true )]
	[DataRow( "a--b-qa", true )]
	[DataRow( "a-b-qa", false )]
	[DataRow( "snake-case", true )]
	[DataRow( "re-use-it", true )]
	[DataRow( "reuse-it", false )]
	[DataRow( "trailing", true )]
	[DataRow( "trailing-", false )]
	[DataRow( "six", true )]
	[DataRow( "seven", false )]
	[DataRow( "nospace", false )]
	[DataRow( "Where-things-are", false )]
	[DataRow( "snake_name", true )]
	[DataRow( "snakename", false )]
	[DataRow( "same", true )]
	[DataRow( "same-1", true )]
	[DataRow( "same-2", true )]
	[DataRow( "same-3", false )]
	[DataRow( "intick", false )]
	[DataRow( "intilde", false )]
	[DataRow( "infour", false )]
	[DataRow( "after-fences", true )]
	[DataRow( "closed", true )]
	[DataRow( "closed-", false )]
	[DataRow( "c", true )]
	[DataRow( "not-closed", true )]
	[DataRow( "sharp--mid", true )]
	[DataRow( "fake", false )]
	public void AnAnchorIsGitHubsSlugOfAHeadingOfTheLinkedPage( string anchor, bool found )
	{
		var (exit, lines) = Check( ["README.md"], ("README.md", [$"[x](docs/guide.md#{anchor})"]) );

		Assert.AreEqual( found ? 0 : 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( found ? new[] { Header( 1, 0 ) } : new[] { Header( 1, 1 ), Problem( "README.md", 1, "docs/guide.md#" + anchor, NoLinkHeading ) }, lines );
	}

	/// <remarks><b>Mutation:</b> a fence closed by the other mark (<c>~~~</c> by backticks and the reverse), by a shorter run, or by a line with text after it; <c>~~~</c> not opening a fence, or two marks opening one; a backtick fence opened by a line whose info string holds a backtick (<c>```x```</c> is inline code); a fence's own line read as prose (a tilde fence's info string may hold backticks).</remarks>
	[TestMethod]
	public void AFenceIsClosedByTheSameMarkAtLeastAsLongAndNothingElse()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"~~~sh `tools/gone0.sh`",
			"ls tools/gone1.sh",
			"```",
			"ls tools/gone2.sh",
			"~~~",
			"`tools/gone3.sh`",
			"````md",
			"```",
			"ls tools/gone4.sh",
			"```",
			"ls tools/gone5.sh",
			"````",
			"`ParkStat`",
			"```ParkStatB```",
			"`ParkStatC`",
			"```",
			"``` text",
			"ls tools/gone6.sh",
			"```",
			"`tools/gone7.sh`",
			"``",
			"`ParkStatD`",
			"~~",
			"`ParkStatE`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 12 ),
			Problem( "README.md", 2, "tools/gone1.sh", NoFile ),
			Problem( "README.md", 4, "tools/gone2.sh", NoFile ),
			Problem( "README.md", 6, "tools/gone3.sh", NoFile ),
			Problem( "README.md", 9, "tools/gone4.sh", NoFile ),
			Problem( "README.md", 11, "tools/gone5.sh", NoFile ),
			Problem( "README.md", 13, "ParkStat", NoType ),
			Problem( "README.md", 14, "ParkStatB", NoType ),
			Problem( "README.md", 15, "ParkStatC", NoType ),
			Problem( "README.md", 18, "tools/gone6.sh", NoFile ),
			Problem( "README.md", 20, "tools/gone7.sh", NoFile ),
			Problem( "README.md", 22, "ParkStatD", NoType ),
			Problem( "README.md", 24, "ParkStatE", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> a link's title (<c>"t"</c>, <c>'t'</c> or <c>(t)</c>, one holding a bracket) or a space before its closing bracket or after its opening one making the link unread.</remarks>
	[TestMethod]
	public void ALinkWithATitleOrASpaceBeforeItsBracketIsReadUpToItsTarget()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"[ok](docs/guide.md \"The guide\") [ok](docs/guide.md 't') [ok](docs/guide.md (t)) [ok](docs/guide.md  )",
			"[bad](docs/missing.md \"t\") [bad](docs/missing2.md 't') [bad](docs/missing3.md (t)) [bad](docs/missing4.md ) [bad]( docs/missing6.md)",
			"[ok](docs/guide.md#six \"t\") [bad](docs/guide.md#nope \"t\") [bad](docs/missing5.md \"a) b\")",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 7 ),
			Problem( "README.md", 2, "docs/missing.md", NoLinkFile ),
			Problem( "README.md", 2, "docs/missing2.md", NoLinkFile ),
			Problem( "README.md", 2, "docs/missing3.md", NoLinkFile ),
			Problem( "README.md", 2, "docs/missing4.md", NoLinkFile ),
			Problem( "README.md", 2, "docs/missing6.md", NoLinkFile ),
			Problem( "README.md", 3, "docs/guide.md#nope", NoLinkHeading ),
			Problem( "README.md", 3, "docs/missing5.md", NoLinkFile ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> any one file type of <c>PATH_TYPES</c> removed (a missing <c>Gone.toml</c> then passes, in backticks and in a fence); the type of a fenced word cut at its first dot (<c>a.b.sh</c>), or taken from a file with no dot (<c>tools/md</c> is a file with no type); a name padded inside its backticks not stripped.</remarks>
	[TestMethod]
	[DataRow( "md", true )]
	[DataRow( "py", true )]
	[DataRow( "cs", true )]
	[DataRow( "sh", true )]
	[DataRow( "yml", true )]
	[DataRow( "yaml", true )]
	[DataRow( "csproj", true )]
	[DataRow( "sln", true )]
	[DataRow( "json", true )]
	[DataRow( "txt", true )]
	[DataRow( "shader", true )]
	[DataRow( "props", true )]
	[DataRow( "config", true )]
	[DataRow( "toml", true )]
	[DataRow( "wad", false )]
	[DataRow( "bin", false )]
	public void EveryFileTypeOfTheListMakesAMissingNameAPath( string type, bool listed )
	{
		var (exit, lines) = Check( ["README.md"], ("README.md", [$"`Gone.{type}`", "```", $"ls tools/gone.{type}", "```"]) );

		Assert.AreEqual( listed ? 1 : 0, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( listed
			? new[] { Header( 1, 2 ), Problem( "README.md", 1, $"Gone.{type}", NoFile ), Problem( "README.md", 3, $"tools/gone.{type}", NoFile ) }
			: new[] { Header( 1, 0 ) }, lines );
	}

	/// <remarks><b>Mutation:</b> see <see cref="EveryFileTypeOfTheListMakesAMissingNameAPath"/>.</remarks>
	[TestMethod]
	public void AFencedWordsTypeIsAfterItsLastDotAndAFileWithNoDotHasNone()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"```",
			"ls tools/a.b.sh",
			"ls tools/x.d/md tools/md",
			"ls tools/x.d/gone.sh",
			"```",
			"`  ParkStat ` `  ParkState `",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 3 ),
			Problem( "README.md", 2, "tools/a.b.sh", NoFile ),
			Problem( "README.md", 4, "tools/x.d/gone.sh", NoFile ),
			Problem( "README.md", 6, "ParkStat", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> a link read from the repository's root and not the page's own folder; <c>..</c> not folded.</remarks>
	[TestMethod]
	public void ALinkIsReadFromTheFolderOfItsPage()
	{
		var (exit, lines) = Check( ["docs/other.md"], ("docs/other.md",
		[
			"[ok](guide.md) [bad](docs/guide.md)",
			"[ok](guide.md#six) [ok](../tools/run.sh) [bad](../tools/gone.sh)",
			"[ok](#own)",
			"# Own",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 2 ),
			Problem( "docs/other.md", 1, "docs/guide.md", NoLinkFile ),
			Problem( "docs/other.md", 2, "../tools/gone.sh", NoLinkFile ),
		}, lines );
	}

	/// <remarks><b>Mutation:</b> <c>PAGES</c> losing one of the three; the arguments ignored; the count of pages or of names wrong; the exit code always 0 or always 1.</remarks>
	[TestMethod]
	public void WithNoArgumentsTheThreePagesAreCheckedAndNamedPagesAloneOtherwise()
	{
		(string, string[])[] pages =
		[
			("README.md", ["`tools/gone1.sh`", "`tools/run.sh`"]),
			("CONTRIBUTING.md", ["ok", "`tools/gone2.sh`"]),
			("ARCHITECTURE.md", ["ok", "ok", "`tools/gone3.sh`"]),
			("docs/other.md", ["`tools/gone4.sh`"]),
		];

		var (exit, lines) = Check( [], pages );
		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 3, 3 ),
			Problem( "README.md", 1, "tools/gone1.sh", NoFile ),
			Problem( "CONTRIBUTING.md", 2, "tools/gone2.sh", NoFile ),
			Problem( "ARCHITECTURE.md", 3, "tools/gone3.sh", NoFile ),
		}, lines );

		(exit, lines) = Check( ["ARCHITECTURE.md", "docs/other.md"], pages );
		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 2, 2 ),
			Problem( "ARCHITECTURE.md", 3, "tools/gone3.sh", NoFile ),
			Problem( "docs/other.md", 1, "tools/gone4.sh", NoFile ),
		}, lines );

		(exit, lines) = Check( ["CONTRIBUTING.md"], ("CONTRIBUTING.md", ["`tools/run.sh`"]) );
		Assert.AreEqual( 0, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[] { Header( 1, 0 ) }, lines );
	}
}
