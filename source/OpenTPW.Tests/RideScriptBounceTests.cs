using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The <c>BOUNCE</c> family: riding, for the rides that carry people on the ride itself.
///
/// <para>
/// <b>Exactly one Lost Kingdom script uses any of it</b> - <c>Bouncy.RSE</c>, the Belly Bounce - which
/// is also the only one that declares slots for it. That one-to-one agreement is the strongest evidence
/// the header field was read correctly, so it is a test here rather than a remark.
/// </para>
///
/// <para>
/// These drive the shipped script rather than calling the interpreter's own helpers. A test that reached
/// past <c>Turn</c> could prove the walk and never prove that any instruction reaches it.
/// </para>
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class RideScriptBounceTests
{
	/// <summary>How many slots <c>Bouncy.RSE</c> declares, read out of its header by hand.</summary>
	private const int BouncySlots = 10;

	/// <summary>The rider's handle. Any non-zero value does - the engine only tests it against nought.</summary>
	private const int Rider = 42;

	/// <summary>
	/// Inside <c>BounceWindow</c>: a rider this many milliseconds past a whole second may leave, because
	/// 150/200 is nought. <b>Under a divisor of 100 it could not</b>, which is what makes this the
	/// number that pins the decode rather than merely exercising it.
	/// </summary>
	private const int InsideTheWindow = 150;

	/// <summary>
	/// Outside it: 300/200 is one. The control for <see cref="InsideTheWindow"/>, and deliberately a
	/// value that fails under either divisor - it is there to show the window exists at all, while its
	/// partner shows how wide it is.
	/// </summary>
	private const int OutsideTheWindow = 300;

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
	/// Every ride script the game ships, found by walking rather than by naming. An archive is addressed
	/// by whatever spelling the file system hands back, and writing those out by hand has produced a
	/// missing file more than once - which fails as a broken test rather than as a wrong answer.
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

	/// <summary>
	/// <b>Lost Kingdom's</b> Belly Bounce, and the theme is not decoration.
	///
	/// <para>
	/// <b>Two themes ship a file of this name and they do not agree</b>: jungle's declares ten slots and
	/// sets its base to 8, space's declares twelve and sets 16. Taking the first match the walk happened
	/// to reach made a jungle-measured constant fail against a different park's file, which is a test
	/// measuring the corpus rather than the engine. Lost Kingdom is the scope, so Lost Kingdom is what
	/// this asks for.
	/// </para>
	/// <para>
	/// Each theme has exactly one ride that bounces, and all four differ: jungle and space call theirs
	/// <c>Bouncy.RSE</c>, hallow's is <c>Brainb.RSE</c> and fantasy's is <c>Jelly.RSE</c> - which is the
	/// only script in the game that uses <c>BOUNCESETNODE</c>.
	/// </para>
	/// </summary>
	private RideScriptFile BouncyFile()
	{
		foreach ( var (path, file) in EveryScript() )
		{
			if ( path.Contains( "/jungle/", StringComparison.OrdinalIgnoreCase )
				&& Path.GetFileName( path ).Equals( "Bouncy.RSE", StringComparison.OrdinalIgnoreCase ) )
				return file;
		}

		Assert.Fail( "Lost Kingdom's Bouncy.RSE was not found by the walk" );

		return null!;
	}

	/// <summary>
	/// Runs the script until it takes the waiting rider aboard, and answers the clock reading of the turn
	/// it did so on - which is the reading the engine stamps into the slot, so every later deadline is
	/// measured from it.
	///
	/// <para>
	/// <b>Clearing <c>VAR_LETMEON</c> is how the script says it has dealt with the rider, not that it had
	/// room.</b> The script copies nought over the slot whether the bounce took or not, so a caller that
	/// needs somebody actually aboard has to ask <c>BOUNCING</c> as well.
	/// </para>
	/// </summary>
	private static float TakeARiderAboard( RideScript script, int capacity, int durationSeconds )
	{
		Assert.IsTrue( script.Set( "VAR_CAPACITY", capacity ), "Bouncy does not declare VAR_CAPACITY" );
		Assert.IsTrue( script.Set( "VAR_DURATION", durationSeconds ), "Bouncy does not declare VAR_DURATION" );
		Assert.IsTrue( script.Set( "VAR_LETMEON", Rider ), "Bouncy does not declare VAR_LETMEON" );

		var clock = 0f;

		for ( int turn = 0; turn < 400; ++turn )
		{
			// Bouncy's loop holds on WAIT 500, so the clock has to move or it would sit there for ever.
			clock += 600f;

			script.Turn( clock );

			if ( script["VAR_LETMEON"] == 0 )
				return clock;
		}

		Assert.Fail( "Bouncy never took the rider aboard" );

		return 0f;
	}

	/// <summary>
	/// The scripts that declare bounce slots are exactly the scripts that bounce anybody.
	///
	/// <para>
	/// This is what identified the header word at <c>0x18</c> in the first place, and it is worth keeping
	/// as a test because it fails loudly if the offset is ever read as one of its neighbours: the fields
	/// on either side are the limbo and walk-on counts, whose non-zero scripts are entirely different
	/// sets. A wrong offset would not give nonsense, it would give somebody else's answer.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OnlyTheScriptsThatBounceDeclareBounceSlots()
	{
		var declared = new List<string>();
		var bouncing = new List<string>();
		var seen = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			++seen;

			var name = Path.GetFileName( path );

			if ( file.BounceCapacity > 0 )
				declared.Add( $"{name}({file.BounceCapacity})" );

			if ( file.Instructions.Any( instruction => instruction.Opcode == Opcode.BOUNCE ) )
				bouncing.Add( name );
		}

		Assert.IsTrue( seen > 100, $"only {seen} scripts were walked, so this proved nothing" );

		CollectionAssert.AreEquivalent(
			bouncing.Select( name => name.ToLowerInvariant() ).ToList(),
			declared.Select( entry => entry[..entry.IndexOf( '(' )].ToLowerInvariant() ).ToList(),
			$"declared [{string.Join( ", ", declared )}] but bounced [{string.Join( ", ", bouncing )}]" );

		Assert.AreNotEqual( 0, declared.Count, "no script declared any slots, so the field read as nought throughout" );
	}

	/// <summary>
	/// A rider handed to the Belly Bounce is taken aboard and counted.
	///
	/// <para>
	/// <c>BOUNCING</c> is how the script sees its own riders, and Bouncy writes it into
	/// <c>VAR_RUNNING</c> at the top of every pass, so reading that variable back is reading the tally
	/// through the instruction rather than around it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ARiderHandedToTheBellyBounceIsTakenAboardAndCounted()
	{
		var file = BouncyFile();

		Assert.AreEqual( BouncySlots, file.BounceCapacity, "Bouncy's declared slot count changed" );

		var script = new RideScript( file );
		var clock = TakeARiderAboard( script, capacity: 4, durationSeconds: 2 );

		// One more pass so the loop comes back round to BOUNCING VAR_RUNNING at word 20.
		script.Turn( clock + 600f );

		Assert.AreEqual( 1, script["VAR_RUNNING"], "the ride does not report anybody bouncing" );
		Assert.AreEqual( 0, script["VAR_LETMEOFF"], "somebody came off before their time was up" );
		Assert.IsTrue( script.Running, "the script stopped" );
	}

	/// <summary>
	/// <b>Which node the ride is carrying a rider on, which is what the drawing needs.</b> Alexah found
	/// the children never appear on the ride: their sprite stays at the front of the queue for the whole
	/// ride. Nothing in the engine moves a rider either - a rider's position legitimately stays where
	/// they queued and the DRAWING puts them on a node of the ride's own model - so the slot the
	/// <c>BOUNCE</c> instruction filled in is the only thing that knows where they are.
	/// </summary>
	/// <remarks>
	/// <b><c>VAR_RUNNING</c> is asserted first, and the helper's own remarks are why.</b> Clearing
	/// <c>VAR_LETMEON</c> says the script DEALT with the rider, not that it had room for them, so a test
	/// that went straight to the slot could pass against a ride that had refused them and prove nothing.
	/// </remarks>
	[TestMethod]
	public void TheRideSaysWhichNodeItIsCarryingARiderOn()
	{
		var script = new RideScript( BouncyFile() );
		var clock = TakeARiderAboard( script, capacity: 4, durationSeconds: 2 );

		script.Turn( clock + 600f );

		Assert.AreEqual( 1, script["VAR_RUNNING"], "nobody is actually aboard, so the rest proves nothing" );

		Assert.IsTrue( script.TryBounceNode( Rider, out var node ),
			"the ride is carrying the rider and cannot say where" );

		// Lost Kingdom's Bouncy never sets a node base - it calls BOUNCESETBASE, which is the field
		// nothing in the family reads - so a rider's node here is their slot index, one of the ten.
		Assert.IsTrue( node is >= 0 and < BouncySlots,
			$"node {node} is outside the ten slots the ride declares" );

		// Anti-vacuity: it answers about THIS rider rather than about anybody at all.
		Assert.IsFalse( script.TryBounceNode( Rider + 1, out _ ), "a stranger is not aboard" );
		Assert.IsFalse( script.TryBounceNode( 0, out _ ), "and nought is not a rider, it is an empty slot" );
	}

	/// <summary>
	/// A rider comes off inside the window and not outside it - the <c>/ 200</c> the two handlers do.
	///
	/// <para>
	/// <b>The pair is the point.</b> Both probes are past the rider's duration, so the duration cannot be
	/// what separates them; the only difference is where the clock sits inside its second. The one at 150
	/// releases only because the divisor is 200 - under the far more common <c>/ 100</c> spelling it
	/// would be refused exactly like its partner, and this test would fail.
	/// </para>
	/// <para>
	/// Each probe gets its own script, and the clock is held still across the turns it is given. Letting
	/// it run on would sample a different offset every pass and eventually release under any divisor,
	/// which would make both halves pass and prove neither.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ARiderComesOffInsideTheWindowAndNotOutsideIt()
	{
		const int duration = 2;

		Assert.AreEqual( 0, Dismissed( duration, OutsideTheWindow ),
			$"a rider {OutsideTheWindow}ms into the second came off, so no window is being applied" );

		Assert.AreEqual( Rider, Dismissed( duration, InsideTheWindow ),
			$"a rider {InsideTheWindow}ms into the second did not come off, which is the /200 window" );
	}

	/// <summary>
	/// Puts one rider aboard, then asks the script to let people off at a clock reading a fixed distance
	/// past the end of their ride, and answers whoever the script reported.
	/// </summary>
	private int Dismissed( int durationSeconds, int millisecondsIntoTheSecond )
	{
		var script = new RideScript( BouncyFile() );
		var aboard = TakeARiderAboard( script, capacity: 4, durationSeconds: durationSeconds );

		// Strictly past the expiry - the engine's compare is a signed JGE that skips a rider whose
		// deadline has not gone by - and then the offset under test within that second.
		var probe = aboard + (durationSeconds * 1000f) + millisecondsIntoTheSecond;

		for ( int turn = 0; turn < 5; ++turn )
		{
			script.Turn( probe );

			if ( script["VAR_LETMEOFF"] != 0 )
				break;
		}

		return script["VAR_LETMEOFF"];
	}

	/// <summary>
	/// The declared slots are a ceiling the ride's capacity cannot raise.
	///
	/// <para>
	/// <c>BOUNCE</c> never consults <c>VAR_CAPACITY</c> - it walks the array and refuses when it finds no
	/// free slot - so a ride told it may carry more people than it has slots for still stops at the
	/// slots. Bouncy gates itself on the variable first, which is why this needs a capacity above the
	/// array size to reach the refusal at all.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheDeclaredSlotsAreACeilingCapacityCannotRaise()
	{
		var file = BouncyFile();
		var slots = file.BounceCapacity;
		var script = new RideScript( file );

		Assert.AreEqual( BouncySlots, slots, "Lost Kingdom's Bouncy declares a different number of slots" );
		Assert.IsTrue( script.Set( "VAR_CAPACITY", slots * 2 ), "Bouncy does not declare VAR_CAPACITY" );
		Assert.IsTrue( script.Set( "VAR_DURATION", 600 ), "Bouncy does not declare VAR_DURATION" );

		var clock = 0f;

		// Far more passes than riders, so the array fills and then keeps being offered people.
		for ( int rider = 1; rider <= slots * 3; ++rider )
		{
			script.Set( "VAR_LETMEON", rider );

			for ( int turn = 0; turn < 40 && script["VAR_LETMEON"] != 0; ++turn )
			{
				clock += 600f;
				script.Turn( clock );
			}
		}

		clock += 600f;
		script.Turn( clock );

		Assert.AreEqual( slots, script["VAR_RUNNING"],
			"the ride held a different number of riders than it has slots" );
	}

	/// <summary>
	/// Every <c>BOUNCESETNODE</c> in the game passes a literal, which is what makes its raw operand safe.
	///
	/// <para>
	/// Its handler has no variable-tag test at all, so a variable operand would be stored as its tagged
	/// word rather than as its value. That is reproduced rather than corrected, and this is the test that
	/// says the difference cannot be observed: <b>one script in the whole game uses the instruction</b> -
	/// <c>Jelly.RSE</c>, and not in Lost Kingdom - and it passes a literal 3.
	/// </para>
	/// <para>
	/// This began life asserting that <i>nothing</i> used it, which was written from Lost Kingdom's
	/// twenty-two scripts and was false of the other three themes. The walk found Jelly immediately. The
	/// assertion that matters was never the count anyway - it is the operand kind, because that is the
	/// only thing the raw store can get wrong.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EverySetBounceNodePassesALiteral()
	{
		var users = new List<string>();
		var tagged = new List<string>();
		var seen = 0;

		foreach ( var (path, file) in EveryScript() )
		{
			if ( !file.IsValid )
				continue;

			++seen;

			foreach ( var instruction in file.Instructions )
			{
				if ( instruction.Opcode != Opcode.BOUNCESETNODE )
					continue;

				var name = Path.GetFileName( path );

				users.Add( name );

				if ( instruction.Operands.Count == 0 || instruction.Operands[0].Kind != RideOperandKind.Literal )
					tagged.Add( $"{name}@{instruction.Address}" );
			}
		}

		Assert.IsTrue( seen > 100, $"only {seen} scripts were walked, so this proved nothing" );
		Assert.AreNotEqual( 0, users.Count, "nothing used BOUNCESETNODE, so the operand kind was never examined" );
		Assert.AreEqual( 0, tagged.Count,
			$"BOUNCESETNODE was given a non-literal at [{string.Join( ", ", tagged )}], "
			+ "so the raw store is now observable and has to be looked at again" );
	}
}
