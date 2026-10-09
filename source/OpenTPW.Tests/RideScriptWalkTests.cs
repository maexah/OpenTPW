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
/// <b>Ten Lost Kingdom scripts use it, not one</b> - <c>incagod</c> (40 slots), <c>Lookout</c>,
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
	/// bounce tests use, and for the same reason: a hand-written list can name a file that is not
	/// there.
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
	/// sets</b> - thirty-seven declare walk slots and four declare bounce ones - so reading either
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
	/// <c>WALKOFF</c> once <c>GETANIM_CH</c> answers -1, and with no model it answers the register as it
	/// stands - the lane's rider, which the <c>TEST</c> before it left - so no slot here reaches the state
	/// <c>WALKGET</c> collects from.
	/// Hand the script its own players and the round trip completes - see
	/// <see cref="WithItsOwnPlayersTheSideshowLetsARiderBackOff"/>, which is the same shipped script and the
	/// same loop. This one is the no-model case.
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
			script.StepTheWalks( clock );
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
	/// <b>Walked on, walked off, and collected - the half the shipped script reaches only with a model.</b>
	///
	/// <para>
	/// <c>WALKGET</c> answers nought until the rider has finished walking off, then answers their handle
	/// exactly once and frees the slot. <b>Only the "nought first" half is asserted</b>: a <c>WALKGET</c>
	/// that simply returned whoever was in slot one would fail it. The second script is asserted only to
	/// run with nothing unimplemented; what its <c>WALKGET</c>s answer is not checked.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWalkIsCollectedOnlyOnceItHasFinished()
	{
		// WALKON, then WALKGET before anyone has walked off - which must answer nought. The second script
		// below adds WALKOFF and two WALKGETs.
		var file = Build( walkSlots: 2,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.WALKGET ), Var( 0 ),
			Word( Opcode.END ) );

		Assert.AreEqual( 2, file.WalkCapacity, "the builder did not declare the slots" );

		var script = new RideScript( file );

		script.Turn( 0f );

		Assert.AreEqual( 0, script.NotImplemented, "an instruction in this script is unimplemented" );

		// <b>The load-bearing half.</b> A WALKGET that simply handed back whoever was in the first slot
		// would fail this one. Nothing below asserts the collection itself.
		Assert.AreEqual( 0, script.Variables[0],
			"WALKGET answered somebody who had not finished walking off" );

		// Now walk them off and let the stepper carry them to the end.
		var off = new RideScript( Build( walkSlots: 2,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.WALKOFF ), Rider,
			Word( Opcode.WALKGET ), Var( 0 ),
			Word( Opcode.WALKGET ), Var( 1 ),
			Word( Opcode.END ) ) );

		// Two turns, but the body runs to its END on the first, and a script that has ended takes no
		// further turn. Nothing steps the walks here (the park's tick does), so the rider is still walking
		// off when both WALKGETs ask, and both answer nought. Only NotImplemented is asserted.
		off.Turn( 0f );
		off.Turn( 10_000f );

		Assert.AreEqual( 0, off.NotImplemented, "an instruction in the second script is unimplemented" );
	}

	/// <summary>
	/// <b>A finished walk off keeps the leg it walked</b>: the engine writes its state alone (<c>0x00558018</c>), where
	/// arriving on restamps start (<c>0x00557e79</c>). With no model every leg is 100 ms. Restamping the walk off's
	/// start leaves its leg at nought or below, and fails this.
	/// </summary>
	[TestMethod]
	public void AFinishedWalkOffKeepsTheLegItWalked()
	{
		var script = new RideScript( Build( walkSlots: 1,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.END ) ) );

		script.Turn( 0f );
		script.StepTheWalks( 31f );

		Assert.AreEqual( RideScript.WalkState.WalkingOn, script.Walking().Single().State, "arrived before the leg was walked" );

		script.StepTheWalks( 124f );

		var carried = script.Walking().Single();
		Assert.AreEqual( RideScript.WalkState.Carried, carried.State, "not arrived on once the leg was walked" );
		Assert.IsNull( carried.Leg, "a rider being carried walks no leg" );

		var off = new RideScript( Build( walkSlots: 1,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.WALKOFF ), Rider,
			Word( Opcode.END ) ) );

		off.Turn( 0f );
		off.StepTheWalks( 93f );

		Assert.AreEqual( RideScript.WalkState.WalkingOff, off.Walking().Single().State, "walked off before the leg was walked" );

		off.StepTheWalks( 124f );

		var done = off.Walking().Single();
		Assert.AreEqual( RideScript.WalkState.Done, done.State, "not done once the walk off's leg was walked" );
		Assert.AreEqual( 100, done.Leg, "a finished walk off keeps the leg it walked" );
	}

	/// <summary>A script's riders as a park file holds them, with these walk slots and nothing else.</summary>
	private static SavedScript Holding( params SavedWalkSlot[] walk )
		=> new( 1, 0, 0, [], 0, 0, 0, [], 0, 0, 0, 0, 0, Walk: walk );

	/// <summary>
	/// <b><c>WALKGET</c> clears a slot's state and its handle alone</b> (<c>0x0055713f</c>, <c>0x00557149</c>): the
	/// slot keeps its four nodes, its two stamps, its action, its flags and its facing, and is written with them; a
	/// slot no walk has used is nought. Emptying the slot whole fails this.
	/// </summary>
	[TestMethod]
	public void ASlotLetGoKeepsAllButItsWalkerAndItsState()
	{
		var script = new RideScript( Build( walkSlots: 2,
			Word( Opcode.WALKGET ), Var( 0 ),
			Word( Opcode.END ) ) );

		var done = new SavedWalkSlot( 4, 2, 2, 4, 5000, 5700, Rider, 6, (short)RideScript.WalkState.Done, 1, Facing: 4 );

		// The file's moment is the walk off's start, so that stamp is nought on this clock and the slot is stamped still.
		Assert.AreEqual( 1, script.RestoreRiders( Holding( done, default ), reading => reading != 0 ? reading - 5000f : null ) );
		Assert.IsFalse( script.LetGo().Any(), "a slot done is still held" );

		script.Turn( 1000f );

		Assert.IsFalse( script.Walking().Any(), "the slot is free" );
		Assert.AreEqual( (0, 2, 4, 700, 4), script.LetGo().Single(), "and keeps the walk off it was let go from" );

		var written = script.Written( moment => (uint)(moment + 5000f) ).Walk!;

		Assert.AreEqual( done with { Handle = 0, State = 0 }, written[0] );
		Assert.AreEqual( default, written[1], "a slot no walk has used is nought, stamps and all" );
	}

	/// <summary>
	/// A let-go slot read from a park file comes back with its leftovers and goes out again the same, and it holds
	/// nobody. Dropping a free slot at the load fails this.
	/// </summary>
	[TestMethod]
	public void ASlotLetGoComesBackFromAFileAndGoesOutAgain()
	{
		var script = new RideScript( Build( walkSlots: 3, Word( Opcode.END ) ) );
		var left = new SavedWalkSlot( 4, 1, 1, 4, 114453842, 114454942, 0, 6, 0, 1, Facing: 3 );
		var carried = new SavedWalkSlot( 4, 2, 2, 4, 114800000, 114800700, Rider, 6, 2, 1 );

		// The file's moment is 114,803,355 and the load's here 2,000: each reading keeps its distance from it.
		Assert.AreEqual( 1, script.RestoreRiders( Holding( left, carried, default ),
			reading => reading != 0 ? 2000f + (int)(reading - 114803355u) : null ), "a slot let go holds nobody" );

		Assert.AreEqual( (0, 1, 4, 1100, 3), script.LetGo().Single() );
		Assert.AreEqual( 1, script.Walking().Single().Slot );

		var written = script.Written( moment => unchecked(114803355u + (uint)(int)(moment - 2000f)) ).Walk!;

		CollectionAssert.AreEqual( new[] { left, carried, default }, written );
	}

	/// <summary>
	/// <c>WALKON</c> takes the first slot whose state is nought, a let-go one as readily as one never used, and
	/// leaves nothing of the walk before in it.
	/// </summary>
	[TestMethod]
	public void AWalkOnTakesASlotLetGoAndLeavesNothingOfTheWalkBefore()
	{
		var script = new RideScript( Build( walkSlots: 2,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1,
			Word( Opcode.END ) ) );

		script.RestoreRiders( Holding( new SavedWalkSlot( 4, 2, 2, 4, 5000, 5700, 0, 6, 0, 1, Facing: 4 ), default ),
			reading => reading != 0 ? reading : null );

		// A hundred milliseconds before this clock's nought, so the leg is due on it and the slot is stamped still.
		script.Turn( -100f );

		Assert.IsFalse( script.LetGo().Any() );

		var taken = script.Written( moment => (uint)(moment + 9100f) ).Walk!;

		Assert.AreEqual( new SavedWalkSlot( 1, 1, 1, 1, 9000, 9100, Rider, 1, 1, 1 ), taken[0] );
		Assert.AreEqual( default, taken[1] );
	}

	/// <summary>
	/// <b>The whole round trip, on the shipped script: a rider walks on, the lane's clip runs, and the ride
	/// gives him back.</b> This is what the <c>_CH</c> family is for.
	///
	/// <para>
	/// Three things have to be true together. The item's own
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
			script.StepTheWalks( clock );
		}

		Assert.AreEqual( 0, script["VAR_LETMEON"], "the sideshow never took the visitor on" );
		Assert.AreEqual( 1, script["VAR_ONRIDE"], "so it should be counting one aboard" );

		for ( var turn = 0; turn < 600 && script["VAR_LETMEOFF"] == 0; ++turn )
		{
			clock += 600f;
			animations.Advance( (int)clock );
			script.Turn( clock );
			script.StepTheWalks( clock );
		}

		Assert.AreEqual( Rider, script["VAR_LETMEOFF"],
			"the lane's clip finished and GETANIM_CH should have answered -1, letting WALKOFF release the rider" );

		Assert.AreEqual( 0, script["VAR_ONRIDE"], "and the ride should no longer count him aboard" );

		// <b>Not "the whole script is built"</b>, which is a different and larger claim. The cycle does reach
		// gaps - the world-touching handlers report that this fixture handed them no park to act on. What
		// this claims is narrower and is what is asserted: no ANIMATION instruction went
		// unbuilt, so the family the round trip depends on is complete.
		Assert.IsFalse(
			Unimplemented.Summary.Any( gap => gap.What.Contains( "ANIM", StringComparison.Ordinal ) ),
			"an animation instruction went unbuilt during the cycle: "
				+ string.Join( "; ", Unimplemented.Summary.Select( gap => $"{gap.What} x{gap.Times}" ) ) );
	}

	/// <summary>
	/// <b>The declared slots are a ceiling.</b> Offered more visitors than it has lanes, the sideshow
	/// takes as many as it declared and refuses the rest - <c>WALKON</c> walks the array and takes nobody
	/// when every slot is busy. What is asserted is only that at least as many visitors were taken on as
	/// there are lanes; the refusals are not counted.
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
				script.StepTheWalks( clock );
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
	/// two to eight, in order - and the corpus is what confirms it: across all four themes the sixth
	/// operand only ever takes <b>1, 2, 4, 5 or 6</b>, which is a plausible set of kinds, while no other
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

		// <b>The scope is ALL FOUR THEMES, and saying so is the point.</b> Lost Kingdom alone uses
		// [1, 4, 5, 6] - the sweep above walks every theme, and hallow's Ghostshp and space's scitour
		// use 2. A jungle-measured constant asserted over the whole corpus is a test about
		// the corpus's boundary rather than about the engine.
		CollectionAssert.AreEquivalent( new[] { 1, 2, 4, 5, 6 },
			actions.Distinct().OrderBy( v => v ).ToList(),
			$"the sixth operand took [{string.Join( ", ", actions.Distinct().OrderBy( v => v ) )}], "
			+ "which is not the small set of kinds the action should be" );

		Assert.IsTrue( actions.Contains( 4 ),
			"nothing passed 4, which is the value the engine tests for - so this could not tell the "
			+ "action apart from any other operand" );
	}

	/// <summary>A thing's model nodes, stood at a cell and a turn as <see cref="ParkRides"/> stands them.</summary>
	private RideNodes Nodes( string directory, string stem, bool doHeadProcessing, RideAnimations animations,
		int cellX, int cellY, int angle )
	{
		var nodes = RideNodes.Load( directory, stem, _data, doHeadProcessing, animations.AllClips );

		Assert.IsNotNull( nodes, $"{stem}'s model should read" );

		nodes.Place( ParkObjects.OriginFor( cellX, cellY, angle ), angle );

		return nodes;
	}

	/// <summary>
	/// <b>Each lane of the shipped Jungle Spray walks its own two nodes' distance, each way</b>: lanes one and three
	/// 1100 ms, lane two 700 - trunc( 11.655 ) and trunc( 7.161 ) hundreds, the legs the original walked 151 times
	/// in its stock park (docs/exe/ride-operation.md, "How long a leg lasts, and where its ends are"). Three
	/// visitors are handed over one at a time, which fills the lanes in order, and each leg is read while it is
	/// being walked.
	/// </summary>
	[TestMethod]
	public void EachJungleSprayLaneWalksItsOwnNodesDistanceEachWay()
	{
		var animations = RideAnimations.Load( "levels/jungle/sideshow/junspray", "Junspray", _data, JunsprayLanes );

		var script = new RideScript( JunsprayFile() )
		{
			Animations = animations,
			Nodes = Nodes( "levels/jungle/sideshow/junspray", "Junspray", false, animations, 51, 30, 0 )
		};

		var on = new Dictionary<int, int>();
		var off = new Dictionary<int, int>();
		var handed = 0;
		var clock = 0f;

		for ( var turn = 0; turn < 4000 && off.Count < JunsprayLanes; ++turn )
		{
			if ( script["VAR_LETMEON"] == 0 && handed < JunsprayLanes )
				script.Set( "VAR_LETMEON", Rider + ++handed );

			// Whoever came off is taken away, as the park does, so the next can be collected.
			if ( script["VAR_LETMEOFF"] != 0 )
				script.Set( "VAR_LETMEOFF", 0 );

			clock += 248f;
			animations.Advance( (int)clock );
			script.Turn( clock );
			script.StepTheWalks( clock );

			foreach ( var walking in script.Walking() )
			{
				if ( walking.State == RideScript.WalkState.WalkingOn )
					on[walking.To] = walking.Leg ?? -1;
				else if ( walking.State == RideScript.WalkState.WalkingOff )
					off[walking.From] = walking.Leg ?? -1;
			}
		}

		var expected = new Dictionary<int, int> { [1] = 1100, [2] = 700, [3] = 1100 };

		CollectionAssert.AreEquivalent( expected, on, $"walked on: {string.Join( ", ", on )}" );
		CollectionAssert.AreEquivalent( expected, off, $"walked off: {string.Join( ", ", off )}" );
	}

	/// <summary>
	/// <b>Each lane of the Jungle Spray faces as the original's saves hold it</b> (docs/exe/ride-operation.md, "How
	/// long a leg lasts, and where its ends are"): turned 0, walking on 7, 0 and 1, walking off 3, 4 and 5, and
	/// carried 0, the way the lane's node points, taken again at the step after the arrival; turned 270, as the
	/// Jungle Spray of Alexah's Lost Kingdom park stands, carried 6 and walking off lane one 1.
	/// </summary>
	[TestMethod]
	public void EachJungleSprayLaneFacesAsTheOriginalsSavesHoldIt()
	{
		(Dictionary<int, int> On, Dictionary<int, int> Carried, Dictionary<int, int> Arrived, Dictionary<int, int> Off) Faced( int angle )
		{
			var animations = RideAnimations.Load( "levels/jungle/sideshow/junspray", "Junspray", _data, JunsprayLanes );

			var script = new RideScript( JunsprayFile() )
			{
				Animations = animations,
				Nodes = Nodes( "levels/jungle/sideshow/junspray", "Junspray", false, animations, 51, 30, angle )
			};

			var on = new Dictionary<int, int>();
			var arrived = new Dictionary<int, int>();
			var carried = new Dictionary<int, int>();
			var off = new Dictionary<int, int>();
			var handed = 0;
			var clock = 0f;

			for ( var turn = 0; turn < 40000 && off.Count < JunsprayLanes; ++turn )
			{
				if ( script["VAR_LETMEON"] == 0 && handed < JunsprayLanes )
					script.Set( "VAR_LETMEON", Rider + ++handed );

				if ( script["VAR_LETMEOFF"] != 0 )
					script.Set( "VAR_LETMEOFF", 0 );

				clock += 31f;
				animations.Advance( (int)clock );
				script.Turn( clock );
				script.StepTheWalks( clock );

				foreach ( var walking in script.Walking() )
				{
					if ( walking.State == RideScript.WalkState.WalkingOn )
						on[walking.To] = walking.Facing;
					else if ( walking.State == RideScript.WalkState.Carried && !arrived.ContainsKey( walking.To ) )
						arrived[walking.To] = walking.Facing;
					else if ( walking.State == RideScript.WalkState.Carried )
						carried[walking.To] = walking.Facing;
					else if ( walking.State == RideScript.WalkState.WalkingOff )
						off[walking.From] = walking.Facing;
				}
			}

			return (on, carried, arrived, off);
		}

		static string Said( Dictionary<int, int> lanes ) => string.Join( ", ", lanes.OrderBy( lane => lane.Key ) );

		var level = Faced( 0 );

		Assert.AreEqual( "[1, 7], [2, 0], [3, 1]", Said( level.On ), "walking on" );
		Assert.AreEqual( "[1, 7], [2, 0], [3, 1]", Said( level.Arrived ), "the step that finds them arrived leaves the walk's facing" );
		Assert.AreEqual( "[1, 0], [2, 0], [3, 0]", Said( level.Carried ), "carried" );
		Assert.AreEqual( "[1, 3], [2, 4], [3, 5]", Said( level.Off ), "walking off" );

		var turned = Faced( 270 );

		Assert.AreEqual( "[1, 6], [2, 6], [3, 6]", Said( turned.Carried ), "carried, turned 270" );
		Assert.AreEqual( 1, turned.Off[1], "walking off lane one, turned 270" );
	}

	/// <summary>
	/// <b>A node whose file flags carry <c>0x400</c> is read turned about</b> (<c>FUN_00556b90</c>): Wonder Land's Well
	/// Drop has one in the walk space, modelled pointing the other way from its neighbour, and both face nought.
	/// This stands on the listing alone: no shipped script carries a rider on such a node, in any theme.
	/// </summary>
	[TestMethod]
	public void ANodeTurnedAboutFacesTheOtherWay()
	{
		var nodes = RideNodes.Load( "levels/fantasy/rides/welldrop", "welldrop", _data, false, [] );

		Assert.IsNotNull( nodes );
		nodes.Place( ParkObjects.OriginFor( 51, 30, 0 ), 0 );

		var flagged = nodes.Model.Nodes[nodes.Model.FindNode( 2, RideNodes.WalkSpace )];
		var plain = nodes.Model.Nodes[nodes.Model.FindNode( 1, RideNodes.WalkSpace )];

		Assert.AreEqual( (0x400u, 0u), (flagged.Flags & 0x400, plain.Flags & 0x400), "which of the two carries the flag" );
		Assert.AreEqual( NodeEnd.Posed, nodes.FindFacing( 2, RideNodes.WalkSpace, out var about ) );
		Assert.AreEqual( NodeEnd.Posed, nodes.FindFacing( 1, RideNodes.WalkSpace, out var straight ) );
		Assert.AreEqual( (0, 0), (about, straight) );
	}

	/// <summary>
	/// <b>A rider carried under action 4 keeps the facing they walked on with</b>: the Aztec Mayhem's five, 0, 1, 0, 7
	/// and 1 in both of the original's saves of Alexah's Lost Kingdom park, where a Jungle Spray's are turned to their
	/// lane's node.
	/// </summary>
	[TestMethod]
	public void AnAztecMayhemRiderKeepsTheFacingTheyWalkedOnWith()
	{
		var item = "levels/jungle/rides/tvsim";
		var animations = RideAnimations.Load( item, "tvsim", _data );
		var nodes = Nodes( item, "tvsim", true, animations, 40, 22, 0 );

		var words = Enumerable.Range( 1, 5 ).SelectMany( k =>
			new[] { Word( Opcode.WALKON ), Rider + k, 1, k, k, 2, 4, 1 } ).ToList();

		var script = new RideScript( Build( walkSlots: 5, [.. words, Word( Opcode.END )] ) ) { Nodes = nodes };

		script.Turn( 0f );

		CollectionAssert.AreEqual( new[] { 0, 1, 0, 7, 1 }, script.Walking().Select( walking => walking.Facing ).ToArray(), "walking on" );

		script.StepTheWalks( 5000f );
		script.StepTheWalks( 5031f );

		Assert.IsTrue( script.Walking().All( walking => walking.State == RideScript.WalkState.Carried ) );
		CollectionAssert.AreEqual( new[] { 0, 1, 0, 7, 1 }, script.Walking().Select( walking => walking.Facing ).ToArray(), "carried" );
	}

	/// <summary>
	/// <b>The Aztec Mayhem, as the original walked it</b>: bought in the reference install's stock park and watched in
	/// its memory, riders one to five walked on in 1400, 1300, 900, 1000 and 2000 ms, three times, and off in 2000, 900,
	/// 1300, 1700 and 1500, twice. Its <c>WALKON</c> passes action 4, so each head is found in the head space and the walk
	/// off starts from it; its heads ride the seats a clip moves, so each end is counted as taken at rest.
	/// </summary>
	[TestMethod]
	public void TheAztecMayhemsRidersWalkTheLegsTheOriginalWalked()
	{
		var item = "levels/jungle/rides/tvsim";
		var animations = RideAnimations.Load( item, "tvsim", _data );
		var nodes = Nodes( item, "tvsim", true, animations, 40, 22, 0 );

		// WALKON handle, WALK1, head k, off from head k, off to WALK02, action 4, flags 1 - the shipped operands.
		var onWords = Enumerable.Range( 1, 5 ).SelectMany( k =>
			new[] { Word( Opcode.WALKON ), Rider + k, 1, k, k, 2, 4, 1 } ).ToList();

		Unimplemented.Forget();

		var boarding = new RideScript( Build( walkSlots: 5, [.. onWords, Word( Opcode.END )] ) ) { Nodes = nodes };
		boarding.Turn( 0f );

		CollectionAssert.AreEqual( new[] { 1400, 1300, 900, 1000, 2000 },
			boarding.Walking().Select( walking => walking.Leg ).ToArray(), "the walk-on legs, riders one to five" );

		var offWords = Enumerable.Range( 1, 5 ).SelectMany( k => new[] { Word( Opcode.WALKOFF ), Rider + k } );

		var leaving = new RideScript( Build( walkSlots: 5, [.. onWords, .. offWords, Word( Opcode.END )] ) ) { Nodes = nodes };
		leaving.Turn( 0f );

		CollectionAssert.AreEqual( new[] { 2000, 900, 1300, 1700, 1500 },
			leaving.Walking().Select( walking => walking.Leg ).ToArray(), "the walk-off legs, riders one to five" );

		Assert.AreEqual( 15, Unimplemented.Summary.Single( gap => gap.What == "WALK_LEG_REST_POSE" ).Times,
			"every head end, five walking on in each script and five walking off, is taken at rest and counted" );
	}

	/// <summary>
	/// <b>With no model, every leg is the shortest, counted</b>: the engine has no walking script without a model - its
	/// stepper reads the model unguarded - so this is OpenTPW's own fallback. <b>A node no record carries is counted</b>,
	/// and its leg is the shortest too rather than whatever the engine's stale buffer held.
	/// </summary>
	[TestMethod]
	public void NoModelOrAMissedNodeWalksTheShortestLeg()
	{
		var words = new[] { Word( Opcode.WALKON ), Rider, 1, 99, 99, 2, 4, 1, Word( Opcode.END ) };

		Unimplemented.Forget();

		var modelless = new RideScript( Build( walkSlots: 1, words ) );
		modelless.Turn( 0f );

		Assert.AreEqual( 100, modelless.Walking().Single().Leg, "a script with no model walks every leg in 100 ms" );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "WALK_NODES_NO_MODEL" ).Times,
			"and the missing model is counted" );

		Unimplemented.Forget();

		var animations = RideAnimations.Load( "levels/jungle/rides/tvsim", "tvsim", _data );

		var missed = new RideScript( Build( walkSlots: 1, words ) )
		{
			Nodes = Nodes( "levels/jungle/rides/tvsim", "tvsim", true, animations, 40, 22, 0 )
		};

		missed.Turn( 0f );

		Assert.AreEqual( 100, missed.Walking().Single().Leg, "a missed head leaves the shortest leg" );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( gap => gap.What == "WALK_NODE_MISS" ).Times,
			"and the miss is counted" );

		// And a leg under a unit, here from WALK1 to itself, is the shortest rather than nought.
		var standing = new RideScript( Build( walkSlots: 1,
			Word( Opcode.WALKON ), Rider, 1, 1, 1, 1, 1, 1, Word( Opcode.END ) ) ) { Nodes = missed.Nodes };

		standing.Turn( 0f );

		Assert.AreEqual( 100, standing.Walking().Single().Leg, "a walk of nought units lasts 100 ms" );
	}

	/// <summary>
	/// <b>A leg is measured between the nodes as they stand in the world, as floats</b>, not in the model: hallow's
	/// Rat Race walks from <c>Head16</c> to <c>Head17</c>, 9.9999995 apart in its own file - one float step short of
	/// ten, which truncates to 900 - and 10 apart once both stand at a placement, where the stored floats round the
	/// difference away, which gives 1000. The 1000 is the engine's arithmetic worked through at (51, 30), not a leg
	/// measured in the original; it is the one shipped leg the two ways part on away from the map's edge.
	/// </summary>
	[TestMethod]
	public void ALegIsMeasuredBetweenTheNodesAsTheyStandInTheWorld()
	{
		var item = "levels/hallow/rides/ratrace";
		var animations = RideAnimations.Load( item, "ratrace", _data );

		var script = new RideScript( Build( walkSlots: 1,
			Word( Opcode.WALKON ), Rider, 1, 2, 3, 4, 1, 1, Word( Opcode.END ) ) )
		{
			Nodes = Nodes( item, "ratrace", false, animations, 51, 30, 0 )
		};

		script.Turn( 0f );

		Assert.AreEqual( 1000, script.Walking().Single().Leg, "the Rat Race's walk on at (51, 30)" );
	}

	/// <summary>
	/// <b>A walk off starts from its own node, not from the head</b>: the Inca Totem walks rider k on from
	/// <c>position01</c> to head k, 12.482 to 8.152 units by head, and every rider off from <c>head13</c> to
	/// <c>position02</c>, exactly one unit apart - so 1200, 1000 or 800 on and 100 off. Its heads ride the cart a clip
	/// moves and are counted at rest; <c>head13</c> hangs from the ground and is not.
	/// </summary>
	[TestMethod]
	public void TheTotemWalksOffFromItsOwnNodeNotTheHead()
	{
		var item = "levels/jungle/rides/totem";
		var animations = RideAnimations.Load( item, "Totem", _data );
		var nodes = Nodes( item, "Totem", true, animations, 40, 22, 0 );

		// WALKON handle, position01, head k, off from head13, off to position02, action 4, flags 1 - the shipped operands.
		var onWords = Enumerable.Range( 1, 12 ).SelectMany( k =>
			new[] { Word( Opcode.WALKON ), Rider + k, 1, k, 13, 2, 4, 1 } ).ToList();
		var offWords = Enumerable.Range( 1, 12 ).SelectMany( k => new[] { Word( Opcode.WALKOFF ), Rider + k } );

		Unimplemented.Forget();

		var boarding = new RideScript( Build( walkSlots: 12, [.. onWords, Word( Opcode.END )] ) ) { Nodes = nodes };
		boarding.Turn( 0f );

		CollectionAssert.AreEqual( new int?[] { 1200, 1200, 1200, 1200, 1000, 1000, 1000, 1000, 800, 800, 800, 800 },
			boarding.Walking().Select( walking => walking.Leg ).ToArray(), "the walk-on legs, riders one to twelve" );

		var leaving = new RideScript( Build( walkSlots: 12, [.. onWords, .. offWords, Word( Opcode.END )] ) ) { Nodes = nodes };
		leaving.Turn( 0f );

		Assert.IsTrue( leaving.Walking().All( walking => walking.Leg == 100 ), "every walk off is one unit, 100 ms" );
		Assert.AreEqual( 24, Unimplemented.Summary.Single( gap => gap.What == "WALK_LEG_REST_POSE" ).Times,
			"only the heads are counted at rest: twelve walks on in each script, and no walk off" );
	}
}
