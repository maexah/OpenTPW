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
		("docs/guide.md", "# Where things are\n## `Foo` Bar!\n### A \u2014 b (Q&A)\n#### Snake case\n###### Six\n####### Seven\n#NoSpace\n##### Re-use it\n## Trailing \n## snake_name\n## Same\n## Same\n## Same\n```sh\n# InTick\n```\n~~~\n```\n# InTilde\n~~~\n````md\n```\n# InFour\n```\n````\n## After fences\n## Closed ##\n## C# ##\n## Not# closed\n## Sharp # mid\nText # Fake\n## _Word_ and *star* x\n## [Link _a_](http://x.y/z_w \"t\") end\n## <b>Html</b> tag_\n## ![Alt](a_b.png) pic\n## __init__.py\n## foo_bar_\n## \\_lit\\_ x\n## `co_de` x\n## a_b_c\nSetext title\n============\nSetext two\n----------\n- item here\n---\n  ### Indented three\n    #### Code four\n> ## Quoted heading\n- ## Item heading\n\n## x `` a `` y\n## \u20ac_x_ y\n## <https://a_b.c/d> auto\n## c<!-- hid_den -->d\n## [_a](u) b_\n## [a [b](u) c](v) d\n## \\<b> x\nPara before\n***\nSetext after hr\n======\n> lazyq\nlazy underline\n=======\n\nTrailing spaces\n--- \n\n## [i ![j](u) k](v) l\n## [o \\![p](u) q](v) r\n## _.__.\n## *a _b* c_\n## $_q_ z\n\n- list a\n> \n  ### Q heading\n-\n\n   ## Empty item then heading\n\nSetext first\n2. second line\n=========\n\nPara one\n__\nSetext cont\n======\n\n## <img src=x> Play\n\nSetext trail  \n=====\n"u8.ToArray()),
		("docs/p(1).md", "x"u8.ToArray()),
		("docs/front.md", "---\nname: x\n---\n# After front\n"u8.ToArray()),
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

	/// <remarks><b>Mutation:</b> <c>slug()</c> collapsing spaces (<c>a-b-qa</c>), keeping backticks, punctuation or <c>&amp;</c>, not lower-casing, dropping <c>_</c>; the heading pattern taking seven hashes or no space after them or missing the sixth level; a repeated heading not numbered (<c>-1</c>, <c>-2</c>) or numbered from 0; a heading inside a backtick, tilde or longer fence counted; a closing run of hashes (<c>## Closed ##</c>) kept, or a run of hashes inside the heading cut (<c>Sharp # mid</c>); a heading found after text on its line. <b>Q879:</b> the heading's markup not rendered first (emphasis, a link's target, inline HTML, an image, a backslash escape, a code span's underscore: `_Word_`, `foo_bar_`, `__init__.py`, `\\_lit\\_`); a setext heading (a paragraph over <c>===</c> or <c>---</c>) not found, or a paragraph over a list item's <c>---</c> found; a heading indented three spaces not found, or four found; emphasis read across a link's brackets, or a link inside a link (an image does not count as one, an escaped-bang link does); an escaped <c>&lt;</c> read as a tag; a symbol (<c>\u20ac</c>) counted as punctuation (CommonMark 0.29, GitHub's, counts only P and ASCII); a `***` between paragraphs not ending one; a lazy continuation line read as a setext underline; trailing spaces after an underline refused; <c>2.</c> interrupting a paragraph, or <c>__</c> read as a rule; a quoted blank line (<c>&gt; </c>) or an empty item before a blank line not ending the list item; the rule of three, or a pair cut across an inner pair, in <c>_</c> emphasis. Multi-line setext rows (<c>setext-first2-second-line</c>, <c>para-one__setext-cont</c>) pin how the paragraph is read; GitHub's id of a heading over two lines is not measured (Q1021). The quoted and list-item rows pin what GitHub's ids are not measured for (Q1021): a heading there is left out.</remarks>
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
	[DataRow( "word-and-star-x", true )]
	[DataRow( "_word_-and-star-x", false )]
	[DataRow( "link-a-end", true )]
	[DataRow( "link-_a_-end", false )]
	[DataRow( "html-tag_", true )]
	[DataRow( "alt-pic", true )]
	[DataRow( "initpy", true )]
	[DataRow( "__init__py", false )]
	[DataRow( "foo_bar_", true )]
	[DataRow( "_lit_-x", true )]
	[DataRow( "lit-x", false )]
	[DataRow( "co_de-x", true )]
	[DataRow( "a_b_c", true )]
	[DataRow( "setext-title", true )]
	[DataRow( "setext-two", true )]
	[DataRow( "item-here", false )]
	[DataRow( "indented-three", true )]
	[DataRow( "code-four", false )]
	[DataRow( "quoted-heading", false )]
	[DataRow( "item-heading", false )]
	[DataRow( "x-a-y", true )]
	[DataRow( "x--a--y", false )]
	[DataRow( "_x_-y", true )]
	[DataRow( "x-y", false )]
	[DataRow( "httpsa_bcd-auto", true )]
	[DataRow( "auto", false )]
	[DataRow( "cd", true )]
	[DataRow( "_a-b_", true )]
	[DataRow( "a-b", false )]
	[DataRow( "a-b-cv-d", true )]
	[DataRow( "a-b-c-d", false )]
	[DataRow( "b-x", true )]
	[DataRow( "x", false )]
	[DataRow( "setext-after-hr", true )]
	[DataRow( "lazy-underline", false )]
	[DataRow( "trailing-spaces", true )]
	[DataRow( "i-j-k-l", true )]
	[DataRow( "i-j-kv-l", false )]
	[DataRow( "o-p-qv-r", true )]
	[DataRow( "o-p-q-r", false )]
	[DataRow( "___", true )]
	[DataRow( "_", false )]
	[DataRow( "a-_b-c_", true )]
	[DataRow( "a-b-c", false )]
	[DataRow( "q-z", true )]
	[DataRow( "_q_-z", false )]
	[DataRow( "q-heading", true )]
	[DataRow( "empty-item-then-heading", true )]
	[DataRow( "setext-first2-second-line", true )]
	[DataRow( "para-one__setext-cont", true )]
	[DataRow( "setext-cont", false )]
	[DataRow( "-play", true )]
	[DataRow( "play", false )]
	[DataRow( "setext-trail", true )]
	[DataRow( "lazyqlazy-underline", false )]
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

	/// <remarks><b>Mutation (Q879, case 6):</b> a quoted line read with its <c>&gt; </c> (the fence not found); a fence opened in a quote surviving the quote's end; the quote depth of a nested fence ignored; a deeper quote marker inside the fence stripped.</remarks>
	[TestMethod]
	public void AFenceInAQuoteIsReadWithoutItsMarkerAndEndsWhereTheQuoteEnds()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"> ```sh",
			"> ls tools/gone1.sh",
			"> ```",
			"`ParkStatQ1`",
			"> ~~~",
			"> ls tools/gone2.sh",
			"`ParkStatQ2`",
			"> > ```",
			"> > ls tools/gone3.sh",
			"> > ```",
			"`ParkStatQ3`",
			"> ```",
			"> > ls tools/gone4.sh",
			"> ```",
			"`ParkStatQ4`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 8 ),
			Problem( "README.md", 2, "tools/gone1.sh", NoFile ),
			Problem( "README.md", 4, "ParkStatQ1", NoType ),
			Problem( "README.md", 6, "tools/gone2.sh", NoFile ),
			Problem( "README.md", 7, "ParkStatQ2", NoType ),
			Problem( "README.md", 9, "tools/gone3.sh", NoFile ),
			Problem( "README.md", 11, "ParkStatQ3", NoType ),
			Problem( "README.md", 13, "tools/gone4.sh", NoFile ),
			Problem( "README.md", 15, "ParkStatQ4", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, case 3):</b> a fence's indent counted from the line's start and not its list item's content column (a fence in an item not found, or four columns past an item's content still a fence); an indented block (four columns, no paragraph above it) opening a fence that is never closed, or its words not checked; an indented line right under a paragraph read as code.</remarks>
	[TestMethod]
	public void AFenceIsIndentedUpToThreeColumnsPastItsContainerAndFourIsCode()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"text",
			"",
			"    ```sh",
			"    ls tools/gone1.sh",
			"`ParkStatI1`",
			"",
			"- item",
			"     ```sh",
			"     ls tools/gone2.sh",
			"     ```",
			"`ParkStatI2`",
			"",
			"- item",
			"",
			"      ```sh",
			"      ls tools/gone3.sh",
			"  `ParkStatI3`",
			"",
			"para",
			"    ~~~ `ParkStatU1`",
			"`ParkStatI4`",
			"",
			"1. item",
			"      ```sh",
			"      ls tools/gone5.sh",
			"      ```",
			"      `ParkStatI5`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 10 ),
			Problem( "README.md", 4, "tools/gone1.sh", NoFile ),
			Problem( "README.md", 5, "ParkStatI1", NoType ),
			Problem( "README.md", 9, "tools/gone2.sh", NoFile ),
			Problem( "README.md", 11, "ParkStatI2", NoType ),
			Problem( "README.md", 16, "tools/gone3.sh", NoFile ),
			Problem( "README.md", 17, "ParkStatI3", NoType ),
			Problem( "README.md", 20, "ParkStatU1", NoType ),
			Problem( "README.md", 21, "ParkStatI4", NoType ),
			Problem( "README.md", 25, "tools/gone5.sh", NoFile ),
			Problem( "README.md", 27, "ParkStatI5", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, cases 3 and 6):</b> a fence outliving the list item or the quote it was opened in; a closing fence indented four columns read as the closer.</remarks>
	[TestMethod]
	public void AFenceEndsWithItsContainerAndOnlyAShortIndentClosesIt()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"- item",
			"  ```sh",
			"  ls tools/gone1.sh",
			"`ParkStatL1`",
			"",
			"```sh",
			"ls tools/gone2.sh",
			"    ```",
			"ls tools/gone3.sh",
			"```",
			"`ParkStatL2`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 5 ),
			Problem( "README.md", 3, "tools/gone1.sh", NoFile ),
			Problem( "README.md", 4, "ParkStatL1", NoType ),
			Problem( "README.md", 7, "tools/gone2.sh", NoFile ),
			Problem( "README.md", 9, "tools/gone3.sh", NoFile ),
			Problem( "README.md", 11, "ParkStatL2", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, cases 2 and 3):</b> a lazy line (under a list item or a quote) read as a setext underline, by quote depth alone or by indent alone; a lazy indented <c>####</c> line read as a block start; <c>-     x</c> (five columns after the marker) not code, or its code column wrong; <c>2)</c> not a list marker.</remarks>
	[TestMethod]
	public void ALazyLineContinuesItsParagraphAndFiveColumnsAfterAMarkerIsCode()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"- list",
			"lazy",
			"===",
			"      ls tools/gone1.sh",
			"",
			"> quote",
			"lazy",
			"===",
			"      ls tools/gone2.sh",
			"",
			"> quote2",
			"    #### `ParkStatZ1`",
			"",
			"-     ls tools/gone3.sh",
			"",
			"2) item",
			"      ```sh",
			"      ls tools/gone4.sh",
			"      ```",
			"      `ParkStatZ2`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 4 ),
			Problem( "README.md", 12, "ParkStatZ1", NoType ),
			Problem( "README.md", 14, "tools/gone3.sh", NoFile ),
			Problem( "README.md", 18, "tools/gone4.sh", NoFile ),
			Problem( "README.md", 20, "ParkStatZ2", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, cases 4 and 10):</b> a <c>&lt;...&gt;</c> target read with its brackets or cut at a space; a bare target cut at the first <c>)</c> (the file <c>p(1).md</c> then reported); parentheses not balanced; a title with an escaped quote (<c>'it\'s'</c>, <c>"say \"hi\""</c>) or an escaped bracket hiding the link; a space inside a bare target read as part of it; a reference definition (<c>[r]: a.md</c>) not read, read inside a paragraph, a footnote (<c>[^1]:</c>) read as one, or its <c>&lt;...&gt;</c> target read raw.</remarks>
	[TestMethod]
	public void ALinkTargetMayBeInAngleBracketsOrHoldBalancedParenthesesAndADefinitionIsALink()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"[ok](<docs/guide.md>) [bad](<docs/missing a.md>) [ok](docs/p(1).md) [bad](docs/p(2).md)",
			"a \\](docs/missing12.md) b \\\\](docs/missing13.md)",
			"[ok](docs/guide.md 'it\\'s') [bad](docs/missing7.md \"say \\\"hi\\\"\") [ok](docs/guide.md (a \\) b)) [bad](docs/missing8.md 'it\\'s')",
			"[not a link](docs/a b.md) [not](<docs/guide.md>x) [not](<docs/missing14.md>\"t\") [not](<a<b.md>) [not](docs/missing9.md (a (b)))",
			"[bad](docs/p\\(3\\).md) [bad](<docs/gone\\>x.md>) [bad](`docs/x.md`)",
			"",
			"[r]: docs/guide.md \"t\"",
			"[bad]: docs/gone-def.md",
			"[bad2]: <docs/gone def.md> 'title'",
			"[anchor]: docs/guide.md#nope",
			"[ok2]: docs/p(1).md",
			"[^1]: footnote docs/gone-note.md `ParkStatF`",
			"",
			"para",
			"[nodef]: docs/gone-nodef.md",
			"",
			"[Note]: this is prose",
			"",
			"[bad3]: docs/gone-junk.md \"t\" junk",
			"",
			"[ ]: docs/gone-blank.md",
			"",
			"[next]:",
			"docs/gone-next.md",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 13 ),
			Problem( "README.md", 1, "docs/missing a.md", NoLinkFile ),
			Problem( "README.md", 1, "docs/p(2).md", NoLinkFile ),
			Problem( "README.md", 2, "docs/missing13.md", NoLinkFile ),
			Problem( "README.md", 3, "docs/missing7.md", NoLinkFile ),
			Problem( "README.md", 3, "docs/missing8.md", NoLinkFile ),
			Problem( "README.md", 5, "docs/p(3).md", NoLinkFile ),
			Problem( "README.md", 5, "docs/gone>x.md", NoLinkFile ),
			Problem( "README.md", 5, "`docs/x.md`", NoLinkFile ),
			Problem( "README.md", 5, "docs/x.md", NoFile ),
			Problem( "README.md", 8, "docs/gone-def.md", NoLinkFile ),
			Problem( "README.md", 9, "docs/gone def.md", NoLinkFile ),
			Problem( "README.md", 10, "docs/guide.md#nope", NoLinkHeading ),
			Problem( "README.md", 12, "ParkStatF", NoType ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, cases 7 and 8):</b> code spans read as a single backtick pair (<c>``x``</c> then lost, or <c>`` `x` ``</c> mangled); a span closed by a run of another length; a link inside a span read as a link, or its text checked as a name; a backslash-escaped backtick opening a span.</remarks>
	[TestMethod]
	public void ACodeSpanIsARunOfBackticksClosedByTheSameRunAndHoldsNoLink()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"``tools/gone1.sh`` and ``ParkStatDbl`` and `` ParkStatPad `` and ```ParkStatTri```",
			"`[x](docs/gone-span.md)` and ``[y](docs/gone-span2.md)`` and `a``b` and `c` `d",
			"`ParkStatR1``x` and ``ParkStatR2`y`` and [not](docs/x\u0001y.md)",
			"\\`tools/gone-esc.sh\\` `ParkStatE1` \\\\`ParkStatE2`",
			"`docs/p(1).md` `docs/gone(1).md` `tools/gone[1].py` `open(tools/gone2.py)` `(a+b)/50` `curve[7]/255`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 9 ),
			Problem( "README.md", 1, "tools/gone1.sh", NoFile ),
			Problem( "README.md", 1, "ParkStatDbl", NoType ),
			Problem( "README.md", 1, "ParkStatPad", NoType ),
			Problem( "README.md", 1, "ParkStatTri", NoType ),
			Problem( "README.md", 4, "ParkStatE1", NoType ),
			Problem( "README.md", 4, "ParkStatE2", NoType ),
			Problem( "README.md", 5, "docs/gone(1).md", NoFile ),
			Problem( "README.md", 5, "tools/gone[1].py", NoFile ),
			Problem( "README.md", 5, "tools/gone2.py", NoFile ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, case 9):</b> a full stop ending a word kept (the file type then empty and the word skipped); <c>rstrip</c> cutting more than the full stops.</remarks>
	[TestMethod]
	public void AFullStopEndingAWordOfACommandIsTheSentencesNotTheFileNames()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md",
		[
			"```",
			"ls tools/gone1.sh. then tools/run.sh.",
			"go to tools/gone2.py, or tools/run.sh... or tools/. or .. ",
			"```",
			"`run tools/gone3.sh.` and `run tools/run.sh.`",
		]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			Header( 1, 3 ),
			Problem( "README.md", 2, "tools/gone1.sh", NoFile ),
			Problem( "README.md", 3, "tools/gone2.py", NoFile ),
			Problem( "README.md", 5, "tools/gone3.sh", NoFile ),
		}, lines );
	}

	/// <remarks><b>Mutation (Q879, case 2 and the page GitHub shows as a table):</b> YAML front matter (a first line <c>---</c> to the next) read as a paragraph over a setext underline, which makes <c>name-x</c> a heading; the heading after it not found.</remarks>
	[TestMethod]
	public void AFrontMatterBlockIsNotAHeading()
	{
		var (exit, lines) = Check( ["README.md"], ("README.md", ["[ok](docs/front.md#after-front) [bad](docs/front.md#name-x)"]) );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[] { Header( 1, 1 ), Problem( "README.md", 1, "docs/front.md#name-x", NoLinkHeading ) }, lines );
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
