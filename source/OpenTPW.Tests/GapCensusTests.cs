using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The census of counted names against the queue (<c>tools/gap_census.py</c>, Q243): every name an
/// <c>Unimplemented.Report</c> call can take, and whether <c>docs/QUEUE.md</c> names it. It is run over a scratch
/// repository that holds a copy of the script, one source file and a queue, so the real source and queue can change
/// without these tests moving.
/// </summary>
[TestClass]
public class GapCensusTests
{
	/// <summary>The fixture's source: each way a call names its gap, and calls that must not count.</summary>
	private const string Source = """
		namespace OpenTPW;
		class Fixture
		{
			// Unimplemented.Report( "IN_A_LINE_COMMENT" );
			/* Unimplemented.Report( "IN_A_BLOCK_COMMENT" ); */
			void Go( bool a, int x, string s, string gap )
			{
				Unimplemented.Report( "HELD_WHOLE" );
				Unimplemented.Report( "HELD" );
				Unimplemented.Report( a ? "HELD_WHOLE" : "OTHER_ARM" );
				Unimplemented.Report( $"TEMPLATE_{x}" );
				Unimplemented.Report( $"UNHELD_PATTERN_{x}_END" );
				Unimplemented.Report( $"{s}: OPCODE with {x} operand(s)" );
				Unimplemented.Report( $"guest state {s}" );
				Unimplemented.Report( "ADVISOR_0xCB" );
				Unimplemented.Report( "LONG_STEM_TWO" );
				Unimplemented.Report( "LONG_STEM_THREE" );
				var text = "Unimplemented.Report( \"IN_A_STRING\" )";
			}

			string Pick( int k ) => k == 0 ? "GAP_ONE" : k == 1 ? "GAP_TWO" : "WORD";

			void Indirect( int k )
			{
				var gap = Pick( k );
				Unimplemented.Report( gap );
			}

			// Dead by CONTENT: no placed thing reaches it (Q50).
			void Dead() => Unimplemented.Report( "DEAD_ONE" );
		}
		""";

	/// <summary>The fixture's queue: an open item holding some names whole, one short, and one only a tail with no stem.</summary>
	private static readonly string[] Queue =
	[
		"# Work queue",
		"## A. Bugs first",
		"- [ ] **Q50. An open item.** Counts `HELD_WHOLE` and `TEMPLATE_3`, and `GAP_ONE`.",
		"- [ ] **Q51. A short-form item.** Counts `LONG_STEM_ONE`, `_TWO` the same.",
		"  More text, and `ADVISOR_0xCB`.",
		"- [ ] **Q52. A tail alone.** Counts `_THREE`, with no stem here.",
	];

	/// <summary>The census's exit code and printed lines over the fixture, run from the scratch repository with these arguments; the end of the test where there is no Python.</summary>
	private static (int Exit, string[] Lines) Census( params string[] arguments )
	{
		var root = RepoFiles.Root();
		var folder = Path.Join( Path.GetTempPath(), "opentpw-gapcensus-" + Guid.NewGuid().ToString( "N" ) );

		if ( RepoFiles.Python( ["--version"] ) is not { Exit: 0 } )
			Assert.Inconclusive( "skipped: no Python here" );

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "tools" ) );
			Directory.CreateDirectory( Path.Join( folder, "docs" ) );
			Directory.CreateDirectory( Path.Join( folder, "source", "OpenTPW", "obj" ) );
			Directory.CreateDirectory( Path.Join( folder, "source", "OpenTPW.Tests" ) );
			File.Copy( Path.Join( root, "tools", "gap_census.py" ), Path.Join( folder, "tools", "gap_census.py" ) );
			File.WriteAllText( Path.Join( folder, "source", "OpenTPW", "Fixture.cs" ), Source.Replace( "\r", "" ) + "\n" );
			File.WriteAllText( Path.Join( folder, "source", "OpenTPW", "obj", "Built.cs" ), "class B { void G() => Unimplemented.Report( \"IN_OBJ\" ); }\n" );
			File.WriteAllText( Path.Join( folder, "source", "OpenTPW.Tests", "T.cs" ), "class T { void G() => Unimplemented.Report( \"IN_A_TEST\" ); }\n" );
			File.WriteAllText( Path.Join( folder, "docs", "QUEUE.md" ), string.Join( "\n", Queue ) + "\n" );

			var result = RepoFiles.Python( new[] { "tools/gap_census.py" }.Concat( arguments ).ToArray(), null, folder );
			var output = result!.Value.Output.Split( '\n', StringSplitOptions.RemoveEmptyEntries ).Select( line => line.TrimEnd( '\r' ) ).ToArray();

			return (result.Value.Exit, output);
		}
		finally
		{
			RepoFiles.DeleteTree( folder );
		}
	}

	/// <summary>The line of a fixture call, counted from the raw string's first line as the file holds it.</summary>
	private static int LineOf( string text ) => Source.Replace( "\r", "" ).Split( '\n' ).ToList().FindIndex( line => line.Contains( text, StringComparison.Ordinal ) ) + 1;

	private const string FixturePath = "source/OpenTPW/Fixture.cs";

	/// <remarks>
	/// <b>Mutation:</b> a name matched as a substring (so <c>HELD</c> is held by <c>HELD_WHOLE</c>); comments not
	/// blanked (the two commented calls counted); strings not skipped (the call inside a string counted); the tests
	/// folder or <c>obj/</c> read; a <c>?:</c> read for its first string only; a short tail held with no stem in its item
	/// (<c>LONG_STEM_THREE</c>); a hex name (<c>0xCB</c>) taken for a message.
	/// </remarks>
	[TestMethod]
	public void EachNameTheQueueDoesNotHoldIsListedAtItsCall()
	{
		var (exit, lines) = Census();

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.AreEqual( new[]
		{
			$"{FixturePath}:{LineOf( "\"HELD\" )" )}: HELD: not in the queue",
			$"{FixturePath}:{LineOf( "\"OTHER_ARM\"" )}: OTHER_ARM: not in the queue",
			$"{FixturePath}:{LineOf( "UNHELD_PATTERN_" )}: UNHELD_PATTERN_*_END (PATTERN): not in the queue",
			$"{FixturePath}:{LineOf( ": OPCODE with" )}: OPCODE (MESSAGE): not in the queue",
			$"{FixturePath}:{LineOf( "\"LONG_STEM_THREE\"" )}: LONG_STEM_THREE: not in the queue",
			$"{FixturePath}:{LineOf( "Report( gap )" )}: GAP_TWO (INDIRECT via Report( gap )): not in the queue",
			$"{FixturePath}:{LineOf( "\"DEAD_ONE\"" )}: DEAD_ONE: not in the queue; near: Q50(open); dead by CONTENT",
			"12 report site(s) (DYNAMIC 1, INDIRECT 1, MESSAGE 1, NAME 7, PATTERN 2); 7 name(s) not in the queue, at 7 site(s)",
		}, lines, string.Join( "\n", lines ) );
	}

	/// <remarks>
	/// <b>Mutation:</b> a pattern's hole not matched by a name's characters (<c>TEMPLATE_3</c> no longer holds
	/// <c>TEMPLATE_*</c>); the short form not read (<c>LONG_STEM_TWO</c> missing); an INDIRECT site given a string with no
	/// underscore (<c>WORD</c>) or a name a direct call already uses (<c>HELD_WHOLE</c>); a run-time name reported as missing.
	/// </remarks>
	[TestMethod]
	public void AllShowsWhereEachHeldNameIsHeldAndTheRunTimeNames()
	{
		var (exit, lines) = Census( "--all" );

		Assert.AreEqual( 1, exit, string.Join( "\n", lines ) );
		CollectionAssert.IsSubsetOf( new[]
		{
			$"{FixturePath}:{LineOf( "\"HELD_WHOLE\" );" )}: HELD_WHOLE: held at QUEUE.md:3",
			$"{FixturePath}:{LineOf( "TEMPLATE_{x}\" )" )}: TEMPLATE_* (PATTERN): held at QUEUE.md:3",
			$"{FixturePath}:{LineOf( "\"ADVISOR_0xCB\"" )}: ADVISOR_0xCB: held at QUEUE.md:5",
			$"{FixturePath}:{LineOf( "\"LONG_STEM_TWO\"" )}: LONG_STEM_TWO: held at QUEUE.md:4",
			$"{FixturePath}:{LineOf( "Report( gap )" )}: GAP_ONE (INDIRECT via Report( gap )): held at QUEUE.md:3",
			$"{FixturePath}:{LineOf( "guest state" )}: DYNAMIC Report( $\"guest state {{s}}\" ): named only at run time",
		}, lines, string.Join( "\n", lines ) );

		var indirect = lines.Where( line => line.Contains( "(INDIRECT via", StringComparison.Ordinal ) ).ToArray();
		Assert.AreEqual( 2, indirect.Length, string.Join( "\n", indirect ) );
		Assert.IsFalse( lines.Any( line => line.Contains( "IN_", StringComparison.Ordinal ) ), string.Join( "\n", lines ) );
	}

	/// <remarks><b>Mutation:</b> the exit code 1 kept when nothing is missing, or a pattern's queue spelling with its own <c>*</c> not held.</remarks>
	[TestMethod]
	public void ANameEveryItemHoldsLeavesNothingAndExitsNought()
	{
		var root = RepoFiles.Root();
		var folder = Path.Join( Path.GetTempPath(), "opentpw-gapcensus-" + Guid.NewGuid().ToString( "N" ) );

		if ( RepoFiles.Python( ["--version"] ) is not { Exit: 0 } )
			Assert.Inconclusive( "skipped: no Python here" );

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "tools" ) );
			Directory.CreateDirectory( Path.Join( folder, "docs" ) );
			Directory.CreateDirectory( Path.Join( folder, "source", "OpenTPW" ) );
			File.Copy( Path.Join( root, "tools", "gap_census.py" ), Path.Join( folder, "tools", "gap_census.py" ) );
			File.WriteAllText( Path.Join( folder, "source", "OpenTPW", "A.cs" ), "class A { void G( int x ) { Unimplemented.Report( \"ONLY_ONE\" ); Unimplemented.Report( $\"KIND_{x}\" ); } }\n" );
			File.WriteAllText( Path.Join( folder, "docs", "QUEUE.md" ), "- [ ] **Q1. Item.** `ONLY_ONE` and `KIND_*`.\n" );

			var result = RepoFiles.Python( ["tools/gap_census.py"], null, folder )!.Value;

			Assert.AreEqual( 0, result.Exit, result.Output );
			Assert.AreEqual( "2 report site(s) (NAME 1, PATTERN 1); 0 name(s) not in the queue, at 0 site(s)", result.Output.Trim() );
		}
		finally
		{
			RepoFiles.DeleteTree( folder );
		}
	}

	/// <summary>The census over one scratch source file and a one-line queue: its exit code and its output.</summary>
	private static (int Exit, string Output) CensusOver( string source, string queue )
	{
		var root = RepoFiles.Root();
		var folder = Path.Join( Path.GetTempPath(), "opentpw-gapcensus-" + Guid.NewGuid().ToString( "N" ) );

		if ( RepoFiles.Python( ["--version"] ) is not { Exit: 0 } )
			Assert.Inconclusive( "skipped: no Python here" );

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "tools" ) );
			Directory.CreateDirectory( Path.Join( folder, "docs" ) );
			Directory.CreateDirectory( Path.Join( folder, "source", "OpenTPW" ) );
			File.Copy( Path.Join( root, "tools", "gap_census.py" ), Path.Join( folder, "tools", "gap_census.py" ) );
			File.WriteAllText( Path.Join( folder, "source", "OpenTPW", "R.cs" ), source.Replace( "\r", "" ) );
			File.WriteAllText( Path.Join( folder, "docs", "QUEUE.md" ), queue );

			var result = RepoFiles.Python( ["tools/gap_census.py"], null, folder )!.Value;
			return (result.Exit, result.Output.Replace( "\r", "" ).Trim());
		}
		finally
		{
			RepoFiles.DeleteTree( folder );
		}
	}

	/// <remarks>
	/// A raw string with one quote inside, a character literal <c>'"'</c> in an interpolation hole and a raw
	/// interpolated string with two dollars each read as C# does, so the calls after them are still found, and a name
	/// joined to something by <c>+</c> is a site to read by hand, not a made-up name.
	/// <b>Mutation:</b> raw strings read as <c>""</c> then a plain string, or a hole that reads no character literal
	/// (the rest of the file taken for a string: no site at all, exit 0); a joined string taken for its prefix
	/// (<c>PREFIX_</c> reported as a name).
	/// </remarks>
	[TestMethod]
	public void RawStringsAndCharactersInHolesHideNoCallAfterThem()
	{
		const string source = """"
			class R
			{
				void G( bool a, int k )
				{
					var t = """one " quote""";
					var u = $"{(a ? '"' : 'b')}";
					var v = $$"""{{k}} "" {""";
					Unimplemented.Report( "AFTER_IT" );
					Unimplemented.Report( "PREFIX_" + k );
					Unimplemented.Report( $$"""RAW_{{k}}""" );
				}
			}

			"""";

		var (exit, output) = CensusOver( source, "- [ ] **Q1. Item.** `AFTER_IT` and `RAW_*`.\n" );

		Assert.AreEqual( 1, exit, output );
		Assert.AreEqual( "source/OpenTPW/R.cs:9: INDIRECT Report( \"PREFIX_\" + k ): no name-shaped string in the file; find its names by hand\n"
			+ "3 report site(s) (INDIRECT 1, NAME 1, PATTERN 1); 1 name(s) not in the queue, at 1 site(s)", output );
	}

	/// <remarks><b>Mutation:</b> a literal left open to the file's end taken as a string, so the census counts what it
	/// read before it and exits nought, and any call after it is lost unsaid.</remarks>
	[TestMethod]
	public void ALiteralThatNeverClosesStopsTheCensusWithTwo()
	{
		var (exit, output) = CensusOver( "class U { void G() => Unimplemented.Report( \"AFTER_IT\" ); string S = \"open; }\n", "- [ ] **Q1. Item.** `AFTER_IT`.\n" );

		Assert.AreEqual( 2, exit, output );
		StringAssert.StartsWith( output, "source/OpenTPW/R.cs: a string or character literal opened at line 1 does not close" );
	}
}
