using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Every ride script the game ships, read the way the game reads them.
///
/// <para>
/// <b>These are a walk rather than a list of names on purpose.</b> The format was worked out on two
/// files and looked settled on both; run against all 308 it turned out that a reading which stopped
/// at the string blob accounted for only 98 of them, and the 98 were exactly those declaring no
/// variables. A test naming a script or two would have been green throughout that, so this one
/// discovers them instead and states its counts afterwards.
/// </para>
///
/// <para>
/// The properties checked below are the ones that can break silently. A script whose instructions
/// are split at the wrong stride still yields instructions - just the wrong ones - and the
/// difference is invisible until something executes them. What catches it is that every branch in
/// the corpus points at a word the same walk calls the start of an instruction: get the stride
/// wrong anywhere and targets start landing in the middle of operands.
/// </para>
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideScriptTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = GameData.Required();
	}

	/// <summary>
	/// The folders a theme keeps scriptable things in. <c>upgrades</c> is here and absent from
	/// <see cref="SignFileTests"/> because upgrades carry a script without carrying a name board.
	/// </summary>
	private static readonly string[] Folders = ["features", "rides", "shops", "sideshow", "upgrades"];

	/// <summary>What the game holds, counted through its own file system: 308 scripts in 262 archives.</summary>
	private const int ExpectedScripts = 308;

	/// <summary>
	/// Three scripts spell their extension <c>.rse</c> where the other 305 spell it <c>.RSE</c>. On the
	/// original's file system that distinction did not exist; here it is the difference between a ride
	/// loading and not, so the count is pinned rather than left to chance.
	/// </summary>
	private const int ExpectedLowerCaseExtension = 3;

	/// <summary>Parsed once and kept - 308 files is more than each test wants to read again.</summary>
	private static (string Path, RideScriptFile Script)[]? _scripts;

	private static (string Path, RideScriptFile Script)[] Scripts()
	{
		return _scripts ??= [.. EveryScriptPath().Select( path => (path, new RideScriptFile( path )) )];
	}

	/// <summary>
	/// Where every ride script lives: inside the archive of the thing it drives, under whichever of
	/// the five item folders that thing is sold from.
	/// </summary>
	private static List<string> EveryScriptPath()
	{
		var scripts = new List<string>();

		foreach ( var theme in Entries( "levels", directories: true ) )
		{
			var themeName = Path.GetFileName( theme );

			if ( string.IsNullOrEmpty( themeName ) )
				continue;

			foreach ( var folder in Folders )
			{
				foreach ( var item in Entries( $"levels/{themeName}/{folder}", directories: true ) )
				{
					var stem = Path.GetFileName( item );

					if ( string.IsNullOrEmpty( stem ) )
						continue;

					foreach ( var entry in Entries( $"levels/{themeName}/{folder}/{stem}", directories: false ) )
					{
						var name = Path.GetFileName( entry );

						// Case-insensitively, because three of them are spelled .rse.
						if ( !string.IsNullOrEmpty( name )
							&& name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) )
							scripts.Add( $"levels/{themeName}/{folder}/{stem}/{name}" );
					}
				}
			}
		}

		return scripts;
	}

	/// <summary>
	/// A theme that does not sell one kind of thing has no folder for it, and asking throws rather
	/// than answering nothing - so this answers nothing, exactly as ParkItemCatalogue does.
	/// </summary>
	private static string[] Entries( string path, bool directories )
	{
		try
		{
			return directories ? FileSystem.GetDirectories( path ) : FileSystem.GetFiles( path );
		}
		catch ( Exception )
		{
			return [];
		}
	}

	/// <summary>The one that would have caught it: not one script in the game is refused.</summary>
	[TestMethod]
	public void EveryRideScriptInTheGameReads()
	{
		var scripts = Scripts();

		var refused = scripts.Where( entry => !entry.Script.IsValid ).Select( entry => entry.Path ).ToArray();

		Assert.AreEqual( 0, refused.Length,
			$"{refused.Length} of {scripts.Length} ride scripts would not read, starting with '{refused.FirstOrDefault()}'" );

		// Counted after the reading rather than before it, so a surprise in the count cannot fire
		// first and hide a script that would not read.
		Assert.AreEqual( ExpectedScripts, scripts.Length, "ride scripts found by the walk" );
	}

	/// <summary>
	/// Three of them are <c>.rse</c> rather than <c>.RSE</c>, and a case-sensitive walk would drop
	/// exactly those - quietly, and only on a case-sensitive file system.
	/// </summary>
	[TestMethod]
	public void ScriptsAreFoundWhicheverWayTheExtensionIsSpelled()
	{
		var lower = Scripts().Count( entry => entry.Path.EndsWith( ".rse", StringComparison.Ordinal ) );

		Assert.AreEqual( ExpectedLowerCaseExtension, lower, "scripts whose extension is lower case" );
	}

	/// <summary>
	/// Every branch in the game points at the start of an instruction.
	///
	/// <para>
	/// This is the test that holds the decoding together. A branch target is a word index into the
	/// body, so it can be compared directly against the addresses the walk produced; if the operand
	/// counts were wrong anywhere, the walk would drift and targets would begin landing on operand
	/// words instead. Getting it right for one script proves little - getting it right for all 2,664
	/// branches in the corpus is the whole instruction set agreeing with the data.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryBranchPointsAtTheStartOfAnInstruction()
	{
		var stray = new List<string>();
		var branches = 0;

		foreach ( var (path, script) in Scripts() )
		{
			var starts = script.Instructions.Select( instruction => instruction.Address ).ToHashSet();

			foreach ( var instruction in script.Instructions )
			{
				foreach ( var operand in instruction.Operands.Where( o => o.Kind == RideOperandKind.Location ) )
				{
					++branches;

					if ( !starts.Contains( operand.Value ) )
						stray.Add( $"{Path.GetFileName( path )} {instruction.Opcode}@{instruction.Address} -> {operand.Value}" );
				}
			}
		}

		Assert.AreEqual( 0, stray.Count,
			$"{stray.Count} branches do not land on an instruction: {string.Join( " | ", stray.Take( 10 ) )}" );

		Assert.IsTrue( branches > 2000, $"only {branches} branch targets were checked, so the walk found little to check" );
	}

	/// <summary>
	/// Every string an instruction names is a string the blob actually holds.
	///
	/// <para>
	/// A string operand carries a byte offset into the blob rather than an index, so an off-by-one in
	/// the blob's parsing puts every one of them a character into the previous string - which still
	/// yields text, just the wrong text. Requiring each to land on a string the walk found is what
	/// tells the two apart.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryStringOperandNamesAStringThatIsThere()
	{
		var missing = new List<string>();
		var referenced = 0;

		foreach ( var (path, script) in Scripts() )
		{
			foreach ( var instruction in script.Instructions )
			{
				foreach ( var operand in instruction.Operands.Where( o => o.Kind == RideOperandKind.String ) )
				{
					++referenced;

					if ( script.StringAt( operand.Value ) == null )
						missing.Add( $"{Path.GetFileName( path )} {instruction.Opcode} -> {operand.Value}" );
				}
			}
		}

		Assert.AreEqual( 0, missing.Count,
			$"{missing.Count} string operands name nothing: {string.Join( " | ", missing.Take( 10 ) )}" );

		Assert.IsTrue( referenced > 300, $"only {referenced} string operands were checked" );
	}

	/// <summary>
	/// Every script names each variable it declares, which is what proves the tail of the file is read.
	///
	/// <para>
	/// The names live past the string blob and the original never reads them, so they are easy to
	/// mistake for junk and stop short of. A script that declares twelve variables and names twelve
	/// of them has been read to its last byte.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryScriptNamesTheVariablesItDeclares()
	{
		var unnamed = new List<string>();
		var declared = 0;

		foreach ( var (path, script) in Scripts() )
		{
			declared += script.VariableNames.Count;

			if ( script.VariableNames.Any( name => string.IsNullOrEmpty( name ) || !name.StartsWith( "VAR_", StringComparison.Ordinal ) ) )
				unnamed.Add( Path.GetFileName( path ) );
		}

		Assert.AreEqual( 0, unnamed.Count,
			$"{unnamed.Count} scripts name a variable oddly: {string.Join( " | ", unnamed.Take( 10 ) )}" );

		// The scripts that declare none are real - 98 of them - so this is well under one per script.
		Assert.IsTrue( declared > 2000, $"only {declared} variables were named across the whole corpus" );
	}

	/// <summary>
	/// The instruction set stops where the original's does.
	///
	/// <para>
	/// The dispatcher accepts an opcode only while <c>(word ^ 0x80000000) &lt; 0x6a</c>, and the table
	/// it reads names from runs out at the same place - past it are pointers, not opcodes. So the
	/// last known opcode must have a count and the one after it must have none, which pins the table
	/// to exactly the length it claims without exposing it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheOpcodeTableStopsWhereTheEngineStops()
	{
		Assert.AreEqual( 106, Opcodes.Count, "opcodes the dispatcher accepts" );

		foreach ( var opcode in Enum.GetValues<Opcode>() )
			Assert.IsTrue( opcode.OperandCount() >= 0, $"{opcode} has no operand count" );

		Assert.IsTrue( ((Opcode)(Opcodes.Count - 1)).OperandCount() >= 0, "the last opcode has no operand count" );
		Assert.AreEqual( -1, ((Opcode)Opcodes.Count).OperandCount(), "an opcode past the end was given a count" );
		Assert.AreEqual( 7, Opcode.WALKON.OperandCount(), "WALKON takes the most operands of any instruction" );
	}
}
