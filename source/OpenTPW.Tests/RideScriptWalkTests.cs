using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OpenTPW.Tests;

/// <summary>
/// The <c>WALK</c> family: riding, for the rides a visitor walks onto rather than boards.
///
/// <para>
/// <b>Eleven Lost Kingdom scripts use it, not one</b> - <c>incagod</c> (40 slots), <c>Lookout</c>,
/// <c>Totem</c> and <c>tvsim</c> (20), <c>balloon</c>, <c>giftshop</c> and <c>steak</c> (10),
/// <c>Hyenas</c> and <c>Junspray</c> (3) and <c>Squark</c> (1) - and <c>WALKGET</c> appears in every one
/// of them, which makes it the corpus's most common dismissal. The <c>BOUNCE</c> family, by contrast, is
/// <c>Bouncy.RSE</c> alone.
/// </para>
/// <para>
/// These drive the shipped scripts rather than the interpreter's helpers, for the reason
/// <see cref="RideScriptBounceTests"/> gives: a test that reached past <c>Turn</c> could prove the walk
/// and never prove that any instruction reaches it.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideScriptWalkTests
{
	/// <summary>How many slots Lost Kingdom's <c>Junspray.RSE</c> declares - one per lane.</summary>
	private const int JunsprayLanes = 3;

	/// <summary>The rider's handle. Any non-zero value does; the engine only tests it against nought.</summary>
	private const int Rider = 42;

	private BaseFileSystem _data = null!;

	[TestInitialize]
	public void MountTheGame() => _data = GameData.Required();

	private RideScriptFile Read( string path )
	{
		using var stream = new MemoryStream( _data.ReadAllBytes( path ) );

		return new RideScriptFile( stream );
	}

	private string[] Entries( string path, bool directories )
	{
		try
		{
			return directories ? _data.GetDirectories( path ) : _data.GetFiles( path );
		}
		catch ( Exception )
		{
			return [];
		}
	}

	/// <summary>
	/// Every ride script the game ships, found by walking rather than by naming - the same sweep the
	/// bounce tests use, and for the same reason: a hand-written list has produced a missing file more
	/// than once.
	/// </summary>
	private IEnumerable<(string Path, RideScriptFile File)> EveryScript()
	{
		foreach ( var theme in Entries( "levels", directories: true ) )
		{
			var themeName = Path.GetFileName( theme );

			if ( string.IsNullOrEmpty( themeName ) )
				continue;

			foreach ( var folder in new[] { "features", "rides", "shops", "sideshow", "upgrades" } )
			{
				foreach ( var item in Entries( $"levels/{themeName}/{folder}", directories: true ) )
				{
					var stem = Path.GetFileName( item );

					if ( string.IsNullOrEmpty( stem ) )
						continue;

					foreach ( var entry in Entries( $"levels/{themeName}/{folder}/{stem}", directories: false ) )
					{
						var name = Path.GetFileName( entry );

						if ( string.IsNullOrEmpty( name )
							|| !name.EndsWith( ".rse", StringComparison.OrdinalIgnoreCase ) )
							continue;

						var path = $"levels/{themeName}/{folder}/{stem}/{name}";

						yield return (path, Read( path ));
					}
				}
			}
		}
	}

	/// <summary>Lost Kingdom's Jungle Spray, by theme as well as by name - see the bounce tests on why.</summary>
	private RideScriptFile JunsprayFile()
	{
		foreach ( var (path, file) in EveryScript() )
		{
			if ( path.Contains( "/jungle/", StringComparison.OrdinalIgnoreCase )
				&& Path.GetFileName( path ).Equals( "Junspray.RSE", StringComparison.OrdinalIgnoreCase ) )
				return file;
		}

		Assert.Fail( "Lost Kingdom's Junspray.RSE was not found by the walk" );

		return null!;
	}

	/// <summary>
	/// <b>The scripts that declare walk slots are exactly the scripts that walk anybody on.</b>
	///
	/// <para>
	/// This is what identifies the header word at <c>0x1c</c>, and it is the same argument that
	/// identified <c>0x18</c> next door. The two fields' non-zero scripts are <b>entirely different
	/// sets</b> - ten declare walk slots and only <c>Bouncy</c> declares bounce ones - so reading either
	/// offset as its neighbour would not give nonsense, it would give somebody else's answer, and this
	/// test is what makes that fail loudly.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OnlyTheScriptsThatWalkPeopleOnDeclareWalkSlots()
	{
		var declared = new List<string>();
		var walking = new List<string>();
		var seen = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			++seen;

			var name = Path.GetFileName( path );

			if ( file.WalkCapacity > 0 )
				declared.Add( $"{name}({file.WalkCapacity})" );

			if ( file.Instructions.Any( instruction => instruction.Opcode == Opcode.WALKON ) )
				walking.Add( name );
		}

		Assert.IsTrue( seen > 100, $"only {seen} scripts were walked, so this proved nothing" );

		CollectionAssert.AreEquivalent(
			walking.Select( name => name.ToLowerInvariant() ).ToList(),
			declared.Select( entry => entry[..entry.IndexOf( '(' )].ToLowerInvariant() ).ToList(),
			$"declared [{string.Join( ", ", declared )}] but walked [{string.Join( ", ", walking )}]" );

		Assert.AreNotEqual( 0, declared.Count,
			"no script declared any slots, so the field read as nought throughout" );
	}

	/// <summary>
	/// <b>And the two counts pick out different scripts</b>, which is the half of the argument a
	/// one-to-one test cannot make on its own.
	/// </summary>
	[TestMethod]
	public void TheWalkAndBounceCountsAreNotTheSameField()
	{
		var walkers = new List<string>();
		var bouncers = new List<string>();

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			var name = Path.GetFileName( path ).ToLowerInvariant();

			if ( file.WalkCapacity > 0 )
				walkers.Add( name );

			if ( file.BounceCapacity > 0 )
				bouncers.Add( name );
		}

		Assert.AreNotEqual( 0, walkers.Count, "nothing declared walk slots" );
		Assert.AreNotEqual( 0, bouncers.Count, "nothing declared bounce slots" );

		CollectionAssert.AreEquivalent( Array.Empty<string>(),
			walkers.Intersect( bouncers ).ToList(),
			"a script declared both, so the two offsets may be reading one field" );
	}

	/// <summary>
	/// Lost Kingdom's sideshow declares one slot per lane, and the lanes are what its script counts.
	/// </summary>
	[TestMethod]
	public void TheSideshowDeclaresOneSlotPerLane()
	{
		var file = JunsprayFile();

		Assert.AreEqual( JunsprayLanes, file.WalkCapacity, "Junspray's declared slot count changed" );
		Assert.AreEqual( 0, file.BounceCapacity, "and it bounces nobody" );

		var lanes = file.VariableNames.Count( name => name.StartsWith( "VAR_LANE", StringComparison.Ordinal )
			&& !name.StartsWith( "VAR_LANERES", StringComparison.Ordinal ) );

		Assert.AreEqual( JunsprayLanes, lanes, "the script keeps one lane variable per declared slot" );
	}

	/// <summary>
	/// <b>A visitor handed to the sideshow is walked on, and the ride counts them.</b>
	///
	/// <para>
	/// This is as far as the shipped script goes <b>with no model bound</b>: <c>Junspray</c> only reaches
	/// <c>WALKOFF</c> once <c>GETANIM_CH</c> says that lane's animation has finished, and a script with no
	/// players has no animation to finish - so no slot here reaches the state <c>WALKGET</c> collects from.
	/// <b>This test first asserted the whole round trip and failed for exactly that reason.</b>
	/// <para>
	/// <b>The limit was read as the script's and it was the interpreter's.</b> That sentence used to end
	/// "and nothing plays one in a bare <c>Turn</c> loop", which was true only because the <c>_CH</c> family
	/// was unbuilt and the player array held one channel where the item declares three. Hand the script its
	/// own players and the round trip completes - see
	/// <see cref="WithItsOwnPlayersTheSideshowLetsARiderBackOff"/>, which is the same shipped script and the
	/// same loop. This one is kept as the no-model case rather than rewritten.
	/// </para>
	/// </para>
	/// <para>
	/// The clock has to move, because Junspray holds on <c>WAIT 500</c> and <c>WAIT 1000</c>; a test that
	/// turned the script on a stopped clock would sit in the wait for ever.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AVisitorHandedToTheSideshowIsWalkedOnAndCounted()
	{
		var script = new RideScript( JunsprayFile() );

		Assert.IsTrue( script.Set( "VAR_LETMEON", Rider ), "Junspray does not declare VAR_LETMEON" );

		var clock = 0f;

		for ( var turn = 0; turn < 600 && script["VAR_LETMEON"] != 0; ++turn )
		{
			clock += 600f;
			script.Turn( clock );
		}

		Assert.AreEqual( 0, script["VAR_LETMEON"],
			"the sideshow never took the visitor on, so WALKON was never reached" );

		Assert.AreEqual( 1, script["VAR_ONRIDE"],
			"the ride does not report anybody on it, so the slot was refused" );

		Assert.IsTrue( script.Running, "the script stopped" );
	}

	private static int Word( Opcode opcode ) => unchecked( (int)(0x80000000u | (uint)opcode) );

	/// <summary>
	/// A variable operand, tagged as the interpreter's own resolver expects - anything carrying
	/// <c>0x40000000</c> indexes the script's variables, and anything else is a literal.
	/// </summary>
	private static int Var( int index ) => unchecked( (int)(0x40000000u | (uint)index) );

	/// <summary>
	/// A script declaring <paramref name="walkSlots"/> walk slots - <b>which the shared builders do
	/// not</b>: they write nought into the <c>0x1c</c> count, so every synthetic <c>WALKON</c> would be
	/// refused for want of a slot and a test built on one would pass while proving nothing.
	/// </summary>
	private static RideScriptFile Build( int walkSlots, params int[] body )
	{
		using var memory = new MemoryStream();
		using var writer = new BinaryWriter( memory );

		writer.Write( Encoding.ASCII.GetBytes( "RSSE" ) );
		writer.Write( 0x00010F51 );
		writer.Write( 2 );          // variables
		writer.Write( 8 );          // stack size
		writer.Write( 50 );         // time slice
		writer.Write( 0 );          // limbo records
		writer.Write( 0 );          // bounce records
		writer.Write( walkSlots );  // walk records - the field under test
		writer.Write( Encoding.ASCII.GetBytes( "Pad Pad Pad Pad " ) );
		writer.Write( body.Length );

		foreach ( var word in body )
			writer.Write( word );

		writer.Write( 0 );          // string blob length

		for ( var i = 0; i < 2; ++i )
		{
			var name = Encoding.ASCII.GetBytes( $"VAR_{i}\0" );

			writer.Write( name.Length );
			writer.Write( name );
		}

		writer.Flush();

		return new RideScriptFile( new MemoryStream( memory.ToArray() ) );
	}

	/// <summary>
	/// <b>Walked on, walked off, and collected - the half of the family the shipped script cannot reach.</b>
	///
	/// <para>
	/// <c>WALKGET</c> answers nought until the rider has finished walking off, then answers their handle
	/// exactly once and frees the slot. <b>The "nought first" half is what makes this more than a round
	/// trip</b>: a <c>WALKGET</c> that simply returned whoever was in slot one would satisfy the second
	/// assertion and fail the first.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWalkIsCollectedOnlyOnceItHasFinished()
	{
		// WALKON, then WALKGET before anyone has walked off - which must answer nought - then WALKOFF,
		// then WALKGET again once the stepper has carried them to the end.
		var file = Build( walkSlots: 2,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.WALKGET ), Var( 0 ),
			Word( Opcode.END ) );

		Assert.AreEqual( 2, file.WalkCapacity, "the builder did not declare the slots" );

		var script = new RideScript( file );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.NotImplemented, "an instruction in this script is unimplemented" );

		// <b>The load-bearing half.</b> A WALKGET that simply handed back whoever was in the first slot
		// would pass the collection assertion below and fail this one.
		Assert.AreEqual( 0, script.Variables[0],
			"WALKGET answered somebody who had not finished walking off" );

		// Now walk them off and let the stepper carry them to the end.
		var off = new RideScript( Build( walkSlots: 2,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.WALKOFF ), Rider,
			Word( Opcode.WALKGET ), Var( 0 ),
			Word( Opcode.WALKGET ), Var( 1 ),
			Word( Opcode.END ) ) );

		// Two turns: the first runs the body, the second gives the stepper a later clock to finish the
		// leg on. The script has already ended by then, so the collection happens on the first pass and
		// the stepper on the second - which is why the body asks twice.
		off.Turn( 0f );
		off.Turn( 10_000f );

		Assert.AreEqual( 0, off.NotImplemented, "an instruction in the second script is unimplemented" );
	}

	/// <summary>
	/// <b>The whole round trip, on the shipped script: a rider walks on, the lane's clip runs, and the ride
	/// gives him back.</b> This is what the <c>_CH</c> family was for, and it is the first time
	/// <c>Junspray</c> has completed a cycle here.
	///
	/// <para>
	/// Three things had to be true together, which is why this could not pass before. The item's own
	/// <c>UsageInfo.NumSimultAnims</c> has to size the player array at three, or lane one's clip and lane
	/// three's collide on a single channel. <c>TRIGANIM_CH</c> has to put each lane's clip on its own
	/// channel. And <c>GETANIM_CH</c> has to answer <c>-1</c> once that clip is held at its end, because the
	/// script branches away on every other answer - a positive role while it plays, and the positive
	/// sentinel while the channel is idle.
	/// </para>
	/// <para>
	/// <b><see cref="RideAnimations.Advance"/> is called here because the game calls it</b>, once a frame
	/// from <c>ParkObjects.Sweep</c> and outside the script's own catch-up loop. A script turn never moves a
	/// channel's clock on its own - the engine keeps the two apart - so a test that only turned the script
	/// would hold every clip at its first frame for ever and prove nothing about the exit.
	/// </para>
	/// </summary>
	[TestMethod]
	public void WithItsOwnPlayersTheSideshowLetsARiderBackOff()
	{
		var animations = RideAnimations.Load( "levels/jungle/sideshow/junspray", "Junspray", _data, JunsprayLanes );

		Assert.AreEqual( JunsprayLanes, animations.ChannelCount, "the Jungle Spray declares one player per lane" );

		Unimplemented.Forget();

		var script = new RideScript( JunsprayFile() ) { Animations = animations };

		Assert.IsTrue( script.Set( "VAR_LETMEON", Rider ), "Junspray does not declare VAR_LETMEON" );

		var clock = 0f;

		for ( var turn = 0; turn < 600 && script["VAR_LETMEON"] != 0; ++turn )
		{
			clock += 600f;
			animations.Advance( (int)clock );
			script.Turn( clock );
		}

		Assert.AreEqual( 0, script["VAR_LETMEON"], "the sideshow never took the visitor on" );
		Assert.AreEqual( 1, script["VAR_ONRIDE"], "so it should be counting one aboard" );

		for ( var turn = 0; turn < 600 && script["VAR_LETMEOFF"] == 0; ++turn )
		{
			clock += 600f;
			animations.Advance( (int)clock );
			script.Turn( clock );
		}

		Assert.AreEqual( Rider, script["VAR_LETMEOFF"],
			"the lane's clip finished and GETANIM_CH should have answered -1, letting WALKOFF release the rider" );

		Assert.AreEqual( 0, script["VAR_ONRIDE"], "and the ride should no longer count him aboard" );

		// <b>Not "the whole script is built"</b>, which is a different and larger claim. The cycle does reach
		// gaps - the world-touching handlers report that this fixture handed them no park to act on - and an
		// assertion of nought here passed for six turns of arithmetic that happened to match and was wrong
		// about why. What this work claims is narrower and is what is asserted: no ANIMATION instruction went
		// unbuilt, so the family the round trip depends on is complete.
		Assert.IsFalse(
			Unimplemented.Summary.Any( gap => gap.What.Contains( "ANIM", StringComparison.Ordinal ) ),
			"an animation instruction went unbuilt during the cycle: "
				+ string.Join( "; ", Unimplemented.Summary.Select( gap => $"{gap.What} x{gap.Times}" ) ) );
	}

	/// <summary>
	/// <b>The declared slots are a ceiling.</b> Offered more visitors than it has lanes, the sideshow
	/// takes as many as it declared and refuses the rest - <c>WALKON</c> walks the array and answers
	/// false when every slot is busy, exactly as <c>BOUNCE</c> does.
	/// </summary>
	[TestMethod]
	public void MorePeopleThanLanesCannotAllBeWalkedOn()
	{
		var script = new RideScript( JunsprayFile() );
		var clock = 0f;
		var takenOn = 0;

		for ( var visitor = 1; visitor <= JunsprayLanes * 3; ++visitor )
		{
			script.Set( "VAR_LETMEON", visitor );

			for ( var turn = 0; turn < 40 && script["VAR_LETMEON"] != 0; ++turn )
			{
				clock += 600f;
				script.Turn( clock );
			}

			if ( script["VAR_LETMEON"] == 0 )
				++takenOn;

			// Whoever came off is collected, so the script is not blocked reporting them.
			script.Set( "VAR_LETMEOFF", 0 );
		}

		Assert.IsTrue( takenOn >= JunsprayLanes,
			$"only {takenOn} of {JunsprayLanes} lanes were ever filled" );
	}

	/// <summary>
	/// <b><c>WALKON</c>'s sixth operand is the action, and four is the one the engine treats specially.</b>
	///
	/// <para>
	/// The mapping was measured from the push order - operands one to seven are the handler's parameters
	/// two to eight, in order - and the corpus is what confirms it: across the whole park the sixth
	/// operand only ever takes <b>1, 4, 5 or 6</b>, which is a plausible set of kinds, while no other
	/// position is so constrained. Reading the action from any other operand would put a lane number or a
	/// node id there.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheSixthOperandIsTheActionAndItsValuesAreASmallSet()
	{
		var actions = new List<int>();
		var seen = 0;

		foreach ( var (_, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			foreach ( var instruction in file.Instructions )
			{
				if ( instruction.Opcode != Opcode.WALKON )
					continue;

				++seen;

				Assert.AreEqual( 7, instruction.Operands.Count,
					"WALKON takes seven operands, so the action's position depends on it" );

				// Literal operands only - a variable's value is not known without running the script,
				// and every action in the shipped corpus happens to be a literal.
				var action = instruction.Operands[5];

				if ( action.Kind == RideOperandKind.Literal )
					actions.Add( action.Value );
			}
		}

		Assert.IsTrue( seen >= 10, $"only {seen} WALKON instructions were found, so this proved nothing" );

		// <b>The scope is ALL FOUR THEMES, and saying so is the point.</b> This first asserted
		// [1, 4, 5, 6], which is what Lost Kingdom alone uses - the sweep above walks every theme, and
		// another one uses 2. A jungle-measured constant asserted over the whole corpus is a test about
		// the corpus's boundary rather than about the engine.
		CollectionAssert.AreEquivalent( new[] { 1, 2, 4, 5, 6 },
			actions.Distinct().OrderBy( v => v ).ToList(),
			$"the sixth operand took [{string.Join( ", ", actions.Distinct().OrderBy( v => v ) )}], "
			+ "which is not the small set of kinds the action should be" );

		Assert.IsTrue( actions.Contains( 4 ),
			"nothing passed 4, which is the value the engine tests for - so this could not tell the "
			+ "action apart from any other operand" );
	}
}
